using log4net.Elasticsearch.Async.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config")]

namespace log4net.Elasticsearch.Async.IntegrationTests
{
    public class AppenderConfigurationIntegrationTests
    {
        [Fact]
        public void Xml_configuration_is_applied_to_the_appender()
        {
            var log = LogManager.GetLogger(typeof(AppenderConfigurationIntegrationTests));
            var appender = log.GetElasticsearchAppender();

            Assert.NotNull(appender);
            Assert.NotNull(appender.Settings);
            Assert.True(appender.Settings.AreValid());
            Assert.True(appender.Initialized);
            Assert.True(appender.AcceptsLoggingEvents);
            Assert.False(appender.IsProcessing);

            // The expected values are those written in the log4net.config file.

            var cs = "Scheme=http;User=me;Pwd=pass;Server=myServer.com;Port=9000;Index=anIndex;Routing=aRoute;rolling=true";
            var ejs = "log4net.Elasticsearch.Async.Helpers.EventJsonSerializer, log4net.Elasticsearch.Async.IntegrationTests";

            Assert.Equal(10, appender.ProcessorsCount);
            Assert.Equal(1024, appender.MaxBatchSize);
            Assert.Equal(15000, appender.CloseTimeoutMillis);
            Assert.NotNull(appender.ErrorHandler);
            Assert.IsType<TraceErrorHandler>(appender.ErrorHandler);

            Assert.Null(appender.EventJsonSerializer);
            Assert.Null(appender.EventJsonSerializerDelegate);
            Assert.Null(appender.EventJsonSerializerType);
            Assert.Equal(ejs, appender.EventJsonSerializerAssemblyQualifiedName);
            Assert.Null(appender.AppenderConfigurator);
            Assert.Null(appender.AppenderConfiguratorDelegate);
            Assert.Null(appender.AppenderConfiguratorType);
            // Do not set a real configurator, because it could override the xml configs.
            Assert.Equal("-configurator-", appender.AppenderConfiguratorAssemblyQualifiedName);
            Assert.Equal(cs, appender.ConnectionString);

            var settings = appender.Settings;
            Assert.Equal("http", settings.Scheme);
            Assert.Equal("me", settings.User);
            Assert.Equal("pass", settings.Password);
            Assert.Equal("myServer.com", settings.Server);
            Assert.Equal("9000", settings.Port);
            Assert.Equal("anIndex", settings.Index);
            Assert.Equal("aRoute", settings.Routing);
            Assert.True(settings.IsRollingIndex);
        }
    }
}
