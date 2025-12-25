using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WinServiceManager.Models;

namespace WinServiceManager.Services
{
    public class WinSWWrapper
    {
        private readonly string _winswTemplatePath;
        private readonly ILogger<WinSWWrapper> _logger;
        private readonly WinSWValidator _validator;

        public WinSWWrapper(ILogger<WinSWWrapper> logger, WinSWValidator validator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _winswTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates", "WinSW-x64.exe");
        }

        /// <summary>
        /// Checks if the current process is running with administrator privileges
        /// </summary>
        private bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Safely executes a WinSW command with proper validation and resource management
        /// </summary>
        private async Task<(int exitCode, string output, string error)> ExecuteWinSWCommandAsync(
            string executablePath,
            string command,
            string? additionalArgs = null)
        {
            // Validate inputs
            if (string.IsNullOrEmpty(executablePath))
                throw new ArgumentException("Executable path cannot be null or empty", nameof(executablePath));

            if (string.IsNullOrEmpty(command))
                throw new ArgumentException("Command cannot be null or empty", nameof(command));

            // Validate executable path
            if (!CommandValidator.IsValidExecutable(executablePath))
                throw new ArgumentException($"Invalid or unsafe executable: {executablePath}");

            // Validate and sanitize command and arguments
            string sanitizedCommand = CommandValidator.SanitizeArguments(command);
            string sanitizedArgs = additionalArgs != null
                ? CommandValidator.SanitizeArguments(additionalArgs)
                : string.Empty;

            string fullArguments = string.IsNullOrEmpty(sanitizedArgs)
                ? sanitizedCommand
                : $"{sanitizedCommand} {sanitizedArgs}";

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = fullArguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Verb = "runas" // Request administrator privileges
                },
                EnableRaisingEvents = true
            };

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            try
            {
                _logger.LogInformation("Starting WinSW command: {ExecutablePath} {Arguments}",
                    executablePath, fullArguments);

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        outputBuilder.AppendLine(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        errorBuilder.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                int exitCode = process.ExitCode;
                string output = outputBuilder.ToString();
                string error = errorBuilder.ToString();

                _logger.LogInformation("WinSW command completed. Exit code: {ExitCode}", exitCode);

                return (exitCode, output, error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute WinSW command");
                throw new InvalidOperationException($"Failed to execute WinSW command: {ex.Message}", ex);
            }
        }

        public async Task<ServiceOperationResult> InstallServiceAsync(ServiceItem service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Starting service installation for: {ServiceId}", service.Id);

                // Validate service paths
                if (!PathValidator.IsValidPath(service.ServiceDirectory) ||
                    !PathValidator.IsValidPath(service.WinSWExecutablePath) ||
                    !PathValidator.IsValidPath(service.WinSWConfigPath))
                {
                    throw new ArgumentException("Service contains invalid paths");
                }

                // Create service directories safely
                Directory.CreateDirectory(service.ServiceDirectory);
                Directory.CreateDirectory(service.LogDirectory);

                // Validate WinSW before copying
                var (isValid, errorMessage) = await _validator.ValidateWinSWAsync().ConfigureAwait(false);
                if (!isValid)
                {
                    throw new InvalidOperationException($"WinSW validation failed: {errorMessage}");
                }

                File.Copy(_winswTemplatePath, service.WinSWExecutablePath, true);

                // Copy wrapper script if restart on exit is enabled
                if (service.EnableRestartOnExit)
                {
                    string wrapperTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates", "wrapper.bat");

                    if (!File.Exists(wrapperTemplatePath))
                    {
                        throw new FileNotFoundException($"Wrapper script template not found: {wrapperTemplatePath}");
                    }

                    string wrapperDestinationPath = Path.Combine(service.ServiceDirectory, "wrapper.bat");
                    File.Copy(wrapperTemplatePath, wrapperDestinationPath, true);

                    _logger.LogInformation("Wrapper script copied to: {WrapperPath}", wrapperDestinationPath);
                }

                // Generate and write configuration file
                var config = service.GenerateWinSWConfig();
                await File.WriteAllTextAsync(service.WinSWConfigPath, config);

                // Execute install command safely
                var (exitCode, output, error) = await ExecuteWinSWCommandAsync(
                    service.WinSWExecutablePath,
                    "install");

                stopwatch.Stop();

                if (exitCode == 0)
                {
                    _logger.LogInformation("Service {ServiceId} installed successfully", service.Id);
                    return ServiceOperationResult.SuccessResult(ServiceOperationType.Install, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogError("Service {ServiceId} installation failed. Exit code: {ExitCode}, Error: {Error}",
                        service.Id, exitCode, error);
                    return ServiceOperationResult.FailureResult(ServiceOperationType.Install,
                        "Installation failed", error, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Exception during service installation: {ServiceId}", service?.Id);
                return ServiceOperationResult.FailureResult(ServiceOperationType.Install,
                    ex.Message, null, stopwatch.ElapsedMilliseconds);
            }
        }

        public async Task<ServiceOperationResult> UninstallServiceAsync(ServiceItem service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Starting service uninstallation for: {ServiceId}", service.Id);

                // Execute uninstall command safely
                var (exitCode, output, error) = await ExecuteWinSWCommandAsync(
                    service.WinSWExecutablePath,
                    "uninstall");

                stopwatch.Stop();

                if (exitCode == 0)
                {
                    _logger.LogInformation("Service {ServiceId} uninstalled successfully", service.Id);

                    // Clean up service directory safely
                    try
                    {
                        if (Directory.Exists(service.ServiceDirectory))
                        {
                            Directory.Delete(service.ServiceDirectory, true);
                            _logger.LogInformation("Service directory cleaned up: {ServiceDirectory}", service.ServiceDirectory);
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogWarning(cleanupEx, "Failed to clean up service directory: {ServiceDirectory}", service.ServiceDirectory);
                        // Don't fail the operation if cleanup fails
                    }

                    return ServiceOperationResult.SuccessResult(ServiceOperationType.Uninstall, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogError("Service {ServiceId} uninstallation failed. Exit code: {ExitCode}, Error: {Error}",
                        service.Id, exitCode, error);
                    return ServiceOperationResult.FailureResult(ServiceOperationType.Uninstall,
                        "Uninstallation failed", error, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Exception during service uninstallation: {ServiceId}", service?.Id);
                return ServiceOperationResult.FailureResult(ServiceOperationType.Uninstall,
                    ex.Message, null, stopwatch.ElapsedMilliseconds);
            }
        }

        public async Task<ServiceOperationResult> StartServiceAsync(ServiceItem service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Starting service: {ServiceId}", service.Id);

                // Execute start command safely
                var (exitCode, output, error) = await ExecuteWinSWCommandAsync(
                    service.WinSWExecutablePath,
                    "start");

                stopwatch.Stop();

                if (exitCode == 0)
                {
                    _logger.LogInformation("Service {ServiceId} started successfully", service.Id);
                    return ServiceOperationResult.SuccessResult(ServiceOperationType.Start, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogError("Service {ServiceId} start failed. Exit code: {ExitCode}, Error: {Error}",
                        service.Id, exitCode, error);
                    return ServiceOperationResult.FailureResult(ServiceOperationType.Start,
                        "Start failed", error, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Exception during service start: {ServiceId}", service?.Id);
                return ServiceOperationResult.FailureResult(ServiceOperationType.Start,
                    ex.Message, null, stopwatch.ElapsedMilliseconds);
            }
        }

        public async Task<ServiceOperationResult> StopServiceAsync(ServiceItem service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Stopping service: {ServiceId}", service.Id);

                // Execute stop command safely
                var (exitCode, output, error) = await ExecuteWinSWCommandAsync(
                    service.WinSWExecutablePath,
                    "stop");

                stopwatch.Stop();

                if (exitCode == 0)
                {
                    _logger.LogInformation("Service {ServiceId} stopped successfully", service.Id);
                    return ServiceOperationResult.SuccessResult(ServiceOperationType.Stop, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogError("Service {ServiceId} stop failed. Exit code: {ExitCode}, Error: {Error}",
                        service.Id, exitCode, error);
                    return ServiceOperationResult.FailureResult(ServiceOperationType.Stop,
                        "Stop failed", error, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Exception during service stop: {ServiceId}", service?.Id);
                return ServiceOperationResult.FailureResult(ServiceOperationType.Stop,
                    ex.Message, null, stopwatch.ElapsedMilliseconds);
            }
        }
    }
}