using System.Threading.Tasks;
using NUnit.Framework;
using static Tests.MockFactory;

namespace Tests
{
    public class TraceTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            EnsureTraceListenerExists();
        }

        [Test]
        public async Task NoTraceIsWrittenWhenTraceIsFalse()
        {
            var appender = GetAnAppender();
            appender.MaxBatchSize = 9;
            appender.MaxConcurrentProcessorsCount = 4;
            appender.Trace = false;

            await RunFullAppenderTestAsync(appender, logsCount: 100);

            var testTraceListener = GetCurrentTestTraceListener();
            Assert.That(testTraceListener.WritesCount, Is.Zero);
        }

        [Test]
        public async Task TracesAreWrittenWhenTraceIsTrue()
        {
            var appender = GetAnAppender();
            appender.MaxBatchSize = 9;
            appender.MaxConcurrentProcessorsCount = 4;
            appender.Trace = true;

            await RunFullAppenderTestAsync(appender, logsCount: 100);

            var testTraceListener = GetCurrentTestTraceListener();
            Assert.That(testTraceListener.WritesCount, Is.Not.Zero);
        }

        private async Task RunFullAppenderTestAsync(TestableAsyncAppender appender, int logsCount)
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

            await processingStartedTask;
            await appender.ProcessingTerminated();

            appender.Close();

            Assert.That(appender.IsProcessing, Is.False);
            Assert.That(appender.ProcessedEventsCount, Is.EqualTo(logsCount));
        }
    }
}
