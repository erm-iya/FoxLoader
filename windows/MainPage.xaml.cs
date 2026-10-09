using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using Windows.Storage.Pickers;
using ErmiyaDesktop.Localization;

namespace ErmiyaDesktop
{
    public sealed partial class MainPage : Page
    {
        public ObservableCollection<DownloadItemViewModel> Downloads { get; } = new();
        public ObservableCollection<BatchLinkItemViewModel> BatchAllLinks { get; } = new();
        private List<BatchLinkItemViewModel> _allDiscoveredBatchLinks = new();
        private DispatcherTimer? _pollTimer;
        private HttpClient _pollClient = new();
        private bool _isDialogShowing = false;
        private string _currentFilter = "all";
        private List<DownloadItemViewModel> _allDownloads = new();
        private List<DownloadItemViewModel> _itemsToDelete = new();
        private string _currentSortCol = "FileName";
        private bool _currentSortAscending = true;

        public MainPage()
        {
            try
            {
                this.InitializeComponent();
                DownloadsList.ItemsSource = Downloads;
                BatchAllLinksListView.ItemsSource = BatchAllLinks;
                NavView.SelectedItem = NavView.MenuItems[0];

                LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLanguage();
                ApplyLanguage();
                UpdateSortGlyphs();

                _pollTimer = new DispatcherTimer();
                _pollTimer.Interval = TimeSpan.FromSeconds(1);
                _pollTimer.Tick += PollTimer_Tick;
                _pollTimer.Start();
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText(@"D:\Project\Ermiya_Download_Manager\crash.log", "MainPage ctor: " + ex.ToString());
            }
        }

        private async void PollTimer_Tick(object? sender, object e)
        {
            if (!_isDialogShowing) {
                try {
                    // Check for single interactive request
                    var irResponse = await _pollClient.GetAsync(FoxLoaderConfig.BaseUrl + "/pop_interactive");
                    if (irResponse.IsSuccessStatusCode) {
                        var irJson = await irResponse.Content.ReadAsStringAsync();
                        if (irJson != "null") {
                            var req = JsonSerializer.Deserialize<InteractiveReq>(irJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (req != null && !string.IsNullOrEmpty(req.Url)) {
                                _isDialogShowing = true;
                                try {
                                    var win = new AddDownloadWindow(req.Url, req.FileName);
                                    win.Closed += (s, ev) => { _isDialogShowing = false; };
                                    win.Activate();
                                } catch (Exception ex) {
                                    _isDialogShowing = false;
                                    try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log"), $"[AddDownloadWindow Error]: {ex}\n"); } catch {}
                                }
                            }
                        }
                    }

                    // Check for batch interactive requests from browser extension
                    var birResponse = await _pollClient.GetAsync(FoxLoaderConfig.BaseUrl + "/pop_batch_interactive");
                    if (birResponse.IsSuccessStatusCode) {
                        var birJson = await birResponse.Content.ReadAsStringAsync();
                        if (birJson != "null") {
                            var batchReq = JsonSerializer.Deserialize<BatchInteractiveIncoming>(birJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (batchReq != null && batchReq.Links?.Count > 0) {
                                _isDialogShowing = true;
                                _allDiscoveredBatchLinks.Clear();
                                foreach (var l in batchReq.Links) {
                                    string ext = System.IO.Path.GetExtension(l.FileName).ToLowerInvariant();
                                    bool isHtml = ext == ".html" || ext == ".htm" || ext == ".php" || ext == ".asp" || ext == ".aspx" || string.IsNullOrEmpty(ext);
                                    bool isImg = ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".webp";
                                    _allDiscoveredBatchLinks.Add(new BatchLinkItemViewModel {
                                        IsSelected = !isHtml,
                                        FileName = l.FileName,
                                        FileType = !string.IsNullOrEmpty(l.FileType) ? l.FileType : "Generic",
                                        Url = l.Url,
                                        LinkText = l.LinkText,
                                        IsHtml = isHtml,
                                        IsImage = isImg
                                    });
                                }
                                ApplyBatchLinksFilter();
                                BatchAllLinksDialog.XamlRoot = this.XamlRoot;
                                SaveToOneDirTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads\FoxLoader";
                                var dialogTask = BatchAllLinksDialog.ShowAsync();
                                _ = dialogTask.AsTask().ContinueWith(_ => { _isDialogShowing = false; });
                            }
                        }
                    }
                } catch {
                    _isDialogShowing = false;
                }
            }

            try 
            {
                var response = await _pollClient.GetAsync(FoxLoaderConfig.BaseUrl + "/status");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var statuses = JsonSerializer.Deserialize<JobStatus[]>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (statuses == null) return;

                    foreach (var status in statuses)
                    {
                        var vm = _allDownloads.FirstOrDefault(d => d.Id == status.Id);
                        if (vm == null)
                        {
                            vm = new DownloadItemViewModel { Id = status.Id, FileName = status.FileName, Url = status.Url, SavePath = status.SavePath };
                            _allDownloads.Add(vm);
                        }

                        // Calculate speed with rolling Exponential Moving Average (EMA)
                        long currentDownloaded = (long)status.Downloaded;
                        long downloadedDelta = Math.Max(0, currentDownloaded - vm.LastDownloadedBytes);
                        vm.LastDownloadedBytes = currentDownloaded;

                        bool isPaused = status.StatusText.StartsWith("Paused", StringComparison.OrdinalIgnoreCase);
                        bool isCompleted = status.StatusText.Equals("Completed", StringComparison.OrdinalIgnoreCase);
                        bool isFailed = status.StatusText.Contains("Failed", StringComparison.OrdinalIgnoreCase);

                        if (isPaused)
                        {
                            vm.SmoothedSpeed = 0;
                            vm.SpeedText = "0 MB/s";
                            vm.RemainingText = "Paused";
                        }
                        else if (isCompleted)
                        {
                            vm.SmoothedSpeed = 0;
                            vm.SpeedText = "0 MB/s";
                            vm.RemainingText = "Completed";
                        }
                        else if (isFailed)
                        {
                            vm.SmoothedSpeed = 0;
                            vm.SpeedText = "0 MB/s";
                            vm.RemainingText = "Failed";
                        }
                        else
                        {
                            const double alpha = 0.35;
                            vm.SmoothedSpeed = alpha * downloadedDelta + (1.0 - alpha) * vm.SmoothedSpeed;
                            vm.SpeedText = $"{(vm.SmoothedSpeed / (1024.0 * 1024.0)):F2} MB/s";

                            if (status.TotalSize.HasValue && status.TotalSize.Value > 0 && vm.SmoothedSpeed > 1024)
                            {
                                long bytesRemaining = (long)Math.Max(0, (long)status.TotalSize.Value - currentDownloaded);
                                long secondsLeft = (long)(bytesRemaining / vm.SmoothedSpeed);
                                vm.RemainingText = FormatRemainingTime(secondsLeft);
                            }
                            else
                            {
                                vm.RemainingText = "Estimating...";
                            }
                        }

                        if (status.TotalSize.HasValue && status.TotalSize.Value > 0)
                        {
                            vm.Progress = ((double)status.Downloaded / status.TotalSize.Value) * 100;
                            vm.PercentageText = $"{vm.Progress:F1}%";
                            vm.TotalSizeText = $"{(status.TotalSize.Value / (1024.0 * 1024.0)):F2} MB";
                            
                            // Update chunks
                            if (vm.Chunks.Count != status.Chunks.Length) {
                                vm.Chunks.Clear();
                                foreach (var c in status.Chunks) vm.Chunks.Add(new ChunkViewModel());
                            }
                            for (int i = 0; i < status.Chunks.Length; i++) {
                                var c = status.Chunks[i];
                                double ratio = 0;
                                if (c.End > c.Start) ratio = (double)c.Downloaded / (c.End - c.Start + 1);
                                vm.Chunks[i].ProgressWidth = Math.Max(0, Math.Min(1, ratio));
                            }
                            
                            vm.Status = status.StatusText;
                        }
                        else
                        {
                            vm.Progress = 0;
                            vm.PercentageText = "Unknown";
                            vm.TotalSizeText = $"{status.Downloaded / (1024 * 1024)} MB";
                            vm.Status = status.StatusText;
                        }
                    }
                    ApplyFilter();
                }
            }
            catch { }
        }

        public async void AddDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            NewDownloadDialog.XamlRoot = this.XamlRoot;
            SavePathTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads\FoxLoader";
            FileNameTextBox.Text = string.Empty;

            try {
                var dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                if (dataPackageView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text)) {
                    var text = await dataPackageView.GetTextAsync();
                    if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) {
                        UrlTextBox.Text = text;
                    } else {
                        UrlTextBox.Text = string.Empty;
                    }
                } else {
                    UrlTextBox.Text = string.Empty;
                }
            } catch { UrlTextBox.Text = string.Empty; }

            await NewDownloadDialog.ShowAsync();
        }

        private async void BatchDownloadBtn_Click(object sender, RoutedEventArgs e)
        {
            BatchAddDialog.XamlRoot = this.XamlRoot;
            BatchSavePathBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads\FoxLoader";
            BatchUrlsTextBox.Text = string.Empty;
            await BatchAddDialog.ShowAsync();
        }

        private async void BatchAddDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            await ProcessBatchUrls(startImmediately: true);
        }

        private async void BatchAddDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            await ProcessBatchUrls(startImmediately: false);
        }

        private async void GrabLinksFromWebpage_Click(object sender, RoutedEventArgs e)
        {
            var pageUrl = BatchWebpageUrlBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(pageUrl)) return;

            if (!pageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !pageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                pageUrl = "https://" + pageUrl;
                BatchWebpageUrlBox.Text = pageUrl;
            }

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) FoxLoader/1.0");
                var html = await client.GetStringAsync(pageUrl);

                var baseUri = new Uri(pageUrl);
                var hrefRegex = new System.Text.RegularExpressions.Regex(@"href\s*=\s*[""'](?<url>[^""'#>]+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var matches = hrefRegex.Matches(html);

                bool hideHtml = HideHtmlCheckBox.IsChecked ?? true;
                var foundUrls = new HashSet<string>();

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var raw = match.Groups["url"].Value.Trim();
                    if (Uri.TryCreate(baseUri, raw, out var resolvedUri) && (resolvedUri.Scheme == Uri.UriSchemeHttp || resolvedUri.Scheme == Uri.UriSchemeHttps))
                    {
                        var path = resolvedUri.LocalPath.ToLowerInvariant();
                        if (hideHtml)
                        {
                            if (path.EndsWith(".html") || path.EndsWith(".htm") || path.EndsWith(".php") || path.EndsWith(".asp") || path.EndsWith(".aspx") || path.EndsWith(".jsp") || !path.Contains('.'))
                            {
                                continue;
                            }
                        }
                        foundUrls.Add(resolvedUri.AbsoluteUri);
                    }
                }

                BatchUrlsTextBox.Text = string.Join(Environment.NewLine, foundUrls);
            }
            catch (Exception ex)
            {
                BatchUrlsTextBox.Text = $"Error extracting links: {ex.Message}";
            }
        }

        private async System.Threading.Tasks.Task ProcessBatchUrls(bool startImmediately)
        {
            var text = BatchUrlsTextBox.Text;
            if (string.IsNullOrWhiteSpace(text)) return;
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var queue = BatchQueueComboBox.SelectedItem is ComboBoxItem cbi ? cbi.Content.ToString() : "Main Queue";

            bool routeByCategory = BatchRoutingRadio.SelectedIndex == 0;
            var customSavePath = BatchSavePathBox.Text;
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads\FoxLoader";
            bool hideHtml = HideHtmlCheckBox.IsChecked ?? false;

            var items = new List<object>();
            foreach (var line in lines)
            {
                var url = line.Trim();
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var ext = System.IO.Path.GetExtension(uri.LocalPath).ToLowerInvariant();
                    if (hideHtml && (ext == ".html" || ext == ".htm" || ext == ".php" || ext == ".asp" || ext == ".aspx" || ext == ".jsp"))
                    {
                        continue;
                    }

                    var fileName = System.IO.Path.GetFileName(uri.LocalPath);
                    if (string.IsNullOrEmpty(fileName)) fileName = "downloaded_file";
                    fileName = Uri.UnescapeDataString(fileName);

                    string targetPath = customSavePath;
                    if (routeByCategory)
                    {
                        string category = "General";
                        if (ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".tar" || ext == ".gz") category = "Compressed";
                        else if (ext == ".pdf" || ext == ".doc" || ext == ".docx" || ext == ".txt" || ext == ".xlsx") category = "Documents";
                        else if (ext == ".mp3" || ext == ".wav" || ext == ".flac") category = "Music";
                        else if (ext == ".exe" || ext == ".msi" || ext == ".apk") category = "Programs";
                        else if (ext == ".mp4" || ext == ".mkv" || ext == ".avi") category = "Video";
                        
                        targetPath = System.IO.Path.Combine(basePath, category);
                    }

                    items.Add(new {
                        url = url,
                        file_name = fileName,
                        save_path = targetPath,
                        start_immediately = startImmediately,
                        queue = queue
                    });
                }
            }

            if (items.Count > 0)
            {
                using var client = new HttpClient();
                var payload = new { items = items };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                await client.PostAsync(FoxLoaderConfig.BaseUrl + "/batch_add", content);
            }
        }

        public void ApplyLanguage()
        {
            var lm = LanguageManager.Instance;
            this.FlowDirection = lm.CurrentFlowDirection;

            if (ItemAllDownloads != null) ItemAllDownloads.Content = lm.Get("Nav_AllDownloads", "All Downloads");
            if (ItemUnfinished != null) ItemUnfinished.Content = lm.Get("Nav_Unfinished", "Unfinished");
            if (ItemCompleted != null) ItemCompleted.Content = lm.Get("Nav_Completed", "Completed");
            if (HeaderCategories != null) HeaderCategories.Content = lm.Get("Nav_Categories", "Categories");
            if (ItemCompressed != null) ItemCompressed.Content = lm.Get("Nav_Compressed", "Compressed");
            if (ItemDocuments != null) ItemDocuments.Content = lm.Get("Nav_Documents", "Documents");
            if (ItemMusic != null) ItemMusic.Content = lm.Get("Nav_Music", "Music");
            if (ItemPrograms != null) ItemPrograms.Content = lm.Get("Nav_Programs", "Programs");
            if (ItemVideo != null) ItemVideo.Content = lm.Get("Nav_Video", "Video");
            if (HeaderQueues != null) HeaderQueues.Content = lm.Get("Nav_Queues", "Queues");
            if (ItemMainQueue != null) ItemMainQueue.Content = lm.Get("Nav_MainQueue", "Main Queue");
            if (ItemNightQueue != null) ItemNightQueue.Content = lm.Get("Nav_NightQueue", "Night Queue");
            if (NavView?.SettingsItem is NavigationViewItem settingsNav)
            {
                settingsNav.Content = lm.Get("Nav_Settings", "Settings");
            }

            if (BtnAddUrl != null) BtnAddUrl.Label = lm.Get("Toolbar_AddUrl", "Add URL");
            if (BtnBatchUrls != null) BtnBatchUrls.Label = lm.Get("Toolbar_BatchUrls", "Batch URLs");
            if (BtnResume != null) BtnResume.Label = lm.Get("Toolbar_Resume", "Resume");
            if (BtnPause != null) BtnPause.Label = lm.Get("Toolbar_Pause", "Pause");
            if (BtnStopAll != null) BtnStopAll.Label = lm.Get("Toolbar_StopAll", "Stop All");
            if (BtnRemove != null) BtnRemove.Label = lm.Get("Toolbar_Remove", "Remove");
            if (BtnRemoveCompleted != null) BtnRemoveCompleted.Label = lm.Get("Toolbar_Clear", "Remove Completed");
            if (BtnScheduler != null) BtnScheduler.Label = lm.Get("Toolbar_Scheduler", "Scheduler");
            if (BtnOptions != null) BtnOptions.Label = lm.Get("Toolbar_Options", "Options");

            if (ColFileName != null) ColFileName.Text = lm.Get("Header_FileName", "File Name");
            if (ColSize != null) ColSize.Text = lm.Get("Header_Size", "Size");
            if (ColStatus != null) ColStatus.Text = lm.Get("Header_Status", "Status");
            if (ColTimeLeft != null) ColTimeLeft.Text = lm.Get("Header_TimeLeft", "Time Left");
            if (ColTransferRate != null) ColTransferRate.Text = lm.Get("Header_TransferRate", "Transfer Rate");
            if (ColLastTry != null) ColLastTry.Text = lm.Get("Header_LastTry", "Last Try");
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            var hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            folderPicker.SuggestedStartLocation = PickerLocationId.Downloads;
            folderPicker.FileTypeFilter.Add("*");

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                SavePathTextBox.Text = folder.Path;
            }
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryComboBox.SelectedItem is ComboBoxItem item && SavePathTextBox != null)
            {
                var cat = item.Content?.ToString() ?? "General";
                var basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads\FoxLoader";
                if (cat != "General")
                {
                    SavePathTextBox.Text = System.IO.Path.Combine(basePath, cat);
                }
                else
                {
                    SavePathTextBox.Text = basePath;
                }
            }
        }

        private void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try {
                if (Uri.TryCreate(UrlTextBox.Text, UriKind.Absolute, out var uri)) {
                    var fileName = System.IO.Path.GetFileName(uri.LocalPath);
                    if (!string.IsNullOrEmpty(fileName)) {
                        FileNameTextBox.Text = Uri.UnescapeDataString(fileName);
                        
                        var ext = System.IO.Path.GetExtension(fileName).ToLower();
                        if (ext == ".zip" || ext == ".rar" || ext == ".7z") CategoryComboBox.SelectedIndex = 1;
                        else if (ext == ".pdf" || ext == ".doc" || ext == ".txt") CategoryComboBox.SelectedIndex = 2;
                        else if (ext == ".mp3" || ext == ".wav") CategoryComboBox.SelectedIndex = 3;
                        else if (ext == ".exe" || ext == ".msi") CategoryComboBox.SelectedIndex = 4;
                        else if (ext == ".mp4" || ext == ".mkv") CategoryComboBox.SelectedIndex = 5;
                        else CategoryComboBox.SelectedIndex = 0;
                    }
                }
            } catch {}
        }

        private async void NewDownloadDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var url = UrlTextBox.Text;
            var fileName = FileNameTextBox.Text;
            var savePath = SavePathTextBox.Text;
            bool startImmediately = StartNowCheckBox.IsChecked ?? true;
            string queueName = QueueComboBox.SelectedItem is ComboBoxItem cbi && cbi.Content != null ? cbi.Content.ToString()! : "Main Queue";
            
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(fileName)) return;

            var item = new DownloadItemViewModel 
            { 
                FileName = fileName, 
                Url = url,
                SavePath = savePath,
                Status = startImmediately ? "Connecting to Rust Core..." : "Paused", 
                Progress = 0,
                TotalSizeText = "Unknown",
                SpeedText = "0 MB/s",
                RemainingText = "Estimating..."
            };
            Downloads.Add(item);
            UrlTextBox.Text = string.Empty;

            try 
            {
                using var client = new HttpClient();
                var payload = new { url = url, file_name = fileName, save_path = savePath, start_immediately = startImmediately, queue = queueName };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(FoxLoaderConfig.BaseUrl + "/add", content);
                
                if (response.IsSuccessStatusCode)
                {
                    item.Status = "Added to queue";
                    var resJson = await response.Content.ReadAsStringAsync();
                    var addRes = JsonSerializer.Deserialize<AddDownloadRes>(resJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (addRes != null) {
                        item.Id = addRes.Id;
                    }
                }
                else 
                {
                    item.Status = $"Failed: {response.StatusCode}";
                }
            }
            catch (Exception)
            {
                item.Status = "Core is offline";
            }
        }

        private void Grid_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is DownloadItemViewModel item)
            {
                try
                {
                    var progressWin = new DownloadProgressWindow(item);
                    progressWin.Activate();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error launching DownloadProgressWindow: {ex}");
                }
            }
        }

        private async void CalculateMD5_Click(object sender, RoutedEventArgs e)
        {
            await CalculateChecksum("md5");
        }

        private async void CalculateSHA256_Click(object sender, RoutedEventArgs e)
        {
            await CalculateChecksum("sha256");
        }

        private async System.Threading.Tasks.Task CalculateChecksum(string algo)
        {
            ChecksumTextBox.Text = "Calculating hash...";
            try
            {
                var filePath = System.IO.Path.Combine(PropPathText.Text, PropFileNameText.Text);
                using var client = new HttpClient();
                var payload = new { file_path = filePath, algorithm = algo };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var res = await client.PostAsync(FoxLoaderConfig.BaseUrl + "/verify_checksum", content);
                if (res.IsSuccessStatusCode)
                {
                    var resJson = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(resJson);
                    if (doc.RootElement.TryGetProperty("hash", out var h))
                    {
                        ChecksumTextBox.Text = h.GetString();
                    }
                }
                else
                {
                    ChecksumTextBox.Text = "Hash calculation failed (File may be downloading)";
                }
            }
            catch (Exception ex)
            {
                ChecksumTextBox.Text = ex.Message;
            }
        }

        private void MenuOpen_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is DownloadItemViewModel item) {
                var filePath = System.IO.Path.Combine(item.SavePath, item.FileName);
                if (System.IO.File.Exists(filePath)) {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
            }
        }

        private void MenuOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is DownloadItemViewModel item) {
                var filePath = System.IO.Path.Combine(item.SavePath, item.FileName);
                if (System.IO.File.Exists(filePath)) {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                } else if (System.IO.Directory.Exists(item.SavePath)) {
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{item.SavePath}\"");
                }
            }
        }

        private List<DownloadItemViewModel> GetSelectedDownloads(DownloadItemViewModel? contextItem = null)
        {
            var selected = DownloadsList.SelectedItems.OfType<DownloadItemViewModel>().ToList();
            if (contextItem != null && !selected.Contains(contextItem))
            {
                selected = new List<DownloadItemViewModel> { contextItem };
            }
            if (selected.Count == 0 && DownloadsList.SelectedItem is DownloadItemViewModel current)
            {
                selected.Add(current);
            }
            return selected;
        }

        private async void MenuPause_Click(object sender, RoutedEventArgs e)
        {
            var contextItem = (sender as FrameworkElement)?.DataContext as DownloadItemViewModel;
            var targets = GetSelectedDownloads(contextItem);
            using var client = new HttpClient();
            foreach (var item in targets)
            {
                try
                {
                    var payload = new { id = item.Id };
                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    await client.PostAsync(FoxLoaderConfig.BaseUrl + "/pause", content);
                }
                catch { }
            }
        }

        private async void MenuResume_Click(object sender, RoutedEventArgs e)
        {
            var contextItem = (sender as FrameworkElement)?.DataContext as DownloadItemViewModel;
            var targets = GetSelectedDownloads(contextItem);
            using var client = new HttpClient();
            foreach (var item in targets)
            {
                try
                {
                    var payload = new { id = item.Id };
                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    await client.PostAsync(FoxLoaderConfig.BaseUrl + "/resume", content);
                }
                catch { }
            }
        }

        private void MenuOpenWith_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is DownloadItemViewModel item) {
                var filePath = System.IO.Path.Combine(item.SavePath, item.FileName);
                if (System.IO.File.Exists(filePath)) {
                    try {
                        System.Diagnostics.Process.Start("rundll32.exe", $"shell32.dll,OpenAs_RunDLL \"{filePath}\"");
                    } catch { }
                }
            }
        }

        private async void MenuRedownload_Click(object sender, RoutedEventArgs e)
        {
            var contextItem = (sender as FrameworkElement)?.DataContext as DownloadItemViewModel;
            var targets = GetSelectedDownloads(contextItem);
            using var client = new HttpClient();
            foreach (var item in targets)
            {
                try
                {
                    var pauseContent = new StringContent(JsonSerializer.Serialize(new { id = item.Id }), Encoding.UTF8, "application/json");
                    await client.PostAsync(FoxLoaderConfig.BaseUrl + "/pause", pauseContent);

                    var payload = new { url = item.Url, file_name = item.FileName, save_path = item.SavePath, start_immediately = true, queue = "Main Queue" };
                    var addContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    await client.PostAsync(FoxLoaderConfig.BaseUrl + "/add", addContent);
                }
                catch { }
            }
        }

        private async void MenuMoveQueue_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem mfi)
            {
                var contextItem = mfi.DataContext as DownloadItemViewModel;
                var targets = GetSelectedDownloads(contextItem);
                string queueName = mfi.Tag?.ToString() ?? "Main Queue";
                using var client = new HttpClient();
                foreach (var item in targets)
                {
                    try
                    {
                        var payload = new { queue = queueName, item_id = item.Id };
                        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                        await client.PostAsync(FoxLoaderConfig.BaseUrl + "/queue_reorder", content);
                        item.Status = $"Moved to {queueName}";
                    }
                    catch { }
                }
            }
        }

        private void MenuCopyUrl_Click(object sender, RoutedEventArgs e)
        {
            var contextItem = (sender as FrameworkElement)?.DataContext as DownloadItemViewModel;
            var targets = GetSelectedDownloads(contextItem);
            if (targets.Count > 0)
            {
                var text = string.Join(Environment.NewLine, targets.Select(t => t.Url));
                var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            }
        }

        private void MenuCopyFileName_Click(object sender, RoutedEventArgs e)
        {
            var contextItem = (sender as FrameworkElement)?.DataContext as DownloadItemViewModel;
            var targets = GetSelectedDownloads(contextItem);
            if (targets.Count > 0)
            {
                var text = string.Join(Environment.NewLine, targets.Select(t => t.FileName));
                var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            }
        }

        private void MenuCopyPath_Click(object sender, RoutedEventArgs e)
        {
            var contextItem = (sender as FrameworkElement)?.DataContext as DownloadItemViewModel;
            var targets = GetSelectedDownloads(contextItem);
            if (targets.Count > 0)
            {
                var text = string.Join(Environment.NewLine, targets.Select(t => System.IO.Path.Combine(t.SavePath, t.FileName)));
                var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            }
        }

        private void MenuVerifyChecksum_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is DownloadItemViewModel item) {
                ShowPropertiesFor(item);
            }
        }

        private void MenuProperties_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is DownloadItemViewModel item) {
                ShowPropertiesFor(item);
            }
        }

        private async void ShowPropertiesFor(DownloadItemViewModel item)
        {
            PropFileNameText.Text = item.FileName;
            PropStatusText.Text = item.Status;
            PropUrlText.Text = item.Url;
            PropSizeText.Text = item.TotalSizeText;
            PropSpeedText.Text = item.SpeedText;
            PropPathText.Text = item.SavePath;
            ChecksumTextBox.Text = string.Empty;

            PropertiesDialog.XamlRoot = this.XamlRoot;
            await PropertiesDialog.ShowAsync();
        }

        private void MenuDeleteFromDisk_Click(object sender, RoutedEventArgs e)
        {
            var contextItem = (sender as FrameworkElement)?.DataContext as DownloadItemViewModel;
            var targets = GetSelectedDownloads(contextItem);
            if (targets.Count > 0)
            {
                PromptDelete(targets, deleteFromDisk: true);
            }
        }

        private void MenuDelete_Click(object sender, RoutedEventArgs e)
        {
            var contextItem = (sender as FrameworkElement)?.DataContext as DownloadItemViewModel;
            var targets = GetSelectedDownloads(contextItem);
            if (targets.Count > 0)
            {
                PromptDelete(targets, deleteFromDisk: false);
            }
        }

        private void TopMenuPause_Click(object sender, RoutedEventArgs e)
        {
            MenuPause_Click(sender, e);
        }

        private void TopMenuResume_Click(object sender, RoutedEventArgs e)
        {
            MenuResume_Click(sender, e);
        }

        private void TopMenuDelete_Click(object sender, RoutedEventArgs e)
        {
            var targets = GetSelectedDownloads();
            if (targets.Count > 0)
            {
                PromptDelete(targets, deleteFromDisk: false);
            }
        }

        private async void PromptDelete(List<DownloadItemViewModel> items, bool deleteFromDisk = false)
        {
            _itemsToDelete = new List<DownloadItemViewModel>(items);
            if (items.Count == 1)
            {
                DeleteFileNameText.Text = items[0].FileName;
            }
            else
            {
                DeleteFileNameText.Text = $"{items.Count} items selected";
            }
            DeleteFromDiskCheckBox.IsChecked = deleteFromDisk;
            DeleteConfirmDialog.XamlRoot = this.XamlRoot;
            await DeleteConfirmDialog.ShowAsync();
        }

        private async void DeleteConfirmDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            bool deleteFromDisk = DeleteFromDiskCheckBox.IsChecked ?? false;
            using var client = new HttpClient();
            foreach (var item in _itemsToDelete)
            {
                try
                {
                    _allDownloads.Remove(item);
                    Downloads.Remove(item);
                    var payload = new { id = item.Id, delete_from_disk = deleteFromDisk };
                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    await client.PostAsync(FoxLoaderConfig.BaseUrl + "/delete", content);
                }
                catch { }
            }
            _itemsToDelete.Clear();
        }

        private void DownloadsList_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Delete)
            {
                var targets = GetSelectedDownloads();
                if (targets.Count > 0)
                {
                    PromptDelete(targets, deleteFromDisk: false);
                    e.Handled = true;
                }
            }
            else if (e.Key == Windows.System.VirtualKey.Space)
            {
                var targets = GetSelectedDownloads();
                if (targets.Count > 0)
                {
                    bool anyActive = targets.Any(t => t.Status == "Downloading" || t.Status == "Connecting");
                    if (anyActive)
                    {
                        MenuPause_Click(sender, e);
                    }
                    else
                    {
                        MenuResume_Click(sender, e);
                    }
                    e.Handled = true;
                }
            }
        }

        private void ColHeader_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string col)
            {
                if (_currentSortCol == col)
                {
                    _currentSortAscending = !_currentSortAscending;
                }
                else
                {
                    _currentSortCol = col;
                    _currentSortAscending = true;
                }
                UpdateSortGlyphs();
                SortDownloadsList();
            }
        }

        private void UpdateSortGlyphs()
        {
            string glyph = _currentSortAscending ? " ▲" : " ▼";
            ColFileNameGlyph.Visibility = _currentSortCol == "FileName" ? Visibility.Visible : Visibility.Collapsed;
            ColFileNameGlyph.Text = glyph;
            ColSizeGlyph.Visibility = _currentSortCol == "Size" ? Visibility.Visible : Visibility.Collapsed;
            ColSizeGlyph.Text = glyph;
            ColStatusGlyph.Visibility = _currentSortCol == "Status" ? Visibility.Visible : Visibility.Collapsed;
            ColStatusGlyph.Text = glyph;
            ColTimeLeftGlyph.Visibility = _currentSortCol == "TimeLeft" ? Visibility.Visible : Visibility.Collapsed;
            ColTimeLeftGlyph.Text = glyph;
            ColTransferRateGlyph.Visibility = _currentSortCol == "TransferRate" ? Visibility.Visible : Visibility.Collapsed;
            ColTransferRateGlyph.Text = glyph;
            ColLastTryGlyph.Visibility = _currentSortCol == "LastTry" ? Visibility.Visible : Visibility.Collapsed;
            ColLastTryGlyph.Text = glyph;
        }

        private void SortDownloadsList()
        {
            IEnumerable<DownloadItemViewModel> query = _currentSortCol switch
            {
                "FileName" => _currentSortAscending ? _allDownloads.OrderBy(d => d.FileName) : _allDownloads.OrderByDescending(d => d.FileName),
                "Size" => _currentSortAscending ? _allDownloads.OrderBy(d => d.TotalSize ?? 0) : _allDownloads.OrderByDescending(d => d.TotalSize ?? 0),
                "Status" => _currentSortAscending ? _allDownloads.OrderBy(d => d.Status) : _allDownloads.OrderByDescending(d => d.Status),
                "TimeLeft" => _currentSortAscending ? _allDownloads.OrderBy(d => d.RemainingText) : _allDownloads.OrderByDescending(d => d.RemainingText),
                "TransferRate" => _currentSortAscending ? _allDownloads.OrderBy(d => d.SmoothedSpeed) : _allDownloads.OrderByDescending(d => d.SmoothedSpeed),
                "LastTry" => _currentSortAscending ? _allDownloads.OrderBy(d => d.Id) : _allDownloads.OrderByDescending(d => d.Id),
                _ => _allDownloads
            };
            _allDownloads = query.ToList();
            ApplyFilter();
        }

        private void BatchWildcardBtn_Click(object sender, RoutedEventArgs e)
        {
            BatchWildcardDialog.XamlRoot = this.XamlRoot;
            UpdateWildcardPreviews();
            _ = BatchWildcardDialog.ShowAsync();
        }

        private void WildcardAddress_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateWildcardPreviews();
        }

        private void WildcardParam_Changed(object sender, RoutedEventArgs e)
        {
            UpdateWildcardPreviews();
        }

        private void WildcardParam_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            UpdateWildcardPreviews();
        }

        private void UpdateWildcardPreviews()
        {
            if (WildcardPreviewFirst == null || WildcardPreviewSecond == null || WildcardPreviewLast == null) return;
            string pattern = WildcardAddressBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(pattern))
            {
                WildcardPreviewFirst.Text = string.Empty;
                WildcardPreviewSecond.Text = string.Empty;
                WildcardPreviewLast.Text = string.Empty;
                return;
            }

            bool isNumbers = WildcardNumbersRadio?.IsChecked ?? true;
            if (isNumbers)
            {
                long from = (long)(WildcardFromBox?.Value ?? 1);
                long to = (long)(WildcardToBox?.Value ?? 10);
                int digits = (int)(WildcardDigitsBox?.Value ?? 2);
                if (digits < 1) digits = 1;
                string fmt = new string('0', digits);

                WildcardPreviewFirst.Text = pattern.Contains('*') ? pattern.Replace("*", from.ToString(fmt)) : pattern;
                WildcardPreviewSecond.Text = pattern.Contains('*') ? pattern.Replace("*", (from + 1).ToString(fmt)) : pattern;
                WildcardPreviewLast.Text = pattern.Contains('*') ? pattern.Replace("*", to.ToString(fmt)) : pattern;
            }
            else
            {
                WildcardPreviewFirst.Text = pattern.Contains('*') ? pattern.Replace("*", "a") : pattern;
                WildcardPreviewSecond.Text = pattern.Contains('*') ? pattern.Replace("*", "b") : pattern;
                WildcardPreviewLast.Text = pattern.Contains('*') ? pattern.Replace("*", "z") : pattern;
            }
        }

        private async void BatchWildcardDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            string pattern = WildcardAddressBox.Text.Trim();
            if (string.IsNullOrEmpty(pattern) || !pattern.Contains('*')) return;

            var generatedUrls = new List<string>();
            bool isNumbers = WildcardNumbersRadio.IsChecked ?? true;
            if (isNumbers)
            {
                long from = (long)WildcardFromBox.Value;
                long to = (long)WildcardToBox.Value;
                int digits = (int)WildcardDigitsBox.Value;
                if (digits < 1) digits = 1;
                string fmt = new string('0', digits);

                long start = Math.Min(from, to);
                long end = Math.Max(from, to);
                for (long i = start; i <= end; i++)
                {
                    generatedUrls.Add(pattern.Replace("*", i.ToString(fmt)));
                }
            }
            else
            {
                for (char c = 'a'; c <= 'z'; c++)
                {
                    generatedUrls.Add(pattern.Replace("*", c.ToString()));
                }
            }

            if (generatedUrls.Count > 0)
            {
                BatchUrlsTextBox.Text = string.Join(Environment.NewLine, generatedUrls);
                BatchAddDialog.XamlRoot = this.XamlRoot;
                BatchSavePathBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads\FoxLoader";
                await BatchAddDialog.ShowAsync();
            }
        }

        private async void ExportList_Click(object sender, RoutedEventArgs e)
        {
            ExportListDialog.XamlRoot = this.XamlRoot;
            await ExportListDialog.ShowAsync();
        }

        private async void ExportListDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            List<DownloadItemViewModel> toExport = new();
            int option = ExportOptionsRadio.SelectedIndex;
            if (option == 0)
            {
                toExport = Downloads.Where(d => d.Status != "Completed").ToList();
            }
            else if (option == 1)
            {
                toExport = GetSelectedDownloads();
            }
            else
            {
                toExport = _allDownloads.ToList();
            }

            if (toExport.Count == 0) return;

            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.Downloads;
            savePicker.FileTypeChoices.Add("Text File (*.txt)", new List<string>() { ".txt" });
            savePicker.SuggestedFileName = "FoxLoader_exported_links.txt";

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                var sb = new StringBuilder();
                foreach (var item in toExport)
                {
                    sb.AppendLine(item.Url);
                }
                await Windows.Storage.FileIO.WriteTextAsync(file, sb.ToString());
            }
        }

        private async void ImportList_Click(object sender, RoutedEventArgs e)
        {
            var openPicker = new FileOpenPicker();
            openPicker.SuggestedStartLocation = PickerLocationId.Downloads;
            openPicker.FileTypeFilter.Add(".txt");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);

            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                var text = await Windows.Storage.FileIO.ReadTextAsync(file);
                var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(l => l.Trim().Trim('<', '>'))
                                .Where(l => l.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || l.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                .ToList();

                if (lines.Count > 0)
                {
                    BatchUrlsTextBox.Text = string.Join(Environment.NewLine, lines);
                    BatchAddDialog.XamlRoot = this.XamlRoot;
                    BatchSavePathBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads\FoxLoader";
                    await BatchAddDialog.ShowAsync();
                }
            }
        }

        private async void GrabAllLinksBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Download all links from webpage",
                PrimaryButtonText = "Fetch Links",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };
            var box = new TextBox
            {
                PlaceholderText = "https://example.com/downloads.html",
                Header = "Webpage Address:"
            };
            dialog.Content = box;
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(box.Text))
            {
                string url = box.Text.Trim();
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }
                await FetchAndShowAllLinksAsync(url);
            }
        }

        private async System.Threading.Tasks.Task FetchAndShowAllLinksAsync(string pageUrl)
        {
            try
            {
                using var client = new HttpClient();
                var html = await client.GetStringAsync(pageUrl);
                var baseUri = new Uri(pageUrl);
                var matches = System.Text.RegularExpressions.Regex.Matches(html, @"<a\s+(?:[^>]*?\s+)?href=[""']([^""'#]+)[""'][^>]*>(.*?)</a>", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                
                _allDiscoveredBatchLinks.Clear();
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    var href = m.Groups[1].Value.Trim();
                    var linkText = System.Text.RegularExpressions.Regex.Replace(m.Groups[2].Value, "<.*?>", string.Empty).Trim();
                    if (string.IsNullOrEmpty(href) || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (Uri.TryCreate(baseUri, href, out var absoluteUri))
                    {
                        var absStr = absoluteUri.ToString();
                        string fileName = System.IO.Path.GetFileName(absoluteUri.LocalPath);
                        if (string.IsNullOrWhiteSpace(fileName)) fileName = "index.html";

                        string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
                        bool isHtml = ext == ".html" || ext == ".htm" || ext == ".php" || ext == ".asp" || ext == ".aspx" || string.IsNullOrEmpty(ext);
                        bool isImg = ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".webp";

                        string fileType = "Document";
                        if (isImg) fileType = "Image";
                        else if (ext == ".zip" || ext == ".rar" || ext == ".7z") fileType = "Compressed";
                        else if (ext == ".mp4" || ext == ".mkv" || ext == ".avi") fileType = "Video";
                        else if (ext == ".mp3" || ext == ".flac") fileType = "Music";
                        else if (ext == ".exe" || ext == ".msi") fileType = "Program";
                        else if (isHtml) fileType = "HTML Page";

                        _allDiscoveredBatchLinks.Add(new BatchLinkItemViewModel
                        {
                            IsSelected = !isHtml,
                            FileName = fileName,
                            FileType = fileType,
                            Url = absStr,
                            LinkText = string.IsNullOrWhiteSpace(linkText) ? fileName : linkText,
                            IsHtml = isHtml,
                            IsImage = isImg
                        });
                    }
                }

                ApplyBatchLinksFilter();
                BatchAllLinksDialog.XamlRoot = this.XamlRoot;
                SaveToOneDirTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads\FoxLoader";
                await BatchAllLinksDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                var errDlg = new ContentDialog
                {
                    Title = "Failed to fetch webpage",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errDlg.ShowAsync();
            }
        }

        private void ApplyBatchLinksFilter()
        {
            string query = BatchAllLinksSearchBox?.Text?.Trim() ?? string.Empty;
            bool hideHtml = BatchHideHtmlCheck?.IsChecked ?? true;
            bool hideImages = BatchHideImagesCheck?.IsChecked ?? false;
            bool hideRepeated = BatchHideRepeatedCheck?.IsChecked ?? true;

            BatchAllLinks.Clear();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in _allDiscoveredBatchLinks)
            {
                if (hideHtml && item.IsHtml) continue;
                if (hideImages && item.IsImage) continue;
                if (hideRepeated && seen.Contains(item.Url)) continue;

                if (!string.IsNullOrEmpty(query))
                {
                    if (!item.FileName.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                        !item.Url.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                        !item.FileType.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                seen.Add(item.Url);
                BatchAllLinks.Add(item);
            }
        }

        private void BatchAllLinksSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyBatchLinksFilter();
        }

        private void BatchFilterOption_Changed(object sender, RoutedEventArgs e)
        {
            ApplyBatchLinksFilter();
        }

        private void BatchAllLinksCheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in BatchAllLinks)
            {
                item.IsSelected = true;
            }
        }

        private void BatchAllLinksUncheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in BatchAllLinks)
            {
                item.IsSelected = false;
            }
        }

        private async void BrowseSaveToOneDir_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.Downloads;
            folderPicker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                SaveToOneDirTextBox.Text = folder.Path;
                SaveToOneDirRadio.IsChecked = true;
            }
        }

        private async void BatchAllLinksDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var selected = BatchAllLinks.Where(l => l.IsSelected).ToList();
            if (selected.Count == 0) return;

            bool startImmediately = BatchStartImmediatelyCheck.IsChecked ?? false;
            string defaultFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads\FoxLoader";

            using var client = new HttpClient();
            foreach (var item in selected)
            {
                string targetDir = defaultFolder;
                if (SaveToOneDirRadio.IsChecked == true && !string.IsNullOrWhiteSpace(SaveToOneDirTextBox.Text))
                {
                    targetDir = SaveToOneDirTextBox.Text.Trim();
                }
                else if (SaveToOneCategoryRadio.IsChecked == true)
                {
                    var cat = (SaveToCategoryComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "General";
                    targetDir = System.IO.Path.Combine(defaultFolder, cat);
                }
                else
                {
                    targetDir = System.IO.Path.Combine(defaultFolder, AutoDetectCategory(item.FileName));
                }

                try
                {
                    var payload = new
                    {
                        url = item.Url,
                        file_name = item.FileName,
                        save_path = targetDir,
                        start_immediately = startImmediately,
                        queue = "Main Queue"
                    };
                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    await client.PostAsync(FoxLoaderConfig.BaseUrl + "/add", content);
                }
                catch { }
            }
        }

        private static string AutoDetectCategory(string fileName)
        {
            string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            if (ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".tar" || ext == ".gz") return "Compressed";
            if (ext == ".pdf" || ext == ".doc" || ext == ".docx" || ext == ".txt" || ext == ".xlsx") return "Documents";
            if (ext == ".mp3" || ext == ".wav" || ext == ".ogg" || ext == ".flac") return "Music";
            if (ext == ".exe" || ext == ".msi" || ext == ".apk" || ext == ".dmg") return "Programs";
            if (ext == ".mp4" || ext == ".mkv" || ext == ".avi" || ext == ".mov") return "Video";
            return "General";
        }

        private async void TopMenuStopAll_Click(object sender, RoutedEventArgs e)
        {
            using var client = new HttpClient();
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            await client.PostAsync(FoxLoaderConfig.BaseUrl + "/pause_all", content);
        }

        private async void TopMenuClear_Click(object sender, RoutedEventArgs e)
        {
            using var client = new HttpClient();
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            await client.PostAsync(FoxLoaderConfig.BaseUrl + "/delete_completed", content);

            var completed = Downloads.Where(d => d.Status == "Completed").ToList();
            foreach (var c in completed) {
                Downloads.Remove(c);
            }
        }

        private object? _lastSelectFoxLoaderenuItem = null;

        public void OpenSettings(string section = "appearance")
        {
            if (NavView.SelectedItem != null && NavView.SelectedItem != NavView.SettingsItem)
            {
                _lastSelectFoxLoaderenuItem = NavView.SelectedItem;
            }

            DownloadsViewGrid.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
            NavView.IsBackButtonVisible = NavigationViewBackButtonVisible.Visible;
            SettingsView.LoadSettings();
            SettingsView.NavigateToSection(section);
        }

        private void CloseSettings(bool restoreNavSelection = true)
        {
            SettingsView.Visibility = Visibility.Collapsed;
            DownloadsViewGrid.Visibility = Visibility.Visible;
            NavView.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;

            if (restoreNavSelection)
            {
                if (_lastSelectFoxLoaderenuItem != null && _lastSelectFoxLoaderenuItem != NavView.SettingsItem)
                {
                    NavView.SelectedItem = _lastSelectFoxLoaderenuItem;
                }
                else if (NavView.MenuItems.Count > 0)
                {
                    NavView.SelectedItem = NavView.MenuItems[0];
                }
            }
        }

        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (SettingsView.Visibility == Visibility.Visible)
            {
                CloseSettings();
            }
        }

        private void SettingsView_CloseRequested(object? sender, EventArgs e)
        {
            CloseSettings();
        }

        private void TopMenuOptions_Click(object sender, RoutedEventArgs e)
        {
            OpenSettings("appearance");
        }

        private void TopMenuScheduler_Click(object sender, RoutedEventArgs e)
        {
            OpenSettings("queues");
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                OpenSettings();
                return;
            }

            if (SettingsView.Visibility == Visibility.Visible)
            {
                CloseSettings(restoreNavSelection: false);
            }

            if (args.SelectedItemContainer != null)
            {
                _lastSelectFoxLoaderenuItem = args.SelectedItem;
                _currentFilter = args.SelectedItemContainer.Tag?.ToString() ?? "all";
                ApplyFilter();
            }
        }

        private void ApplyFilter()
        {
            var filtered = _allDownloads.Where(MatchesFilter).ToList();
            for (int i = Downloads.Count - 1; i >= 0; i--)
            {
                if (!filtered.Contains(Downloads[i]))
                {
                    Downloads.RemoveAt(i);
                }
            }
            for (int i = 0; i < filtered.Count; i++)
            {
                var targetItem = filtered[i];
                int existingIdx = Downloads.IndexOf(targetItem);
                if (existingIdx == -1)
                {
                    Downloads.Insert(i, targetItem);
                }
                else if (existingIdx != i)
                {
                    Downloads.Move(existingIdx, i);
                }
            }
        }

        private bool MatchesFilter(DownloadItemViewModel item) {
            if (_currentFilter == "all") return true;
            if (_currentFilter == "unfinished") return item.Status != "Completed";
            if (_currentFilter == "completed") return item.Status == "Completed";
            
            string ext = System.IO.Path.GetExtension(item.FileName)?.ToLower() ?? "";
            if (_currentFilter == "cat_zip") return ext == ".zip" || ext == ".rar" || ext == ".7z" || ext == ".tar" || ext == ".gz";
            if (_currentFilter == "cat_docs") return ext == ".pdf" || ext == ".doc" || ext == ".docx" || ext == ".txt" || ext == ".xlsx";
            if (_currentFilter == "cat_music") return ext == ".mp3" || ext == ".wav" || ext == ".ogg" || ext == ".flac";
            if (_currentFilter == "cat_progs") return ext == ".exe" || ext == ".msi" || ext == ".apk" || ext == ".dmg";
            if (_currentFilter == "cat_video") return ext == ".mp4" || ext == ".mkv" || ext == ".avi" || ext == ".mov";
            if (_currentFilter == "queue_main") return true;
            if (_currentFilter == "queue_night") return true;
            
            return true;
        }

        private static string FormatRemainingTime(long totalSeconds)
        {
            if (totalSeconds <= 0) return "--";
            if (totalSeconds < 60) return $"{totalSeconds} sec";
            
            var ts = TimeSpan.FromSeconds(totalSeconds);
            if (ts.TotalHours < 1)
            {
                return $"{ts.Minutes} min {ts.Seconds} sec";
            }
            if (ts.TotalDays < 1)
            {
                return $"{ts.Hours} hr {ts.Minutes} min";
            }
            return $"{ts.Days} d {ts.Hours} hr";
        }
    }

    public class AddDownloadRes {
        public ulong Id { get; set; }
    }

    public class InteractiveReq {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
        
        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = string.Empty;
    }

    public class ChunkStatus {
        [JsonPropertyName("start")]
        public ulong Start { get; set; }
        
        [JsonPropertyName("end")]
        public ulong End { get; set; }
        
        [JsonPropertyName("downloaded")]
        public ulong Downloaded { get; set; }
    }

    public class JobStatus {
        [JsonPropertyName("id")]
        public ulong Id { get; set; }
        
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
        
        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = string.Empty;
        
        [JsonPropertyName("save_path")]
        public string SavePath { get; set; } = string.Empty;
        
        [JsonPropertyName("total_size")]
        public ulong? TotalSize { get; set; }
        
        [JsonPropertyName("downloaded")]
        public ulong Downloaded { get; set; }
        
        [JsonPropertyName("status_text")]
        public string StatusText { get; set; } = string.Empty;

        [JsonPropertyName("chunks")]
        public ChunkStatus[] Chunks { get; set; } = Array.Empty<ChunkStatus>();
    }

    [Microsoft.UI.Xaml.Data.Bindable]
    public class DownloadItemViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public ulong Id { get; set; }
        public long LastDownloadedBytes { get; set; }
        public double SmoothedSpeed { get; set; }

        private string _url = string.Empty;
        public string Url 
        { 
            get => _url; 
            set { _url = value; PropertyChanged?.Invoke(this, new(nameof(Url))); } 
        }

        private string _savePath = string.Empty;
        public string SavePath 
        { 
            get => _savePath; 
            set { _savePath = value; PropertyChanged?.Invoke(this, new(nameof(SavePath))); } 
        }

        private ObservableCollection<ChunkViewModel> _chunks = new();
        public ObservableCollection<ChunkViewModel> Chunks { get => _chunks; }

        private string _fileName = string.Empty;
        public string FileName 
        { 
            get => _fileName; 
            set { _fileName = value; PropertyChanged?.Invoke(this, new(nameof(FileName))); } 
        }

        private string _status = string.Empty;
        public string Status 
        { 
            get => _status; 
            set { _status = value; PropertyChanged?.Invoke(this, new(nameof(Status))); } 
        }

        private double _progress;
        public double Progress 
        { 
            get => _progress; 
            set { _progress = value; PropertyChanged?.Invoke(this, new(nameof(Progress))); } 
        }

        private string _percentageText = "0%";
        public string PercentageText 
        { 
            get => _percentageText; 
            set { _percentageText = value; PropertyChanged?.Invoke(this, new(nameof(PercentageText))); } 
        }

        private string _totalSizeText = "";
        public string TotalSizeText 
        { 
            get => _totalSizeText; 
            set { _totalSizeText = value; PropertyChanged?.Invoke(this, new(nameof(TotalSizeText))); } 
        }

        private string _remainingText = "";
        public string RemainingText 
        { 
            get => _remainingText; 
            set { _remainingText = value; PropertyChanged?.Invoke(this, new(nameof(RemainingText))); } 
        }

        public ulong? TotalSize { get; set; }

        private string _speedText = "0 MB/s";
        public string SpeedText 
        { 
            get => _speedText; 
            set { _speedText = value; PropertyChanged?.Invoke(this, new(nameof(SpeedText))); } 
        }
    }

    [Microsoft.UI.Xaml.Data.Bindable]
    public class BatchLinkItemViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); }
        }

        private string _url = string.Empty;
        public string Url
        {
            get => _url;
            set { _url = value; PropertyChanged?.Invoke(this, new(nameof(Url))); }
        }

        private string _fileName = string.Empty;
        public string FileName
        {
            get => _fileName;
            set { _fileName = value; PropertyChanged?.Invoke(this, new(nameof(FileName))); }
        }

        private string _fileType = string.Empty;
        public string FileType
        {
            get => _fileType;
            set { _fileType = value; PropertyChanged?.Invoke(this, new(nameof(FileType))); }
        }

        private string _linkText = string.Empty;
        public string LinkText
        {
            get => _linkText;
            set { _linkText = value; PropertyChanged?.Invoke(this, new(nameof(LinkText))); }
        }

        public bool IsHtml { get; set; }
        public bool IsImage { get; set; }
    }

    public class BatchInteractiveLinkIncoming
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("file_name")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("file_type")]
        public string FileType { get; set; } = string.Empty;

        [JsonPropertyName("link_text")]
        public string LinkText { get; set; } = string.Empty;
    }

    public class BatchInteractiveIncoming
    {
        [JsonPropertyName("page_url")]
        public string PageUrl { get; set; } = string.Empty;

        [JsonPropertyName("page_title")]
        public string PageTitle { get; set; } = string.Empty;

        [JsonPropertyName("links")]
        public List<BatchInteractiveLinkIncoming> Links { get; set; } = new();
    }

    [Microsoft.UI.Xaml.Data.Bindable]
    public class ChunkViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        
        private double _progressWidth;
        public double ProgressWidth 
        { 
            get => _progressWidth; 
            set { _progressWidth = value; PropertyChanged?.Invoke(this, new(nameof(ProgressWidth))); } 
        }
    }

    public class ProgressWidthConverter : Microsoft.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double ratio) return ratio * 19.0;
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
