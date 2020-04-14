using System;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EntityFrameworkCore.MemoryJoin.IntegrationTests.Utils
{
	/// <summary>
	/// An implementation of <see cref="ILogger"/> that will attach the logs to the test case output.
	/// </summary>
	internal class TestLogger : ILogger
    {
        private readonly TestContext context;

		public TestLogger(TestContext context) => this.context = context;

        /// <inheritdoc/>
        public IDisposable BeginScope<TState>(TState state) => StubScope.Instance;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            try
            {
                this.context.WriteLine($"[{logLevel}]: {formatter(state, exception)} (EventId: {eventId})");
            }
            catch (InvalidOperationException)
            {
                // InvalidOperationExceptions are thrown when no test case is running (e.g. during Dispose()).
                // As such, if the service is logging something during disposal, we can't log it associated to
                // the test case.
            }
        }

        private class StubScope : IDisposable
        {
            public static readonly StubScope Instance = new StubScope();

            public void Dispose()
            {
                // Method intentionally left empty.
            }
        }
    }
}
