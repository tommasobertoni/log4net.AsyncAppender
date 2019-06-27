using log4net.AsyncAppender;
using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    internal class MockAsyncAppenderConfigurator : IAsyncAppenderConfigurator
    {
        public int InvocationsCount { get; private set; }

        public bool Throw { get; set; } = false;

        public void Configure(AsyncAppender appender)
        {
            if (this.Throw)
                throw new Exception();

            this.InvocationsCount++;
        }
    }

    internal class ChangeAppenderNameConfigurator : IAsyncAppenderConfigurator
    {
        public void Configure(AsyncAppender appender) => appender.Name = Guid.NewGuid().ToString();
    }
}
