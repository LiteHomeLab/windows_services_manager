using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                service.Status = await GetActualServiceStatusAsync(service);
            }

            return services;
        }

        public async Task<ServiceOperationResult> CreateServiceAsync(ServiceCreateRequest request)
        {
            var service = new ServiceItem
            {
                DisplayName = request.DisplayName,
                Description = request.Description ?? "Managed by WinServiceManager",
                ExecutablePath = request.ExecutablePath,
                ScriptPath = request.ScriptPath,
                Arguments = request.Arguments,
                WorkingDirectory = request.WorkingDirectory,
                Status = ServiceStatus.Installing
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

                return result;
            }
            catch (Exception ex)
            {
                service.Status = ServiceStatus.Error;
                service.UpdatedAt = DateTime.Now;
                await _dataStorage.UpdateServiceAsync(service);

                return ServiceOperationResult.FailureResult(ServiceOperationType.Install, ex.Message);
            }
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

        private async Task<ServiceStatus> GetActualServiceStatusAsync(ServiceItem service)
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
    }
}