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
    public delegate Task ProcessAsync(IReadOnlyList<LoggingEvent> events, CancellationToken cancellationToken);

    internal class EventsHandler : IDisposable
    {
        #region Properties

        public Action<Exception, IReadOnlyList<LoggingEvent>> ErrorHandler { get; set; }

        public int QueuedEventsCount => _eventsQueue.Count;

        public bool IsProcessing => _processingAsyncManualResetEvent.IsSet;

        public IReadOnlyList<Task> Processors => _processors.Values.ToList().AsReadOnly();

        #endregion

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
        private readonly ConcurrentQueue<LoggingEvent> _eventsQueue = new ConcurrentQueue<LoggingEvent>();
        private readonly CancellationTokenSource _disposeCancellationTokenSource;

        private readonly AsyncManualResetEvent _processingAsyncManualResetEvent;
        private readonly AsyncManualResetEvent _idleAsyncManualResetEvent;

        private readonly ConcurrentDictionary<Task, Task> _processors;
        private List<LoggingEvent> _eventsBuffer;
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

            _processingAsyncManualResetEvent = new AsyncManualResetEvent(set: false);
            _idleAsyncManualResetEvent = new AsyncManualResetEvent(set: true);
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

            _eventsBuffer = new List<LoggingEvent>(_maxBatchSize /* initial capacity */);

            _router = Task.Factory.StartNew(
                WaitForEventsAsync, _cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current)
                .Result /* get the WaitForEventsAsync task */;
        }

        #region Processing state

        public Task ProcessingStarted() => _processingAsyncManualResetEvent.WaitAsync();

        public Task ProcessingTerminated() => _idleAsyncManualResetEvent.WaitAsync();

        #endregion

        private async Task WaitForEventsAsync()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                await DequeueIntoBufferAsync(_eventsBuffer).ConfigureAwait(false);

                if (_eventsBuffer.Any() && !_cancellationToken.IsCancellationRequested)
                {
                    var eventsToProcess = new List<LoggingEvent>(_eventsBuffer);
                    await this.RegisterBufferProcessorAsync(eventsToProcess).ConfigureAwait(false);
                    _eventsBuffer.Clear();
                }
            }
        }

        private async Task RegisterBufferProcessorAsync(List<LoggingEvent> eventsToProcess)
        {
            if (_processors.Count >= _maxConcurrentProcessorsCount)
            {
                // Wait for a task to complete.
                var _ = await Task.WhenAny(this.Processors).ConfigureAwait(false);
            }

            Task processingTask = null;

            processingTask = Task.Run(async () =>
            {
                try
                {
                    await _processAsyncDelegate(eventsToProcess.AsReadOnly(), _cancellationToken).ConfigureAwait(false);
                    _processors.TryRemove(processingTask, out var _);

                    if (_eventsQueue.IsEmpty && _processors.IsEmpty && _eventsBuffer.Count == 0)
                    {
                        _processingAsyncManualResetEvent.Reset();
                        _idleAsyncManualResetEvent.Set();
                    }
                }
                catch (Exception ex)
                {
                    this.ErrorHandler?.Invoke(ex, eventsToProcess);
                }

            }, _cancellationToken);

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
                    {
                        buffer.Add(@event);
                        _processingAsyncManualResetEvent.Set();
                        _idleAsyncManualResetEvent.Reset();
                    }

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
            catch (Exception ex)
            {
                this.ErrorHandler?.Invoke(ex, buffer);
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
