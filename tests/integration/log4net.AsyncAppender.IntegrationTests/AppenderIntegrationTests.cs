using IntegrationTests.Helpers;
using log4net;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IntegrationTests
{
    public class AppenderIntegrationTests
    {
        private ILog _log;
        private TestToolbox _toolbox;

        [SetUp]
        public void SetUp()
        {
            _log = LogManager.GetLogger(typeof(AppenderIntegrationTests));
            _toolbox = new TestToolbox(_log, TestContext.Out.WriteLine);
        }

        #region Single log

        [Test]
        public async Task LogIsProcessedWithSingleProcessor()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 1);
            await Test_Log_is_processed();
        }

        [Test]
        public async Task Log_is_processed_with_two_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 2);
            await Test_Log_is_processed();
        }

        [Test]
        public async Task Log_is_processed_with_many_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 10);
            await Test_Log_is_processed();
        }

        private async Task Test_Log_is_processed()
        {
            _log.Info("test");
            _toolbox.LogsCount += 1;

            await _toolbox.Appender.ProcessingStarted();

            var testTimeoutTask = GetTimeoutTask();
            var processingTerminationTask = _toolbox.Appender.ProcessingTerminated();
            var completedTask = await Task.WhenAny(testTimeoutTask, processingTerminationTask);

            if (completedTask == testTimeoutTask)
                Assert.True(false, $"Timed out.");

            _toolbox.VerifyLogsCount();
        }

        #endregion

        #region Many logs

        [Test]
        public async Task Logs_are_processed_with_single_processor()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 1);
            var loggedEventsCount = (int)(_toolbox.Appender.MaxBatchSize * 1.2);
            await Test_Logs_are_processed(loggedEventsCount, nameof(Logs_are_processed_with_single_processor));
        }

        [Test]
        public async Task Logs_are_processed_with_two_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 2);
            var loggedEventsCount = (_toolbox.Appender.MaxBatchSize * 4) + 1;
            await Test_Logs_are_processed(loggedEventsCount, nameof(Logs_are_processed_with_two_processors));
        }

        [Test]
        public async Task Logs_are_processed_with_many_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 100);
            var loggedEventsCount = (_toolbox.Appender.MaxBatchSize * 50) + 1;
            await Test_Logs_are_processed(loggedEventsCount, nameof(Logs_are_processed_with_many_processors));
        }

        private async Task Test_Logs_are_processed(int loggedEventsCount, string testName)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < loggedEventsCount; i++) _log.Info("test");
            _toolbox.LogsCount += loggedEventsCount;

            await _toolbox.Appender.ProcessingStarted();

            var testTimeoutTask = GetTimeoutTask();
            var processingTerminationTask = _toolbox.Appender.ProcessingTerminated();
            var resultingTask = await Task.WhenAny(testTimeoutTask, processingTerminationTask);

            sw.Stop();

            if (resultingTask == processingTerminationTask)
            {
                TestContext.Out.WriteLine($"{testName}: {loggedEventsCount} logs have been processed in: {sw.Elapsed}");
                if (sw.ElapsedMilliseconds > 0)
                {
                    var efficiency = loggedEventsCount / sw.ElapsedMilliseconds;
                    TestContext.Out.WriteLine($"Processed ~{efficiency} logs / millisecond.");
                }
                else
                {
                    TestContext.Out.WriteLine("Elapsed 0 milliseconds.");
                }
            }
            else Assert.True(false, $"Timed out.");

            _toolbox.VerifyLogsCount();
        }

        #endregion

        #region Some logs

        [Test]
        public async Task Some_logs_are_processed_with_single_processor()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 1);
            // Only in this case, with only one processor, the logs are usually too many to be serialized
            // and sent before the test finishes.
            await Test_Some_logs_are_processed(allowZeroHttpCallswZero: true);
        }

        [Test]
        public async Task Some_logs_are_processed_with_two_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 2);
            await Test_Some_logs_are_processed();
        }

        [Test]
        public async Task Some_logs_are_processed_with_many_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 10);
            await Test_Some_logs_are_processed();
        }

        private async Task Test_Some_logs_are_processed(bool allowZeroHttpCallswZero = false)
        {
            int n = (_toolbox.Appender.MaxBatchSize * 30) + 1;
            for (int i = 0; i < n; i++) _log.Info("test");
            _toolbox.LogsCount += n;

            // Delay exiting the test to allow some logs to be processed
            // but don't wait the full processing to complete.
            await Task.Delay(_toolbox.Appender.MaxBatchSize);

            _toolbox.VerifyPartialLogsCount(allowZeroHttpCallswZero);
        }

        #endregion

        [TearDown]
        public void TearDown()
        {
            _toolbox.Appender.Close();
            _toolbox.VerifyNoErrors();
        }

        private Task GetTimeoutTask()
        {
            return Task.Delay(System.Diagnostics.Debugger.IsAttached ? int.MaxValue : 2_000);
        }
    }
}
