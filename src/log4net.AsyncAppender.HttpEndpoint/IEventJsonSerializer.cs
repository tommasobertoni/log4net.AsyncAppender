using log4net.Core;

namespace log4net.AsyncAppender
{
    public interface IEventJsonSerializer
    {
        string SerializeToJson(LoggingEvent loggingEvent);
    }
}
