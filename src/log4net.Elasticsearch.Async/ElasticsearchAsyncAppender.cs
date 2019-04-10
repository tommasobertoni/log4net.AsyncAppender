using log4net.Appender;
using log4net.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("log4net.Elasticsearch.Async.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("log4net.Elasticsearch.Async.IntegrationTests")]

namespace log4net.Elasticsearch.Async
{
    public class ElasticsearchAsyncAppender : AppenderSkeleton
    {
        public string ConnectionString { get; set; }

        public int ProcessorsCount { get; set; } = 1;

        public int MaxBatchSize { get; set; } = 512;

        public int CloseTimeoutMillis { get; set; } = 10000;

        #region Appender configuration

        public IElasticsearchAsyncAppenderConfigurator AppenderConfigurator { get; set; }

        public Type AppenderConfiguratorType { get; set; }

        public string AppenderConfiguratorAssemblyQualifiedName { get; set; }

        public Action<ElasticsearchAsyncAppender> AppenderConfiguratorDelegate { get; set; }

        #endregion

        #region Json serialization

        public IEventJsonSerializer EventJsonSerializer { get; set; }

        public Type EventJsonSerializerType { get; set; }

        public string EventJsonSerializerAssemblyQualifiedName { get; set; }

        public Func<LoggingEvent, string> EventJsonSerializerDelegate { get; set; }

        #endregion

        public HttpClient HttpClient { get; set; }

        public bool Initialized { get; private set; }

        internal Task RunningJobs { get; private set; }

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
        private readonly ConcurrentQueue<LoggingEvent> _eventsQueue = new ConcurrentQueue<LoggingEvent>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly List<EventProcessor> _processors = new List<EventProcessor>();

        private AppenderSettings _settings;

        public ElasticsearchAsyncAppender()
        {
        }

        #region Appender

        public override void ActivateOptions()
        {
            base.ActivateOptions();

            if (TryActivate())
            {
                Configure();
                Init();
            }
        }

        protected override void Append(LoggingEvent[] loggingEvents)
        {
            if (!this.Initialized) return;

            foreach (var @event in loggingEvents)
                _eventsQueue.Enqueue(@event);

            _semaphore.Release(loggingEvents.Length);
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (!this.Initialized) return;

            _eventsQueue.Enqueue(loggingEvent);
            _semaphore.Release();
        }
        
        protected override void OnClose()
        {
            base.OnClose();

            if (!this.Initialized) return;

            _cts.Cancel();
            var closingTimeout = Task.Delay(this.CloseTimeoutMillis);
            var runningJobs = this._processors.Select(p => p.RunningJob);
            var runningJobsTermination = Task.WhenAll(runningJobs);
            int completedTaskIndex = Task.WaitAny(closingTimeout, runningJobsTermination);

            _semaphore.Dispose();
            _processors.Clear();

            this.Initialized = false;
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

                if (this.EventJsonSerializer == null &&
                    this.EventJsonSerializerType == null &&
                    this.EventJsonSerializerDelegate == null &&
                    string.IsNullOrWhiteSpace(this.EventJsonSerializerAssemblyQualifiedName))
                {
                    this.ErrorHandler?.Error("Missing event to json serializer.");
                    return false;
                }

                if (this.EventJsonSerializerType != null &&
                    !typeof(IEventJsonSerializer).IsAssignableFrom(EventJsonSerializerType))
                {
                    this.ErrorHandler?.Error($"{EventJsonSerializerType.Name} is not an {nameof(IEventJsonSerializer)}.");
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

        private void Configure()
        {
            var appenderConfiguratorDelegate = GetAppenderConfiguratorDelegate();
            if (appenderConfiguratorDelegate != default)
            {
                appenderConfiguratorDelegate(this);
            }
        }

        private void Init()
        {
            if (this.HttpClient == null)
                this.HttpClient = new HttpClient();

            Func<LoggingEvent, string> eventJsonSerializer = GetEventJsonSerializerDelegate();

            for (int i = 0; i < this.ProcessorsCount; i++)
            {
                var processor = new EventProcessor(
                    this.HttpClient,
                    _settings.Uri,
                    _eventsQueue,
                    _semaphore,
                    eventJsonSerializer,
                    _cts.Token)
                {
                    MaxBatchSize = this.MaxBatchSize,
                    ExceptionHandler = (ex, events) => this.ErrorHandler?.Error("An error occurred during events processing", ex)
                };

                processor.Start();

                _processors.Add(processor);
            }

            this.Initialized = true;
        }

        private Func<LoggingEvent, string> GetEventJsonSerializerDelegate()
        {
            if (this.EventJsonSerializer != null)
                return this.EventJsonSerializer.SerializeToJson;

            if (this.EventJsonSerializerDelegate != null)
                return this.EventJsonSerializerDelegate;

            if (this.EventJsonSerializerType != null)
            {
                var serializer = (IEventJsonSerializer)Activator.CreateInstance(this.EventJsonSerializerType);
                return serializer.SerializeToJson;
            }

            if (!string.IsNullOrWhiteSpace(this.EventJsonSerializerAssemblyQualifiedName))
            {
                var serializerType = Type.GetType(this.EventJsonSerializerAssemblyQualifiedName);
                var serializer = (IEventJsonSerializer)Activator.CreateInstance(serializerType);
                return serializer.SerializeToJson;
            }

            throw new InvalidOperationException("Cannot identify an event-to-json serialization strategy");
        }

        private Action<ElasticsearchAsyncAppender> GetAppenderConfiguratorDelegate()
        {
            if (this.AppenderConfigurator != null)
                return this.AppenderConfigurator.Configure;

            if (this.AppenderConfiguratorDelegate != null)
                return this.AppenderConfiguratorDelegate;

            if (this.AppenderConfiguratorType != null)
            {
                var configurator = (IElasticsearchAsyncAppenderConfigurator)Activator.CreateInstance(this.AppenderConfiguratorType);
                return configurator.Configure;
            }

            if (!string.IsNullOrWhiteSpace(this.AppenderConfiguratorAssemblyQualifiedName))
            {
                var configuratorType = Type.GetType(this.AppenderConfiguratorAssemblyQualifiedName);
                var configurator = (IElasticsearchAsyncAppenderConfigurator)Activator.CreateInstance(configuratorType);
                return configurator.Configure;
            }

            return default;
        }
    }
}
