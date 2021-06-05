using Xunit;
using Xunit.Abstractions;
using static Tests.MockFactory;

namespace Tests
{
    public class AppenderConfiguratorTests
    {
        private readonly TestableAsyncAppender _appender;
        private readonly MockErrorHandler _eh;

        public AppenderConfiguratorTests(ITestOutputHelper testOutputHelper)
        {
            var (appender, eh) = GetAnAppenderWithErrorHandler(testOutputHelper);
            _appender = appender;
            _eh = eh;
        }

        [Fact]
        public void There_is_no_default_configurator()
        {
            Assert.Null(_appender.Configurator);
        }

        [Fact]
        public void Appender_without_configurator_is_allowed()
        {
            Assert.Null(_appender.Configurator);

            _appender.Configure();

            Assert.Equal(0, _eh.ErrorsCount);
        }

        [Fact]
        public void Faulty_configurator_does_not_throw()
        {
            var configurator = GetAConfigurator();

            configurator.Throw = true;
            _appender.Configurator = configurator;

            _appender.Configure();

            Assert.Equal(1, _eh.ErrorsCount);
        }
    }
}
