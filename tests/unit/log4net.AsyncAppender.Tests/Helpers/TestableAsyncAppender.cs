using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using log4net.AsyncAppender;
using log4net.Core;

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
                Configure();
        }

        protected override Task ProcessAsync(IReadOnlyList<LoggingEvent> events, CancellationToken cancellationToken)
        {
            Interlocked.Add(ref _processedEventsCount, events.Count);
            Interlocked.Increment(ref _processAsyncInvocationsCount);
            return Task.CompletedTask;
        }

        public new void Configure() => Configure();

        public new bool ValidateSelf() => ValidateSelf();

        public new void Append(LoggingEvent @event) => Append(@event);
    }
}
