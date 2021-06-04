using NUnit.Framework;
using static Tests.MockFactory;

namespace Tests
{
    public class AppenderSetupTests
    {
        [Test]
        public void DefaultConfigurationIsValid()
        {
            var appender = GetAnAppender();
            Assert.That(appender.ValidateSelf());
        }

        [Test]
        public void MaxConcurrentProcessorsCountValidation()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler();
            Assert.That(appender.MaxConcurrentProcessorsCount, Is.GreaterThan(0));

            appender.MaxConcurrentProcessorsCount = 0;
            Assert.That(appender.ValidateSelf(), Is.False);
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));

            appender.MaxConcurrentProcessorsCount = -1;
            Assert.That(appender.ValidateSelf(), Is.False);
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));

            appender.MaxConcurrentProcessorsCount = 1;
            Assert.That(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));
        }

        [Test]
        public void MaxBatchSizeValidation()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler();
            Assert.That(appender.MaxBatchSize, Is.GreaterThan(0));

            appender.MaxBatchSize = 0;
            Assert.That(appender.ValidateSelf(), Is.False);
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));

            appender.MaxBatchSize = -1;
            Assert.That(appender.ValidateSelf(), Is.False);
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));

            appender.MaxBatchSize = 1;
            Assert.That(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));
        }

        [Test]
        public void CloseTimeoutMillisValidation()
        {
            var (appender, meh) = GetAnAppenderWithErrorHandler();
            Assert.That(appender.CloseTimeoutMillis, Is.GreaterThan(0));

            appender.CloseTimeoutMillis = 0;
            Assert.That(appender.ValidateSelf(), Is.False);
            Assert.That(meh.ErrorsCount, Is.EqualTo(1));

            appender.CloseTimeoutMillis = -1;
            Assert.That(appender.ValidateSelf(), Is.False);
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));

            appender.CloseTimeoutMillis = 1;
            Assert.That(appender.ValidateSelf());
            Assert.That(meh.ErrorsCount, Is.EqualTo(2));
        }
    }
}
