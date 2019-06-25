using log4net.Elasticsearch.Async.Helpers;
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

            var processingStartedTask = new ProcessingStartedTask(_toolbox.Appender).AsTask();
            await processingStartedTask;

            var testTimeoutTask = GetTimeoutTask();
            var processingTerminationTask = new ProcessingTerminationTask(_toolbox.Appender).AsTask();
            var completedTask = await Task.WhenAny(testTimeoutTask, processingTerminationTask);

            if (completedTask == testTimeoutTask)
                Assert.True(false, $"Timed out.");

            _toolbox.VerifyLogsCount();
        }

        #endregion

        #region Many logs

        [Fact]
        public async Task Logs_are_processed_with_single_processor()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 1);
            var loggedEventsCount = (int)(_toolbox.Appender.MaxBatchSize * 1.2);
            await Test_Logs_are_processed(loggedEventsCount, nameof(Logs_are_processed_with_single_processor));
        }

        [Fact]
        public async Task Logs_are_processed_with_two_processors()
        {
            _toolbox.ReplaceConfiguredAppenderWithTestAppender(processorsCount: 2);
            var loggedEventsCount = (_toolbox.Appender.MaxBatchSize * 4) + 1;
            await Test_Logs_are_processed(loggedEventsCount, nameof(Logs_are_processed_with_two_processors));
        }

        [Fact]
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

            var processingStartedTask = new ProcessingStartedTask(_toolbox.Appender).AsTask();
            await processingStartedTask;

            var testTimeoutTask = GetTimeoutTask();
            var processingTerminationTask = new ProcessingTerminationTask(_toolbox.Appender).AsTask();
            var resultingTask = await Task.WhenAny(testTimeoutTask, processingTerminationTask);

            sw.Stop();

            if (resultingTask == processingTerminationTask)
            {
                _output.WriteLine($"{testName}: {loggedEventsCount} logs have been processed in: {sw.Elapsed}");
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
            await Task.Delay(_toolbox.Appender.MaxBatchSize);

            _toolbox.VerifyPartialLogsCount(allowZeroHttpCallswZero);
        }

        #endregion

        public void Dispose()
        {
            _toolbox.Appender.Close();
            _toolbox.VerifyNoErrors();

            var testReport = _toolbox.GetReport();
            _output.WriteLine($"Total json serializations: {testReport.JsonSerializationsCount}");
            //_output.WriteLine($"Total errors: {testReport.ErrorsCount}");
            _output.WriteLine($"Total http calls: {testReport.HttpCallsCount}");
            _output.WriteLine($"Http calls batch sizes: [ {string.Join(" , ", testReport.HttpCallsBatchSizes)} ]");

            Assert.Equal(testReport.HttpCallsCount, testReport.HttpCallsBatchSizes.Count);
            var totalSentEvents = testReport.HttpCallsBatchSizes.Sum(x => x);
            Assert.Equal(testReport.JsonSerializationsCount, totalSentEvents);
        }

        private Task GetTimeoutTask()
        {
            return Task.Delay(System.Diagnostics.Debugger.IsAttached ? int.MaxValue : 2_000);
        }
    }
}
