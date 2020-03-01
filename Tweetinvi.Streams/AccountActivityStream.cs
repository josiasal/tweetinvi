﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Tweetinvi.Client.Tools;
using Tweetinvi.Core.Events;
using Tweetinvi.Core.Extensions;
using Tweetinvi.Core.Helpers;
using Tweetinvi.Core.Models.Properties;
using Tweetinvi.Core.Wrappers;
using Tweetinvi.Events;
using Tweetinvi.Models;
using Tweetinvi.Models.DTO;
using Tweetinvi.Streaming;
using Tweetinvi.Streams.Model.AccountActivity;

namespace Tweetinvi.Streams
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class AccountActivityStream : IAccountActivityStream
    {
        private readonly IJObjectStaticWrapper _jObjectWrapper;
        private readonly IJsonObjectConverter _jsonObjectConverter;
        private readonly ITwitterClientFactories _factories;

        private readonly Dictionary<string, Action<string, JObject>> _events;

        public AccountActivityStream(
            IJObjectStaticWrapper jObjectWrapper,
            IJsonObjectConverter jsonObjectConverter,
            ITwitterClientFactories factories)
        {
            _jObjectWrapper = jObjectWrapper;
            _jsonObjectConverter = jsonObjectConverter;
            _factories = factories;
            _events = new Dictionary<string, Action<string, JObject>>();

            InitializeEvents();
        }

        private void InitializeEvents()
        {
            // Tweets
            _events.Add("tweet_create_events", TryRaiseTweetCreatedEvents);
            _events.Add("tweet_delete_events", TryRaiseTweetDeletedEvents);
            _events.Add("favorite_events", TryRaiseTweetFavouritedEvents);

            // User
            _events.Add("follow_events", TryRaiseFollowedEvents);
            _events.Add("block_events", TryRaiseUserBlockedEvents);
            _events.Add("mute_events", TryRaiseUserMutedEvents);

            // App
            _events.Add("user_event", TryRaiseUserEvent);

            // Messages
            _events.Add("direct_message_events", TryRaiseMessageEvent);
            _events.Add("direct_message_indicate_typing_events", TryRaiseIndicateUserIsTypingMessage);
            _events.Add("direct_message_mark_read_events", TryRaiseMessageReadEvent);
        }

        public long AccountUserId { get; set; }

        // Tweets
        public EventHandler<AccountActivityTweetCreatedEventArgs> TweetCreated { get; set; }
        public EventHandler<AccountActivityTweetFavouritedEventArgs> TweetFavourited { get; set; }
        public EventHandler<AccountActivityTweetDeletedEventArgs> TweetDeleted { get; set; }

        // User Events
        public EventHandler<AccountActivityUserFollowedEventArgs> UserFollowed { get; set; }
        public EventHandler<AccountActivityUserUnfollowedEventArgs> UserUnfollowed { get; set; }

        public EventHandler<AccountActivityUserBlockedEventArgs> UserBlocked { get; set; }
        public EventHandler<AccountActivityUserUnblockedEventArgs> UserUnblocked { get; set; }
        public EventHandler<AccountActivityUserMutedEventArgs> UserMuted { get; set; }
        public EventHandler<AccountActivityUserUnmutedEventArgs> UserUnmuted { get; set; }
        public EventHandler<AccountActivityUserRevokedAppPermissionsEventArgs> UserRevokedAppPermissions { get; set; }

        // Messages
        public EventHandler<AccountActivityMessageReceivedEventArgs> MessageReceived { get; set; }
        public EventHandler<AccountActivityMessageSentEventArgs> MessageSent { get; set; }
        public EventHandler<AccountActivityUserIsTypingMessageEventArgs> UserIsTypingMessage { get; set; }
        public EventHandler<AccountActivityUserReadMessageConversationEventArgs> UserReadMessage { get; set; }

        // Others
        public EventHandler<UnsupportedEventReceivedEventArgs> UnsupportedEventReceived { get; set; }
        public EventHandler<EventKnownButNotSupportedReceivedEventArgs> EventKnownButNotFullySupportedReceived { get; set; }
        public EventHandler<AccountActivityEvent> EventReceived { get; set; }
        public EventHandler<UnexpectedExceptionThrownEventArgs> UnexpectedExceptionThrown { get;set;}

        public void WebhookMessageReceived(IWebhookMessage message)
        {
            if (message == null)
            {
                return;
            }

            try
            {
                var json = message.Json;
                var jsonObjectEvent = _jObjectWrapper.GetJobjectFromJson(json);

                var jsonEventChildren = jsonObjectEvent.Children().ToArray();
                var keys = jsonEventChildren.Where(x => x.Path.EndsWith("event") || x.Path.EndsWith("events"));
                var key = keys.SingleOrDefault();

                if (key == null)
                {
                    return;
                }

                this.Raise(EventReceived, new AccountActivityEvent
                {
                    Json = json,
                    AccountUserId = AccountUserId
                });

                var eventName = key.Path;
                if (_events.ContainsKey(eventName))
                {
                    _events[eventName].Invoke(eventName, jsonObjectEvent);
                }
                else
                {
                    this.Raise(UnsupportedEventReceived, new UnsupportedEventReceivedEventArgs(json));
                }
            }
            catch (Exception e)
            {
                this.Raise(UnexpectedExceptionThrown, new UnexpectedExceptionThrownEventArgs(e));
            }
        }

        private void TryRaiseTweetCreatedEvents(string eventName, JObject jsonObjectEvent)
        {
            var json = jsonObjectEvent.ToString();
            var tweetCreatedEvent = jsonObjectEvent[eventName];
            var tweetCreatedEventJson = tweetCreatedEvent.ToString();
            var tweetDTOs = _jsonObjectConverter.DeserializeObject<ITweetDTO[]>(tweetCreatedEventJson);

            tweetDTOs.ForEach(tweetDTO =>
            {
                var tweet = _factories.CreateTweet(tweetDTO);

                var accountActivityEvent = new AccountActivityEvent<ITweet>(tweet)
                {
                    AccountUserId = AccountUserId,
                    EventDate = tweet.CreatedAt,
                    Json = json
                };

                var eventArgs = new AccountActivityTweetCreatedEventArgs(accountActivityEvent);
                this.Raise(TweetCreated, eventArgs);

                if (eventArgs.InResultOf == TweetCreatedRaisedInResultOf.Unknown)
                {
                    this.Raise(EventKnownButNotFullySupportedReceived, new EventKnownButNotSupportedReceivedEventArgs(json, eventArgs));
                }
            });
        }

        private void TryRaiseTweetDeletedEvents(string eventName, JObject jsonObjectEvent)
        {
            var json = jsonObjectEvent.ToString();
            var tweetDeletedEventJToken = jsonObjectEvent[eventName];
            var tweetDeletedEventDTOs = tweetDeletedEventJToken.ToObject<AccountActivityTweetDeletedEventDTO[]>();

            tweetDeletedEventDTOs.ForEach(tweetDeletedEventDTO =>
            {
                var dateOffset = DateTimeOffset.FromUnixTimeMilliseconds(tweetDeletedEventDTO.Timestamp);

                var accountActivityEvent = new AccountActivityEvent<long>(tweetDeletedEventDTO.Status.TweetId)
                {
                    AccountUserId = AccountUserId,
                    EventDate = dateOffset.UtcDateTime,
                    Json = json
                };

                var eventArgs = new AccountActivityTweetDeletedEventArgs(accountActivityEvent, tweetDeletedEventDTO.Status.UserId);

                this.Raise(TweetDeleted, eventArgs);

                if (eventArgs.InResultOf == TweetDeletedRaisedInResultOf.Unknown)
                {
                    this.Raise(EventKnownButNotFullySupportedReceived, new EventKnownButNotSupportedReceivedEventArgs(json, eventArgs));
                }
            });
        }

        private void TryRaiseTweetFavouritedEvents(string eventName, JObject jsonObjectEvent)
        {
            var json = jsonObjectEvent.ToString();
            var favouriteTweetEvent = jsonObjectEvent[eventName];
            var favouritedTweetEventJson = favouriteTweetEvent.ToString();
            var favouriteEventDTOs = _jsonObjectConverter.DeserializeObject<AccountActivityFavouriteEventDTO[]>(favouritedTweetEventJson);

            favouriteEventDTOs.ForEach(favouriteEventDTO =>
            {
                var tweet = _factories.CreateTweet(favouriteEventDTO.FavouritedTweet);
                var user = _factories.CreateUser(favouriteEventDTO.User);

                var accountActivityEvent = new AccountActivityEvent<Tuple<ITweet, IUser>>(new Tuple<ITweet, IUser>(tweet, user))
                {
                    AccountUserId = AccountUserId,
                    EventDate = tweet.CreatedAt,
                    Json = json
                };

                var eventArgs = new AccountActivityTweetFavouritedEventArgs(accountActivityEvent);

                this.Raise(TweetFavourited, eventArgs);

                if (eventArgs.InResultOf == TweetFavouritedRaisedInResultOf.Unknown)
                {
                    this.Raise(EventKnownButNotFullySupportedReceived, new EventKnownButNotSupportedReceivedEventArgs(json, eventArgs));
                }
            });
        }

        private void TryRaiseFollowedEvents(string eventName, JObject jsonObjectEvent)
        {
            var json = jsonObjectEvent.ToString();
            var followEvent = jsonObjectEvent[eventName];
            var followedUsersEvents = ExtractUserToUserEventDTOs(followEvent);

            followedUsersEvents.ForEach(followedUsersEvent =>
            {
                var sourceUser = _factories.CreateUser(followedUsersEvent.Source);
                var targetUser = _factories.CreateUser(followedUsersEvent.Target);

                var timestamp = long.Parse(followedUsersEvent.CreatedTimestamp);
                var dateOffset = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);

                var accountActivityEvent = new AccountActivityEvent<Tuple<IUser, IUser>>(new Tuple<IUser, IUser>(sourceUser, targetUser))
                {
                    AccountUserId = AccountUserId,
                    EventDate = dateOffset.UtcDateTime,
                    Json = json
                };

                if (followedUsersEvent.Type == "follow")
                {
                    var eventArgs = new AccountActivityUserFollowedEventArgs(accountActivityEvent);

                    this.Raise(UserFollowed, eventArgs);

                    if (eventArgs.InResultOf == UserFollowedRaisedInResultOf.Unknown)
                    {
                        this.Raise(EventKnownButNotFullySupportedReceived, new EventKnownButNotSupportedReceivedEventArgs(json, eventArgs));
                    }
                }
                else if (followedUsersEvent.Type == "unfollow")
                {
                    var eventArgs = new AccountActivityUserUnfollowedEventArgs(accountActivityEvent);

                    this.Raise(UserUnfollowed, eventArgs);

                    if (eventArgs.InResultOf == UserUnfollowedRaisedInResultOf.Unknown)
                    {
                        this.Raise(EventKnownButNotFullySupportedReceived, new EventKnownButNotSupportedReceivedEventArgs(json, eventArgs));
                    }
                }
                else
                {
                    this.Raise(UnsupportedEventReceived, new UnsupportedEventReceivedEventArgs(jsonObjectEvent.ToString()));
                }
            });
        }

        private void TryRaiseUserBlockedEvents(string eventName, JObject jsonObjectEvent)
        {
            var json = jsonObjectEvent.ToString();
            var userBlockedEvent = jsonObjectEvent[eventName];
            var blockedEventInfos = ExtractUserToUserEventDTOs(userBlockedEvent);

            blockedEventInfos.ForEach(blockedEventInfo =>
            {
                var sourceUser = _factories.CreateUser(blockedEventInfo.Source);
                var targetUser = _factories.CreateUser(blockedEventInfo.Target);

                var timestamp = long.Parse(blockedEventInfo.CreatedTimestamp);
                var dateOffset = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);

                var accountActivityEvent = new AccountActivityEvent<Tuple<IUser, IUser>>(new Tuple<IUser, IUser>(sourceUser, targetUser))
                {
                    AccountUserId = AccountUserId,
                    EventDate = dateOffset.UtcDateTime,
                    Json = json
                };

                if (blockedEventInfo.Type == "block")
                {
                    var eventArgs = new AccountActivityUserBlockedEventArgs(accountActivityEvent);

                    this.Raise(UserBlocked, eventArgs);

                    if (eventArgs.InResultOf == UserBlockedRaisedInResultOf.Unknown)
                    {
                        this.Raise(EventKnownButNotFullySupportedReceived, new EventKnownButNotSupportedReceivedEventArgs(json, eventArgs));
                    }
                }
                else if (blockedEventInfo.Type == "unblock")
                {
                    var eventArgs = new AccountActivityUserUnblockedEventArgs(accountActivityEvent);

                    this.Raise(UserUnblocked, eventArgs);

                    if (eventArgs.InResultOf == UserUnblockedRaisedInResultOf.Unknown)
                    {
                        this.Raise(EventKnownButNotFullySupportedReceived, new EventKnownButNotSupportedReceivedEventArgs(json, eventArgs));
                    }
                }
                else
                {
                    this.Raise(UnsupportedEventReceived, new UnsupportedEventReceivedEventArgs(json));
                }
            });
        }

        private void TryRaiseUserMutedEvents(string eventName, JObject jsonObjectEvent)
        {
            var json = jsonObjectEvent.ToString();
            var userMutedEvent = jsonObjectEvent[eventName];
            var mutedEventInfos = ExtractUserToUserEventDTOs(userMutedEvent);

            mutedEventInfos.ForEach(mutedEventInfo =>
            {
                var sourceUser = _factories.CreateUser(mutedEventInfo.Source);
                var targetUser = _factories.CreateUser(mutedEventInfo.Target);

                var timestamp = long.Parse(mutedEventInfo.CreatedTimestamp);
                var dateOffset = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);

                var accountActivityEvent = new AccountActivityEvent<Tuple<IUser, IUser>>(new Tuple<IUser, IUser>(sourceUser, targetUser))
                {
                    AccountUserId = AccountUserId,
                    EventDate = dateOffset.UtcDateTime,
                    Json = json
                };

                if (mutedEventInfo.Type == "mute")
                {
                    var eventArgs = new AccountActivityUserMutedEventArgs(accountActivityEvent);
                    this.Raise(UserMuted, eventArgs);

                    if (eventArgs.InResultOf == UserMutedRaisedInResultOf.Unknown)
                    {
                        this.Raise(EventKnownButNotFullySupportedReceived, new EventKnownButNotSupportedReceivedEventArgs(json, eventArgs));
                    }
                }
                else if (mutedEventInfo.Type == "unmute")
                {
                    var eventArgs = new AccountActivityUserUnmutedEventArgs(accountActivityEvent);
                    this.Raise(UserUnmuted, eventArgs);

                    if (eventArgs.InResultOf == UserUnmutedRaisedInResultOf.Unknown)
                    {
                        this.Raise(EventKnownButNotFullySupportedReceived, new EventKnownButNotSupportedReceivedEventArgs(json, eventArgs));
                    }
                }
                else
                {
                    this.Raise(UnsupportedEventReceived, new UnsupportedEventReceivedEventArgs(jsonObjectEvent.ToString()));
                }
            });
        }

        private void TryRaiseUserEvent(string eventName, JObject jsonObjectEvent)
        {
            var json = jsonObjectEvent.ToString();
            var userEvent = jsonObjectEvent[eventName];
            var eventType = userEvent.Children().First().Path;

            if (eventType == "user_event.revoke")
            {
                var userRevokedAppEventDTO = userEvent["revoke"].ToObject<ActivityStreamUserRevokedAppPermissionsDTO>();

                var accountActivityEvent = new AccountActivityEvent()
                {
                    AccountUserId = AccountUserId,
                    EventDate = userRevokedAppEventDTO.DateTime.ToUniversalTime(),
                    Json = json
                };

                var userId = userRevokedAppEventDTO.Source.UserId;
                var appId = userRevokedAppEventDTO.Target.AppId;

                var userRevokedAppEventArgs = new AccountActivityUserRevokedAppPermissionsEventArgs(accountActivityEvent, userId, appId);

                this.Raise(UserRevokedAppPermissions, userRevokedAppEventArgs);

                if (userRevokedAppEventArgs.InResultOf == UserRevokedAppPermissionsInResultOf.Unknown)
                {
                    this.Raise(EventKnownButNotFullySupportedReceived, new EventKnownButNotSupportedReceivedEventArgs(json, userRevokedAppEventArgs));
                }
            }
            else
            {
                this.Raise(UnsupportedEventReceived, new UnsupportedEventReceivedEventArgs(jsonObjectEvent.ToString()));
            }
        }

        private void TryRaiseMessageEvent(string eventName, JObject jsonObjectEvent)
        {
            var json = jsonObjectEvent.ToString();
            var eventInfo = jsonObjectEvent.ToObject<AccountActivityMessageCreatedEventDTO>();

            eventInfo.MessageEvents.ForEach(messageEventDTO =>
            {
                App app = null;

                if (messageEventDTO.MessageCreate.SourceAppId != null)
                {
                    eventInfo.Apps.TryGetValue(messageEventDTO.MessageCreate.SourceAppId.ToString(), out app);
                }

                eventInfo.UsersById.TryGetValue(messageEventDTO.MessageCreate.SenderId.ToString(), out var senderDTO);
                eventInfo.UsersById.TryGetValue(messageEventDTO.MessageCreate.Target.RecipientId.ToString(), out var recipientDTO);

                var sender = _factories.CreateUser(senderDTO);
                var recipient = _factories.CreateUser(recipientDTO);

                var message = _factories.CreateMessage(messageEventDTO, app);

                var accountActivityEvent = new AccountActivityEvent<IMessage>(message)
                {
                    AccountUserId = AccountUserId,
                    EventDate = message.CreatedAt,
                    Json = json
                };

                if (message.SenderId == AccountUserId)
                {
                    var eventArgs = new AccountActivityMessageSentEventArgs(accountActivityEvent, message, sender, recipient, app);
                    this.Raise(MessageSent, eventArgs);

                    if (eventArgs.InResultOf == MessageSentInResultOf.Unknown)
                    {
                        this.Raise(EventKnownButNotFullySupportedReceived, new EventKnownButNotSupportedReceivedEventArgs(json, eventArgs));
                    }
                }
                else if (message.RecipientId == AccountUserId)
                {
                    var eventArgs = new AccountActivityMessageReceivedEventArgs(accountActivityEvent, message, sender, recipient, app);
                    this.Raise(MessageReceived, eventArgs);

                    if (eventArgs.InResultOf == MessageReceivedInResultOf.Unknown)
                    {
                        this.Raise(EventKnownButNotFullySupportedReceived, new EventKnownButNotSupportedReceivedEventArgs(json, eventArgs));
                    }
                }
                else
                {
                    this.Raise(UnsupportedEventReceived, new UnsupportedEventReceivedEventArgs(jsonObjectEvent.ToString()));
                }
            });
        }

        private void TryRaiseIndicateUserIsTypingMessage(string eventName, JObject jsonObjectEvent)
        {
            var json = jsonObjectEvent.ToString();
            var events = _jsonObjectConverter.DeserializeObject<AccountActivityUserIsTypingMessageDTO>(json);

            events.TypingEvents.ForEach(typingEvent =>
            {
                var activityEvent = new AccountActivityEvent
                {
                    AccountUserId = AccountUserId,
                    EventDate = typingEvent.CreatedAt,
                    Json = json
                };

                events.UsersById.TryGetValue(typingEvent.SenderId.ToString(), out var senderDTO);
                events.UsersById.TryGetValue(typingEvent.Target.RecipientId.ToString(), out var recipientDTO);

                var sender = _factories.CreateUser(senderDTO);
                var recipient = _factories.CreateUser(recipientDTO);

                var eventArgs = new AccountActivityUserIsTypingMessageEventArgs(activityEvent, sender, recipient);

                this.Raise(UserIsTypingMessage, eventArgs);

                if (eventArgs.InResultOf == UserIsTypingMessageInResultOf.Unknown)
                {
                    this.Raise(EventKnownButNotFullySupportedReceived, new EventKnownButNotSupportedReceivedEventArgs(json, eventArgs));
                }
            });
        }

        private void TryRaiseMessageReadEvent(string eventName, JObject jsonObjectEvent)
        {
            var json = jsonObjectEvent.ToString();
            var events = _jsonObjectConverter.DeserializeObject<AccountActivityUserReadMessageConversationDTO>(json);

            events.MessageConversationReadEvents.ForEach(messageConversationReadEvent =>
            {
                var activityEvent = new AccountActivityEvent
                {
                    AccountUserId = AccountUserId,
                    EventDate = messageConversationReadEvent.CreatedAt,
                    Json = json
                };

                events.UsersById.TryGetValue(messageConversationReadEvent.SenderId.ToString(), out var senderDTO);
                events.UsersById.TryGetValue(messageConversationReadEvent.Target.RecipientId.ToString(), out var recipientDTO);

                var sender = _factories.CreateUser(senderDTO);
                var recipient = _factories.CreateUser(recipientDTO);

                var eventArgs = new AccountActivityUserReadMessageConversationEventArgs(activityEvent, sender, recipient, messageConversationReadEvent.LastReadEventId);

                this.Raise(UserReadMessage, eventArgs);

                if (eventArgs.InResultOf == UserReadMessageConversationInResultOf.Unknown)
                {
                    this.Raise(EventKnownButNotFullySupportedReceived, new EventKnownButNotSupportedReceivedEventArgs(json, eventArgs));
                }
            });
        }

        private AccountActivityUserToUserEventDTO[] ExtractUserToUserEventDTOs(JToken userToUserEvent)
        {
            var userToUserEventJson = userToUserEvent.ToString();
            return ExtractUserToUserEventDTOs(userToUserEventJson);
        }

        private AccountActivityUserToUserEventDTO[] ExtractUserToUserEventDTOs(string userToUserEventJson)
        {
            var userToUserEventDTO = _jsonObjectConverter.DeserializeObject<AccountActivityUserToUserEventDTO[]>(userToUserEventJson);
            return userToUserEventDTO;
        }
    }
}
