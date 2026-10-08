let FoxLoaderCorePort = 2764;
let FoxLoader_CORE_URL = `http://127.0.0.1:${FoxLoaderCorePort}`;

chrome.storage.local.get(["corePort"], (res) => {
    if (res && res.corePort) {
        FoxLoaderCorePort = res.corePort;
        FoxLoader_CORE_URL = `http://127.0.0.1:${FoxLoaderCorePort}`;
    }
});

chrome.storage.onChanged.addListener((changes, area) => {
    if (area === "local" && changes.corePort) {
        FoxLoaderCorePort = changes.corePort.newValue || 2764;
        FoxLoader_CORE_URL = `http://127.0.0.1:${FoxLoaderCorePort}`;
    }
});

// Setup context menus on installation
chrome.runtime.onInstalled.addListener(() => {
    chrome.contextMenus.create({
        id: "FoxLoader_download_link",
        title: "Download with FoxLoader",
        contexts: ["link", "video", "audio", "image", "selection"]
    });
    chrome.contextMenus.create({
        id: "FoxLoader_download_all",
        title: "Download all links on page with FoxLoader",
        contexts: ["page"]
    });
});

// Handle context menu clicks
chrome.contextMenus.onClicked.addListener(async (info, tab) => {
    if (info.menuItemId === "FoxLoader_download_link") {
        let url = info.linkUrl || info.srcUrl || info.selectionText?.trim();
        if (url && (url.startsWith("http://") || url.startsWith("https://"))) {
            let fileName = "downloaded_file";
            try {
                const parsed = new URL(url);
                fileName = parsed.pathname.split('/').pop() || "downloaded_file";
                fileName = decodeURIComponent(fileName.split('?')[0]);
            } catch {}
            await sendToCore(url, fileName);
        }
    } else if (info.menuItemId === "FoxLoader_download_all") {
        if (tab?.id) {
            chrome.tabs.sendMessage(tab.id, { action: "collect_all_links_for_FoxLoader" }, async (response) => {
                if (chrome.runtime.lastError) {
                    console.log("Could not communicate with tab:", chrome.runtime.lastError.message);
                }
            });
        }
    }
});

// Handle messages from content script & popup
chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
    (async () => {
        if (msg.action === "download_media") {
            await sendToCore(msg.url, msg.title || "video.mp4");
            sendResponse({ success: true });
        } else if (msg.action === "batch_links_collected_advanced" && msg.items?.length > 0) {
            const queue = msg.queue || "Main Queue";
            const items = msg.items.map(it => {
                let name = "file";
                try {
                    name = new URL(it.url).pathname.split('/').pop() || "file";
                    name = decodeURIComponent(name.split('?')[0]);
                } catch {}

                return {
                    url: it.url,
                    file_name: name,
                    save_path: it.save_path || "",
                    start_immediately: false,
                    queue: queue
                };
            });

            try {
                await fetch(`${FoxLoader_CORE_URL}/batch_add`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ items })
                });
                showNotification("FoxLoader Batch Download Added", `Added ${items.length} link(s) to ${queue}.`);
                sendResponse({ success: true });
            } catch {
                showNotification("FoxLoader Offline", "Could not connect to FoxLoader Core.");
                sendResponse({ success: false });
            }
        } else if (msg.action === "batch_links_collected" && msg.links?.length > 0) {
            const queue = msg.queue || "Main Queue";
            const items = msg.links.map(l => {
                let name = "file";
                try {
                    name = new URL(l).pathname.split('/').pop() || "file";
                    name = decodeURIComponent(name.split('?')[0]);
                } catch {}
                return {
                    url: l,
                    file_name: name,
                    save_path: "",
                    start_immediately: false,
                    queue: queue
                };
            });

            try {
                await fetch(`${FoxLoader_CORE_URL}/batch_add`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ items })
                });
                showNotification("Batch Download Added", `Added ${items.length} link(s) to ${queue}.`);
                sendResponse({ success: true });
            } catch {
                showNotification("FoxLoader Offline", "Could not connect to FoxLoader Core.");
                sendResponse({ success: false });
            }
        } else if (msg.action === "batch_interactive_ready" && msg.links?.length > 0) {
            await sendBatchInteractiveToCore(msg.page_url, msg.page_title, msg.links);
            sendResponse({ success: true });
        } else if (msg.action === "media_detected") {
            if (sender.tab?.id) {
                const countStr = msg.count > 0 ? String(msg.count) : "";
                await chrome.action.setBadgeText({ text: countStr, tabId: sender.tab.id });
                await chrome.action.setBadgeBackgroundColor({ color: "#00B4D8", tabId: sender.tab.id });
            }
            sendResponse({ success: true });
        }
    })();
    return true;
});

// Listen to browser downloads for interception
chrome.downloads.onCreated.addListener(async (downloadItem) => {
    const rawUrl = downloadItem.finalUrl || downloadItem.url;
    if (!rawUrl || rawUrl.startsWith("blob:") || rawUrl.startsWith("data:")) {
        return;
    }

    // Check storage configurations
    const storage = await chrome.storage.local.get(["interceptEnabled", "excludedDomains"]);
    if (storage.interceptEnabled === false) {
        return; // Interception turned off by user
    }

    const excludedDomains = Array.isArray(storage.excludedDomains) ? storage.excludedDomains : [];
    try {
        const downloadDomain = new URL(rawUrl).hostname;
        if (excludedDomains.some(d => downloadDomain.endsWith(d))) {
            return; // Domain is whitelisted/excluded from FoxLoader
        }
    } catch {}

    try {
        // Cancel browser native download
        await chrome.downloads.cancel(downloadItem.id);
        await chrome.downloads.erase({ id: downloadItem.id });

        let fileName = downloadItem.filename;
        if (!fileName) {
            try {
                fileName = new URL(rawUrl).pathname.split('/').pop() || "downloaded_file";
                fileName = decodeURIComponent(fileName.split('?')[0]);
            } catch {
                fileName = "downloaded_file";
            }
        }

        await sendToCore(rawUrl, fileName);
    } catch (e) {
        console.error("Interception error:", e);
    }
});

async function sendToCore(url, fileName) {
    const payload = {
        url: url,
        file_name: fileName
    };

    try {
        const response = await fetch(`${FoxLoader_CORE_URL}/add_interactive`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(payload)
        });

        if (response.ok) {
            showNotification("FoxLoader", `Intercepted: ${fileName}\nPrompting FoxLoader window...`);
        } else {
            throw new Error("Core rejected");
        }
    } catch {
        showNotification("FoxLoader Connection Failed", "Ensure FoxLoader desktop app is running.");
    }
}

async function sendBatchInteractiveToCore(pageUrl, pageTitle, links) {
    const payload = {
        page_url: pageUrl || "",
        page_title: pageTitle || "",
        links: links
    };

    try {
        const response = await fetch(`${FoxLoader_CORE_URL}/add_batch_interactive`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(payload)
        });

        if (response.ok) {
            showNotification("FoxLoader", `Found ${links.length} link(s) on page.\nOpening FoxLoader Selection Dialog...`);
        } else {
            throw new Error("Core rejected");
        }
    } catch {
        showNotification("FoxLoader Connection Failed", "Ensure FoxLoader desktop app is running.");
    }
}

function showNotification(title, message) {
    chrome.notifications.create({
        type: "basic",
        iconUrl: "icons/icon-48.png",
        title: title,
        message: message
    });
}
