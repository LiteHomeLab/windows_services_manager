using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WinServiceManager.Models;
using WinServiceManager.Services;

namespace WinServiceManager.ViewModels
{
    public class MainWindowViewModel : BaseViewModel, IDisposable
    {
        private readonly ServiceManagerService _serviceManager;
        private readonly ServiceStatusMonitor _statusMonitor;
        private string _statusMessage = "就绪";
        private int _serviceCount;
        private bool _disposed = false;

        public ObservableCollection<ServiceItem> Services { get; } = new();

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public int ServiceCount
        {
            get => _serviceCount;
            set => SetProperty(ref _serviceCount, value);
        }

        public MainWindowViewModel(ServiceManagerService serviceManager, ServiceStatusMonitor statusMonitor)
        {
            _serviceManager = serviceManager;
            _statusMonitor = statusMonitor;

            // 订阅状态更新
            _statusMonitor.Subscribe(OnServicesUpdated);

            // 初始加载
            _ = RefreshServicesAsync();
        }

        private void OnServicesUpdated(System.Collections.Generic.List<ServiceItem> services)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Services.Clear();
                foreach (var service in services)
                {
                    Services.Add(service);
                }
                ServiceCount = services.Count;
            });
        }

        public async Task CreateNewServiceAsync()
        {
            // TODO: 打开创建服务对话框
            StatusMessage = "功能开发中...";
            await Task.Delay(1000);
            StatusMessage = "就绪";
        }

        public async Task StartServiceAsync(ServiceItem service)
        {
            if (!service.Status.CanStart())
            {
                MessageBox.Show($"服务当前状态 ({service.Status.GetDisplayText()}) 不允许启动", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            StatusMessage = $"正在启动服务 {service.DisplayName}...";
            var result = await _serviceManager.StartServiceAsync(service);

            if (result.Success)
            {
                StatusMessage = $"服务 {service.DisplayName} 启动成功";
            }
            else
            {
                StatusMessage = $"启动失败: {result.ErrorMessage}";
                MessageBox.Show(result.ErrorMessage, "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            await Task.Delay(2000);
            StatusMessage = "就绪";
        }

        public async Task StopServiceAsync(ServiceItem service)
        {
            if (!service.Status.CanStop())
            {
                MessageBox.Show($"服务当前状态 ({service.Status.GetDisplayText()}) 不允许停止", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            StatusMessage = $"正在停止服务 {service.DisplayName}...";
            var result = await _serviceManager.StopServiceAsync(service);

            if (result.Success)
            {
                StatusMessage = $"服务 {service.DisplayName} 停止成功";
            }
            else
            {
                StatusMessage = $"停止失败: {result.ErrorMessage}";
                MessageBox.Show(result.ErrorMessage, "停止失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            await Task.Delay(2000);
            StatusMessage = "就绪";
        }

        public async Task RestartServiceAsync(ServiceItem service)
        {
            StatusMessage = $"正在重启服务 {service.DisplayName}...";
            var result = await _serviceManager.RestartServiceAsync(service);

            if (result.Success)
            {
                StatusMessage = $"服务 {service.DisplayName} 重启成功";
            }
            else
            {
                StatusMessage = $"重启失败: {result.ErrorMessage}";
                MessageBox.Show(result.ErrorMessage, "重启失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            await Task.Delay(2000);
            StatusMessage = "就绪";
        }

        public async Task UninstallServiceAsync(ServiceItem service)
        {
            StatusMessage = $"正在卸载服务 {service.DisplayName}...";
            var result = await _serviceManager.UninstallServiceAsync(service);

            if (result.Success)
            {
                StatusMessage = $"服务 {service.DisplayName} 卸载成功";
                await RefreshServicesAsync();
            }
            else
            {
                StatusMessage = $"卸载失败: {result.ErrorMessage}";
                MessageBox.Show(result.ErrorMessage, "卸载失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            await Task.Delay(2000);
            StatusMessage = "就绪";
        }

        public async Task RefreshServicesAsync()
        {
            StatusMessage = "正在刷新服务列表...";
            try
            {
                var services = await _serviceManager.GetAllServicesAsync();
                OnServicesUpdated(services);
                StatusMessage = $"已刷新 {services.Count} 个服务";
            }
            catch (Exception ex)
            {
                StatusMessage = $"刷新失败: {ex.Message}";
                MessageBox.Show($"刷新服务列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            await Task.Delay(2000);
            StatusMessage = "就绪";
        }

        public async Task ViewServiceLogsAsync(ServiceItem service)
        {
            // TODO: 打开日志查看窗口
            StatusMessage = "日志查看功能开发中...";
            await Task.Delay(1000);
            StatusMessage = "就绪";
        }

        public void SortServices(string columnName)
        {
            // TODO: 实现排序逻辑
            StatusMessage = $"按 {columnName} 排序功能开发中...";
            _ = Task.Delay(1000).ContinueWith(_ => StatusMessage = "就绪");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // 取消订阅状态监控
                _statusMonitor?.Unsubscribe(OnServicesUpdated);
                _disposed = true;
            }
        }
    }
}