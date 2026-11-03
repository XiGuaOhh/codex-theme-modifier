/* Codex Background 2.0.0 — local appearance customization. No network requests. */
(() => {
  "use strict";
  if (window.top !== window || location.protocol !== "app:" || location.host !== "-" || !["/index.html", "/detached-window.html"].includes(location.pathname) || new URLSearchParams(location.search).get("initialRoute")?.startsWith("/avatar-overlay")) return;
  const VERSION = "2.0.0";
  if (window.__codexBackgroundVersion === VERSION) return;
  if (window.__codexBackgroundVersion) {
    if (typeof window.__codexBackgroundDispose === "function") window.__codexBackgroundDispose();
    else return;
  }
  let disposed = false;
  const cleanup = [];
  const listen = (target, type, handler, options) => { target.addEventListener(type, handler, options); cleanup.push(() => target.removeEventListener(type, handler, options)); };
  const KEY = "codex.custom-background.settings.v1";
  const IMAGE_REVISION_KEY = "codex.custom-background.image-revision.v1";
  const DEFAULTS = Object.freeze({ enabled: true, dim: 42, blur: 0, panel: 70, bubble: 18, fit: "cover", position: "center" });
  const TYPES = new Set(["image/png", "image/jpeg", "image/webp", "image/gif", "image/avif"]);
  const POSITIONS = new Set(["center", "top", "bottom", "left", "right"]);
  const clamp = (value, min, max, fallback) => Number.isFinite(Number(value)) ? Math.min(max, Math.max(min, Number(value))) : fallback;
  function normalize(value) {
    value = value && typeof value === "object" ? value : {};
    return {
      enabled: typeof value.enabled === "boolean" ? value.enabled : true,
      dim: clamp(value.dim, 0, 90, DEFAULTS.dim),
      blur: clamp(value.blur, 0, 24, DEFAULTS.blur),
      panel: clamp(value.panel, 0, 100, DEFAULTS.panel),
      bubble: clamp(value.bubble, 0, 100, DEFAULTS.bubble),
      fit: value.fit === "contain" ? "contain" : "cover",
      position: POSITIONS.has(value.position) ? value.position : "center",
    };
  }
  let settings;
  try { settings = normalize(JSON.parse(localStorage.getItem(KEY))); } catch { settings = { ...DEFAULTS }; }
  let database;
  let photo = null;
  let imageUrl = null;
  let busy = false;
  let pendingPhotoRefresh = false;
  let photoLoadGeneration = 0;
  let ui;
  let open = false;
  let host;
  let titlebar;
  let positionFrame;
  let titlebarResizeObserver;

  function syncPosition() {
    if (!host || disposed) return;
    const overlay = navigator.windowControlsOverlay;
    const overlayRect = overlay?.visible ? overlay.getTitlebarAreaRect() : null;
    const nextTitlebar = document.querySelector('[data-app-shell-application-menu-bar="true"]') || document.querySelector('[data-app-shell-titlebar="true"]') || document.querySelector('[class*="_ApplicationMenuTopBar_"]');
    if (nextTitlebar !== titlebar) {
      titlebar?.removeAttribute("data-cbg-titlebar-anchor");
      titlebarResizeObserver?.disconnect();
      titlebar = nextTitlebar;
      if (titlebar) {
        titlebar.setAttribute("data-cbg-titlebar-anchor", "");
        titlebarResizeObserver = new ResizeObserver(schedulePosition);
        titlebarResizeObserver.observe(titlebar);
      }
    }
    const barRect = titlebar?.getBoundingClientRect();
    const minimize = document.querySelector('[data-cbg-minimize],button[aria-label="Minimize"],button[aria-label="最小化"]');
    const minimizeRect = minimize?.getBoundingClientRect();
    const inset = titlebar ? parseFloat(getComputedStyle(titlebar).getPropertyValue("--spacing-token-safe-header-right")) : NaN;
    const right = overlayRect?.width > 0 ? Math.max(0, window.innerWidth - (overlayRect.x + overlayRect.width)) : minimizeRect?.width > 0 ? Math.max(0, window.innerWidth - minimizeRect.left) : Number.isFinite(inset) && inset > 0 ? inset : 138;
    const top = overlayRect?.height > 0 ? overlayRect.y : barRect?.top >= 0 ? barRect.top : 0;
    const height = Math.max(24, Math.min(48, overlayRect?.height || barRect?.height || 32));
    host.style.right = `${right}px`;
    host.style.top = `${top}px`;
    host.style.setProperty("--cbg-caption-height", `${height}px`);
    document.documentElement.setAttribute("data-cbg-titlebar-ready", "");
  }

  function schedulePosition() {
    if (positionFrame || disposed) return;
    positionFrame = requestAnimationFrame(() => { positionFrame = null; syncPosition(); });
  }

  function openDatabase() {
    if (database) return database;
    database = new Promise((resolve, reject) => {
      const request = indexedDB.open("codex-custom-background-v1", 1);
      request.onupgradeneeded = () => request.result.createObjectStore("images");
      request.onsuccess = () => {
        const db = request.result;
        db.onversionchange = () => { db.close(); database = null; };
        resolve(db);
      };
      request.onerror = () => reject(request.error || new Error("无法打开本地图片存储。"));
      request.onblocked = () => reject(new Error("图片存储正忙，请关闭其他背景设置窗口后重试。"));
    });
    database.catch(() => { database = null; });
    return database;
  }

  async function imageStore(action, value) {
    const db = await openDatabase();
    return new Promise((resolve, reject) => {
      const transaction = db.transaction("images", action === "get" ? "readonly" : "readwrite");
      const store = transaction.objectStore("images");
      const request = action === "get" ? store.get("background") : action === "put" ? store.put(value, "background") : store.delete("background");
      let result;
      request.onsuccess = () => { result = request.result; };
      transaction.oncomplete = () => resolve(result);
      transaction.onerror = () => reject(transaction.error || new Error("图片保存失败，请检查可用存储空间。"));
      transaction.onabort = () => reject(transaction.error || new Error("图片保存未完成，请重试。"));
    });
  }

  function message(text, error = false) {
    const status = ui?.querySelector("#status");
    if (status) { status.textContent = text; status.dataset.error = String(error); }
  }

  function saveSettings() {
    try { localStorage.setItem(KEY, JSON.stringify(settings)); return true; }
    catch { message("设置暂时无法保存，关闭后可能需要重新调整。", true); return false; }
  }

  function publishPhotoChange() {
    try { localStorage.setItem(IMAGE_REVISION_KEY, `${Date.now()}-${Math.random()}`); } catch {}
  }

  async function refreshPhoto() {
    const generation = ++photoLoadGeneration;
    const saved = await imageStore("get");
    if (generation === photoLoadGeneration) usePhoto(saved?.blob instanceof Blob && TYPES.has(saved.blob.type) ? saved : null);
  }

  function receivePhotoChange() {
    if (busy) { pendingPhotoRefresh = true; return; }
    pendingPhotoRefresh = false;
    refreshPhoto().catch(error => message(error.message || "无法同步背景图片。", true));
  }

  function apply() {
    if (disposed) return;
    const root = document.documentElement;
    const active = Boolean(settings.enabled && imageUrl);
    root.toggleAttribute("data-cbg-enabled", active);
    root.style.setProperty("--cbg-image", imageUrl ? `url("${imageUrl}")` : "none");
    root.style.setProperty("--cbg-dim", String(settings.dim / 100));
    root.style.setProperty("--cbg-blur", `${settings.blur}px`);
    root.style.setProperty("--cbg-fit", settings.fit);
    root.style.setProperty("--cbg-position", settings.position);
    root.style.setProperty("--cbg-panel-alpha", String(settings.panel / 100));
    root.style.setProperty("--cbg-bubble-alpha", String(settings.bubble / 100));
    const dark = root.dataset.theme !== "light" && (!root.classList.contains("light"));
    root.style.setProperty("--cbg-base", dark ? "24 24 27" : "248 249 252");
    root.style.setProperty("--cbg-surface", `var(--codex-base-surface, ${dark ? "#18181b" : "#f8f9fc"})`);
    root.style.setProperty("--cbg-accent", "var(--codex-base-accent, #339cff)");
    root.style.setProperty("--cbg-scheme", dark ? "dark" : "light");
    if (ui) {
      ui.querySelector("#enabled").checked = settings.enabled;
      ui.querySelector("#filename").textContent = photo?.name || "尚未选择图片";
      for (const key of ["dim", "blur", "panel", "bubble"]) {
        ui.querySelector(`#${key}`).value = String(settings[key]);
        ui.querySelector(`#${key}-value`).textContent = `${settings[key]}${key === "blur" ? " px" : "%"}`;
      }
      ui.querySelector("#fit").value = settings.fit;
      ui.querySelector("#position").value = settings.position;
      ui.querySelector("#remove").disabled = !photo || busy;
      ui.querySelector("#choose").disabled = busy;
      ui.querySelector("#choose").textContent = busy ? "正在保存…" : photo ? "更换本地图片" : "选择本地图片";
      ui.querySelector("#thumbnail").style.backgroundImage = imageUrl ? `url("${imageUrl}")` : "none";
      ui.querySelector("#thumbnail").classList.toggle("has-image", Boolean(imageUrl));
    }
    schedulePosition();
  }

  function usePhoto(next) {
    if (disposed) return;
    photoLoadGeneration++;
    const previous = imageUrl;
    photo = next || null;
    imageUrl = photo ? URL.createObjectURL(photo.blob) : null;
    apply();
    if (previous) URL.revokeObjectURL(previous);
  }

  async function validateImage(file) {
    if (!TYPES.has(file.type)) throw new Error("请选择 PNG、JPG、WebP、GIF 或 AVIF 图片。");
    if (file.size > 25 * 1024 * 1024) throw new Error("图片超过 25 MB，请选择小一点的图片。");
    const temporaryUrl = URL.createObjectURL(file);
    try {
      const image = new Image();
      image.src = temporaryUrl;
      await image.decode();
      if (!image.naturalWidth || image.naturalWidth * image.naturalHeight > 60_000_000) throw new Error("图片尺寸过大，请选择小于 6000 万像素的图片。");
    } catch (error) {
      if (error.message?.includes("尺寸过大")) throw error;
      throw new Error("这张图片无法读取，请换一张图片。");
    } finally { URL.revokeObjectURL(temporaryUrl); }
  }

  async function chooseImage(file) {
    if (!file || busy) return;
    busy = true; apply(); message("正在读取本地图片…");
    try {
      await validateImage(file);
      const next = { blob: file, name: file.name };
      await imageStore("put", next);
      settings.enabled = true;
      usePhoto(next);
      publishPhotoChange();
      if (saveSettings()) message("已保存到本机，下次打开会自动恢复。");
    } catch (error) { message(error.message || "图片保存失败，请重试。", true); }
    finally { busy = false; apply(); ui.querySelector("#file").value = ""; if (pendingPhotoRefresh) receivePhotoChange(); }
  }

  function setOpen(value) {
    open = value;
    ui.querySelector("#settings-panel").hidden = !open;
    ui.querySelector("#toggle").setAttribute("aria-expanded", String(open));
    if (open) ui.querySelector("#close").focus();
    else ui.querySelector("#toggle").focus();
  }

  function boot() {
    if (disposed || !document.getElementById("root") || document.getElementById("codex-background-controls")) return;
    window.__codexBackgroundVersion = VERSION;
    const style = document.createElement("style");
    style.id = "codex-custom-background-styles";
    style.textContent = `
      html[data-cbg-enabled] body { background: rgb(var(--cbg-base)) !important; }
      html[data-cbg-enabled] #root { position: relative; isolation: isolate; background: transparent !important; }
      html[data-cbg-enabled] #root::before {
        content: ""; position: fixed; inset: calc(var(--cbg-blur) * -2); z-index: -2; pointer-events: none;
        background-color: var(--cbg-surface); background-image: var(--cbg-image);
        background-size: var(--cbg-fit); background-position: var(--cbg-position); background-repeat: no-repeat;
        filter: blur(var(--cbg-blur));
      }
      html[data-cbg-enabled] #root::after {
        content: ""; position: fixed; inset: 0; z-index: -1; pointer-events: none;
        background: color-mix(in srgb, var(--cbg-surface) calc(var(--cbg-dim) * 100%), transparent);
      }
      :root {
        --cbg-panel-bg: color-mix(in srgb, var(--cbg-surface) calc(var(--cbg-panel-alpha) * 100%), transparent);
        --cbg-bubble-bg: color-mix(in srgb, var(--cbg-accent) calc(var(--cbg-bubble-alpha) * 100%), transparent);
        --cbg-popup-bg: color-mix(in srgb, var(--cbg-surface) 96%, transparent);
      }
      [data-cbg-titlebar-anchor] { padding-inline-end: calc(var(--spacing-token-safe-header-right, 0px) + 40px) !important; }
      html[data-cbg-enabled], html[data-cbg-enabled] #root, html[data-cbg-enabled] #root [data-theme] {
        --color-surface: transparent !important;
        --color-surface-canvas: transparent !important;
        --color-surface-secondary: rgb(var(--cbg-base) / 0.12) !important;
        --color-surface-tertiary: rgb(var(--cbg-base) / 0.16) !important;
        --color-surface-elevated: var(--cbg-panel-bg) !important;
        --color-surface-elevated-secondary: var(--cbg-panel-bg) !important;
        --color-surface-card: var(--cbg-panel-bg) !important;
        --app-color-background-surface: transparent !important;
        --app-color-background-surface-under: transparent !important;
        --color-background-panel: transparent !important;
        --color-background-composer-surface: var(--cbg-panel-bg) !important;
        --color-background-composer-action-bar: var(--cbg-panel-bg) !important;
        --app-color-background-control: var(--cbg-panel-bg) !important;
        --app-color-background-card: var(--cbg-panel-bg) !important;
        --app-color-background-elevated-primary: var(--cbg-panel-bg) !important;
        --app-color-background-elevated-secondary: var(--cbg-panel-bg) !important;
        --app-color-background-editor-opaque: var(--cbg-panel-bg) !important;
        --color-background-user-message: var(--cbg-bubble-bg) !important;
        --color-background-user-message-compact: var(--cbg-bubble-bg) !important;
        --color-background-other-user-message: var(--cbg-bubble-bg) !important;
        --color-text-user-message: var(--color-text) !important;
      }
      html[data-cbg-enabled] #root :is(.bg-user-message, [class*="_compactMessageBubble_"], [class*="_accentedBubble_"]) {
        --color-background-user-message: var(--cbg-bubble-bg) !important;
        --color-text-user-message: var(--color-text) !important;
        background-color: var(--cbg-bubble-bg) !important;
        color: var(--color-text) !important;
      }
      html[data-cbg-enabled] #root :is([data-composer-surface-variant], [class*="_ComposerLayoutRoot_"]) {
        --composer-layout-surface-background: var(--cbg-panel-bg) !important;
        --composer-surface-overlay-background: transparent !important;
        --color-background-composer-surface: var(--cbg-panel-bg) !important;
        --color-surface-elevated: var(--cbg-panel-bg) !important;
        --color-surface-elevated-secondary: var(--cbg-panel-bg) !important;
      }
      html[data-cbg-enabled] #root :is([data-composer-surface-variant], [class*="_ComposerLayoutRoot_"]) :is(textarea, [contenteditable="true"], .ProseMirror) {
        background-color: transparent !important;
      }
      html[data-cbg-enabled] :is(#root, body) :is([role="dialog"], [role="alertdialog"], [role="menu"], [role="listbox"], [role="tooltip"], [data-radix-popper-content-wrapper]) {
        --color-surface: var(--cbg-popup-bg) !important;
        --color-surface-secondary: var(--cbg-popup-bg) !important;
        --color-surface-tertiary: var(--cbg-popup-bg) !important;
        --color-surface-elevated: var(--cbg-popup-bg) !important;
        --color-surface-elevated-secondary: var(--cbg-popup-bg) !important;
        --color-surface-card: var(--cbg-popup-bg) !important;
        --app-color-background-surface: var(--cbg-popup-bg) !important;
        --app-color-background-surface-under: var(--cbg-popup-bg) !important;
        --app-color-background-elevated-primary: var(--cbg-popup-bg) !important;
        --app-color-background-elevated-secondary: var(--cbg-popup-bg) !important;
        --app-color-background-card: var(--cbg-popup-bg) !important;
        --app-color-background-editor-opaque: var(--cbg-popup-bg) !important;
        --color-background-panel: var(--cbg-popup-bg) !important;
      }
      :root:is([data-codex-window-type="extension"], [data-codex-window-type="avatar"], [data-codex-window-type="pet"]) #codex-background-controls { display: none; }
    `;
    document.head.appendChild(style);
    host = document.createElement("div");
    host.id = "codex-background-controls";
    host.style.cssText = "position:fixed;right:138px;top:0;z-index:2147483640;pointer-events:none;-webkit-app-region:no-drag;";
    ui = host.attachShadow({ mode: "open" });
    ui.innerHTML = `
      <style>
        :host { color-scheme:var(--cbg-scheme,dark); font:13px/1.5 var(--font-sans,"Segoe UI Variable Text","Microsoft YaHei UI",system-ui,sans-serif); color:var(--color-text,#f4f6fb); }
        * { box-sizing:border-box; } [hidden] { display:none !important; }
        button,select,input { font:inherit; } button,select,input[type="range"],input[type="checkbox"] { cursor:pointer; }
        button { border:1px solid var(--color-border,#ffffff25); background:var(--color-background-primary-ghost-hover,#ffffff0c); color:inherit; border-radius:8px; padding:8px 12px; }
        button:hover { background:var(--color-background-primary-ghost-active,#ffffff18); } button:disabled { opacity:.45; cursor:default; }
        button:focus-visible,select:focus-visible,input:focus-visible { outline:2px solid #8ab9ff; outline-offset:3px; }
        #toggle { pointer-events:auto; -webkit-app-region:no-drag; display:flex; align-items:center; justify-content:center; margin-left:auto; padding:0; width:40px; height:var(--cbg-caption-height,32px); border:0; border-radius:4px; color:var(--color-text-secondary,inherit); background:transparent; }
        #toggle:hover { color:var(--color-text,inherit); background:var(--color-background-primary-ghost-hover,#ffffff12); } #toggle[aria-expanded="true"] { background:var(--color-background-primary-ghost-hover,#ffffff12); } #toggle svg { width:16px; height:16px; }
        #settings-panel { pointer-events:auto; position:absolute; top:calc(var(--cbg-caption-height,32px) + 8px); right:0; width:min(326px,calc(100vw - 32px)); max-height:calc(100vh - var(--cbg-caption-height,32px) - 24px); overflow:auto; background:var(--cbg-popup-bg,#1c202a); border:1px solid var(--color-border,#ffffff24); border-radius:12px; box-shadow:0 12px 40px #0004; padding:18px; }
        header { display:flex; align-items:center; justify-content:space-between; gap:10px; margin-bottom:16px; }
        h2 { font-size:15px; font-weight:600; margin:0; } #close { border:0; background:none; padding:0; width:24px; height:24px; font-size:22px; line-height:1; color:var(--color-text-secondary,#b3bbc9); }
        #thumbnail { height:104px; background:linear-gradient(135deg,#262c3d,#36314b); background-position:center; background-size:cover; border-radius:12px; border:1px solid #ffffff15; display:grid; place-items:center; margin-bottom:10px; }
        #thumbnail span { color:#c2cad9; font-size:12px; background:#171b25c9; border-radius:8px; padding:5px 9px; } #thumbnail.has-image span { display:none; }
        #filename { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; font-size:12px; color:var(--color-text-secondary,#b9c3d3); margin:0 0 12px; }
        #choose { width:100%; background:var(--color-background-accent-action,var(--cbg-accent,#3d73d9)); color:var(--color-text-accent-action,#fff); border-color:transparent; font-weight:600; } #choose:hover { filter:brightness(1.08); }
        .row { display:flex; align-items:center; justify-content:space-between; gap:12px; margin-top:17px; } .row label { display:flex; align-items:center; gap:8px; }
        input[type="checkbox"] { accent-color:var(--cbg-accent,#76a7ff); width:16px; height:16px; }
        .control { margin-top:14px; } .control label { display:flex; justify-content:space-between; margin-bottom:8px; } output { color:var(--color-text-secondary,#a4b5d0); font-size:12px; font-variant-numeric:tabular-nums; }
        input[type="range"] { display:block; accent-color:var(--cbg-accent,#79a8ff); width:100%; margin:0; }
        select { color:inherit; background:var(--cbg-surface,#282e3d); border:1px solid var(--color-border,#ffffff20); border-radius:6px; padding:5px 8px; max-width:150px; }
        #remove { padding:0; border:0; color:var(--color-text-secondary,#c1cad9); font-size:12px; background:none; text-decoration:underline; text-underline-offset:3px; }
        .footer { border-top:1px solid var(--color-border,#ffffff13); margin-top:16px; padding-top:12px; font-size:11px; color:var(--color-text-secondary,#9ba6ba); }
        #status { min-height:18px; margin:10px 0 0; color:var(--color-text-info,#a5cbff); font-size:12px; } #status[data-error="true"] { color:var(--color-text-danger,#ffb7b7); }
        @media (prefers-reduced-motion:no-preference) { button { transition:background .12s; } }
      </style>
      <section id="settings-panel" role="dialog" aria-labelledby="title" hidden>
        <header><h2 id="title">自定义背景</h2><button id="close" aria-label="关闭背景设置" title="关闭">×</button></header>
        <div id="thumbnail" aria-hidden="true"><span>选择一张喜欢的图片</span></div>
        <p id="filename">尚未选择图片</p>
        <input id="file" type="file" accept="image/png,image/jpeg,image/webp,image/gif,image/avif" aria-label="选择背景图片文件" hidden>
        <button id="choose">选择本地图片</button>
        <div class="row"><label><input id="enabled" type="checkbox" checked>启用图片背景</label><button id="remove" disabled>移除图片</button></div>
        <div class="control"><label for="dim">背景遮罩<output id="dim-value" for="dim">42%</output></label><input id="dim" type="range" min="0" max="90" value="42"></div>
        <div class="control"><label for="blur">模糊程度<output id="blur-value" for="blur">0 px</output></label><input id="blur" type="range" min="0" max="24" value="0"></div>
        <div class="control"><label for="panel">面板不透明度<output id="panel-value" for="panel">70%</output></label><input id="panel" type="range" min="0" max="100" value="70"></div>
        <div class="control"><label for="bubble">消息气泡不透明度<output id="bubble-value" for="bubble">18%</output></label><input id="bubble" type="range" min="0" max="100" value="18"></div>
        <div class="row"><label for="fit">图片适配</label><select id="fit"><option value="cover">铺满窗口</option><option value="contain">完整显示</option></select></div>
        <div class="row"><label for="position">图片位置</label><select id="position"><option value="center">居中</option><option value="top">靠上</option><option value="bottom">靠下</option><option value="left">靠左</option><option value="right">靠右</option></select></div>
        <p id="status" role="status" aria-live="polite"></p>
        <div class="footer">图片只保存在本机 · 支持 PNG / JPG / WebP / GIF / AVIF<br>快捷键 Alt + Shift + B</div>
      </section>
      <button id="toggle" aria-label="背景设置" aria-expanded="false" aria-controls="settings-panel" title="背景设置 (Alt+Shift+B)"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M12 3a9 9 0 1 0 0 18h1.5a2 2 0 0 0 1.4-3.4 1.1 1.1 0 0 1 .8-1.9H17a4 4 0 0 0 4-4A8.8 8.8 0 0 0 12 3Z"/><circle cx="7.5" cy="10" r=".75"/><circle cx="10.5" cy="6.8" r=".75"/><circle cx="15" cy="7.5" r=".75"/><circle cx="17.2" cy="11.3" r=".75"/></svg></button>
    `;
    document.body.appendChild(host);
    ui.querySelector("#toggle").addEventListener("click", () => setOpen(!open));
    ui.querySelector("#close").addEventListener("click", () => setOpen(false));
    ui.querySelector("#choose").addEventListener("click", () => ui.querySelector("#file").click());
    ui.querySelector("#file").addEventListener("change", event => chooseImage(event.target.files?.[0]));
    ui.querySelector("#enabled").addEventListener("change", event => { settings.enabled = event.target.checked; apply(); saveSettings(); });
    for (const key of ["dim", "blur", "panel", "bubble", "fit", "position"]) {
      ui.querySelector(`#${key}`).addEventListener(key === "fit" || key === "position" ? "change" : "input", event => {
        settings = normalize({ ...settings, [key]: event.target.value }); apply(); saveSettings();
      });
    }
    ui.querySelector("#remove").addEventListener("click", async () => {
      if (busy) return;
      busy = true; apply();
      try { await imageStore("delete"); usePhoto(null); publishPhotoChange(); message("已移除图片，恢复原来的主题背景。"); }
      catch (error) { message(error.message || "移除失败，请重试。", true); }
      finally { busy = false; apply(); if (pendingPhotoRefresh) receivePhotoChange(); }
    });
    listen(window, "storage", event => {
      if (event.key === KEY) {
        try { settings = normalize(JSON.parse(event.newValue)); apply(); }
        catch { message("无法同步背景设置。", true); }
      } else if (event.key === IMAGE_REVISION_KEY) receivePhotoChange();
    });
    listen(document, "keydown", event => {
      if (event.altKey && event.shiftKey && event.code === "KeyB") { event.preventDefault(); setOpen(!open); }
      else if (event.key === "Escape" && open) { event.preventDefault(); event.stopPropagation(); setOpen(false); }
    }, true);
    listen(document, "pointerdown", event => { if (open && !event.composedPath().includes(host)) { open = false; ui.querySelector("#settings-panel").hidden = true; ui.querySelector("#toggle").setAttribute("aria-expanded", "false"); } });
    const themeObserver = new MutationObserver(apply);
    themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme", "class"] });
    cleanup.push(() => themeObserver.disconnect());
    const layoutObserver = new MutationObserver(() => { if (!titlebar?.isConnected) schedulePosition(); });
    layoutObserver.observe(document.body, { childList: true, subtree: true });
    cleanup.push(() => layoutObserver.disconnect());
    listen(window, "resize", schedulePosition);
    if (navigator.windowControlsOverlay) listen(navigator.windowControlsOverlay, "geometrychange", schedulePosition);
    window.__codexBackgroundDispose = () => {
      if (disposed) return;
      disposed = true;
      for (const fn of cleanup.splice(0)) { try { fn(); } catch {} }
      if (positionFrame) cancelAnimationFrame(positionFrame);
      titlebarResizeObserver?.disconnect();
      titlebar?.removeAttribute("data-cbg-titlebar-anchor");
      host?.remove(); style.remove();
      document.documentElement.removeAttribute("data-cbg-enabled");
      document.documentElement.removeAttribute("data-cbg-titlebar-ready");
      for (const name of Array.from(document.documentElement.style)) if (name.startsWith("--cbg-")) document.documentElement.style.removeProperty(name);
      if (imageUrl) URL.revokeObjectURL(imageUrl);
      if (database) database.then(db => db.close()).catch(() => {});
      delete window.__codexBackgroundVersion;
      delete window.__codexBackgroundReload;
      delete window.__codexBackgroundDispose;
    };
    apply();
    window.__codexBackgroundReload = async () => {
      try { settings = normalize(JSON.parse(localStorage.getItem(KEY))); } catch {}
      await refreshPhoto();
      return Boolean(imageUrl);
    };
    window.__codexBackgroundReload().catch(error => message(error.message || "无法读取已保存的图片。", true));
  }
  if (document.readyState === "loading") listen(document, "DOMContentLoaded", boot, { once: true });
  else boot();
})();
