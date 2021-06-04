using System.Linq;
using log4net;
using log4net.Repository.Hierarchy;

namespace IntegrationTests.Helpers
{
    static class ILogExtensions
    {
        public static TestableAsyncAppender GetElasticsearchAppender(this ILog log)
        {
            var hierarchy = (Hierarchy)log.Logger.Repository;
            return hierarchy.Root.Appenders.ToArray()
                .FirstOrDefault(a => a is TestableAsyncAppender) as TestableAsyncAppender;
        }

        public static void SetElasticsearchAppender(this ILog log, TestableAsyncAppender appender)
        {
            var hierarchy = (Hierarchy)log.Logger.Repository;
            var existingAppender = log.GetElasticsearchAppender();
            if (existingAppender is not null)
            {
                var removedAppender = hierarchy.Root.RemoveAppender(existingAppender);
                removedAppender.Close();
            }

            hierarchy.Root.AddAppender(appender);
        }
    }
}
