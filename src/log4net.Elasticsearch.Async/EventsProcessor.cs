using log4net.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace log4net.Elasticsearch.Async
{
    internal class EventsProcessor : IDisposable
    {
        public Action<Exception, List<LoggingEvent>> ExceptionHandler { get; set; }

        private volatile int _maxBatchSize = 512;
        public int MaxBatchSize
        {
            get { return _maxBatchSize; }
            set
            {
                _maxBatchSize = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(MaxBatchSize));
            }
        }

        public int QueuedEventsCount => _eventsQueue.Count;

        public bool IsProcessing => _processingJobsCount > 0;

        public IReadOnlyList<Task> ProcessingJobs => _processingJobs.Values.ToList().AsReadOnly();

        public bool IsRunning => !_routerJob?.IsCompleted ?? false;

        private volatile int _processingJobsCount;
        private readonly ConcurrentDictionary<Task, Task> _processingJobs;
        private Task _routerJob;

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
        private readonly ConcurrentQueue<LoggingEvent> _eventsQueue = new ConcurrentQueue<LoggingEvent>();

        private readonly HttpClient _httpClient;
        private readonly Uri _endpoint;
        private readonly Func<LoggingEvent, string> _eventJsonSerializer;
        private readonly int _maxConcurrentProcessingJobsCount;
        private readonly CancellationToken _cancellationToken;

        private readonly CancellationTokenSource _disposeCancellationTokenSource;

        public EventsProcessor(
            HttpClient httpClient,
            Uri endpoint,
            Func<LoggingEvent, string> eventJsonFormatter,
            int maxConcurrentProcessingJobsCount,
            CancellationToken cancellationToken)
        {
            _httpClient = httpClient;
            _endpoint = endpoint;
            _eventJsonSerializer = eventJsonFormatter;
            _maxConcurrentProcessingJobsCount = maxConcurrentProcessingJobsCount;

            _disposeCancellationTokenSource = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _disposeCancellationTokenSource.Token);

            _cancellationToken = linkedCts.Token;

            _processingJobs = new ConcurrentDictionary<Task, Task>();
        }

        public void Append(LoggingEvent @event)
        {
            _eventsQueue.Enqueue(@event);
            _semaphore.Release();
        }

        public void Start()
        {
            if (_routerJob != null && !_routerJob.IsCompleted)
                throw new InvalidOperationException("The processor is already running.");

            _routerJob = Task.Factory.StartNew(
                WaitForEventsAsync,
                _cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Current)
                .Result /* get the WaitForEventsAsync task */;
        }

        private async Task WaitForEventsAsync()
        {
            var eventsBuffer = new List<LoggingEvent>(this.MaxBatchSize /* initial capacity */);

            while (!_cancellationToken.IsCancellationRequested)
            {
                eventsBuffer.Clear();

                await DequeueIntoBufferAsync(eventsBuffer).ConfigureAwait(false);

                if (eventsBuffer.Any() && !_cancellationToken.IsCancellationRequested)
                {
                    Interlocked.Increment(ref _processingJobsCount);

                    if (_processingJobs.Count >= _maxConcurrentProcessingJobsCount)
                    {
                        System.Diagnostics.Debug.WriteLine("Wait for a job to finish");
                        // Wait for a task to complete.
                        var _ = await Task.WhenAny(this.ProcessingJobs).ConfigureAwait(false);
                    }

                    var eventsToProcess = new List<LoggingEvent>(eventsBuffer);

                    Task processingTask = null;
                    processingTask = Task.Factory.StartNew(
                        async () =>
                        {
                            await DoProcessAsync(eventsToProcess);
                            Interlocked.Decrement(ref _processingJobsCount);
                            _processingJobs.TryRemove(processingTask, out var _);
                        },
                        _cancellationToken,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Current);

                    _processingJobs.TryAdd(processingTask, processingTask);

                    System.Diagnostics.Debug.WriteLine($"Started new job with {eventsToProcess.Count} logs, current count: {_processingJobsCount}");

                    eventsBuffer.Clear();
                }
            }

            // Local functions

            async Task DequeueIntoBufferAsync(List<LoggingEvent> buffer)
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
                        buffer.Count < this.MaxBatchSize &&
                        !_cancellationToken.IsCancellationRequested);

                    if (_eventsQueue.IsEmpty) System.Diagnostics.Debug.WriteLine("Buffer completed reason: empty queue");
                    if (buffer.Count >= this.MaxBatchSize) System.Diagnostics.Debug.WriteLine("Buffer completed reason: max batch size reached");
                    if (_cancellationToken.IsCancellationRequested) System.Diagnostics.Debug.WriteLine("Buffer completed reason: cancellation requested");
                }
                catch (OperationCanceledException)
                {
                    // The cancellation token has been canceled.
                    // Continue execution and flush the last dequeue events.
                }
            }
        }

        private async Task DoProcessAsync(List<LoggingEvent> events)
        {
            try
            {
                var json = SerializeToJson(events);
                var baseRequest = CreateBaseRequest();
                baseRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
                // Don't use the cancellation token here, allow the last events to flush.
                var response = await _httpClient.SendAsync(baseRequest, CancellationToken.None).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                this.ExceptionHandler?.Invoke(ex, events);
            }
        }

        private HttpRequestMessage CreateBaseRequest()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);

            if (!string.IsNullOrWhiteSpace(_endpoint.UserInfo))
            {
                request.Headers.Remove("Authorization");
                request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(_endpoint.UserInfo)));
            }

            return request;
        }

        private string SerializeToJson(List<LoggingEvent> eventsBuffer)
        {
            var sb = new StringBuilder();

            if (eventsBuffer.Count != 1)
                sb.AppendLine("{\"index\" : {} }");

            eventsBuffer.ForEach(e => sb.AppendLine(_eventJsonSerializer(e)));

            var json = sb.ToString();
            return json;
        }

        public void Dispose()
        {
            _disposeCancellationTokenSource.Cancel();
            _semaphore.Dispose();

            Task.WhenAll(this.ProcessingJobs);
        }
    }
}
