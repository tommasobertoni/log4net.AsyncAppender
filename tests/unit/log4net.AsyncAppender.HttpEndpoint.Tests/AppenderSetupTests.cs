using System;
using System.Threading.Tasks;
using log4net.AsyncAppender;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using static Tests.MockFactory;

namespace Tests
{
    public class AppenderSetupTests
    {
        [Test]
        public void DefaultConfigurationIsInvalid()
        {
            var appender = GetAnAppender();
            Assert.False(appender.ValidateSelf());
        }

        [Test]
        public void UrlIsAllYouNeed()
        {
            var appender = GetAnAppender();
            appender.Url = "https://www.server.com:8080/test/api?v=1";
            Assert.True(appender.ValidateSelf());
        }

        [Test]
        public void UrlValidation()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler();

            appender.Url = "-invalid-";
            Assert.False(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));

            appender.Url = "8080://https.www.server.com/test/api";
            Assert.False(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));

            appender.Url = "https://www.server.com:8080/test/api?v=1";
            Assert.True(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));
        }

        [Test]
        public void ValidUnauthorizedUrlTokens()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler();

            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Port = "8080";
            appender.Path = "/test/api";
            appender.Query = "v=1";
            Assert.True(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(0));
        }

        [Test]
        public void MandatoryUrlTokensValidation()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler();

            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Path = "/test/api";

            appender.Scheme = null;
            Assert.False(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));
            appender.Scheme = "https";

            appender.Host = null;
            Assert.False(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));
            appender.Host = "www.server.com";

            appender.Path = null;
            Assert.False(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(3));
            appender.Path = "/test/api";

            appender.Scheme = "http";
            Assert.True(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(3));
            appender.Scheme = "https";

            appender.Scheme = "invalid";
            Assert.False(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(4));
            appender.Scheme = "https";

            Assert.True(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(4));
        }

        [Test]
        public void PortUrlTokenValidation()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler();

            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Port = "8080";
            appender.Path = "/test/api";

            Assert.True(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(0));

            appender.Port = "80a80";

            Assert.False(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));

            appender.Port = null;

            Assert.True(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));
        }

        [Test]
        public void CredentialsUrlTokensValidation()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler();

            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Path = "/test/api";

            appender.UserName = "foo";
            Assert.False(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));

            appender.Password = "bar";
            Assert.True(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));

            appender.UserName = string.Empty;
            Assert.False(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));

            appender.Password = null;

            Assert.True(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));
        }

        [Test]
        public void DefaultJsonSerializerIsAssigned()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler(autoConfigure: false);

            Assert.That(appender.EventJsonSerializer, Is.Null);
            Assert.That(appender.EventJsonSerializerDelegate, Is.Null);

            appender.Configure();

            Assert.That(meh.ErrorsCount, Is.EqualTo(0));
            Assert.That(appender.EventJsonSerializerDelegate, Is.Not.Null);
            var @event = new log4net.Core.LoggingEvent(new log4net.Core.LoggingEventData());
            Assert.That(() => appender.EventJsonSerializerDelegate.Invoke(@event), Throws.Nothing);
        }

        [Test]
        public void DefaultJsonSerializerIsNotAssignedByConfiguration()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler(autoConfigure: false);

            Assert.That(appender.EventJsonSerializer, Is.Null);
            Assert.That(appender.EventJsonSerializerDelegate, Is.Null);

            appender.UseDefaultEventJsonSerializerWhenMissing = false;
            appender.Configure();

            Assert.That(meh.ErrorsCount, Is.EqualTo(0));
            Assert.That(appender.EventJsonSerializer, Is.Null);
            Assert.That(appender.EventJsonSerializerDelegate, Is.Null);
        }

        [Test]
        public void JsonSerializerIsRequiredByValidation()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler(autoConfigure: false);

            Assert.That(appender.EventJsonSerializer, Is.Null);
            Assert.That(appender.EventJsonSerializerDelegate, Is.Null);

            appender.UseDefaultEventJsonSerializerWhenMissing = false;
            appender.Configure();

            Assert.That(meh.ErrorsCount, Is.EqualTo(0));
            Assert.That(appender.EventJsonSerializer, Is.Null);
            Assert.That(appender.EventJsonSerializerDelegate, Is.Null);

            Assert.That(() => appender.ValidateSelf(), Throws.Nothing);
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));
        }

        [Test]
        public void CustomJsonSerializerIsNotOverridden()
        {
            {
                var (appender, meh) = GetAnAppenderWithErrorHandler(autoConfigure: false);

                Assert.That(appender.EventJsonSerializer, Is.Null);
                Assert.That(appender.EventJsonSerializerDelegate, Is.Null);

                var mockEjs = new MockEventJsonSerializer();
                appender.EventJsonSerializer = mockEjs;

                appender.Configure();

                Assert.That(meh.ErrorsCount, Is.EqualTo(0));
                Assert.That(appender.EventJsonSerializerDelegate, Is.Null);
                Assert.That(appender.EventJsonSerializer, Is.Not.Null);
                Assert.That(appender.EventJsonSerializer, Is.EqualTo(mockEjs));
            }

            {
                var (appender, meh) = GetAnAppenderWithErrorHandler(autoConfigure: false);

                Assert.That(appender.EventJsonSerializer, Is.Null);
                Assert.That(appender.EventJsonSerializerDelegate, Is.Null);

                Func<log4net.Core.LoggingEvent, string> ejsDelegate = _ => string.Empty;
                appender.EventJsonSerializerDelegate = ejsDelegate;

                appender.Configure();

                Assert.That(meh.ErrorsCount, Is.EqualTo(0));
                Assert.That(appender.EventJsonSerializer, Is.Null);
                Assert.That(appender.EventJsonSerializerDelegate, Is.Not.Null);
                Assert.That(appender.EventJsonSerializerDelegate, Is.EqualTo(ejsDelegate));
            }
        }

        private class MockEventJsonSerializer : IEventJsonSerializer
        {
            public string SerializeToJson(log4net.Core.LoggingEvent loggingEvent) => string.Empty;
        }
    }
}