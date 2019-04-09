using log4net.Appender;
using log4net.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace log4net.Elasticsearch.Async
{
    public class ElasticsearchAsyncAppender : AppenderSkeleton
    {
        public string ConnectionString { get; set; }

        public int ProcessorsCount { get; set; } = 1;

        public int MaxBatchSize { get; set; } = 512;

        public int CloseTimeoutMillis { get; set; } = 10000;

        public Func<LoggingEvent, string> EventJsonSerializer { get; set; }

        public HttpClient HttpClient { get; set; }

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
        private readonly ConcurrentQueue<LoggingEvent> _eventsQueue = new ConcurrentQueue<LoggingEvent>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly List<EventProcessor> _processors = new List<EventProcessor>();

        private AppenderSettings _settings;

        #region Appender

        public override void ActivateOptions()
        {
            base.ActivateOptions();

            if (TryActivate())
            {
                Init();
            }
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            _eventsQueue.Enqueue(loggingEvent);
            _semaphore.Release();
        }

        protected override void OnClose()
        {
            base.OnClose();

            _cts.Cancel();
            var closingTimeout = Task.Delay(this.CloseTimeoutMillis);
            var runningJobs = this._processors.Select(p => p.RunningJob);
            var runningJobsTermination = Task.WhenAll(runningJobs);
            Task.WaitAny(closingTimeout, runningJobsTermination);

            _semaphore.Dispose();
            _processors.Clear();
        }

        #endregion

        private bool TryActivate()
        {
            try
            {
                if (this.ProcessorsCount < 1)
                {
                    this.ErrorHandler?.Error("Processors count must be positive.");
                    return false;
                }

                if (this.MaxBatchSize < 1)
                {
                    this.ErrorHandler?.Error("Batch size must be positive.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(this.ConnectionString))
                {
                    this.ErrorHandler?.Error("Missing connection string.");
                    return false;
                }

                if (EventJsonSerializer == null)
                {
                    this.ErrorHandler?.Error("Missing event to json serializer.");
                    return false;
                }

                _settings = new AppenderSettings(this.ConnectionString);

                if (!_settings.AreValid())
                {
                    this.ErrorHandler?.Error("Some required settings are missing.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                this.ErrorHandler?.Error("Error during activation", ex);
                return false;
            }

            return true;
        }

        private void Init()
        {
            if (this.HttpClient == null)
                this.HttpClient = new HttpClient();

            for (int i = 0; i < this.ProcessorsCount; i++)
            {
                var processor = new EventProcessor(
                    this.HttpClient,
                    _settings.Uri,
                    _eventsQueue,
                    _semaphore,
                    EventJsonSerializer,
                    _cts.Token)
                {
                    MaxBatchSize = this.MaxBatchSize,
                    ExceptionHandler = (ex, events) => this.ErrorHandler?.Error("An error occurred during events processing", ex)
                };

                processor.Start();

                _processors.Add(processor);
            }
        }
    }
}
