using log4net.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Tests
{
    internal class MockErrorHandler : IErrorHandler
    {
        public int ErrorsCount { get; private set; }

        public void Error(string message) => this.Error(message, null as Exception);

        public void Error(string message, Exception e) => this.Error(message, e, ErrorCode.GenericFailure);

        public void Error(string message, Exception e, ErrorCode errorCode) => this.ErrorsCount++;        
    }
}
