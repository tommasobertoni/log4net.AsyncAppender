using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Tests.MockFactory;

namespace Tests
{
    public class EndpointTests
    {
        [Test]
        public void CorrectEndpointFromUrlAndIndexToken()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler(autoConfigure: false);
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
            appender.Url = "https://www.server.com:8080/test/api?v=1";
            appender.Index = "anIndex";
            appender.Routing = "route123";

            appender.Configure();
            Assert.That(appender.ValidateSelf());
            appender.ActivateOptions();
            Assert.That(meh.ErrorsCount, Is.Zero);

            var endpoint = appender.CreateEndpoint();
            Assert.That(endpoint, Is.Not.Null);

            var expectedUrl = "https://www.server.com:8080/test/api/anIndex/logEvent?routing=route123/_bulk?v=1";
            Assert.That(endpoint.AbsoluteUri, Is.EqualTo(expectedUrl));

            appender.Close();
        }

        [Test, TestCaseSource(nameof(ConnectionStringTestCases))]
        public void CorrectEndpointFromConnectionString(string connectionString, string expectedUrl)
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler(autoConfigure: false);
            appender.ConnectionString = connectionString;

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

            yield return new TestCaseData(
                "Server=localhost;Index=log;Port=9200;rolling=true",
                $"http://localhost:9200/log-{today}/logEvent/_bulk"
            ).SetName("Rolling");

            yield return new TestCaseData(
                "Server=localhost;Index=log;Port=9200",
                "http://localhost:9200/log/logEvent/_bulk"
            ).SetName("Implicit non-rolling");

            yield return new TestCaseData(
                "Server=localhost;Index=log;Port=9200;rolling=false",
                "http://localhost:9200/log/logEvent/_bulk"
            ).SetName("Explicit non-rolling");

            yield return new TestCaseData(
                "Server=localhost;Index=log;rolling=true",
                $"http://localhost/log-{today}/logEvent/_bulk"
            ).SetName("Rolling portless");

            yield return new TestCaseData(
                "Server=localhost;Index=log",
                $"http://localhost/log/logEvent/_bulk"
            ).SetName("Implicit non-rolling portless");

            yield return new TestCaseData(
                "Server=localhost;Index=log;rolling=false",
                $"http://localhost/log/logEvent/_bulk"
            ).SetName("Explicit non-rolling portless");

            yield return new TestCaseData(
                "Server=localhost;Index=log;Routing=foo",
                $"http://localhost/log/logEvent?routing=foo/_bulk"
            ).SetName("Routing");

            yield return new TestCaseData(
                "Server=localhost;Index=log;Routing=foo;rolling=false;User=user;Pwd=pass",
                $"http://user:pass@localhost/log/logEvent?routing=foo/_bulk"
            ).SetName("With credentials");

            yield return new TestCaseData(
                "Server=localhost;Index=log;Routing=foo;Query=v=1",
                $"http://localhost/log/logEvent?routing=foo/_bulk?v=1"
            ).SetName("With query string");
        }

        #endregion
    }
}
