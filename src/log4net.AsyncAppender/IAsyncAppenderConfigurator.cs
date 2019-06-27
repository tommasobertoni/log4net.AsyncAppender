using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace log4net.AsyncAppender
{
    public interface IAsyncAppenderConfigurator
    {
        void Configure(AsyncAppender appender);
    }
}
