using NUnit.Framework;
using System.Diagnostics;
using System.Threading.Tasks;
using static Tests.MockFactory;

namespace Tests
{
    public class AppenderStateTests
    {
        [Test]
        public async Task AppenderStartsAndStops()
        {
            var appender = GetAnAppender();

            Assert.That(appender.Activated, Is.False);
            Assert.That(appender.AcceptsLoggingEvents, Is.False);

            appender.ActivateOptions();

            Assert.That(appender.Activated);
            Assert.That(appender.AcceptsLoggingEvents);

            // Appender is not processing events.
            var completedProcessingTerminatedTask = appender.ProcessingTerminated();
            Assert.That(completedProcessingTerminatedTask, Is.Not.Null);
            Assert.That(completedProcessingTerminatedTask.IsCompleted);

            var processingStartedTask = appender.ProcessingStarted();
            Assert.That(processingStartedTask, Is.Not.Null);
            Assert.That(processingStartedTask.IsCompleted == false);

            Assert.That(appender.ProcessedEventsCount, Is.Zero);

            var @event = new log4net.Core.LoggingEvent(new log4net.Core.LoggingEventData());

            appender.Append(@event);
            appender.Append(@event);

            var startTimeoutTask = Task.Delay(100 * (Debugger.IsAttached ? 1000 : 1));
            var completedStartTask = await Task.WhenAny(startTimeoutTask, processingStartedTask);
            if (completedStartTask == startTimeoutTask) Assert.Fail("Start timed out");

            var processingTerminatedTask = appender.ProcessingTerminated();
            Assert.That(processingTerminatedTask, Is.Not.Null);

            var stopTimeoutTask = Task.Delay(100 * (Debugger.IsAttached ? 1000 : 1));
            var completedTerminationTask = await Task.WhenAny(stopTimeoutTask, processingTerminatedTask);
            if (completedTerminationTask == stopTimeoutTask) Assert.Fail("Stop timed out");

            appender.Close();

            Assert.That(appender.IsProcessing, Is.False);
            Assert.That(appender.ProcessedEventsCount, Is.EqualTo(2));
        }
    }
}
