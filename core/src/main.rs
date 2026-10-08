use ermiya_core::manager::DownloadManager;
use ermiya_core::server::start_server;

#[tokio::main]
async fn main() {
    let manager = DownloadManager::new();
    start_server(manager).await;
}
