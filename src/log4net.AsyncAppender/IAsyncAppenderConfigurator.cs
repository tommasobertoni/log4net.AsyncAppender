namespace log4net.AsyncAppender
{
    public interface IAsyncAppenderConfigurator
    {
        void Configure(AsyncAppender appender);
    }
}
