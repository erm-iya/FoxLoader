# Long-Term Project Memory

## 2026-10-01: Project Initialization & Planning
- Reviewed foundational architecture (`docs/DIAGRAMS.md`) and settings matrix (`docs/SETTINGS_MATRIX.md`).
- Prepared implementation plan for `ermiya-core` (pure Rust) and workspace setup.
- **Architectural Decisions:** 
  - Core will be written in Rust (`ermiya-core`).
  - No UI dependencies in core; interaction via FFI (`uniffi`) and local REST server (`Axum`).
  - Storage will be JSON-based using a transactional file saver.
  - **Platform Strategy:** Focus solely on Windows (WinUI 3 native look) and the pure Rust core for now. Other OS versions are deferred to future years.
  - **Feature Phasing:** Telegram (Feature 27) and Ermiya Brain (Feature 24) are deferred to post-release updates.
  - **FFI Strategy:** Delay Uniffi integration until the core logic is fully stable.
  - **Browser Extension:** A scaffold for the Manifest V3 extension will be built from scratch in Phase 1 to validate HTTP download logic.

## 2026-10-01: Core Logic & Windows UI Integration
- Implemented `Axum` REST API in `ermiya-core` to host the `127.0.0.1:2764/add` endpoint.
- Implemented HTTP chunking logic in `HttpDownloadJob` using `HEAD` requests to verify `Accept-Ranges` and split jobs into 16 concurrent workers.
- Introduced `TransactionalFileSaver` and Sparse Files (`tokio::fs::OpenOptions` and `set_len`) to pre-allocate storage and write chunks simultaneously at correct offsets using `tokio::io::AsyncSeekExt`.
- **Known Trade-offs / Deliberate Skips**: 
  - Substituted `dyn DownloadJob` for an Enum to bypass Rust's limitations with async trait methods.
  - Used `$env:CARGO_TARGET_DIR="target_cli"` to bypass `os error 32` file locking caused by `rust-analyzer` concurrent checks during development.
- Built a native WinUI 3 front-end (`ErmiyaDesktop`) using Mica Backdrop and WinUI controls, sending REST calls directly to the core.

## 2026-10-02: Version 1.0 Finalization & Release Packaging
- Completed all disabled navigation handlers in WinUI 3 `MainPage.xaml.cs`.
- Implemented `TopMenuStopAll_Click` and `TopMenuClear_Click` integrating with the `/pause_all` and `/delete_completed` Axum routes.
- Implemented live category filtering in `NavigationView` using dynamic evaluation in the polling loop to prevent layout flicker and selection loss.
- Configured `App.xaml.cs` to launch the `ermiya-core.exe` process automatically upon start and safely terminate it on exit.
- Built release artifacts (`cargo build --release` and `dotnet publish -c Release -p:PublishSingleFile=true`).
- Prepared `build_installer.ps1` to assemble `ErmiyaDesktop.exe`, `ermiya-core.exe`, and `extension.zip` into a single release package `Ermiya_Download_Manager_v1.0.zip` ready for public bug hunting on GitHub.

## 2026-10-02: Comprehensive Engine Architecture & Custom Feature Enhancements
- Completed comprehensive architectural audit and capability expansion for FoxLoader (FoxLoader) across all modules and configuration options.
- **Dynamic Connection Splitting (1-32 Threads)**:
  - Upgraded `HttpDownloadJob` in `core/src/job/http.rs` to dynamically recalculate active worker threads between 1 and 32 based on remaining bytes and connection speed thresholds (>5 MB/s triggers higher concurrency up to 32; small byte chunks downscale to avoid server connection thrashing).
- **`.ermiya` Storage Format & Timestamp Sync**:
  - Replaced `.tmp` storage with `.ermiya` partial files in `core/src/storage/mod.rs`. On completion, files are atomically committed and renamed to the clean target filename.
  - Integrated `filetime` crate in core to read HTTP `Last-Modified` response headers and set matching filesystem modified timestamps on finished downloads.
- **Queue Engine & Background Scheduler**:
  - Implemented `QueueEngine` (`core/src/queue.rs`) managing unlimited named queues, each with custom schedules (`start_time`, `stop_time`), worker concurrency (`max_concurrent`), auto-stop triggers, and power actions (`Shutdown`, `Sleep`, `Hibernate` via Windows API bindings).
  - Background scheduler worker continuously monitors active queues, dispatches pending downloads, pauses on schedule expiration, and executes power actions when queues finish.
- **Disk File Tracker & User-Decisioned Deletion**:
  - Added `missing_from_disk` flag in core download manager, updating items when finished files are moved or deleted externally.
  - Added user-decisioned Delete dialog in WinUI 3 `MainPage.xaml` featuring an optional `CheckBox` ("Also delete file from disk") mapped to `delete_from_disk` in core `delete_job`.
- **Auto-Retry & Network Resilience**:
  - Added exponential backoff network retry loop in `HttpDownloadJob` configured via `app_settings.retry_attempts` and `retry_delay_seconds`.
- **Comprehensive Settings System**:
  - Created `SettingsManager` in `core/src/settings.rs` with persistent `settings.json` and REST endpoints `/settings` (GET/POST).
  - Built comprehensive WinUI `SettingsWindow.xaml` across 7 customizable categories:
    1. *Engine*: Concurrency (1-32), buffer size, retry policy, User-Agent, strict TLS toggle.
    2. *Appearance*: Themes (System Settings, Light, Dark, Light Teal, Dark Teal), icon labels toggle, sound notifications, speed units (Auto/KB/MB).
    3. *Speed Limiter*: Global and per-job token bucket bandwidth caps.
    4. *Queues & Scheduler*: Default queue management and auto-power actions.
    5. *Network & Proxy*: System proxy, manual HTTP/SOCKS5 proxy, DNS-over-HTTPS (DoH).
    6. *Per-Host Rules*: Domain-based thread caps, credentials (Basic Auth), and custom User-Agent headers.
    7. *Browser & API Integration*: Port configuration and API key controls.
- **Category Paths & Checksum Verification**:
  - Configured default download destination to `downloads/FoxLoader/{category}`.
  - Implemented MD5 and SHA-256 checksum calculation in `core/src/manager.rs` (`/verify_checksum`) and surfaced in the WinUI Properties dialog.
- **Batch / Multi-URL Import**:
  - Implemented batch URL modal in `MainPage.xaml` accepting multi-line URLs, auto-cleaning query strings, and batch queuing to core.
- **Browser Extension Upgrades**:
  - Added Context Menu options ("Download with FoxLoader", "Download all links on page") in `extension/background.js`.
  - Created `extension/sniffer.js` with floating download badge for video/audio streams.
  - Added `native_host_manifest.json` and `register_browser_integration.bat` for Chrome, Edge, and Brave native integration.
- **Speed Smoothing**:
  - Integrated rolling Exponential Moving Average (EMA, alpha=0.3) in WinUI `MainPage.xaml.cs` for jitter-free real-time speed reporting.
- **Release Verification**:
  - Re-compiled `ermiya-core.exe` (Rust release) and `ErmiyaDesktop.exe` (.NET 8 WinUI release).
  - **AddDownloadWindow UI Overhaul**:
  - Resolved window height truncation where action buttons were cut off on high-DPI Windows displays.
  - Implemented DPI-aware sizing (`GetDpiForWindow`) and screen centering via `DisplayArea`.
  - Upgraded styling with Windows 11 `MicaBackdrop`, custom header, and dedicated footer toolbar.
  - Added live HTTP HEAD check fetching formatted file size, `Accept-Ranges` resume status, and `Content-Disposition` server filename.
  - Added Category selection dropdown with automatic extension detection routing downloads to `downloads/FoxLoader/{category}`.
  - Added "Download Later" action button alongside "Start Download" and "Cancel".
  - Integrated custom draggable header directly into WinUI title bar (`ExtendsContentIntoTitleBar = true`), completely eliminating the legacy black Windows non-client title bar and blending caption controls into Mica.
  - Implemented Always-on-Top (Pinned) behavior via `OverlappedPresenter.IsAlwaysOnTop` and Win32 `SetWindowPos(HWND_TOPMOST)` so download dialogs pop directly above active browsers without falling behind, complete with a visual pin toggle button.

## 2026-10-02: Smart Download Intelligence System
- Introduced `SmartDownloadIntelligence` engine in `windows/SmartDownloadIntelligence.cs` bringing AI/heuristic download assistance:
  - **Duplicate File Detection**: Strips transient query strings/tokens and matches against download history. If identical file exists on disk, renders an alert banner offering to "Open Existing File" (shell execute), "Show in Folder", or proceed with "Download Again".
  - **Application Version Recognition**: Heuristically extracts Application Title and Semantic Version strings (e.g. `Adobe Photoshop 2026` vs `2025`, `v27.10.0` vs `v26.0`, `App 6.43.12` vs `6.43.10`). Alerts the user if an older version of the same software is already installed/stored on disk.
  - **Multi-Part Archive Discovery (RAR / ZIP / 7z)**: Identifies multi-part naming conventions (`.partN.rar`, `.z01`, `.r00`, etc.), automatically sends asynchronous `HEAD` requests to discover all companion parts available on the remote server, and displays an interactive parts checklist with file sizes, offering "Download All Parts" in a single batch.
  - **TV Series Season & Episode Batching**: Parses TV show naming patterns (`S{season}E{episode}`, quality tags e.g. `720p`, `WEB-DL`), automatically routes save destination to structured directories (`downloads/FoxLoader/Video/{ShowTitle}/S{Season:D2}`), and offers an episode range generator to download multiple episodes in a single click.

## 2026-10-02: Instant Pause Speed Zeroing & Adaptive Time Formatting
- **Adaptive Remaining Time (`FormatRemainingTime`)**: Replaced raw seconds display (`2426 sec`) with intelligent multi-unit formatting (`40 min 26 sec`, `1 hr 15 min`, `1 d 4 hr`, etc.) based on duration thresholds.
- **Immediate Pause Zeroing**: Fixed lingering speed decay caused by Exponential Moving Average (EMA) rolling filter. As soon as a job reports `Paused` or `Completed`, smoothed speed snaps to `0 MB/s` immediately with zero lag.

## 2026-10-02: Settings Window Wiring & Granular Context Menu Expansion
- **Settings Window Fix**: Wired `args.IsSettingsSelected` in `NavigationView.SelectionChanged` and hooked `TopMenuOptions_Click` / `TopMenuScheduler_Click` with automatic section routing (`NavigateToSection`). Enhanced `SettingsWindow` with DPI-aware sizing (`860x640 * scale`), screen centering, icon setup, and foreground activation.
- **Granular Context Menu Overhaul**: Replaced the 4-item menu with a full-featured suite:
  - *Open File* and *Open With...* (Windows shell picker).
  - *Open Folder in Explorer*.
  - *Resume / Start*, *Pause*, and *Redownload from Beginning*.
  - *Move to Queue* submenu (Main Queue, Night Queue, Sync Queue).
  - *Copy Information* submenu (Copy Download URL, Copy File Name, Copy Full File Path).
  - *Verify Checksum (MD5 / SHA-256)* and *Properties* dialog.
## 2026-10-02: Native In-App Settings Architecture Migration
- **Architectural Shift from Floating Window to In-App View**:
  - Replaced the standalone `SettingsWindow` with a native in-app view (`SettingsControl.xaml` & `SettingsControl.xaml.cs`) directly hosted inside `MainPage.xaml`.
  - **Eliminated Window Layering Bugs**: As an in-app view, settings can never open behind the main application or get hidden beneath other windows.
  - **100% Theme & Title Bar Consistency**: Inherits `MainWindow`'s Mica backdrop, custom dark title bar, and system caption buttons seamlessly.
  - **Unified Navigation & Fluid UX**:
    - Clicking the `NavigationView` Settings gear icon, toolbar **Options**, or toolbar **Scheduler** smoothly hides `DownloadsViewGrid` and reveals `SettingsView` without reinitializing or losing the download list state.
    - Toolbar **Scheduler** automatically jumps directly to the "Queues & Scheduler" settings section.
    - Activates `NavView.IsBackButtonVisible = Visible` when settings are shown. Clicking the Back button, the in-form "Back to Downloads" button, "Discard", "Save Changes", or any category in the sidebar instantly transitions back to the downloads view.
    - Preserves download monitoring ticker and scroll position in the background.

## 2026-10-02: Complete Browser Extension Suite Overhaul (v1.2)
- **Modern Fluent Popup Interface (`extension/popup/`)**:
  - Engineered an obsidian/cyan dark Glassmorphism UI (`popup.html`, `popup.css`, `popup.js`) with live FoxLoader core connectivity indicator (🟢 Connected / 🔴 Offline).
  - Added live engine metrics card reflecting active background downloads.
  - Added Master Interception toggle and Media Sniffer toggle with `chrome.storage.local` persistence.
  - **Active Tab Media Discovery**: Scans current tab for video/audio streams (`.mp4`, `.webm`, `.m3u8`, `.mp3`) and provides 1-click download actions or "Download All Captured Media".
  - **Quick Action Tools**: Added "Grab All Links" launcher and collapsible "Add Custom URL" bar with clipboard auto-paste.
  - **Per-Site Exclusion (Whitelist)**: 1-click button to exclude current domain (e.g. internal intranets, banks) from FoxLoader interception.
- **In-Page Interactive Batch Link Grabber (`extension/sniffer.js`)**:
  - Replaced legacy blind link scraping with an in-page modal dialog.
  - Automatic categorization across Archives, Videos, Audio, Documents, Programs, and Images with live search filtering, multi-selection checkboxes, queue targeting (Main / Night Queue), and batch dispatch to FoxLoader `/batch_add`.
- **Floating Media Player Widget (`extension/sniffer.js`)**:
  - Clean floating download badge on HTML5 video players (`<video>`, YouTube, Aparat, Vimeo) with auto-hide to avoid obstructing player controls or subtitles.
- **Service Worker Hardening (`extension/background.js`)**:
  - Validates `interceptEnabled` and `excludedDomains` before cancelling native browser downloads.
  - Dynamic tab badge counter displaying detected media count on extension icon.
- **Manifest V3 Optimization (`extension/manifest.json`)**:
  - Added `storage` and `activeTab` permissions, registered `popup/popup.html`, and generated dedicated 16×16, 48×48, 128×128 icons.

## 2026-10-02: Batch Link Grabber & Kurdish Sorani / Persian Multilingual Architecture
- **Batch "Download All Links" Grabber**:
  - **In-Page Extension Grabber (`extension/sniffer.js` & `extension/background.js`)**:
    - Discovers all anchor and media links on the active webpage, automatically deduping and classifying by file extension (Archives, Videos, Audio, Documents, Programs, Images).
    - **Hide HTML Files Toggle**: Filters out `.html`, `.htm`, `.php`, `.asp`, `.aspx`, and generic web links to focus solely on downloadable media and binaries.
    - **Advanced Multi-Selection**: Fully supports Shift+Click for contiguous range selections and Ctrl+Click (Cmd+Click) for discrete multi-selection.
    - **Category Auto-Routing vs Custom Folder**: Allows users to either automatically route downloads into their respective category folders (`downloads/FoxLoader/{Category}`) or specify a single destination folder for the entire batch.
    - **Queue Targeting**: Directly assigns the batch to the user's selected queue ("Main Queue", "Night Queue").
  - **Desktop Webpage Link Grabber (`windows/MainPage.xaml` & `windows/MainPage.xaml.cs`)**:
    - Enhanced `BatchAddDialog` with a webpage link grabber bar (`BatchWebpageUrlBox`), "Grab Links" button (`GrabLinksFromWebpage_Click`), "Hide HTML files" checkbox (`HideHtmlCheckBox`), and category routing options (`BatchRoutingRadio`).
- **Full Multilingual Localization Engine (Persian & Kurdish Sorani)**:
  - **Desktop Engine (`windows/Localization/LanguageManager.cs`)**:
    - Built a dynamic localization manager supporting English (`en`), Persian / پارسی (`fa`), and Kurdish Sorani / کوردی سۆرانی (`ku`).
    - Loads localized strings from `windows/Localization/{en,fa,ku}.json` with automatic fallback to English defaults.
    - Persists user language choice in `%USERPROFILE%/.FoxLoader/language.cfg`.
    - **Dynamic RTL Layout Mirroring**: Automatically toggles `FlowDirection = RightToLeft` on WinUI 3 pages for Persian and Kurdish, neatly mirroring the NavigationView pane and aligning text.
    - Integrated language selector in `windows/SettingsControl.xaml` under General settings.
  - **Extension Multilingual UI (`extension/popup/` & `extension/sniffer.js`)**:
    - Added quick language selector pills (`EN`, `فا`, `کوردی`) in the popup header and in-page grabber modal.
    - Supports instant RTL (`dir="rtl"`) text direction, typography adjustments, and translated labels across popup cards, status tags, and dialog actions.

## 2026-10-02: Comprehensive System Audit, Security Hardening & UX Layout Anchoring
- **Security Audit & Vulnerability Remediation**:
  - **Path Traversal & Arbitrary Overwrite Protection (CWE-22 / CWE-23)**: Implemented `sanitize_filename` in `TransactionalFileSaver` (`core/src/storage/mod.rs`). Strips relative directory traversal tokens (`..`, `/`, `\`), neutralizes Windows forbidden characters (`< > : " / \ | ? *`), and prefixes reserved device stems (`CON`, `PRN`, `AUX`, `NUL`, etc.) to guarantee all file operations remain strictly inside the user's download directory.
  - **DNS Rebinding & Web-to-Localhost Shield**: Implemented `security_headers_middleware` in Axum router (`core/src/server/mod.rs`). Enforces strict `Host` validation against `127.0.0.1` and `localhost`. Analyzes incoming `Origin` headers on cross-origin requests; rejects unauthorized internet origins (403 Forbidden) while permitting browser extension callers (`chrome-extension://*`, `moz-extension://*`) and desktop client requests.
- **Dynamic Port Architecture & Cross-Component Synchronization**:
  - Removed hardcoded port `2764` across all 20+ desktop client endpoints and browser extension scripts.
  - Introduced `FoxLoaderConfig` (`windows/FoxLoaderConfig.cs`) to dynamically resolve and update the active port from `settings.json`.
  - Updated Rust core `start_server` to dynamically bind to the user-configured port with safe fallback to `2764` if unavailable.
  - Updated browser extension `manifest.json` host permissions to wildcard `http://127.0.0.1/*` and wired `chrome.storage.local` port synchronization in `background.js` and `popup.js`.
- **AddDownloadWindow UI/UX Pinned Anchoring Overhaul**:
  - Addressed layout displacement in `AddDownloadWindow.xaml`: previously, when multi-part archive lists (10+ parts) or series episode ranges expanded, the destination folder input, browse button, and action controls were pushed below the fold.
  - Structured the window with fixed rows: Address/Name at top, dynamic intelligence cards in a middle `ScrollViewer`, and Destination path, Category, Queue, and Action Buttons (`Start Download`, `Download Later`, `Cancel`) permanently anchored at the bottom.
  - Increased DPI-aware window baseline dimensions to 680×640 for generous whitespace and clarity.

## 2026-10-02: Settings Persistence, Per-Host Rules Engine & Live Theme Architecture
- **Root-Cause Resolution for Settings Reversion**:
  - **Rust Backend Axum 422 Fix**:
    - Discovered that `AppSettings` in `core/src/settings.rs` lacked `#[serde(default)]`. Whenever the desktop client serialized a subset of settings without `per_host_settings`, `queues`, or `categories`, Axum rejected the payload with HTTP 422 Unprocessable Entity, which was caught silently and caused settings to never reach disk.
    - Added `#[serde(default)]` to `AppSettings` and introduced `UiSettings` (`theme`, `show_icon_labels`, `notification_sounds`, `speed_unit`, `start_on_boot`, `minimize_to_tray`, `close_to_tray`, `language`).
  - **Dual-Storage Persistence System (`windows/SettingsStorage.cs`)**:
    - Engineered dual persistence: all settings edits are written directly to `settings.json` locally on disk (project root and `%USERPROFILE%/.FoxLoader/settings.json`) AND synchronized live via HTTP POST `/settings`.
    - If the desktop app is closed and restarted (`dotnet run`), settings are instantly reloaded from disk with zero reversion to defaults.
- **Per-Host Rules System Completion (`windows/SettingsControl.xaml` & `SettingsControl.xaml.cs`)**:
  - Implemented the missing `SaveHostRule_Click` handler.
  - Added full domain normalization, validation, and credentials/thread limit assignment.
  - Added `HostRulesListView` with dedicated `Edit` and `Delete` actions, giving users instant visual confirmation of configured rules.
- **Dynamic Live Theme Switching Engine (`windows/ThemeManager.cs`)**:
  - Implemented `ThemeManager` supporting `System Settings`, `Dark`, `Light`, `Dark Teal`, and `Light Teal`.
  - Wired `ThemeComboBox_SelectionChanged` to update `RequestedTheme` on `App.MainWindowInstance.Content` and dynamically inject curated teal accent brush resources (`SystemAccentColor`, `AccentFillColorDefaultBrush`, `AccentButtonBackground`).
  - Integrated theme application into `MainWindow.xaml.cs` and `AddDownloadWindow.xaml.cs` to ensure unified appearance across all windows.
- **Core Process Discovery & Lifecycle Enhancement (`windows/App.xaml.cs`)**:
  - Enhanced `App.xaml.cs` to discover `ermiya-core.exe` across candidate development and release directories, set the working directory strictly to the project root, and check if an existing instance is already running before spawning.

## 2026-10-03: Feature Parity Overhaul, Crash Fixes & Advanced Transfer Mechanics
- **Root Cause & Resolution for Options/Scheduler White Screen & Blank Settings View**:
  - In `windows/SettingsControl.xaml.cs`, `RefreshHostRulesList()` attempted direct lookup on `Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]`. Because WinUI 3 stores theme brushes in `ThemeDictionaries` rather than root resource dictionaries, this threw an unhandled `KeyNotFoundException` during initial `LoadSettings()`, freezing the XAML visual tree into a white/blank state.
  - Resolved by using safe `TryGetValue` with fallback brush fallbacks. Re-aligned default theme to `"Dark"` in `settings.json` and `%USERPROFILE%\.FoxLoader\theme.cfg`.
- **Interactive Multi-Link Grabber (`BatchAllLinksDialog`)**:
  - Replaced the direct batch add behavior with an interactive link selection window matching IDM reference architecture (`idm_dl_all_links_page.png`).
  - Added `BatchInteractiveReq` state and endpoints (`POST /add_batch_interactive`, `GET /pop_batch_interactive`) in `core/src/manager.rs` and `core/src/server/mod.rs`.
  - Updated browser extension (`extension/background.js` and `extension/sniffer.js`) so clicking "Download all links on page with FoxLoader" crawls all `a[href]` links and streaming media, formatting URLs, file names, categories, and link text, and dispatches them to the desktop app.
  - Implemented `BatchAllLinksDialog` with live text filter, "Check All", "Uncheck All", "Hide HTML files", "Hide image files", "Hide repeated files", and destination routing (by category rules, specific category, or custom directory).
- **Multi-Selection & Batch Operation Controls**:
  - Configured `DownloadsList` with `SelectionMode="Extended"` and added `DownloadsList_KeyDown` handling `Delete` (prompts batch removal), `Space` (toggles batch pause/resume), and `Ctrl+A`.
  - Created `GetSelectedDownloads` utility. All toolbar buttons (`Resume`, `Pause`, `Remove`) and context menu items now execute across all selected downloads simultaneously.
  - Enhanced delete confirmation dialog to display total selected item count and allow optional collective disk deletion.
- **Sequential Wildcard Batch Generator (`BatchWildcardDialog`)**:
  - Implemented asterisk wildcard batch generator matching `idm_batch_dl.png`.
  - Supports numeric sequences (with start, end, and wildcard digit padding e.g. `001`, `002`) and alphabetical sequences (`a-z`), complete with real-time preview of first, second, and last file URLs.
- **Dedicated Single-Download Status Window (`DownloadProgressWindow.xaml`)**:
  - Double-clicking any item in the downloads list opens a dedicated, DPI-aware status window matching `idm_download_file_window.png`.
  - Includes a segmented visual progress bar rendering individual download chunk ratios, real-time EMA speed calculation, connection threads list, and three tabs:
    - *Download status*: live metrics, transfer stats, resume capability.
    - *Speed limiter*: configurable bandwidth throttle.
    - *Options on completion*: automated actions upon finish (open file, open directory, exit application, sleep/shutdown computer).
- **Export & Import Engine (`ExportListDialog`)**:
  - Integrated list export supporting three scopes: download queue, selected items, or all files, exporting to clean plain-text URL lists matching `idm_exported_file.txt`.
  - Implemented import functionality reading plain-text and `<URL>` formatted files directly into the batch download queue.
- **Interactive Column Sorting**:
  - Added click handlers to column headers (`FileName`, `Size`, `Status`, `TimeLeft`, `TransferRate`, `LastTry`).
  - Toggles ascending/descending with visual glyphs (▲/▼) and updates the observable collection using an in-place sort that preserves selection and avoids list recreation.
- **Settings UI Audit & Field Verification**:
  - Audited against reference screenshots in `docs/idm_setting/`.
  - **Deliberate Skips**: Obsolete dial-up modem redialing / telephone networking settings were intentionally excluded as they provide no value in modern network architectures.
  - Added `file_types` (editable list of auto-intercepted file extensions) and `whitelist_domains` (hosts to bypass interception) to core schema and UI, with verified cross-session persistence.



## 2026-10-08: Application Renaming to FoxLoader
- Renamed all user-facing names from 'Ermiya Download Manager', 'Ermiya DM', and 'EDM' to 'FoxLoader' across the codebase.
- Retained 'ermiya_core' as the internal backend engine name.

## 2026-10-08: Logo Background Removal & Multi-Platform Asset Generation
- Extracted user's official geometric Fox emblem with subpixel mathematical alpha deconvolution, eliminating solid black background noise and halos.
- Centered emblem on transparent 1080x1080 master canvas with subtle ambient contrast silhouette to guarantee legibility across both dark and light modes.
- Generated complete suite of Windows and browser extension assets:
  - Windows app icons (`windows/Assets/AppIcon.ico`, `windows/Assets/logo.ico`) with multi-resolution mipmaps (16x16, 24x24, 32x32, 48x48, 64x64, 128x128, 256x256).
  - WinUI 3 manifest tiles (`Square150x150Logo`, `Square44x44Logo`, unplated target sizes, `StoreLogo`, `Wide310x150Logo`, `SplashScreen`, `LockScreenLogo`).
  - Browser extension icons (`extension/icons/icon-16.png`, `icon-48.png`, `icon-128.png`, `extension/icon.png`).
  - Root and release packages (`logo.png`, `ReleaseOutput/logo.png`, `ReleaseOutput/extension/`).
- Verified zero-warning WinUI build compilation (`dotnet build`).

## 2026-10-08: Streamlined Root .gitignore
- Removed unnecessary and obsolete entries from `.gitignore`:
  - Removed `Cargo.lock` (must be tracked for Rust binary applications to ensure deterministic dependencies).
  - Consolidated redundant `target/`, `target_build/`, `target_cli/`, `target_rel/` into `target*/`.
  - Removed obsolete backup/IDE artifacts (`**/*.rs.bk`, `*.userosscache`, `*.sln.docstates`).
- Added missing runtime and OS ignore rules (`*.log`, `.vs/`, `Thumbs.db`, `desktop.ini`, `.DS_Store`).

