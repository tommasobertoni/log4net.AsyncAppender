using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace log4net.Elasticsearch.Async
{
    public interface IElasticsearchAsyncAppenderConfigurator
    {
        void Configure(ElasticsearchAsyncAppender appender);
    }
}
