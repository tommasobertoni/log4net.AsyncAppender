using System;
using log4net.AsyncAppender;

namespace Tests
{
    internal class MockAsyncAppenderConfigurator : IAsyncAppenderConfigurator
    {
        public int InvocationsCount { get; private set; }

        public bool Throw { get; set; } = false;

        public void Configure(AsyncAppender appender)
        {
            if (Throw)
                throw new Exception();

            InvocationsCount++;
        }
    }
}
