using System.Diagnostics;
using System.Threading;
using Xunit.Abstractions;

namespace Tests
{
    internal class TestTraceListener : TraceListener
    {
        public int WritesCount => _writesCount;

        private int _writesCount = 0;
        private readonly ITestOutputHelper _testOutputHelper;

        public TestTraceListener(ITestOutputHelper testOutputHelper = null)
        {
            _testOutputHelper = testOutputHelper;
        }

        public override void Write(string message)
        {
            if (_testOutputHelper is not null) _testOutputHelper.WriteLine(message);
            Interlocked.Increment(ref _writesCount);
        }

        public override void WriteLine(string message)
        {
            if (_testOutputHelper is not null) _testOutputHelper.WriteLine(message);
            Interlocked.Increment(ref _writesCount);
        }
    }
}
