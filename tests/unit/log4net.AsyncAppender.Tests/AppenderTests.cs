using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using static Tests.MockFactory;

namespace Tests
{
    public class AppenderTests
    {
        [Test]
        public void ActivatedAppenderAcceptsEvents()
        {
            var appender = GetAnAppender();

            Assert.That(appender.Activated, Is.False);
            Assert.That(appender.AcceptsLoggingEvents, Is.False);

            appender.ActivateOptions();

            Assert.That(appender.Activated);
            Assert.That(appender.AcceptsLoggingEvents);

            appender.Close();
        }

        [Test]
        public void AppenderWithInvalidConfigurationDoesNotAcceptEvents()
        {
            var appender = GetAnAppender();

            Assert.That(appender.Activated, Is.False);
            Assert.That(appender.AcceptsLoggingEvents, Is.False);

            appender.MaxConcurrentProcessorsCount = -1;

            appender.ActivateOptions();

            Assert.That(appender.Activated, Is.False);
            Assert.That(appender.AcceptsLoggingEvents, Is.False);

            appender.Close();
        }

        [Test]
        public void ClosedAppenderDoesNotAcceptEvents()
        {
            var appender = GetAnAppender();

            Assert.That(appender.Activated, Is.False);
            Assert.That(appender.AcceptsLoggingEvents, Is.False);

            appender.ActivateOptions();

            Assert.That(appender.Activated);
            Assert.That(appender.AcceptsLoggingEvents);

            appender.Close();

            Assert.That(appender.Activated, Is.False);
            Assert.That(appender.AcceptsLoggingEvents, Is.False);
        }

        [Test]
        public async Task AppenderProcessesOneEventAtATime()
        {
            var appender = GetAnAppender();
            appender.MaxBatchSize = 1;
            appender.MaxConcurrentProcessorsCount = 5;

            await RunFullAppenderTestAsync(
                appender,
                logsCount: 51,
                expectedProcessAsyncInvocationsCount: 51);

            appender.Close();
        }

        [Test]
        public async Task AppenderProcessesInBatches()
        {
            var appender = GetAnAppender();
            appender.MaxBatchSize = 5;
            appender.MaxConcurrentProcessorsCount = 4;

            await RunFullAppenderTestAsync(
                appender,
                logsCount: 21,
                expectedProcessAsyncInvocationsCount: 5);

            appender.Close();
        }

        [Test]
        public async Task AppenderProcessesInBatchesWithOneProcessor()
        {
            var appender = GetAnAppender();
            appender.MaxBatchSize = 5;
            appender.MaxConcurrentProcessorsCount = 1;

            await RunFullAppenderTestAsync(
                appender,
                logsCount: 11,
                expectedProcessAsyncInvocationsCount: 3);

            appender.Close();
        }

        private async Task RunFullAppenderTestAsync(
            TestableAsyncAppender appender,
            int logsCount,
            long expectedProcessAsyncInvocationsCount)
        {
            appender.ActivateOptions();

            Assert.That(appender.Activated);
            Assert.That(appender.AcceptsLoggingEvents);

            var processingStartedTask = appender.ProcessingStarted();
            Assert.That(processingStartedTask, Is.Not.Null);
            Assert.That(processingStartedTask.IsCompleted == false);

            var @event = new log4net.Core.LoggingEvent(new log4net.Core.LoggingEventData());
            for (int i = 0; i < logsCount; i++)
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

            Assert.That(appender.ProcessedEventsCount, Is.EqualTo(logsCount));
            Assert.That(appender.ProcessAsyncInvocationsCount, Is.EqualTo(expectedProcessAsyncInvocationsCount));
        }

        [Test]
        public async Task AppenderCloses()
        {
            var appender = GetAnAppender();
            appender.MaxBatchSize = 5;
            appender.MaxConcurrentProcessorsCount = 1;

            await RunFullAppenderTestAsync(
                appender,
                logsCount: 11,
                expectedProcessAsyncInvocationsCount: 3);

            appender.Close();

            Assert.That(appender.IsProcessing, Is.False);
            Assert.That(appender.Activated, Is.False);
            Assert.That(appender.AcceptsLoggingEvents, Is.False);
        }
    }
}
