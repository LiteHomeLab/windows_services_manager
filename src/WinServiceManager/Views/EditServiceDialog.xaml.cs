using System;
using System.Windows;
using System.Windows.Input;
using WinServiceManager.ViewModels;

namespace WinServiceManager.Views
{
    /// <summary>
    /// EditServiceDialog.xaml 的交互逻辑
    /// </summary>
    public partial class EditServiceDialog : Window
    {
        public EditServiceDialog()
        {
            InitializeComponent();
        }

        public EditServiceDialog(EditServiceViewModel viewModel) : this()
        {
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            // Subscribe to close event
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
            // Set focus
            MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }
    }
}
