using System;
using log4net.Core;
using Xunit.Abstractions;

namespace Tests
{
    internal class MockErrorHandler : IErrorHandler
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public MockErrorHandler(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        public int ErrorsCount { get; private set; }

        public void Error(string message)
        {
            _testOutputHelper.WriteLine(message);
            ErrorsCount++;
        }

        public void Error(string message, Exception e) => Error(message, e, ErrorCode.GenericFailure);

        public void Error(string message, Exception e, ErrorCode errorCode) => Error(message);
    }
}
