﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Linq;

namespace Tests
{
    internal class TestTraceListener : TraceListener
    {
        public int WritesCount => _writesCount;

        private int _writesCount = 0;
        private readonly bool _writeToTestContext;

        public TestTraceListener(bool writeToTestContext = false)
        {
            _writeToTestContext = writeToTestContext;
        }

        public override void Write(string message)
        {
            if (_writeToTestContext) TestContext.Write(message);
            Interlocked.Increment(ref _writesCount);
        }

        public override void WriteLine(string message)
        {
            if (_writeToTestContext) TestContext.WriteLine(message);
            Interlocked.Increment(ref _writesCount);
        }
    }
}