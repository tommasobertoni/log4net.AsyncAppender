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
            Assert.That(appender.ValidateSelf(), Is.False);
        }

        [Test]
        public void UrlIsAllYouNeed()
        {
            var appender = GetAnAppender();
            appender.Url = "https://www.server.com:8080/test/api?v=1";
            Assert.That(appender.ValidateSelf());
        }

        [Test]
        public void UrlValidation()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler();

            appender.Url = "-invalid-";
            Assert.That(appender.ValidateSelf(), Is.False);
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));

            appender.Url = "8080://https.www.server.com/test/api";
            Assert.That(appender.ValidateSelf(), Is.False);
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));

            appender.Url = "https://www.server.com:8080/test/api?v=1";
            Assert.That(appender.ValidateSelf());
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
            Assert.That(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.Zero);
        }

        [Test]
        public void MandatoryUrlTokensValidation()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler();

            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Path = "/test/api";

            appender.Scheme = null;
            Assert.That(appender.ValidateSelf(), Is.False);
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));
            appender.Scheme = "https";

            appender.Host = null;
            Assert.That(appender.ValidateSelf(), Is.False);
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));
            appender.Host = "www.server.com";

            appender.Scheme = "http";
            Assert.That(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));
            appender.Scheme = "https";

            appender.Scheme = "invalid";
            Assert.That(appender.ValidateSelf(), Is.False);
            Assert.That(meh.ErrorsCount, Is.EqualTo(3));
            appender.Scheme = "https";

            Assert.That(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(3));
        }

        [Test]
        public void PortUrlTokenValidation()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler();

            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Port = "8080";
            appender.Path = "/test/api";

            Assert.That(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.Zero);

            appender.Port = "80a80";

            Assert.That(appender.ValidateSelf(), Is.False);
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));

            appender.Port = null;

            Assert.That(appender.ValidateSelf());
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
            Assert.That(appender.ValidateSelf(), Is.False);
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));

            appender.Password = "bar";
            Assert.That(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));

            appender.UserName = string.Empty;
            Assert.That(appender.ValidateSelf(), Is.False);
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));

            appender.Password = null;

            Assert.That(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));
        }

        [Test]
        public void DefaultJsonSerializerIsAssigned()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler(autoConfigure: false);

            Assert.That(appender.EventJsonSerializer, Is.Null);
            Assert.That(appender.EventJsonSerializerDelegate, Is.Null);

            appender.Configure();

            Assert.That(meh.ErrorsCount, Is.Zero);
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

            Assert.That(meh.ErrorsCount, Is.Zero);
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

            Assert.That(meh.ErrorsCount, Is.Zero);
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

                Assert.That(meh.ErrorsCount, Is.Zero);
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

                Assert.That(meh.ErrorsCount, Is.Zero);
                Assert.That(appender.EventJsonSerializer, Is.Null);
                Assert.That(appender.EventJsonSerializerDelegate, Is.Not.Null);
                Assert.That(appender.EventJsonSerializerDelegate, Is.EqualTo(ejsDelegate));
            }
        }

        private class MockEventJsonSerializer : IEventJsonSerializer
        {
            public string SerializeToJson(log4net.Core.LoggingEvent loggingEvent) => string.Empty;
        }

        [Test]
        public async Task SuccessStatusCodeCanBeEnsured()
        {
            //bool fail = false;

            //var mockHandler = new Mock<System.Net.Http.HttpClientHandler>();
            //mockHandler.Protected()
            //    .Setup<Task<System.Net.Http.HttpResponseMessage>>(
            //        "SendAsync",
            //        ItExpr.IsAny<System.Net.Http.HttpRequestMessage>(),
            //        ItExpr.IsAny<System.Threading.CancellationToken>())
            //    .Returns(() => Task.FromResult(new System.Net.Http.HttpResponseMessage(fail
            //        ? System.Net.HttpStatusCode.BadRequest
            //        : System.Net.HttpStatusCode.OK)));

            //var (appender, meh) = GetAnAppenderWithErrorHandler();

            //Assert.That(appender.EnsureSuccessStatusCode, Is.False); // Default.

            //appender.EnsureSuccessStatusCode = true;
            //appender.HttpClient = new System.Net.Http.HttpClient(mockHandler.Object);

            //appender.Scheme = "https";
            //appender.Host = "www.server.com";
            //appender.Path = "/test/api";

            //appender.ActivateOptions();

            //await new ProcessingStarted(appender);

            //Assert.That(meh.ErrorsCount, Is.Zero);
            //Assert.That(appender.Activated);
            //Assert.That(appender.AcceptsLoggingEvents);

            //var @event = new log4net.Core.LoggingEvent(new log4net.Core.LoggingEventData());

            //appender.Append(@event);
            //await new ProcessingTerminationTask(appender);
            //Assert.That(meh.ErrorsCount, Is.Zero);

            //fail = true;
            //appender.Append(@event);
            //appender.Append(@event);
            //await Task.Delay(1000);
            //await new ProcessingTerminationTask(appender);
            //Assert.That(meh.ErrorsCount, Is.EqualTo(2));

            //Assert.That(fail);
            //appender.EnsureSuccessStatusCode = false;
            //appender.Append(@event);
            //appender.Append(@event);
            //await Task.Delay(10);
            //await new ProcessingTerminationTask(appender);
            //Assert.That(meh.ErrorsCount, Is.EqualTo(2));

            //appender.Close();
        }
    }
}