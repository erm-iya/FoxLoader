using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace ErmiyaDesktop
{
    public sealed partial class DownloadProgressWindow : Window
    {
        private readonly DownloadItemViewModel _item;
        private readonly DispatcherTimer _pollTimer;
        private readonly HttpClient _http = new();
        private bool _completedHandled = false;

        public DownloadProgressWindow(DownloadItemViewModel item)
        {
            this.InitializeComponent();
            _item = item;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            AppWindow.SetIcon("Assets/AppIcon.ico");

            // Apply active theme
            try
            {
                if (this.Content is FrameworkElement root)
                {
                    ThemeManager.ApplyToElement(root);
                }
            }
            catch { }

            // DPI-aware sizing and screen centering
            ApplyDpiAndCenter(600, 460);

            // Populate initial metadata
            HeaderFileNameText.Text = _item.FileName;
            HeaderUrlText.Text = _item.Url;
            SaveToValText.Text = System.IO.Path.Combine(_item.SavePath, _item.FileName);
            ChunksItemsControl.ItemsSource = _item.Chunks;

            UpdateUIFromItem();

            // Background polling for this specific download
            _pollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(750)
            };
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();

            this.Closed += (s, e) =>
            {
                _pollTimer.Stop();
            };
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        private void ApplyDpiAndCenter(int baseWidth, int baseHeight)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            uint dpi = GetDpiForWindow(hwnd);
            if (dpi == 0) dpi = 96;
            double scale = dpi / 96.0;

            int scaledWidth = (int)(baseWidth * scale);
            int scaledHeight = (int)(baseHeight * scale);

            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32(scaledWidth, scaledHeight));

                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var centerPos = new Windows.Graphics.PointInt32(
                        displayArea.WorkArea.X + (displayArea.WorkArea.Width - scaledWidth) / 2,
                        displayArea.WorkArea.Y + (displayArea.WorkArea.Height - scaledHeight) / 2
                    );
                    appWindow.Move(centerPos);
                }
            }
        }

        private void UpdateUIFromItem()
        {
            StatusValText.Text = _item.Status;
            FileSizeValText.Text = _item.TotalSizeText;
            DownloadedValText.Text = $"{_item.TotalSizeText} ({_item.PercentageText})";
            TransferRateValText.Text = _item.SpeedText;
            TimeLeftValText.Text = _item.RemainingText;

            OverallProgressBar.Value = Math.Max(0, Math.Min(100, _item.Progress));
            PercentText.Text = _item.PercentageText;
            Title = $"{_item.PercentageText} - {_item.FileName}";

            bool isCompleted = _item.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase);
            bool isPaused = _item.Status.StartsWith("Paused", StringComparison.OrdinalIgnoreCase);

            if (isCompleted)
            {
                PauseResumeBtn.Visibility = Visibility.Collapsed;
                OpenFileBtn.Visibility = Visibility.Visible;
                HandleCompletionOptions();
            }
            else
            {
                OpenFileBtn.Visibility = Visibility.Collapsed;
                PauseResumeBtn.Visibility = Visibility.Visible;
                PauseResumeBtn.Content = isPaused ? "Resume" : "Pause";
            }
        }

        private async void PollTimer_Tick(object? sender, object e)
        {
            try
            {
                var response = await _http.GetAsync(FoxLoaderConfig.BaseUrl + "/status");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var statuses = JsonSerializer.Deserialize<JobStatus[]>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (statuses == null) return;

                    foreach (var s in statuses)
                    {
                        if (s.Id == _item.Id)
                        {
                            _item.Status = s.StatusText;
                            if (s.TotalSize.HasValue && s.TotalSize.Value > 0)
                            {
                                _item.Progress = ((double)s.Downloaded / s.TotalSize.Value) * 100;
                                _item.PercentageText = $"{_item.Progress:F1}%";
                                _item.TotalSizeText = $"{(s.TotalSize.Value / (1024.0 * 1024.0)):F2} MB";
                            }
                            UpdateUIFromItem();
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        private void HandleCompletionOptions()
        {
            if (_completedHandled) return;
            _completedHandled = true;

            var fullPath = System.IO.Path.Combine(_item.SavePath, _item.FileName);

            if (OptionOpenFileCheck.IsChecked == true && File.Exists(fullPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = fullPath, UseShellExecute = true });
                }
                catch { }
            }

            if (OptionOpenFolderCheck.IsChecked == true)
            {
                try
                {
                    if (File.Exists(fullPath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
                    }
                    else if (Directory.Exists(_item.SavePath))
                    {
                        Process.Start("explorer.exe", $"\"{_item.SavePath}\"");
                    }
                }
                catch { }
            }

            if (OptionShutdownCheck.IsChecked == true)
            {
                Process.Start("shutdown.exe", "/s /t 60 /c \"FoxLoader: Download completed. Shutting down in 60 seconds.\"");
            }
            else if (OptionSleepCheck.IsChecked == true)
            {
                Process.Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
            }

            if (OptionCloseWindowCheck.IsChecked == true)
            {
                this.Close();
            }
        }

        private async void PauseResumeBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool isPaused = _item.Status.StartsWith("Paused", StringComparison.OrdinalIgnoreCase);
                string endpoint = isPaused ? "/resume" : "/pause";
                var payload = new { id = _item.Id };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                await _http.PostAsync(FoxLoaderConfig.BaseUrl + endpoint, content);

                _item.Status = isPaused ? "Connecting..." : "Paused";
                UpdateUIFromItem();
            }
            catch { }
        }

        private async void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var payload = new { id = _item.Id };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                await _http.PostAsync(FoxLoaderConfig.BaseUrl + "/pause", content);
            }
            catch { }
            this.Close();
        }

        private void HideBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OpenFileBtn_Click(object sender, RoutedEventArgs e)
        {
            var fullPath = System.IO.Path.Combine(_item.SavePath, _item.FileName);
            if (File.Exists(fullPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = fullPath, UseShellExecute = true });
                }
                catch { }
            }
        }

        private void OpenFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var fullPath = System.IO.Path.Combine(_item.SavePath, _item.FileName);
            try
            {
                if (File.Exists(fullPath))
                {
                    Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
                }
                else if (Directory.Exists(_item.SavePath))
                {
                    Process.Start("explorer.exe", $"\"{_item.SavePath}\"");
                }
            }
            catch { }
        }

        private void JobSpeedLimiterToggle_Toggled(object sender, RoutedEventArgs e)
        {
            JobSpeedLimitBox.IsEnabled = JobSpeedLimiterToggle.IsOn;
        }

        private async void ApplySpeedLimit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ulong limitBytes = JobSpeedLimiterToggle.IsOn ? (ulong)(JobSpeedLimitBox.Value * 1024.0) : 0;
                var payload = new { id = _item.Id, speed_limit = limitBytes };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                // Inform core if per-job speed limit endpoint is available
                await _http.PostAsync(FoxLoaderConfig.BaseUrl + "/speed_limit", content);
            }
            catch { }
        }
    }
}
