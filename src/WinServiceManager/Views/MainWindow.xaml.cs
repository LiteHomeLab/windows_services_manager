using System;
using System.Windows;
using WinServiceManager.ViewModels;

namespace WinServiceManager.Views
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
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 检查管理员权限
            if (!IsRunningAsAdministrator())
            {
                MessageBox.Show(
                    "此应用程序需要管理员权限才能正常工作。\n\n请以管理员身份重新运行此程序。",
                    "需要管理员权限",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Close();
                return;
            }
        }

        private static bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}