using System;
using log4net.Core;

namespace Tests
{
    internal class MockErrorHandler : IErrorHandler
    {
        public int ErrorsCount { get; private set; }

        public void Error(string message)
        {
            NUnit.Framework.TestContext.Out.WriteLine(message);
            ErrorsCount++;
        }

        public void Error(string message, Exception e) => Error(message, e, ErrorCode.GenericFailure);

        public void Error(string message, Exception e, ErrorCode errorCode) => Error(message);
    }
}
