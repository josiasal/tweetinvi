﻿using System.Globalization;
using System.Linq;
using System.Text;
using Tweetinvi.Controllers.Properties;
using Tweetinvi.Controllers.Shared;
using Tweetinvi.Core.Extensions;
using Tweetinvi.Core.QueryGenerators;
using Tweetinvi.Core.QueryValidators;
using Tweetinvi.Models.DTO;
using Tweetinvi.Parameters;

namespace Tweetinvi.Controllers.Tweet
{
    public class TweetQueryGenerator : ITweetQueryGenerator
    {
        private readonly IQueryParameterGenerator _queryParameterGenerator;
        private readonly IUserQueryParameterGenerator _userQueryParameterGenerator;
        private readonly ITweetQueryValidator _tweetQueryValidator;

        public TweetQueryGenerator(
            IQueryParameterGenerator queryParameterGenerator,
            IUserQueryParameterGenerator userQueryParameterGenerator,
            ITweetQueryValidator tweetQueryValidator)
        {
            _queryParameterGenerator = queryParameterGenerator;
            _userQueryParameterGenerator = userQueryParameterGenerator;
            _tweetQueryValidator = tweetQueryValidator;
        }

        // Get Tweet
        public string GetTweetQuery(IGetTweetParameters parameters, TweetMode? tweetMode)
        {
            var query = new StringBuilder(Resources.Tweet_Get);

            query.AddParameterToQuery("id", parameters.Tweet?.Id.ToString() ?? parameters.Tweet?.IdStr);
            query.AddParameterToQuery("include_card_uri", parameters.IncludeCardUri);
            query.AddParameterToQuery("include_entities", parameters.IncludeEntities);
            query.AddParameterToQuery("include_ext_alt_text", parameters.IncludeExtAltText);
            query.AddParameterToQuery("include_my_retweet", parameters.IncludeMyRetweet);
            query.AddParameterToQuery("trim_user", parameters.TrimUser);

            query.AddFormattedParameterToQuery(_queryParameterGenerator.GenerateTweetModeParameter(tweetMode));
            query.AddFormattedParameterToQuery(parameters.FormattedCustomQueryParameters);

            return query.ToString();
        }

        public string GetTweetsQuery(IGetTweetsParameters parameters, TweetMode? tweetMode)
        {
            var query = new StringBuilder(Resources.Tweet_Lookup);

            var validTweetIdentifiers = parameters.Tweets.Where(x => x?.Id != null || !string.IsNullOrEmpty(x?.IdStr));
            var tweetIds = validTweetIdentifiers.Select(x => x.Id?.ToString() ?? x.IdStr);

            query.AddParameterToQuery("id", string.Join(",", tweetIds));
            query.AddParameterToQuery("include_card_uri", parameters.IncludeCardUri);
            query.AddParameterToQuery("include_entities", parameters.IncludeEntities);
            query.AddParameterToQuery("include_ext_alt_text", parameters.IncludeExtAltText);
            query.AddParameterToQuery("trim_user", parameters.TrimUser);

            query.AddFormattedParameterToQuery(_queryParameterGenerator.GenerateTweetModeParameter(tweetMode));
            query.AddFormattedParameterToQuery(parameters.FormattedCustomQueryParameters);

            return query.ToString();
        }

        // Publish Tweet
        public string GetPublishTweetQuery(IPublishTweetParameters parameters, TweetMode? tweetMode)
        {
            var text = parameters.Text;
            var useExtendedTweetMode = tweetMode == null || tweetMode == TweetMode.Extended;

            var quotedTweetUrl = GetQuotedTweetUrl(parameters);
            var attachmentUrl = parameters.QuotedTweetUrl;

            if (quotedTweetUrl != null)
            {
                // if there is a quoted tweet we need to pass the url in the text or attachment url
                // attachment_url is only available under tweetMode
                if (useExtendedTweetMode && attachmentUrl == null)
                {
                    attachmentUrl = quotedTweetUrl;
                }
                else
                {
                    text = text.TrimEnd() + " " + quotedTweetUrl;
                }
            }

            var query = new StringBuilder(Resources.Tweet_Publish);

            query.AddParameterToQuery("status", text);
            query.AddParameterToQuery("auto_populate_reply_metadata", parameters.AutoPopulateReplyMetadata);
            query.AddParameterToQuery("attachment_url", attachmentUrl);
            query.AddParameterToQuery("card_uri", parameters.CardUri);
            query.AddParameterToQuery("display_coordinates", parameters.DisplayExactCoordinates);

            if (parameters.ExcludeReplyUserIds != null)
            {
                query.AddParameterToQuery("exclude_reply_user_ids", string.Join(",", parameters.ExcludeReplyUserIds));
            }

            query.AddParameterToQuery("in_reply_to_status_id", parameters.InReplyToTweet?.Id);
            query.AddParameterToQuery("lat", parameters.Coordinates?.Latitude.ToString(CultureInfo.InvariantCulture));
            query.AddParameterToQuery("long", parameters.Coordinates?.Longitude.ToString(CultureInfo.InvariantCulture));

            if (parameters.MediaIds.Count > 0)
            {
                var mediaIdsParameter = string.Join(",", parameters.MediaIds.Select(x => x.ToString(CultureInfo.InvariantCulture)));
                query.AddParameterToQuery("media_ids", mediaIdsParameter);
            }

            query.AddParameterToQuery("place_id", parameters.PlaceId);
            query.AddParameterToQuery("possibly_sensitive", parameters.PossiblySensitive);
            query.AddParameterToQuery("trim_user", parameters.TrimUser);
            query.AddParameterToQuery("tweet_mode", tweetMode?.ToString().ToLowerInvariant());

            query.AddFormattedParameterToQuery(parameters.FormattedCustomQueryParameters);

            return query.ToString();
        }

        private string GetQuotedTweetUrl(IPublishTweetParameters parameters)
        {
            if (parameters.QuotedTweet?.CreatedBy?.ScreenName == null)
            {
                return null;
            }

            if (parameters.QuotedTweet?.Id == null)
            {
                return null;
            }

            return $"https://twitter.com/{parameters.QuotedTweet.CreatedBy.ScreenName}/status/{parameters.QuotedTweet.Id.Value.ToString(CultureInfo.InvariantCulture)}";
        }
        
        public string GetDestroyTweetQuery(IDestroyTweetParameters parameters, TweetMode? tweetMode)
        {
            var query = new StringBuilder(string.Format(Resources.Tweet_Destroy, _queryParameterGenerator.GenerateTweetIdentifier(parameters.Tweet)));

            query.AddParameterToQuery("trim_user", parameters.TrimUser);

            var tweetModeParameter = _queryParameterGenerator.GenerateTweetModeParameter(tweetMode);
            query.AddFormattedParameterToQuery(tweetModeParameter);
            query.AddFormattedParameterToQuery(parameters.FormattedCustomQueryParameters);

            return query.ToString();
        }
        
        public string GetFavoriteTweetsQuery(IGetFavoriteTweetsParameters parameters, TweetMode? tweetMode)
        {
            var userParameter = _userQueryParameterGenerator.GenerateIdOrScreenNameParameter(parameters.User);
            var query = new StringBuilder(Resources.User_GetFavourites + userParameter);

            query.AddParameterToQuery("include_entities", parameters.IncludeEntities);
            _queryParameterGenerator.AddMinMaxQueryParameters(query, parameters);

            var tweetModeParameter = _queryParameterGenerator.GenerateTweetModeParameter(tweetMode);
            query.AddFormattedParameterToQuery(tweetModeParameter);
            query.AddFormattedParameterToQuery(parameters.FormattedCustomQueryParameters);

            return query.ToString();
        }
        
        public string GetRetweetsQuery(IGetRetweetsParameters parameters, TweetMode? tweetMode)
        {
            var tweetIdentifierValue = parameters.Tweet.Id?.ToString() ?? parameters.Tweet.IdStr;
            var query = new StringBuilder(string.Format(Resources.Tweet_Retweet_GetRetweets, tweetIdentifierValue));
            
            query.AddParameterToQuery("count", parameters.PageSize);
            query.AddParameterToQuery("trim_user", parameters.TrimUser);
            
            query.AddFormattedParameterToQuery(_queryParameterGenerator.GenerateTweetModeParameter(tweetMode));
            query.AddFormattedParameterToQuery(parameters.FormattedCustomQueryParameters);

            return query.ToString();
        }

        public string GetPublishRetweetQuery(IPublishRetweetParameters parameters, TweetMode? tweetMode)
        {
            var tweetIdentifierValue = parameters.Tweet.Id?.ToString() ?? parameters.Tweet.IdStr;
            var query = new StringBuilder(string.Format(Resources.Tweet_Retweet_Publish, tweetIdentifierValue));
            
            query.AddParameterToQuery("trim_user", parameters.TrimUser);
            
            query.AddFormattedParameterToQuery(_queryParameterGenerator.GenerateTweetModeParameter(tweetMode));
            query.AddFormattedParameterToQuery(parameters.FormattedCustomQueryParameters);

            return query.ToString();
        }

        public string GetDestroyRetweetQuery(IDestroyRetweetParameters parameters, TweetMode? tweetMode)
        {
            var tweetIdentifierValue = parameters.Tweet.Id?.ToString() ?? parameters.Tweet.IdStr;
            var query = new StringBuilder(string.Format(Resources.Tweet_DestroyRetweet, tweetIdentifierValue));
            
            query.AddParameterToQuery("trim_user", parameters.TrimUser);
            
            query.AddFormattedParameterToQuery(_queryParameterGenerator.GenerateTweetModeParameter(tweetMode));
            query.AddFormattedParameterToQuery(parameters.FormattedCustomQueryParameters);

            return query.ToString();
        }

        public string GetRetweeterIdsQuery(IGetRetweeterIdsParameters parameters)
        {
            var query = new StringBuilder(Resources.Tweet_GetRetweeters);

            query.AddParameterToQuery("id", parameters.Tweet.Id?.ToString() ?? parameters.Tweet.IdStr);
            query.AddParameterToQuery("cursor", parameters.Cursor);
            query.AddParameterToQuery("count", parameters.PageSize);
            
            query.AddFormattedParameterToQuery(parameters.FormattedCustomQueryParameters);

            return query.ToString();
        }

        // Favorite Tweet
        public string GetFavoriteTweetQuery(ITweetDTO tweetDTO)
        {
            _tweetQueryValidator.ThrowIfTweetCannotBeUsed(tweetDTO);
            return GetFavoriteTweetQuery(tweetDTO.Id);
        }

        public string GetFavoriteTweetQuery(long? tweetId)
        {
            _tweetQueryValidator.ThrowIfTweetCannotBeUsed(tweetId);
            return string.Format(Resources.Tweet_Favorite_Create, tweetId);
        }

        // Unfavourite Tweet
        public string GetUnFavoriteTweetQuery(ITweetDTO tweetDTO)
        {
            _tweetQueryValidator.ThrowIfTweetCannotBeUsed(tweetDTO);
            return GetUnFavoriteTweetQuery(tweetDTO.Id);
        }

        public string GetUnFavoriteTweetQuery(long? tweetId)
        {
            _tweetQueryValidator.ThrowIfTweetCannotBeUsed(tweetId);
            return string.Format(Resources.Tweet_Favorite_Destroy, tweetId);
        }

        // OEmbed Tweet
        public string GetGenerateOEmbedTweetQuery(ITweetDTO tweetDTO)
        {
            _tweetQueryValidator.ThrowIfTweetCannotBeUsed(tweetDTO);
            return GetGenerateOEmbedTweetQuery(tweetDTO.Id);
        }

        public string GetGenerateOEmbedTweetQuery(long? tweetId)
        {
            _tweetQueryValidator.ThrowIfTweetCannotBeUsed(tweetId);
            return string.Format(Resources.Tweet_GenerateOEmbed, tweetId);
        }
    }
}