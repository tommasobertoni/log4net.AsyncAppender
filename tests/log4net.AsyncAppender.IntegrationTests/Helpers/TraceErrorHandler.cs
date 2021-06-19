using System;
using System.Diagnostics;
using log4net.Core;

namespace IntegrationTests.Helpers
{
    public class TraceErrorHandler : IErrorHandler
    {
        public void Error(string message)
        {
            Console.WriteLine(message);
            Trace.WriteLine(message);
        }

        public void Error(string message, Exception ex)
        {
            var x = $"{message}: {ex}";
            Console.WriteLine(x);
            Trace.WriteLine(x);
        }

        public void Error(string message, Exception ex, ErrorCode errorCode)
        {
            var x = $"[{errorCode}] {message}: {ex}";
            Console.WriteLine(x);
            Trace.WriteLine(x);
        }
    }
}
