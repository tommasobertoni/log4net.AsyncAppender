using System.Diagnostics;
using System.Linq;
using Xunit.Abstractions;

namespace Tests
{
    internal static class MockFactory
    {
        public static TestableAsyncAppender GetAnAppender(ITestOutputHelper testOutputHelper)
        {
            var (appender, _) = GetAnAppenderWithErrorHandler(testOutputHelper);
            return appender;
        }

        public static (TestableAsyncAppender, MockErrorHandler) GetAnAppenderWithErrorHandler(ITestOutputHelper testOutputHelper)
        {
            var appender = new TestableAsyncAppender();

            if (Debugger.IsAttached)
            {
                Trace.AutoFlush = true;
                appender.Trace = Debugger.IsAttached;
                EnsureTraceListenerExists(testOutputHelper);
            }

            var mockErrorHandler = new MockErrorHandler(testOutputHelper);
            appender.ErrorHandler = mockErrorHandler;

            return (appender, mockErrorHandler);
        }

        public static TestTraceListener GetCurrentTestTraceListener()
        {
            var existingTraceListener = Trace.Listeners.OfType<TestTraceListener>();
            return existingTraceListener?.FirstOrDefault();
        }

        public static void EnsureTraceListenerExists(ITestOutputHelper testOutputHelper)
        {
            var currentTraceListener = GetCurrentTestTraceListener();

            if (currentTraceListener is not null)
                Trace.Listeners.Remove(currentTraceListener);

            var testTraceListener = new TestTraceListener(testOutputHelper);
            Trace.Listeners.Add(testTraceListener);
        }

        public static MockAsyncAppenderConfigurator GetAConfigurator() => new();
    }
}
