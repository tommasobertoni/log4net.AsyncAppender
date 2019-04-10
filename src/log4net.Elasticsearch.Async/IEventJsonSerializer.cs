using log4net.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace log4net.Elasticsearch.Async
{
    public interface IEventJsonSerializer
    {
        string SerializeToJson(LoggingEvent loggingEvent);
    }
}
