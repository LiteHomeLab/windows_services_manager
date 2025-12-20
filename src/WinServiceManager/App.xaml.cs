using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinServiceManager.Services;
using WinServiceManager.ViewModels;
using WinServiceManager.Views;

namespace WinServiceManager
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private ServiceProvider? _serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 配置高DPI支持
            ConfigureHighDpi();

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

            // 启动性能监控
            var performanceMonitor = _serviceProvider.GetRequiredService<IPerformanceMonitorService>();
            performanceMonitor.StartMonitoring();

            // 创建并显示主窗口
            var mainWindow = _serviceProvider.GetRequiredService<Views.MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // 添加日志
            services.AddLogging(configure =>
            {
                configure.AddConsole();
                configure.SetMinimumLevel(LogLevel.Information);
            });

            // 配置性能监控选项
            services.Configure<PerformanceMonitorOptions>(options =>
            {
                options.IntervalMs = 5000;
                options.MaxHistoryCount = 1000;
                options.CpuHighThreshold = 80;
                options.MemoryHighThresholdMB = 512;
            });

            // 注册服务
            services.AddSingleton<IDataStorageService, JsonDataStorageService>();
            services.AddSingleton<WinSWWrapper>();
            services.AddSingleton<ServiceManagerService>();
            services.AddSingleton<LogReaderService>();
            services.AddSingleton<ServiceStatusMonitor>();
            services.AddSingleton<ServiceDependencyValidator>();
            services.AddSingleton<IPerformanceMonitorService, PerformanceMonitorService>();

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

        private void ConfigureHighDpi()
        {
            // 高DPI设置已在项目文件中配置
            // SetHighDpiMode 应在 Main 方法中调用，而不是在 OnStartup 中
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