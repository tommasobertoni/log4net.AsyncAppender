namespace Tests
{
    internal static class MockFactory
    {
        public static TestableHttpEndpointAsyncAppender GetAnAppender(bool autoConfigure = true)
        {
            var (appender, _) = GetAnAppenderWithErrorHandler(autoConfigure);
            return appender;
        }

        public static (TestableHttpEndpointAsyncAppender, MockErrorHandler) GetAnAppenderWithErrorHandler(bool autoConfigure = true)
        {
            var appender = new TestableHttpEndpointAsyncAppender(autoConfigure);

            var mockErrorHandler = new MockErrorHandler();
            appender.ErrorHandler = mockErrorHandler;

            return (appender, mockErrorHandler);
        }
    }
}
