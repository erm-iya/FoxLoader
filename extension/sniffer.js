// FoxLoader - Advanced Media Sniffer & In-Page Link Grabber
(function () {
    const capturFoxLoaderediaMap = new Map();
    let isSnifferEnabled = true;

    // Load initial settings
    chrome.storage.local.get(["snifferEnabled"], (res) => {
        if (res && res.snifferEnabled === false) {
            isSnifferEnabled = false;
        }
    });

    chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
        if (msg.action === "sniffer_toggle_updated") {
            isSnifferEnabled = msg.enabled;
            if (!isSnifferEnabled) {
                document.querySelectorAll('.FoxLoader-floating-badge').forEach(el => el.remove());
            } else {
                scanMediaElements();
            }
        } else if (msg.action === "get_captured_media") {
            scanMediaElements();
            sendResponse({ media: Array.from(capturFoxLoaderediaMap.values()) });
            return true;
        } else if (msg.action === "collect_all_links_for_FoxLoader" || msg.action === "open_batch_modal" || msg.action === "collect_all_links") {
            const links = extractAllLinksFromPage();
            chrome.runtime.sendMessage({
                action: "batch_interactive_ready",
                page_url: window.location.href,
                page_title: document.title,
                links: links
            }).catch(() => {});
            sendResponse({ success: true, links: links });
            return true;
        }
    });

    function extractAllLinksFromPage() {
        const rawLinks = Array.from(document.querySelectorAll('a[href]'));
        const discovered = [];
        const seen = new Set();

        for (const a of rawLinks) {
            const href = a.getAttribute('href');
            if (!href || href.startsWith('#') || href.startsWith('javascript:') || href.startsWith('mailto:')) continue;

            let absUrl = "";
            try {
                absUrl = new URL(href, window.location.href).href;
            } catch {
                continue;
            }

            if (seen.has(absUrl)) continue;
            seen.add(absUrl);

            let fileName = "file";
            try {
                const u = new URL(absUrl);
                fileName = u.pathname.split('/').pop() || "file";
                fileName = decodeURIComponent(fileName.split('?')[0]);
            } catch {}

            const ext = fileName.includes('.') ? ('.' + fileName.split('.').pop().toLowerCase()) : "";
            let fileType = "Document";
            if (['.jpg', '.jpeg', '.png', '.gif', '.webp', '.svg', '.bmp'].includes(ext)) fileType = "Image";
            else if (['.zip', '.rar', '.7z', '.tar', '.gz', '.iso'].includes(ext)) fileType = "Compressed";
            else if (['.mp4', '.mkv', '.avi', '.mov', '.webm', '.flv'].includes(ext)) fileType = "Video";
            else if (['.mp3', '.wav', '.flac', '.ogg', '.m4a'].includes(ext)) fileType = "Music";
            else if (['.exe', '.msi', '.apk', '.dmg', '.pkg', '.deb'].includes(ext)) fileType = "Program";
            else if (['.html', '.htm', '.php', '.asp', '.aspx'].includes(ext) || !ext) fileType = "HTML Page";

            const linkText = (a.innerText || a.getAttribute('title') || fileName).trim().replace(/\s+/g, ' ');

            discovered.push({
                url: absUrl,
                file_name: fileName,
                file_type: fileType,
                link_text: linkText
            });
        }

        for (const [mUrl, mInfo] of capturFoxLoaderediaMap.entries()) {
            if (!seen.has(mUrl)) {
                seen.add(mUrl);
                discovered.push({
                    url: mUrl,
                    file_name: (mInfo.title || "media") + "." + (mInfo.ext || "mp4"),
                    file_type: "Video",
                    link_text: mInfo.title || "Media Stream"
                });
            }
        }

        return discovered;
    }

    function cleanUrl(rawUrl) {
        if (!rawUrl) return "";
        try {
            return new URL(rawUrl, window.location.href).href;
        } catch {
            return rawUrl;
        }
    }

    function extractMediaInfo(srcUrl, element) {
        const fullUrl = cleanUrl(srcUrl);
        if (!fullUrl || fullUrl.startsWith('data:') || (fullUrl.startsWith('blob:') && !fullUrl.includes('m3u8'))) {
            return null;
        }

        let ext = "mp4";
        try {
            const path = new URL(fullUrl).pathname.toLowerCase();
            if (path.endsWith('.m3u8')) ext = "m3u8";
            else if (path.endsWith('.mpd')) ext = "mpd";
            else if (path.endsWith('.webm')) ext = "webm";
            else if (path.endsWith('.mkv')) ext = "mkv";
            else if (path.endsWith('.mp3')) ext = "mp3";
            else if (path.endsWith('.wav')) ext = "wav";
            else if (path.endsWith('.flac')) ext = "flac";
            else if (path.endsWith('.ogg')) ext = "ogg";
            else if (element && element.tagName === 'AUDIO') ext = "mp3";
        } catch {}

        let title = document.title.replace(/[\\/:*?"<>|]/g, '').trim() || "media";
        if (title.length > 50) title = title.substring(0, 50);

        return {
            url: fullUrl,
            title: title,
            ext: ext,
            size: element?.duration ? `${Math.round(element.duration)}s` : ""
        };
    }

    function registerMedia(info, videoEl) {
        if (!info || capturFoxLoaderediaMap.has(info.url)) return;
        capturFoxLoaderediaMap.set(info.url, info);

        // Notify background to update extension badge
        chrome.runtime.sendMessage({
            action: "media_detected",
            count: capturFoxLoaderediaMap.size,
            url: info.url
        }).catch(() => {});

        if (isSnifferEnabled && videoEl) {
            injectFloatingBadge(videoEl, info);
        }
    }

    function injectFloatingBadge(videoEl, mediaInfo) {
        if (!videoEl || !videoEl.parentElement) return;

        let existing = videoEl.parentElement.querySelector('.FoxLoader-floating-badge');
        if (existing) return;

        const badge = document.createElement('div');
        badge.className = 'FoxLoader-floating-badge';
        badge.style.cssText = `
            position: absolute !important;
            top: 12px !important;
            right: 12px !important;
            z-index: 2147483640 !important;
            background: rgba(15, 23, 42, 0.9) !important;
            border: 1px solid rgba(0, 180, 216, 0.6) !important;
            color: #ffffff !important;
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif !important;
            font-size: 12px !important;
            font-weight: 600 !important;
            padding: 6px 12px !important;
            border-radius: 8px !important;
            box-shadow: 0 4px 16px rgba(0, 0, 0, 0.4), 0 0 10px rgba(0, 180, 216, 0.3) !important;
            cursor: pointer !important;
            display: flex !important;
            align-items: center !important;
            gap: 7px !important;
            transition: all 0.25s ease !important;
            backdrop-filter: blur(8px) !important;
            opacity: 0.88 !important;
            user-select: none !important;
        `;

        badge.innerHTML = `
            <svg width="14" height="14" viewBox="0 0 24 24" fill="#00B4D8">
                <path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/>
            </svg>
            <span style="color:#F8FAFC;">Download (${mediaInfo.ext.toUpperCase()})</span>
        `;

        badge.addEventListener('mouseenter', () => {
            badge.style.transform = 'scale(1.05)';
            badge.style.opacity = '1';
            badge.style.borderColor = '#00B4D8';
        });

        badge.addEventListener('mouseleave', () => {
            badge.style.transform = 'scale(1)';
            badge.style.opacity = '0.88';
        });

        badge.addEventListener('click', (e) => {
            e.stopPropagation();
            e.preventDefault();
            chrome.runtime.sendMessage({
                action: "download_media",
                url: mediaInfo.url,
                title: `${mediaInfo.title}.${mediaInfo.ext}`
            });
        });

        const parentPos = window.getComputedStyle(videoEl.parentElement).position;
        if (parentPos === 'static') {
            videoEl.parentElement.style.position = 'relative';
        }
        videoEl.parentElement.appendChild(badge);
    }

    function scanMediaElements() {
        const mediaNodes = document.querySelectorAll('video, audio');
        mediaNodes.forEach(node => {
            const src = node.currentSrc || node.src;
            if (src) {
                const info = extractMediaInfo(src, node);
                if (info) registerMedia(info, node);
            }

            node.querySelectorAll('source').forEach(s => {
                if (s.src) {
                    const info = extractMediaInfo(s.src, node);
                    if (info) registerMedia(info, node);
                }
            });
        });
    }

    // Monitor DOM mutations
    const observer = new MutationObserver(() => {
        scanMediaElements();
    });

    observer.observe(document.body || document.documentElement, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ['src']
    });

    scanMediaElements();

    // -------------------------------------------------------------
    // Full Interactive In-Page Batch Link Grabber Modal
    // -------------------------------------------------------------
    const GRABBER_I18N = {
        en: {
            title: "FoxLoader Link Grabber",
            discovered: "Discovered {0} links on this page",
            searchPlaceholder: "Filter by filename or domain...",
            selectAll: "Select All",
            deselectAll: "Deselect All",
            hideHtml: "Hide HTML files (.html, .htm, .php, .asp)",
            all: "All",
            archives: "Archives",
            videos: "Videos",
            audio: "Audio",
            documents: "Documents",
            programs: "Programs",
            images: "Images",
            selectedCount: "{0} items selected",
            routeCategory: "Auto-route by Category (Music, Video, Compressed, Programs...)",
            saveCustom: "Save all files to folder:",
            cancel: "Cancel",
            download: "Download with FoxLoader",
            noLinks: "No links matching the current filters."
        },
        fa: {
            title: "استخراج لینک‌های صفحه (FoxLoader Grabber)",
            discovered: "تعداد {0} لینک در این صفحه پیدا شد",
            searchPlaceholder: "جستجو بر اساس نام فایل یا دامنه...",
            selectAll: "انتخاب همه",
            deselectAll: "لغو انتخاب همه",
            hideHtml: "مخفی‌سازی صفحات وب (.html, .php, .asp)",
            all: "همه",
            archives: "فشرده",
            videos: "ویدیوها",
            audio: "صوت و موزیک",
            documents: "اسناد",
            programs: "برنامه‌ها",
            images: "تصاویر",
            selectedCount: "{0} فایل انتخاب شده",
            routeCategory: "تفکیک خودکار در پوشه دسته‌بندی‌ها (موزیک، ویدیو، فشرده...)",
            saveCustom: "ذخیره تمام فایل‌ها در پوشه مشخص:",
            cancel: "انصراف",
            download: "دانلود با FoxLoader",
            noLinks: "هیچ لینکی مطابق فیلتر فعلی یافت نشد."
        },
        ku: {
            title: "دەرهێنەری بەستەرەکان (FoxLoader Grabber)",
            discovered: "{0} بەستەر لەم پەڕەیەدا دۆزرایەوە",
            searchPlaceholder: "گەڕان بەپێی ناوی پەڕگە یان ماڵپەڕ...",
            selectAll: "دیاریکردنی هەموو",
            deselectAll: "لابردنی دیاریکراوەکان",
            hideHtml: "شاردنەوەی پەڕەکانی وێب (.html, .php, .asp)",
            all: "هەموو",
            archives: "پەستێنراو",
            videos: "ڤیدیۆکان",
            audio: "مۆسیقا و دەنگ",
            documents: "بەڵگەنامەکان",
            programs: "بەرنامەکان",
            images: "وێنەکان",
            selectedCount: "{0} پەڕگە دیاریکراوە",
            routeCategory: "دابەشکردنی خودکار بەپێی پۆلەکان (مۆسیقا، ڤیدیۆ...)",
            saveCustom: "پاشەکەوتکردنی هەموو لەم بوخچەیەدا:",
            cancel: "هەڵوەشاندنەوە",
            download: "داگرتن لەگەڵ FoxLoader",
            noLinks: "هیچ بەستەرێک نەدۆزرایەوە."
        }
    };

    function openBatchGrabberModal() {
        let existingModal = document.getElementById('FoxLoader-batch-grabber-overlay');
        if (existingModal) {
            existingModal.remove();
        }

        let currentLang = "en";
        // Check saved language
        chrome.storage.local.get(["language"], (res) => {
            if (res && res.language && GRABBER_I18N[res.language]) {
                currentLang = res.language;
            }
        });

        // Collect all links on page
        const rawLinks = Array.from(document.querySelectorAll('a[href], area[href]'))
            .map(a => {
                try {
                    return {
                        url: new URL(a.href, window.location.href).href,
                        text: (a.textContent || a.title || "").trim()
                    };
                } catch {
                    return null;
                }
            })
            .filter(item => item && (item.url.startsWith('http://') || item.url.startsWith('https://')));

        // Deduplicate by URL
        const seenUrls = new Set();
        const linkItems = [];
        for (const item of rawLinks) {
            if (!seenUrls.has(item.url)) {
                seenUrls.add(item.url);

                let fileName = "file";
                try {
                    const parsed = new URL(item.url);
                    fileName = parsed.pathname.split('/').pop() || "file";
                    fileName = decodeURIComponent(fileName.split('?')[0]);
                } catch {}

                let ext = fileName.includes('.') ? fileName.split('.').pop().toLowerCase() : "";
                let category = "other";
                let isHtml = !ext || ['html', 'htm', 'php', 'asp', 'aspx', 'jsp', 'cgi'].includes(ext);

                if (['zip', 'rar', '7z', 'tar', 'gz', 'iso'].includes(ext)) category = "archive";
                else if (['mp4', 'mkv', 'avi', 'mov', 'webm', 'flv'].includes(ext)) category = "video";
                else if (['mp3', 'wav', 'flac', 'ogg', 'm4a'].includes(ext)) category = "audio";
                else if (['pdf', 'doc', 'docx', 'xls', 'xlsx', 'ppt', 'pptx', 'txt'].includes(ext)) category = "document";
                else if (['exe', 'msi', 'apk', 'dmg', 'iso'].includes(ext)) category = "program";
                else if (['png', 'jpg', 'jpeg', 'webp', 'gif', 'svg'].includes(ext)) category = "image";

                linkItems.push({
                    url: item.url,
                    fileName: fileName,
                    text: item.text,
                    ext: ext,
                    category: category,
                    isHtml: isHtml,
                    selected: !isHtml && category !== "other"
                });
            }
        }

        // Modal DOM
        const overlay = document.createElement('div');
        overlay.id = 'FoxLoader-batch-grabber-overlay';
        overlay.style.cssText = `
            position: fixed !important;
            top: 0 !important;
            left: 0 !important;
            width: 100vw !important;
            height: 100vh !important;
            background: rgba(11, 15, 25, 0.75) !important;
            backdrop-filter: blur(12px) !important;
            z-index: 2147483647 !important;
            display: flex !important;
            align-items: center !important;
            justify-content: center !important;
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif !important;
            box-sizing: border-box !important;
        `;

        const card = document.createElement('div');
        card.id = 'FoxLoader-grabber-card';
        card.style.cssText = `
            width: 820px !important;
            max-width: 95vw !important;
            height: 660px !important;
            max-height: 92vh !important;
            background: #0F172A !important;
            border: 1px solid rgba(0, 180, 216, 0.35) !important;
            border-radius: 16px !important;
            box-shadow: 0 20px 50px rgba(0, 0, 0, 0.6), 0 0 25px rgba(0, 180, 216, 0.2) !important;
            display: flex !important;
            flex-direction: column !important;
            overflow: hidden !important;
            color: #F8FAFC !important;
        `;

        card.innerHTML = `
            <!-- Modal Header -->
            <div style="display:flex; justify-content:space-between; align-items:center; padding:14px 20px; border-bottom:1px solid rgba(255,255,255,0.08); background:rgba(30,41,59,0.5);">
                <div style="display:flex; align-items:center; gap:10px;">
                    <div style="width:28px; height:28px; background:#00B4D8; border-radius:6px; display:flex; align-items:center; justify-content:center; font-weight:bold; color:#0F172A;">E</div>
                    <div>
                        <div id="FoxLoader-txt-title" style="font-size:15px; font-weight:700;">FoxLoader Link Grabber</div>
                        <div id="FoxLoader-txt-discovered" style="font-size:11px; color:#94A3B8;">Discovered ${linkItems.length} links on this page</div>
                    </div>
                </div>
                <div style="display:flex; align-items:center; gap:12px;">
                    <!-- Language Switcher -->
                    <div style="display:flex; background:rgba(255,255,255,0.06); border-radius:6px; padding:2px;">
                        <button class="FoxLoader-lang-btn" data-lang="en" style="border:none; background:#00B4D8; color:#0F172A; font-weight:700; font-size:10px; padding:2px 8px; border-radius:4px; cursor:pointer;">EN</button>
                        <button class="FoxLoader-lang-btn" data-lang="fa" style="border:none; background:transparent; color:#94A3B8; font-size:10px; padding:2px 8px; border-radius:4px; cursor:pointer;">فا</button>
                        <button class="FoxLoader-lang-btn" data-lang="ku" style="border:none; background:transparent; color:#94A3B8; font-size:10px; padding:2px 8px; border-radius:4px; cursor:pointer;">کوردی</button>
                    </div>
                    <button id="FoxLoader-modal-close-btn" style="background:transparent; border:none; color:#94A3B8; font-size:20px; cursor:pointer; padding:4px 8px; border-radius:6px; line-height:1;">✕</button>
                </div>
            </div>

            <!-- Controls: Filter & Search -->
            <div style="padding:12px 20px; display:flex; flex-direction:column; gap:10px; background:rgba(15,23,42,0.8); border-bottom:1px solid rgba(255,255,255,0.06);">
                <div style="display:flex; gap:10px; align-items:center;">
                    <input type="text" id="FoxLoader-search-input" placeholder="Filter by filename or domain..." style="flex:1; background:#1E293B; border:1px solid rgba(255,255,255,0.1); border-radius:8px; padding:7px 12px; color:#FFF; font-size:12px; outline:none;" />
                    <button id="FoxLoader-select-all-btn" style="background:rgba(255,255,255,0.08); border:none; border-radius:6px; color:#E2E8F0; padding:6px 12px; font-size:11.5px; cursor:pointer;">Select All</button>
                    <button id="FoxLoader-deselect-all-btn" style="background:rgba(255,255,255,0.08); border:none; border-radius:6px; color:#E2E8F0; padding:6px 12px; font-size:11.5px; cursor:pointer;">Deselect All</button>
                    <label style="display:flex; align-items:center; gap:6px; font-size:11.5px; color:#94A3B8; cursor:pointer; user-select:none; margin-left:6px;">
                        <input type="checkbox" id="FoxLoader-hide-html-chk" checked style="accent-color:#00B4D8;" />
                        <span id="FoxLoader-txt-hidehtml">Hide HTML files</span>
                    </label>
                </div>

                <!-- Category Pills -->
                <div id="FoxLoader-category-pills" style="display:flex; gap:6px; flex-wrap:wrap;">
                    <button class="FoxLoader-pill active" data-cat="all" style="padding:3px 10px; font-size:11px; border-radius:12px; border:1px solid #00B4D8; background:rgba(0,180,216,0.2); color:#00B4D8; cursor:pointer;">All</button>
                    <button class="FoxLoader-pill" data-cat="archive" style="padding:3px 10px; font-size:11px; border-radius:12px; border:1px solid rgba(255,255,255,0.1); background:transparent; color:#94A3B8; cursor:pointer;">Archives</button>
                    <button class="FoxLoader-pill" data-cat="video" style="padding:3px 10px; font-size:11px; border-radius:12px; border:1px solid rgba(255,255,255,0.1); background:transparent; color:#94A3B8; cursor:pointer;">Videos</button>
                    <button class="FoxLoader-pill" data-cat="audio" style="padding:3px 10px; font-size:11px; border-radius:12px; border:1px solid rgba(255,255,255,0.1); background:transparent; color:#94A3B8; cursor:pointer;">Audio</button>
                    <button class="FoxLoader-pill" data-cat="document" style="padding:3px 10px; font-size:11px; border-radius:12px; border:1px solid rgba(255,255,255,0.1); background:transparent; color:#94A3B8; cursor:pointer;">Documents</button>
                    <button class="FoxLoader-pill" data-cat="program" style="padding:3px 10px; font-size:11px; border-radius:12px; border:1px solid rgba(255,255,255,0.1); background:transparent; color:#94A3B8; cursor:pointer;">Programs</button>
                    <button class="FoxLoader-pill" data-cat="image" style="padding:3px 10px; font-size:11px; border-radius:12px; border:1px solid rgba(255,255,255,0.1); background:transparent; color:#94A3B8; cursor:pointer;">Images</button>
                </div>
            </div>

            <!-- Links Table / Scrollable List -->
            <div id="FoxLoader-links-container" style="flex:1; overflow-y:auto; padding:8px 20px; display:flex; flex-direction:column; gap:4px;">
            </div>

            <!-- Destination & Routing Options -->
            <div style="padding:10px 20px; background:rgba(15,23,42,0.9); border-top:1px solid rgba(255,255,255,0.06); display:flex; flex-direction:column; gap:6px; font-size:11.5px;">
                <label style="display:flex; align-items:center; gap:8px; cursor:pointer;">
                    <input type="radio" name="FoxLoader_save_mode" id="FoxLoader-route-category-radio" checked style="accent-color:#00B4D8;" />
                    <span id="FoxLoader-txt-route-cat">Auto-route by Category (Music, Video, Compressed, Programs...)</span>
                </label>
                <div style="display:flex; align-items:center; gap:8px;">
                    <label style="display:flex; align-items:center; gap:8px; cursor:pointer;">
                        <input type="radio" name="FoxLoader_save_mode" id="FoxLoader-route-custom-radio" style="accent-color:#00B4D8;" />
                        <span id="FoxLoader-txt-save-custom">Save all files to folder:</span>
                    </label>
                    <input type="text" id="FoxLoader-custom-folder-input" placeholder="e.g. C:\\Downloads\\BatchProject" style="flex:1; max-width:280px; background:#1E293B; border:1px solid rgba(255,255,255,0.1); border-radius:4px; padding:3px 8px; color:#FFF; font-size:11px; outline:none;" />
                </div>
            </div>

            <!-- Modal Footer -->
            <div style="padding:12px 20px; background:rgba(30,41,59,0.7); border-top:1px solid rgba(255,255,255,0.08); display:flex; justify-content:space-between; align-items:center;">
                <div style="display:flex; align-items:center; gap:12px;">
                    <span style="font-size:12px; color:#94A3B8;"><strong id="FoxLoader-selected-count" style="color:#00B4D8;">0</strong> <span id="FoxLoader-txt-selected-suffix">items selected</span></span>
                    <select id="FoxLoader-queue-select" style="background:#1E293B; border:1px solid rgba(255,255,255,0.1); color:#FFF; font-size:11.5px; border-radius:6px; padding:4px 8px; outline:none;">
                        <option value="Main Queue">Main Queue</option>
                        <option value="Night Queue">Night Queue</option>
                    </select>
                </div>
                <div style="display:flex; gap:10px;">
                    <button id="FoxLoader-cancel-btn" style="background:rgba(255,255,255,0.08); border:none; border-radius:8px; color:#E2E8F0; padding:7px 16px; font-size:12px; cursor:pointer;">Cancel</button>
                    <button id="FoxLoader-download-selected-btn" style="background:linear-gradient(135deg, #00B4D8 0%, #0077B6 100%); border:none; border-radius:8px; color:#FFF; font-weight:600; padding:7px 20px; font-size:12px; cursor:pointer; box-shadow:0 2px 10px rgba(0,180,216,0.35);">Download with FoxLoader</button>
                </div>
            </div>
        `;

        overlay.appendChild(card);
        document.body.appendChild(overlay);

        let activeCategory = "all";
        let searchQuery = "";
        let hideHtml = true;
        let lastClickedIndex = -1;

        const linksContainer = document.getElementById('FoxLoader-links-container');
        const selectedCountEl = document.getElementById('FoxLoader-selected-count');
        const searchInput = document.getElementById('FoxLoader-search-input');
        const selectAllBtn = document.getElementById('FoxLoader-select-all-btn');
        const deselectAllBtn = document.getElementById('FoxLoader-deselect-all-btn');
        const hideHtmlChk = document.getElementById('FoxLoader-hide-html-chk');
        const closeBtn = document.getElementById('FoxLoader-modal-close-btn');
        const cancelBtn = document.getElementById('FoxLoader-cancel-btn');
        const downloadBtn = document.getElementById('FoxLoader-download-selected-btn');
        const queueSelect = document.getElementById('FoxLoader-queue-select');
        const routeCategoryRadio = document.getElementById('FoxLoader-route-category-radio');
        const customFolderInput = document.getElementById('FoxLoader-custom-folder-input');

        function applyI18n(lang) {
            currentLang = lang;
            const t = GRABBER_I18N[lang] || GRABBER_I18N.en;
            const isRtl = lang === "fa" || lang === "ku";
            card.dir = isRtl ? "rtl" : "ltr";

            document.getElementById('FoxLoader-txt-title').textContent = t.title;
            document.getElementById('FoxLoader-txt-discovered').textContent = t.discovered.replace('{0}', linkItems.length);
            searchInput.placeholder = t.searchPlaceholder;
            selectAllBtn.textContent = t.selectAll;
            deselectAllBtn.textContent = t.deselectAll;
            document.getElementById('FoxLoader-txt-hidehtml').textContent = t.hideHtml;
            document.getElementById('FoxLoader-txt-route-cat').textContent = t.routeCategory;
            document.getElementById('FoxLoader-txt-save-custom').textContent = t.saveCustom;
            cancelBtn.textContent = t.cancel;
            downloadBtn.textContent = t.download;

            document.querySelectorAll('.FoxLoader-lang-btn').forEach(b => {
                if (b.dataset.lang === lang) {
                    b.style.background = '#00B4D8';
                    b.style.color = '#0F172A';
                    b.style.fontWeight = '700';
                } else {
                    b.style.background = 'transparent';
                    b.style.color = '#94A3B8';
                    b.style.fontWeight = 'normal';
                }
            });

            chrome.storage.local.set({ language: lang });
        }

        document.querySelectorAll('.FoxLoader-lang-btn').forEach(btn => {
            btn.addEventListener('click', () => applyI18n(btn.dataset.lang));
        });

        applyI18n(currentLang);

        function renderRows() {
            linksContainer.innerHTML = "";
            let selectedCount = 0;

            const filtered = linkItems.filter(item => {
                if (hideHtml && item.isHtml) return false;
                if (activeCategory !== "all" && item.category !== activeCategory) return false;
                if (searchQuery) {
                    const matchName = item.fileName.toLowerCase().includes(searchQuery);
                    const matchUrl = item.url.toLowerCase().includes(searchQuery);
                    if (!matchName && !matchUrl) return false;
                }
                return true;
            });

            linkItems.forEach(item => {
                if (item.selected) selectedCount++;
            });
            selectedCountEl.textContent = selectedCount;

            if (filtered.length === 0) {
                const t = GRABBER_I18N[currentLang] || GRABBER_I18N.en;
                linksContainer.innerHTML = `
                    <div style="text-align:center; padding:40px 20px; color:#64748B; font-size:12.5px;">
                        ${t.noLinks}
                    </div>
                `;
                return;
            }

            filtered.forEach((item, index) => {
                const row = document.createElement('div');
                row.className = 'FoxLoader-grabber-row';
                row.style.cssText = `
                    display:flex;
                    align-items:center;
                    gap:10px;
                    padding:7px 10px;
                    border-radius:6px;
                    background: ${item.selected ? 'rgba(0,180,216,0.08)' : 'rgba(30,41,59,0.3)'};
                    border:1px solid ${item.selected ? 'rgba(0,180,216,0.3)' : 'rgba(255,255,255,0.04)'};
                    cursor:pointer;
                    transition:all 0.15s ease;
                    user-select:none;
                `;

                const checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.checked = item.selected;
                checkbox.style.accentColor = '#00B4D8';

                // Keyboard handling: Shift+Click range & Ctrl+Click multi-select
                row.addEventListener('click', (e) => {
                    const currentIndex = linkItems.indexOf(item);

                    if (e.shiftKey && lastClickedIndex !== -1) {
                        // Range selection
                        const start = Math.min(lastClickedIndex, currentIndex);
                        const end = Math.max(lastClickedIndex, currentIndex);
                        const targetState = !item.selected;

                        for (let i = start; i <= end; i++) {
                            linkItems[i].selected = targetState;
                        }
                    } else if (e.ctrlKey || e.metaKey) {
                        // Ctrl/Cmd toggle
                        item.selected = !item.selected;
                        lastClickedIndex = currentIndex;
                    } else {
                        // Normal click on row or checkbox
                        if (e.target !== checkbox) {
                            item.selected = !item.selected;
                        } else {
                            item.selected = checkbox.checked;
                        }
                        lastClickedIndex = currentIndex;
                    }

                    renderRows();
                });

                const infoBox = document.createElement('div');
                infoBox.style.cssText = "flex:1; overflow:hidden; display:flex; flex-direction:column; gap:2px;";
                infoBox.innerHTML = `
                    <div style="font-size:12px; font-weight:600; color:#F1F5F9; white-space:nowrap; overflow:hidden; text-overflow:ellipsis;">
                        ${item.fileName}
                    </div>
                    <div style="font-size:10px; color:#64748B; white-space:nowrap; overflow:hidden; text-overflow:ellipsis;">
                        ${item.url}
                    </div>
                `;

                const tag = document.createElement('span');
                tag.style.cssText = `
                    font-size:9.5px;
                    font-weight:700;
                    text-transform:uppercase;
                    padding:2px 6px;
                    border-radius:4px;
                    background:rgba(255,255,255,0.06);
                    color:#94A3B8;
                `;
                tag.textContent = item.ext || item.category;

                row.appendChild(checkbox);
                row.appendChild(infoBox);
                row.appendChild(tag);
                linksContainer.appendChild(row);
            });
        }

        renderRows();

        // Search listener
        searchInput.addEventListener('input', (e) => {
            searchQuery = e.target.value.toLowerCase().trim();
            renderRows();
        });

        // Hide HTML checkbox listener
        hideHtmlChk.addEventListener('change', () => {
            hideHtml = hideHtmlChk.checked;
            renderRows();
        });

        // Category pills listener
        document.querySelectorAll('#FoxLoader-category-pills .FoxLoader-pill').forEach(pill => {
            pill.addEventListener('click', () => {
                document.querySelectorAll('#FoxLoader-category-pills .FoxLoader-pill').forEach(p => {
                    p.style.borderColor = 'rgba(255,255,255,0.1)';
                    p.style.background = 'transparent';
                    p.style.color = '#94A3B8';
                });
                pill.style.borderColor = '#00B4D8';
                pill.style.background = 'rgba(0,180,216,0.2)';
                pill.style.color = '#00B4D8';

                activeCategory = pill.dataset.cat;
                renderRows();
            });
        });

        // Select / Deselect
        selectAllBtn.addEventListener('click', () => {
            linkItems.forEach(item => {
                if (hideHtml && item.isHtml) return;
                if (activeCategory === "all" || item.category === activeCategory) {
                    item.selected = true;
                }
            });
            renderRows();
        });

        deselectAllBtn.addEventListener('click', () => {
            linkItems.forEach(item => {
                if (activeCategory === "all" || item.category === activeCategory) {
                    item.selected = false;
                }
            });
            renderRows();
        });

        function closeModal() {
            overlay.remove();
        }

        closeBtn.addEventListener('click', closeModal);
        cancelBtn.addEventListener('click', closeModal);
        overlay.addEventListener('click', (e) => {
            if (e.target === overlay) closeModal();
        });

        // Submit to FoxLoader
        downloadBtn.addEventListener('click', () => {
            const selectedItems = linkItems.filter(i => i.selected);
            if (selectedItems.length === 0) {
                alert("Please select at least one link to download.");
                return;
            }

            const queue = queueSelect.value || "Main Queue";
            const isCategoryRouting = routeCategoryRadio.checked;
            const customFolder = customFolderInput.value.trim();

            const itemsPayload = selectedItems.map(item => {
                let savePath = "";
                if (!isCategoryRouting && customFolder) {
                    savePath = customFolder;
                }
                return {
                    url: item.url,
                    category: item.category,
                    save_path: savePath
                };
            });

            chrome.runtime.sendMessage({
                action: "batch_links_collected_advanced",
                items: itemsPayload,
                queue: queue,
                route_category: isCategoryRouting
            });

            closeModal();
        });
    }
})();
