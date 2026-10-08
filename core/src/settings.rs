use serde::{Deserialize, Serialize};
use tokio::sync::RwLock;
use std::sync::Arc;

#[derive(Serialize, Deserialize, Clone, Debug)]
pub struct DownloadEngineSettings {
    pub default_save_path: String,
    pub incomplete_extension: bool, // Appends .ermiya
    pub default_threads: u32,       // 1 to 32
    pub dynamic_parts: bool,
    pub min_part_size: u64,
    pub use_server_last_modified: bool,
    pub auto_retry_count: u32,
    pub auto_retry_interval_secs: u64,
    pub ignore_ssl_certificates: bool,
    pub custom_user_agent: String,
    pub track_deleted_files: bool,
    pub delete_file_on_cancel: bool,
}

impl Default for DownloadEngineSettings {
    fn default() -> Self {
        let default_path = match dirs_next_or_default() {
            Some(d) => format!("{}\\FoxLoader", d),
            None => "downloads\\FoxLoader".to_string(),
        };

        Self {
            default_save_path: default_path,
            incomplete_extension: true,
            default_threads: 16,
            dynamic_parts: true,
            min_part_size: 102400, // 100 KB
            use_server_last_modified: true,
            auto_retry_count: 3,
            auto_retry_interval_secs: 2,
            ignore_ssl_certificates: false,
            custom_user_agent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 FoxLoader/1.0".to_string(),
            track_deleted_files: true,
            delete_file_on_cancel: false,
        }
    }
}

fn dirs_next_or_default() -> Option<String> {
    std::env::var("USERPROFILE").ok().map(|p| format!("{}\\Downloads", p))
}

#[derive(Serialize, Deserialize, Clone, Debug)]
pub struct SpeedLimiterSettings {
    pub enabled: bool,
    pub global_speed_limit: u64, // bytes per second, 0 = unlimited
}

impl Default for SpeedLimiterSettings {
    fn default() -> Self {
        Self {
            enabled: false,
            global_speed_limit: 0,
        }
    }
}

#[derive(Serialize, Deserialize, Clone, Debug)]
pub struct NetworkSettings {
    pub proxy_mode: String, // "Direct", "System", "Manual", "PAC"
    pub proxy_type: String, // "HTTP", "HTTPS", "SOCKS5"
    pub proxy_host: String,
    pub proxy_port: u16,
    pub proxy_username: Option<String>,
    pub proxy_password: Option<String>,
    pub proxy_pac_url: Option<String>,
    pub dns_mode: String,   // "System", "DoH"
    pub doh_provider: String, // "Cloudflare", "Google", "Quad9", "Custom"
    pub doh_url: Option<String>,
}

impl Default for NetworkSettings {
    fn default() -> Self {
        Self {
            proxy_mode: "Direct".to_string(),
            proxy_type: "HTTP".to_string(),
            proxy_host: String::new(),
            proxy_port: 8080,
            proxy_username: None,
            proxy_password: None,
            proxy_pac_url: None,
            dns_mode: "System".to_string(),
            doh_provider: "Cloudflare".to_string(),
            doh_url: Some("https://cloudflare-dns.com/dns-query".to_string()),
        }
    }
}

#[derive(Serialize, Deserialize, Clone, Debug)]
pub struct PerHostSetting {
    pub host: String,
    pub username: Option<String>,
    pub password: Option<String>,
    pub user_agent: Option<String>,
    pub threads: Option<u32>,
    pub speed_limit: Option<u64>,
}

#[derive(Serialize, Deserialize, Clone, Debug)]
pub struct QueueConfig {
    pub id: String,
    pub name: String,
    pub max_concurrent: u32,
    pub stop_on_empty: bool,
    pub scheduler_enabled: bool,
    pub start_time: Option<String>, // "HH:mm"
    pub stop_time: Option<String>,  // "HH:mm"
    pub active_days: Vec<u8>,       // 1 (Mon) .. 7 (Sun)
    pub power_action_on_finish: String, // "None", "Shutdown", "Sleep", "Hibernate"
}

#[derive(Serialize, Deserialize, Clone, Debug)]
pub struct CategoryConfig {
    pub name: String,
    pub folder: String,
    pub extensions: Vec<String>,
}

#[derive(Serialize, Deserialize, Clone, Debug)]
#[serde(default)]
pub struct UiSettings {
    pub theme: String,
    pub show_icon_labels: bool,
    pub notification_sounds: bool,
    pub speed_unit: String,
    pub start_on_boot: bool,
    pub minimize_to_tray: bool,
    pub close_to_tray: bool,
    pub language: String,
}

impl Default for UiSettings {
    fn default() -> Self {
        Self {
            theme: "System Settings".to_string(),
            show_icon_labels: true,
            notification_sounds: true,
            speed_unit: "Bytes".to_string(),
            start_on_boot: false,
            minimize_to_tray: true,
            close_to_tray: true,
            language: "en".to_string(),
        }
    }
}

#[derive(Serialize, Deserialize, Clone, Debug)]
#[serde(default)]
pub struct AppSettings {
    pub engine: DownloadEngineSettings,
    pub speed_limiter: SpeedLimiterSettings,
    pub network: NetworkSettings,
    pub per_host_settings: Vec<PerHostSetting>,
    pub queues: Vec<QueueConfig>,
    pub categories: Vec<CategoryConfig>,
    pub api_key_enabled: bool,
    pub api_key: String,
    pub browser_extension_path: String,
    pub port: u16,
    pub file_types: String,
    pub whitelist_domains: String,
    pub ui: UiSettings,
}

impl Default for AppSettings {
    fn default() -> Self {
        Self {
            engine: DownloadEngineSettings::default(),
            speed_limiter: SpeedLimiterSettings::default(),
            network: NetworkSettings::default(),
            per_host_settings: Vec::new(),
            queues: vec![
                QueueConfig {
                    id: "main".to_string(),
                    name: "Main Queue".to_string(),
                    max_concurrent: 3,
                    stop_on_empty: true,
                    scheduler_enabled: false,
                    start_time: None,
                    stop_time: None,
                    active_days: vec![1, 2, 3, 4, 5, 6, 7],
                    power_action_on_finish: "None".to_string(),
                },
                QueueConfig {
                    id: "night".to_string(),
                    name: "Night Queue".to_string(),
                    max_concurrent: 2,
                    stop_on_empty: true,
                    scheduler_enabled: true,
                    start_time: Some("02:00".to_string()),
                    stop_time: Some("07:00".to_string()),
                    active_days: vec![1, 2, 3, 4, 5, 6, 7],
                    power_action_on_finish: "Sleep".to_string(),
                },
            ],
            categories: vec![
                CategoryConfig {
                    name: "Compressed".to_string(),
                    folder: "Compressed".to_string(),
                    extensions: vec!["zip".into(), "rar".into(), "7z".into(), "tar".into(), "gz".into(), "iso".into()],
                },
                CategoryConfig {
                    name: "Documents".to_string(),
                    folder: "Documents".to_string(),
                    extensions: vec!["doc".into(), "docx".into(), "pdf".into(), "txt".into(), "xlsx".into(), "pptx".into()],
                },
                CategoryConfig {
                    name: "Music".to_string(),
                    folder: "Music".to_string(),
                    extensions: vec!["mp3".into(), "wav".into(), "flac".into(), "aac".into(), "ogg".into(), "m4a".into()],
                },
                CategoryConfig {
                    name: "Programs".to_string(),
                    folder: "Programs".to_string(),
                    extensions: vec!["exe".into(), "msi".into(), "bat".into(), "apk".into(), "deb".into(), "bin".into()],
                },
                CategoryConfig {
                    name: "Video".to_string(),
                    folder: "Video".to_string(),
                    extensions: vec!["mp4".into(), "mkv".into(), "avi".into(), "mov".into(), "webm".into(), "flv".into()],
                },
            ],
            api_key_enabled: false,
            api_key: String::new(),
            browser_extension_path: String::new(),
            port: 2764,
            file_types: "3GP 7Z AAC ACE AI AIF ARJ ASF AVI BIN BZ2 DMG DOC DOCX EXE GZ GZIP IMG ISO LZH M4A M4V MKV MOV MP3 MP4 MPA MPE MPEG MPG MSI MSU OGG OGV PDF PPT PPTX PSD QT R0* R1* RA RAR RM RMVB SEA SIT SITX TAR TIF TIFF TS TXT WAV WMA WMV XLS XLSX Z ZIP".to_string(),
            whitelist_domains: String::new(),
            ui: UiSettings::default(),
        }
    }
}

pub fn resolve_config_path(filename: &str) -> std::path::PathBuf {
    let p = std::path::Path::new(filename);
    if p.exists() {
        return p.to_path_buf();
    }
    // Check executable directory
    if let Ok(exe) = std::env::current_exe() {
        if let Some(dir) = exe.parent() {
            let candidate = dir.join(filename);
            if candidate.exists() {
                return candidate;
            }
        }
    }
    // Check parent directory
    if let Ok(cwd) = std::env::current_dir() {
        let candidate = cwd.join("..").join(filename);
        if candidate.exists() {
            return candidate;
        }
    }
    // Check user profile directory
    if let Ok(userprofile) = std::env::var("USERPROFILE") {
        let candidate = std::path::Path::new(&userprofile).join(".FoxLoader").join(filename);
        if candidate.exists() {
            return candidate;
        }
    }
    std::path::PathBuf::from(filename)
}

pub struct SettingsManager {
    file_path: String,
    pub settings: Arc<RwLock<AppSettings>>,
}

impl SettingsManager {
    pub fn new(path: &str) -> Self {
        let resolved = resolve_config_path(path);
        let loaded = if resolved.exists() {
            if let Ok(content) = std::fs::read_to_string(&resolved) {
                serde_json::from_str::<AppSettings>(&content).unwrap_or_default()
            } else {
                AppSettings::default()
            }
        } else {
            let default_settings = AppSettings::default();
            if let Ok(json) = serde_json::to_string_pretty(&default_settings) {
                let _ = std::fs::write(&resolved, json);
            }
            default_settings
        };

        Self {
            file_path: resolved.to_string_lossy().to_string(),
            settings: Arc::new(RwLock::new(loaded)),
        }
    }

    pub async fn get(&self) -> AppSettings {
        self.settings.read().await.clone()
    }

    pub async fn update(&self, new_settings: AppSettings) -> Result<(), String> {
        let mut guard = self.settings.write().await;
        *guard = new_settings.clone();
        drop(guard);

        let json = serde_json::to_string_pretty(&new_settings).map_err(|e| e.to_string())?;
        std::fs::write(&self.file_path, json).map_err(|e| e.to_string())?;
        Ok(())
    }
}
