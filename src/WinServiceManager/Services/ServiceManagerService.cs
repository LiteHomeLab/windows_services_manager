using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using WinServiceManager.Models;

namespace WinServiceManager.Services
{
    public class ServiceManagerService
    {
        private readonly WinSWWrapper _winswWrapper;
        private readonly IDataStorageService _dataStorage;

        public ServiceManagerService(WinSWWrapper winswWrapper, IDataStorageService dataStorage)
        {
            _winswWrapper = winswWrapper;
            _dataStorage = dataStorage;
        }

        public async Task<List<ServiceItem>> GetAllServicesAsync()
        {
            var services = await _dataStorage.LoadServicesAsync();

            // 更新每个服务的实际状态
            foreach (var service in services)
            {
                var oldResult = service.LastStartupResult;

                service.Status = GetActualServiceStatus(service);

                // 如果启动结果为 Unknown，根据当前状态自动设置
                if (service.LastStartupResult == StartupResult.Unknown)
                {
                    service.LastStartupResult = service.Status switch
                    {
                        ServiceStatus.Running => StartupResult.Success,
                        ServiceStatus.Starting => StartupResult.Warning,
                        ServiceStatus.Stopped => StartupResult.Failed,
                        ServiceStatus.Error => StartupResult.Failed,
                        ServiceStatus.Stopping => StartupResult.Warning,
                        ServiceStatus.Paused => StartupResult.Warning,
                        // NotInstalled 保持 Unknown（可能刚创建还未安装）
                        _ => StartupResult.Unknown
                    };

                    // 如果启动结果有变化，保存更新
                    if (service.LastStartupResult != oldResult && service.LastStartupResult != StartupResult.Unknown)
                    {
                        service.UpdatedAt = DateTime.Now;
                        await _dataStorage.UpdateServiceAsync(service);
                    }
                }
            }

            return services;
        }

        public async Task<ServiceOperationResult<string>> CreateServiceAsync(ServiceCreateRequest request)
        {
            var service = new ServiceItem
            {
                DisplayName = request.DisplayName,
                Description = request.Description ?? "Managed by WinServiceManager",
                ExecutablePath = request.ExecutablePath,
                ScriptPath = request.ScriptPath,
                Arguments = request.Arguments,
                WorkingDirectory = request.WorkingDirectory,
                Status = ServiceStatus.Installing,
                Dependencies = request.Dependencies ?? new List<string>(),
                EnvironmentVariables = request.EnvironmentVariables ?? new Dictionary<string, string>(),
                ServiceAccount = request.ServiceAccount,
                StartMode = ParseStartMode(request.StartMode),
                StopTimeout = request.StopTimeout,
                EnableRestartOnExit = request.EnableRestartOnExit,
                RestartExitCode = request.RestartExitCode
            };

            try
            {
                // 先尝试安装服务，仅在成功后才保存到存储
                var result = await _winswWrapper.InstallServiceAsync(service);

                if (!result.Success)
                {
                    // 安装失败，不保存到存储
                    return ServiceOperationResult<string>.FailureResult(ServiceOperationType.Install, result.ErrorMessage ?? "安装失败");
                }

                // 安装成功，保存到存储
                await _dataStorage.AddServiceAsync(service);

                if (request.AutoStart)
                {
                    // 如果需要自动启动
                    var startResult = await _winswWrapper.StartServiceAsync(service);
                    if (startResult.Success)
                    {
                        service.Status = ServiceStatus.Running;
                    }
                    else
                    {
                        service.Status = ServiceStatus.Stopped;
                    }
                }
                else
                {
                    service.Status = ServiceStatus.Stopped;
                }

                // 更新状态
                service.UpdatedAt = DateTime.Now;
                await _dataStorage.UpdateServiceAsync(service);

                return ServiceOperationResult<string>.SuccessResult(service.Id, ServiceOperationType.Install);
            }
            catch (Exception ex)
            {
                // 如果已经保存到存储，尝试清理
                try
                {
                    await _dataStorage.DeleteServiceAsync(service.Id);
                }
                catch
                {
                    // 忽略删除失败
                }

                return ServiceOperationResult<string>.FailureResult(ServiceOperationType.Install, ex.Message);
            }
        }

        public async Task<ServiceOperationResult> InstallServiceAsync(ServiceItem service)
        {
            return await _winswWrapper.InstallServiceAsync(service);
        }

        public async Task<ServiceOperationResult> StartServiceAsync(ServiceItem service)
        {
            var result = await _winswWrapper.StartServiceAsync(service);

            if (result.Success)
            {
                service.Status = ServiceStatus.Running;
                service.LastStartupResult = StartupResult.Success;
                service.LastStartupErrorMessage = null;
                service.LastStartupTime = DateTime.Now;
                service.UpdatedAt = DateTime.Now;
                await _dataStorage.UpdateServiceAsync(service);
            }
            else
            {
                service.LastStartupResult = StartupResult.Failed;
                service.LastStartupErrorMessage = result.ErrorMessage;
                service.LastStartupTime = DateTime.Now;
                await _dataStorage.UpdateServiceAsync(service);
            }

            return result;
        }

        public async Task<ServiceOperationResult> StopServiceAsync(ServiceItem service)
        {
            var result = await _winswWrapper.StopServiceAsync(service);

            if (result.Success)
            {
                service.Status = ServiceStatus.Stopped;
                service.UpdatedAt = DateTime.Now;
                await _dataStorage.UpdateServiceAsync(service);
            }

            return result;
        }

        public async Task<ServiceOperationResult> RestartServiceAsync(ServiceItem service)
        {
            var stopResult = await StopServiceAsync(service);
            if (!stopResult.Success)
            {
                return stopResult;
            }

            // 等待一下确保服务完全停止
            await Task.Delay(1000);

            return await StartServiceAsync(service);
        }

        public async Task<ServiceOperationResult> UninstallServiceAsync(ServiceItem service)
        {
            // 先停止服务（如果正在运行）
            if (service.Status == ServiceStatus.Running)
            {
                var stopResult = await _winswWrapper.StopServiceAsync(service);
                if (!stopResult.Success)
                {
                    return stopResult;
                }
            }

            // 卸载服务
            var result = await _winswWrapper.UninstallServiceAsync(service);

            if (result.Success)
            {
                // 从数据存储中删除
                await _dataStorage.DeleteServiceAsync(service.Id);
            }

            return result;
        }

        /// <summary>
        /// 从数据存储中删除服务（无论是否已安装）
        /// </summary>
        /// <param name="serviceId">服务ID</param>
        /// <returns>操作结果</returns>
        public async Task<ServiceOperationResult> DeleteServiceAsync(string serviceId)
        {
            // 加载服务信息
            var services = await _dataStorage.LoadServicesAsync();
            var service = services.FirstOrDefault(s => s.Id == serviceId);

            if (service == null)
            {
                return ServiceOperationResult.FailureResult(ServiceOperationType.Delete, $"服务ID {serviceId} 不存在");
            }

            // 禁止删除正在运行的服务
            if (service.Status == ServiceStatus.Running)
            {
                return ServiceOperationResult.FailureResult(ServiceOperationType.Delete, "无法删除正在运行的服务，请先停止服务");
            }

            // 如果服务已安装，尝试卸载
            if (service.Status != ServiceStatus.NotInstalled && service.Status != ServiceStatus.Error)
            {
                var uninstallResult = await _winswWrapper.UninstallServiceAsync(service);
                // 即使卸载失败，也继续从存储中删除
            }

            // 从数据存储中删除
            await _dataStorage.DeleteServiceAsync(serviceId);

            return ServiceOperationResult.SuccessResult(ServiceOperationType.Delete);
        }

        /// <summary>
        /// 根据服务ID获取服务状态
        /// </summary>
        /// <param name="serviceId">服务ID</param>
        /// <returns>服务状态</returns>
        public async Task<ServiceStatus> GetServiceStatusByIdAsync(string serviceId)
        {
            var service = await _dataStorage.LoadServiceAsync(serviceId);
            if (service == null)
            {
                return ServiceStatus.NotInstalled;
            }

            var status = GetActualServiceStatus(service);
            return status;
        }

        public ServiceStatus GetActualServiceStatus(ServiceItem service)
        {
            try
            {
                // 检查服务是否在Windows服务列表中
                var sc = new ServiceController(service.Id);

                switch (sc.Status)
                {
                    case ServiceControllerStatus.Running:
                        return ServiceStatus.Running;
                    case ServiceControllerStatus.Stopped:
                        return ServiceStatus.Stopped;
                    case ServiceControllerStatus.StartPending:
                        return ServiceStatus.Starting;
                    case ServiceControllerStatus.StopPending:
                        return ServiceStatus.Stopping;
                    case ServiceControllerStatus.Paused:
                        return ServiceStatus.Paused;
                    case ServiceControllerStatus.PausePending:
                        return ServiceStatus.Stopping;
                    case ServiceControllerStatus.ContinuePending:
                        return ServiceStatus.Starting;
                    default:
                        return ServiceStatus.Stopped;
                }
            }
            catch
            {
                // 如果无法获取服务状态，可能是未安装
                return ServiceStatus.NotInstalled;
            }
        }

        /// <summary>
        /// 解析启动模式字符串为枚举值
        /// </summary>
        /// <param name="startMode">启动模式字符串</param>
        /// <returns>启动模式枚举值</returns>
        private static ServiceStartupMode ParseStartMode(string? startMode)
        {
            if (string.IsNullOrEmpty(startMode))
                return ServiceStartupMode.Automatic;

            return startMode.ToLowerInvariant() switch
            {
                "automatic" => ServiceStartupMode.Automatic,
                "manual" => ServiceStartupMode.Manual,
                "disabled" => ServiceStartupMode.Disabled,
                _ => ServiceStartupMode.Automatic
            };
        }

        /// <summary>
        /// 更新服务配置
        /// </summary>
        /// <param name="request">更新服务请求</param>
        /// <returns>操作结果</returns>
        public async Task<ServiceOperationResult> UpdateServiceAsync(ServiceUpdateRequest request)
        {
            try
            {
                // 加载现有服务
                var services = await _dataStorage.LoadServicesAsync();
                var existingService = services.FirstOrDefault(s => s.Id == request.Id);

                if (existingService == null)
                {
                    return ServiceOperationResult.FailureResult(ServiceOperationType.Update, $"服务ID {request.Id} 不存在");
                }

                // 更新服务属性
                existingService.DisplayName = request.DisplayName;
                existingService.Description = request.Description ?? "Managed by WinServiceManager";
                existingService.ExecutablePath = request.ExecutablePath;
                existingService.ScriptPath = request.ScriptPath;
                existingService.Arguments = request.Arguments;
                existingService.WorkingDirectory = request.WorkingDirectory;
                existingService.Dependencies = request.Dependencies ?? new List<string>();
                existingService.EnvironmentVariables = request.EnvironmentVariables ?? new Dictionary<string, string>();
                existingService.ServiceAccount = request.ServiceAccount;
                existingService.StartMode = ParseStartMode(request.StartMode);
                existingService.StopTimeout = request.StopTimeout;
                existingService.EnableRestartOnExit = request.EnableRestartOnExit;
                existingService.RestartExitCode = request.RestartExitCode;
                existingService.UpdatedAt = DateTime.Now;

                // 确保服务目录存在
                if (!Directory.Exists(existingService.ServiceDirectory))
                {
                    Directory.CreateDirectory(existingService.ServiceDirectory);
                }

                // 生成新的 WinSW 配置文件
                var configContent = existingService.GenerateWinSWConfig();
                await File.WriteAllTextAsync(existingService.WinSWConfigPath, configContent);

                // 更新数据存储
                await _dataStorage.UpdateServiceAsync(existingService);

                return ServiceOperationResult.SuccessResult(ServiceOperationType.Update);
            }
            catch (Exception ex)
            {
                return ServiceOperationResult.FailureResult(ServiceOperationType.Update, $"更新服务配置失败: {ex.Message}");
            }
        }
    }
}