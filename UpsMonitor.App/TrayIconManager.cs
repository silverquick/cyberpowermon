using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using UpsMonitor.Core;

namespace UpsMonitor.App;

internal sealed class TrayIconManager : IDisposable
{
    private const int WmUser = 0x0400;
    public const int WmTrayCallback = WmUser + 101;

    private const int NimAdd = 0x00000000;
    private const int NimModify = 0x00000001;
    private const int NimDelete = 0x00000002;
    private const int NimSetVersion = 0x00000004;

    private const int NifMessage = 0x00000001;
    private const int NifIcon = 0x00000002;
    private const int NifTip = 0x00000004;
    private const int NifInfo = 0x00000010;

    private const int NiifNone = 0x00000000;
    private const int NiifInfo = 0x00000001;
    private const int NiifWarning = 0x00000002;
    private const int NiifError = 0x00000003;

    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmRButtonUp = 0x0205;

    private const uint NotifyIconId = 1001;

    private readonly Window _mainWindow;
    private readonly Func<bool> _notificationsEnabled;
    private IntPtr _hwnd;
    private IntPtr _hIcon;
    private bool _isAdded;
    private bool _disposed;
    private ContextMenu? _contextMenu;

    public TrayIconManager(Window mainWindow, Func<bool> notificationsEnabled)
    {
        _mainWindow = mainWindow;
        _notificationsEnabled = notificationsEnabled;
    }

    public void Initialize()
    {
        if (_isAdded)
        {
            return;
        }

        _hwnd = new WindowInteropHelper(_mainWindow).Handle;
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        // Extract default application icon or system icon
        var processModule = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processModule))
        {
            _hIcon = ExtractIcon(IntPtr.Zero, processModule, 0);
        }

        if (_hIcon == IntPtr.Zero)
        {
            _hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION
        }

        CreateContextMenu();

        var nid = CreateNotifyIconData(NifMessage | NifIcon | NifTip);
        nid.szTip = "PowerGuard / UPS Monitor";

        _isAdded = Shell_NotifyIcon(NimAdd, ref nid);
    }

    public void UpdateTooltip(string tooltip)
    {
        if (!_isAdded || _disposed)
        {
            return;
        }

        var nid = CreateNotifyIconData(NifTip);
        nid.szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip;
        Shell_NotifyIcon(NimModify, ref nid);
    }

    public void ShowNotification(string title, string message, UpsEventSeverity severity = UpsEventSeverity.Information)
    {
        if (!_isAdded || _disposed || !_notificationsEnabled())
        {
            return;
        }

        var nid = CreateNotifyIconData(NifInfo);
        nid.szInfoTitle = title.Length > 63 ? title[..63] : title;
        nid.szInfo = message.Length > 255 ? message[..255] : message;
        nid.dwInfoFlags = severity switch
        {
            UpsEventSeverity.Critical => NiifError,
            UpsEventSeverity.Warning => NiifWarning,
            _ => NiifInfo,
        };

        Shell_NotifyIcon(NimModify, ref nid);
    }

    public void HandleMessage(int message, IntPtr wParam, IntPtr lParam)
    {
        if (message != WmTrayCallback)
        {
            return;
        }

        var eventMsg = lParam.ToInt32();
        switch (eventMsg)
        {
            case WmLButtonDblClk:
                ToggleMainWindow();
                break;

            case WmRButtonUp:
                ShowContextMenu();
                break;
        }
    }

    private void ToggleMainWindow()
    {
        if (_mainWindow.IsVisible && _mainWindow.WindowState != WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Minimized;
        }
        else
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }
    }

    private void ShowContextMenu()
    {
        if (_contextMenu is null)
        {
            CreateContextMenu();
        }

        SetForegroundWindow(_hwnd);
        _contextMenu!.IsOpen = true;
    }

    private void CreateContextMenu()
    {
        _contextMenu = new ContextMenu();

        var showItem = new MenuItem { Header = LocalizationManager.IsJapanese ? "開く" : "Open" };
        showItem.Click += (_, _) =>
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        };

        var hideItem = new MenuItem { Header = LocalizationManager.IsJapanese ? "最小化 / 非表示" : "Hide to Tray" };
        hideItem.Click += (_, _) =>
        {
            _mainWindow.Hide();
        };

        var exitItem = new MenuItem { Header = LocalizationManager.IsJapanese ? "終了" : "Exit" };
        exitItem.Click += (_, _) =>
        {
            if (_mainWindow.DataContext is MainViewModel vm)
            {
                vm.IsExiting = true;
            }

            Dispose();
            Application.Current.Shutdown();
        };

        _contextMenu.Items.Add(showItem);
        _contextMenu.Items.Add(hideItem);
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(exitItem);
    }

    private NotifyIconData CreateNotifyIconData(uint flags)
    {
        return new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _hwnd,
            uID = NotifyIconId,
            uFlags = flags,
            uCallbackMessage = WmTrayCallback,
            hIcon = _hIcon,
            szTip = string.Empty,
            szInfo = string.Empty,
            szInfoTitle = string.Empty,
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_isAdded)
        {
            var nid = CreateNotifyIconData(0);
            Shell_NotifyIcon(NimDelete, ref nid);
            _isAdded = false;
        }

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
