using log4net.Appender;
using log4net.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("log4net.AsyncAppender.Tests")]

namespace log4net.AsyncAppender
{
    public abstract class AsyncAppender : AppenderSkeleton
    {
        #region Properties

        public int MaxConcurrentProcessorsCount { get; set; } = 3;

        public int MaxBatchSize { get; set; } = 512;

        public int CloseTimeoutMillis { get; set; } = 5000;

        public IAsyncAppenderConfigurator Configurator { get; set; }

        public bool Activated { get; protected set; }

        public bool AcceptsLoggingEvents { get; protected set; }

        public bool IsProcessing => _handler?.IsProcessing ?? false;

        #endregion

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private EventsHandler _handler;

        public AsyncAppender()
        {
            this.Activated = false;
            this.AcceptsLoggingEvents = false;
        }

        protected abstract Task ProcessAsync(IReadOnlyList<LoggingEvent> events, CancellationToken cancellationToken);

        #region Setup

        public override void ActivateOptions()
        {
            base.ActivateOptions();

            Configure();

            if (ValidateSelf())
            {
                Activate();
            }
        }

        protected virtual void Configure()
        {
            try
            {
                this.Configurator?.Configure(this);
            }
            catch (Exception ex)
            {
                this.ErrorHandler?.Error("Error during configuration", ex);
            }
        }

        protected virtual bool ValidateSelf()
        {
            try
            {
                if (this.MaxConcurrentProcessorsCount < 1)
                {
                    this.ErrorHandler?.Error($"{nameof(this.MaxConcurrentProcessorsCount)} must be positive.");
                    return false;
                }

                if (this.MaxBatchSize < 1)
                {
                    this.ErrorHandler?.Error($"{nameof(this.MaxBatchSize)} must be positive.");
                    return false;
                }

                if (this.CloseTimeoutMillis <= 0)
                {
                    this.ErrorHandler?.Error($"{nameof(this.CloseTimeoutMillis)} must be positive.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                this.ErrorHandler?.Error("Error during validation", ex);
                return false;
            }

            return true;
        }

        protected virtual void Activate()
        {
            _handler = new EventsHandler(
                this.ProcessAsync,
                this.MaxConcurrentProcessorsCount,
                this.MaxBatchSize,
                _cts.Token)
            {
                ErrorHandler = (ex, events) => this.ErrorHandler?.Error("An error occurred during events processing", ex)
            };

            _handler.Start();

            this.Activated = true;
            this.AcceptsLoggingEvents = true;
        }

        #endregion

        #region Appending

        protected override void Append(LoggingEvent[] loggingEvents)
        {
            if (!this.Activated || !this.AcceptsLoggingEvents)
            {
                this.ErrorHandler?.Error("This appender cannot process logging events.");
                return;
            }

            _handler.Handle(loggingEvents);
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (!this.Activated || !this.AcceptsLoggingEvents)
            {
                this.ErrorHandler?.Error("This appender cannot process logging events.");
                return;
            }

            _handler.Handle(loggingEvent);
        }

        #endregion

        #region Termination

        ~AsyncAppender() => this.OnClose();

        protected override void OnClose()
        {
            base.OnClose();

            if (!this.Activated) return;

            this.AcceptsLoggingEvents = false;

            _cts.Cancel();
            _handler.Dispose();

            var closingTimeout = Task.Delay(this.CloseTimeoutMillis);

            var processors = _handler.Processors;
            var processorsTermination = Task.WhenAll(processors);

            int completedTaskIndex = Task.WaitAny(closingTimeout, processorsTermination);
            if (completedTaskIndex == 0)
                this.ErrorHandler?.Error($"Processors {processors.Count} termination timed out during appender OnClose.");

            this.Activated = false;
        }

        #endregion

        #region State changed

        public Task ProcessingStarted() =>
            _handler?.ProcessingStarted() ?? throw new Exception("Appender was not activated");

        public Task ProcessingTerminated() =>
            _handler?.ProcessingTerminated() ?? throw new Exception("Appender was not activated");

        #endregion
    }
}
