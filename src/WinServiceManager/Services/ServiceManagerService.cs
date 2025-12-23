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
                service.Status = GetActualServiceStatus(service);
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
                StopTimeout = request.StopTimeout
            };

            try
            {
                // 先保存到数据存储
                await _dataStorage.AddServiceAsync(service);

                // 安装服务
                var result = await _winswWrapper.InstallServiceAsync(service);

                if (result.Success && request.AutoStart)
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
                else if (result.Success)
                {
                    service.Status = ServiceStatus.Stopped;
                }
                else
                {
                    service.Status = ServiceStatus.Error;
                }

                // 更新状态
                service.UpdatedAt = DateTime.Now;
                await _dataStorage.UpdateServiceAsync(service);

                if (result.Success)
                {
                    return ServiceOperationResult<string>.SuccessResult(service.Id, ServiceOperationType.Install);
                }
                else
                {
                    return ServiceOperationResult<string>.FailureResult(ServiceOperationType.Install, result.ErrorMessage ?? "安装失败");
                }
            }
            catch (Exception ex)
            {
                service.Status = ServiceStatus.Error;
                service.UpdatedAt = DateTime.Now;
                await _dataStorage.UpdateServiceAsync(service);

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
                service.UpdatedAt = DateTime.Now;
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