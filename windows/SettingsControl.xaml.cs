using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using ErmiyaDesktop.Localization;

namespace ErmiyaDesktop
{
    public sealed partial class SettingsControl : UserControl
    {
        public event EventHandler? CloseRequested;
        public event EventHandler? Saved;

        private readonly HttpClient _http = new();
        private AppSettingsModel _currentSettings = new();
        private string _activeQueueId = "main";
        private bool _isInitialized = false;

        public SettingsControl()
        {
            try
            {
                this.InitializeComponent();
                LanguageManager.Instance.LanguageChanged += (s, e) => ApplyLanguage();
                SetInitialLanguageSelection();
                ApplyLanguage();
                LoadSettings();
                NavigateToSection("appearance");
                ThreadsSlider.ValueChanged += (s, e) =>
                {
                    if (ThreadsValueText != null)
                    {
                        ThreadsValueText.Text = ((int)e.NewValue).ToString();
                    }
                };
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsControl ctor error: {ex}");
            }
        }

        private void SetInitialLanguageSelection()
        {
            string lang = LanguageManager.Instance.CurrentLanguage;
            LanguageComboBox.SelectedIndex = lang switch
            {
                "fa" => 1,
                "ku" => 2,
                _ => 0
            };
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                LanguageManager.Instance.SetLanguage(tag);
                if (_currentSettings?.Ui != null)
                {
                    _currentSettings.Ui.Language = tag;
                }
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Content is string themeName)
            {
                ThemeManager.ApplyTheme(themeName);
                if (_currentSettings?.Ui != null)
                {
                    _currentSettings.Ui.Theme = themeName;
                }
            }
        }

        private string _currentSectionTag = "appearance";

        public void ApplyLanguage()
        {
            var lm = LanguageManager.Instance;
            this.FlowDirection = lm.CurrentFlowDirection;

            if (BackToDownloadsText != null) BackToDownloadsText.Text = lm.Get("Common_BackToDownloads", "Back to Downloads");
            if (DiscardBtn != null) DiscardBtn.Content = lm.Get("Common_Discard", "Discard");
            if (SaveBtn != null) SaveBtn.Content = lm.Get("Common_Save", "Save Changes");
            if (BottomDiscardBtn != null) BottomDiscardBtn.Content = lm.Get("Common_Discard", "Discard");
            if (BottomSaveBtn != null) BottomSaveBtn.Content = lm.Get("Common_Save", "Save Changes");

            if (NavAppearanceText != null) NavAppearanceText.Text = lm.Get("Settings_Appearance", "Appearance");
            if (NavEngineText != null) NavEngineText.Text = lm.Get("Settings_Engine", "Download Engine");
            if (NavFileTypesText != null) NavFileTypesText.Text = lm.Get("Settings_FileTypes", "File Types & Save");
            if (NavSpeedText != null) NavSpeedText.Text = lm.Get("Settings_Speed", "Speed Limiter");
            if (NavQueuesText != null) NavQueuesText.Text = lm.Get("Settings_Queues", "Queues & Scheduler");
            if (NavNetworkText != null) NavNetworkText.Text = lm.Get("Settings_Network", "Network & Proxy");
            if (NavHostsText != null) NavHostsText.Text = lm.Get("Settings_Hosts", "Per-Host Rules");
            if (NavBrowserText != null) NavBrowserText.Text = lm.Get("Settings_Browser", "Browser & API");

            if (LanguageLabel != null) LanguageLabel.Text = lm.Get("Settings_Language", "Language");
            if (ThemeLabel != null) ThemeLabel.Text = lm.Get("Settings_Theme", "Theme");
            if (ShowIconLabelsToggle != null) ShowIconLabelsToggle.Header = lm.Get("Settings_ShowIconLabels", "Show Icon Labels");
            if (NotificationSoundsToggle != null) NotificationSoundsToggle.Header = lm.Get("Settings_NotificationSounds", "Notification Sounds");
            if (StartOnBootToggle != null) StartOnBootToggle.Header = lm.Get("Settings_StartOnBoot", "Start on Windows Boot");
            if (MinimizeToTrayToggle != null) MinimizeToTrayToggle.Header = lm.Get("Settings_MinimizeToTray", "Minimize to System Tray");
            if (CloseToTrayToggle != null) CloseToTrayToggle.Header = lm.Get("Settings_CloseToTray", "Close button minimizes to Tray");

            ShowSection(_currentSectionTag);
        }

        public void ShowSection(string tag, string? title = null)
        {
            _currentSectionTag = tag;
            AppearancePanel.Visibility = tag == "appearance" ? Visibility.Visible : Visibility.Collapsed;
            EnginePanel.Visibility = tag == "engine" ? Visibility.Visible : Visibility.Collapsed;
            FileTypesPanel.Visibility = tag == "filetypes" ? Visibility.Visible : Visibility.Collapsed;
            SpeedPanel.Visibility = tag == "speed" ? Visibility.Visible : Visibility.Collapsed;
            QueuesPanel.Visibility = tag == "queues" ? Visibility.Visible : Visibility.Collapsed;
            NetworkPanel.Visibility = tag == "network" ? Visibility.Visible : Visibility.Collapsed;
            HostsPanel.Visibility = tag == "hosts" ? Visibility.Visible : Visibility.Collapsed;
            BrowserPanel.Visibility = tag == "browser" ? Visibility.Visible : Visibility.Collapsed;

            var lm = LanguageManager.Instance;
            SectionTitle.Text = tag switch
            {
                "appearance" => lm.Get("Settings_Appearance", "Appearance"),
                "engine" => lm.Get("Settings_Engine", "Download Engine"),
                "filetypes" => lm.Get("Settings_FileTypes", "File Types & Save"),
                "speed" => lm.Get("Settings_Speed", "Speed Limiter"),
                "queues" => lm.Get("Settings_Queues", "Queues & Scheduler"),
                "network" => lm.Get("Settings_Network", "Network & Proxy"),
                "hosts" => lm.Get("Settings_Hosts", "Per-Host Rules"),
                "browser" => lm.Get("Settings_Browser", "Browser & API"),
                _ => "Settings"
            };
        }

        public void NavigateToSection(string tag)
        {
            foreach (var item in SettingsSidebarList.Items)
            {
                if (item is FrameworkElement elem && elem.Tag?.ToString() == tag)
                {
                    SettingsSidebarList.SelectedItem = elem;
                    ShowSection(tag);
                    return;
                }
            }
            ShowSection(tag);
        }

        public async void LoadSettings()
        {
            try
            {
                SaveNotification.IsOpen = false;

                // 1. Instantly load persistent settings from disk as reliable baseline
                var localSettings = SettingsStorage.LoadFromDisk();
                if (localSettings != null)
                {
                    _currentSettings = localSettings;
                    PopulateUI(_currentSettings);
                }

                // 2. Fetch live settings from background engine if connected
                try
                {
                    var res = await _http.GetAsync($"{FoxLoaderConfig.BaseUrl}/settings");
                    if (res.IsSuccessStatusCode)
                    {
                        var json = await res.Content.ReadAsStringAsync();
                        var remoteSettings = JsonSerializer.Deserialize<AppSettingsModel>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (remoteSettings != null)
                        {
                            _currentSettings = remoteSettings;
                            PopulateUI(_currentSettings);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Core endpoint offline during LoadSettings: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSettings failed: {ex}");
            }
        }

        private void PopulateUI(AppSettingsModel s)
        {
            bool prevInit = _isInitialized;
            _isInitialized = false;

            s.Ui ??= new();
            s.Engine ??= new();
            s.SpeedLimiter ??= new();
            s.Network ??= new();
            s.Queues ??= new();
            s.PerHostSettings ??= new();
            s.Categories ??= new();
            _currentSettings = s;

            // Appearance
            SetThemeSelection(s.Ui?.Theme ?? ThemeManager.CurrentTheme);
            ShowIconLabelsToggle.IsOn = s.Ui?.ShowIconLabels ?? true;
            NotificationSoundsToggle.IsOn = s.Ui?.NotificationSounds ?? true;
            SpeedUnitComboBox.SelectedIndex = (s.Ui?.SpeedUnit == "Bits") ? 1 : 0;
            StartOnBootToggle.IsOn = s.Ui?.StartOnBoot ?? false;
            MinimizeToTrayToggle.IsOn = s.Ui?.MinimizeToTray ?? true;
            CloseToTrayToggle.IsOn = s.Ui?.CloseToTray ?? true;

            // Engine
            DefaultPathBox.Text = s.Engine.DefaultSavePath ?? "";
            ThreadsSlider.Value = s.Engine.DefaultThreads > 0 ? s.Engine.DefaultThreads : 16;
            DynamicPartsToggle.IsOn = s.Engine.DynamicParts;
            IncompleteExtToggle.IsOn = s.Engine.IncompleteExtension;
            LastModifiedToggle.IsOn = s.Engine.UseServerLastModified;
            TrackDeletedToggle.IsOn = s.Engine.TrackDeletedFiles;
            DeleteOnCancelToggle.IsOn = s.Engine.DeleteFileOnCancel;
            RetrySlider.Value = s.Engine.AutoRetryCount;
            IgnoreSslToggle.IsOn = s.Engine.IgnoreSslCertificates;
            UserAgentBox.Text = s.Engine.CustomUserAgent ?? "";

            // File Types & Categories
            PopulateCategoriesUI(s);

            // Speed
            SpeedLimiterToggle.IsOn = s.SpeedLimiter.Enabled;
            SpeedLimitBox.Value = s.SpeedLimiter.GlobalSpeedLimit / 1024.0;

            // Queues
            PopulateQueueFields(_activeQueueId);

            // Network
            ProxyModeComboBox.SelectedIndex = s.Network.ProxyMode switch
            {
                "System" => 1,
                "Manual" => 2,
                _ => 0
            };
            ProxyTypeComboBox.SelectedIndex = s.Network.ProxyType switch
            {
                "HTTPS" => 1,
                "SOCKS5" => 2,
                _ => 0
            };
            ProxyHostBox.Text = s.Network.ProxyHost ?? "";
            ProxyPortBox.Value = s.Network.ProxyPort > 0 ? s.Network.ProxyPort : 8080;

            DnsModeComboBox.SelectedIndex = s.Network.DnsMode == "DoH" ? 1 : 0;
            DohProviderComboBox.SelectedIndex = s.Network.DohProvider switch
            {
                "Google" => 1,
                "Quad9" => 2,
                "Custom" => 3,
                _ => 0
            };

            // Per-Host Rules list
            RefreshHostRulesList();

            // API & Browser Extension
            ApiPortBox.Value = s.Port > 0 ? s.Port : 2764;
            ApiKeyToggle.IsOn = s.ApiKeyEnabled;
            ApiKeyBox.Text = s.ApiKey ?? "";
            ExtensionPathBox.Text = !string.IsNullOrWhiteSpace(s.BrowserExtensionPath) ? s.BrowserExtensionPath : GetExtensionDirectory();

            _isInitialized = prevInit;
        }

        private void PopulateCategoriesUI(AppSettingsModel s)
        {
            var defaultBase = s.Engine.DefaultSavePath ?? "";
            if (string.IsNullOrEmpty(defaultBase)) defaultBase = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "FoxLoader");

            CatCompressedPathBox.Text = s.Categories.FirstOrDefault(c => c.Name == "Compressed")?.Folder ?? System.IO.Path.Combine(defaultBase, "Compressed");
            CatDocumentsPathBox.Text = s.Categories.FirstOrDefault(c => c.Name == "Documents")?.Folder ?? System.IO.Path.Combine(defaultBase, "Documents");
            CatMusicPathBox.Text = s.Categories.FirstOrDefault(c => c.Name == "Music")?.Folder ?? System.IO.Path.Combine(defaultBase, "Music");
            CatProgramsPathBox.Text = s.Categories.FirstOrDefault(c => c.Name == "Programs")?.Folder ?? System.IO.Path.Combine(defaultBase, "Programs");
            CatVideoPathBox.Text = s.Categories.FirstOrDefault(c => c.Name == "Video")?.Folder ?? System.IO.Path.Combine(defaultBase, "Video");

            FileTypesBox.Text = !string.IsNullOrWhiteSpace(s.FileTypes)
                ? s.FileTypes
                : "3GP 7Z AAC ACE AI AIF ARJ ASF AVI BIN BZ2 DMG DOC DOCX EXE GZ GZIP IMG ISO LZH M4A M4V MKV MOV MP3 MP4 MPA MPE MPEG MPG MSI MSU OGG OGV PDF PPT PPTX PSD QT R0* R1* RA RAR RM RMVB SEA SIT SITX TAR TIF TIFF TS TXT WAV WMA WMV XLS XLSX Z ZIP";
            WhitelistDomainsBox.Text = s.WhitelistDomains ?? "";
        }

        private async void BrowseCategoryPath_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string category)
            {
                var folderPicker = new FolderPicker();
                IntPtr hwnd = IntPtr.Zero;
                if (App.MainWindowInstance != null)
                {
                    hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                }
                if (hwnd == IntPtr.Zero)
                {
                    hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                }
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
                folderPicker.SuggestedStartLocation = PickerLocationId.Downloads;
                folderPicker.FileTypeFilter.Add("*");

                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    switch (category)
                    {
                        case "Compressed": CatCompressedPathBox.Text = folder.Path; break;
                        case "Documents": CatDocumentsPathBox.Text = folder.Path; break;
                        case "Music": CatMusicPathBox.Text = folder.Path; break;
                        case "Programs": CatProgramsPathBox.Text = folder.Path; break;
                        case "Video": CatVideoPathBox.Text = folder.Path; break;
                    }
                }
            }
        }

        private void SetThemeSelection(string theme)
        {
            int index = theme switch
            {
                "Dark" => 1,
                "Light" => 2,
                "Dark Teal" => 3,
                "Light Teal" => 4,
                _ => 0
            };

            if (ThemeComboBox.SelectedIndex != index)
            {
                ThemeComboBox.SelectedIndex = index;
            }
        }

        private void SettingsSidebarList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (SettingsSidebarList.SelectedItem is FrameworkElement elem && elem.Tag is string tag)
            {
                ShowSection(tag);
            }
        }

        private async void BrowsePath_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            IntPtr hwnd = IntPtr.Zero;
            if (App.MainWindowInstance != null)
            {
                hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
            }
            if (hwnd == IntPtr.Zero)
            {
                hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            }
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            folderPicker.SuggestedStartLocation = PickerLocationId.Downloads;
            folderPicker.FileTypeFilter.Add("*");

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                DefaultPathBox.Text = folder.Path;
            }
        }

        private void QueueSelectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            SaveCurrentQueueFields();
            _activeQueueId = QueueSelectComboBox.SelectedIndex == 1 ? "night" : "main";
            PopulateQueueFields(_activeQueueId);
        }

        private void PopulateQueueFields(string queueId)
        {
            _currentSettings.Queues ??= new();
            var q = _currentSettings.Queues.FirstOrDefault(x => x.Id == queueId);
            if (q == null)
            {
                q = new QueueConfigModel
                {
                    Id = queueId,
                    Name = queueId == "night" ? "Night Queue" : "Main Queue"
                };
                _currentSettings.Queues.Add(q);
            }

            QueueMaxConcurrentBox.Value = q.MaxConcurrent > 0 ? q.MaxConcurrent : 3;
            QueueStopEmptyToggle.IsOn = q.StopOnEmpty;
            QueueSchedulerToggle.IsOn = q.SchedulerEnabled;

            if (TimeSpan.TryParse(q.StartTime, out var startTime))
            {
                QueueStartTimePicker.Time = startTime;
            }
            else
            {
                QueueStartTimePicker.Time = new TimeSpan(2, 0, 0);
            }

            if (TimeSpan.TryParse(q.StopTime, out var stopTime))
            {
                QueueStopTimePicker.Time = stopTime;
            }
            else
            {
                QueueStopTimePicker.Time = new TimeSpan(7, 0, 0);
            }

            PowerActionComboBox.SelectedIndex = q.PowerActionOnFinish switch
            {
                "Shutdown" => 1,
                "Sleep" => 2,
                "Hibernate" => 3,
                _ => 0
            };
        }

        private void SaveCurrentQueueFields()
        {
            var q = _currentSettings.Queues.FirstOrDefault(x => x.Id == _activeQueueId);
            if (q == null)
            {
                q = new QueueConfigModel { Id = _activeQueueId };
                _currentSettings.Queues.Add(q);
            }

            q.MaxConcurrent = (uint)Math.Max(1, QueueMaxConcurrentBox.Value);
            q.StopOnEmpty = QueueStopEmptyToggle.IsOn;
            q.SchedulerEnabled = QueueSchedulerToggle.IsOn;
            q.StartTime = QueueStartTimePicker.Time.ToString(@"hh\:mm");
            q.StopTime = QueueStopTimePicker.Time.ToString(@"hh\:mm");
            q.PowerActionOnFinish = PowerActionComboBox.SelectedIndex switch
            {
                1 => "Shutdown",
                2 => "Sleep",
                3 => "Hibernate",
                _ => "None"
            };
        }

        private static readonly Dictionary<string, string> ChromiumBrowserKeys = new()
        {
            ["Google Chrome"] = @"Software\Google\Chrome\NativeMessagingHosts\com.ermiya.downloadmanager",
            ["Microsoft Edge"] = @"Software\Microsoft\Edge\NativeMessagingHosts\com.ermiya.downloadmanager",
            ["Brave Browser"] = @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.ermiya.downloadmanager",
            ["Yandex Browser"] = @"Software\Yandex\YandexBrowser\NativeMessagingHosts\com.ermiya.downloadmanager",
            ["Arc Browser"] = @"Software\The Browser Company\Arc\NativeMessagingHosts\com.ermiya.downloadmanager",
            ["Opera Stable"] = @"Software\Opera Software\Opera Stable\NativeMessagingHosts\com.ermiya.downloadmanager",
            ["Opera GX"] = @"Software\Opera Software\Opera GX Stable\NativeMessagingHosts\com.ermiya.downloadmanager",
            ["Vivaldi"] = @"Software\Vivaldi\NativeMessagingHosts\com.ermiya.downloadmanager",
            ["Chromium"] = @"Software\Chromium\NativeMessagingHosts\com.ermiya.downloadmanager"
        };

        private const string FirefoxRegistryKey = @"Software\Mozilla\NativeMessagingHosts\com.ermiya.downloadmanager";

        private void RefreshHostRulesList()
        {
            try
            {
                HostRulesListContainer.Children.Clear();

                if (_currentSettings.PerHostSettings == null || _currentSettings.PerHostSettings.Count == 0)
                {
                    var emptyNotice = new TextBlock
                    {
                        Text = "No host rules configured yet.",
                        FontStyle = Windows.UI.Text.FontStyle.Italic,
                        Margin = new Thickness(0, 4, 0, 4)
                    };
                    HostRulesListContainer.Children.Add(emptyNotice);
                    return;
                }

                foreach (var rule in _currentSettings.PerHostSettings)
                {
                    var card = new Grid
                    {
                        Padding = new Thickness(14, 10, 14, 10),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6)
                    };

                    if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out var bg) && bg is Microsoft.UI.Xaml.Media.Brush bgBrush)
                    {
                        card.Background = bgBrush;
                    }
                    if (Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out var stroke) && stroke is Microsoft.UI.Xaml.Media.Brush strokeBrush)
                    {
                        card.BorderBrush = strokeBrush;
                    }

                    card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    card.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var textStack = new StackPanel { Spacing = 3 };
                    var domainBlock = new TextBlock { Text = rule.Host, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
                    var detailsBlock = new TextBlock
                    {
                        Text = $"User: {(!string.IsNullOrEmpty(rule.Username) ? rule.Username : "(none)")} | Threads: {(rule.Threads.HasValue && rule.Threads > 0 ? rule.Threads.ToString() : "Default")}",
                        FontSize = 12
                    };
                    if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var txtSec) && txtSec is Microsoft.UI.Xaml.Media.Brush txtSecBrush)
                    {
                        detailsBlock.Foreground = txtSecBrush;
                    }

                    textStack.Children.Add(domainBlock);
                    textStack.Children.Add(detailsBlock);
                    Grid.SetColumn(textStack, 0);
                    card.Children.Add(textStack);

                    var actionsStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
                    var editBtn = new Button { Content = "Edit", Tag = rule.Host };
                    editBtn.Click += EditHostRule_Click;
                    var deleteBtn = new Button { Content = "Delete", Tag = rule.Host };
                    deleteBtn.Click += DeleteHostRule_Click;
                    actionsStack.Children.Add(editBtn);
                    actionsStack.Children.Add(deleteBtn);
                    Grid.SetColumn(actionsStack, 1);
                    card.Children.Add(actionsStack);

                    HostRulesListContainer.Children.Add(card);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshHostRulesList error: {ex}");
            }
        }

        private async void SaveHostRule_Click(object sender, RoutedEventArgs e)
        {
            var domain = HostDomainBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(domain))
            {
                SaveNotification.Severity = InfoBarSeverity.Warning;
                SaveNotification.Title = "Missing Host";
                SaveNotification.Message = "Please enter a valid domain name (e.g. luximo.ir).";
                SaveNotification.IsOpen = true;
                return;
            }

            // Normalize domain
            domain = domain.Replace("https://", "").Replace("http://", "").TrimEnd('/');

            var existing = _currentSettings.PerHostSettings.FirstOrDefault(r => r.Host.Equals(domain, StringComparison.OrdinalIgnoreCase));
            string? username = string.IsNullOrWhiteSpace(HostUsernameBox.Text) ? null : HostUsernameBox.Text.Trim();
            string? password = string.IsNullOrWhiteSpace(HostPasswordBox.Password) ? null : HostPasswordBox.Password;
            uint? threads = HostThreadsBox.Value > 0 ? (uint)HostThreadsBox.Value : null;

            if (existing != null)
            {
                existing.Username = username;
                existing.Password = password;
                existing.Threads = threads;
            }
            else
            {
                _currentSettings.PerHostSettings.Add(new PerHostSettingModel
                {
                    Host = domain,
                    Username = username,
                    Password = password,
                    Threads = threads
                });
            }

            // Persist immediately to disk
            SettingsStorage.SaveToDisk(_currentSettings);

            // Sync with backend engine
            await SyncSettingsToCoreAsync();

            RefreshHostRulesList();

            SaveNotification.Severity = InfoBarSeverity.Success;
            SaveNotification.Title = "Host Rule Saved";
            SaveNotification.Message = $"Rule for '{domain}' saved successfully and applied.";
            SaveNotification.IsOpen = true;
        }

        private void EditHostRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string host)
            {
                var rule = _currentSettings.PerHostSettings.FirstOrDefault(r => r.Host.Equals(host, StringComparison.OrdinalIgnoreCase));
                if (rule != null)
                {
                    HostDomainBox.Text = rule.Host;
                    HostUsernameBox.Text = rule.Username ?? "";
                    HostPasswordBox.Password = rule.Password ?? "";
                    HostThreadsBox.Value = rule.Threads ?? 0;
                }
            }
        }

        private async void DeleteHostRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string host)
            {
                _currentSettings.PerHostSettings.RemoveAll(r => r.Host.Equals(host, StringComparison.OrdinalIgnoreCase));
                SettingsStorage.SaveToDisk(_currentSettings);
                await SyncSettingsToCoreAsync();
                RefreshHostRulesList();

                SaveNotification.Severity = InfoBarSeverity.Informational;
                SaveNotification.Title = "Rule Removed";
                SaveNotification.Message = $"Rule for '{host}' removed.";
                SaveNotification.IsOpen = true;
            }
        }

        private void GenerateKey_Click(object sender, RoutedEventArgs e)
        {
            ApiKeyBox.Text = Guid.NewGuid().ToString("N");
        }

        private string GetExtensionDirectory()
        {
            if (!string.IsNullOrWhiteSpace(ExtensionPathBox?.Text) && System.IO.Directory.Exists(ExtensionPathBox.Text.Trim()))
            {
                return ExtensionPathBox.Text.Trim();
            }

            if (!string.IsNullOrWhiteSpace(_currentSettings.BrowserExtensionPath) && System.IO.Directory.Exists(_currentSettings.BrowserExtensionPath))
            {
                return _currentSettings.BrowserExtensionPath;
            }

            var candidates = new[]
            {
                System.IO.Path.Combine(AppContext.BaseDirectory, "extension"),
                @"D:\Project\Ermiya_Download_Manager\extension",
                System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "extension")),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".FoxLoader", "extension")
            };

            foreach (var candidate in candidates)
            {
                if (System.IO.Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return @"D:\Project\Ermiya_Download_Manager\extension";
        }

        private string FindCoreExecutable()
        {
            var candidates = new[]
            {
                System.IO.Path.Combine(AppContext.BaseDirectory, "ermiya-core.exe"),
                @"D:\Project\Ermiya_Download_Manager\ReleaseOutput\ermiya-core.exe",
                @"D:\Project\Ermiya_Download_Manager\target\debug\ermiya-core.exe",
                @"D:\Project\Ermiya_Download_Manager\target\release\ermiya-core.exe",
                System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ReleaseOutput", "ermiya-core.exe")),
                System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "target", "debug", "ermiya-core.exe"))
            };

            foreach (var candidate in candidates)
            {
                if (System.IO.File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return System.IO.Path.Combine(AppContext.BaseDirectory, "ermiya-core.exe");
        }

        private (string chromiumManifest, string firefoxManifest) EnsureNativeHostManifests()
        {
            var FoxLoaderDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".FoxLoader");
            if (!System.IO.Directory.Exists(FoxLoaderDir))
            {
                System.IO.Directory.CreateDirectory(FoxLoaderDir);
            }

            var corePath = FindCoreExecutable();
            var escapedCorePath = corePath.Replace(@"\", @"\\");

            var chromiumManifest = System.IO.Path.Combine(FoxLoaderDir, "native_host_manifest_chromium.json");
            var firefoxManifest = System.IO.Path.Combine(FoxLoaderDir, "native_host_manifest_firefox.json");

            var chromiumJson = "{\n" +
                "  \"name\": \"com.ermiya.downloadmanager\",\n" +
                "  \"description\": \"FoxLoader Native Messaging Host\",\n" +
                $"  \"path\": \"{escapedCorePath}\",\n" +
                "  \"type\": \"stdio\",\n" +
                "  \"allowed_origins\": [\n" +
                "    \"chrome-extension://*/\"\n" +
                "  ]\n" +
                "}\n";

            var firefoxJson = "{\n" +
                "  \"name\": \"com.ermiya.downloadmanager\",\n" +
                "  \"description\": \"FoxLoader Native Messaging Host\",\n" +
                $"  \"path\": \"{escapedCorePath}\",\n" +
                "  \"type\": \"stdio\",\n" +
                "  \"allowed_extensions\": [\n" +
                "    \"ermiya-downloader@ermiya.com\",\n" +
                "    \"{c18c5f0a-47b9-4701-95e8-75841084e4b4}\"\n" +
                "  ]\n" +
                "}\n";

            System.IO.File.WriteAllText(chromiumManifest, chromiumJson);
            System.IO.File.WriteAllText(firefoxManifest, firefoxJson);

            try
            {
                var localManifest = System.IO.Path.Combine(AppContext.BaseDirectory, "native_host_manifest.json");
                System.IO.File.WriteAllText(localManifest, chromiumJson);
            }
            catch { }

            return (chromiumManifest, firefoxManifest);
        }

        private bool RegisterBrowserKey(string registryPath, string manifestPath)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(registryPath);
                if (key != null)
                {
                    key.SetValue("", manifestPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed registering {registryPath}: {ex.Message}");
            }
            return false;
        }

        private void RegisterAllBrowsers_Click(object sender, RoutedEventArgs e)
        {
            var (chromiumManifest, firefoxManifest) = EnsureNativeHostManifests();

            int count = 0;
            foreach (var kvp in ChromiumBrowserKeys)
            {
                if (RegisterBrowserKey(kvp.Value, chromiumManifest))
                {
                    count++;
                }
            }

            if (RegisterBrowserKey(FirefoxRegistryKey, firefoxManifest))
            {
                count++;
            }

            SaveNotification.Severity = InfoBarSeverity.Success;
            SaveNotification.Title = "All Browsers Integrated";
            SaveNotification.Message = $"Native Messaging Host registered for Chrome, Edge, Brave, Firefox, Yandex, Arc, Opera, and Vivaldi! ({count} registry entries written).";
            SaveNotification.IsOpen = true;
        }

        private void RegisterChrome_Click(object sender, RoutedEventArgs e)
        {
            var (chromiumManifest, _) = EnsureNativeHostManifests();
            RegisterSingleBrowser("Google Chrome", ChromiumBrowserKeys["Google Chrome"], chromiumManifest);
        }

        private void RegisterEdge_Click(object sender, RoutedEventArgs e)
        {
            var (chromiumManifest, _) = EnsureNativeHostManifests();
            RegisterSingleBrowser("Microsoft Edge", ChromiumBrowserKeys["Microsoft Edge"], chromiumManifest);
        }

        private void RegisterBrave_Click(object sender, RoutedEventArgs e)
        {
            var (chromiumManifest, _) = EnsureNativeHostManifests();
            RegisterSingleBrowser("Brave Browser", ChromiumBrowserKeys["Brave Browser"], chromiumManifest);
        }

        private void RegisterFirefox_Click(object sender, RoutedEventArgs e)
        {
            var (_, firefoxManifest) = EnsureNativeHostManifests();
            RegisterSingleBrowser("Mozilla Firefox", FirefoxRegistryKey, firefoxManifest);
        }

        private void RegisterYandex_Click(object sender, RoutedEventArgs e)
        {
            var (chromiumManifest, _) = EnsureNativeHostManifests();
            RegisterSingleBrowser("Yandex Browser", ChromiumBrowserKeys["Yandex Browser"], chromiumManifest);
        }

        private void RegisterArc_Click(object sender, RoutedEventArgs e)
        {
            var (chromiumManifest, _) = EnsureNativeHostManifests();
            RegisterSingleBrowser("Arc Browser", ChromiumBrowserKeys["Arc Browser"], chromiumManifest);
        }

        private void RegisterOperaVivaldi_Click(object sender, RoutedEventArgs e)
        {
            var (chromiumManifest, _) = EnsureNativeHostManifests();
            bool op1 = RegisterBrowserKey(ChromiumBrowserKeys["Opera Stable"], chromiumManifest);
            bool op2 = RegisterBrowserKey(ChromiumBrowserKeys["Opera GX"], chromiumManifest);
            bool viv = RegisterBrowserKey(ChromiumBrowserKeys["Vivaldi"], chromiumManifest);

            SaveNotification.Severity = (op1 || op2 || viv) ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            SaveNotification.Title = "Opera & Vivaldi Integrated";
            SaveNotification.Message = "Native Messaging keys registered for Opera Stable, Opera GX, and Vivaldi.";
            SaveNotification.IsOpen = true;
        }

        private void RegisterSingleBrowser(string browserName, string subKey, string manifestPath)
        {
            if (RegisterBrowserKey(subKey, manifestPath))
            {
                SaveNotification.Severity = InfoBarSeverity.Success;
                SaveNotification.Title = $"{browserName} Integrated";
                SaveNotification.Message = $"Native Messaging Host registered successfully for {browserName}.";
            }
            else
            {
                SaveNotification.Severity = InfoBarSeverity.Error;
                SaveNotification.Title = "Integration Failed";
                SaveNotification.Message = $"Could not write registry key for {browserName}.";
            }
            SaveNotification.IsOpen = true;
        }

        private async void BrowseExtensionPath_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();
            IntPtr hwnd = IntPtr.Zero;
            if (App.MainWindowInstance != null)
            {
                hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
            }
            if (hwnd == IntPtr.Zero)
            {
                hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            }
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            folderPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            folderPicker.FileTypeFilter.Add("*");

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                ExtensionPathBox.Text = folder.Path;
                _currentSettings.BrowserExtensionPath = folder.Path;
                SettingsStorage.SaveToDisk(_currentSettings);
            }
        }

        private void OpenExtensionFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = GetExtensionDirectory();
            if (System.IO.Directory.Exists(folder))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{folder}\"",
                    UseShellExecute = true
                });

                SaveNotification.Severity = InfoBarSeverity.Informational;
                SaveNotification.Title = "Extension Directory Opened";
                SaveNotification.Message = "In your browser's Extensions page, enable 'Developer Mode' and click 'Load Unpacked' to select this directory.";
                SaveNotification.IsOpen = true;
            }
            else
            {
                SaveNotification.Severity = InfoBarSeverity.Warning;
                SaveNotification.Title = "Directory Not Found";
                SaveNotification.Message = $"Could not locate extension directory: {folder}";
                SaveNotification.IsOpen = true;
            }
        }

        private void OpenChromeExtensions_Click(object sender, RoutedEventArgs e)
        {
            LaunchBrowserUrl("chrome.exe", "chrome://extensions");
        }

        private void OpenEdgeExtensions_Click(object sender, RoutedEventArgs e)
        {
            LaunchBrowserUrl("msedge.exe", "edge://extensions");
        }

        private void LaunchBrowserUrl(string browserExe, string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = browserExe,
                    Arguments = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    SaveNotification.Severity = InfoBarSeverity.Warning;
                    SaveNotification.Title = "Launch Browser";
                    SaveNotification.Message = $"Could not launch browser: {ex.Message}";
                    SaveNotification.IsOpen = true;
                }
            }
        }

        private async void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentQueueFields();

            // Collect UI preferences
            string selectedTheme = (ThemeComboBox.SelectedItem is ComboBoxItem cbi && cbi.Content is string t) ? t : "System Settings";
            ThemeManager.ApplyTheme(selectedTheme);

            _currentSettings.Ui.Theme = selectedTheme;
            _currentSettings.Ui.ShowIconLabels = ShowIconLabelsToggle.IsOn;
            _currentSettings.Ui.NotificationSounds = NotificationSoundsToggle.IsOn;
            _currentSettings.Ui.SpeedUnit = SpeedUnitComboBox.SelectedIndex == 1 ? "Bits" : "Bytes";
            _currentSettings.Ui.StartOnBoot = StartOnBootToggle.IsOn;
            _currentSettings.Ui.MinimizeToTray = MinimizeToTrayToggle.IsOn;
            _currentSettings.Ui.CloseToTray = CloseToTrayToggle.IsOn;
            _currentSettings.Ui.Language = LanguageManager.Instance.CurrentLanguage;

            // Engine
            _currentSettings.Engine.DefaultSavePath = DefaultPathBox.Text?.Trim() ?? "";
            _currentSettings.Engine.DefaultThreads = (uint)Math.Max(1, ThreadsSlider.Value);
            _currentSettings.Engine.DynamicParts = DynamicPartsToggle.IsOn;
            _currentSettings.Engine.IncompleteExtension = IncompleteExtToggle.IsOn;
            _currentSettings.Engine.UseServerLastModified = LastModifiedToggle.IsOn;
            _currentSettings.Engine.TrackDeletedFiles = TrackDeletedToggle.IsOn;
            _currentSettings.Engine.DeleteFileOnCancel = DeleteOnCancelToggle.IsOn;
            _currentSettings.Engine.AutoRetryCount = (uint)RetrySlider.Value;
            _currentSettings.Engine.IgnoreSslCertificates = IgnoreSslToggle.IsOn;
            _currentSettings.Engine.CustomUserAgent = UserAgentBox.Text?.Trim() ?? "";

            // Categories & File Types
            UpdateCategoryFolder("Compressed", CatCompressedPathBox.Text?.Trim());
            UpdateCategoryFolder("Documents", CatDocumentsPathBox.Text?.Trim());
            UpdateCategoryFolder("Music", CatMusicPathBox.Text?.Trim());
            UpdateCategoryFolder("Programs", CatProgramsPathBox.Text?.Trim());
            UpdateCategoryFolder("Video", CatVideoPathBox.Text?.Trim());
            _currentSettings.FileTypes = FileTypesBox.Text?.Trim() ?? "";
            _currentSettings.WhitelistDomains = WhitelistDomainsBox.Text?.Trim() ?? "";

            // Speed
            _currentSettings.SpeedLimiter.Enabled = SpeedLimiterToggle.IsOn;
            _currentSettings.SpeedLimiter.GlobalSpeedLimit = (ulong)(SpeedLimitBox.Value * 1024.0);

            // Network
            _currentSettings.Network.ProxyMode = ProxyModeComboBox.SelectedIndex switch
            {
                1 => "System",
                2 => "Manual",
                _ => "Direct"
            };
            _currentSettings.Network.ProxyType = ProxyTypeComboBox.SelectedIndex switch
            {
                1 => "HTTPS",
                2 => "SOCKS5",
                _ => "HTTP"
            };
            _currentSettings.Network.ProxyHost = ProxyHostBox.Text?.Trim() ?? "";
            _currentSettings.Network.ProxyPort = (ushort)ProxyPortBox.Value;
            _currentSettings.Network.DnsMode = DnsModeComboBox.SelectedIndex == 1 ? "DoH" : "System";
            _currentSettings.Network.DohProvider = DohProviderComboBox.SelectedIndex switch
            {
                1 => "Google",
                2 => "Quad9",
                3 => "Custom",
                _ => "Cloudflare"
            };

            // API & Extension
            ushort newPort = (ushort)ApiPortBox.Value;
            bool portChanged = (newPort != FoxLoaderConfig.Port);
            _currentSettings.Port = newPort;
            _currentSettings.ApiKeyEnabled = ApiKeyToggle.IsOn;
            _currentSettings.ApiKey = ApiKeyBox.Text?.Trim() ?? "";
            _currentSettings.BrowserExtensionPath = ExtensionPathBox.Text?.Trim() ?? "";

            // 1. Save directly to disk (guarantees persistence across dotnet run restarts)
            SettingsStorage.SaveToDisk(_currentSettings);

            // 2. Synchronize with live core daemon
            bool syncOk = await SyncSettingsToCoreAsync();

            if (portChanged)
            {
                FoxLoaderConfig.Port = newPort;
                SaveNotification.Severity = InfoBarSeverity.Warning;
                SaveNotification.Title = "Port Changed";
                SaveNotification.Message = $"Settings saved. Port updated to {newPort}. Restart core/desktop for the new port to rebind.";
            }
            else
            {
                SaveNotification.Severity = InfoBarSeverity.Success;
                SaveNotification.Title = "Saved Successfully";
                SaveNotification.Message = syncOk
                    ? "All settings saved and applied to download engine."
                    : "Settings saved to disk. (Download core will load them upon start).";
            }

            SaveNotification.IsOpen = true;
            Saved?.Invoke(this, EventArgs.Empty);
        }

        private async System.Threading.Tasks.Task<bool> SyncSettingsToCoreAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_currentSettings);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await _http.PostAsync($"{FoxLoaderConfig.BaseUrl}/settings", content);
                return res.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateCategoryFolder(string name, string? folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return;
            var cat = _currentSettings.Categories.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (cat != null)
            {
                cat.Folder = folder;
            }
            else
            {
                _currentSettings.Categories.Add(new CategoryConfigModel
                {
                    Name = name,
                    Folder = folder
                });
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public class HostRuleDisplayItem
    {
        public string Host { get; set; } = "";
        public string Details { get; set; } = "";
    }
}
