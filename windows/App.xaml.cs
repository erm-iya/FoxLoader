using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;

namespace ErmiyaDesktop;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private System.Diagnostics.Process? _coreProcess;
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();

        this.UnhandledException += (s, e) => {
            try
            {
                var msg = $"[UnhandledException Event]\nMessage: {e.Message}\nException: {e.Exception}\nNative HR: 0x{e.Exception?.HResult:X8}\n";
                File.WriteAllText(@"D:\Project\Ermiya_Download_Manager\crash.log", msg);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), msg);
            }
            catch { }
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) => {
            try
            {
                var msg = e.ExceptionObject?.ToString() ?? "Unknown domain error";
                File.WriteAllText(@"D:\Project\Ermiya_Download_Manager\crash_domain.log", msg);
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash_domain.log"), msg);
            }
            catch { }
        };
    }

    public static MainWindow? MainWindowInstance { get; private set; }
    public static Window? CurrentWindow => MainWindowInstance;

    private static string? FindCoreBinary()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "ermiya-core.exe"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "target", "debug", "ermiya-core.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ReleaseOutput", "ermiya-core.exe")),
            @"D:\Project\Ermiya_Download_Manager\target\debug\ermiya-core.exe",
            @"D:\Project\Ermiya_Download_Manager\ReleaseOutput\ermiya-core.exe"
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }
        return null;
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            var running = System.Diagnostics.Process.GetProcessesByName("ermiya-core");
            if (running.Length == 0)
            {
                var corePath = FindCoreBinary();
                if (corePath != null)
                {
                    var proc = new System.Diagnostics.Process();
                    proc.StartInfo.FileName = corePath;
                    proc.StartInfo.WorkingDirectory = @"D:\Project\Ermiya_Download_Manager";
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.CreateNoWindow = true;
                    proc.Start();
                    _coreProcess = proc;
                }
            }
        }
        catch { }

        MainWindowInstance = new MainWindow();
        _window = MainWindowInstance;
        _window.Closed += (s, e) => {
            if (_coreProcess != null && !_coreProcess.HasExited)
            {
                try { _coreProcess.Kill(); } catch { }
            }
        };
        _window.Activate();
    }
}
