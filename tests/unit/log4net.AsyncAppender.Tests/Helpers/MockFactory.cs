using log4net.AsyncAppender;
using log4net.Core;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Tests
{
    internal static class MockFactory
    {
        public static TestableAsyncAppender GetAnAppender()
        {
            var (appender, _) = GetAnAppenderWithErrorHandler();
            return appender;
        }

        public static (TestableAsyncAppender, MockErrorHandler) GetAnAppenderWithErrorHandler()
        {
            var appender = new TestableAsyncAppender();

            if (Debugger.IsAttached)
            {
                Trace.AutoFlush = true;
                appender.Trace = Debugger.IsAttached;
                EnsureTraceListenerExists();
            }

            var mockErrorHandler = new MockErrorHandler();
            appender.ErrorHandler = mockErrorHandler;

            return (appender, mockErrorHandler);
        }

        public static TestTraceListener GetCurrentTestTraceListener()
        {
            var existingTraceListener = Trace.Listeners.OfType<TestTraceListener>();
            return existingTraceListener?.FirstOrDefault();
        }

        public static void EnsureTraceListenerExists()
        {
            var currentTraceListener = GetCurrentTestTraceListener();

            if (currentTraceListener != null)
                Trace.Listeners.Remove(currentTraceListener);

            var testTraceListener = new TestTraceListener(writeToTestContext: true);
            Trace.Listeners.Add(testTraceListener);
        }

        public static MockAsyncAppenderConfigurator GetAConfigurator()
            => new MockAsyncAppenderConfigurator();
    }
}
