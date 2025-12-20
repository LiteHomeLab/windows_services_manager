using System;
using System.ComponentModel;
using System.Windows;
using WinServiceManager.Models;
using WinServiceManager.ViewModels;

namespace WinServiceManager.Views
{
    /// <summary>
    /// LogViewerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LogViewerWindow : Window
    {
        private LogViewerViewModel _viewModel;

        public LogViewerWindow()
        {
            InitializeComponent();

            // 订阅窗口关闭事件
            Closing += LogViewerWindow_Closing;
        }

        public LogViewerWindow(ServiceItem service, LogViewerViewModel viewModel) : this()
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            // 订阅滚动到底部事件
            _viewModel.ScrollToBottomRequested += OnScrollToBottomRequested;

            // 设置焦点到日志区域
            Loaded += (s, e) => LogScrollViewer?.Focus();
        }

        private void LogViewerWindow_Closing(object sender, CancelEventArgs e)
        {
            // 停止监控
            if (_viewModel != null)
            {
                _viewModel.StopMonitoring();
                _viewModel.ScrollToBottomRequested -= OnScrollToBottomRequested;
            }
        }

        private void OnScrollToBottomRequested(object sender, EventArgs e)
        {
            // 使用 Dispatcher 确保在 UI 线程执行
            Dispatcher.Invoke(() =>
            {
                LogScrollViewer?.ScrollToEnd();
            });
        }

        private void LogScrollViewer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            // 如果用户手动滚动，可能需要暂停自动滚动
            if (_viewModel != null && e.ExtentHeightChange == 0)
            {
                // 检查是否滚动到底部
                bool isAtBottom = Math.Abs(e.VerticalOffset - (e.ExtentHeight - e.ViewportHeight)) < 10;

                // 如果不是自动滚动且不在底部，可以在这里添加逻辑
                // 例如：_viewModel.IsAutoScroll = isAtBottom;
            }
        }
    }
}