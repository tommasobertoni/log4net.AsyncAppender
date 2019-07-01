using log4net.AsyncAppender;
using log4net.Core;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    public class TestableHttpEndpointAsyncAppender : HttpEndpointAsyncAppender
    {
        public int ProcessAsyncInvocationsCount { get; private set; }

        public TestableHttpEndpointAsyncAppender(bool autoConfigure = true)
        {
            if (autoConfigure)
                this.Configure();
        }

        protected override Task ProcessAsync(List<LoggingEvent> events, CancellationToken cancellationToken)
        {
            this.ProcessAsyncInvocationsCount++;
            return Task.CompletedTask;
        }

        protected override Task<HttpContent> GetHttpContentAsync(List<LoggingEvent> events) => null;

        public new void Configure() => base.Configure();

        public new bool ValidateSelf() => base.ValidateSelf();
    }
}
