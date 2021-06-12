using Xunit;
using Xunit.Abstractions;
using static Tests.MockFactory;

namespace Tests
{
    public class HttpEndpointTests
    {
        private readonly TestableHttpEndpointAsyncAppender _appender;
        private readonly MockErrorHandler _eh;

        public HttpEndpointTests(ITestOutputHelper testOutputHelper)
        {
            var (appender, eh) = GetAnAppenderWithErrorHandler(testOutputHelper, autoConfigure: false);
            _appender = appender;
            _eh = eh;
        }

        [Fact]
        public void Correct_endpoint_from_url()
        {
            _appender.Url = "https://www.server.com:8080/test/api?v=1";

            _appender.Configure();
            Assert.True(_appender.ValidateSelf());

            _appender.ActivateOptions();
            Assert.Equal(0, _eh.ErrorsCount);

            var endpoint = _appender.CreateEndpoint();
            Assert.NotNull(endpoint);

            var expectedUrl = "https://www.server.com:8080/test/api?v=1";
            Assert.Equal(expectedUrl, endpoint.AbsoluteUri);

            _appender.Close();
        }

        [Fact]
        public void Correct_endpoint_from_tokens()
        {
            _appender.Scheme = "https";
            _appender.Host = "www.server.com";
            _appender.Port = "8080";
            _appender.Path = "/test/api";
            _appender.Query = "v=1";
            _appender.UserName = "user";
            _appender.Password = "pass";

            _appender.Configure();
            Assert.True(_appender.ValidateSelf());
            
            _appender.ActivateOptions();
            Assert.Equal(0, _eh.ErrorsCount);

            var endpoint = _appender.CreateEndpoint();
            Assert.NotNull(endpoint);

            var expectedUrl = "https://user:pass@www.server.com:8080/test/api?v=1";
            Assert.Equal(expectedUrl, endpoint.AbsoluteUri);

            _appender.Close();
        }
    }
}
