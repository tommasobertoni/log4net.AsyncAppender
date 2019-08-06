using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using static Tests.MockFactory;

namespace Tests
{
    public class EndpointTests
    {
        [Test]
        public void CorrectEndpointFromUrlAndIndexToken()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler(autoConfigure: false);
            appender.RequestSlimResponse = false;
            appender.Url = "https://www.server.com:8080/test/api?v=1";
            appender.Index = "anIndex";

            appender.Configure();
            Assert.That(appender.ValidateSelf());
            appender.ActivateOptions();
            Assert.That(meh.ErrorsCount, Is.Zero);

            var endpoint = appender.CreateEndpoint();
            Assert.That(endpoint, Is.Not.Null);

            var expectedUrl = "https://www.server.com:8080/test/api/anIndex/logEvent/_bulk?v=1";
            Assert.That(endpoint.AbsoluteUri, Is.EqualTo(expectedUrl));

            appender.Close();
        }

        [Test]
        public void CorrectEndpointFromUrlAndIndexAndRoutingToken()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler(autoConfigure: false);
            appender.RequestSlimResponse = false;
            appender.Url = "https://www.server.com:8080/test/api?v=1";
            appender.Index = "anIndex";
            appender.Routing = "route123";

            appender.Configure();
            Assert.That(appender.ValidateSelf());
            appender.ActivateOptions();
            Assert.That(meh.ErrorsCount, Is.Zero);

            var endpoint = appender.CreateEndpoint();
            Assert.That(endpoint, Is.Not.Null);

            var expectedUrl = "https://www.server.com:8080/test/api/anIndex/logEvent/_bulk?v=1&routing=route123";
            Assert.That(endpoint.AbsoluteUri, Is.EqualTo(expectedUrl));

            appender.Close();
        }

        [Test, TestCaseSource(nameof(ConnectionStringTestCases))]
        public void CorrectEndpointFromConnectionString(string connectionString, string expectedUrl, bool requestSlimResponse)
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler(autoConfigure: false);
            appender.ConnectionString = connectionString;
            appender.RequestSlimResponse = requestSlimResponse;

            appender.Configure();
            Assert.That(appender.ValidateSelf());
            appender.ActivateOptions();
            Assert.That(meh.ErrorsCount, Is.Zero);

            var endpoint = appender.CreateEndpoint();
            Assert.That(endpoint, Is.Not.Null);

            Assert.That(endpoint.AbsoluteUri, Is.EqualTo(expectedUrl));

            appender.Close();
        }

        #region Test case sources

        static IEnumerable<TestCaseData> ConnectionStringTestCases()
        {
            var today = DateTime.UtcNow.ToString("yyyy.MM.dd");

            var testCaseData = new List<(string cs, string url, string name)>
            {
                ( cs:   "Server=localhost;Index=log;Port=9200;rolling=true",
                  url:  $"http://localhost:9200/log-{today}/logEvent/_bulk",
                  name: "Rolling" ),

                ( cs:   "Server=localhost;Index=log;Port=9200",
                  url:  "http://localhost:9200/log/logEvent/_bulk",
                  name: "Implicit non-rolling" ),

                ( cs:   "Server=localhost;Index=log;Port=9200;rolling=false",
                  url:  "http://localhost:9200/log/logEvent/_bulk",
                  name: "Explicit non-rolling" ),

                ( cs:   "Server=localhost;Index=log;rolling=true",
                  url:  $"http://localhost/log-{today}/logEvent/_bulk",
                  name: "Rolling portless" ),

                ( cs:   "Server=localhost;Index=log",
                  url:  $"http://localhost/log/logEvent/_bulk",
                  name: "Implicit non-rolling portless" ),

                ( cs:   "Server=localhost;Index=log;rolling=false",
                  url:  $"http://localhost/log/logEvent/_bulk",
                  name: "Explicit non-rolling portless" ),

                ( cs:   "Server=localhost;Index=log;Routing=foo",
                  url:  $"http://localhost/log/logEvent/_bulk?routing=foo",
                  name: "Routing" ),

                ( cs:   "Server=localhost;Index=log;Routing=foo;rolling=false;User=user;Pwd=pass",
                  url:  $"http://user:pass@localhost/log/logEvent/_bulk?routing=foo",
                  name: "With credentials" ),

                ( cs:   "Server=localhost;Index=log;Routing=foo;Query=v=1",
                  url:  $"http://localhost/log/logEvent/_bulk?v=1&routing=foo",
                  name: "With query string" ),
            };

            foreach (var x in testCaseData)
            {
                var requestSlimResponse = false;
                var (cs, url, name) = x;

                yield return new TestCaseData(cs, url, requestSlimResponse).SetName(name);

                // Slim response.

                var builder = new UriBuilder(url);
                var query = HttpUtility.ParseQueryString(builder.Query);
                query["filter_path"] = "took,errors";
                builder.Query = HttpUtility.UrlDecode(query.ToString());

                var slimResponseUrl = builder.Uri.AbsoluteUri;
                var slimName = $"{name} - slim";
                requestSlimResponse = true;

                yield return new TestCaseData(cs, slimResponseUrl, requestSlimResponse).SetName(slimName);
            }
        }

        #endregion
    }
}
