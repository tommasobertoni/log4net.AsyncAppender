using System;
using System.Threading.Tasks;
using IntegrationTests.Helpers;
using log4net;
using Xunit;
using Xunit.Abstractions;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config")]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace IntegrationTests
{
    public class AppenderIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly ILog _log;
        private readonly TestToolbox _toolbox;

        public AppenderIntegrationTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _log = LogManager.GetLogger(typeof(AppenderIntegrationTests));
            _toolbox = new TestToolbox(_log, testOutputHelper.WriteLine);
        }

        #region Single log

        [Fact]
        public async Task Single_log_is_processed_with_single_processor()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 1);
            await Test_Log_is_processed();
        }

        [Fact]
        public async Task Single_log_is_processed_with_two_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 2);
            await Test_Log_is_processed();
        }

        [Fact]
        public async Task Single_log_is_processed_with_many_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 10);
            await Test_Log_is_processed();
        }

        private async Task Test_Log_is_processed()
        {
            _log.Info("test");
            _toolbox.LogsCount += 1;

            await _toolbox.Appender.ProcessingStarted();

            var testTimeoutTask = Task.Delay(3000);
            var processingTerminationTask = _toolbox.Appender.ProcessingTerminated();
            var completedTask = await Task.WhenAny(testTimeoutTask, processingTerminationTask);

            if (completedTask == testTimeoutTask)
                Assert.True(false, $"Timed out.");

            _toolbox.VerifyLogsCount();
        }

        #endregion

        #region Many logs

        [Fact]
        public async Task Many_logs_are_processed_with_single_processor()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 1);
            var loggedEventsCount = (int)(_toolbox.Appender.MaxBatchSize * 1.2);
            await Test_Logs_are_processed(loggedEventsCount, nameof(Many_logs_are_processed_with_single_processor));
        }

        [Fact]
        public async Task Many_logs_are_processed_with_two_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 2);
            var loggedEventsCount = (_toolbox.Appender.MaxBatchSize * 4) + 1;
            await Test_Logs_are_processed(loggedEventsCount, nameof(Many_logs_are_processed_with_two_processors));
        }

        [Fact]
        public async Task Many_logs_are_processed_with_many_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 100);
            var loggedEventsCount = (_toolbox.Appender.MaxBatchSize * 50) + 1;
            await Test_Logs_are_processed(loggedEventsCount, nameof(Many_logs_are_processed_with_many_processors));
        }

        private async Task Test_Logs_are_processed(int loggedEventsCount, string testName)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < loggedEventsCount; i++) _log.Info("test");
            _toolbox.LogsCount += loggedEventsCount;

            await _toolbox.Appender.ProcessingStarted();

            var testTimeoutTask = Task.Delay(3000);
            var processingTerminationTask = _toolbox.Appender.ProcessingTerminated();
            var resultingTask = await Task.WhenAny(testTimeoutTask, processingTerminationTask);

            sw.Stop();

            if (resultingTask == processingTerminationTask)
            {
                _testOutputHelper.WriteLine($"{testName}: {loggedEventsCount} logs have been processed in: {sw.Elapsed}");
                if (sw.ElapsedMilliseconds > 0)
                {
                    var efficiency = loggedEventsCount / sw.ElapsedMilliseconds;
                    _testOutputHelper.WriteLine($"Processed ~{efficiency} logs / millisecond.");
                }
                else
                {
                    _testOutputHelper.WriteLine("Elapsed 0 milliseconds.");
                }
            }
            else Assert.True(false, $"Timed out.");

            _toolbox.VerifyLogsCount();
        }

        #endregion

        #region Some logs

        [Fact]
        public async Task Some_logs_are_processed_with_single_processor()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 1);
            // Only in this case, with only one processor, the logs are usually too many to be serialized
            // and sent before the test finishes.
            await Test_Some_logs_are_processed(allowZeroHttpCallswZero: true);
        }

        [Fact]
        public async Task Some_logs_are_processed_with_two_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 2);
            await Test_Some_logs_are_processed();
        }

        [Fact]
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
            await Task.Yield();

            _toolbox.VerifyPartialLogsCount(allowZeroHttpCallswZero);
        }

        #endregion

        public void Dispose()
        {
            _toolbox.Appender.Close();
            _toolbox.VerifyNoErrors();
        }
    }
}
