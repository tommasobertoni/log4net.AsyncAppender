using System;
using System.Collections.Generic;
using System.Web;
using Xunit;
using Xunit.Abstractions;
using static Tests.MockFactory;

namespace Tests
{
    public class ElasticSearchEndpointTests
    {
        private readonly TestableElasticSearchAsyncAppender _appender;
        private readonly MockErrorHandler _eh;

        public ElasticSearchEndpointTests(ITestOutputHelper testOutputHelper)
        {
            var (appender, eh) = GetAnAppenderWithErrorHandler(testOutputHelper, autoConfigure: false);
            _appender = appender;
            _eh = eh;
        }

        [Fact]
        public void Correct_endpoint_from_url_and_index_token()
        {
            _appender.RequestSlimResponse = false;
            _appender.Url = "https://www.server.com:8080/test/api?v=1";
            _appender.Index = "anIndex";

            _appender.Configure();
            Assert.True(_appender.ValidateSelf());
            _appender.ActivateOptions();
            Assert.Equal(0, _eh.ErrorsCount);

            var endpoint = _appender.CreateEndpoint();
            Assert.NotNull(endpoint);

            var expectedUrl = "https://www.server.com:8080/test/api/anIndex/logEvent/_bulk?v=1";
            Assert.Equal(expectedUrl, endpoint.AbsoluteUri);

            _appender.Close();
        }

        [Fact]
        public void Correct_endpoint_from_url_and_index_and_routing_token()
        {
            _appender.RequestSlimResponse = false;
            _appender.Url = "https://www.server.com:8080/test/api?v=1";
            _appender.Index = "anIndex";
            _appender.Routing = "route123";

            _appender.Configure();
            Assert.True(_appender.ValidateSelf());
            _appender.ActivateOptions();
            Assert.Equal(0, _eh.ErrorsCount);

            var endpoint = _appender.CreateEndpoint();
            Assert.NotNull(endpoint);

            var expectedUrl = "https://www.server.com:8080/test/api/anIndex/logEvent/_bulk?v=1&routing=route123";
            Assert.Equal(expectedUrl, endpoint.AbsoluteUri);

            _appender.Close();
        }

        [Theory, MemberData(nameof(Connection_string_test_cases))]
        public void Correct_endpoint_from_connection_string(string connectionString, string expectedUrl, bool requestSlimResponse)
        {
            _appender.ConnectionString = connectionString;
            _appender.RequestSlimResponse = requestSlimResponse;

            _appender.Configure();
            Assert.True(_appender.ValidateSelf());
            _appender.ActivateOptions();
            Assert.Equal(0, _eh.ErrorsCount);

            var endpoint = _appender.CreateEndpoint();
            Assert.NotNull(endpoint);

            Assert.Equal(expectedUrl, endpoint.AbsoluteUri);

            _appender.Close();
        }

        #region Test case sources

        public static IEnumerable<object[]> Connection_string_test_cases()
        {
            var today = DateTime.UtcNow.ToString("yyyy.MM.dd");

            var testCases = new List<(string cs, string url)>
            {
                ( cs:   "Server=localhost;Index=log;Port=9200;rolling=true",
                  url:  $"http://localhost:9200/log-{today}/logEvent/_bulk"
                ),

                ( cs:   "Server=localhost;Index=log;Port=9200",
                  url:  "http://localhost:9200/log/logEvent/_bulk"
                ),

                ( cs:   "Server=localhost;Index=log;Port=9200;rolling=false",
                  url:  "http://localhost:9200/log/logEvent/_bulk"
                ),

                ( cs:   "Server=localhost;Index=log;rolling=true",
                  url:  $"http://localhost/log-{today}/logEvent/_bulk"
                ),

                ( cs:   "Server=localhost;Index=log",
                  url:  $"http://localhost/log/logEvent/_bulk"
                ),

                ( cs:   "Server=localhost;Index=log;rolling=false",
                  url:  $"http://localhost/log/logEvent/_bulk"
                ),

                ( cs:   "Server=localhost;Index=log;Routing=foo",
                  url:  $"http://localhost/log/logEvent/_bulk?routing=foo"
                ),

                ( cs:   "Server=localhost;Index=log;Routing=foo;rolling=false;User=user;Pwd=pass",
                  url:  $"http://user:pass@localhost/log/logEvent/_bulk?routing=foo"
                ),

                ( cs:   "Server=localhost;Index=log;Routing=foo;Query=v=1",
                  url:  $"http://localhost/log/logEvent/_bulk?v=1&routing=foo"
                ),
            };

            foreach (var x in testCases)
            {
                var requestSlimResponse = false;
                var (cs, url) = x;

                yield return new object[] { cs, url, requestSlimResponse };

                // Slim response.

                var builder = new UriBuilder(url);
                var query = HttpUtility.ParseQueryString(builder.Query);
                query["filter_path"] = "took,errors";
                builder.Query = HttpUtility.UrlDecode(query.ToString());

                var slimResponseUrl = builder.Uri.AbsoluteUri;
                requestSlimResponse = true;

                yield return new object[] { cs, slimResponseUrl, requestSlimResponse };
            }
        }

        #endregion
    }
}
