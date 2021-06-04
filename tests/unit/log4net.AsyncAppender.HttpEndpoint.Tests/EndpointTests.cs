using NUnit.Framework;
using static Tests.MockFactory;

namespace Tests
{
    public class EndpointTests
    {
        [Test]
        public void CorrectEndpointFromUrl()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler(autoConfigure: false);
            appender.Url = "https://www.server.com:8080/test/api?v=1";

            appender.Configure();
            Assert.That(appender.ValidateSelf());
            appender.ActivateOptions();
            Assert.That(meh.ErrorsCount, Is.Zero);

            var endpoint = appender.CreateEndpoint();
            Assert.That(endpoint, Is.Not.Null);

            var expectedUrl = "https://www.server.com:8080/test/api?v=1";
            Assert.That(endpoint.AbsoluteUri, Is.EqualTo(expectedUrl));

            appender.Close();
        }

        [Test]
        public void CorrectEndpointFromTokens()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler(autoConfigure: false);
            appender.Scheme = "https";
            appender.Host = "www.server.com";
            appender.Port = "8080";
            appender.Path = "/test/api";
            appender.Query = "v=1";
            appender.UserName = "user";
            appender.Password = "pass";

            appender.Configure();
            Assert.That(appender.ValidateSelf());
            appender.ActivateOptions();
            Assert.That(meh.ErrorsCount, Is.Zero);

            var endpoint = appender.CreateEndpoint();
            Assert.That(endpoint, Is.Not.Null);

            var expectedUrl = "https://user:pass@www.server.com:8080/test/api?v=1";
            Assert.That(endpoint.AbsoluteUri, Is.EqualTo(expectedUrl));

            appender.Close();
        }
    }
}
