using log4net.AsyncAppender;
using log4net.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntegrationTests.Helpers
{
    public class TestableAsyncAppender : AsyncAppender
    {
        public int ProcessInvocationsCount => _processInvocationsCount;

        public int EventsProcessedCount => _eventsProcessedCount;

        private int _processInvocationsCount = 0;
        private int _eventsProcessedCount = 0;

        protected override Task ProcessAsync(IReadOnlyList<LoggingEvent> events, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _processInvocationsCount);
            Interlocked.Add(ref _eventsProcessedCount, events?.Count ?? 0);
            return Task.CompletedTask;
        }
    }
}
