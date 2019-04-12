using log4net.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace log4net.Elasticsearch.Async
{
    internal class EventProcessor
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

        private volatile bool _isProcessing;
        public bool IsProcessing
        {
            get { return _isProcessing; }
            private set { _isProcessing = value; }
        }

        public bool IsRunning => !this.RunningJob.IsCompleted;

        public Task RunningJob { get; private set; }

        private readonly StringBuilder _stringBuilder = new StringBuilder();

        private readonly HttpClient _httpClient;
        private readonly Uri _endpoint;
        private readonly ConcurrentQueue<LoggingEvent> _queue;
        private readonly SemaphoreSlim _semaphore;
        private readonly Func<LoggingEvent, string> _eventJsonSerializer;
        private readonly CancellationToken _cancellationToken;

        public EventProcessor(
            HttpClient httpClient,
            Uri endpoint,
            ConcurrentQueue<LoggingEvent> queue,
            SemaphoreSlim semaphore,
            Func<LoggingEvent, string> eventJsonFormatter,
            CancellationToken cancellationToken)
        {
            _httpClient = httpClient;
            _endpoint = endpoint;
            _queue = queue;
            _semaphore = semaphore;
            _eventJsonSerializer = eventJsonFormatter;
            _cancellationToken = cancellationToken;

            this.RunningJob = Task.CompletedTask;
            this.IsProcessing = false;
        }

        public void Start()
        {
            if (this.RunningJob != null && !this.RunningJob.IsCompleted)
                throw new InvalidOperationException("The processor is already running.");

            this.RunningJob = Task.Factory.StartNew(
                DoProcessAsync,
                _cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Current)
                .Result /* get the DoProcessAsync task */ ;
        }

        private async Task DoProcessAsync()
        {
            var eventsBuffer = new List<LoggingEvent>(this.MaxBatchSize /* initial capacity */);

            while (!_cancellationToken.IsCancellationRequested)
            {
                eventsBuffer.Clear();

                await DequeueIntoBufferAsync(eventsBuffer).ConfigureAwait(false);

                if (eventsBuffer.Any())
                {
                    this.IsProcessing = true;

                    try
                    {
                        var json = SerializeToJson(eventsBuffer);
                        var baseRequest = CreateBaseRequest();
                        baseRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
                        // Don't use the cancellation token here, allow the last events to flush.
                        var response = await _httpClient.SendAsync(baseRequest, CancellationToken.None).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();
                    }
                    catch (Exception ex)
                    {
                        this.ExceptionHandler?.Invoke(ex, new List<LoggingEvent>(eventsBuffer));
                    }

                    eventsBuffer.Clear();

                    this.IsProcessing = false;
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
                        bool dequeued = _queue.TryDequeue(out var @event);
                        if (dequeued && @event != null)
                            buffer.Add(@event);

                    } while (
                        !_queue.IsEmpty &&
                        buffer.Count <= this.MaxBatchSize &&
                        !_cancellationToken.IsCancellationRequested);
                }
                catch (OperationCanceledException)
                {
                    // The cancellation token has been canceled.
                    // Continue execution and flush the last dequeue events.
                }
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
            _stringBuilder.Clear();

            if (eventsBuffer.Count != 1)
                _stringBuilder.AppendLine("{\"index\" : {} }");

            eventsBuffer.ForEach(e => _stringBuilder.AppendLine(_eventJsonSerializer(e)));

            var json = _stringBuilder.ToString();
            _stringBuilder.Clear();

            return json;
        }
    }
}
