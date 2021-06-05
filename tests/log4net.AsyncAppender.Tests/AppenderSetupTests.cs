using Xunit;
using Xunit.Abstractions;
using static Tests.MockFactory;

namespace Tests
{
    public class AppenderSetupTests
    {
        private readonly TestableAsyncAppender _appender;
        private readonly MockErrorHandler _eh;

        public AppenderSetupTests(ITestOutputHelper testOutputHelper)
        {
            var (appender, eh) = GetAnAppenderWithErrorHandler(testOutputHelper);
            _appender = appender;
            _eh = eh;
        }

        [Fact]
        public void Default_configuration_is_valid()
        {
            Assert.True(_appender.ValidateSelf());
        }

        [Fact]
        public void Max_concurrent_processors_count_validation()
        {
            Assert.True(_appender.MaxConcurrentProcessorsCount > 0);

            _appender.MaxConcurrentProcessorsCount = 0;
            Assert.False(_appender.ValidateSelf());
            Assert.Equal(1, _eh.ErrorsCount);

            _appender.MaxConcurrentProcessorsCount = -1;
            Assert.False(_appender.ValidateSelf());
            Assert.Equal(2, _eh.ErrorsCount);

            _appender.MaxConcurrentProcessorsCount = 1;
            Assert.True(_appender.ValidateSelf());
            Assert.Equal(2, _eh.ErrorsCount);
        }

        [Fact]
        public void Max_batch_size_validation()
        {
            Assert.True(_appender.MaxBatchSize > 0);

            _appender.MaxBatchSize = 0;
            Assert.False(_appender.ValidateSelf());
            Assert.Equal(1, _eh.ErrorsCount);

            _appender.MaxBatchSize = -1;
            Assert.False(_appender.ValidateSelf());
            Assert.Equal(2, _eh.ErrorsCount);

            _appender.MaxBatchSize = 1;
            Assert.True(_appender.ValidateSelf());
            Assert.Equal(2, _eh.ErrorsCount);
        }

        [Fact]
        public void Close_timeout_millis_validation()
        {
            Assert.True(_appender.CloseTimeoutMillis > 0);

            _appender.CloseTimeoutMillis = 0;
            Assert.False(_appender.ValidateSelf());
            Assert.Equal(1, _eh.ErrorsCount);

            _appender.CloseTimeoutMillis = -1;
            Assert.False(_appender.ValidateSelf());
            Assert.Equal(2, _eh.ErrorsCount);

            _appender.CloseTimeoutMillis = 1;
            Assert.True(_appender.ValidateSelf());
            Assert.Equal(2, _eh.ErrorsCount);
        }
    }
}
