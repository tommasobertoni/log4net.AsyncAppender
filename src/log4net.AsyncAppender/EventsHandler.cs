using log4net.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace log4net.AsyncAppender
{
    public delegate Task ProcessAsync(List<LoggingEvent> events, CancellationToken cancellationToken);

    internal class EventsHandler : IDisposable
    {
        #region Properties

        public Action<Exception, List<LoggingEvent>> ErrorHandler { get; set; }

        public int QueuedEventsCount => _eventsQueue.Count;

        public bool IsProcessing => _processorsCount > 0;

        public IReadOnlyList<Task> Processors => _processors.Values.ToList().AsReadOnly();

        public bool IsRunning => !_router?.IsCompleted ?? false;

        #endregion

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
        private readonly ConcurrentQueue<LoggingEvent> _eventsQueue = new ConcurrentQueue<LoggingEvent>();
        private readonly CancellationTokenSource _disposeCancellationTokenSource;

        private volatile int _processorsCount;
        private readonly ConcurrentDictionary<Task, Task> _processors;
        private Task _router;

        private readonly ProcessAsync _processAsyncDelegate;
        private readonly int _maxConcurrentProcessorsCount;
        private readonly int _maxBatchSize;

        private readonly CancellationToken _cancellationToken;

        public EventsHandler(
            ProcessAsync processAsyncDelegate,
            int maxConcurrentProcessorsCount = 3,
            int maxBatchSize = 512,
            CancellationToken cancellationToken = default)
        {
            _processAsyncDelegate = processAsyncDelegate;
            _maxConcurrentProcessorsCount = maxConcurrentProcessorsCount;
            _maxBatchSize = maxBatchSize;

            _disposeCancellationTokenSource = new CancellationTokenSource();

            _cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _disposeCancellationTokenSource.Token).Token;

            _processors = new ConcurrentDictionary<Task, Task>();
        }

        public void Handle(LoggingEvent @event)
        {
            _eventsQueue.Enqueue(@event);
            _semaphore.Release();
        }

        public void Handle(IEnumerable<LoggingEvent> events)
        {
            int eventsCount = 0;

            foreach (var e in events)
            {
                _eventsQueue.Enqueue(e);
                eventsCount++;
            }

            _semaphore.Release(eventsCount);
        }

        public void Start()
        {
            if (_router != null && !_router.IsCompleted)
                throw new InvalidOperationException("The handler is already running.");

            _router = Task.Factory.StartNew(
                WaitForEventsAsync, _cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current)
                .Result /* get the WaitForEventsAsync task */;
        }

        private async Task WaitForEventsAsync()
        {
            var eventsBuffer = new List<LoggingEvent>(_maxBatchSize /* initial capacity */);

            while (!_cancellationToken.IsCancellationRequested)
            {
                await DequeueIntoBufferAsync(eventsBuffer).ConfigureAwait(false);

                if (eventsBuffer.Any() && !_cancellationToken.IsCancellationRequested)
                {
                    var eventsToProcess = new List<LoggingEvent>(eventsBuffer);
                    await this.RegisterBufferProcessorAsync(eventsToProcess).ConfigureAwait(false);
                    eventsBuffer.Clear();
                }
            }
        }

        private async Task RegisterBufferProcessorAsync(List<LoggingEvent> eventsToProcess)
        {
            Interlocked.Increment(ref _processorsCount);

            if (_processors.Count >= _maxConcurrentProcessorsCount)
            {
                // Wait for a task to complete.
                var _ = await Task.WhenAny(this.Processors).ConfigureAwait(false);
            }

            Task processingTask = null;

            processingTask = Task.Factory.StartNew(async () =>
            {
                await _processAsyncDelegate(eventsToProcess, _cancellationToken).ConfigureAwait(false);

                Interlocked.Decrement(ref _processorsCount);
                _processors.TryRemove(processingTask, out var _);

            }, _cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);

            _processors.TryAdd(processingTask, processingTask);
        }

        private async Task DequeueIntoBufferAsync(List<LoggingEvent> buffer)
        {
            try
            {
                do
                {
                    await _semaphore.WaitAsync(_cancellationToken).ConfigureAwait(false);
                    bool dequeued = _eventsQueue.TryDequeue(out var @event);
                    if (dequeued && @event != null)
                        buffer.Add(@event);

                } while (
                    !_eventsQueue.IsEmpty &&
                    buffer.Count < _maxBatchSize &&
                    !_cancellationToken.IsCancellationRequested);
            }
            catch (OperationCanceledException)
            {
                // The cancellation token has been canceled.
                // Continue execution and flush the last dequeued events.
            }
        }

        public void Dispose()
        {
            _disposeCancellationTokenSource.Cancel();
            _semaphore.Dispose();

            Task.WhenAll(this.Processors);
        }
    }
}
