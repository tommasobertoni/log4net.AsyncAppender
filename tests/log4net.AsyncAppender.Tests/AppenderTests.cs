using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static Tests.MockFactory;

namespace Tests
{
    public class AppenderTests
    {
        private readonly TestableAsyncAppender _appender;

        public AppenderTests(ITestOutputHelper testOutputHelper)
        {
            _appender = GetAnAppender(testOutputHelper);
        }

        [Fact]
        public void Activated_appender_accepts_events()
        {
            Assert.False(_appender.Activated);
            Assert.False(_appender.AcceptsLoggingEvents);

            _appender.ActivateOptions();

            Assert.True(_appender.Activated);
            Assert.True(_appender.AcceptsLoggingEvents);

            _appender.Close();
        }

        [Fact]
        public void Appender_with_invalid_configuration_does_not_accept_events()
        {
            Assert.False(_appender.Activated);
            Assert.False(_appender.AcceptsLoggingEvents);

            _appender.MaxConcurrentProcessorsCount = -1;

            _appender.ActivateOptions();

            Assert.False(_appender.Activated);
            Assert.False(_appender.AcceptsLoggingEvents);

            _appender.Close();
        }

        [Fact]
        public void Closed_appender_does_not_accept_events()
        {
            Assert.False(_appender.Activated);
            Assert.False(_appender.AcceptsLoggingEvents);

            _appender.ActivateOptions();

            Assert.True(_appender.Activated);
            Assert.True(_appender.AcceptsLoggingEvents);

            _appender.Close();

            Assert.False(_appender.Activated);
            Assert.False(_appender.AcceptsLoggingEvents);
        }

        [Fact]
        public async Task Appender_processes_one_event_at_a_time()
        {
            _appender.MaxBatchSize = 1;
            _appender.MaxConcurrentProcessorsCount = 5;

            await RunFullAppenderTestAsync(
                _appender,
                logsCount: 51,
                expectedProcessAsyncInvocationsCount: 51);

            _appender.Close();
        }

        [Fact]
        public async Task Appender_processes_in_batches()
        {
            _appender.MaxBatchSize = 5;
            _appender.MaxConcurrentProcessorsCount = 4;

            await RunFullAppenderTestAsync(
                _appender,
                logsCount: 21,
                expectedProcessAsyncInvocationsCount: 5);

            _appender.Close();
        }

        [Fact]
        public async Task Appender_processes_in_batches_with_one_processor()
        {
            _appender.MaxBatchSize = 5;
            _appender.MaxConcurrentProcessorsCount = 1;

            await RunFullAppenderTestAsync(
                _appender,
                logsCount: 11,
                expectedProcessAsyncInvocationsCount: 3);

            _appender.Close();
        }

        [Fact]
        public async Task Appender_closes()
        {
            _appender.MaxBatchSize = 5;
            _appender.MaxConcurrentProcessorsCount = 1;

            await RunFullAppenderTestAsync(
                _appender,
                logsCount: 11,
                expectedProcessAsyncInvocationsCount: 3);

            _appender.Close();

            Assert.False(_appender.IsProcessing);
            Assert.False(_appender.Activated);
            Assert.False(_appender.AcceptsLoggingEvents);
        }

        private async Task RunFullAppenderTestAsync(
            TestableAsyncAppender _appender,
            int logsCount,
            long expectedProcessAsyncInvocationsCount)
        {
            _appender.ActivateOptions();

            Assert.True(_appender.Activated);
            Assert.True(_appender.AcceptsLoggingEvents);

            var processingStartedTask = _appender.ProcessingStarted();
            Assert.NotNull(processingStartedTask);
            Assert.False(processingStartedTask.IsCompleted);

            var @event = new log4net.Core.LoggingEvent(new log4net.Core.LoggingEventData());
            for (int i = 0; i < logsCount; i++)
                _appender.Append(@event);

            var startTimeoutTask = Task.Delay(100 * (Debugger.IsAttached ? 1000 : 1));
            var completedStartTask = await Task.WhenAny(startTimeoutTask, processingStartedTask);
            if (completedStartTask == startTimeoutTask) throw new Exception("Start timed out");

            var processingTerminatedTask = _appender.ProcessingTerminated();
            Assert.NotNull(processingTerminatedTask);

            var stopTimeoutTask = Task.Delay(100 * (Debugger.IsAttached ? 1000 : 1));
            var completedTerminationTask = await Task.WhenAny(stopTimeoutTask, processingTerminatedTask);
            if (completedTerminationTask == stopTimeoutTask) throw new Exception("Stop timed out");

            _appender.Close();

            Assert.Equal(logsCount, _appender.ProcessedEventsCount);
            Assert.Equal(expectedProcessAsyncInvocationsCount, _appender.ProcessAsyncInvocationsCount);
        }
    }
}
