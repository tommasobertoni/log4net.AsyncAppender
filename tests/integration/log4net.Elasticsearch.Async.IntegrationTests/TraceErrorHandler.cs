using log4net.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace log4net.Elasticsearch.Async.IntegrationTests
{
    public class TraceErrorHandler : IErrorHandler
    {
        public void Error(string message)
        {
            Trace.WriteLine(message);
        }

        public void Error(string message, Exception e)
        {
            Trace.WriteLine(message);
            Trace.WriteLine(e);
        }

        public void Error(string message, Exception e, ErrorCode errorCode)
        {
            Trace.WriteLine($"{errorCode} {message}");
            Trace.WriteLine(e);
        }
    }
}
