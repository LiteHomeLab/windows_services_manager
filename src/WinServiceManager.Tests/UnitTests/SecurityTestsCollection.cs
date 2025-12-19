using Xunit;

namespace WinServiceManager.Tests.UnitTests
{
    /// <summary>
    /// Test collection for organizing all security-related unit tests
    /// This allows running security tests as a separate group if needed
    /// </summary>
    [CollectionDefinition("Security Tests")]
    public class SecurityTestsCollection : ICollectionFixture<SecurityTestsFixture>
    {
    }

    /// <summary>
    /// Fixture for security tests - can be used to set up common test data
    /// </summary>
    public class SecurityTestsFixture
    {
        /// <summary>
        /// Initialize security test fixture
        /// </summary>
        public SecurityTestsFixture()
        {
            // Any common setup for security tests can go here
            // For example: creating test directories, setting up test data, etc.
        }
    }
}