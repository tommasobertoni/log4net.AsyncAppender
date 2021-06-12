using Xunit.Abstractions;

namespace Tests
{
    internal static class MockFactory
    {
        public static TestableHttpEndpointAsyncAppender GetAnAppender(
            ITestOutputHelper testOutputHelper,
            bool autoConfigure = true)
        {
            var (appender, _) = GetAnAppenderWithErrorHandler(testOutputHelper, autoConfigure);
            return appender;
        }

        public static (TestableHttpEndpointAsyncAppender, MockErrorHandler) GetAnAppenderWithErrorHandler(
            ITestOutputHelper testOutputHelper,
            bool autoConfigure = true)
        {
            var appender = new TestableHttpEndpointAsyncAppender(autoConfigure);

            var mockErrorHandler = new MockErrorHandler(testOutputHelper);
            appender.ErrorHandler = mockErrorHandler;

            return (appender, mockErrorHandler);
        }
    }
}
