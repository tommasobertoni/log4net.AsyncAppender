using log4net.Core;
using System;
using System.Threading.Tasks;
using System.Linq;
using Xunit;
using Moq;
using System.Net.Http;
using Moq.Protected;
using System.Threading;
using System.Collections.Generic;

namespace log4net.Elasticsearch.Async.IntegrationTests
{
    [Collection("Appender integration tests collection")]
    public class AppenderIntegrationTests : IDisposable
    {
        private readonly ILog _log;

        private ElasticsearchAsyncAppender _appender;

        private Mock<IErrorHandler> _mockErrorHandler;
        private Mock<IEventJsonSerializer> _mockEventJsonSerializer;
        private Mock<HttpClientHandler> _mockHttpMessageHandler;

        public int _logsCount;

        public AppenderIntegrationTests()
        {
            _logsCount = 0;
            _log = LogManager.GetLogger(typeof(AppenderIntegrationTests));
            ReplaceConfiguredAppenderWithTestAppender(_log);
        }

        #region Setup

        private void ReplaceConfiguredAppenderWithTestAppender(ILog log)
        {
            var appender = new ElasticsearchAsyncAppender
            {
                ProcessorsCount = 1,
                MaxBatchSize = 512,
                CloseTimeoutMillis = 5000,
                EventJsonSerializer = TestEventJsonSerializer(),
                ConnectionString = "Scheme=http;User=me;Pwd=pass;Server=myServer.com;Port=9000;Index=anIndex;Routing=aRoute;rolling=true",
                ErrorHandler = TestErrorHandler(),
                HttpClient = TestHttpClient()
            };

            appender.ActivateOptions();

            Assert.True(appender.Initialized);
            Assert.True(appender.AcceptsLoggingEvents);
            Assert.False(appender.IsProcessing);
            Assert.NotNull(appender.Settings);
            Assert.True(appender.Settings.AreValid());

            Assert.NotSame(appender, log.GetElasticsearchAppender());
            log.SetElasticsearchAppender(appender);
            Assert.Same(appender, log.GetElasticsearchAppender());

            _appender = appender;

            // Local functions

            IEventJsonSerializer TestEventJsonSerializer()
            {
                var ejs = new EventJsonSerializer();
                _mockEventJsonSerializer = new Mock<IEventJsonSerializer>();
                _mockEventJsonSerializer
                    .Setup(x => x.SerializeToJson(It.IsAny<LoggingEvent>()))
                    .Returns<LoggingEvent>(e => ejs.SerializeToJson(e));

                return _mockEventJsonSerializer.Object;
            }

            IErrorHandler TestErrorHandler()
            {
                var trace = new TraceErrorHandler();
                _mockErrorHandler = new Mock<IErrorHandler>();

                _mockErrorHandler
                    .Setup(x => x.Error(It.IsAny<string>()))
                    .Callback<string>(message => trace.Error(message));

                _mockErrorHandler
                    .Setup(x => x.Error(It.IsAny<string>(), It.IsAny<Exception>()))
                    .Callback<string, Exception>((message, ex) => trace.Error(message, ex));

                _mockErrorHandler
                    .Setup(x => x.Error(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<ErrorCode>()))
                    .Callback<string, Exception, ErrorCode>((message, ex, code) => trace.Error(message, ex, code));

                return _mockErrorHandler.Object;
            }

            HttpClient TestHttpClient()
            {
                _mockHttpMessageHandler = new Mock<HttpClientHandler>();
                _mockHttpMessageHandler
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>()
                    ).ReturnsAsync(new HttpResponseMessage
                    {
                        StatusCode = System.Net.HttpStatusCode.OK,
                        Content = new StringContent(string.Empty)
                    }).Verifiable();

                return new HttpClient(_mockHttpMessageHandler.Object);
            }
        }

        private void AddToLogsCount(int times) => _logsCount += times;

        public void Dispose()
        {
            _appender.Close();
            VerifyLogsCount();
            VerifyNoErrors();
        }

        private void VerifyLogsCount(bool exact = false)
        {
            if (exact)
            {
                _mockEventJsonSerializer.Verify(x =>
                    x.SerializeToJson(It.IsAny<LoggingEvent>()),
                    Times.Exactly(_logsCount));

                var httpCalls = (_logsCount / _appender.MaxBatchSize) + 1;
                _mockHttpMessageHandler.Protected().Verify<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Times.Exactly(httpCalls),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
            }
            else
            {
                _mockEventJsonSerializer.Verify(x =>
                    x.SerializeToJson(It.IsAny<LoggingEvent>()),
                    Times.Between(1, _logsCount, Moq.Range.Inclusive));

                var httpCalls = (_logsCount / _appender.MaxBatchSize) + 1;
                _mockHttpMessageHandler.Protected().Verify<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Times.Between(1, httpCalls, Moq.Range.Inclusive),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
            }
        }

        private void VerifyNoErrors()
        {
            _mockErrorHandler.Verify(x => x.Error(It.IsAny<string>()), Times.Never);
            _mockErrorHandler.Verify(x => x.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            _mockErrorHandler.Verify(x => x.Error(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<ErrorCode>()), Times.Never);
        }

        #endregion

        [Fact]
        public void Log_is_processed()
        {
            _log.Info("test");
            AddToLogsCount(1);
        }

        [Fact]
        public void Logs_are_processed()
        {
            int n = 1001;
            for (int i = 0; i < n; i++) _log.Info("test");
            AddToLogsCount(n);
        }

        [Fact]
        public async Task All_logs_are_processed()
        {
            int n = (_appender.MaxBatchSize * 3) + 1;
            for (int i = 0; i < n; i++) _log.Info("test");
            AddToLogsCount(n);

            var testTimeoutTask = Task.Delay(5000);
            var processingTerminationTask = WaitForProcessingToTerminate();

            await Task.WhenAny(testTimeoutTask, processingTerminationTask);

            VerifyLogsCount(exact: true);

            // Local functions

            async Task WaitForProcessingToTerminate() => await new ProcessingTerminationTask(_appender);
        }
    }

    internal class ProcessingTerminationTask
    {
        private readonly ElasticsearchAsyncAppender _appender;

        public ProcessingTerminationTask(ElasticsearchAsyncAppender appender)
        {
            this._appender = appender;
        }

        public ProcessingTerminationAwaiter GetAwaiter() => new ProcessingTerminationAwaiter(_appender);
    }

    internal class ProcessingTerminationAwaiter : System.Runtime.CompilerServices.INotifyCompletion
    {
        private readonly ElasticsearchAsyncAppender _appender;
        private Action _continuation;

        public bool IsCompleted => !_appender.IsProcessing;

        public ProcessingTerminationAwaiter(ElasticsearchAsyncAppender appender)
        {
            _appender = appender;
        }

        public void OnCompleted(Action continuation)
        {
            if (this.IsCompleted)
                continuation();
            else
            {
                _continuation = continuation;

                Task.Run(async () =>
                {
                    while (!this.IsCompleted) 
                        await Task.Delay(100); // Check interval

                    this.Resume();
                });
            }
        }

        private void Resume() => _continuation();

        public void GetResult() { }
    }
}
