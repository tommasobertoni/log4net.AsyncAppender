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

        public int CloseTimeoutMillis { get; set; } = 5000;

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

        private volatile bool _acceptsLoggingEvents;
        public bool AcceptsLoggingEvents
        {
            get { return _acceptsLoggingEvents; }
            private set { _acceptsLoggingEvents = value; }
        }

        public bool IsProcessing => _processors?.Any(p => p.IsProcessing) ?? false;

        internal Task RunningJobs { get; private set; }

        public AppenderSettings Settings { get; private set; }

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly List<EventsProcessor> _processors = new List<EventsProcessor>();
        private int _targetProcessorIndex = 0;

        public ElasticsearchAsyncAppender()
        {
            this.Initialized = false;
            this.AcceptsLoggingEvents = false;
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
            if (!this.Initialized || !this.AcceptsLoggingEvents)
            {
                this.ErrorHandler?.Error("This appender cannot process logging events.");
                return;
            }

            var processor = GetTargetProcessor();

            foreach (var @event in loggingEvents)
                processor.Append(@event);
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (!this.Initialized || !this.AcceptsLoggingEvents)
            {
                this.ErrorHandler?.Error("This appender cannot process logging events.");
                return;
            }

            var processor = GetTargetProcessor();
            processor.Append(loggingEvent);
        }
        
        protected override void OnClose()
        {
            base.OnClose();

            if (!this.Initialized) return;

            this.AcceptsLoggingEvents = false;

            _cts.Cancel();
            _processors.ForEach(p => p.Dispose());

            var closingTimeout = Task.Delay(this.CloseTimeoutMillis);

            var runningJobs = this._processors.Select(p => p.RunningJob);
            var runningJobsTermination = Task.WhenAll(runningJobs);

            int completedTaskIndex = Task.WaitAny(closingTimeout, runningJobsTermination);
            if (completedTaskIndex == 0)
                this.ErrorHandler?.Error("Running jobs termination timed out during appender closing.");

            _processors.Clear();

            this.Initialized = false;
        }

        #endregion

        ~ElasticsearchAsyncAppender() => this.OnClose();

        private EventsProcessor GetTargetProcessor() => _processors[GetTargetProcessorIndex()];

        private int GetTargetProcessorIndex()
        {
            var targetProcessorIndex = Interlocked.Increment(ref _targetProcessorIndex) % _processors.Count;
            Interlocked.Exchange(ref _targetProcessorIndex, targetProcessorIndex);
            targetProcessorIndex = _targetProcessorIndex;
            return targetProcessorIndex;
        }

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

                Settings = new AppenderSettings(this.ConnectionString);

                if (!Settings.AreValid())
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
            try
            {
                var appenderConfiguratorDelegate = GetAppenderConfiguratorDelegate();
                appenderConfiguratorDelegate?.Invoke(this);
            }
            catch (Exception ex)
            {
                this.ErrorHandler?.Error("Error during configuration", ex);
            }
        }

        private void Init()
        {
            if (this.HttpClient == null)
                this.HttpClient = new HttpClient();

            Func<LoggingEvent, string> eventJsonSerializer = GetEventJsonSerializerDelegate();

            for (int i = 0; i < this.ProcessorsCount; i++)
            {
                var processor = new EventsProcessor(
                    this.HttpClient,
                    Settings.Uri,
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
            this.AcceptsLoggingEvents = true;
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
