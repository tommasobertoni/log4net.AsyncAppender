using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
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
        public void UrlIsNotEnough()
        {
            var appender = GetAnAppender();
            appender.Url = "https://www.server.com:8080/test/api?v=1";
            Assert.That(appender.ValidateSelf(), Is.False);
        }

        [Test]
        public void ConnectionStringIsParsedCorrectly()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler(autoConfigure: false);

            Assert.That(appender.ConnectionString, Is.Null);
            appender.ConnectionString =
                "Scheme=http;User=me;Pwd=pass;Server=www.server.com;Port=8080;path=/test/api;query=v=1;Index=anIndex;Routing=aRoute;rolling=true";

            appender.Configure();

            Assert.That(meh.ErrorsCount, Is.Zero);

            Assert.That(appender.Scheme, Is.EqualTo("http"));
            Assert.That(appender.Host, Is.EqualTo("www.server.com"));
            Assert.That(appender.Port, Is.EqualTo("8080"));
            Assert.That(appender.Path, Is.EqualTo("/test/api"));
            Assert.That(appender.Query, Is.EqualTo("v=1"));
            Assert.That(appender.Index, Is.EqualTo("anIndex"));
            Assert.That(appender.Routing, Is.EqualTo("aRoute"));
            Assert.That(appender.IsRollingIndex);
            Assert.That(appender.UserName, Is.EqualTo("me"));
            Assert.That(appender.Password, Is.EqualTo("pass"));
        }

        [Test]
        public void MandatoryTokensValidation()
        {
            var appender = GetAnAppender(autoConfigure: false);
            appender.Url = "https://www.server.com:8080/test/api?v=1";
            appender.Configure();

            appender.Index = "anIndex";
            Assert.That(appender.ValidateSelf(), Is.True);

            appender.Index = null;
            Assert.That(appender.ValidateSelf(), Is.False);
        }

        [Test]
        public void ThereIsADefaultEventProjection()
        {
            var appender = GetAnAppender(autoConfigure: false);

            Assert.That(appender.Projection, Is.Null);

            appender.Configure();

            Assert.That(appender.Projection, Is.Not.Null);
        }

        [Test]
        public void ThereIsADefaultContentType()
        {
            var appender = GetAnAppender(autoConfigure: false);

            Assert.That(appender.ContentType, Is.Null);

            appender.Configure();

            Assert.That(appender.ContentType, Is.EqualTo("application/json"));
        }

        [Test]
        public void ContentTypeCanBeSet()
        {
            var appender = GetAnAppender(autoConfigure: false);

            appender.ContentType = "application/x-ndjson";

            appender.Configure();

            Assert.That(appender.ContentType, Is.EqualTo("application/x-ndjson"));
        }

        [Test]
        public void SlimResponseIsRequestedByDefault()
        {
            var appender = GetAnAppender();
            Assert.That(appender.RequestSlimResponse, Is.True);
        }

        [Test]
        public async Task CustomProjectionIsUsed()
        {
            int projectionInvocationsCount = 0;
            Func<log4net.Core.LoggingEvent, string> customProjection = e =>
            {
                Interlocked.Increment(ref projectionInvocationsCount);
                return e.TimeStampUtc.ToString();
            };

            var appender = GetAnAppender(autoConfigure: false);
            appender.MaxBatchSize = 2;
            appender.MaxConcurrentProcessorsCount = 3;
            appender.Url = "https://www.server.com:8080/test/api?v=1";
            appender.Index = "anIndex";
            appender.Projection = customProjection;

            appender.ActivateOptions();

            Assert.That(appender.Activated);
            Assert.That(appender.AcceptsLoggingEvents);
            Assert.That(appender.Projection, Is.EqualTo(customProjection));

            var @event = new log4net.Core.LoggingEvent(new log4net.Core.LoggingEventData());

            for (int i = 0; i < 100; i++)
                appender.Append(@event);

            await appender.ProcessingStarted();
            await appender.ProcessingTerminated();

            appender.Close();

            Assert.That(appender.IsProcessing, Is.False);
            Assert.That(projectionInvocationsCount, Is.EqualTo(100));
        }
    }
}