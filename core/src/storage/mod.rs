use std::path::{Path, PathBuf};
use tokio::fs::{File, OpenOptions};

pub struct TransactionalFileSaver {
    base_dir: PathBuf,
}

impl TransactionalFileSaver {
    pub fn new(base_dir: PathBuf) -> Self {
        Self { base_dir }
    }

    /// Neutralize path traversal sequences and illegal filesystem tokens
    pub fn sanitize_filename(name: &str) -> String {
        let trimmed = name.trim();
        let basename = Path::new(trimmed)
            .file_name()
            .and_then(|s| s.to_str())
            .unwrap_or("downloaded_file");

        let mut clean: String = basename
            .chars()
            .map(|c| match c {
                '<' | '>' | ':' | '"' | '/' | '\\' | '|' | '?' | '*' => '_',
                c if c.is_control() => '_',
                c => c,
            })
            .collect();

        // Prevent Windows reserved device names from locking filesystem handles
        let upper = clean.to_uppercase();
        let root_stem = upper.split('.').next().unwrap_or("");
        let reserved = [
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        ];
        if reserved.contains(&root_stem) {
            clean = format!("_{}", clean);
        }

        if clean.is_empty() || clean == "." || clean == ".." {
            clean = "downloaded_file".to_string();
        }

        clean
    }

    pub fn get_temp_path(&self, file_name: &str) -> PathBuf {
        let clean = Self::sanitize_filename(file_name);
        self.base_dir.join(format!("{}.ermiya", clean))
    }

    pub fn get_final_path(&self, file_name: &str) -> PathBuf {
        let clean = Self::sanitize_filename(file_name);
        self.base_dir.join(clean)
    }

    pub async fn create_sparse_file(&self, file_name: &str, size: u64) -> std::io::Result<File> {
        tokio::fs::create_dir_all(&self.base_dir).await?;
        let path = self.get_temp_path(file_name);
        
        let file = OpenOptions::new()
            .read(true)
            .write(true)
            .create(true)
            .truncate(false)
            .open(&path).await?;
        
        // Sparse preallocation on filesystems that support set_len
        if size > 0 {
            let metadata = file.metadata().await?;
            if metadata.len() < size {
                file.set_len(size).await?;
            }
        }
        Ok(file)
    }

    pub async fn commit_file(&self, file_name: &str, last_modified_rfc2822: Option<&str>) -> std::io::Result<()> {
        let temp_path = self.get_temp_path(file_name);
        let final_path = self.get_final_path(file_name);

        if temp_path.exists() {
            // Atomic commit from .ermiya scratch file to target filename
            tokio::fs::rename(&temp_path, &final_path).await?;
            
            // Sync server last modified timestamp to local filesystem
            if let Some(lm_str) = last_modified_rfc2822 {
                if let Ok(system_time) = httpdate::parse_http_date(lm_str) {
                    let file_time = filetime::FileTime::from_system_time(system_time);
                    let _ = filetime::set_file_times(&final_path, file_time, file_time);
                }
            }
        }
        Ok(())
    }

    pub async fn delete_file(&self, file_name: &str) -> std::io::Result<()> {
        let temp_path = self.get_temp_path(file_name);
        let final_path = self.get_final_path(file_name);

        if temp_path.exists() {
            let _ = tokio::fs::remove_file(temp_path).await;
        }
        if final_path.exists() {
            let _ = tokio::fs::remove_file(final_path).await;
        }
        Ok(())
    }
}
