use chrono::{Datelike, Local, Timelike};
use std::collections::HashMap;
use std::sync::Arc;
use tokio::sync::RwLock;
use crate::settings::SettingsManager;

#[derive(Clone, Debug)]
pub struct QueueState {
    pub is_running: bool,
    pub item_ids: Vec<u64>,
}

#[derive(Clone)]
pub struct QueueEngine {
    settings_manager: Arc<SettingsManager>,
    // Queue ID -> QueueState
    states: Arc<RwLock<HashMap<String, QueueState>>>,
}

impl QueueEngine {
    pub fn new(settings_manager: Arc<SettingsManager>) -> Self {
        Self {
            settings_manager,
            states: Arc::new(RwLock::new(HashMap::new())),
        }
    }

    pub async fn add_item_to_queue(&self, queue_id: &str, item_id: u64) {
        let mut states = self.states.write().await;
        let state = states.entry(queue_id.to_string()).or_insert_with(|| QueueState {
            is_running: true,
            item_ids: Vec::new(),
        });
        if !state.item_ids.contains(&item_id) {
            state.item_ids.push(item_id);
        }
    }

    pub async fn remove_item(&self, item_id: u64) {
        let mut states = self.states.write().await;
        for state in states.values_mut() {
            state.item_ids.retain(|&id| id != item_id);
        }
    }

    pub async fn reorder_queue(&self, queue_id: &str, new_order: Vec<u64>) {
        let mut states = self.states.write().await;
        if let Some(state) = states.get_mut(queue_id) {
            state.item_ids = new_order;
        }
    }

    pub async fn set_queue_running(&self, queue_id: &str, running: bool) {
        let mut states = self.states.write().await;
        if let Some(state) = states.get_mut(queue_id) {
            state.is_running = running;
        }
    }

    pub async fn is_queue_running(&self, queue_id: &str) -> bool {
        let states = self.states.read().await;
        states.get(queue_id).map(|s| s.is_running).unwrap_or(true)
    }

    pub async fn get_queue_items(&self, queue_id: &str) -> Vec<u64> {
        let states = self.states.read().await;
        states.get(queue_id).map(|s| s.item_ids.clone()).unwrap_or_default()
    }

    pub async fn check_scheduler_and_dispatch<F, Fut, S, FutS>(&self, start_job: F, pause_job: S)
    where
        F: Fn(u64) -> Fut + Send + Sync + 'static,
        Fut: std::future::Future<Output = ()> + Send + 'static,
        S: Fn(u64) -> FutS + Send + Sync + 'static,
        FutS: std::future::Future<Output = ()> + Send + 'static,
    {
        let now = Local::now();
        let current_hm = format!("{:02}:{:02}", now.hour(), now.minute());
        // 1=Mon .. 7=Sun
        let weekday = now.weekday().number_from_monday() as u8;

        let settings = self.settings_manager.get().await;
        for q in &settings.queues {
            if q.scheduler_enabled && q.active_days.contains(&weekday) {
                if let Some(ref start) = q.start_time {
                    if start == &current_hm {
                        self.set_queue_running(&q.id, true).await;
                        let items = self.get_queue_items(&q.id).await;
                        for id in items {
                            start_job(id).await;
                        }
                    }
                }
                if let Some(ref stop) = q.stop_time {
                    if stop == &current_hm {
                        self.set_queue_running(&q.id, false).await;
                        // Pause running jobs in this queue
                        let items = self.get_queue_items(&q.id).await;
                        for id in items {
                            pause_job(id).await;
                        }
                    }
                }
            }
        }
    }

    pub fn execute_power_action(action: &str) {
        match action {
            "Shutdown" => {
                println!("Executing Scheduled Power Action: Shutdown");
                let _ = std::process::Command::new("shutdown")
                    .args(["/s", "/t", "60", "/c", "FoxLoader completed downloads"])
                    .spawn();
            }
            "Sleep" => {
                println!("Executing Scheduled Power Action: Sleep");
                let _ = std::process::Command::new("rundll32.exe")
                    .args(["powrprof.dll,SetSuspendState", "0,1,0"])
                    .spawn();
            }
            "Hibernate" => {
                println!("Executing Scheduled Power Action: Hibernate");
                let _ = std::process::Command::new("shutdown")
                    .args(["/h"])
                    .spawn();
            }
            _ => {}
        }
    }
}
