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

        public bool IsRunning => !this.RunningJob.IsCompleted;

        public Task RunningJob { get; private set; } = Task.CompletedTask;

        private readonly StringBuilder _stringBuilder = new StringBuilder();

        private readonly HttpClient _httpClient;
        private readonly Uri _endpoint;
        private readonly ConcurrentQueue<LoggingEvent> _queue;
        private readonly SemaphoreSlim _semaphore;
        private readonly Func<LoggingEvent, string> _eventJsonFormatter;
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
            _eventJsonFormatter = eventJsonFormatter;
            _cancellationToken = cancellationToken;
        }

        public void Start()
        {
            if (RunningJob != null && !RunningJob.IsCompleted)
                throw new InvalidOperationException("The processor is already running.");

            RunningJob = Task.Factory.StartNew(
                DoProcessAsync,
                _cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Current);
        }

        private async Task DoProcessAsync()
        {
            var eventsBuffer = new List<LoggingEvent>(this.MaxBatchSize /* initial capacity */);
            var baseRequest = CreateBaseRequest();

            while (!_cancellationToken.IsCancellationRequested)
            {
                eventsBuffer.Clear();
                await DequeueIntoBufferAsync(eventsBuffer);

                if (eventsBuffer.Any())
                {
                    try
                    {
                        var json = SerializeToJson(eventsBuffer);
                        baseRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
                        var response = await _httpClient.SendAsync(baseRequest, _cancellationToken);
                        response.EnsureSuccessStatusCode();
                    }
                    catch (Exception ex)
                    {
                        this.ExceptionHandler?.Invoke(ex, new List<LoggingEvent>(eventsBuffer));
                    }

                    eventsBuffer.Clear();
                }
            }

            // Local functions

            async Task DequeueIntoBufferAsync(List<LoggingEvent> buffer)
            {
                do
                {
                    await _semaphore.WaitAsync();
                    bool dequeued = _queue.TryDequeue(out var @event);
                    if (dequeued && @event != null)
                        buffer.Add(@event);

                } while (
                    !_queue.IsEmpty &&
                    buffer.Count <= this.MaxBatchSize &&
                    !_cancellationToken.IsCancellationRequested);
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

            eventsBuffer.ForEach(e => _stringBuilder.AppendLine(_eventJsonFormatter(e)));

            var json = eventsBuffer.ToString();
            _stringBuilder.Clear();

            return json;
        }
    }
}
