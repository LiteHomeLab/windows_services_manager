using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinServiceManager.Services;
using WinServiceManager.ViewModels;

namespace WinServiceManager
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 配置服务
            var services = new ServiceCollection();
            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();

            // 检查管理员权限
            if (!IsRunningAsAdministrator())
            {
                MessageBox.Show(
                    "此应用程序需要管理员权限才能正常运行。\n请以管理员身份重新运行。",
                    "需要管理员权限",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                Shutdown();
                return;
            }

            // 创建并显示主窗口
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // 添加日志
            services.AddLogging(configure => configure.AddConsole());

            // 注册服务
            services.AddSingleton<IDataStorageService, JsonDataStorageService>();
            services.AddSingleton<WinSWWrapper>();
            services.AddSingleton<ServiceManagerService>();
            services.AddSingleton<LogReaderService>();
            services.AddSingleton<ServiceStatusMonitor>();

            // 注册ViewModels
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<CreateServiceViewModel>();
            services.AddTransient<ServiceItemViewModel>();
            services.AddTransient<LogViewerViewModel>();

            // 注册Views
            services.AddTransient<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }

        private bool IsRunningAsAdministrator()
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