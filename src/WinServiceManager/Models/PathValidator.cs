using System;
using System.IO;
using System.Text.RegularExpressions;

namespace WinServiceManager.Models
{
    /// <summary>
    /// Path validation utilities to prevent path traversal attacks
    /// </summary>
    public static class PathValidator
    {
        // Regex to detect dangerous path patterns
        private static readonly Regex DangerousPathPattern = new Regex(
            @"[~]|(\.\.[\\/])|([\\/]\.\.[\\/])|^(\.\.[\\/])|([\\/]\.\.$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        /// <summary>
        /// Validates if a path is safe and prevents traversal attacks
        /// </summary>
        /// <param name="path">The path to validate</param>
        /// <returns>True if the path is safe, false otherwise</returns>
        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                // Check for null or empty
                if (string.IsNullOrEmpty(path))
                    return false;

                // Check for dangerous patterns
                if (DangerousPathPattern.IsMatch(path))
                    return false;

                // Get full path to resolve relative paths
                string fullPath = Path.GetFullPath(path);

                // Check for invalid characters
                if (fullPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                    return false;

                // Prevent UNC paths that could be used for network attacks
                if (fullPath.StartsWith(@"\\"))
                    return false;

                // Ensure the path is within reasonable bounds
                if (fullPath.Length > 260) // Windows MAX_PATH limit
                    return false;

                // Additional check: ensure path doesn't reference system directories directly
                string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                if (fullPath.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase) ||
                    fullPath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase) ||
                    fullPath.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase))
                {
                    // Allow only specific subdirectories that are safe
                    if (!IsAllowedSystemDirectory(fullPath))
                        return false;
                }

                return true;
            }
            catch
            {
                // Any exception means the path is invalid
                return false;
            }
        }

        /// <summary>
        /// Gets a safe, validated full path
        /// </summary>
        /// <param name="path">The path to validate and normalize</param>
        /// <returns>The validated full path</returns>
        /// <exception cref="ArgumentException">Thrown when the path is invalid</exception>
        public static string GetSafePath(string path)
        {
            if (!IsValidPath(path))
                throw new ArgumentException($"Invalid or unsafe path: {path}", nameof(path));

            return Path.GetFullPath(path);
        }

        /// <summary>
        /// Checks if a path is within an allowed system directory
        /// </summary>
        private static bool IsAllowedSystemDirectory(string fullPath)
        {
            // Define safe subdirectories
            string[] allowedPaths = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64")
            };

            foreach (string allowedPath in allowedPaths)
            {
                if (fullPath.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase))
                {
                    // Additional check to ensure we're not accessing critical system files
                    string fileName = Path.GetFileName(fullPath).ToLowerInvariant();
                    string[] forbiddenFiles = { "cmd.exe", "powershell.exe", "regedit.exe", "taskmgr.exe" };

                    if (Array.Exists(forbiddenFiles, f => f == fileName))
                        return false;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Validates file name to prevent injection
        /// </summary>
        /// <param name="fileName">The file name to validate</param>
        /// <returns>True if the file name is safe</returns>
        public static bool IsValidFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            // Check for invalid characters
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;

            // Check for dangerous patterns
            if (DangerousPathPattern.IsMatch(fileName))
                return false;

            // Prevent reserved names
            string[] reservedNames = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();

            if (Array.Exists(reservedNames, r => r == nameWithoutExt))
                return false;

            return true;
        }
    }
}