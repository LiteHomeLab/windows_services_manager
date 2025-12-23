using System.Windows;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WinServiceManager.Services;
using WinServiceManager.ViewModels;
using WinServiceManager.Views;
using Microsoft.Extensions.Logging.Log4Net.AspNetCore;

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

            // 初始化日志目录
            InitializeLogDirectory();

            // 配置服务
            var services = new ServiceCollection();
            ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();

            // 添加全局异常处理
            SetupExceptionHandling();

            // 记录启动信息
            var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            logger.LogInformation("WinServiceManager application starting up...");

            // 检查管理员权限
            if (!IsRunningAsAdministrator())
            {
                logger.LogError("Application not running as administrator");
                MessageBox.Show(
                    "此应用程序需要管理员权限才能正常运行。\n请以管理员身份重新运行。",
                    "需要管理员权限",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                Shutdown();
                return;
            }

            logger.LogInformation("Administrator privileges verified");

            // 启动性能监控
            var performanceMonitor = _serviceProvider.GetRequiredService<IPerformanceMonitorService>();
            performanceMonitor.StartMonitoring();

            // 创建并显示主窗口
            var mainWindow = _serviceProvider.GetRequiredService<Views.MainWindow>();
            mainWindow.Show();

            logger.LogInformation("Main window displayed successfully");
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // 添加日志 - 集成 log4net
            services.AddLogging(configure =>
            {
                configure.AddLog4Net();
                configure.AddConsole();
                configure.SetMinimumLevel(LogLevel.Debug);
                configure.AddFilter("Microsoft", LogLevel.Warning);
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

        /// <summary>
        /// 初始化日志目录
        /// </summary>
        private void InitializeLogDirectory()
        {
            try
            {
                var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create logs directory: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置全局异常处理
        /// </summary>
        private void SetupExceptionHandling()
        {
            // 处理 UI 线程未捕获的异常
            this.DispatcherUnhandledException += (sender, e) =>
            {
                var logger = _serviceProvider?.GetRequiredService<ILogger<App>>();
                logger?.LogCritical(e.Exception, "Unhandled UI thread exception: {ExceptionMessage}", e.Exception.Message);

                MessageBox.Show(
                    $"发生未处理的异常：{e.Exception.Message}\n\n详细信息已记录到日志文件。",
                    "严重错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                e.Handled = true;
            };

            // 处理非 UI 线程未捕获的异常
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var logger = _serviceProvider?.GetRequiredService<ILogger<App>>();
                if (e.ExceptionObject is Exception ex)
                {
                    logger?.LogCritical(ex, "Unhandled non-UI thread exception: {ExceptionMessage}", ex.Message);
                }
                else
                {
                    logger?.LogCritical("Unhandled non-UI exception (non-Exception object): {ExceptionObject}", e.ExceptionObject);
                }

                MessageBox.Show(
                    "应用程序遇到严重错误并即将关闭。\n错误信息已记录到日志文件。",
                    "严重错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            };

            // 处理 Task 中未观察到的异常
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                var logger = _serviceProvider?.GetRequiredService<ILogger<App>>();
                logger?.LogError(e.Exception, "Unobserved task exception: {ExceptionMessage}", e.Exception.Message);
                e.SetObserved();
            };
        }
    }
}