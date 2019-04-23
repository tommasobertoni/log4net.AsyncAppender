using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace log4net.Elasticsearch.Async.IntegrationTests
{
    [Collection("Appender integration tests collection")]
    public class AppenderIntegrationTests : IDisposable
    {
        private readonly ILog _log;
        private readonly ITestOutputHelper _output;
        private readonly TestToolbox _toolbox;

        public AppenderIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _log = LogManager.GetLogger(typeof(AppenderIntegrationTests));
            _toolbox = new TestToolbox(_log, output);
        }

        #region Single log

        [Fact]
        public async Task Log_is_processed_with_single_processor()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 1);
            await Test_Log_is_processed();
        }

        [Fact]
        public async Task Log_is_processed_with_two_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 2);
            await Test_Log_is_processed();
        }

        [Fact]
        public async Task Log_is_processed_with_many_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 10);
            await Test_Log_is_processed();
        }

        private async Task Test_Log_is_processed()
        {
            _log.Info("test");
            _toolbox.LogsCount += 1;

            var testTimeoutTask = Task.Delay(5000);
            var processingTerminationTask = new ProcessingTerminationTask(_toolbox.Appender).AsTask();
            var completedTask = await Task.WhenAny(testTimeoutTask, processingTerminationTask);

            if (completedTask == testTimeoutTask)
                Assert.True(false, $"Timed out.");
        }

        #endregion

        #region Many logs

        [Fact]
        public async Task Logs_are_processed_with_single_processor()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 1);
            var loggedEventsCount = _toolbox.Appender.MaxBatchSize + 1;
            await Test_Logs_are_processed(loggedEventsCount, nameof(Logs_are_processed_with_single_processor));
        }

        [Fact]
        public async Task Logs_are_processed_with_two_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 2);
            var loggedEventsCount = (_toolbox.Appender.MaxBatchSize * 2) + 1;
            await Test_Logs_are_processed(loggedEventsCount, nameof(Logs_are_processed_with_two_processors));
        }

        [Fact]
        public async Task Logs_are_processed_with_many_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 10);
            var loggedEventsCount = (_toolbox.Appender.MaxBatchSize * 30) + 1;
            await Test_Logs_are_processed(loggedEventsCount, nameof(Logs_are_processed_with_many_processors));
        }

        private async Task Test_Logs_are_processed(int loggedEventsCount, string testName)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < loggedEventsCount; i++) _log.Info("test");
            _toolbox.LogsCount += loggedEventsCount;

            var testTimeoutTask = Task.Delay(5000);
            var processingTerminationTask = new ProcessingTerminationTask(_toolbox.Appender).AsTask();
            var resultingTask = await Task.WhenAny(testTimeoutTask, processingTerminationTask);

            sw.Stop();

            if (resultingTask == processingTerminationTask)
            {
                _output.WriteLine($"{testName}: All {loggedEventsCount} logs have been processed in: {sw.Elapsed}");
                var efficiency = loggedEventsCount / sw.ElapsedMilliseconds;
                _output.WriteLine($"Processed ~{efficiency} logs / millisecond.");
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
            await Test_Some_logs_are_processed(allallowZeroHttpCallswZero: true);
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

        private async Task Test_Some_logs_are_processed(bool allallowZeroHttpCallswZero = false)
        {
            int n = (_toolbox.Appender.MaxBatchSize * 30) + 1;
            for (int i = 0; i < n; i++) _log.Info("test");
            _toolbox.LogsCount += n;

            // Delay exiting the test to allow some logs to be processed
            // but don't wait the full processing to complete.
            await Task.Delay(_toolbox.Appender.MaxBatchSize);

            _toolbox.VerifyPartialLogsCount(allallowZeroHttpCallswZero);
        }

        #endregion

        public void Dispose()
        {
            _toolbox.Appender.Close();
            _toolbox.VerifyNoErrors();
        }
    }
}
