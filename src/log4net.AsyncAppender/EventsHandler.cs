using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net.Core;

namespace log4net.AsyncAppender
{
    public delegate Task ProcessAsync(IReadOnlyList<LoggingEvent> events, CancellationToken cancellationToken);

    internal class EventsHandler : IDisposable
    {
        private readonly object _syncCompletion = new();

        private readonly SemaphoreSlim _semaphore = new(0);
        private readonly ConcurrentQueue<LoggingEvent> _eventsQueue = new();
        private readonly CancellationTokenSource _disposeCancellationTokenSource = new();

        private readonly AsyncManualResetEvent _processingAsyncManualResetEvent = new(set: false);
        private readonly AsyncManualResetEvent _idleAsyncManualResetEvent = new(set: true);

        private readonly ConcurrentDictionary<Task, Task> _processors = new();
        private long _processorsCount;
        private volatile List<LoggingEvent> _eventsBuffer = new();
        private Task? _router;

        private readonly ProcessAsync _processAsyncDelegate;
        private readonly SemaphoreSlim _processorSlotsSemaphore;

        private readonly CancellationToken _cancellationToken;

        public EventsHandler(
            ProcessAsync processAsyncDelegate,
            int maxConcurrentProcessorsCount = 3,
            int maxBatchSize = 512,
            CancellationToken cancellationToken = default)
        {
            _processAsyncDelegate = processAsyncDelegate;

            MaxConcurrentProcessorsCount = maxConcurrentProcessorsCount;
            MaxBatchSize = maxBatchSize;

            _processorSlotsSemaphore = new SemaphoreSlim(maxConcurrentProcessorsCount, maxConcurrentProcessorsCount);

            _cancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _disposeCancellationTokenSource.Token).Token;
        }

        #region Properties

        public Action<Exception, IReadOnlyList<LoggingEvent>>? ErrorHandler { get; set; }

        public Action<string>? Tracer { get; set; }

        public int QueuedEventsCount => _eventsQueue.Count;

        public bool IsProcessing => _processingAsyncManualResetEvent.IsSet && !_idleAsyncManualResetEvent.IsSet;

        public IReadOnlyList<Task> Processors => _processors.Values.ToList().AsReadOnly();

        public int MaxConcurrentProcessorsCount { get; }

        public int MaxBatchSize { get; }

        #endregion

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
            if (_router is not null && !_router.IsCompleted)
                throw new InvalidOperationException("The handler is already running.");

            _eventsBuffer = new List<LoggingEvent>(capacity: MaxBatchSize);

            _router = Task.Factory.StartNew(
                WaitForEventsAsync, _cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current)
                .Result /* get the WaitForEventsAsync task */;

            _ = _router.ContinueWith(t => Tracer?.Invoke("Router stopped."), TaskContinuationOptions.ExecuteSynchronously);
        }

        #region Processing state

        public Task ProcessingStarted() => _processingAsyncManualResetEvent.WaitAsync();

        public Task ProcessingTerminated() => _idleAsyncManualResetEvent.WaitAsync();

        #endregion

        private async Task WaitForEventsAsync()
        {
            Tracer?.Invoke("Router started.");

            while (!_cancellationToken.IsCancellationRequested)
            {
                await DequeueIntoBufferAsync(_eventsBuffer).ConfigureAwait(false);

                if (_eventsBuffer.Any() && !_cancellationToken.IsCancellationRequested)
                {
                    // Register starting of new processor.
                    Interlocked.Increment(ref _processorsCount);

                    var eventsToProcess = new List<LoggingEvent>(_eventsBuffer);
                    _eventsBuffer.Clear();

                    var processorTask = await StartNewProcessorAsync(eventsToProcess);
                    _processors.TryAdd(processorTask, processorTask);

                    _ = processorTask.ContinueWith(NotifyCompletion, TaskContinuationOptions.ExecuteSynchronously);
                }
            }

            Tracer?.Invoke("Router received cancellation request.");
        }

        private async Task<Task> StartNewProcessorAsync(List<LoggingEvent> eventsToProcess)
        {
            // Wait for a processing slot (these define how many concurrent processors can run at a time).
            await _processorSlotsSemaphore.WaitAsync(_cancellationToken).ConfigureAwait(false);

            var processorTask = Task.Run(async () =>
            {
                try
                {
                    Tracer?.Invoke("Processor started.");
                    await _processAsyncDelegate(eventsToProcess.AsReadOnly(), _cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ErrorHandler?.Invoke(ex, eventsToProcess);
                }
                finally
                {
                    Tracer?.Invoke("Processor completed.");
                    _processorSlotsSemaphore.Release(); // Release the slot just used.
                }

            }, _cancellationToken);

            return processorTask;
        }

        private void NotifyCompletion(Task processorTask)
        {
            Interlocked.Decrement(ref _processorsCount);
            _processors.TryRemove(processorTask, out _);

            lock (_syncCompletion)
            {
                if (_eventsQueue.TryPeek(out _) == false &&
                    _eventsBuffer.Count == 0 &&
                    Interlocked.Read(ref _processorsCount) == 0)
                {
                    _processingAsyncManualResetEvent.Reset();
                    _idleAsyncManualResetEvent.Set();
                    Tracer?.Invoke("Idle");
                }
            }
        }

        private async Task DequeueIntoBufferAsync(List<LoggingEvent> buffer)
        {
            try
            {
                do
                {
                    await _semaphore.WaitAsync(_cancellationToken).ConfigureAwait(false);

                    bool dequeued = _eventsQueue.TryDequeue(out var @event);

                    if (dequeued && @event is not null)
                    {
                        buffer.Add(@event);
                        _idleAsyncManualResetEvent.Reset();
                        _processingAsyncManualResetEvent.Set();
                    }

                } while (KeepBuffering());

                Tracer?.Invoke($"Dequeued {buffer.Count} events.");
            }
            catch (OperationCanceledException)
            {
                // The cancellation token has been canceled.
                // Continue execution and flush the last dequeued events.

                if (buffer.Count > 0)
                    Tracer?.Invoke($"Cancellation requested, with {buffer.Count} events buffered.");
                else
                    Tracer?.Invoke("Cancellation requested.");
            }
            catch (Exception ex)
            {
                ErrorHandler?.Invoke(ex, buffer);
            }

            // Local functions

            bool KeepBuffering()
            {
                if (_eventsQueue.TryPeek(out _) == false) return false;

                if (buffer.Count >= MaxBatchSize) return false;

                if (_cancellationToken.IsCancellationRequested) return false;

                return true;
            }
        }

        public void Dispose()
        {
            var processors = Processors;

            if (processors.Count > 0)
                Tracer?.Invoke($"Disposing events handler with {processors.Count} processors.");

            _disposeCancellationTokenSource.Cancel();
            _semaphore.Dispose();

            Task.WhenAll(processors);

            _processingAsyncManualResetEvent.Reset();
            _idleAsyncManualResetEvent.Set();
        }
    }
}
