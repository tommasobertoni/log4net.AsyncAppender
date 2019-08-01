using log4net;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IntegrationTests.Helpers;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config")]

namespace IntegrationTests
{
    public class AppenderConfigurationIntegrationTests
    {
        [Test]
        public void XmlConfigurationIsAppliedToTheAppender()
        {
            var log = LogManager.GetLogger(typeof(AppenderConfigurationIntegrationTests));
            var appender = log.GetElasticsearchAppender();

            Assert.That(appender, Is.Not.Null);
            Assert.That(appender.Activated, Is.True);
            Assert.That(appender.AcceptsLoggingEvents, Is.True);
            Assert.That(appender.IsProcessing, Is.False);

            // The expected values are those written in the log4net.config file.

            Assert.That(appender.MaxConcurrentProcessorsCount, Is.EqualTo(10));
            Assert.That(appender.MaxBatchSize, Is.EqualTo(1024));
            Assert.That(appender.CloseTimeoutMillis, Is.EqualTo(15000));
            Assert.That(appender.Trace, Is.True);
            Assert.That(appender.ErrorHandler, Is.Not.Null);
            Assert.That(appender.ErrorHandler, Is.TypeOf<TraceErrorHandler>());
            Assert.That(appender.Configurator, Is.Not.Null);
            Assert.That(appender.Configurator, Is.TypeOf<TestConfigurator>());
        }
    }
}
