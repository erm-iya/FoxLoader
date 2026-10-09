use std::io::{Read, Write};
use crate::manager::{DownloadManager, InteractiveReq};

pub fn ensure_desktop_running() {
    #[cfg(target_os = "windows")]
    {
        let output = std::process::Command::new("tasklist")
            .args(["/FI", "IMAGENAME eq ErmiyaDesktop.exe"])
            .output();

        let is_running = match output {
            Ok(o) => {
                let s = String::from_utf8_lossy(&o.stdout);
                s.contains("ErmiyaDesktop.exe")
            }
            Err(_) => false,
        };

        if !is_running {
            if let Ok(exe_path) = std::env::current_exe() {
                if let Some(dir) = exe_path.parent() {
                    let candidates = [
                        dir.join("ErmiyaDesktop.exe"),
                        dir.join("ReleaseOutput").join("ErmiyaDesktop.exe"),
                        std::path::PathBuf::from(r"C:\Program Files\FoxLoader\ErmiyaDesktop.exe"),
                        std::path::PathBuf::from(r"D:\Project\Ermiya_Download_Manager\ReleaseOutput\ErmiyaDesktop.exe"),
                    ];
                    for candidate in candidates {
                        if candidate.exists() {
                            let _ = std::process::Command::new(&candidate)
                                .current_dir(candidate.parent().unwrap_or(dir))
                                .spawn();
                            break;
                        }
                    }
                }
            }
        }
    }
}

pub fn run_native_messaging_loop(manager: DownloadManager) {
    std::thread::spawn(move || {
        let mut stdin = std::io::stdin().lock();
        let mut stdout = std::io::stdout().lock();

        loop {
            let mut len_bytes = [0u8; 4];
            if stdin.read_exact(&mut len_bytes).is_err() {
                break;
            }

            let msg_len = u32::from_ne_bytes(len_bytes) as usize;
            if msg_len == 0 || msg_len > 10 * 1024 * 1024 {
                break;
            }

            let mut buffer = vec![0u8; msg_len];
            if stdin.read_exact(&mut buffer).is_err() {
                break;
            }

            if let Ok(val) = serde_json::from_slice::<serde_json::Value>(&buffer) {
                let action = val.get("action").and_then(|v| v.as_str()).unwrap_or("");
                if action == "add_interactive" {
                    let url = val.get("url").and_then(|v| v.as_str()).unwrap_or("").to_string();
                    let file_name = val.get("file_name").and_then(|v| v.as_str()).unwrap_or("downloaded_file").to_string();

                    if !url.is_empty() {
                        let mgr = manager.clone();
                        tokio::spawn(async move {
                            mgr.push_interactive(InteractiveReq { url, file_name }).await;
                        });
                        ensure_desktop_running();
                    }
                } else if action == "ping" || action == "launch" {
                    ensure_desktop_running();
                }

                let response = serde_json::json!({ "status": "ok" });
                let resp_str = response.to_string();
                let resp_len = (resp_str.len() as u32).to_ne_bytes();
                if stdout.write_all(&resp_len).is_ok() {
                    let _ = stdout.write_all(resp_str.as_bytes());
                    let _ = stdout.flush();
                }
            }
        }
    });
}
