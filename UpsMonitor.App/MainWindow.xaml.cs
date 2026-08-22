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
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmSystemBackdropMainWindow = 2;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowMessageHook);
        ApplyWindows11Backdrop(handle);
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

    private IntPtr WindowMessageHook(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmDeviceChange
            && (wParam.ToInt64() == DbtDeviceArrival || wParam.ToInt64() == DbtDeviceRemoveComplete)
            && DataContext is MainViewModel viewModel)
        {
            viewModel.NotifyDeviceChange();
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
