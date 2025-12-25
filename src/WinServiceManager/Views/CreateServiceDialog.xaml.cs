using System;
using System.Windows;
using System.Windows.Input;
using WinServiceManager.ViewModels;

namespace WinServiceManager.Views
{
    /// <summary>
    /// CreateServiceDialog.xaml 的交互逻辑
    /// </summary>
    public partial class CreateServiceDialog : Window
    {
        public CreateServiceDialog()
        {
            InitializeComponent();
        }

        public CreateServiceDialog(CreateServiceViewModel viewModel) : this()
        {
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            // 订阅关闭事件
            viewModel.RequestClose += () =>
            {
                // 使用 Dispatcher.Invoke 确保在 UI 线程执行
                Dispatcher.Invoke(() =>
                {
                    DialogResult = true;
                    Close();
                });
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 设置焦点
            MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
    }
}