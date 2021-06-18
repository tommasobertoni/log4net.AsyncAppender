using System;
using System.Diagnostics;
using System.Threading.Tasks;
using log4net.Core;
using Xunit;
using Xunit.Abstractions;
using static Tests.MockFactory;

namespace Tests
{
    public class AppenderStateTests
    {
        private readonly TestableAsyncAppender _appender;

        public AppenderStateTests(ITestOutputHelper testOutputHelper)
        {
            _appender = GetAnAppender(testOutputHelper);
        }

        [Fact]
        public async Task Appender_starts_and_stops()
        {
            Assert.False(_appender.Activated);
            Assert.False(_appender.AcceptsLoggingEvents);

            _appender.ActivateOptions();

            Assert.True(_appender.Activated);
            Assert.True(_appender.AcceptsLoggingEvents);

            // _appender is not processing events.
            var completedProcessingTerminatedTask = _appender.ProcessingTerminated();
            Assert.NotNull(completedProcessingTerminatedTask);
            Assert.True(completedProcessingTerminatedTask.IsCompleted);

            var processingStartedTask = _appender.ProcessingStarted();
            Assert.NotNull(processingStartedTask);
            Assert.False(processingStartedTask.IsCompleted);

            Assert.Equal(0, _appender.ProcessedEventsCount);

            var @event = new LoggingEvent(new LoggingEventData());

            _appender.Append(@event);
            _appender.Append(@event);

            var startTimeoutTask = Task.Delay(3000);
            var completedStartTask = await Task.WhenAny(startTimeoutTask, processingStartedTask);
            if (completedStartTask == startTimeoutTask) throw new Exception("Start timed out");

            var processingTerminatedTask = _appender.ProcessingTerminated();
            Assert.NotNull(processingTerminatedTask);

            var stopTimeoutTask = Task.Delay(3000);
            var completedTerminationTask = await Task.WhenAny(stopTimeoutTask, processingTerminatedTask);
            if (completedTerminationTask == stopTimeoutTask) throw new Exception("Stop timed out");

            _appender.Close();

            Assert.False(_appender.IsProcessing);
            Assert.Equal(2, _appender.ProcessedEventsCount);
        }
    }
}
