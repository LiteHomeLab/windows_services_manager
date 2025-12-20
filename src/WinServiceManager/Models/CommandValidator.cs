using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace WinServiceManager.Models
{
    /// <summary>
    /// Command argument validation and sanitization utilities
    /// </summary>
    public static class CommandValidator
    {
        // Dangerous characters and patterns that could lead to command injection
        private static readonly string[] DangerousCharacters = {
            "&", "|", ";", "<", ">", "`", "$", "(", ")", "{", "}", "[", "]",
            "!", "*", "?", "~", "#", "%", "^", "\"", "'", "\\", "\n", "\r", "\t"
        };

        // Regex patterns for detecting command injection attempts
        private static readonly Regex[] DangerousPatterns = {
            new Regex(@"\|\|", RegexOptions.Compiled | RegexOptions.IgnoreCase),  // OR operator
            new Regex(@"&&", RegexOptions.Compiled | RegexOptions.IgnoreCase),   // AND operator
            new Regex(@";\s*\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase),  // Command chaining
            new Regex(@"`[^`]*`", RegexOptions.Compiled | RegexOptions.IgnoreCase),   // Command substitution
            new Regex(@"\$\([^)]*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),  // Command substitution
            new Regex(@"@\(.*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),     // Delayed expansion
            new Regex(@"%[^%]*%", RegexOptions.Compiled | RegexOptions.IgnoreCase),     // Environment variable expansion
            new Regex(@"\^\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase),       // Escape sequences
            new Regex(@"<[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase),     // Input/output redirection
            new Regex(@">>[^>]*", RegexOptions.Compiled | RegexOptions.IgnoreCase),     // Append redirection
            new Regex(@"1>&2|2>&1", RegexOptions.Compiled | RegexOptions.IgnoreCase),   // File descriptor redirection
            new Regex(@"\/dev\/(null|zero|random|urandom)", RegexOptions.Compiled | RegexOptions.IgnoreCase),  // Unix devices (shouldn't be on Windows but defensive)
        };

        // Allowed file extensions for executables
        private static readonly HashSet<string> AllowedExecutableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".bat", ".cmd", ".ps1", ".py", ".js", ".vbs", ".wsf", ".com"
        };

        /// <summary>
        /// Validates and sanitizes command arguments
        /// </summary>
        /// <param name="arguments">The arguments to validate</param>
        /// <returns>Sanitized arguments string</returns>
        /// <exception cref="ArgumentException">Thrown when arguments contain dangerous content</exception>
        public static string SanitizeArguments(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
                return string.Empty;

            // Check for dangerous patterns
            foreach (var pattern in DangerousPatterns)
            {
                if (pattern.IsMatch(arguments))
                    throw new ArgumentException($"Arguments contain potentially dangerous pattern: {pattern}");
            }

            // Additional validation for specific injection attempts
            if (ContainsCommandInjection(arguments))
            {
                throw new ArgumentException("Arguments contain potential command injection attempts");
            }

            // Quote arguments that contain spaces or special characters
            var sanitizedArgs = QuoteArguments(arguments);

            return sanitizedArgs;
        }

        /// <summary>
        /// Validates executable file path
        /// </summary>
        /// <param name="executablePath">The executable path to validate</param>
        /// <returns>True if the executable appears to be safe</returns>
        public static bool IsValidExecutable(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                return false;

            try
            {
                // First, validate the path using PathValidator
                if (!PathValidator.IsValidPath(executablePath))
                    return false;

                // Check file extension
                string extension = Path.GetExtension(executablePath);
                if (!string.IsNullOrEmpty(extension) && !AllowedExecutableExtensions.Contains(extension))
                {
                    // Allow some system executables without extension
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(executablePath).ToLowerInvariant();
                    string[] allowedSystemExes = { "cmd", "powershell", "wscript", "cscript" };

                    if (!Array.Exists(allowedSystemExes, ex => ex == fileNameWithoutExt))
                        return false;
                }

                // Check filename doesn't contain dangerous characters
                string fileName = Path.GetFileName(executablePath);
                if (ContainsDangerousCharacters(fileName))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a string contains command injection attempts
        /// </summary>
        /// <param name="input">The input to check</param>
        /// <returns>True if command injection is detected</returns>
        private static bool ContainsCommandInjection(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Check for common command injection patterns
            string[] injectionPatterns = {
                "cmd.exe", "command.com", "powershell.exe", "wscript.exe", "cscript.exe",
                "net user", "net localgroup", "whoami", "ipconfig", "netstat",
                "del ", "rmdir ", "rd ", "format ", "shutdown ", "reboot ",
                "reg add", "reg delete", "regsvr32",
                "start ", "taskkill ", "schtasks",
                "powershell -", "cmd /c", "cmd /k",
                "&", "|", ";", "<", ">", "`", "$", "\"", "'"
            };

            string lowerInput = input.ToLowerInvariant();

            foreach (string pattern in injectionPatterns)
            {
                if (lowerInput.Contains(pattern))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a string contains dangerous characters
        /// </summary>
        /// <param name="input">The input to check</param>
        /// <returns>True if dangerous characters are found</returns>
        private static bool ContainsDangerousCharacters(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            foreach (string dangerousChar in DangerousCharacters)
            {
                if (input.Contains(dangerousChar))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Properly quotes command arguments to prevent injection
        /// </summary>
        /// <param name="arguments">The arguments to quote</param>
        /// <returns>Properly quoted arguments</returns>
        private static string QuoteArguments(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
                return string.Empty;

            // Split arguments while preserving quoted parts
            var argList = ParseArguments(arguments);
            var quotedArgs = new List<string>();

            foreach (var arg in argList)
            {
                if (NeedsQuoting(arg))
                {
                    // Properly escape quotes within arguments
                    var escapedArg = arg.Replace("\"", "\\\"");
                    quotedArgs.Add($"\"{escapedArg}\"");
                }
                else
                {
                    quotedArgs.Add(arg);
                }
            }

            return string.Join(" ", quotedArgs);
        }

        /// <summary>
        /// Determines if an argument needs to be quoted
        /// </summary>
        /// <param name="argument">The argument to check</param>
        /// <returns>True if the argument needs quoting</returns>
        private static bool NeedsQuoting(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
                return false;

            // Quote if it contains spaces or special characters
            return argument.Contains(" ") ||
                   argument.Contains("\t") ||
                   argument.Contains("\"") ||
                   argument.Contains("'") ||
                   argument.StartsWith("-") ||  // Options that might be confused with commands
                   argument.StartsWith("/");
        }

        /// <summary>
        /// Parses command line arguments respecting quoted sections
        /// </summary>
        /// <param name="commandLine">The command line to parse</param>
        /// <returns>List of parsed arguments</returns>
        private static List<string> ParseArguments(string commandLine)
        {
            var args = new List<string>();
            var currentArg = new StringBuilder();
            bool inQuotes = false;
            bool escapeNext = false;

            for (int i = 0; i < commandLine.Length; i++)
            {
                char c = commandLine[i];

                if (escapeNext)
                {
                    currentArg.Append(c);
                    escapeNext = false;
                }
                else if (c == '\\')
                {
                    escapeNext = true;
                }
                else if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (currentArg.Length > 0)
                    {
                        args.Add(currentArg.ToString());
                        currentArg.Clear();
                    }
                }
                else
                {
                    currentArg.Append(c);
                }
            }

            if (currentArg.Length > 0)
            {
                args.Add(currentArg.ToString());
            }

            return args;
        }

        /// <summary>
        /// Validates and sanitizes user input for use in command arguments
        /// </summary>
        /// <param name="input">The input to validate and sanitize</param>
        /// <returns>Sanitized input string</returns>
        public static string SanitizeInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Use the existing SanitizeArguments method
            try
            {
                return SanitizeArguments(input);
            }
            catch
            {
                // If sanitization fails, return empty string for safety
                return string.Empty;
            }
        }

        /// <summary>
        /// Validates if input is safe to use in command arguments
        /// </summary>
        /// <param name="input">The input to validate</param>
        /// <returns>True if the input is safe, false otherwise</returns>
        public static bool IsValidInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            try
            {
                // Try to sanitize - if it throws exception, input is invalid
                SanitizeArguments(input);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}