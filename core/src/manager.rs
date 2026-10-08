use std::collections::HashMap;
use std::sync::Arc;
use tokio::sync::RwLock;
use crate::job::DownloadJob;
use crate::job::http::HttpDownloadJob;
use crate::job::JobStatus;
use crate::settings::SettingsManager;
use crate::queue::QueueEngine;
use crate::storage::TransactionalFileSaver;
use serde::{Serialize, Deserialize};
use sha2::{Sha256, Digest};
use md5::Md5;
use std::path::Path;

#[derive(Serialize, Deserialize, Clone)]
pub struct DbItem {
    pub id: u64,
    pub url: String,
    pub file_name: String,
    pub save_path: String,
    pub queue: String,
}

#[derive(Serialize, Deserialize, Clone, Debug)]
pub struct InteractiveReq {
    pub url: String,
    pub file_name: String,
}

#[derive(Serialize, Deserialize, Clone, Debug)]
pub struct BatchInteractiveLink {
    pub url: String,
    pub file_name: String,
    pub file_type: String,
    pub link_text: String,
}

#[derive(Serialize, Deserialize, Clone, Debug)]
pub struct BatchInteractiveReq {
    pub page_url: String,
    pub links: Vec<BatchInteractiveLink>,
}

#[derive(Serialize, Deserialize, Clone)]
pub struct ChecksumReq {
    pub file_path: String,
    pub algorithm: String, // "md5" or "sha256"
}

#[derive(Serialize, Deserialize, Clone)]
pub struct ChecksumRes {
    pub file_path: String,
    pub hash: String,
    pub matches: Option<bool>,
}

#[derive(Clone)]
pub struct DownloadManager {
    last_id: Arc<RwLock<u64>>,
    jobs: Arc<RwLock<HashMap<u64, Arc<RwLock<DownloadJob>>>>>,
    interactive_queue: Arc<RwLock<Vec<InteractiveReq>>>,
    batch_interactive_queue: Arc<RwLock<Vec<BatchInteractiveReq>>>,
    pub settings_manager: Arc<SettingsManager>,
    pub queue_engine: Arc<QueueEngine>,
}

impl DownloadManager {
    pub fn new() -> Self {
        let settings_mgr = Arc::new(SettingsManager::new("settings.json"));
        let queue_eng = Arc::new(QueueEngine::new(settings_mgr.clone()));

        Self {
            last_id: Arc::new(RwLock::new(0)),
            jobs: Arc::new(RwLock::new(HashMap::new())),
            interactive_queue: Arc::new(RwLock::new(Vec::new())),
            batch_interactive_queue: Arc::new(RwLock::new(Vec::new())),
            settings_manager: settings_mgr,
            queue_engine: queue_eng,
        }
    }

    pub fn start_background_workers(&self) {
        let mgr = self.clone();
        tokio::spawn(async move {
            loop {
                tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
                mgr.process_queues().await;
            }
        });

        let mgr_sched = self.clone();
        tokio::spawn(async move {
            loop {
                tokio::time::sleep(tokio::time::Duration::from_secs(10)).await;
                let mgr_start = mgr_sched.clone();
                let mgr_pause = mgr_sched.clone();
                mgr_sched.queue_engine.check_scheduler_and_dispatch(
                    move |id| {
                        let m = mgr_start.clone();
                        async move { m.resume_job(id).await; }
                    },
                    move |id| {
                        let m = mgr_pause.clone();
                        async move { m.pause_job(id).await; }
                    },
                ).await;
            }
        });
    }

    pub async fn process_queues(&self) {
        let settings = self.settings_manager.get().await;
        for q_cfg in &settings.queues {
            let is_running = self.queue_engine.is_queue_running(&q_cfg.id).await;
            if !is_running { continue; }

            let item_ids = self.queue_engine.get_queue_items(&q_cfg.id).await;
            let mut running_count = 0;
            let mut pending_ids = Vec::new();
            let mut all_finished = true;

            let jobs_guard = self.jobs.read().await;
            for &id in &item_ids {
                if let Some(job_arc) = jobs_guard.get(&id) {
                    let j = job_arc.read().await;
                    let st = j.get_status().await;
                    if st.status_text.starts_with("Downloading") {
                        running_count += 1;
                        all_finished = false;
                    } else if st.status_text == "Added to queue" || st.status_text == "Paused (Loaded)" {
                        pending_ids.push(id);
                        all_finished = false;
                    } else if st.status_text != "Completed" {
                        all_finished = false;
                    }
                }
            }
            drop(jobs_guard);

            // If queue completed and power action specified
            if all_finished && !item_ids.is_empty() && q_cfg.power_action_on_finish != "None" {
                QueueEngine::execute_power_action(&q_cfg.power_action_on_finish);
            }

            // Start pending jobs up to max_concurrent limit
            let slots = (q_cfg.max_concurrent as usize).saturating_sub(running_count);
            for &p_id in pending_ids.iter().take(slots) {
                self.resume_job(p_id).await;
            }
        }
    }

    pub async fn push_interactive(&self, req: InteractiveReq) {
        self.interactive_queue.write().await.push(req);
    }

    pub async fn pop_interactive(&self) -> Option<InteractiveReq> {
        let mut q = self.interactive_queue.write().await;
        if q.is_empty() {
            None
        } else {
            Some(q.remove(0))
        }
    }

    pub async fn push_batch_interactive(&self, req: BatchInteractiveReq) {
        self.batch_interactive_queue.write().await.push(req);
    }

    pub async fn pop_batch_interactive(&self) -> Option<BatchInteractiveReq> {
        let mut q = self.batch_interactive_queue.write().await;
        if q.is_empty() {
            None
        } else {
            Some(q.remove(0))
        }
    }

    pub async fn load_db(&self) {
        let settings = self.settings_manager.get().await;
        let db_path = crate::settings::resolve_config_path("database.json");
        if let Ok(json) = std::fs::read_to_string(&db_path) {
            if let Ok(items) = serde_json::from_str::<Vec<DbItem>>(&json) {
                let mut max_id = 0;
                for item in items {
                    if item.id > max_id { max_id = item.id; }
                    let job = HttpDownloadJob::new(
                        item.id,
                        item.url,
                        item.file_name,
                        item.save_path,
                        item.queue.clone(),
                        settings.engine.clone(),
                        settings.speed_limiter.global_speed_limit,
                    );
                    job.is_paused.store(true, std::sync::atomic::Ordering::SeqCst);
                    let mut status = job.status_text.try_write().unwrap();
                    *status = "Paused (Loaded)".to_string();
                    drop(status);
                    
                    let enum_job = DownloadJob::Http(job);
                    let arc_job = Arc::new(RwLock::new(enum_job));
                    self.jobs.write().await.insert(item.id, arc_job);
                    self.queue_engine.add_item_to_queue(&item.queue, item.id).await;
                }
                *self.last_id.write().await = max_id;
            }
        }
    }

    pub async fn save_db(&self) {
        let mut items = Vec::new();
        let jobs = self.jobs.read().await;
        for job in jobs.values() {
            let j = job.read().await;
            let status = j.get_status().await;
            items.push(DbItem {
                id: status.id,
                url: status.url,
                file_name: status.file_name,
                save_path: status.save_path,
                queue: "main".to_string(),
            });
        }
        let json = serde_json::to_string_pretty(&items).unwrap_or_default();
        let db_path = crate::settings::resolve_config_path("database.json");
        let _ = std::fs::write(db_path, json);
    }

    pub async fn add_http_download(
        &self,
        url: String,
        file_name: String,
        save_path: String,
        start_immediately: bool,
        queue: String,
    ) -> u64 {
        let mut id_guard = self.last_id.write().await;
        *id_guard += 1;
        let id = *id_guard;
        drop(id_guard);

        let settings = self.settings_manager.get().await;
        let queue_id = if queue.is_empty() { "main".to_string() } else { queue };
        
        let job = HttpDownloadJob::new(
            id,
            url,
            file_name,
            save_path,
            queue_id.clone(),
            settings.engine.clone(),
            settings.speed_limiter.global_speed_limit,
        );
        
        if !start_immediately {
            job.is_paused.store(true, std::sync::atomic::Ordering::SeqCst);
            let mut status = job.status_text.write().await;
            *status = "Added to queue".to_string();
        }

        let mut job_clone = job.clone();
        let enum_job = DownloadJob::Http(job);
        let arc_job = Arc::new(RwLock::new(enum_job));
        let arc_job_clone = arc_job.clone();
        
        tokio::spawn(async move {
            if let Err(e) = job_clone.fetch_info_and_validate().await {
                *job_clone.status_text.write().await = format!("Failed: {}", e);
                let mut writer = arc_job_clone.write().await;
                *writer = DownloadJob::Http(job_clone);
            } else {
                {
                    let mut writer = arc_job_clone.write().await;
                    *writer = DownloadJob::Http(job_clone.clone());
                }
                
                if start_immediately {
                    job_clone.boot().await;
                }
                
                let mut writer = arc_job_clone.write().await;
                *writer = DownloadJob::Http(job_clone);
            }
        });

        let mut jobs = self.jobs.write().await;
        jobs.insert(id, arc_job);
        drop(jobs);

        self.queue_engine.add_item_to_queue(&queue_id, id).await;
        self.save_db().await;
        id
    }

    pub async fn delete_job(&self, id: u64, delete_from_disk: bool) {
        let mut jobs = self.jobs.write().await;
        if let Some(arc_job) = jobs.remove(&id) {
            let mut job = arc_job.write().await;
            let status = job.get_status().await;
            job.cancel().await;
            
            if delete_from_disk {
                let saver = TransactionalFileSaver::new(std::path::PathBuf::from(&status.save_path));
                let _ = saver.delete_file(&status.file_name).await;
            }
        }
        drop(jobs);
        self.queue_engine.remove_item(id).await;
        self.save_db().await;
    }

    pub async fn get_all_status(&self) -> Vec<JobStatus> {
        let mut statuses = Vec::new();
        let jobs = self.jobs.read().await;
        let settings = self.settings_manager.get().await;

        for job in jobs.values() {
            let j = job.read().await;
            let mut st = j.get_status().await;
            
            // Track deleted files on disk if enabled
            if settings.engine.track_deleted_files && st.status_text == "Completed" {
                let full_path = Path::new(&st.save_path).join(&st.file_name);
                if !full_path.exists() {
                    st.status_text = "Missing from disk".to_string();
                }
            }

            statuses.push(st);
        }
        statuses
    }

    pub async fn pause_job(&self, id: u64) {
        if let Some(job_arc) = self.jobs.read().await.get(&id) {
            let mut job = job_arc.write().await;
            job.pause().await;
        }
    }

    pub async fn resume_job(&self, id: u64) {
        if let Some(job_arc) = self.jobs.read().await.get(&id) {
            let mut job = job_arc.write().await;
            job.resume().await;
        }
    }

    pub async fn pause_all(&self) {
        let jobs = self.jobs.read().await;
        for job_arc in jobs.values() {
            let mut job = job_arc.write().await;
            job.pause().await;
        }
    }

    pub async fn delete_completed(&self) {
        let mut jobs = self.jobs.write().await;
        let mut ids_to_remove = Vec::new();
        for (id, job_arc) in jobs.iter() {
            let job = job_arc.read().await;
            if job.get_status().await.status_text == "Completed" {
                ids_to_remove.push(*id);
            }
        }
        for id in ids_to_remove {
            if let Some(arc_job) = jobs.remove(&id) {
                let mut job = arc_job.write().await;
                job.cancel().await;
            }
            self.queue_engine.remove_item(id).await;
        }
        drop(jobs);
        self.save_db().await;
    }

    pub fn calculate_checksum(file_path: &str, algorithm: &str) -> Result<String, String> {
        let bytes = std::fs::read(file_path).map_err(|e| e.to_string())?;
        match algorithm.to_lowercase().as_str() {
            "sha256" => {
                let mut hasher = Sha256::new();
                hasher.update(&bytes);
                Ok(hex::encode(hasher.finalize()))
            }
            "md5" => {
                let mut hasher = Md5::new();
                hasher.update(&bytes);
                Ok(hex::encode(hasher.finalize()))
            }
            _ => Err("Unsupported algorithm. Supported: md5, sha256".to_string()),
        }
    }
}
