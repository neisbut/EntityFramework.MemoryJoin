using System;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EntityFrameworkCore.MemoryJoin.IntegrationTests.Utils
{
    /// <summary>
    /// An implementation of <see cref="ILoggerFactory"/> that will produce <see cref="TestLogger"/>s.
    /// </summary>
    internal class TestLoggerFactory : ILoggerFactory
    {
		private readonly Func<TestContext> testContextFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestLoggerFactory"/> class.
        /// </summary>
        /// <param name="testContextFactory">
        /// A factory for producing <see cref="ITestOutputHelper"/>s. When calling upon this factory,
        /// it is expected to product a <see cref="ITestOutputHelper"/> for the active test case.
        /// </param>
        public TestLoggerFactory(Func<TestContext> testContextFactory) => this.testContextFactory = testContextFactory;

        /// <inheritdoc/>
        public void AddProvider(ILoggerProvider provider)
        {
            // Ignore any providers passed to this factory.
        }

        /// <inheritdoc/>
        public ILogger CreateLogger(string categoryName) => new TestLogger(this.testContextFactory());

        /// <inheritdoc/>
        public void Dispose()
        {
            // Intentionally left blank as there are no resources to dispose
        }
    }
}
