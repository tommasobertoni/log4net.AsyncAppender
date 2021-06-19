using IntegrationTests.Helpers;
using log4net;
using Xunit;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config")]

namespace IntegrationTests
{
    [Collection("Integration with log4net.config")]
    public class AppenderConfigurationIntegrationTests
    {
        [Fact]
        public void Xml_configuration_is_applied_to_the_appender()
        {
            var log = LogManager.GetLogger(typeof(AppenderConfigurationIntegrationTests));
            var appender = log.GetElasticsearchAppender();

            Assert.NotNull(appender);
            Assert.True(appender.Activated);
            Assert.True(appender.AcceptsLoggingEvents);
            Assert.False(appender.IsProcessing);

            // The expected values are those written in the log4net.config file.

            Assert.Equal(10, appender.MaxConcurrentProcessorsCount);
            Assert.Equal(1024, appender.MaxBatchSize);
            Assert.Equal(15000, appender.CloseTimeoutMillis);
            Assert.True(appender.Trace);
            Assert.NotNull(appender.ErrorHandler);
            Assert.IsType<TraceErrorHandler>(appender.ErrorHandler);
            Assert.NotNull(appender.Configurator);
            Assert.IsType<TestConfigurator>(appender.Configurator);
        }
    }
}
