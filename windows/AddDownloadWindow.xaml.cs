using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ErmiyaDesktop
{
    public sealed partial class AddDownloadWindow : Window
    {
        [DllImport("User32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("User32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("User32.dll", SetLastError = true)]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("User32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private readonly string _defaultBasePath;
        private bool _isManualPathChange = false;

        private string? _duplicateFilePath;
        private string? _previousVersionFilePath;
        private MultiPartInfo? _multiPartInfo;
        private SeriesInfo? _seriesInfo;

        public AddDownloadWindow(string url, string fileName)
        {
            this.InitializeComponent();

            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            // Extend content into title bar to remove the legacy black title bar
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(CustomHeaderGrid);

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = appWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(40, 255, 255, 255);
                titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(60, 255, 255, 255);
            }

            // Calculate DPI-aware window dimensions
            uint dpi = GetDpiForWindow(hWnd);
            float scale = dpi > 0 ? (dpi / 96.0f) : 1.0f;
            int windowWidth = (int)(680 * scale);
            int windowHeight = (int)(640 * scale);
            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = windowWidth, Height = windowHeight });

            // Center window on screen
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                int centeredX = (displayArea.WorkArea.Width - windowWidth) / 2;
                int centeredY = (displayArea.WorkArea.Height - windowHeight) / 2;
                appWindow.Move(new Windows.Graphics.PointInt32 { X = Math.Max(0, centeredX), Y = Math.Max(0, centeredY) });
            }

            // Set window icon with verified path
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }

            try
            {
                string logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
                if (File.Exists(logoPath) && HeaderLogoImage != null)
                {
                    HeaderLogoImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(logoPath));
                }
            }
            catch { }

            // Pin on top by default so it stays above the browser window
            SetAlwaysOnTop(true);

            // Inherit theme from ThemeManager
            ThemeManager.ApplyToElement(this.Content as FrameworkElement);

            _defaultBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "FoxLoader");

            UrlTextBox.Text = url ?? string.Empty;

            if (string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    var uri = new Uri(url);
                    fileName = Path.GetFileName(uri.LocalPath);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        fileName = Uri.UnescapeDataString(fileName);
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "downloaded_file";
            }

            FileNameTextBox.Text = fileName;
            AutoSelectCategory(fileName);

            if (!string.IsNullOrWhiteSpace(url))
            {
                CheckFileDetailsAsync(url);
                RunSmartIntelligenceAsync(url, fileName);
            }
            else
            {
                if (DetectProgressRing != null)
                {
                    DetectProgressRing.IsActive = false;
                    DetectProgressRing.Visibility = Visibility.Collapsed;
                }
                if (FileStatusBadge != null)
                {
                    FileStatusBadge.Text = "Please enter download address";
                }
            }
        }

        private async void RunSmartIntelligenceAsync(string url, string fileName)
        {
            try
            {
                // Fetch existing downloads from core to check duplicates and previous versions
                List<JobStatus>? existingJobs = null;
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                    var res = await client.GetAsync($"{FoxLoaderConfig.BaseUrl}/status");
                    if (res.IsSuccessStatusCode)
                    {
                        var json = await res.Content.ReadAsStringAsync();
                        existingJobs = JsonSerializer.Deserialize<List<JobStatus>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                }
                catch { }

                var analysis = await SmartDownloadIntelligence.AnalyzeAsync(url, fileName, existingJobs);

                // 1. Duplicate Detection Card
                if (analysis.Duplicate.IsDuplicate)
                {
                    _duplicateFilePath = analysis.Duplicate.ExistingFilePath;
                    DuplicateDescText.Text = $"You already downloaded this file on {analysis.Duplicate.DownloadedDate} in:\n{analysis.Duplicate.ExistingFilePath}";
                    DuplicateCard.Visibility = Visibility.Visible;
                }

                // 2. Previous App Version Card
                if (analysis.PreviousVersion.HasPreviousVersion)
                {
                    _previousVersionFilePath = analysis.PreviousVersion.PreviousFilePath;
                    PrevVerTitleText.Text = $"Previous Version Found ({analysis.PreviousVersion.AppName})";
                    PrevVerDescText.Text = $"Found older version ({analysis.PreviousVersion.PreviousVersion}) from {analysis.PreviousVersion.DownloadedDate} in:\n{analysis.PreviousVersion.PreviousFilePath}\nThis download will be {analysis.PreviousVersion.CurrentVersion}.";
                    PreviousVersionCard.Visibility = Visibility.Visible;
                }

                // 3. Multi-Part Archive Card
                if (analysis.MultiPart.IsMultiPart && analysis.MultiPart.AvailableParts.Count > 0)
                {
                    _multiPartInfo = analysis.MultiPart;
                    MultiPartTitleText.Text = $"Multi-Part Archive ({analysis.MultiPart.AvailableParts.Count} Parts Found)";
                    MultiPartDescText.Text = $"Detected part {analysis.MultiPart.CurrentPart} • Total parts available on server: {analysis.MultiPart.AvailableParts.Count}";
                    PartsListView.ItemsSource = analysis.MultiPart.AvailableParts;
                    MultiPartCard.Visibility = Visibility.Visible;
                }

                // 4. TV Series Season & Episode Card
                if (analysis.Series.IsSeries)
                {
                    _seriesInfo = analysis.Series;
                    SeriesTitleText.Text = $"TV Series: {analysis.Series.SeriesTitle} - Season {analysis.Series.Season:D2}, Episode {analysis.Series.Episode:D2}";
                    SeriesSubtitleText.Text = $"Quality: {analysis.Series.Quality} • Codec: {analysis.Series.Codec}";
                    
                    // Auto-route to specific series season directory
                    CategoryComboBox.SelectedIndex = 5; // Video
                    SavePathTextBox.Text = analysis.Series.SuggestedFolder;

                    SeriesStartEpBox.Value = 1;
                    SeriesEndEpBox.Value = Math.Max(10, analysis.Series.Episode);
                    SeriesCard.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Intelligence error: {ex}");
            }
        }

        #region Smart Intelligence Handlers

        private void OpenExistingFileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_duplicateFilePath) && File.Exists(_duplicateFilePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(_duplicateFilePath) { UseShellExecute = true });
                    this.Close();
                }
                catch { }
            }
        }

        private void OpenFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_duplicateFilePath))
            {
                try
                {
                    if (File.Exists(_duplicateFilePath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{_duplicateFilePath}\"");
                    }
                    else
                    {
                        var dir = Path.GetDirectoryName(_duplicateFilePath);
                        if (Directory.Exists(dir))
                        {
                            Process.Start("explorer.exe", $"\"{dir}\"");
                        }
                    }
                }
                catch { }
            }
        }

        private void DismissDuplicateBtn_Click(object sender, RoutedEventArgs e)
        {
            DuplicateCard.Visibility = Visibility.Collapsed;
        }

        private void OpenOldVerFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_previousVersionFilePath))
            {
                try
                {
                    if (File.Exists(_previousVersionFilePath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{_previousVersionFilePath}\"");
                    }
                    else
                    {
                        var dir = Path.GetDirectoryName(_previousVersionFilePath);
                        if (Directory.Exists(dir))
                        {
                            Process.Start("explorer.exe", $"\"{dir}\"");
                        }
                    }
                }
                catch { }
            }
        }

        private void DismissPrevVerBtn_Click(object sender, RoutedEventArgs e)
        {
            PreviousVersionCard.Visibility = Visibility.Collapsed;
        }

        private async void DownloadAllPartsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_multiPartInfo != null && _multiPartInfo.AvailableParts.Count > 0)
            {
                await SubmitBatchPartsAsync(_multiPartInfo.AvailableParts);
            }
        }

        private async void DownloadSelectedPartsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_multiPartInfo != null && _multiPartInfo.AvailableParts.Count > 0)
            {
                var selected = _multiPartInfo.AvailableParts.Where(p => p.IsSelected).ToList();
                if (selected.Count > 0)
                {
                    await SubmitBatchPartsAsync(selected);
                }
            }
        }

        private async Task SubmitBatchPartsAsync(List<PartItem> parts)
        {
            try
            {
                using var client = new HttpClient();
                string savePath = SavePathTextBox?.Text ?? _defaultBasePath;
                string queue = QueueComboBox?.SelectedItem is ComboBoxItem cbi ? cbi.Content?.ToString() ?? "Main Queue" : "Main Queue";

                var items = parts.Select(p => new
                {
                    url = p.Url,
                    file_name = p.FileName,
                    save_path = savePath,
                    start_immediately = StartNowCheckBox?.IsChecked ?? true,
                    queue = queue
                }).ToList();

                var content = new StringContent(JsonSerializer.Serialize(new { items }), Encoding.UTF8, "application/json");
                await client.PostAsync($"{FoxLoaderConfig.BaseUrl}/batch_add", content);
            }
            catch { }

            this.Close();
        }

        private async void DownloadSeriesEpisodesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_seriesInfo != null)
            {
                int start = (int)SeriesStartEpBox.Value;
                int end = (int)SeriesEndEpBox.Value;
                var episodes = SmartDownloadIntelligence.GenerateEpisodeBatch(_seriesInfo, start, end);

                if (episodes.Count > 0)
                {
                    try
                    {
                        using var client = new HttpClient();
                        string savePath = SavePathTextBox?.Text ?? _seriesInfo.SuggestedFolder;
                        string queue = QueueComboBox?.SelectedItem is ComboBoxItem cbi ? cbi.Content?.ToString() ?? "Main Queue" : "Main Queue";

                        var items = episodes.Select(ep => new
                        {
                            url = ep.Url,
                            file_name = ep.FileName,
                            save_path = savePath,
                            start_immediately = StartNowCheckBox?.IsChecked ?? true,
                            queue = queue
                        }).ToList();

                        var content = new StringContent(JsonSerializer.Serialize(new { items }), Encoding.UTF8, "application/json");
                        await client.PostAsync($"{FoxLoaderConfig.BaseUrl}/batch_add", content);
                    }
                    catch { }

                    this.Close();
                }
            }
        }

        #endregion

        private void SetAlwaysOnTop(bool onTop)
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = onTop;
            }

            // Enforce via Win32 API to pin window above Chrome / Edge
            SetWindowPos(hWnd, onTop ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

            if (onTop)
            {
                BringWindowToTop(hWnd);
                SetForegroundWindow(hWnd);
            }
        }

        private void PinOnTopButton_Click(object sender, RoutedEventArgs e)
        {
            bool isPinned = PinOnTopButton.IsChecked ?? false;
            SetAlwaysOnTop(isPinned);
        }

        private void AutoSelectCategory(string fileName)
        {
            if (CategoryComboBox == null) return;

            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            int selectedIndex = 0; // General

            if (ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".tar" || ext == ".gz" || ext == ".iso")
                selectedIndex = 1; // Compressed
            else if (ext == ".pdf" || ext == ".doc" || ext == ".docx" || ext == ".txt" || ext == ".epub" || ext == ".xlsx")
                selectedIndex = 2; // Documents
            else if (ext == ".mp3" || ext == ".wav" || ext == ".flac" || ext == ".m4a" || ext == ".aac")
                selectedIndex = 3; // Music
            else if (ext == ".exe" || ext == ".msi" || ext == ".apk" || ext == ".deb")
                selectedIndex = 4; // Programs
            else if (ext == ".mp4" || ext == ".mkv" || ext == ".avi" || ext == ".mov" || ext == ".webm")
                selectedIndex = 5; // Video

            CategoryComboBox.SelectedIndex = selectedIndex;
            UpdateSavePathFromCategory();
        }

        private void UpdateSavePathFromCategory()
        {
            if (_isManualPathChange || SavePathTextBox == null || CategoryComboBox == null) return;

            if (CategoryComboBox.SelectedItem is ComboBoxItem item)
            {
                string category = item.Content?.ToString() ?? "General";
                if (category == "General")
                {
                    SavePathTextBox.Text = _defaultBasePath;
                }
                else
                {
                    SavePathTextBox.Text = Path.Combine(_defaultBasePath, category);
                }
            }
            else
            {
                SavePathTextBox.Text = _defaultBasePath;
            }
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSavePathFromCategory();
        }

        private void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (UrlTextBox == null || FileNameTextBox == null) return;

            if (string.IsNullOrWhiteSpace(FileNameTextBox.Text) || FileNameTextBox.Text == "downloaded_file")
            {
                try
                {
                    if (Uri.TryCreate(UrlTextBox.Text, UriKind.Absolute, out var uri))
                    {
                        var fn = Path.GetFileName(uri.LocalPath);
                        if (!string.IsNullOrWhiteSpace(fn))
                        {
                            FileNameTextBox.Text = Uri.UnescapeDataString(fn);
                            AutoSelectCategory(fn);
                        }
                    }
                }
                catch { }
            }
        }

        private async void CheckFileDetailsAsync(string url)
        {
            try
            {
                using var handler = new HttpClientHandler { AllowAutoRedirect = true };
                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) FoxLoader/1.0");

                using var req = new HttpRequestMessage(HttpMethod.Head, url);
                var resp = await client.SendAsync(req);

                if (DetectProgressRing != null)
                {
                    DetectProgressRing.IsActive = false;
                    DetectProgressRing.Visibility = Visibility.Collapsed;
                }

                if (resp.IsSuccessStatusCode)
                {
                    if (resp.Content.Headers.ContentLength.HasValue)
                    {
                        long bytes = resp.Content.Headers.ContentLength.Value;
                        string sizeStr = FormatBytes(bytes);
                        if (FileSizeText != null) FileSizeText.Text = sizeStr;
                        if (FileStatusBadge != null) FileStatusBadge.Text = $"Ready to download ({sizeStr})";
                    }
                    else
                    {
                        if (FileSizeText != null) FileSizeText.Text = "Unknown (Dynamic stream)";
                        if (FileStatusBadge != null) FileStatusBadge.Text = "Ready to download";
                    }

                    bool canResume = resp.Headers.AcceptRanges.Contains("bytes");
                    if (ResumeSupportText != null)
                    {
                        ResumeSupportText.Text = canResume ? "Yes (Multi-threaded)" : "No (Single stream)";
                    }

                    if (resp.Content.Headers.ContentDisposition?.FileName != null)
                    {
                        var serverFileName = resp.Content.Headers.ContentDisposition.FileName.Trim('\"', '\'');
                        if (!string.IsNullOrWhiteSpace(serverFileName))
                        {
                            if (FileNameTextBox != null) FileNameTextBox.Text = serverFileName;
                            AutoSelectCategory(serverFileName);
                        }
                    }
                }
                else
                {
                    if (FileSizeText != null) FileSizeText.Text = "Unavailable";
                    if (ResumeSupportText != null) ResumeSupportText.Text = "Unknown";
                    if (FileStatusBadge != null) FileStatusBadge.Text = $"Server status: {(int)resp.StatusCode}";
                }
            }
            catch (Exception)
            {
                if (DetectProgressRing != null)
                {
                    DetectProgressRing.IsActive = false;
                    DetectProgressRing.Visibility = Visibility.Collapsed;
                }
                if (FileSizeText != null) FileSizeText.Text = "Unavailable";
                if (ResumeSupportText != null) ResumeSupportText.Text = "Unknown";
                if (FileStatusBadge != null) FileStatusBadge.Text = "Ready (Preview skipped)";
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F2} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
            folderPicker.FileTypeFilter.Add("*");

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                _isManualPathChange = true;
                if (SavePathTextBox != null)
                {
                    SavePathTextBox.Text = folder.Path;
                }
            }
        }

        private async void DownloadLaterButton_Click(object sender, RoutedEventArgs e)
        {
            await SubmitDownloadAsync(false);
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            await SubmitDownloadAsync(StartNowCheckBox?.IsChecked ?? true);
        }

        private async Task SubmitDownloadAsync(bool startImmediately)
        {
            try
            {
                using var client = new HttpClient();
                var payload = new
                {
                    url = UrlTextBox?.Text ?? string.Empty,
                    file_name = FileNameTextBox?.Text ?? string.Empty,
                    save_path = SavePathTextBox?.Text ?? _defaultBasePath,
                    start_immediately = startImmediately,
                    queue = QueueComboBox?.SelectedItem is ComboBoxItem cbi ? cbi.Content?.ToString() ?? "Main Queue" : "Main Queue"
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                await client.PostAsync($"{FoxLoaderConfig.BaseUrl}/add", content);
            }
            catch { }

            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
