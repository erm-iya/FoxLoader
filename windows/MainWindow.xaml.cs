using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace ErmiyaDesktop;

/// <summary>
/// The application window. This hosts a Frame that displays pages.
/// Handles system tray persistence, taskbar icon binding, and window lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int WM_SETICON = 0x0080;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;
    private const int SW_HIDE = 0;

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    private readonly IntPtr _hwnd;
    private TrayManager? _trayManager;
    private bool _isExplicitExit = false;

    public MainWindow()
    {
        InitializeComponent();

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Bind explicit AppUserModelID so Windows Taskbar associates process with FoxLoader
        try
        {
            SetCurrentProcessExplicitAppUserModelID("FoxLoader.App");
        }
        catch { }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        SetupIcons();
        SetupTray();

        AppWindow.Closing += AppWindow_Closing;

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));

        // Apply saved theme preference once content tree is active
        try
        {
            ThemeManager.Initialize();
        }
        catch { }
    }

    private void SetupIcons()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (!File.Exists(iconPath))
        {
            iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.ico");
        }

        if (File.Exists(iconPath))
        {
            try
            {
                AppWindow.SetIcon(iconPath);
            }
            catch { }

            try
            {
                IntPtr hIconBig = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
                IntPtr hIconSmall = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);

                if (hIconBig != IntPtr.Zero)
                {
                    SendMessage(_hwnd, WM_SETICON, (IntPtr)ICON_BIG, hIconBig);
                }
                if (hIconSmall != IntPtr.Zero)
                {
                    SendMessage(_hwnd, WM_SETICON, (IntPtr)ICON_SMALL, hIconSmall);
                }
            }
            catch { }
        }
    }

    private void SetupTray()
    {
        try
        {
            _trayManager = new TrayManager(
                _hwnd,
                onOpen: RestoreAndActivate,
                onAdd: OpenAddDownloadDialog,
                onSettings: OpenSettingsPage,
                onToggleSpeedLimiter: ToggleSpeedLimiter,
                onExit: ExitApplication
            );
        }
        catch { }
    }

    public void OpenAddDownloadDialog()
    {
        RestoreAndActivate();
        if (RootFrame.Content is MainPage mainPage)
        {
            mainPage.AddDownloadBtn_Click(this, new RoutedEventArgs());
        }
    }

    public void OpenSettingsPage()
    {
        RestoreAndActivate();
        if (RootFrame.Content is MainPage mainPage)
        {
            mainPage.OpenSettings();
        }
    }

    public void ToggleSpeedLimiter()
    {
        try
        {
            var settings = SettingsStorage.LoadFromDisk();
            if (settings.SpeedLimiter == null) settings.SpeedLimiter = new();
            settings.SpeedLimiter.Enabled = !settings.SpeedLimiter.Enabled;
            SettingsStorage.SaveToDisk(settings);
        }
        catch { }
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExplicitExit)
        {
            return;
        }

        bool closeToTray = true;
        try
        {
            var settings = SettingsStorage.LoadFromDisk();
            closeToTray = settings?.Ui?.CloseToTray ?? true;
        }
        catch { }

        if (closeToTray)
        {
            args.Cancel = true;
            AppWindow.Hide();
        }
        else
        {
            ExitApplication();
        }
    }

    public void RestoreAndActivate()
    {
        AppWindow.Show();
        ShowWindow(_hwnd, SW_RESTORE);
        BringWindowToTop(_hwnd);
        SetForegroundWindow(_hwnd);
        this.Activate();
    }

    public void ExitApplication()
    {
        _isExplicitExit = true;

        if (_trayManager != null)
        {
            _trayManager.Dispose();
            _trayManager = null;
        }

        if (Application.Current is App app)
        {
            app.StopCoreProcess();
        }

        try
        {
            AppWindow.Destroy();
        }
        catch
        {
            this.Close();
        }
    }
}
