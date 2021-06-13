using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using log4net.AsyncAppender.ElasticSearch;
using log4net.Core;

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

        public new Uri CreateEndpoint() => base.CreateEndpoint();

        public new void Configure() => base.Configure();

        public new bool ValidateSelf() => base.ValidateSelf();

        public new void Append(LoggingEvent @event) => base.Append(@event);
    }
}
