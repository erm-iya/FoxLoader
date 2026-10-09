use ermiya_core::manager::DownloadManager;
use ermiya_core::server::start_server;

#[tokio::main]
async fn main() {
    let manager = DownloadManager::new();
    ermiya_core::native_messaging::run_native_messaging_loop(manager.clone());
    start_server(manager).await;
}
