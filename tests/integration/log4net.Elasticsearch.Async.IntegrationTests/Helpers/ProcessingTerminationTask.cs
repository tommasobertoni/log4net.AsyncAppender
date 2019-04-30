using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace log4net.Elasticsearch.Async.Helpers
{
    internal class ProcessingTerminationTask
    {
        private readonly ElasticsearchAsyncAppender _appender;

        public ProcessingTerminationTask(ElasticsearchAsyncAppender appender)
        {
            this._appender = appender;
        }

        public ProcessingTerminationAwaiter GetAwaiter() => new ProcessingTerminationAwaiter(_appender);

        public async Task AsTask() => await this;
    }

    internal class ProcessingTerminationAwaiter : System.Runtime.CompilerServices.INotifyCompletion
    {
        private readonly ElasticsearchAsyncAppender _appender;
        private Action _continuation;

        public bool IsCompleted => !_appender.IsProcessing;

        public ProcessingTerminationAwaiter(ElasticsearchAsyncAppender appender)
        {
            _appender = appender;
        }

        public void OnCompleted(Action continuation)
        {
            if (this.IsCompleted)
                continuation();
            else
            {
                _continuation = continuation;

                Task.Run(async () =>
                {
                    while (!this.IsCompleted)
                        await Task.Delay(100); // Check interval

                    this.Resume();
                });
            }
        }

        private void Resume() => _continuation();

        public void GetResult() { }
    }
}
