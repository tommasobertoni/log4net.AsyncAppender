using log4net.Core;
using System.Linq;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace log4net.Elasticsearch.Async.Helpers
{
    internal class TestReport
    {
        public int JsonSerializationsCount { get; set; }

        public int ErrorsCount { get; set; }

        public int HttpCallsCount { get; set; }

        public List<int> HttpCallsBatchSizes { get; set; }
    }

    internal class TestToolbox
    {
        public ElasticsearchAsyncAppender Appender { get; private set; }

        public int LogsCount { get; set; }

        private readonly ITestOutputHelper _output;

        private readonly ILog _log;
        private readonly Mock<IErrorHandler> _mockErrorHandler;
        private readonly Mock<IEventJsonSerializer> _mockEventJsonSerializer;
        private readonly Mock<HttpClientHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly ConcurrentQueue<int> _httpCallsBatchSizes = new ConcurrentQueue<int>();

        public TestToolbox(ILog log, ITestOutputHelper output)
        {
            _log = log;
            _output = output;

            LogsCount = 0;

            #region Setup

            // EventJsonSerializer

            var ejs = new EventJsonSerializer();
            _mockEventJsonSerializer = new Mock<IEventJsonSerializer>();
            _mockEventJsonSerializer
                .Setup(x => x.SerializeToJson(It.IsAny<LoggingEvent>()))
                .Returns<LoggingEvent>(e => ejs.SerializeToJson(e));

            // TraceErrorHandler

            var trace = new TraceErrorHandler();
            _mockErrorHandler = new Mock<IErrorHandler>();

            _mockErrorHandler
                .Setup(x => x.Error(It.IsAny<string>()))
                .Callback<string>(message =>
                {
                    _output.WriteLine($"[ERROR] {message}");
                    trace.Error(message);
                });

            _mockErrorHandler
                .Setup(x => x.Error(It.IsAny<string>(), It.IsAny<Exception>()))
                .Callback<string, Exception>((message, ex) =>
                {
                    _output.WriteLine($"[ERROR] {message} {ex}");
                    trace.Error(message, ex);
                });

            _mockErrorHandler
                .Setup(x => x.Error(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<ErrorCode>()))
                .Callback<string, Exception, ErrorCode>((message, ex, code) =>
                {
                    _output.WriteLine($"[ERROR] {code} {message} {ex}");
                    trace.Error(message);
                });

            // HttpClient

            _mockHttpMessageHandler = new Mock<HttpClientHandler>();
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                ).Callback<HttpRequestMessage, CancellationToken>((request, token) =>
                {
                    var stringContent = request.Content as StringContent;
                    var content = stringContent.ReadAsStringAsync().Result;
                    var rows = content.Split(Environment.NewLine);

                    var actualLogs = rows.Where(r => r != "{\"index\" : {} }" && !string.IsNullOrWhiteSpace(r)).ToList();
                    _httpCallsBatchSizes.Enqueue(actualLogs.Count);

                }).ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StringContent(string.Empty)
                }).Verifiable();

            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);

            #endregion
        }

        public TestReport GetReport()
        {
            return new TestReport
            {
                JsonSerializationsCount = _mockEventJsonSerializer.Invocations.Count,
                ErrorsCount = _mockErrorHandler.Invocations.Count,
                HttpCallsCount = _mockHttpMessageHandler.Invocations.Count,
                HttpCallsBatchSizes = _httpCallsBatchSizes.ToList(),
            };
        }

        public void ReplaceConfiguredAppenderWithTestAppender(int processorsCount)
        {
            var appender = new ElasticsearchAsyncAppender
            {
                MaxProcessorsCount = processorsCount,
                MaxBatchSize = 512,
                CloseTimeoutMillis = 5000,
                EventJsonSerializer = _mockEventJsonSerializer.Object,
                ConnectionString = "Scheme=http;User=me;Pwd=pass;Server=myServer.com;Port=9000;Index=anIndex;Routing=aRoute;rolling=true",
                ErrorHandler = _mockErrorHandler.Object,
                HttpClient = _httpClient,
            };

            appender.ActivateOptions();

            Assert.True(appender.Initialized);
            Assert.True(appender.AcceptsLoggingEvents);
            Assert.False(appender.IsProcessing);
            Assert.NotNull(appender.Settings);
            Assert.True(appender.Settings.AreValid());

            var currentAppender = _log.GetElasticsearchAppender();
            Assert.NotSame(appender, currentAppender);

            _log.SetElasticsearchAppender(appender);
            Assert.Same(appender, _log.GetElasticsearchAppender());

            currentAppender?.Close();
            this.Appender = appender;
        }

        public void VerifyLogsCount()
        {
            _mockEventJsonSerializer.Verify(x =>
                x.SerializeToJson(It.IsAny<LoggingEvent>()),
                Times.Exactly(LogsCount));

            var httpCalls = (LogsCount / Appender.MaxBatchSize) + 1;
            _mockHttpMessageHandler.Protected().Verify<Task<HttpResponseMessage>>(
                "SendAsync",
                Times.AtLeast(httpCalls),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        public void VerifyPartialLogsCount(bool allowZeroHttpCalls = false)
        {
            // Serialization is called at most n times, and should have been called at least one.
            _mockEventJsonSerializer.Verify(x =>
                x.SerializeToJson(It.IsAny<LoggingEvent>()),
                Times.Between(1, LogsCount, Moq.Range.Inclusive));

            if (!allowZeroHttpCalls)
            {
                // Http calls can be more than expected if the logs are not completed in perfect batches (maxing out the batch size).
                var httpCalls = (LogsCount / Appender.MaxBatchSize) + 1;
                _mockHttpMessageHandler.Protected().Verify<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Times.AtLeast(1),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
            }
        }

        public void VerifyNoErrors()
        {
            _mockErrorHandler.Verify(x => x.Error(It.IsAny<string>()), Times.Never);
            _mockErrorHandler.Verify(x => x.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
            _mockErrorHandler.Verify(x => x.Error(It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<ErrorCode>()), Times.Never);
        }
    }
}
