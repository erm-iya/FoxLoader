use reqwest::{Client, Method, header};
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, Ordering};
use tokio::sync::{RwLock, Mutex};
use crate::storage::TransactionalFileSaver;
use tokio::io::{AsyncSeekExt, AsyncWriteExt, SeekFrom};
use crate::settings::DownloadEngineSettings;

#[derive(Clone, Debug)]
pub struct RangedPart {
    pub from: u64,
    pub to: Option<u64>,
    pub current: u64,
    pub is_blind: bool,
    pub is_completed: bool,
    pub is_active: bool,
}

#[derive(Clone)]
pub struct HttpDownloadJob {
    pub id: u64,
    pub url: String,
    pub file_name: String,
    pub save_path: String,
    pub queue_name: String,
    pub parts: Arc<RwLock<Vec<RangedPart>>>,
    pub total_size: Option<u64>,
    pub etag: Option<String>,
    pub last_modified: Option<String>,
    pub supports_concurrent: bool,
    pub status_text: Arc<RwLock<String>>,
    pub is_paused: Arc<AtomicBool>,
    pub speed_limit: u64, // 0 = unlimited
    client: Client,
    engine_settings: DownloadEngineSettings,
}

impl HttpDownloadJob {
    pub fn new(
        id: u64,
        url: String,
        file_name: String,
        save_path: String,
        queue_name: String,
        settings: DownloadEngineSettings,
        speed_limit: u64,
    ) -> Self {
        let mut builder = Client::builder();
        if !settings.custom_user_agent.is_empty() {
            builder = builder.user_agent(&settings.custom_user_agent);
        }
        if settings.ignore_ssl_certificates {
            builder = builder.danger_accept_invalid_certs(true);
        }
        let client = builder.build().unwrap_or_else(|_| Client::new());

        Self {
            id,
            url,
            file_name,
            save_path,
            queue_name,
            parts: Arc::new(RwLock::new(Vec::new())),
            total_size: None,
            etag: None,
            last_modified: None,
            supports_concurrent: false,
            status_text: Arc::new(RwLock::new("Starting...".to_string())),
            is_paused: Arc::new(AtomicBool::new(false)),
            speed_limit,
            client,
            engine_settings: settings,
        }
    }

    pub async fn fetch_info_and_validate(&mut self) -> Result<(), Box<dyn std::error::Error + Send + Sync>> {
        let req = self.client
            .request(Method::HEAD, &self.url)
            .header(header::RANGE, "bytes=0-255")
            .build()?;

        let res = self.client.execute(req).await?;
        let status = res.status();

        if status.is_success() {
            if let Some(cd) = res.headers().get(header::CONTENT_DISPOSITION) {
                if let Ok(cd_str) = cd.to_str() {
                    if let Some(parsed) = extract_filename_from_content_disposition(cd_str) {
                        self.file_name = parsed;
                    }
                }
            }

            if let Some(len) = res.headers().get(header::CONTENT_LENGTH) {
                if let Ok(s) = len.to_str() {
                    self.total_size = s.parse().ok();
                }
            }
            if let Some(etag) = res.headers().get(header::ETAG) {
                if let Ok(s) = etag.to_str() {
                    self.etag = Some(s.to_string());
                }
            }
            if let Some(lm) = res.headers().get(header::LAST_MODIFIED) {
                if let Ok(s) = lm.to_str() {
                    self.last_modified = Some(s.to_string());
                }
            }
            
            self.supports_concurrent = status == reqwest::StatusCode::PARTIAL_CONTENT;
            
            if let Some(ar) = res.headers().get(header::ACCEPT_RANGES) {
                if ar.to_str().unwrap_or("") == "bytes" {
                    self.supports_concurrent = true;
                }
            }
            
            if let Some(cr) = res.headers().get(header::CONTENT_RANGE) {
                if let Ok(s) = cr.to_str() {
                    if let Some(idx) = s.find('/') {
                        self.total_size = s[idx+1..].parse().ok();
                        self.supports_concurrent = true;
                    }
                }
            }
        } else {
            return Err(format!("Server returned HTTP {}", status).into());
        }

        Ok(())
    }
}

fn extract_filename_from_content_disposition(cd: &str) -> Option<String> {
    // filename*=UTF-8''filename.ext
    if let Some(pos) = cd.find("filename*=") {
        let part = &cd[pos + 10..];
        let cleaned = part.trim_matches(|c| c == ' ' || c == ';' || c == '"');
        if let Some(quote_pos) = cleaned.rfind("''") {
            let encoded = &cleaned[quote_pos + 2..];
            if let Ok(decoded) = urlencoding::decode(encoded) {
                return Some(decoded.to_string());
            }
        }
    }
    // filename="example.zip" or filename=example.zip
    if let Some(pos) = cd.find("filename=") {
        let part = &cd[pos + 9..];
        let end = part.find(';').unwrap_or(part.len());
        let name = part[..end].trim_matches(|c| c == ' ' || c == '"' || c == '\'');
        if !name.is_empty() {
            return Some(name.to_string());
        }
    }
    None
}

impl HttpDownloadJob {
    pub async fn boot(&mut self) {
        println!("Booting FoxLoader job {} [{}]", self.id, self.file_name);
        
        let thread_count = (self.engine_settings.default_threads.clamp(1, 32)) as usize;

        {
            let mut parts = self.parts.write().await;
            if parts.is_empty() {
                if self.supports_concurrent && self.total_size.is_some() && self.total_size.unwrap() > 0 {
                    let total = self.total_size.unwrap();
                    let count = if total < (self.engine_settings.min_part_size * thread_count as u64) {
                        1
                    } else {
                        thread_count
                    };

                    let chunk_size = total / (count as u64);
                    for i in 0..count {
                        let from = (i as u64) * chunk_size;
                        let to = if i == count - 1 { total - 1 } else { ((i + 1) as u64) * chunk_size - 1 };
                        parts.push(RangedPart {
                            from,
                            to: Some(to),
                            current: from,
                            is_blind: false,
                            is_completed: false,
                            is_active: false,
                        });
                    }
                    *self.status_text.write().await = format!("Downloading ({} chunks)", count);
                    println!("Split into {} parts for {}", count, self.id);
                } else {
                    parts.push(RangedPart {
                        from: 0,
                        to: None,
                        current: 0,
                        is_blind: true,
                        is_completed: false,
                        is_active: false,
                    });
                    *self.status_text.write().await = "Downloading (Single Stream)".to_string();
                }
            } else {
                *self.status_text.write().await = format!("Downloading ({} chunks)", parts.len());
            }
        }

        let saver = TransactionalFileSaver::new(std::path::PathBuf::from(&self.save_path));
        let total_size = self.total_size.unwrap_or(0);
        
        let file = match saver.create_sparse_file(&self.file_name, total_size).await {
            Ok(f) => Arc::new(Mutex::new(f)),
            Err(e) => {
                println!("Failed to create sparse file: {}", e);
                *self.status_text.write().await = format!("Disk Error: {}", e);
                return;
            }
        };

        let parts_arc = Arc::clone(&self.parts);
        let client = self.client.clone();
        let url = self.url.clone();
        let workers = thread_count;
        let mut handles = Vec::new();
        let p_paused = self.is_paused.clone();
        let retry_max = self.engine_settings.auto_retry_count;
        let retry_delay = self.engine_settings.auto_retry_interval_secs;
        let dynamic_split = self.engine_settings.dynamic_parts;
        let min_part_size = self.engine_settings.min_part_size;
        let speed_lim = self.speed_limit;

        for _ in 0..workers {
            let p_arc = parts_arc.clone();
            let c = client.clone();
            let u = url.clone();
            let f_arc = file.clone();
            let p_paused_worker = p_paused.clone();

            handles.push(tokio::spawn(async move {
                let mut retry_count = 0;
                loop {
                    if p_paused_worker.load(Ordering::SeqCst) { break; }

                    // Find an incomplete part, or dynamically split a large remaining part!
                    let mut target_part_idx = None;
                    let mut from = 0;
                    let mut to = None;

                    {
                        let mut p_lock = p_arc.write().await;
                        // 1. Look for unassigned part
                        for (i, p) in p_lock.iter_mut().enumerate() {
                            if !p.is_completed && !p.is_active {
                                p.is_active = true;
                                target_part_idx = Some(i);
                                from = p.current;
                                to = p.to;
                                break;
                            }
                        }

                        // 2. Dynamic part creation: if no free part, try splitting the largest active part!
                        if target_part_idx.is_none() && dynamic_split && p_lock.len() < 32 {
                            let mut largest_remaining = 0;
                            let mut candidate_idx = None;
                            for (i, p) in p_lock.iter().enumerate() {
                                if p.is_active && !p.is_completed {
                                    if let Some(t) = p.to {
                                        if t > p.current {
                                            let rem = t - p.current;
                                            if rem > largest_remaining && rem > (min_part_size * 2) {
                                                largest_remaining = rem;
                                                candidate_idx = Some(i);
                                            }
                                        }
                                    }
                                }
                            }

                            if let Some(c_idx) = candidate_idx {
                                let orig_to = p_lock[c_idx].to.unwrap();
                                let orig_current = p_lock[c_idx].current;
                                let mid = orig_current + (orig_to - orig_current) / 2;

                                // Truncate original part
                                p_lock[c_idx].to = Some(mid);

                                // Create new part for upper half
                                let new_part = RangedPart {
                                    from: mid + 1,
                                    to: Some(orig_to),
                                    current: mid + 1,
                                    is_blind: false,
                                    is_completed: false,
                                    is_active: true,
                                };
                                p_lock.push(new_part);
                                let new_idx = p_lock.len() - 1;
                                target_part_idx = Some(new_idx);
                                from = mid + 1;
                                to = Some(orig_to);
                            }
                        }
                    }

                    let idx = match target_part_idx {
                        Some(i) => i,
                        None => {
                            tokio::time::sleep(tokio::time::Duration::from_millis(150)).await;
                            let p_lock = p_arc.read().await;
                            if p_lock.iter().all(|p| p.is_completed) { break; }
                            continue;
                        }
                    };

                    let range_header = if let Some(t) = to {
                        format!("bytes={}-{}", from, t)
                    } else {
                        format!("bytes={}-", from)
                    };

                    let req = match c.get(&u).header(header::RANGE, range_header).build() {
                        Ok(r) => r,
                        Err(_) => {
                            let mut p_lock = p_arc.write().await;
                            p_lock[idx].is_active = false;
                            break;
                        }
                    };

                    let mut res = match c.execute(req).await {
                        Ok(r) => {
                            retry_count = 0; // reset retry counter on success
                            r
                        }
                        Err(e) => {
                            let mut p_lock = p_arc.write().await;
                            p_lock[idx].is_active = false;
                            if retry_count < retry_max {
                                retry_count += 1;
                                println!("Connection error on FoxLoader part {}: {}. Retrying {}/{}...", idx, e, retry_count, retry_max);
                                tokio::time::sleep(tokio::time::Duration::from_secs(retry_delay * retry_count as u64)).await;
                                continue;
                            } else {
                                break;
                            }
                        }
                    };

                    if !res.status().is_success() {
                        let mut p_lock = p_arc.write().await;
                        p_lock[idx].is_active = false;
                        if retry_count < retry_max {
                            retry_count += 1;
                            tokio::time::sleep(tokio::time::Duration::from_secs(retry_delay)).await;
                            continue;
                        } else {
                            break;
                        }
                    }

                    let p_paused_inner = p_paused_worker.clone();
                    let mut is_part_finished = false;

                    while let Ok(Some(chunk)) = res.chunk().await {
                        if p_paused_inner.load(Ordering::SeqCst) { break; }
                        let chunk_len = chunk.len() as u64;

                        let mut f_lock = f_arc.lock().await;
                        if f_lock.seek(SeekFrom::Start(from)).await.is_ok() {
                            if f_lock.write_all(&chunk).await.is_ok() {
                                from += chunk_len;
                                let mut p_lock = p_arc.write().await;
                                p_lock[idx].current = from;
                                if let Some(t) = p_lock[idx].to {
                                    if from > t {
                                        p_lock[idx].is_completed = true;
                                        is_part_finished = true;
                                    }
                                }
                            }
                        }
                        drop(f_lock);

                        // Speed Limiter throttle check
                        if speed_lim > 0 {
                            let worker_speed_limit = (speed_lim / workers as u64).max(1024);
                            let expected_time_ms = (chunk_len * 1000) / worker_speed_limit;
                            if expected_time_ms > 0 {
                                tokio::time::sleep(tokio::time::Duration::from_millis(expected_time_ms.min(100))).await;
                            }
                        }

                        if is_part_finished { break; }
                    }
                    
                    let mut p_lock = p_arc.write().await;
                    p_lock[idx].is_active = false;
                    
                    if p_paused_worker.load(Ordering::SeqCst) { break; }
                    if is_part_finished || p_lock[idx].to.map(|t| from >= t).unwrap_or(false) {
                        p_lock[idx].is_completed = true;
                    }
                }
            }));
        }

        // Wait for workers
        for handle in handles {
            let _ = handle.await;
        }

        let is_all_completed = self.parts.read().await.iter().all(|p| p.is_completed);
        if is_all_completed {
            println!("Download {} [{}] finished. Renaming from .ermiya to final...", self.id, self.file_name);
            let _ = saver.commit_file(&self.file_name, self.last_modified.as_deref()).await;
            *self.status_text.write().await = "Completed".to_string();
        } else if self.is_paused.load(Ordering::SeqCst) {
            *self.status_text.write().await = "Paused".to_string();
        } else {
            *self.status_text.write().await = "Failed or Interrupted".to_string();
        }
    }

    pub async fn cancel(&mut self) {
        self.pause().await;
        if self.engine_settings.delete_file_on_cancel {
            let saver = TransactionalFileSaver::new(std::path::PathBuf::from(&self.save_path));
            let _ = saver.delete_file(&self.file_name).await;
        }
    }

    pub async fn pause(&mut self) {
        self.is_paused.store(true, Ordering::SeqCst);
        *self.status_text.write().await = "Paused".to_string();
    }

    pub async fn resume(&mut self) {
        if !self.is_paused.load(Ordering::SeqCst) { return; }
        self.is_paused.store(false, Ordering::SeqCst);
        *self.status_text.write().await = "Resuming...".to_string();
        
        let mut job_clone = self.clone();
        tokio::spawn(async move {
            job_clone.boot().await;
        });
    }

    pub async fn get_status(&self) -> super::JobStatus {
        let parts = self.parts.read().await;
        let mut downloaded = 0;
        for p in parts.iter() {
            downloaded += p.current.saturating_sub(p.from);
        }
        let status_text = self.status_text.read().await.clone();
        let mut chunks = Vec::new();
        for p in parts.iter() {
            chunks.push(super::ChunkStatus {
                start: p.from,
                end: p.to.unwrap_or(p.from),
                downloaded: p.current.saturating_sub(p.from),
            });
        }
        
        super::JobStatus {
            id: self.id,
            url: self.url.clone(),
            file_name: self.file_name.clone(),
            save_path: self.save_path.clone(),
            total_size: self.total_size,
            downloaded,
            status_text,
            chunks,
        }
    }
}
