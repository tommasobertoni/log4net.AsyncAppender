using Xunit.Abstractions;

namespace Tests
{
    internal static class MockFactory
    {
        public static TestableElasticSearchAsyncAppender GetAnAppender(
            ITestOutputHelper testOutputHelper,
            bool autoConfigure = true)
        {
            var (appender, _) = GetAnAppenderWithErrorHandler(testOutputHelper, autoConfigure);
            return appender;
        }

        public static (TestableElasticSearchAsyncAppender, MockErrorHandler) GetAnAppenderWithErrorHandler(
            ITestOutputHelper testOutputHelper,
            bool autoConfigure = true)
        {
            var appender = new TestableElasticSearchAsyncAppender(autoConfigure);

            var mockErrorHandler = new MockErrorHandler(testOutputHelper);
            appender.ErrorHandler = mockErrorHandler;

            return (appender, mockErrorHandler);
        }
    }
}
