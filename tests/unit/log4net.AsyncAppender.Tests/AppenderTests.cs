using NUnit.Framework;
using static Tests.MockFactory;

namespace Tests
{
    public class AppenderTests
    {
        [Test]
        public void ActivatedAppenderAcceptsEvents()
        {
            var appender = GetAnAppender();

            Assert.False(appender.Activated);
            Assert.False(appender.AcceptsLoggingEvents);

            appender.ActivateOptions();

            Assert.True(appender.Activated);
            Assert.True(appender.AcceptsLoggingEvents);
        }

        [Test]
        public void AppenderWithInvalidConfigurationDoesNotAcceptEvents()
        {
            var appender = GetAnAppender();

            Assert.False(appender.Activated);
            Assert.False(appender.AcceptsLoggingEvents);

            appender.MaxConcurrentProcessorsCount = -1;

            appender.ActivateOptions();

            Assert.False(appender.Activated);
            Assert.False(appender.AcceptsLoggingEvents);
        }

        [Test]
        public void ClosedAppenderDoesNotAcceptEvents()
        {
            var appender = GetAnAppender();

            Assert.False(appender.Activated);
            Assert.False(appender.AcceptsLoggingEvents);

            appender.ActivateOptions();

            Assert.True(appender.Activated);
            Assert.True(appender.AcceptsLoggingEvents);

            appender.Close();

            Assert.False(appender.Activated);
            Assert.False(appender.AcceptsLoggingEvents);
        }
    }
}
