using System.Windows;
using System.Windows.Input;

namespace UpsMonitor.App;

public partial class MiniMonitorWindow : Window
{
    private readonly Window _mainWindow;

    public MiniMonitorWindow(Window mainWindow, object dataContext)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        DataContext = dataContext;

        // 画面右下に初期配置
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OpenMainWindow();
    }

    private void OpenMainWindow_Click(object sender, RoutedEventArgs e)
    {
        OpenMainWindow();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OpenMainWindow()
    {
        Hide();
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }
}
