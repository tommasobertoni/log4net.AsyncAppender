using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static Tests.MockFactory;

namespace Tests
{
    public class TraceTests
    {
        private readonly TestableAsyncAppender _appender;

        public TraceTests(ITestOutputHelper testOutputHelper)
        {
            EnsureTraceListenerExists(testOutputHelper);
            _appender = GetAnAppender(testOutputHelper);
        }

        [Fact]
        public async Task No_trace_is_written_when_trace_is_false()
        {
            _appender.MaxBatchSize = 9;
            _appender.MaxConcurrentProcessorsCount = 4;
            _appender.Trace = false;

            await RunFullAppenderTestAsync(_appender, logsCount: 100);

            var testTraceListener = GetCurrentTestTraceListener();
            Assert.Equal(0, testTraceListener.WritesCount);
        }

        [Fact]
        public async Task Traces_are_written_when_trace_is_true()
        {
            _appender.MaxBatchSize = 9;
            _appender.MaxConcurrentProcessorsCount = 4;
            _appender.Trace = true;

            await RunFullAppenderTestAsync(_appender, logsCount: 100);

            var testTraceListener = GetCurrentTestTraceListener();
            Assert.NotEqual(0, testTraceListener.WritesCount);
        }

        private async Task RunFullAppenderTestAsync(TestableAsyncAppender appender, int logsCount)
        {
            appender.ActivateOptions();

            Assert.True(appender.Activated);
            Assert.True(appender.AcceptsLoggingEvents);

            var processingStartedTask = appender.ProcessingStarted();
            Assert.NotNull(processingStartedTask);
            Assert.False(processingStartedTask.IsCompleted);

            var @event = new log4net.Core.LoggingEvent(new log4net.Core.LoggingEventData());
            for (int i = 0; i < logsCount; i++)
                appender.Append(@event);

            await processingStartedTask;
            await appender.ProcessingTerminated();

            appender.Close();

            Assert.False(appender.IsProcessing);
            Assert.Equal(logsCount, appender.ProcessedEventsCount);
        }
    }
}
