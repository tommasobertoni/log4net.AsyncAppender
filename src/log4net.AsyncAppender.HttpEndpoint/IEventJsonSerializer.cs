using log4net.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace log4net.AsyncAppender
{
    public interface IEventJsonSerializer
    {
        string SerializeToJson(LoggingEvent loggingEvent);
    }
}
