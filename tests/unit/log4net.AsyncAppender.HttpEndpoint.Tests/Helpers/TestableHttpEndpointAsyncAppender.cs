using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using log4net.AsyncAppender;
using log4net.Core;

namespace Tests
{
    public class TestableHttpEndpointAsyncAppender : HttpEndpointAsyncAppender
    {
        public long ProcessAsyncInvocationsCount => Interlocked.Read(ref _processAsyncInvocationsCount);

        private long _processAsyncInvocationsCount;

        public TestableHttpEndpointAsyncAppender(bool autoConfigure = true)
        {
            if (autoConfigure)
                Configure();
        }

        protected override Task ProcessAsync(IReadOnlyList<LoggingEvent> events, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _processAsyncInvocationsCount);
            return Task.CompletedTask;
        }

        protected override Task<HttpContent> GetHttpContentAsync(IReadOnlyList<LoggingEvent> events) => null;

        public new Uri CreateEndpoint() => CreateEndpoint();

        public new void Configure() => Configure();

        public new bool ValidateSelf() => ValidateSelf();

        public new void Append(LoggingEvent @event) => Append(@event);
    }
}
