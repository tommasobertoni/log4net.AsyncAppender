using System;
using log4net;
using log4net.Core;
using Moq;
using NUnit.Framework;

namespace IntegrationTests.Helpers
{
    internal class TestReport
    {
        public int ErrorsCount { get; set; }
    }

    internal class TestToolbox
    {
        public TestableAsyncAppender Appender { get; private set; }

        public int LogsCount { get; set; }

        private readonly Action<string> _outputHandler;

        private readonly ILog _log;
        private readonly Mock<IErrorHandler> _mockErrorHandler;

        public TestToolbox(ILog log, Action<string> outputHandler)
        {
            _log = log;
            _outputHandler = outputHandler;

            LogsCount = 0;

            #region Setup

            // TraceErrorHandler

            var trace = new TraceErrorHandler();
            _mockErrorHandler = new Mock<IErrorHandler>();

            _mockErrorHandler
                .Setup(x => x.Error(It.IsAny<string>()))
                .Callback<string>(message =>
                {
                    _outputHandler($"[ERROR] {message}");
                    trace.Error(message);
                });

            _mockErrorHandler
                .Setup(x => x.Error(It.IsAny<string>(), It.IsAny<Exception>()))
                .Callback<string, Exception>((message, ex) =>
                {
                    _outputHandler($"[ERROR] {message} {ex}");
                    trace.Error(message, ex);
                });

            _mockErrorHandler
                .Setup(x => x.Error(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<ErrorCode>()))
                .Callback<string, Exception, ErrorCode>((message, ex, code) =>
                {
                    _outputHandler($"[ERROR] {code} {message} {ex}");
                    trace.Error(message);
                });

            #endregion
        }

        public TestReport GetReport()
        {
            return new TestReport
            {
                ErrorsCount = _mockErrorHandler.Invocations.Count,
            };
        }

        public void ReplaceConfiguredAppenderWithTestAppender(int processorsCount)
        {
            var appender = new TestableAsyncAppender
            {
                MaxConcurrentProcessorsCount = processorsCount,
                MaxBatchSize = 512,
                CloseTimeoutMillis = 5000,
                ErrorHandler = _mockErrorHandler.Object,
            };

            appender.ActivateOptions();

            Assert.That(appender.Activated, Is.True);
            Assert.That(appender.AcceptsLoggingEvents, Is.True);
            Assert.That(appender.IsProcessing, Is.False);

            var currentAppender = _log.GetElasticsearchAppender();
            Assert.That(appender, Is.Not.EqualTo(currentAppender));

            _log.SetElasticsearchAppender(appender);
            Assert.That(appender, Is.EqualTo(_log.GetElasticsearchAppender()));

            currentAppender?.Close();
            Appender = appender;
        }

        public void VerifyLogsCount()
        {
            Assert.That(Appender.EventsProcessedCount, Is.EqualTo(LogsCount));

            var invocations = (LogsCount / Appender.MaxBatchSize) + 1;
            Assert.That(Appender.ProcessInvocationsCount, Is.GreaterThanOrEqualTo(invocations));
        }

        public void VerifyPartialLogsCount(bool allowZeroInvocations = false)
        {
            Assert.That(Appender.EventsProcessedCount, Is.InRange(1, LogsCount));

            if (!allowZeroInvocations)
                Assert.That(Appender.EventsProcessedCount, Is.GreaterThanOrEqualTo(1));
        }

        public void VerifyNoErrors()
        {
            _mockErrorHandler.Verify(x => x.Error(It.IsAny<string>()), Times.Never);
            _mockErrorHandler.Verify(x => x.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            _mockErrorHandler.Verify(x => x.Error(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<ErrorCode>()), Times.Never);
        }
    }
}
