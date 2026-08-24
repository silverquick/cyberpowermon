using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace UpsMonitor.App;

public partial class MainWindow : Window
{
    private const int WmDeviceChange = 0x0219;
    private const int DbtDeviceArrival = 0x8000;
    private const int DbtDeviceRemoveComplete = 0x8004;

    private const int WmPowerBroadcast = 0x0218;
    private const int PbtApmResumeAutomatic = 0x0012;
    private const int PbtApmPowerStatusChange = 0x000A;

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmSystemBackdropMainWindow = 2;

    private TrayIconManager? _trayManager;

    public MainWindow()
    {
        InitializeComponent();
        IsVisibleChanged += (_, _) => UpdateViewModelVisibility();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowMessageHook);
        ApplyWindows11Backdrop(handle);

        if (DataContext is MainViewModel viewModel)
        {
            _trayManager = new TrayIconManager(this, () => viewModel.EnableNotifications);
            _trayManager.Initialize();

            viewModel.NotificationRequested += (title, message, severity) =>
            {
                _trayManager.ShowNotification(title, message, severity);
            };

            viewModel.TooltipUpdated += tooltip =>
            {
                _trayManager.UpdateTooltip(tooltip);
            };

            UpdateViewModelVisibility();
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized && DataContext is MainViewModel { MinimizeToTray: true })
        {
            Hide();
        }
        UpdateViewModelVisibility();
    }

    private void UpdateViewModelVisibility()
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.IsWindowVisible = IsVisible && WindowState != WindowState.Minimized;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel { CloseToTray: true, IsExiting: false })
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _trayManager?.Dispose();
        base.OnClosing(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F5 && DataContext is MainViewModel viewModel)
        {
            viewModel.NotifyDeviceChange();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private uint _showWindowMessageId;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private IntPtr WindowMessageHook(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_showWindowMessageId == 0)
        {
            _showWindowMessageId = RegisterWindowMessage(App.ShowWindowMessageName);
        }

        if (_showWindowMessageId != 0 && (uint)message == _showWindowMessageId)
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
            SetForegroundWindow(window);
            handled = true;
            return IntPtr.Zero;
        }

        if (message == TrayIconManager.WmTrayCallback)
        {
            _trayManager?.HandleMessage(message, wParam, lParam);
            handled = true;
            return IntPtr.Zero;
        }

        if (message == WmDeviceChange
            && (wParam.ToInt64() == DbtDeviceArrival || wParam.ToInt64() == DbtDeviceRemoveComplete)
            && DataContext is MainViewModel viewModel)
        {
            viewModel.NotifyDeviceChange();
        }

        if (message == WmPowerBroadcast
            && (wParam.ToInt64() == PbtApmResumeAutomatic || wParam.ToInt64() == PbtApmPowerStatusChange)
            && DataContext is MainViewModel vm)
        {
            vm.NotifyDeviceChange();
        }

        return IntPtr.Zero;
    }

    private static void ApplyWindows11Backdrop(IntPtr window)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        var darkMode = ThemeManager.IsDarkMode ? 1 : 0;
        var backdrop = DwmSystemBackdropMainWindow;
        _ = DwmSetWindowAttribute(window, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));
        _ = DwmSetWindowAttribute(window, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr window,
        int attribute,
        ref int value,
        int valueSize);
}
