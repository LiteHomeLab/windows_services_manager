using System;

namespace WinServiceManager.Tests.UnitTests
{
    /// <summary>
    /// Custom attribute to provide file path data for unit tests
    /// This helps distinguish path-related test data from other string parameters
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class FilePathAttribute : Attribute
    {
        /// <summary>
        /// Indicates that the parameter represents a file path
        /// </summary>
        public FilePathAttribute()
        {
        }
    }
}