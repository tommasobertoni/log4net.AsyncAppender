using log4net.AsyncAppender.ElasticSearch;
using log4net.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    internal class TestableElasticSearchAsyncAppender : ElasticSearchAsyncAppender
    {
        public long ProcessAsyncInvocationsCount => Interlocked.Read(ref _processAsyncInvocationsCount);

        private long _processAsyncInvocationsCount;

        public TestableElasticSearchAsyncAppender(bool autoConfigure = true)
        {
            if (autoConfigure)
                Configure();
        }

        protected override Task ProcessAsync(IReadOnlyList<LoggingEvent> events, CancellationToken cancellationToken)
        {
            foreach (var e in events)
                Projection(e);

            Interlocked.Increment(ref _processAsyncInvocationsCount);
            return Task.CompletedTask;
        }

        public new Uri CreateEndpoint() => CreateEndpoint();

        public new void Configure() => Configure();

        public new bool ValidateSelf() => ValidateSelf();

        public new void Append(LoggingEvent @event) => Append(@event);
    }
}
