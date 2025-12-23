using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WinServiceManager.ViewModels;

namespace WinServiceManager.Views
{
    /// <summary>
    /// SettingsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            // 从 DI 容器获取 ViewModel
            var viewModel = App.Services?.GetService<SettingsViewModel>();
            if (viewModel != null)
            {
                viewModel.SetOwnerWindow(this);
                DataContext = viewModel;
            }
        }
    }
}
