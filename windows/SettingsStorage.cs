using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ErmiyaDesktop
{
    public static class SettingsStorage
    {
        public static string ResolveSettingsFilePath()
        {
            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, "settings.json"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "settings.json")),
                @"D:\Project\Ermiya_Download_Manager\settings.json",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".FoxLoader", "settings.json")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            // Default save destination
            var projectRoot = @"D:\Project\Ermiya_Download_Manager";
            if (Directory.Exists(projectRoot))
            {
                return Path.Combine(projectRoot, "settings.json");
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".FoxLoader", "settings.json");
        }

        public static AppSettingsModel LoadFromDisk()
        {
            try
            {
                var filePath = ResolveSettingsFilePath();
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var settings = JsonSerializer.Deserialize<AppSettingsModel>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    });
                    if (settings != null)
                    {
                        settings.Ui ??= new();
                        settings.Engine ??= new();
                        settings.SpeedLimiter ??= new();
                        settings.Network ??= new();
                        settings.Queues ??= new();
                        settings.PerHostSettings ??= new();
                        settings.Categories ??= new();
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings from disk: {ex.Message}");
            }

            return new AppSettingsModel();
        }

        public static void SaveToDisk(AppSettingsModel settings)
        {
            if (settings == null) return;

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(settings, options);

                // 1. Primary target file
                var primaryPath = ResolveSettingsFilePath();
                var primaryDir = Path.GetDirectoryName(primaryPath);
                if (!string.IsNullOrEmpty(primaryDir) && !Directory.Exists(primaryDir))
                {
                    Directory.CreateDirectory(primaryDir);
                }
                File.WriteAllText(primaryPath, json);

                // 2. Also keep a synced backup in ~/.FoxLoader/settings.json
                var userFoxLoaderDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".FoxLoader");
                Directory.CreateDirectory(userFoxLoaderDir);
                File.WriteAllText(Path.Combine(userFoxLoaderDir, "settings.json"), json);

                // 3. If running inside project tree, sync to project root as well
                var projectRootFile = @"D:\Project\Ermiya_Download_Manager\settings.json";
                if (Directory.Exists(@"D:\Project\Ermiya_Download_Manager") && primaryPath != projectRootFile)
                {
                    File.WriteAllText(projectRootFile, json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to persist settings to disk: {ex.Message}");
            }
        }
    }

    public class AppSettingsModel
    {
        [JsonPropertyName("engine")]
        public EngineSettingsModel Engine { get; set; } = new();

        [JsonPropertyName("speed_limiter")]
        public SpeedLimiterModel SpeedLimiter { get; set; } = new();

        [JsonPropertyName("network")]
        public NetworkModel Network { get; set; } = new();

        [JsonPropertyName("per_host_settings")]
        public List<PerHostSettingModel> PerHostSettings { get; set; } = new();

        [JsonPropertyName("queues")]
        public List<QueueConfigModel> Queues { get; set; } = new()
        {
            new QueueConfigModel { Id = "main", Name = "Main Queue", MaxConcurrent = 3, StopOnEmpty = true },
            new QueueConfigModel { Id = "night", Name = "Night Queue", MaxConcurrent = 2, StopOnEmpty = true, SchedulerEnabled = true, StartTime = "02:00", StopTime = "07:00", PowerActionOnFinish = "Sleep" }
        };

        [JsonPropertyName("categories")]
        public List<CategoryConfigModel> Categories { get; set; } = new()
        {
            new CategoryConfigModel { Name = "Compressed", Folder = "Compressed", Extensions = new() { "zip", "rar", "7z", "tar", "gz", "iso" } },
            new CategoryConfigModel { Name = "Documents", Folder = "Documents", Extensions = new() { "doc", "docx", "pdf", "txt", "xlsx", "pptx" } },
            new CategoryConfigModel { Name = "Music", Folder = "Music", Extensions = new() { "mp3", "wav", "flac", "aac", "ogg", "m4a" } },
            new CategoryConfigModel { Name = "Programs", Folder = "Programs", Extensions = new() { "exe", "msi", "bat", "apk", "deb", "bin" } },
            new CategoryConfigModel { Name = "Video", Folder = "Video", Extensions = new() { "mp4", "mkv", "avi", "mov", "webm", "flv" } }
        };

        [JsonPropertyName("port")]
        public ushort Port { get; set; } = 2764;

        [JsonPropertyName("api_key_enabled")]
        public bool ApiKeyEnabled { get; set; }

        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; } = "";

        [JsonPropertyName("browser_extension_path")]
        public string BrowserExtensionPath { get; set; } = "";

        [JsonPropertyName("file_types")]
        public string FileTypes { get; set; } = "3GP 7Z AAC ACE AI AIF ARJ ASF AVI BIN BZ2 DMG DOC DOCX EXE GZ GZIP IMG ISO LZH M4A M4V MKV MOV MP3 MP4 MPA MPE MPEG MPG MSI MSU OGG OGV PDF PPT PPTX PSD QT R0* R1* RA RAR RM RMVB SEA SIT SITX TAR TIF TIFF TS TXT WAV WMA WMV XLS XLSX Z ZIP";

        [JsonPropertyName("whitelist_domains")]
        public string WhitelistDomains { get; set; } = "";

        [JsonPropertyName("ui")]
        public UiSettingsModel Ui { get; set; } = new();
    }

    public class EngineSettingsModel
    {
        [JsonPropertyName("default_save_path")]
        public string DefaultSavePath { get; set; } = "";

        [JsonPropertyName("incomplete_extension")]
        public bool IncompleteExtension { get; set; } = true;

        [JsonPropertyName("default_threads")]
        public uint DefaultThreads { get; set; } = 16;

        [JsonPropertyName("dynamic_parts")]
        public bool DynamicParts { get; set; } = true;

        [JsonPropertyName("min_part_size")]
        public ulong MinPartSize { get; set; } = 102400;

        [JsonPropertyName("use_server_last_modified")]
        public bool UseServerLastModified { get; set; } = true;

        [JsonPropertyName("auto_retry_count")]
        public uint AutoRetryCount { get; set; } = 3;

        [JsonPropertyName("auto_retry_interval_secs")]
        public ulong AutoRetryIntervalSecs { get; set; } = 2;

        [JsonPropertyName("ignore_ssl_certificates")]
        public bool IgnoreSslCertificates { get; set; }

        [JsonPropertyName("custom_user_agent")]
        public string CustomUserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 FoxLoader/1.0";

        [JsonPropertyName("track_deleted_files")]
        public bool TrackDeletedFiles { get; set; } = true;

        [JsonPropertyName("delete_file_on_cancel")]
        public bool DeleteFileOnCancel { get; set; }
    }

    public class SpeedLimiterModel
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("global_speed_limit")]
        public ulong GlobalSpeedLimit { get; set; }
    }

    public class NetworkModel
    {
        [JsonPropertyName("proxy_mode")]
        public string ProxyMode { get; set; } = "Direct";

        [JsonPropertyName("proxy_type")]
        public string ProxyType { get; set; } = "HTTP";

        [JsonPropertyName("proxy_host")]
        public string ProxyHost { get; set; } = "";

        [JsonPropertyName("proxy_port")]
        public ushort ProxyPort { get; set; } = 8080;

        [JsonPropertyName("proxy_username")]
        public string? ProxyUsername { get; set; }

        [JsonPropertyName("proxy_password")]
        public string? ProxyPassword { get; set; }

        [JsonPropertyName("proxy_pac_url")]
        public string? ProxyPacUrl { get; set; }

        [JsonPropertyName("dns_mode")]
        public string DnsMode { get; set; } = "System";

        [JsonPropertyName("doh_provider")]
        public string DohProvider { get; set; } = "Cloudflare";

        [JsonPropertyName("doh_url")]
        public string? DohUrl { get; set; }
    }

    public class PerHostSettingModel
    {
        [JsonPropertyName("host")]
        public string Host { get; set; } = "";

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("password")]
        public string? Password { get; set; }

        [JsonPropertyName("user_agent")]
        public string? UserAgent { get; set; }

        [JsonPropertyName("threads")]
        public uint? Threads { get; set; }

        [JsonPropertyName("speed_limit")]
        public ulong? SpeedLimit { get; set; }
    }

    public class QueueConfigModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("max_concurrent")]
        public uint MaxConcurrent { get; set; } = 3;

        [JsonPropertyName("stop_on_empty")]
        public bool StopOnEmpty { get; set; } = true;

        [JsonPropertyName("scheduler_enabled")]
        public bool SchedulerEnabled { get; set; }

        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonPropertyName("stop_time")]
        public string? StopTime { get; set; }

        [JsonPropertyName("active_days")]
        public List<byte> ActiveDays { get; set; } = new() { 1, 2, 3, 4, 5, 6, 7 };

        [JsonPropertyName("power_action_on_finish")]
        public string PowerActionOnFinish { get; set; } = "None";
    }

    public class CategoryConfigModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("folder")]
        public string Folder { get; set; } = "";

        [JsonPropertyName("extensions")]
        public List<string> Extensions { get; set; } = new();
    }

    public class UiSettingsModel
    {
        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "System Settings";

        [JsonPropertyName("show_icon_labels")]
        public bool ShowIconLabels { get; set; } = true;

        [JsonPropertyName("notification_sounds")]
        public bool NotificationSounds { get; set; } = true;

        [JsonPropertyName("speed_unit")]
        public string SpeedUnit { get; set; } = "Bytes";

        [JsonPropertyName("start_on_boot")]
        public bool StartOnBoot { get; set; }

        [JsonPropertyName("minimize_to_tray")]
        public bool MinimizeToTray { get; set; } = true;

        [JsonPropertyName("close_to_tray")]
        public bool CloseToTray { get; set; } = true;

        [JsonPropertyName("language")]
        public string Language { get; set; } = "en";
    }
}
