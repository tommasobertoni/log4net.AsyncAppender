using log4net.AsyncAppender;
using log4net.Core;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    internal static class MockFactory
    {
        public static AsyncAppender GetAnAppender()
        {
            var (appender, _) = GetAnAppenderWithErrorHandler();
            return appender;
        }

        public static (AsyncAppender, MockErrorHandler) GetAnAppenderWithErrorHandler()
        {
            var appenderMock = new Mock<AsyncAppender> { CallBase = true };

            appenderMock.Setup(x => x.ProcessAsync(It.IsAny<List<LoggingEvent>>()))
                .Returns(Task.CompletedTask);

            var appender = appenderMock.Object;

            var mockErrorHandler = new MockErrorHandler();
            appender.ErrorHandler = mockErrorHandler;

            return (appender, mockErrorHandler);
        }

        public static MockAsyncAppenderConfigurator GetAConfigurator()
            => new MockAsyncAppenderConfigurator();
    }
}
