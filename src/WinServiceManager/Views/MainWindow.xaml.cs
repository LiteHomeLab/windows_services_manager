using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WinServiceManager.ViewModels;
using WinServiceManager.Models;

namespace WinServiceManager
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;

        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;

            // 设置状态转换器
            Resources["ServiceStatusConverter"] = new ServiceStatusConverter();
        }

        private async void BtnNewService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _viewModel.CreateNewServiceAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建服务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lvServices.SelectedItem is WinServiceManager.Models.ServiceItem selectedService)
                {
                    await _viewModel.StartServiceAsync(selectedService);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动服务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lvServices.SelectedItem is WinServiceManager.Models.ServiceItem selectedService)
                {
                    await _viewModel.StopServiceAsync(selectedService);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"停止服务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lvServices.SelectedItem is WinServiceManager.Models.ServiceItem selectedService)
                {
                    await _viewModel.RestartServiceAsync(selectedService);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重启服务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lvServices.SelectedItem is WinServiceManager.Models.ServiceItem selectedService)
                {
                    var result = MessageBox.Show(
                        $"确定要卸载服务 '{selectedService.DisplayName}' 吗？\n此操作不可撤销。",
                        "确认卸载",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        await _viewModel.UninstallServiceAsync(selectedService);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"卸载服务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _viewModel.RefreshServicesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新服务列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnViewLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lvServices.SelectedItem is WinServiceManager.Models.ServiceItem selectedService)
                {
                    await _viewModel.ViewServiceLogsAsync(selectedService);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开日志窗口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            // 实现排序逻辑
            if (sender is GridViewColumnHeader header && header.Content is string columnName)
            {
                _viewModel.SortServices(columnName);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // 释放 ViewModel 资源
            if (_viewModel is IDisposable disposableViewModel)
            {
                disposableViewModel.Dispose();
            }
        }
    }

    /// <summary>
    /// 服务状态转换器
    /// </summary>
    public class ServiceStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is WinServiceManager.Models.ServiceStatus status)
            {
                return status.GetDisplayText();
            }
            return "未知";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}