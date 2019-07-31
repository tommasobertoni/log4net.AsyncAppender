using log4net.AsyncAppender;
using System;
using System.Collections.Generic;
using System.Text;

namespace IntegrationTests.Helpers
{
    public class TestConfigurator : IAsyncAppenderConfigurator
    {
        public void Configure(AsyncAppender appender)
        {
        }
    }
}
