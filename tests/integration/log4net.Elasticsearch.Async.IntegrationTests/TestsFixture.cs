using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace log4net.Elasticsearch.Async.IntegrationTests
{
    [Collection("Appender integration tests collection")]
    public class TestsFixture : IDisposable
    {
        private readonly ILog _log;

        public TestsFixture()
        {
            // Set up
            _log = LogManager.GetLogger(typeof(TestsFixture));

            // this.RegisterAppDomainEvents();
            // This doesn't work in the test project,
            // therefore the shutdown is registered on test termination, in the Dispose method.
        }

        private void RegisterAppDomainEvents()
        {
            /*
             * The .NET standard implementation of log4net doesn't attach to the AppDomain events
             * for shutdown, because it targets netstandard1.3 and AppDomain was reintroduced in netstandard2.0.
             * Manual shutdown must be set in place, in order to ensure finalization of the appenders (expecially the async ones).
             * 
             * ref: https://github.com/apache/logging-log4net/blob/master/src/Core/LoggerManager.cs#L167
             */

            void shutdown(object e, EventArgs o) => _log?.Logger.Repository?.Shutdown();

            // ProcessExit seems to be fired if we are part of the default domain
            AppDomain.CurrentDomain.ProcessExit += shutdown;

            // Otherwise DomainUnload is fired
            AppDomain.CurrentDomain.DomainUnload += shutdown;
        }

        public void Dispose()
        {
            // Tear down
            var appender = _log.GetElasticsearchAppender();

            _log.Logger.Repository.Shutdown();

            Assert.False(appender.IsProcessing);
        }
    }

    [CollectionDefinition("Appender integration tests collection")]
    public class AppenderTestsCollection : ICollectionFixture<TestsFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
