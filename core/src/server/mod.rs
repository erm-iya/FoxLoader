use axum::{
    routing::{post, get},
    Router, Json,
    extract::{State, Request},
    middleware::{self, Next},
    response::{Response, IntoResponse},
    http::{StatusCode, HeaderValue, Method},
};
use serde::{Deserialize, Serialize};
use std::net::SocketAddr;
use crate::manager::{DownloadManager, ChecksumReq, ChecksumRes, DbItem};
use crate::job::JobStatus;
use crate::settings::{AppSettings, QueueConfig};

#[derive(Deserialize)]
pub struct AddDownloadReq {
    pub url: String,
    pub file_name: String,
    pub save_path: String,
    pub start_immediately: bool,
    pub queue: String,
}

#[derive(Deserialize)]
pub struct BatchAddReq {
    pub items: Vec<AddDownloadReq>,
}

#[derive(Deserialize)]
pub struct JobDeleteReq {
    pub id: u64,
    pub delete_from_disk: Option<bool>,
}

#[derive(Deserialize)]
pub struct JobActionReq {
    pub id: u64,
}

#[derive(Deserialize)]
pub struct QueueToggleReq {
    pub queue_id: String,
    pub running: bool,
}

#[derive(Deserialize)]
pub struct QueueReorderReq {
    pub queue_id: String,
    pub item_ids: Vec<u64>,
}

#[derive(Serialize)]
pub struct AddDownloadRes {
    pub id: u64,
}

/// Security middleware enforcing Host binding and blocking arbitrary web cross-origin requests
async fn security_headers_middleware(req: Request, next: Next) -> Result<Response, StatusCode> {
    // 1. Host header validation to guard against DNS rebinding
    if let Some(host) = req.headers().get("host").and_then(|h| h.to_str().ok()) {
        let host_name = host.split(':').next().unwrap_or(host);
        if host_name != "127.0.0.1" && host_name != "localhost" && host_name != "[::1]" {
            return Err(StatusCode::FORBIDDEN);
        }
    }

    // 2. Origin validation: Block web-to-localhost cross-origin requests
    let origin_owned = req.headers().get("origin")
        .and_then(|h| h.to_str().ok())
        .map(|s| s.to_string());

    if let Some(ref origin) = origin_owned {
        let is_allowed = origin.starts_with("chrome-extension://")
            || origin.starts_with("moz-extension://")
            || origin.starts_with("http://127.0.0.1")
            || origin.starts_with("http://localhost");

        if !is_allowed {
            return Err(StatusCode::FORBIDDEN);
        }
    }

    // Handle preflight OPTIONS requests
    if req.method() == Method::OPTIONS {
        let mut response = StatusCode::NO_CONTENT.into_response();
        let res_headers = response.headers_mut();
        if let Some(ref origin) = origin_owned {
            if let Ok(val) = HeaderValue::from_str(origin) {
                res_headers.insert("Access-Control-Allow-Origin", val);
            }
        }
        res_headers.insert("Access-Control-Allow-Methods", HeaderValue::from_static("GET, POST, OPTIONS"));
        res_headers.insert("Access-Control-Allow-Headers", HeaderValue::from_static("Content-Type, Authorization, X-Requested-With"));
        return Ok(response);
    }

    let mut response = next.run(req).await;

    // Attach CORS headers for allowed caller origins
    if let Some(ref origin) = origin_owned {
        if let Ok(val) = HeaderValue::from_str(origin) {
            response.headers_mut().insert("Access-Control-Allow-Origin", val);
        }
    }
    response.headers_mut().insert("Access-Control-Allow-Methods", HeaderValue::from_static("GET, POST, OPTIONS"));
    response.headers_mut().insert("Access-Control-Allow-Headers", HeaderValue::from_static("Content-Type, Authorization, X-Requested-With"));

    Ok(response)
}

pub async fn start_server(manager: DownloadManager) {
    manager.load_db().await;
    manager.start_background_workers();

    let app = Router::new()
        .route("/add", post(add_handler))
        .route("/batch_add", post(batch_add_handler))
        .route("/add_interactive", post(add_interactive_handler))
        .route("/pop_interactive", get(pop_interactive_handler))
        .route("/add_batch_interactive", post(add_batch_interactive_handler))
        .route("/pop_batch_interactive", get(pop_batch_interactive_handler))
        .route("/pause", post(pause_handler))
        .route("/pause_all", post(pause_all_handler))
        .route("/resume", post(resume_handler))
        .route("/delete", post(delete_handler))
        .route("/delete_completed", post(delete_completed_handler))
        .route("/status", get(status_handler))
        .route("/settings", get(get_settings_handler).post(update_settings_handler))
        .route("/queues", get(get_queues_handler))
        .route("/queue_toggle", post(queue_toggle_handler))
        .route("/queue_reorder", post(queue_reorder_handler))
        .route("/verify_checksum", post(verify_checksum_handler))
        .route("/export_json", get(export_json_handler))
        .route("/import_json", post(import_json_handler))
        .layer(middleware::from_fn(security_headers_middleware))
        .with_state(manager.clone());

    // Dynamic port resolution from settings with fallback
    let configured_port = manager.settings_manager.get().await.port;
    let target_port = if configured_port >= 1024 { configured_port } else { 2764 };
    let addr = SocketAddr::from(([127, 0, 0, 1], target_port));

    let listener = match tokio::net::TcpListener::bind(&addr).await {
        Ok(l) => l,
        Err(e) => {
            eprintln!("Failed to bind port {}: {}. Falling back to default port 2764", target_port, e);
            let fallback_addr = SocketAddr::from(([127, 0, 0, 1], 2764));
            tokio::net::TcpListener::bind(&fallback_addr).await.expect("Failed to bind to 127.0.0.1:2764")
        }
    };

    let bound_port = listener.local_addr().map(|a| a.port()).unwrap_or(target_port);
    println!("FoxLoader Server running on http://127.0.0.1:{}", bound_port);
    axum::serve(listener, app).await.unwrap();
}

async fn add_handler(
    State(manager): State<DownloadManager>,
    Json(payload): Json<AddDownloadReq>,
) -> Json<AddDownloadRes> {
    let id = manager.add_http_download(
        payload.url,
        payload.file_name,
        payload.save_path,
        payload.start_immediately,
        payload.queue,
    ).await;
    Json(AddDownloadRes { id })
}

async fn batch_add_handler(
    State(manager): State<DownloadManager>,
    Json(payload): Json<BatchAddReq>,
) -> Json<Vec<u64>> {
    let mut ids = Vec::new();
    for item in payload.items {
        let id = manager.add_http_download(
            item.url,
            item.file_name,
            item.save_path,
            item.start_immediately,
            item.queue,
        ).await;
        ids.push(id);
    }
    Json(ids)
}

async fn add_interactive_handler(
    State(manager): State<DownloadManager>,
    Json(payload): Json<crate::manager::InteractiveReq>,
) -> &'static str {
    manager.push_interactive(payload).await;
    "OK"
}

async fn pop_interactive_handler(
    State(manager): State<DownloadManager>,
) -> Json<Option<crate::manager::InteractiveReq>> {
    Json(manager.pop_interactive().await)
}

async fn add_batch_interactive_handler(
    State(manager): State<DownloadManager>,
    Json(payload): Json<crate::manager::BatchInteractiveReq>,
) -> &'static str {
    manager.push_batch_interactive(payload).await;
    "OK"
}

async fn pop_batch_interactive_handler(
    State(manager): State<DownloadManager>,
) -> Json<Option<crate::manager::BatchInteractiveReq>> {
    Json(manager.pop_batch_interactive().await)
}

async fn status_handler(State(manager): State<DownloadManager>) -> Json<Vec<JobStatus>> {
    let statuses = manager.get_all_status().await;
    Json(statuses)
}

async fn pause_handler(
    State(manager): State<DownloadManager>,
    Json(payload): Json<JobActionReq>,
) -> &'static str {
    manager.pause_job(payload.id).await;
    "OK"
}

async fn resume_handler(
    State(manager): State<DownloadManager>,
    Json(payload): Json<JobActionReq>,
) -> &'static str {
    manager.resume_job(payload.id).await;
    "OK"
}

async fn delete_handler(
    State(manager): State<DownloadManager>,
    Json(payload): Json<JobDeleteReq>,
) -> &'static str {
    let delete_disk = payload.delete_from_disk.unwrap_or(false);
    manager.delete_job(payload.id, delete_disk).await;
    "OK"
}

async fn pause_all_handler(
    State(manager): State<DownloadManager>,
) -> &'static str {
    manager.pause_all().await;
    "OK"
}

async fn delete_completed_handler(
    State(manager): State<DownloadManager>,
) -> &'static str {
    manager.delete_completed().await;
    "OK"
}

async fn get_settings_handler(
    State(manager): State<DownloadManager>,
) -> Json<AppSettings> {
    Json(manager.settings_manager.get().await)
}

async fn update_settings_handler(
    State(manager): State<DownloadManager>,
    Json(new_settings): Json<AppSettings>,
) -> &'static str {
    let old_port = manager.settings_manager.get().await.port;
    if new_settings.port != old_port {
        println!("Port setting updated from {} to {}. Requires server restart to rebind.", old_port, new_settings.port);
    }
    let _ = manager.settings_manager.update(new_settings).await;
    "OK"
}

async fn get_queues_handler(
    State(manager): State<DownloadManager>,
) -> Json<Vec<QueueConfig>> {
    let settings = manager.settings_manager.get().await;
    Json(settings.queues)
}

async fn queue_toggle_handler(
    State(manager): State<DownloadManager>,
    Json(payload): Json<QueueToggleReq>,
) -> &'static str {
    manager.queue_engine.set_queue_running(&payload.queue_id, payload.running).await;
    "OK"
}

async fn queue_reorder_handler(
    State(manager): State<DownloadManager>,
    Json(payload): Json<QueueReorderReq>,
) -> &'static str {
    manager.queue_engine.reorder_queue(&payload.queue_id, payload.item_ids).await;
    "OK"
}

async fn verify_checksum_handler(
    Json(payload): Json<ChecksumReq>,
) -> Json<ChecksumRes> {
    match DownloadManager::calculate_checksum(&payload.file_path, &payload.algorithm) {
        Ok(hash) => Json(ChecksumRes {
            file_path: payload.file_path,
            hash,
            matches: None,
        }),
        Err(e) => Json(ChecksumRes {
            file_path: payload.file_path,
            hash: format!("Error: {}", e),
            matches: Some(false),
        }),
    }
}

async fn export_json_handler(
    State(manager): State<DownloadManager>,
) -> Json<Vec<JobStatus>> {
    Json(manager.get_all_status().await)
}

async fn import_json_handler(
    State(manager): State<DownloadManager>,
    Json(items): Json<Vec<DbItem>>,
) -> &'static str {
    for item in items {
        manager.add_http_download(
            item.url,
            item.file_name,
            item.save_path,
            false,
            item.queue,
        ).await;
    }
    "OK"
}
