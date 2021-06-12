using log4net.AsyncAppender;
using Xunit;
using Xunit.Abstractions;
using static Tests.MockFactory;

namespace Tests
{
    public class HttpEndpointAppenderSetupTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public HttpEndpointAppenderSetupTests(ITestOutputHelper testOutputHelper)
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
        public void Url_is_all_you_need()
        {
            var appender = GetAnAppender(_testOutputHelper);
            appender.Url = "https://www.server.com:8080/test/api?v=1";
            Assert.True(appender.ValidateSelf());
        }

        [Fact]
        public void Url_validation()
        {
            var (appender, eh) = GetAnAppenderWithErrorHandler(_testOutputHelper);

            appender.Url = "-invalid-";
            Assert.False(appender.ValidateSelf());
            Assert.Equal(1, eh.ErrorsCount);

            appender.Url = "8080://https.www.server.com/test/api";
            Assert.False(appender.ValidateSelf());
            Assert.Equal(2, eh.ErrorsCount);

            appender.Url = "https://www.server.com:8080/test/api?v=1";
            Assert.True(appender.ValidateSelf());
            Assert.Equal(2, eh.ErrorsCount);
        }

        [Fact]
        public void Valid_unauthorized_url_tokens()
        {
            var (appender, eh) = GetAnAppenderWithErrorHandler(_testOutputHelper);

            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Port = "8080";
            appender.Path = "/test/api";
            appender.Query = "v=1";
            Assert.True(appender.ValidateSelf());
            Assert.Equal(0, eh.ErrorsCount);
        }

        [Fact]
        public void Mandatory_url_tokens_validation()
        {
            var (appender, eh) = GetAnAppenderWithErrorHandler(_testOutputHelper);

            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Path = "/test/api";

            appender.Scheme = null;
            Assert.False(appender.ValidateSelf());
            Assert.Equal(1, eh.ErrorsCount);
            appender.Scheme = "https";

            appender.Host = null;
            Assert.False(appender.ValidateSelf());
            Assert.Equal(2, eh.ErrorsCount);
            appender.Host = "www.server.com";

            appender.Scheme = "http";
            Assert.True(appender.ValidateSelf());
            Assert.Equal(2, eh.ErrorsCount);
            appender.Scheme = "https";

            appender.Scheme = "invalid";
            Assert.False(appender.ValidateSelf());
            Assert.Equal(3, eh.ErrorsCount);
            appender.Scheme = "https";

            Assert.True(appender.ValidateSelf());
            Assert.Equal(3, eh.ErrorsCount);
        }

        [Fact]
        public void Port_url_token_validation()
        {
            var (appender, eh) = GetAnAppenderWithErrorHandler(_testOutputHelper);

            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Port = "8080";
            appender.Path = "/test/api";

            Assert.True(appender.ValidateSelf());
            Assert.Equal(0, eh.ErrorsCount);

            appender.Port = "80a80";

            Assert.False(appender.ValidateSelf());
            Assert.Equal(1, eh.ErrorsCount);

            appender.Port = null;

            Assert.True(appender.ValidateSelf());
            Assert.Equal(1, eh.ErrorsCount);
        }

        [Fact]
        public void Credentials_url_tokens_validation()
        {
            var (appender, eh) = GetAnAppenderWithErrorHandler(_testOutputHelper);

            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Path = "/test/api";

            appender.UserName = "foo";
            Assert.False(appender.ValidateSelf());
            Assert.Equal(1, eh.ErrorsCount);

            appender.Password = "bar";
            Assert.True(appender.ValidateSelf());
            Assert.Equal(1, eh.ErrorsCount);

            appender.UserName = string.Empty;
            Assert.False(appender.ValidateSelf());
            Assert.Equal(2, eh.ErrorsCount);

            appender.Password = null;

            Assert.True(appender.ValidateSelf());
            Assert.Equal(2, eh.ErrorsCount);
        }

        [Fact]
        public void Default_http_client_is_assigned()
        {
            var (appender, eh) = GetAnAppenderWithErrorHandler(_testOutputHelper, autoConfigure: false);

            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Path = "/test/api";

            Assert.Null(appender.HttpClient);

            appender.Configure();

            Assert.Equal(0, eh.ErrorsCount);
            Assert.NotNull(appender.HttpClient);
        }

        [Fact]
        public void Http_client_is_required_by_validation()
        {
            var (appender, eh) = GetAnAppenderWithErrorHandler(_testOutputHelper, autoConfigure: false);

            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Path = "/test/api";

            Assert.Null(appender.HttpClient);
            appender.Configure();
            Assert.NotNull(appender.HttpClient);

            appender.ValidateSelf();
            Assert.Equal(0, eh.ErrorsCount);

            appender.HttpClient = null;
            appender.ValidateSelf();
            Assert.Equal(1, eh.ErrorsCount);
        }

        [Fact]
        public void Custom_http_client_is_not_overridden()
        {
            {
                var (appender, eh) = GetAnAppenderWithErrorHandler(_testOutputHelper, autoConfigure: false);

                appender.Scheme = "https";
                appender.Host = "www.server.com";
                appender.Path = "/test/api";

                Assert.Null(appender.HttpClient);

                var myHttpClient = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    AutomaticDecompression = System.Net.DecompressionMethods.None
                });

                appender.HttpClient = myHttpClient;

                appender.Configure();

                Assert.Equal(0, eh.ErrorsCount);
                Assert.NotNull(appender.HttpClient);
                Assert.Equal(myHttpClient, appender.HttpClient);
            }

            {
                var (appender, eh) = GetAnAppenderWithErrorHandler(_testOutputHelper, autoConfigure: false);

                appender.Scheme = "https";
                appender.Host = "www.server.com";
                appender.Path = "/test/api";

                Assert.Null(appender.EventJsonSerializer);
                Assert.Null(appender.EventJsonSerializerDelegate);

                static string ejsDelegate(log4net.Core.LoggingEvent _) => string.Empty;
                appender.EventJsonSerializerDelegate = ejsDelegate;

                appender.Configure();

                Assert.Equal(0, eh.ErrorsCount);
                Assert.Null(appender.EventJsonSerializer);
                Assert.NotNull(appender.EventJsonSerializerDelegate);
                Assert.Equal(ejsDelegate, appender.EventJsonSerializerDelegate);
            }
        }

        [Fact]
        public void Default_json_serializer_is_assigned()
        {
            var (appender, eh) = GetAnAppenderWithErrorHandler(_testOutputHelper, autoConfigure: false);

            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Path = "/test/api";

            Assert.Null(appender.EventJsonSerializer);
            Assert.Null(appender.EventJsonSerializerDelegate);

            appender.Configure();

            Assert.Equal(0, eh.ErrorsCount);
            Assert.NotNull(appender.EventJsonSerializerDelegate);
            var @event = new log4net.Core.LoggingEvent(new log4net.Core.LoggingEventData());
            appender.EventJsonSerializerDelegate.Invoke(@event);
        }

        [Fact]
        public void Default_json_serializer_is_not_assigned_by_configuration()
        {
            var (appender, eh) = GetAnAppenderWithErrorHandler(_testOutputHelper, autoConfigure: false);

            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Path = "/test/api";

            Assert.Null(appender.EventJsonSerializer);
            Assert.Null(appender.EventJsonSerializerDelegate);

            appender.UseDefaultEventJsonSerializerWhenMissing = false;
            appender.Configure();

            Assert.Equal(0, eh.ErrorsCount);
            Assert.Null(appender.EventJsonSerializer);
            Assert.Null(appender.EventJsonSerializerDelegate);
        }

        [Fact]
        public void Json_serializer_ss_required_by_validation()
        {
            var (appender, eh) = GetAnAppenderWithErrorHandler(_testOutputHelper, autoConfigure: false);

            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Path = "/test/api";

            Assert.Null(appender.EventJsonSerializer);
            Assert.Null(appender.EventJsonSerializerDelegate);

            appender.UseDefaultEventJsonSerializerWhenMissing = false;
            appender.Configure();

            Assert.Equal(0, eh.ErrorsCount);
            Assert.Null(appender.EventJsonSerializer);
            Assert.Null(appender.EventJsonSerializerDelegate);

            appender.ValidateSelf();
            Assert.Equal(1, eh.ErrorsCount);
        }

        [Fact]
        public void Custom_json_serializer_is_not_overridden()
        {
            {
                var (appender, eh) = GetAnAppenderWithErrorHandler(_testOutputHelper, autoConfigure: false);

                appender.Scheme = "https";
                appender.Host = "www.server.com";
                appender.Path = "/test/api";

                Assert.Null(appender.EventJsonSerializer);
                Assert.Null(appender.EventJsonSerializerDelegate);

                var mockEjs = new MockEventJsonSerializer();
                appender.EventJsonSerializer = mockEjs;

                appender.Configure();

                Assert.Equal(0, eh.ErrorsCount);
                Assert.Null(appender.EventJsonSerializerDelegate);
                Assert.NotNull(appender.EventJsonSerializer);
                Assert.Equal(mockEjs, appender.EventJsonSerializer);
            }

            {
                var (appender, eh) = GetAnAppenderWithErrorHandler(_testOutputHelper, autoConfigure: false);

                appender.Scheme = "https";
                appender.Host = "www.server.com";
                appender.Path = "/test/api";

                Assert.Null(appender.EventJsonSerializer);
                Assert.Null(appender.EventJsonSerializerDelegate);

                static string ejsDelegate(log4net.Core.LoggingEvent _) => string.Empty;
                appender.EventJsonSerializerDelegate = ejsDelegate;

                appender.Configure();

                Assert.Equal(0, eh.ErrorsCount);
                Assert.Null(appender.EventJsonSerializer);
                Assert.NotNull(appender.EventJsonSerializerDelegate);
                Assert.Equal(ejsDelegate, appender.EventJsonSerializerDelegate);
            }
        }

        private class MockEventJsonSerializer : IEventJsonSerializer
        {
            public string SerializeToJson(log4net.Core.LoggingEvent loggingEvent) => string.Empty;
        }
    }
}
