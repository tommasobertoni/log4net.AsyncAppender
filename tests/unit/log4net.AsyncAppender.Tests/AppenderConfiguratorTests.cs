using NUnit.Framework;
using System;
using static Tests.MockFactory;

namespace Tests
{
    public class AppenderConfiguratorTests
    {
        [Test]
        public void ThereIsNoDefaultConfigurator()
        {
            var appender = GetAnAppender();
            Assert.Null(appender.Configurator);
        }

        [Test]
        public void AppenderWithoutConfiguratorIsAllowed()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler();
            Assert.That(appender.Configurator, Is.Null);
            Assert.That(() => appender.Configure(), Throws.Nothing);
            Assert.That(meh.ErrorsCount, Is.Zero);
        }

        [Test]
        public void FaultyConfiguratorDoesNotThrow()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler();
            var configurator = GetAConfigurator();
            configurator.Throw = true;
            appender.Configurator = configurator;
            Assert.That(() => appender.Configure(), Throws.Nothing);
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));
        }
    }
}
