using log4net.Repository.Hierarchy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace log4net.Elasticsearch.Async.Helpers
{
    static class ILogExtensions
    {
        public static ElasticsearchAsyncAppender GetElasticsearchAppender(this ILog log)
        {
            var hierarchy = (Hierarchy)log.Logger.Repository;
            return hierarchy.Root.Appenders.ToArray()
                .FirstOrDefault(a => a is ElasticsearchAsyncAppender) as ElasticsearchAsyncAppender;
        }

        public static void SetElasticsearchAppender(this ILog log, ElasticsearchAsyncAppender appender)
        {
            var hierarchy = (Hierarchy)log.Logger.Repository;
            var existingAppender = log.GetElasticsearchAppender();
            if (existingAppender != null)
            {
                var removedAppender = hierarchy.Root.RemoveAppender(existingAppender);
                removedAppender.Close();
            }

            hierarchy.Root.AddAppender(appender);
        }
    }
}
