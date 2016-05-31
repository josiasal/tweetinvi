﻿using System;
using FakeItEasy;
using FakeItEasy.ExtensionSyntax.Full;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testinvi.Helpers;
using Testinvi.SetupHelpers;
using Tweetinvi.Controllers.Messages;
using Tweetinvi.Core;
using Tweetinvi.Core.Interfaces.DTO;
using Tweetinvi.Core.Interfaces.QueryValidators;
using Tweetinvi.Core.Parameters;

namespace Testinvi.TweetinviControllers.MessageTests
{
    [TestClass]
    public class MessageQueryValidatorTests
    {
        private FakeClassBuilder<MessageQueryValidator> _fakeBuilder;
        private Fake<IUserQueryValidator> _fakeUserQueryValidator;

        [TestInitialize]
        public void TestInitialize()
        {
            _fakeBuilder = new FakeClassBuilder<MessageQueryValidator>();
            _fakeUserQueryValidator = _fakeBuilder.GetFake<IUserQueryValidator>();
            _fakeUserQueryValidator.ArrangeIsUserIdValid();
        }

        #region Can Message Be Published

        [TestMethod]
        public void CanMessageBePublished_BasedOnMessageHasBeenPublishedOrDestroyed()
        {
            // Arrange - Act - Assert
            CanMessageBePublished_BasedOnPublishedAndDestroyed(true, true, typeof(ArgumentException));
            CanMessageBePublished_BasedOnPublishedAndDestroyed(true, false, typeof(ArgumentException));
            CanMessageBePublished_BasedOnPublishedAndDestroyed(false, true, typeof(ArgumentException));
            CanMessageBePublished_BasedOnPublishedAndDestroyed(false, false, null);
        }

        private void CanMessageBePublished_BasedOnPublishedAndDestroyed(
            bool messageHasBeenPublished,
            bool messageHasBeenDestroyed,
            Type exceptionType)
        {
            // Arrange
            var queryValidator = CreateMessageQueryValidator();
            var messageDTO = CreateMessageDTO(messageHasBeenPublished, messageHasBeenDestroyed);

            var parameters = A.Fake<IPublishMessageParameters>();
            parameters.CallsTo(x => x.Message).Returns(messageDTO);

            ArrangeMessagePublishParameterText(parameters, true, true);
            ArrangeMessageDTORecipient(parameters, true, true, true);

            // Act

            try
            {
                queryValidator.ThrowIfMessageCannotBePublished(parameters);
            }
            catch (Exception e)
            {
                if (e.GetType() == exceptionType)
                {
                    return;
                }

                throw new Exception("Exception was not of the correct type.");
            }

            if (exceptionType != null)
            {
                throw new Exception("Exception was expected.");
            }
        }

        [TestMethod]
        public void CanMessageDTOBePublished_BasedOnText()
        {
            // Arrange - Act - Assert
            CanMessageDTOBePublished_BasedOnText(true, true, null);
            CanMessageDTOBePublished_BasedOnText(true, false, typeof(ArgumentException));
            CanMessageDTOBePublished_BasedOnText(false, true, typeof(ArgumentException));
            CanMessageDTOBePublished_BasedOnText(false, false, typeof(ArgumentException));
        }

        private void CanMessageDTOBePublished_BasedOnText(bool doesTextExists, bool textContainsChars, Type exceptionType)
        {
            // Arrange
            var queryValidator = CreateMessageQueryValidator();

            var parameters = A.Fake<IPublishMessageParameters>();
            parameters.CallsTo(x => x.Message).Returns(null);

            ArrangeMessagePublishParameterText(parameters, doesTextExists, textContainsChars);

            // Act
            try
            {
                queryValidator.ThrowIfMessageCannotBePublished(parameters);

            }
            catch (Exception e)
            {
                if (e.GetType() == exceptionType)
                {
                    return;
                }

                throw new Exception("Exception was not of the correct type.");
            }

            if (exceptionType != null)
            {
                throw new Exception("Exception was expected.");
            }
        }

        [TestMethod]
        public void CanMessageBePublished_BasedOnRecipient()
        {
            // Arrange
            var queryValidator = CreateMessageQueryValidator();
            var parameter = A.Fake<IPublishMessageParameters>();
            parameter.CallsTo(x => x.Text).Returns(TestHelper.GenerateString());

            // Act
            queryValidator.ThrowIfMessageCannotBePublished(parameter);

            // No exception thrown
        }

        private void ArrangeMessagePublishParameterText(IPublishMessageParameters parameters, bool doesTextExists, bool textContainsChars)
        {
            string text = doesTextExists ? textContainsChars ? TestHelper.GenerateString() : string.Empty : null;

            parameters.CallsTo(x => x.Text).Returns(text);
        }

        private void ArrangeMessageDTORecipient(
            IPublishMessageParameters parameters,
            bool isRecipientValid,
            bool isRecipientIdValid,
            bool isRecipientScreenNameValid)
        {
            var recipient = A.Fake<IUserDTO>();
            var recipientId = TestHelper.GenerateRandomLong();
            var recipientScreenName = TestHelper.GenerateString();

            parameters.CallsTo(x => x.Recipient).Returns(recipient);
            parameters.CallsTo(x => x.RecipientId).Returns(recipientId);
            parameters.CallsTo(x => x.RecipientScreenName).Returns(recipientScreenName);

            _fakeUserQueryValidator.CallsTo(x => x.CanUserBeIdentified(recipient)).Returns(isRecipientValid);
            _fakeUserQueryValidator.CallsTo(x => x.IsUserIdValid(recipientId)).Returns(isRecipientIdValid);
            _fakeUserQueryValidator.CallsTo(x => x.IsScreenNameValid(recipientScreenName)).Returns(isRecipientScreenNameValid);
        }

        #endregion

        #region Can Message Be Destroyed

        [TestMethod]
        public void CanMessageDTOBeDestroyed_BasedOnMessageHasBeenPublishedOrDestroyed()
        {
            // Arrange - Act
            var result1 = CanMessageDTOBeDestroyed_BasedOnPublishedAndDestroyed(true, true, true);
            var result2 = CanMessageDTOBeDestroyed_BasedOnPublishedAndDestroyed(true, true, false);
            var result3 = CanMessageDTOBeDestroyed_BasedOnPublishedAndDestroyed(true, false, true);
            var result4 = CanMessageDTOBeDestroyed_BasedOnPublishedAndDestroyed(true, false, false);

            var result5 = CanMessageDTOBeDestroyed_BasedOnPublishedAndDestroyed(false, true, true);
            var result6 = CanMessageDTOBeDestroyed_BasedOnPublishedAndDestroyed(false, true, false);
            var result7 = CanMessageDTOBeDestroyed_BasedOnPublishedAndDestroyed(false, false, true);
            var result8 = CanMessageDTOBeDestroyed_BasedOnPublishedAndDestroyed(false, false, false);

            // Assert
            Assert.IsFalse(result1);
            Assert.IsFalse(result2);
            Assert.IsTrue(result3);
            Assert.IsFalse(result4);

            Assert.IsFalse(result5);
            Assert.IsFalse(result6);
            Assert.IsFalse(result7);
            Assert.IsFalse(result8);
        }

        private bool CanMessageDTOBeDestroyed_BasedOnPublishedAndDestroyed(
            bool messageHasBeenPublished,
            bool messageHasBeenDestroyed,
            bool isMessageIdSetup)
        {
            // Arrange
            var queryValidator = CreateMessageQueryValidator();
            var messageDTO = CreateMessageDTO(messageHasBeenPublished, messageHasBeenDestroyed);
            messageDTO.Id = isMessageIdSetup ? new Random().Next() : TweetinviSettings.DEFAULT_ID;

            // Act
            return queryValidator.CanMessageDTOBeDestroyed(messageDTO);
        }

        [TestMethod]
        public void CanMessageDTOBeDestroyed_MessageDTOIsNull_ReturnsFalse()
        {
            // Arrange
            var queryValidator = CreateMessageQueryValidator();

            // Act
            var result = queryValidator.CanMessageDTOBeDestroyed(null);

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region Is Text Message Valid

        [TestMethod]
        public void IsTextMessageValid_IsNull_ReturnsFalse()
        {
            // Arrange
            var queryValidator = CreateMessageQueryValidator();

            // Act
            var result = queryValidator.IsMessageTextValid(null);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsTextMessageValid_IsEmpty_ReturnsFalse()
        {
            // Arrange
            var queryValidator = CreateMessageQueryValidator();

            // Act
            var result = queryValidator.IsMessageTextValid(String.Empty);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsTextMessageValid_IsRandom_ReturnsTrue()
        {
            string message = TestHelper.GenerateString();

            // Arrange
            var queryValidator = CreateMessageQueryValidator();

            // Act
            var result = queryValidator.IsMessageTextValid(message);

            // Assert
            Assert.IsTrue(result);
        }

        #endregion

        #region Is User Id Valid

        [TestMethod]
        public void IsMessageIdValid_IsDefault_ReturnsFalse()
        {
            // Arrange
            var queryValidator = CreateMessageQueryValidator();

            // Act
            var result = queryValidator.IsMessageIdValid(TestHelper.DefaultId());

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsMessageIdValid_IsRandom_ReturnsTrue()
        {
            var id = TestHelper.GenerateRandomLong();

            // Arrange
            var queryValidator = CreateMessageQueryValidator();

            // Act
            var result = queryValidator.IsMessageIdValid(id);

            // Assert
            Assert.IsTrue(result);
        }

        #endregion

        private IMessageDTO CreateMessageDTO(
           bool hasBeenPublished,
           bool hasBeenDestroyed)
        {
            var messageDTO = A.Fake<IMessageDTO>();

            messageDTO.CallsTo(x => x.IsMessagePublished).Returns(hasBeenPublished);
            messageDTO.CallsTo(x => x.IsMessageDestroyed).Returns(hasBeenDestroyed);

            return messageDTO;
        }

        public MessageQueryValidator CreateMessageQueryValidator()
        {
            return _fakeBuilder.GenerateClass();
        }
    }
}