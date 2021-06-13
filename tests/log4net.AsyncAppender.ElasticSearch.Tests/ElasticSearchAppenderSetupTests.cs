using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static Tests.MockFactory;

namespace Tests
{
    public class ElasticSearchAppenderSetupTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public ElasticSearchAppenderSetupTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void Default_configuration_is_invalid()
        {
            var appender = GetAnAppender(_testOutputHelper);
            Assert.False(appender.ValidateSelf());
        }

        [Fact]
        public void Url_is_not_enough()
        {
            var appender = GetAnAppender(_testOutputHelper);
            appender.Url = "https://www.server.com:8080/test/api?v=1";
            Assert.False(appender.ValidateSelf());
        }

        [Fact]
        public void Connection_string_is_parsed_correctly()
        {
            var (appender, eh) = GetAnAppenderWithErrorHandler(_testOutputHelper, autoConfigure: false);

            Assert.Null(appender.ConnectionString);

            appender.ConnectionString =
                "Scheme=http;User=me;Pwd=pass;Server=www.server.com;Port=8080;path=/test/api;query=v=1;Index=anIndex;Routing=aRoute;rolling=true";

            appender.Configure();

            Assert.Equal(0, eh.ErrorsCount);

            Assert.Equal("http", appender.Scheme);
            Assert.Equal("www.server.com", appender.Host);
            Assert.Equal("8080", appender.Port);
            Assert.Equal("/test/api", appender.Path);
            Assert.Equal("v=1", appender.Query);
            Assert.Equal("anIndex", appender.Index);
            Assert.Equal("aRoute", appender.Routing);
            Assert.True(appender.IsRollingIndex);
            Assert.Equal("me", appender.UserName);
            Assert.Equal("pass", appender.Password);
        }

        [Fact]
        public void Mandatory_tokens_validation()
        {
            var appender = GetAnAppender(_testOutputHelper, autoConfigure: false);
            appender.Url = "https://www.server.com:8080/test/api?v=1";
            appender.Configure();

            appender.Index = "anIndex";
            Assert.True(appender.ValidateSelf());

            appender.Index = null;
            Assert.False(appender.ValidateSelf());
        }

        [Fact]
        public void There_is_a_default_event_projection()
        {
            var appender = GetAnAppender(_testOutputHelper, autoConfigure: false);

            Assert.Null(appender.Projection);

            appender.Configure();

            Assert.NotNull(appender.Projection);
        }

        [Fact]
        public void There_is_a_default_content_type()
        {
            var appender = GetAnAppender(_testOutputHelper, autoConfigure: false);

            Assert.Null(appender.ContentType);

            appender.Configure();

            Assert.Equal("application/json", appender.ContentType);
        }

        [Fact]
        public void Content_type_can_be_set()
        {
            var appender = GetAnAppender(_testOutputHelper, autoConfigure: false);

            appender.ContentType = "application/x-ndjson";

            appender.Configure();

            Assert.Equal("application/x-ndjson", appender.ContentType);
        }

        [Fact]
        public void Slim_response_is_requested_by_default()
        {
            var appender = GetAnAppender(_testOutputHelper);
            Assert.True(appender.RequestSlimResponse);
        }

        [Fact]
        public async Task Custom_projection_is_used()
        {
            int projectionInvocationsCount = 0;

            string customProjection(log4net.Core.LoggingEvent e)
            {
                Interlocked.Increment(ref projectionInvocationsCount);
                return e.TimeStampUtc.ToString();
            }

            var appender = GetAnAppender(_testOutputHelper, autoConfigure: false);
            appender.MaxBatchSize = 2;
            appender.MaxConcurrentProcessorsCount = 3;
            appender.Url = "https://www.server.com:8080/test/api?v=1";
            appender.Index = "anIndex";
            appender.Projection = customProjection;

            appender.ActivateOptions();

            Assert.True(appender.Activated);
            Assert.True(appender.AcceptsLoggingEvents);
            Assert.Equal(customProjection, appender.Projection);

            var @event = new log4net.Core.LoggingEvent(new log4net.Core.LoggingEventData());

            for (int i = 0; i < 100; i++)
                appender.Append(@event);

            await appender.ProcessingStarted();
            await appender.ProcessingTerminated();

            appender.Close();

            Assert.False(appender.IsProcessing);
            Assert.Equal(100, projectionInvocationsCount);
        }
    }
}
