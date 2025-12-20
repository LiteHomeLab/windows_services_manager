using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WinServiceManager.Models;

namespace WinServiceManager.Services
{
    /// <summary>
    /// 服务依赖验证器
    /// </summary>
    public class ServiceDependencyValidator
    {
        private readonly ILogger<ServiceDependencyValidator>? _logger;
        private readonly ServiceManagerService _serviceManager;

        public ServiceDependencyValidator(ServiceManagerService serviceManager, ILogger<ServiceDependencyValidator>? logger = null)
        {
            _serviceManager = serviceManager ?? throw new ArgumentNullException(nameof(serviceManager));
            _logger = logger;
        }

        /// <summary>
        /// 验证服务依赖关系
        /// </summary>
        /// <param name="service">要验证的服务</param>
        /// <param name="allServices">所有可用的服务列表</param>
        /// <returns>验证结果</returns>
        public async Task<DependencyValidationResult> ValidateDependenciesAsync(
            ServiceItem service,
            IEnumerable<ServiceItem>? allServices = null)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            try
            {
                _logger?.LogDebug("开始验证服务 {ServiceId} 的依赖关系", service.Id);

                var result = new DependencyValidationResult { IsValid = true };

                // 获取所有服务列表
                allServices ??= await _serviceManager.GetAllServicesAsync();

                // 1. 检查依赖服务是否存在
                await ValidateDependencyExistenceAsync(service, allServices, result);

                // 2. 检查循环依赖
                ValidateCircularDependencies(service, allServices, result);

                // 3. 计算依赖启动顺序
                CalculateStartupOrder(service, allServices, result);

                _logger?.LogDebug("服务依赖验证完成: {IsValid}, 错误数: {ErrorCount}",
                    result.IsValid, result.Errors.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "验证服务依赖时发生错误: {ServiceId}", service.Id);
                return new DependencyValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"验证过程中发生错误: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// 验证依赖服务是否存在
        /// </summary>
        private async Task ValidateDependencyExistenceAsync(
            ServiceItem service,
            IEnumerable<ServiceItem> allServices,
            DependencyValidationResult result)
        {
            if (!service.Dependencies.Any())
                return;

            var existingServiceIds = allServices.Select(s => s.Id).ToHashSet();

            foreach (var dependencyId in service.Dependencies)
            {
                if (!existingServiceIds.Contains(dependencyId))
                {
                    result.IsValid = false;
                    result.Errors.Add($"依赖服务 '{dependencyId}' 不存在");
                }
            }
        }

        /// <summary>
        /// 检查循环依赖
        /// </summary>
        private void ValidateCircularDependencies(
            ServiceItem service,
            IEnumerable<ServiceItem> allServices,
            DependencyValidationResult result)
        {
            if (!service.Dependencies.Any())
                return;

            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            if (HasCircularDependency(service.Id, allServices, visited, recursionStack))
            {
                result.IsValid = false;
                result.Errors.Add($"检测到循环依赖: {string.Join(" -> ", recursionStack)} -> {service.Id}");
            }
        }

        /// <summary>
        /// 使用DFS检测循环依赖
        /// </summary>
        private bool HasCircularDependency(
            string serviceId,
            IEnumerable<ServiceItem> allServices,
            HashSet<string> visited,
            HashSet<string> recursionStack)
        {
            if (recursionStack.Contains(serviceId))
                return true;

            if (visited.Contains(serviceId))
                return false;

            visited.Add(serviceId);
            recursionStack.Add(serviceId);

            var service = allServices.FirstOrDefault(s => s.Id == serviceId);
            if (service?.Dependencies.Any() == true)
            {
                foreach (var dependency in service.Dependencies)
                {
                    if (HasCircularDependency(dependency, allServices, visited, recursionStack))
                        return true;
                }
            }

            recursionStack.Remove(serviceId);
            return false;
        }

        /// <summary>
        /// 计算依赖启动顺序
        /// </summary>
        private void CalculateStartupOrder(
            ServiceItem service,
            IEnumerable<ServiceItem> allServices,
            DependencyValidationResult result)
        {
            if (!service.Dependencies.Any())
            {
                result.StartupOrder = new List<string> { service.Id };
                return;
            }

            var visited = new HashSet<string>();
            var order = new List<string>();

            TopologicalSort(service.Id, allServices, visited, order);
            result.StartupOrder = order;
        }

        /// <summary>
        /// 拓扑排序计算启动顺序
        /// </summary>
        private void TopologicalSort(
            string serviceId,
            IEnumerable<ServiceItem> allServices,
            HashSet<string> visited,
            List<string> order)
        {
            if (visited.Contains(serviceId))
                return;

            visited.Add(serviceId);

            var service = allServices.FirstOrDefault(s => s.Id == serviceId);
            if (service?.Dependencies.Any() == true)
            {
                foreach (var dependency in service.Dependencies)
                {
                    TopologicalSort(dependency, allServices, visited, order);
                }
            }

            if (!order.Contains(serviceId))
            {
                order.Add(serviceId);
            }
        }
    }

    /// <summary>
    /// 依赖验证结果
    /// </summary>
    public class DependencyValidationResult
    {
        /// <summary>
        /// 是否验证通过
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 错误信息列表
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// 依赖启动顺序（从依赖到服务本身）
        /// </summary>
        public List<string> StartupOrder { get; set; } = new List<string>();

        /// <summary>
        /// 获取错误摘要
        /// </summary>
        public string GetErrorSummary() => string.Join("; ", Errors);
    }

    /// <summary>
    /// 依赖验证异常
    /// </summary>
    public class DependencyValidationException : Exception
    {
        public DependencyValidationException(string message) : base(message) { }
        public DependencyValidationException(string message, Exception innerException) : base(message, innerException) { }
    }
}