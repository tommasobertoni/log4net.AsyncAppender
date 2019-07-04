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

        public bool Trace { get; set; }

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
                var message = "Error during configuration";
                this.TryTrace(message, ex);
                this.ErrorHandler?.Error(message, ex);
            }
        }

        protected virtual bool ValidateSelf()
        {
            try
            {
                if (this.MaxConcurrentProcessorsCount < 1)
                {
                    var message = $"{nameof(this.MaxConcurrentProcessorsCount)} must be positive.";
                    this.TryTrace(message);
                    this.ErrorHandler?.Error(message);
                    return false;
                }

                if (this.MaxBatchSize < 1)
                {
                    var message = $"{nameof(this.MaxBatchSize)} must be positive.";
                    this.TryTrace(message);
                    this.ErrorHandler?.Error(message);
                    return false;
                }

                if (this.CloseTimeoutMillis <= 0)
                {
                    var message = $"{nameof(this.CloseTimeoutMillis)} must be positive.";
                    this.TryTrace(message);
                    this.ErrorHandler?.Error(message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                var message = "Error during validation";
                this.TryTrace(message, ex);
                this.ErrorHandler?.Error(message, ex);
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
                ErrorHandler = (ex, events) =>
                {
                    var message = "An error occurred during events processing";
                    this.TryTrace(message, ex);
                    this.ErrorHandler?.Error(message, ex);
                },

                Tracer = message => this.TryTrace(message),
            };

            _handler.Start();

            this.Activated = true;
            this.AcceptsLoggingEvents = true;

            this.TryTrace("Activated");
        }

        #endregion

        #region Appending

        protected override void Append(LoggingEvent[] loggingEvents)
        {
            if (!this.Activated || !this.AcceptsLoggingEvents)
            {
                var message = "This appender cannot process logging events.";
                this.TryTrace(message);
                this.ErrorHandler?.Error(message);
                return;
            }

            _handler.Handle(loggingEvents);
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (!this.Activated || !this.AcceptsLoggingEvents)
            {
                var message = "This appender cannot process logging events.";
                this.TryTrace(message);
                this.ErrorHandler?.Error(message);
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
            this.TryTrace("Closing");

            _cts.Cancel();
            _handler.Dispose();

            var closingTimeout = Task.Delay(this.CloseTimeoutMillis);

            var processors = _handler.Processors;
            var processorsTermination = Task.WhenAll(processors);

            if (processors.Count > 0)
                this.TryTrace($"Waiting for {processors.Count} processors to terminate.");

            int completedTaskIndex = Task.WaitAny(closingTimeout, processorsTermination);
            if (completedTaskIndex == 0)
            {
                var message = $"Processors {processors.Count} termination timed out during appender OnClose.";
                this.TryTrace(message);
                this.ErrorHandler?.Error(message);
            }

            this.Activated = false;
            this.TryTrace("Deactivated");
        }

        #endregion

        #region State changed

        public Task ProcessingStarted() =>
            _handler?.ProcessingStarted() ?? throw new Exception("Appender was not activated");

        public Task ProcessingTerminated() =>
            _handler?.ProcessingTerminated() ?? throw new Exception("Appender was not activated");

        #endregion

        #region Trace

        protected void TryTrace(string message, Exception exception = null)
        {
            if (!this.Trace) return;

            if (exception == null)
                System.Diagnostics.Trace.WriteLine(message);
            else
                System.Diagnostics.Trace.WriteLine($"{message}, exception: {exception.Message}");
        }

        #endregion
    }
}
