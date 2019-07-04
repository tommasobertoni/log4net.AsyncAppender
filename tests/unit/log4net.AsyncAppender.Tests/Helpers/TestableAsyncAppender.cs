using log4net.AsyncAppender;
using log4net.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    internal class TestableAsyncAppender : AsyncAppender
    {
        public long ProcessedEventsCount => Interlocked.Read(ref _processedEventsCount);

        public long ProcessAsyncInvocationsCount => Interlocked.Read(ref _processAsyncInvocationsCount);

        private long _processedEventsCount;
        private long _processAsyncInvocationsCount;

        public TestableAsyncAppender(bool autoConfigure = true)
        {
            if (autoConfigure)
                this.Configure();
        }

        protected override Task ProcessAsync(IReadOnlyList<LoggingEvent> events, CancellationToken cancellationToken)
        {
            Interlocked.Add(ref _processedEventsCount, events.Count);
            Interlocked.Increment(ref _processAsyncInvocationsCount);
            return Task.CompletedTask;
        }

        public new void Configure() => base.Configure();

        public new bool ValidateSelf() => base.ValidateSelf();

        public new void Append(LoggingEvent @event) => base.Append(@event);
    }
}
