let FoxLoaderCorePort = 2764;
let FoxLoader_CORE_URL = `http://127.0.0.1:${FoxLoaderCorePort}`;

const POPUP_I18N = {
  en: {
    statsTitleActive: (n) => `${n} active download${n > 1 ? "s" : ""}`,
    statsSubActive: "Monitoring speed in background",
    statsTitleIdle: "FoxLoader Engine Idle",
    statsSubIdle: (n) => `${n} total tasks in database`,
    openApp: "Open App",
    interceptLabel: "Intercept Downloads",
    interceptDesc: "Forward browser downloads to FoxLoader",
    snifferLabel: "Media & Video Sniffer",
    snifferDesc: "Show download button on video players",
    capturFoxLoaderedia: "Captured Media",
    rescan: "Rescan",
    noMedia: "No media captured on this page yet",
    noMediaHint: "Play a video or audio stream on this page to capture it with FoxLoader",
    downloadAll: "Download All Captured Media",
    grabAll: "Grab All Links",
    grabAllDesc: "Extract links from page",
    addCustom: "Add Custom URL",
    addCustomDesc: "Download any link",
    urlPlaceholder: "Paste URL here (http://...)",
    addBtn: "Add",
    excludeSite: "Exclude Site",
    excluded: "Excluded",
    excludeTitleActive: "FoxLoader is disabled on this site. Click to re-enable.",
    excludeTitleInactive: "Click to prevent FoxLoader from intercepting on this domain.",
    options: "Options",
    connecting: "Connecting...",
    connected: "Connected",
    offline: "Offline",
    download: "Download",
    internalPage: "Browser Internal Page"
  },
  fa: {
    statsTitleActive: (n) => `${n} دانلود فعال در حال انجام`,
    statsSubActive: "پایش سرعت دانلود در پس‌زمینه",
    statsTitleIdle: "موتور دانلود آماده‌به‌کار",
    statsSubIdle: (n) => `${n} فایل در پایگاه داده ثبت شده`,
    openApp: "باز کردن برنامه",
    interceptLabel: "رهگیری خودکار دانلودها",
    interceptDesc: "انتقال خودکار دانلودهای مرورگر به نرم‌افزار",
    snifferLabel: "شنودگر ویدیو و رسانه",
    snifferDesc: "نمایش دکمه دانلود روی پخش‌کننده‌های وب",
    capturFoxLoaderedia: "رسانه‌های شناسایی‌شده",
    rescan: "اسکن مجدد",
    noMedia: "هنوز رسانه‌ای در این صفحه شناسایی نشده",
    noMediaHint: "برای شناسایی توسط FoxLoader، ویدیو یا صدایی را پخش کنید",
    downloadAll: "دانلود تمام ویدیوها و رسانه‌ها",
    grabAll: "استخراج تمام لینک‌ها",
    grabAllDesc: "استخراج تمام لینک‌های درون صفحه (گربر FoxLoader)",
    addCustom: "افزودن دستی آدرس",
    addCustomDesc: "دانلود مستقیم هر نوع لینک اینترنتی",
    urlPlaceholder: "آدرس اینترنتی را وارد کنید (http://...)",
    addBtn: "افزودن",
    excludeSite: "استثنا کردن سایت",
    excluded: "استثنا شده",
    excludeTitleActive: "FoxLoader در این سایت غیرفعال است. جهت فعال‌سازی کلیک کنید.",
    excludeTitleInactive: "جهت غیرفعال کردن رهگیری FoxLoader در این سایت کلیک کنید.",
    options: "تنظیمات",
    connecting: "در حال اتصال...",
    connected: "متصل به FoxLoader",
    offline: "نرم‌افزار بسته است",
    download: "دانلود",
    internalPage: "صفحه داخلی مرورگر"
  },
  ku: {
    statsTitleActive: (n) => `${n} داونلۆدی چالاک بەڕێوەدەچێت`,
    statsSubActive: "چاودێریکردنی خێرایی لە پاشبنەما",
    statsTitleIdle: "مۆتۆری FoxLoader ئامادەیە",
    statsSubIdle: (n) => `${n} فایل لە داتابەیسدا تۆمارکراوە`,
    openApp: "کردنەوەی بەرنامە",
    interceptLabel: "گرتنی خودکاری داونلۆدەکان",
    interceptDesc: "ناردنی داونلۆدی وێبگەڕ ڕاستەوخۆ بۆ FoxLoader",
    snifferLabel: "دۆزەرەوەی ڤیدیۆ و میدیا",
    snifferDesc: "پیشاندانی دوگمەی داونلۆد لەسەر پەخشکەرەکان",
    capturFoxLoaderedia: "میدیای دۆزراوە لەم پەڕەیە",
    rescan: "دووبارە پشکنین",
    noMedia: "تا ئێستا هیچ میدیایەک نەدۆزراوەتەوە",
    noMediaHint: "ڤیدیۆیەک یان دەنگێک لێبدە تا FoxLoader دەستنیشانی بکات",
    downloadAll: "داونلۆدکردنی هەموو میدیاکان",
    grabAll: "دەرهێنانی هەموو لینکەکان",
    grabAllDesc: "کۆکردنەوەی هەموو لینکەکانی ئەم پەڕەیە",
    addCustom: "زیادکردنی لینکی تایبەت",
    addCustomDesc: "داونلۆدکردنی هەر بەستەرێک بە دەستی",
    urlPlaceholder: "بەستەری ئینتەرنێت لێرە بنووسە (http://...)",
    addBtn: "زیادکردن",
    excludeSite: "جیاکردنەوەی ماڵپەڕ",
    excluded: "جیاکراوەتەوە",
    excludeTitleActive: "FoxLoader لەم ماڵپەڕە ناچالاکە. کلیک بکە بۆ چالاککردنەوە.",
    excludeTitleInactive: "کلیک بکە بۆ ڕێگریکردن لە گرتنی داونلۆد لەم دۆمەینە.",
    options: "ڕێکخستنەکان",
    connecting: "پەیوەست دەبێت...",
    connected: "پەیوەستە بە FoxLoader",
    offline: "بەرنامەکە داخراوە",
    download: "داونلۆد",
    internalPage: "پەڕەی ناوخۆیی وێبگەڕ"
  }
};

document.addEventListener("DOMContentLoaded", async () => {
  const container = document.querySelector(".FoxLoader-container");
  const statusPill = document.getElementById("connectionStatus");
  const statsCard = document.getElementById("statsCard");
  const statsTitle = document.getElementById("statsTitle");
  const statsSub = document.getElementById("statsSub");
  const interceptToggle = document.getElementById("interceptToggle");
  const snifferToggle = document.getElementById("snifferToggle");
  const interceptLabel = document.getElementById("interceptLabel");
  const interceptDesc = document.getElementById("interceptDesc");
  const snifferLabel = document.getElementById("snifferLabel");
  const snifferDesc = document.getElementById("snifferDesc");
  const capturFoxLoaderediaLabel = document.getElementById("capturFoxLoaderediaLabel");
  const mediaList = document.getElementById("mediaList");
  const mediaCountBadge = document.getElementById("mediaCountBadge");
  const refreshMediaBtn = document.getElementById("refreshMediaBtn");
  const downloadAllMediaBtn = document.getElementById("downloadAllMediaBtn");
  const downloadAllMediaContainer = document.getElementById("downloadAllMediaContainer");
  const batchGrabBtn = document.getElementById("batchGrabBtn");
  const grabLabel = document.getElementById("grabLabel");
  const grabDesc = document.getElementById("grabDesc");
  const quickAddUrlBtn = document.getElementById("quickAddUrlBtn");
  const addLabel = document.getElementById("addLabel");
  const addDesc = document.getElementById("addDesc");
  const quickAddContainer = document.getElementById("quickAddContainer");
  const quickUrlInput = document.getElementById("quickUrlInput");
  const pasteUrlBtn = document.getElementById("pasteUrlBtn");
  const submitQuickUrlBtn = document.getElementById("submitQuickUrlBtn");
  const quickAddFeedback = document.getElementById("quickAddFeedback");
  const currentDomainText = document.getElementById("currentDomainText");
  const toggleDomainExceptionBtn = document.getElementById("toggleDomainExceptionBtn");
  const openDesktopBtn = document.getElementById("openDesktopBtn");
  const openOptionsLink = document.getElementById("openOptionsLink");
  const langButtons = document.querySelectorAll(".lang-btn");

  let currentTab = null;
  let currentDomain = "";
  let capturFoxLoaderediaItems = [];
  let currentLang = "en";

  // Load persisted extension configurations
  const storage = await chrome.storage.local.get([
    "interceptEnabled",
    "snifferEnabled",
    "excludedDomains",
    "extensionLang",
    "corePort"
  ]);

  currentLang = storage.extensionLang || "en";
  FoxLoaderCorePort = storage.corePort || 2764;
  FoxLoader_CORE_URL = `http://127.0.0.1:${FoxLoaderCorePort}`;
  const portDisplay = document.getElementById("portDisplay");
  if (portDisplay) {
    portDisplay.textContent = `Port: ${FoxLoaderCorePort}`;
    portDisplay.addEventListener("click", async () => {
      const input = prompt("Enter FoxLoader Integration Server Port:", FoxLoaderCorePort);
      if (input) {
        const parsed = parseInt(input, 10);
        if (!isNaN(parsed) && parsed >= 1024 && parsed <= 65535) {
          FoxLoaderCorePort = parsed;
          FoxLoader_CORE_URL = `http://127.0.0.1:${FoxLoaderCorePort}`;
          portDisplay.textContent = `Port: ${FoxLoaderCorePort}`;
          await chrome.storage.local.set({ corePort: FoxLoaderCorePort });
          checkFoxLoaderStatus();
        }
      }
    });
  }

  const interceptEnabled = storage.interceptEnabled !== false;
  const snifferEnabled = storage.snifferEnabled !== false;
  const excludedDomains = Array.isArray(storage.excludedDomains) ? storage.excludedDomains : [];

  interceptToggle.checked = interceptEnabled;
  snifferToggle.checked = snifferEnabled;

  function applyLanguage(lang) {
    currentLang = lang;
    const t = POPUP_I18N[lang] || POPUP_I18N.en;
    const isRtl = (lang === "fa" || lang === "ku");

    if (container) {
      container.setAttribute("dir", isRtl ? "rtl" : "ltr");
    }

    // Update static labels
    if (interceptLabel) interceptLabel.textContent = t.interceptLabel;
    if (interceptDesc) interceptDesc.textContent = t.interceptDesc;
    if (snifferLabel) snifferLabel.textContent = t.snifferLabel;
    if (snifferDesc) snifferDesc.textContent = t.snifferDesc;
    if (capturFoxLoaderediaLabel) capturFoxLoaderediaLabel.textContent = t.capturFoxLoaderedia;
    if (refreshMediaBtn) refreshMediaBtn.textContent = t.rescan;
    if (downloadAllMediaBtn) downloadAllMediaBtn.textContent = t.downloadAll;
    if (grabLabel) grabLabel.textContent = t.grabAll;
    if (grabDesc) grabDesc.textContent = t.grabAllDesc;
    if (addLabel) addLabel.textContent = t.addCustom;
    if (addDesc) addDesc.textContent = t.addCustomDesc;
    if (quickUrlInput) quickUrlInput.placeholder = t.urlPlaceholder;
    if (submitQuickUrlBtn) submitQuickUrlBtn.textContent = t.addBtn;
    if (openDesktopBtn) openDesktopBtn.textContent = t.openApp;
    if (openOptionsLink) openOptionsLink.textContent = t.options;

    // Update active style on language buttons
    langButtons.forEach(btn => {
      const btnLang = btn.getAttribute("data-lang");
      if (btnLang === lang) {
        btn.classList.add("active");
        btn.style.background = "#00B4D8";
        btn.style.color = "#0F172A";
        btn.style.fontWeight = "700";
      } else {
        btn.classList.remove("active");
        btn.style.background = "transparent";
        btn.style.color = "#94A3B8";
        btn.style.fontWeight = "normal";
      }
    });

    updateDomainButtonState();
    renderMediaList(capturFoxLoaderediaItems);
  }

  // Language buttons event binding
  langButtons.forEach(btn => {
    btn.addEventListener("click", async () => {
      const selected = btn.getAttribute("data-lang");
      if (selected && POPUP_I18N[selected]) {
        applyLanguage(selected);
        await chrome.storage.local.set({ extensionLang: selected });
      }
    });
  });

  // Query active browser tab
  try {
    const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
    if (tabs && tabs[0]) {
      currentTab = tabs[0];
      if (currentTab.url && (currentTab.url.startsWith("http://") || currentTab.url.startsWith("https://"))) {
        const parsed = new URL(currentTab.url);
        currentDomain = parsed.hostname;
        currentDomainText.textContent = currentDomain;
      } else {
        const t = POPUP_I18N[currentLang] || POPUP_I18N.en;
        currentDomainText.textContent = t.internalPage;
        toggleDomainExceptionBtn.disabled = true;
      }
    }
  } catch (err) {
    console.error("Failed to query active tab:", err);
  }

  function updateDomainButtonState() {
    if (!currentDomain || toggleDomainExceptionBtn.disabled) return;
    const t = POPUP_I18N[currentLang] || POPUP_I18N.en;
    const isExcluded = excludedDomains.includes(currentDomain);
    if (isExcluded) {
      toggleDomainExceptionBtn.textContent = t.excluded;
      toggleDomainExceptionBtn.classList.add("active");
      toggleDomainExceptionBtn.title = t.excludeTitleActive;
    } else {
      toggleDomainExceptionBtn.textContent = t.excludeSite;
      toggleDomainExceptionBtn.classList.remove("active");
      toggleDomainExceptionBtn.title = t.excludeTitleInactive;
    }
  }

  // Intercept & sniffer setting updates
  interceptToggle.addEventListener("change", async () => {
    await chrome.storage.local.set({ interceptEnabled: interceptToggle.checked });
  });

  snifferToggle.addEventListener("change", async () => {
    await chrome.storage.local.set({ snifferEnabled: snifferToggle.checked });
    if (currentTab?.id) {
      try {
        await chrome.tabs.sendMessage(currentTab.id, {
          action: "sniffer_toggle_updated",
          enabled: snifferToggle.checked
        });
      } catch {}
    }
  });

  toggleDomainExceptionBtn.addEventListener("click", async () => {
    if (!currentDomain) return;
    const idx = excludedDomains.indexOf(currentDomain);
    if (idx > -1) {
      excludedDomains.splice(idx, 1);
    } else {
      excludedDomains.push(currentDomain);
    }
    await chrome.storage.local.set({ excludedDomains });
    updateDomainButtonState();
  });

  // Query FoxLoader core daemon status
  async function checkFoxLoaderStatus() {
    const t = POPUP_I18N[currentLang] || POPUP_I18N.en;
    statusPill.className = "status-pill offline";
    statusPill.querySelector(".status-text").textContent = t.connecting;

    try {
      const response = await fetch(`${FoxLoader_CORE_URL}/status`, { method: "GET" });
      if (response.ok) {
        const jobs = await response.json();
        statusPill.className = "status-pill online";
        statusPill.querySelector(".status-text").textContent = t.connected;

        const activeJobs = jobs.filter(j => j.status_text === "Downloading");
        statsCard.classList.remove("hidden");
        if (activeJobs.length > 0) {
          statsTitle.textContent = t.statsTitleActive(activeJobs.length);
          statsSub.textContent = t.statsSubActive;
        } else {
          statsTitle.textContent = t.statsTitleIdle;
          statsSub.textContent = t.statsSubIdle(jobs.length);
        }
      } else {
        throw new Error("Core offline");
      }
    } catch {
      statusPill.className = "status-pill offline";
      statusPill.querySelector(".status-text").textContent = t.offline;
      statsCard.classList.add("hidden");
    }
  }

  statusPill.addEventListener("click", checkFoxLoaderStatus);

  // Retrieve media captured on active tab
  async function loadCapturFoxLoaderedia() {
    if (!currentTab?.id) return;
    try {
      const response = await chrome.tabs.sendMessage(currentTab.id, { action: "get_captured_media" });
      if (response && Array.isArray(response.media)) {
        capturFoxLoaderediaItems = response.media;
        renderMediaList(capturFoxLoaderediaItems);
      }
    } catch {
      renderMediaList([]);
    }
  }

  function renderMediaList(items) {
    const t = POPUP_I18N[currentLang] || POPUP_I18N.en;
    mediaCountBadge.textContent = items.length;
    if (items.length === 0) {
      mediaList.innerHTML = `
        <div class="empty-state">
          <div class="empty-icon">🎬</div>
          <div class="empty-text">${t.noMedia}</div>
          <div class="empty-hint">${t.noMediaHint}</div>
        </div>
      `;
      downloadAllMediaContainer.classList.add("hidden");
      return;
    }

    downloadAllMediaContainer.classList.remove("hidden");
    mediaList.innerHTML = "";

    items.forEach((item, index) => {
      const el = document.createElement("div");
      el.className = "media-item";

      const ext = item.ext || "mp4";
      const tagClass = ext.toLowerCase().replace(/[^a-z0-9]/g, "");

      el.innerHTML = `
        <div class="media-info">
          <div class="media-name" title="${item.title || item.url}">${item.title || "Video Stream"}</div>
          <div class="media-tags">
            <span class="media-tag ${tagClass}">${ext.toUpperCase()}</span>
            ${item.size ? `<span class="media-size">${item.size}</span>` : ""}
          </div>
        </div>
        <button class="btn-download-sm" data-index="${index}">${t.download}</button>
      `;

      el.querySelector(".btn-download-sm").addEventListener("click", () => {
        downloadSingleMedia(item);
      });

      mediaList.appendChild(el);
    });
  }

  async function downloadSingleMedia(item) {
    try {
      await fetch(`${FoxLoader_CORE_URL}/add_interactive`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          url: item.url,
          file_name: item.title ? `${item.title}.${item.ext || "mp4"}` : "video.mp4"
        })
      });
      window.close();
    } catch {
      alert("Failed to send download to FoxLoader. Ensure desktop app is running.");
    }
  }

  downloadAllMediaBtn.addEventListener("click", async () => {
    if (capturFoxLoaderediaItems.length === 0) return;
    const batch = capturFoxLoaderediaItems.map(item => ({
      url: item.url,
      file_name: item.title ? `${item.title}.${item.ext || "mp4"}` : "media_download",
      save_path: "",
      start_immediately: true,
      queue: "Main Queue"
    }));

    try {
      await fetch(`${FoxLoader_CORE_URL}/batch_add`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ items: batch })
      });
      window.close();
    } catch {
      alert("Failed to add batch media to FoxLoader.");
    }
  });

  refreshMediaBtn.addEventListener("click", loadCapturFoxLoaderedia);

  // In-Page Link Grabber Modal Trigger
  batchGrabBtn.addEventListener("click", async () => {
    if (!currentTab?.id) return;
    try {
      await chrome.tabs.sendMessage(currentTab.id, { action: "open_batch_modal" });
      window.close();
    } catch {
      alert("Could not open Link Grabber on this tab (internal or restricted page).");
    }
  });

  // Manual URL Add section
  quickAddUrlBtn.addEventListener("click", () => {
    quickAddContainer.classList.toggle("hidden");
    if (!quickAddContainer.classList.contains("hidden")) {
      quickUrlInput.focus();
    }
  });

  pasteUrlBtn.addEventListener("click", async () => {
    try {
      const text = await navigator.clipboard.readText();
      if (text) {
        quickUrlInput.value = text.trim();
      }
    } catch {
      quickUrlInput.focus();
    }
  });

  submitQuickUrlBtn.addEventListener("click", async () => {
    const url = quickUrlInput.value.trim();
    if (!url) return;

    quickAddFeedback.textContent = "Sending to FoxLoader...";
    try {
      let fileName = "downloaded_file";
      try {
        const parsed = new URL(url);
        fileName = parsed.pathname.split("/").pop() || "downloaded_file";
        fileName = decodeURIComponent(fileName.split("?")[0]);
      } catch {}

      const res = await fetch(`${FoxLoader_CORE_URL}/add_interactive`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ url, file_name: fileName })
      });

      if (res.ok) {
        quickAddFeedback.textContent = "Sent to FoxLoader!";
        setTimeout(() => window.close(), 600);
      } else {
        quickAddFeedback.textContent = "FoxLoader rejected the request.";
      }
    } catch {
      quickAddFeedback.textContent = "Failed. Is FoxLoader desktop app running?";
    }
  });

  quickUrlInput.addEventListener("keydown", (e) => {
    if (e.key === "Enter") {
      submitQuickUrlBtn.click();
    }
  });

  openOptionsLink.addEventListener("click", (e) => {
    e.preventDefault();
    chrome.tabs.create({ url: `${FoxLoader_CORE_URL}/status` });
  });

  openDesktopBtn.addEventListener("click", async () => {
    try {
      await fetch(`${FoxLoader_CORE_URL}/status`);
    } catch {}
  });

  // Initialize language and load states
  applyLanguage(currentLang);
  await checkFoxLoaderStatus();
  await loadCapturFoxLoaderedia();
});
