using log4net.Appender;
using log4net.Core;
using log4net.Repository.Hierarchy;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq;
using Xunit;
using Moq;
using System.Net.Http;
using Moq.Protected;
using System.Threading;
using System.Collections.Generic;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config")]

namespace log4net.Elasticsearch.Async.IntegrationTests
{
    class AppenderConfigurator : IElasticsearchAsyncAppenderConfigurator
    {
        public static Dictionary<ElasticsearchAsyncAppender, AppenderConfigurator> Configurators =
            new Dictionary<ElasticsearchAsyncAppender, AppenderConfigurator>();

        public Mock<IEventJsonSerializer> MockEventJsonSerializer;
        public Mock<HttpClientHandler> MockHttpMessageHandler;

        void IElasticsearchAsyncAppenderConfigurator.Configure(ElasticsearchAsyncAppender appender)
        {
            var ejs = new EventJsonSerializer();
            MockEventJsonSerializer = new Mock<IEventJsonSerializer>();
            MockEventJsonSerializer
                .Setup(x => x.SerializeToJson(It.IsAny<LoggingEvent>()))
                .Returns<LoggingEvent>(e => ejs.SerializeToJson(e));
            appender.EventJsonSerializer = MockEventJsonSerializer.Object;

            MockHttpMessageHandler = new Mock<HttpClientHandler>();
            MockHttpMessageHandler
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

            appender.HttpClient = new HttpClient(MockHttpMessageHandler.Object);

            // Save the current configurator in order to be accessible by the tests.
            Configurators[appender] = this;
        }
    }

    public class ElasticsearchAsyncAppenderIntegrationTests : IDisposable
    {
        private readonly ILog _log;
        public int _logsCount;

        public ElasticsearchAsyncAppenderIntegrationTests()
        {
            // RegisterAppDomainEvents();
            // This doesn't work in the test project,
            // therefore the shutdown is registered on test termination, in the Dispose method.

            _log = LogManager.GetLogger(typeof(ElasticsearchAsyncAppenderIntegrationTests));
            _logsCount = 0;

            var appender = GetAppender();

            if (!appender.Initialized)
                appender.ActivateOptions();
        }

        #region Setup

        private ElasticsearchAsyncAppender GetAppender() =>
            _log.Logger.Repository.GetAppenders().FirstOrDefault(a => a is ElasticsearchAsyncAppender) as ElasticsearchAsyncAppender;

        private void AddToLogsCount(int times) => _logsCount += times;

        public void Dispose()
        {
            var appender = GetAppender();

            //_log.Logger.Repository.Shutdown();
            appender.Close();

            VerifyLogsCount(appender);
        }

        private void VerifyLogsCount(ElasticsearchAsyncAppender appender)
        {
            var configurator = AppenderConfigurator.Configurators[appender];

            configurator.MockEventJsonSerializer.Verify(x =>
                x.SerializeToJson(It.IsAny<LoggingEvent>()),
                Times.Between(1, _logsCount, Moq.Range.Inclusive));

            var httpCalls = (_logsCount & appender.MaxBatchSize) + 1;
            configurator.MockHttpMessageHandler.Protected().Verify<Task<HttpResponseMessage>>(
                "SendAsync",
                Times.Between(1, httpCalls, Moq.Range.Inclusive),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        private void RegisterAppDomainEvents()
        {
            /*
             * The .NET standard implementation of log4net doesn't attach to the AppDomain events
             * for shutdown, because it targets netstandard1.3 and AppDomain was reintroduced in netstandard2.0.
             * Manual shutdown must be set in place, in order to ensure finalization of the appenders (expecially the async ones).
             * 
             * ref: https://github.com/apache/logging-log4net/blob/master/src/Core/LoggerManager.cs#L167
             */

            void shutdown(object e, EventArgs o) => _log?.Logger.Repository?.Shutdown();

            // ProcessExit seems to be fired if we are part of the default domain
            AppDomain.CurrentDomain.ProcessExit += shutdown;

            // Otherwise DomainUnload is fired
            AppDomain.CurrentDomain.DomainUnload += shutdown;
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
    }
}
