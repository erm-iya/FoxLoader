<p align="center">
  <img src="logo.png" alt="FoxLoader Logo" width="140" height="140" />
</p>

<h1 align="center">FoxLoader</h1>

<p align="center">
  <b>Ultra-fast, modern, multi-threaded download manager with a native Windows 11 Fluent UI and a high-performance Rust core engine.</b>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D4?logo=windows&logoColor=white" alt="Platform Windows" />
  <img src="https://img.shields.io/badge/UI-WinUI%203%20%2F%20Windows%20App%20SDK-blue" alt="WinUI 3" />
  <img src="https://img.shields.io/badge/Engine-Rust%20(Tokio%20%2B%20Axum)-DEA584?logo=rust&logoColor=white" alt="Rust Core" />
  <img src="https://img.shields.io/badge/Extension-Manifest%20V3-00B4D8?logo=googlechrome&logoColor=white" alt="Manifest V3 Extension" />
  <img src="https://img.shields.io/badge/License-MIT-green" alt="License MIT" />
</p>

---

## 🚀 Overview

**FoxLoader** is a next-generation download manager engineered from the ground up for speed, reliability, and aesthetics:
- **Rust Core Engine (`ermiya-core`)**: Asynchronous, multi-threaded worker pool powered by Tokio and Axum, capable of splitting downloads into 1 to 32 parallel streams with sparse-file pre-allocation and atomic completion.
- **Native Windows 11 Experience (`WinUI 3`)**: Modern Mica backdrop, fluid WinUI controls, dynamic dark/light theme switching, and seamless DPI-aware layout.
- **Browser Integration (Manifest V3)**: Works seamlessly with Chrome, Edge, and Brave via Native Messaging, featuring media sniffing and batch link extraction.
- **Smart Download Intelligence**: Automatic duplicate file detection, version comparison, multi-part RAR/ZIP discovery, and episode batching.
- **Multilingual Support**: Dynamic runtime translation for **English**, **Persian (پارسی)**, and **Kurdish Sorani (کوردی سۆرانی)** with automatic RTL layout mirroring.

---

## ✨ Features

- **⚡ Dynamic Connection Acceleration**: Auto-scales between 1 and 32 threads based on server speed and connection limits.
- **🧩 Segmented Progress Visualization**: Real-time chunk progress display with jitter-free EMA speed tracking.
- **📦 Multi-Part Archive Discovery**: Automatically scans servers for companion parts (`.part1.rar`, `.part2.rar`) and imports them in 1 click.
- **🎬 Media & Video Sniffer**: Detects HTML5 streaming video/audio on web pages and provides a 1-click download floating widget.
- **📋 Batch Link Grabber**: Advanced URL filtering, extension categorization, wildcard ranges (`001` to `100`), and queue routing.
- **⏱️ Queue Scheduler & Power Actions**: Schedule queues with start/stop timers, bandwidth quotas, and auto-shutdown/sleep triggers.
- **🔒 Security & Integrity**: Integrated MD5 and SHA-256 checksum verification, path traversal protection, and DNS rebinding shields.

---

## 📥 Installation & Download

Download the latest pre-compiled bundle from the [Releases](https://github.com/erm-iya/FoxLoader/releases) page:

1. Extract `FoxLoader_v1.0_Windows_x64.zip`.
2. Run `ErmiyaDesktop.exe`.
3. (Optional) Run `register_browser_integration.bat` to register Chrome, Edge, and Brave browser interception.
4. Load the `extension/` folder in Chrome / Edge via `chrome://extensions` (Developer Mode > Load unpacked).

---

## 🛠️ Building from Source

### Prerequisites
- Windows 10/11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Rust & Cargo](https://rustup.rs/) (latest stable)

### 1. Build Rust Core
```powershell
cargo build --release
```

### 2. Build Windows WinUI 3 App
```powershell
cd windows
dotnet build -c Release
```

### 3. Package Release Bundle
```powershell
powershell -ExecutionPolicy Bypass -File .\build_installer.ps1
```

---

## 📜 License

This project is licensed under the MIT License.
