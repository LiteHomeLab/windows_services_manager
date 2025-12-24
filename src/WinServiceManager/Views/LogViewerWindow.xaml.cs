using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WinServiceManager.Models;
using WinServiceManager.ViewModels;

namespace WinServiceManager.Views
{
    /// <summary>
    /// LogViewerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LogViewerWindow : Window
    {
        private LogViewerViewModel? _viewModel;
        private bool _userIsScrolling = false;

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

        private void LogViewerWindow_Closing(object? sender, CancelEventArgs e)
        {
            // 停止监控并清理资源
            if (_viewModel != null)
            {
                _viewModel.Dispose();
                _viewModel.ScrollToBottomRequested -= OnScrollToBottomRequested;
            }
        }

        private void OnScrollToBottomRequested(object? sender, EventArgs e)
        {
            // 使用 Dispatcher 确保在 UI 线程执行
            Dispatcher.Invoke(() =>
            {
                if (!_userIsScrolling)
                {
                    LogScrollViewer?.ScrollToEnd();
                }
            });
        }

        private void LogScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // 检测用户是否正在手动滚动
            if (e.ExtentHeightChange == 0)
            {
                // 计算是否在底部
                var isAtBottom = e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 10;

                // 如果用户向上滚动（不在底部），标记用户正在滚动
                _userIsScrolling = !isAtBottom;
            }
        }
    }
}
