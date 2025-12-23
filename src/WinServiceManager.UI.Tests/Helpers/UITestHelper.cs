using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.UIA3;

namespace WinServiceManager.UI.Tests.Helpers
{
    /// <summary>
    /// UI test helper for WPF application automation
    /// Provides common utilities for UI testing
    /// </summary>
    public class UITestHelper : IDisposable
    {
        private readonly string _applicationPath;
        private Application? _application;
        private Window? _mainWindow;

        public UITestHelper(string applicationPath)
        {
            _applicationPath = applicationPath ?? throw new ArgumentNullException(nameof(applicationPath));
        }

        /// <summary>
        /// Launches the application and returns the main window
        /// </summary>
        public async Task<Window> LaunchApplicationAsync()
        {
            // Verify the application exists
            if (!File.Exists(_applicationPath))
            {
                throw new FileNotFoundException($"Application not found: {_applicationPath}");
            }

            // Create FlaUI application
            _application = Application.Launch(_applicationPath);

            // Get main window with timeout
            using var automation = new UIA3Automation();
            _mainWindow = _application.GetMainWindow(automation, new TimeSpan(0, 0, 10));

            if (_mainWindow == null)
            {
                throw new Exception("Failed to find main window");
            }

            // Wait for window to be ready
            await Task.Delay(1000);

            return _mainWindow;
        }

        /// <summary>
        /// Gets the application window
        /// </summary>
        public Window? MainWindow => _mainWindow;

        /// <summary>
        /// Gets the automation object
        /// </summary>
        public UIA3Automation GetAutomation()
        {
            return new UIA3Automation();
        }

        /// <summary>
        /// Finds a button by name or automation ID
        /// </summary>
        public AutomationElement FindButton(Window window, string name)
        {
            return window.FindFirstDescendant(cf => cf
                .ByControlType(ControlType.Button)
                .And(cf.ByName(name, PropertyConditionFlags.MatchSubstring)))
                ?? throw new Exception($"Button not found: {name}");
        }

        /// <summary>
        /// Finds a text box by name or automation ID
        /// </summary>
        public AutomationElement FindTextBox(Window window, string name)
        {
            return window.FindFirstDescendant(cf => cf
                .ByControlType(ControlType.Edit)
                .And(cf.ByName(name, PropertyConditionFlags.MatchSubstring)))
                ?? throw new Exception($"TextBox not found: {name}");
        }

        /// <summary>
        /// Finds a list item by name
        /// </summary>
        public AutomationElement FindListItem(Window window, string name)
        {
            return window.FindFirstDescendant(cf => cf
                .ByControlType(ControlType.DataItem)
                .And(cf.ByName(name, PropertyConditionFlags.MatchSubstring)))
                ?? throw new Exception($"ListItem not found: {name}");
        }

        /// <summary>
        /// Finds a menu item by name
        /// </summary>
        public AutomationElement FindMenuItem(Window window, string name)
        {
            return window.FindFirstDescendant(cf => cf
                .ByControlType(ControlType.MenuItem)
                .And(cf.ByName(name, PropertyConditionFlags.MatchSubstring)))
                ?? throw new Exception($"MenuItem not found: {name}");
        }

        /// <summary>
        /// Clicks an element
        /// </summary>
        public void Click(AutomationElement element)
        {
            element.Click();
        }

        /// <summary>
        /// Enters text into a text box
        /// </summary>
        public void EnterText(AutomationElement textBox, string text)
        {
            textBox.Focus();
            Keyboard.Type(text);
        }

        /// <summary>
        /// Takes a screenshot of the current window
        /// </summary>
        public void CaptureScreenshot(string outputPath)
        {
            if (_mainWindow != null)
            {
                // Use FlaUI's built-in capture method
                _mainWindow.CaptureToFile(outputPath);
            }
        }

        /// <summary>
        /// Waits for an element to appear
        /// </summary>
        public async Task<AutomationElement?> WaitForElementAsync(
            Window window,
            Func<Window, AutomationElement?> finder,
            TimeSpan? timeout = null)
        {
            var timeoutMs = (int)(timeout?.TotalMilliseconds ?? 5000);
            var startTime = DateTime.UtcNow;

            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                var element = finder(window);
                if (element != null)
                {
                    return element;
                }

                await Task.Delay(100);
            }

            return null;
        }

        /// <summary>
        /// Gets the window title
        /// </summary>
        public string GetWindowTitle()
        {
            return _mainWindow?.Name ?? string.Empty;
        }

        /// <summary>
        /// Checks if window is still open
        /// </summary>
        public bool IsWindowOpen()
        {
            try
            {
                return _mainWindow?.IsAvailable == true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            try
            {
                _mainWindow?.Close();
            }
            catch
            {
                // Ignore close errors
            }

            try
            {
                _application?.Close();
                _application?.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
    }

    /// <summary>
    /// Simple screenshot capture helper
    /// </summary>
    public static class ScreenshotHelper
    {
        /// <summary>
        /// Takes a screenshot of the specified window
        /// </summary>
        public static void CaptureWindow(Window window, string outputPath)
        {
            try
            {
                window.CaptureToFile(outputPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to capture screenshot: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Takes a screenshot of the entire screen
        /// </summary>
        public static void CaptureScreen(string outputPath)
        {
            try
            {
                using var automation = new UIA3Automation();
                var desktop = automation.GetDesktop();
                desktop.CaptureToFile(outputPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to capture screen: {ex.Message}", ex);
            }
        }
    }
}
