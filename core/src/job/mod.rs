pub mod http;
use self::http::HttpDownloadJob;
use serde::Serialize;

#[derive(Serialize)]
pub struct ChunkStatus {
    pub start: u64,
    pub end: u64,
    pub downloaded: u64,
}

#[derive(Serialize)]
pub struct JobStatus {
    pub id: u64,
    pub url: String,
    pub file_name: String,
    pub save_path: String,
    pub total_size: Option<u64>,
    pub downloaded: u64,
    pub status_text: String,
    pub chunks: Vec<ChunkStatus>,
}

#[derive(Clone)]
pub enum DownloadJob {
    Http(HttpDownloadJob),
}

impl DownloadJob {
    pub async fn boot(&mut self) {
        match self {
            Self::Http(job) => job.boot().await,
        }
    }

    pub async fn get_status(&self) -> JobStatus {
        match self {
            Self::Http(job) => job.get_status().await,
        }
    }

    pub async fn pause(&mut self) {
        match self {
            Self::Http(job) => job.pause().await,
        }
    }

    pub async fn resume(&mut self) {
        match self {
            Self::Http(job) => job.resume().await,
        }
    }

    pub async fn cancel(&mut self) {
        match self {
            Self::Http(job) => job.cancel().await,
        }
    }
}
