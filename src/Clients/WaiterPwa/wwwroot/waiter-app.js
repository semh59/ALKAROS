// ALKAROS Waiter PWA (V1-WTR-010 rewrite of V1-WTR-008 / V1-RMD-051 / -066 / -129)
//
// Rewritten against docs/design/foundations.md. Its §0 rule - "backend akilli,
// frontend aptal" - is what shapes this file: the client keeps no order state
// of its own. Every line, price, kitchen state and remaining-stock figure on
// screen is the server's own DTO rendered back; the only local state is the
// round the waiter is still composing and has not sent yet.
//
// Endpoints this screen speaks to:
//   GET  /api/v1/auth/session?terminalId=
//   POST /api/v1/auth/login | /logout | /unlock | /pin
//   GET  /api/v1/terminals/{t}/table-management/zones
//   GET  /api/v1/terminals/{t}/table-management/tables
//   POST /api/v1/terminals/{t}/table-management/transfers
//   GET  /api/v1/terminals/{t}/catalog
//   GET  /api/v1/terminals/{t}/orders/table/{tableId}
//   GET  /api/v1/terminals/{t}/orders/pending
//   POST /api/v1/terminals/{t}/orders/table-draft
//   POST /api/v1/terminals/{t}/orders/{o}/submit-draft
//   POST /api/v1/terminals/{t}/orders/{o}/items/{i}/void
//   POST /api/v1/terminals/{t}/orders/{o}/accept | /reject
(function () {
  'use strict';

  // ══ Utilities ══════════════════════════════════════════════════════

  // Escapes for BOTH text and double-quoted attribute contexts.
  //
  // This used to serialize a text node (`div.textContent = …; return
  // div.innerHTML`), which escapes only & < > — a quote passed through
  // untouched. Every attribute in this file is double-quoted and several
  // carry human-entered text (a product name, a table number, a line note),
  // so a product called `Kola" onmouseover="…` closed the attribute and
  // injected an event handler that ran in the app's own origin with the
  // waiter's session. The stored variant needed no menu access at all: a QR
  // guest typed a quote into a special instruction and *Turu tekrarla*
  // copied it into the note field's value attribute.
  const HTML_ESCAPES = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };

  function escapeHtml(value) {
    if (value === null || value === undefined) return '';
    return String(value).replace(/[&<>"']/g, (character) => HTML_ESCAPES[character]);
  }

  const moneyFormat = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' });
  const quantityFormat = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 3 });
  const clockFormat = new Intl.DateTimeFormat('tr-TR', { hour: '2-digit', minute: '2-digit' });

  function formatMoney(amount) {
    return moneyFormat.format(Number(amount) || 0);
  }

  // A half portion is 0,5 - the quantity itself, never a separate product or
  // a priced option (V1-RMD-146).
  function formatQuantity(quantity) {
    return quantityFormat.format(Number(quantity) || 0);
  }

  function formatClock(value) {
    const date = value ? new Date(value) : null;
    return date && !Number.isNaN(date.getTime()) ? clockFormat.format(date) : '';
  }

  // docs/UI_STYLE_GUIDE.md §3: a raw status code never reaches the screen.
  function describeHttpFailure(status) {
    if (status === 400) return 'İstek doğrulanamadı. Masa ve ürün bilgilerini kontrol edin.';
    if (status === 401) return 'Oturum geçersiz veya süresi doldu. Yeniden giriş yapın.';
    if (status === 403) return 'Bu işlem için yetkiniz yok.';
    if (status === 404) return 'İlgili kayıt bulunamadı.';
    if (status === 409) return 'Kayıt başka bir işlem tarafından değiştirildi. Tekrar deneyin.';
    if (status === 423) return 'Çok fazla hatalı deneme yapıldı.';
    if (status >= 500) return 'Sunucu hatası oluştu. Tekrar deneyin.';
    return 'İstek sunucu tarafından reddedildi. Tekrar deneyin.';
  }

  // crypto.randomUUID() is secure-context only, so it is undefined over plain
  // HTTP on a LAN IP - which is exactly how a waiter phone reaches the stack.
  // crypto.getRandomValues() IS available there, so build a v4 UUID from it.
  function randomUUID() {
    if (window.crypto && typeof window.crypto.randomUUID === 'function') {
      return window.crypto.randomUUID();
    }
    const bytes = window.crypto.getRandomValues(new Uint8Array(16));
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
  }

  // Each device gets its own terminal id (persisted) so concurrent waiters do
  // not share - and revoke - one cashier device session.
  function deviceTerminalId() {
    let id = localStorage.getItem('alkaros_waiter_terminal_id');
    if (!id) {
      id = randomUUID();
      localStorage.setItem('alkaros_waiter_terminal_id', id);
    }
    return id;
  }

  // ══ Turkish dictionaries ═══════════════════════════════════════════
  // Enum values arrive from the server in English and are never printed raw.

  const TABLE_STATUS = {
    available: { label: 'Boş', cls: 'is-available' },
    occupied: { label: 'Dolu', cls: 'is-occupied' },
    reserved: { label: 'Rezerve', cls: 'is-reserved' },
    cleaning: { label: 'Temizlik', cls: 'is-cleaning' },
    outofservice: { label: 'Servis dışı', cls: 'is-cleaning' }
  };

  const KITCHEN_STATE = {
    notsent: { label: 'Gönderilmedi', cls: '' },
    sent: { label: 'Mutfakta', cls: 'is-sent' },
    preparing: { label: 'Hazırlanıyor', cls: 'is-preparing' },
    ready: { label: 'Hazır', cls: 'is-ready' },
    served: { label: 'Servis edildi', cls: 'is-ready' },
    cancelled: { label: 'İptal', cls: '' }
  };

  // VoidReasonCatalog (src/Modules/Orders/ItemExceptions/ReasonCatalogs.cs).
  // The codes are the server's; only the wording is ours.
  const VOID_REASONS = [
    { code: 'CustomerChange', label: 'Müşteri vazgeçti' },
    { code: 'OperatorError', label: 'Yanlış girdim' },
    { code: 'ProductUnavailable', label: 'Ürün kalmadı' },
    { code: 'DuplicateEntry', label: 'İki kez girilmiş' }
  ];

  const IDLE_LOCK_MS = 3 * 60 * 1000;

  // ══ State ══════════════════════════════════════════════════════════
  // Everything under `server` is a copy of a DTO. Everything under `draft` is
  // the round being composed on this device and not yet sent.

  const state = {
    terminalId: deviceTerminalId(),
    isOnline: navigator.onLine,
    offlineDisabled: false,
    offlineDisabledReason: null,
    user: null,
    capabilities: [],

    zones: [],
    tables: [],
    products: [],
    categories: [],

    activeZone: 'all',
    activeCategory: 'all',
    search: '',

    table: null,
    order: null,
    draft: [],
    // Unsent rounds for tables the waiter stepped away from, keyed by table
    // id. A waiter checking another table mid-order is ordinary; losing what
    // they typed is not.
    //
    // V1-RMD-169: found by the 2026-09-10 Garson audit — this used to be
    // in-memory only, so a page reload (an accidental pull-to-refresh, the
    // browser reclaiming memory, a crash) silently erased whatever a
    // waiter had typed but not yet sent, with no warning and nothing to
    // undo. Restored from localStorage at startup the same way
    // offlineQueue/failedOrders already are; loadDraftsByTable/
    // persistDraftsByTable below keep it in sync every time it changes.
    draftsByTable: loadDraftsByTable(),
    // Bumped whenever the current round is cleared or sent, so a stale undo
    // cannot resurrect a line into a round that no longer exists.
    draftEpoch: 0,

    pending: [],
    offlineQueue: JSON.parse(localStorage.getItem('alkaros_waiter_offline_queue') || '[]'),
    failedOrders: JSON.parse(localStorage.getItem('alkaros_waiter_failed_orders') || '[]'),

    sendInFlight: false,
    optionsMode: null,
    optionsContext: null,
    pinArmed: localStorage.getItem('alkaros_waiter_pin_armed') === '1',
    locked: false,
    pinBuffer: '',
    pushEnabled: false,
    wakeLock: null
  };

  const el = {};
  [
    'ribbon', 'ribbonText', 'ribbonQueue', 'userName', 'userRole', 'userInitials', 'btnProfile',
    'pendingBanner', 'pendingTitle', 'pendingSub',
    'tablesScreen', 'menuScreen', 'zoneChips', 'tablesGrid',
    'btnMenuBack', 'menuTableName', 'menuTableSub', 'productSearch', 'categoryChips', 'productList',
    'cartBar', 'cartCount', 'cartTotal', 'btnOpenBill', 'btnSendFromMenu',
    'billBackdrop', 'billSheet', 'billTitle', 'billSub', 'billBody', 'billTotal',
    'btnAddItems', 'btnMoveTable', 'btnSendToCashier', 'billClose', 'btnSendFromBill',
    'optionsBackdrop', 'optionsSheet', 'optionsTitle', 'optionsSub', 'optionsBody',
    'optionsClose', 'optionsConfirm', 'optionsFootLabel', 'optionsFootValue',
    'toasts', 'loginOverlay', 'loginForm', 'loginUsername', 'loginPassword', 'loginError', 'loginSubmit',
    'lockOverlay', 'lockSub', 'pinDots', 'pinKeys'
  ].forEach((id) => { el[id] = document.getElementById(id); });

  // ══ API ════════════════════════════════════════════════════════════

  function apiUrl(path) {
    return `/api/v1/terminals/${state.terminalId}${path}`;
  }

  // One shape for every call: never throws, never leaks a browser message.
  async function api(path, options) {
    const config = Object.assign({ credentials: 'include' }, options || {});
    if (config.body !== undefined && typeof config.body !== 'string') {
      config.headers = Object.assign({ 'Content-Type': 'application/json' }, config.headers || {});
      config.body = JSON.stringify(config.body);
    }

    let response;
    try {
      response = await fetch(path, config);
    } catch {
      // A network-level failure throws before a response exists and its
      // message is the browser's own English text ("Failed to fetch").
      return { ok: false, status: 0, offline: true, message: 'Sunucuya ulaşılamadı. Bağlantınızı kontrol edin.' };
    }

    let data = null;
    if (response.status !== 204) {
      try { data = await response.json(); } catch { data = null; }
    }

    if (response.ok) return { ok: true, status: response.status, data, headers: response.headers };

    if (response.status === 401) showLogin();
    return {
      ok: false,
      status: response.status,
      data,
      headers: response.headers,
      // The server's own error.message is already Turkish everywhere this
      // client calls (V1-RMD-127); the dictionary covers anything that is not.
      message: (data && data.error && data.error.message) || describeHttpFailure(response.status)
    };
  }

  // ══ Toasts ═════════════════════════════════════════════════════════

  // V1-RMD-166: found by the 2026-09-10 Garson audit — the toast count was
  // unbounded, so a round of several items (one toast each, e.g. a
  // nine-line table) stacked up and covered the screen. An undo toast is
  // never force-closed early — cutting it short would silently take away
  // the one chance to undo a removal — so the cap only ever prunes plain
  // (non-undo) toasts, oldest first.
  const MAX_VISIBLE_TOASTS = 4;

  function toast(text, options) {
    const settings = options || {};
    while (el.toasts.children.length >= MAX_VISIBLE_TOASTS) {
      const oldest = [...el.toasts.children].find((child) => !child.dataset.hasUndo);
      if (!oldest) break;
      oldest.remove();
    }

    const node = document.createElement('div');
    node.className = 'toast';
    if (settings.undo) node.dataset.hasUndo = 'true';
    node.innerHTML = `
      <span class="toast-mark${settings.warning ? ' is-warning' : ''}">
        <svg class="icon" aria-hidden="true"><use href="#ico-${settings.warning ? 'alert' : 'check'}"/></svg>
      </span>
      <span class="toast-text">${escapeHtml(text)}</span>
      ${settings.undo ? '<button type="button" class="toast-undo">Geri al</button>' : ''}`;

    const close = () => { window.clearTimeout(timer); node.remove(); };
    const timer = window.setTimeout(close, settings.warning ? 6000 : 5000);
    if (settings.undo) {
      node.querySelector('.toast-undo').addEventListener('click', () => { close(); settings.undo(); });
    }
    el.toasts.appendChild(node);
  }

  // ══ Screens and sheets ═════════════════════════════════════════════

  function showScreen(name) {
    const onMenu = name === 'menu';
    el.tablesScreen.dataset.state = onMenu ? 'behind' : 'on';
    el.menuScreen.dataset.state = onMenu ? 'on' : 'off';
    if (onMenu) window.setTimeout(() => el.productSearch.focus({ preventScroll: true }), 300);
  }

  // On a tablet the bill is a fixed column, so these calls are no-ops there -
  // the sheet ignores its own transform under the wide media query.
  function openBill() {
    el.billSheet.classList.add('is-open');
    el.billBackdrop.classList.add('is-open');
  }

  function closeBill() {
    el.billSheet.classList.remove('is-open');
    el.billBackdrop.classList.remove('is-open');
  }

  function closeOptions() {
    el.optionsSheet.classList.remove('is-open');
    el.optionsBackdrop.classList.remove('is-open');
    // V1-RMD-172: this sheet stays in the DOM at all times (CSS moves it
    // off-screen instead of removing it), so a closed sheet is still a
    // tab stop unless told otherwise.
    el.optionsSheet.inert = true;
    releaseTrap();
    if (lastOptionsFocus) { lastOptionsFocus.focus(); lastOptionsFocus = null; }
    state.optionsMode = null;
    state.optionsContext = null;
  }

  let lastOptionsFocus = null;

  function openOptions(mode, title, subtitle, bodyHtml, confirmLabel, footLabel, footValue) {
    state.optionsMode = mode;
    el.optionsTitle.textContent = title;
    el.optionsSub.textContent = subtitle || '';
    el.optionsBody.innerHTML = bodyHtml;
    el.optionsFootLabel.textContent = footLabel || '';
    el.optionsFootValue.textContent = footValue || '';
    el.optionsConfirm.textContent = confirmLabel;
    el.optionsConfirm.hidden = !confirmLabel;
    // Reset what the previous caller may have changed, so a danger-styled or
    // disabled button never leaks into the next sheet.
    el.optionsConfirm.className = 'btn btn-primary';
    el.optionsConfirm.disabled = false;
    el.optionsSheet.inert = false;
    el.optionsSheet.classList.add('is-open');
    el.optionsBackdrop.classList.add('is-open');
    lastOptionsFocus = document.activeElement;
    trapBackgroundExcept(el.optionsSheet, el.optionsBackdrop);
    window.setTimeout(() => {
      (el.optionsBody.querySelector('button, input, [tabindex]') || el.optionsConfirm)?.focus();
    }, 0);
  }

  // The tablet bill column starts below whatever chrome is currently showing;
  // the guest banner appears and disappears, so this is measured, not assumed.
  function measureChrome() {
    const header = document.querySelector('.app-header');
    let height = (header ? header.offsetHeight : 0) + (el.ribbon ? el.ribbon.offsetHeight : 0);
    if (!el.pendingBanner.hidden) height += el.pendingBanner.offsetHeight;
    document.documentElement.style.setProperty('--chrome-height', `${height}px`);
  }

  // ══ Sign-in and session ════════════════════════════════════════════

  // V1-RMD-171: found by the 2026-09-10 Garson audit — a reload while
  // genuinely offline used to be treated exactly like "not logged in":
  // this call cannot reach the server to confirm the session either way,
  // so init() showed the full-screen login overlay, hiding the ribbon
  // (and the pending-orders queue behind it) until connectivity came back
  // AND the waiter logged in again — even though their offline queue was
  // sitting safely in localStorage the whole time and their real session
  // was very likely still valid. 'offline' now means "cannot tell, do not
  // assume logged out"; only a real 401/403 from a server that actually
  // answered means 'no'. api()'s own 401 handler already calls
  // showLogin() the moment any later call proves the session really is
  // gone, so proceeding optimistically here is self-correcting, not a
  // security gap.
  async function hasValidSession() {
    const result = await api(`/api/v1/auth/session?terminalId=${state.terminalId}`);
    if (result.offline) return 'offline';
    if (!result.ok) return 'no';
    applyUser(result.data);
    return 'yes';
  }

  function applyUser(user) {
    state.user = user;
    state.capabilities = (user && user.capabilities) || [];
    const name = (user && user.displayName) || 'Garson';
    el.userName.textContent = name;
    el.userInitials.textContent = name.trim().charAt(0).toLocaleUpperCase('tr-TR') || '?';
    // V1-RMD-175: found by the 2026-09-10 Garson audit — #userRole was
    // never written to at all, so it stayed on its static "Garson" HTML
    // default no matter who actually signed in (a supervisor's own
    // profile still said "Garson"). /auth/login and /auth/session now
    // both send the real roleName.
    el.userRole.textContent = (user && user.roleName) || 'Garson';
  }

  function can(permission) {
    return state.capabilities.indexOf(permission) >= 0;
  }

  // V1-RMD-172: found by the 2026-09-10 Garson audit — every overlay here
  // (PIN lock, login, the options/product/void/transfer sheet) hid the
  // rest of the screen visually but left it fully focusable: a Bluetooth
  // keyboard's Tab key, or a screen reader's virtual cursor, could still
  // reach and activate buttons behind the lock screen — the PIN lock in
  // particular is a real security gap, not just an accessibility one.
  // `inert` (standard, no polyfill needed at this app's browser baseline)
  // makes everything outside the active overlay simultaneously
  // unfocusable, unclickable and invisible to assistive tech - the
  // platform's own answer to "trap focus", nothing to reimplement by
  // hand. `toasts` is deliberately never inert-ed: a toast's own "geri
  // al" button must stay reachable no matter what else is open.
  let releaseBackgroundTrap = null;

  function trapBackgroundExcept(...activeElements) {
    if (releaseBackgroundTrap) releaseBackgroundTrap();
    const active = new Set(activeElements);
    const affected = Array.from(document.body.children)
      .filter((child) => !active.has(child) && child.id !== 'toasts');
    affected.forEach((child) => { child.inert = true; });
    releaseBackgroundTrap = () => {
      affected.forEach((child) => { child.inert = false; });
      releaseBackgroundTrap = null;
    };
  }

  function releaseTrap() {
    if (releaseBackgroundTrap) releaseBackgroundTrap();
  }

  function showLogin() {
    el.loginOverlay.hidden = false;
    el.lockOverlay.hidden = true;
    state.locked = false;
    trapBackgroundExcept(el.loginOverlay);
    el.loginUsername.focus();
  }

  async function submitLogin(event) {
    event.preventDefault();
    const username = (el.loginUsername.value || '').trim();
    const password = el.loginPassword.value || '';
    if (!username || !password) return;

    el.loginError.hidden = true;
    el.loginSubmit.disabled = true;
    el.loginSubmit.textContent = 'Giriş yapılıyor…';
    try {
      const result = await api('/api/v1/auth/login', {
        method: 'POST',
        body: { terminalId: state.terminalId, username, password }
      });
      if (!result.ok) {
        el.loginError.textContent = result.status === 401
          ? 'Kullanıcı adı veya şifre hatalı.'
          : result.message;
        el.loginError.hidden = false;
        return;
      }
      applyUser(result.data);
      el.loginPassword.value = '';
      el.loginOverlay.hidden = true;
      releaseTrap();
      await start();
    } finally {
      el.loginSubmit.disabled = false;
      el.loginSubmit.textContent = 'Giriş yap';
    }
  }

  async function signOut() {
    // The subscription is attributed to whoever signed in, so it goes with
    // them - the next waiter on this device subscribes as themselves.
    await unsubscribePush();
    await api(`/api/v1/auth/logout?terminalId=${state.terminalId}`, { method: 'POST', body: {} });
    localStorage.removeItem('alkaros_waiter_pin_armed');
    window.location.reload();
  }

  // ══ Connection ribbon ══════════════════════════════════════════════

  function renderRibbon() {
    const offline = state.offlineDisabled || !state.isOnline;
    el.ribbon.classList.toggle('is-offline', offline);
    if (state.offlineDisabled) {
      // V1-RMD-171: found by the 2026-09-10 Garson audit — this said
      // "güvenli bağlantı (HTTPS) gerekli" for every reason offline mode
      // could be disabled, including two where the connection is already
      // secure and HTTPS is not the problem at all (the browser lacking
      // service worker support, or registration failing for an unrelated
      // reason such as sw.js itself being unreachable) - on a genuinely
      // secure connection the banner blamed HTTPS anyway.
      el.ribbonText.textContent = OFFLINE_DISABLED_REASONS[state.offlineDisabledReason]
        || OFFLINE_DISABLED_REASONS.unknown;
    } else if (state.isOnline) {
      el.ribbonText.textContent = 'Bağlı';
    } else {
      el.ribbonText.textContent = 'Bağlantı yok — siparişler kuyrukta bekliyor';
    }

    const waiting = state.offlineQueue.length;
    const failed = state.failedOrders.length;
    const parts = [];
    if (waiting > 0) parts.push(`${waiting} bekleyen`);
    if (failed > 0) parts.push(`${failed} hatalı`);
    el.ribbonQueue.textContent = parts.join(' • ');
    // V1-RMD-171: found by the 2026-09-10 Garson audit — the count used
    // to be the whole story; nothing said which table, what was in it, or
    // gave a way to clear it. Now a real button into a real list.
    el.ribbonQueue.hidden = waiting === 0 && failed === 0;
    el.ribbonQueue.setAttribute('aria-label',
      `${parts.join(', ')} - sipariş kuyruğunu göster`);
    measureChrome();
  }

  // V1-RMD-171: one accurate Turkish sentence per real reason offline mode
  // can end up disabled, instead of a single "HTTPS gerekli" that was
  // wrong whenever the actual cause was something else.
  const OFFLINE_DISABLED_REASONS = {
    insecure: 'Çevrimdışı mod kapalı — güvenli bağlantı (HTTPS) gerekli',
    unsupported: 'Çevrimdışı mod bu tarayıcıda desteklenmiyor',
    'registration-failed': 'Çevrimdışı mod kurulamadı — sayfayı yenileyin',
    unknown: 'Çevrimdışı mod kapalı'
  };

  function registerOfflineWorker() {
    if (!window.isSecureContext) {
      state.offlineDisabled = true;
      state.offlineDisabledReason = 'insecure';
      renderRibbon();
      console.warn('Offline mode disabled: a secure context (HTTPS or localhost) is required.');
      return;
    }
    if (!('serviceWorker' in navigator)) {
      state.offlineDisabled = true;
      state.offlineDisabledReason = 'unsupported';
      renderRibbon();
      console.warn('Offline mode disabled: this browser has no service worker support.');
      return;
    }
    navigator.serviceWorker.register('./sw.js').catch((err) => {
      state.offlineDisabled = true;
      state.offlineDisabledReason = 'registration-failed';
      renderRibbon();
      console.warn('Service worker registration failed; offline mode is disabled:', err);
    });
  }

  // ══ Loading ════════════════════════════════════════════════════════

  async function loadZones() {
    const result = await api(apiUrl('/table-management/zones'));
    const list = result.ok ? (Array.isArray(result.data) ? result.data : result.data.zones || []) : [];
    state.zones = [{ id: 'all', name: 'Tümü' }].concat(
      list.map((zone) => ({ id: zone.zoneId || zone.id, name: zone.zoneName || zone.name })));
  }

  // V1-RMD-163: found by the 2026-09-10 Garson audit — the catalog
  // endpoint paginates at 1000 rows and answers an X-Next-Cursor header
  // whenever more remain, but this used to fetch one page and stop. A
  // restaurant with more than 1000 rows would silently lose everything
  // past the first page. Follows the cursor until the server stops
  // sending one.
  //
  // V1-RMD-167: found in independent review of V1-RMD-163 — a failure on
  // a LATER page (items already collected from earlier ones) used to
  // return ok:true with only the partial catalog, which is exactly the
  // silently-incomplete-catalog failure mode this whole fix exists to
  // close, just moved one page later. Any page failing now fails the
  // whole fetch, matching cashier-app.js's own fetchWholeCatalogAsync
  // (which throws on any non-ok page) — showing nothing/an error beats
  // showing a catalog missing an unknown number of products with no
  // indication anything is wrong.
  async function fetchWholeCatalogAsync() {
    const items = [];
    let cursor = null;
    for (;;) {
      const path = cursor
        ? `${apiUrl('/catalog')}?cursor=${encodeURIComponent(cursor)}`
        : apiUrl('/catalog');
      const result = await api(path);
      if (!result.ok) return { ok: false, items: [] };
      const pageItems = Array.isArray(result.data) ? result.data : result.data.items || [];
      items.push(...pageItems);
      cursor = result.headers && result.headers.get ? result.headers.get('X-Next-Cursor') : null;
      if (!cursor) return { ok: true, items };
    }
  }

  async function loadCatalog() {
    // V1-RMD-129: categories are derived from this one response's own
    // categoryCode/categoryName. There is no separate categories endpoint a
    // waiter session may call.
    const result = await fetchWholeCatalogAsync();
    if (!result.ok) {
      state.products = [];
      state.categories = [];
      return;
    }
    const list = result.items;
    state.products = list.map((product) => ({
      id: product.productId,
      name: product.name,
      price: product.unitPrice || 0,
      categoryCode: product.categoryCode,
      categoryName: product.categoryName,
      // V1-RMD-148: the option groups the server says this product has. The
      // client never invents one and never prices one.
      modifierGroups: product.modifierGroups || []
    }));

    const seen = new Map();
    for (const product of list) {
      if (product.categoryCode && !seen.has(product.categoryCode)) {
        seen.set(product.categoryCode, product.categoryName || product.categoryCode);
      }
    }
    state.categories = Array.from(seen, ([id, name]) => ({ id, name }));
  }

  async function loadTables() {
    const result = await api(apiUrl('/table-management/tables'));
    const list = result.ok ? (Array.isArray(result.data) ? result.data : result.data.tables || []) : [];
    state.tables = list.map((table) => ({
      id: table.tableId,
      number: table.tableNumber,
      seats: table.capacity || 0,
      zoneId: table.zoneId,
      status: (table.status || 'Available').toLowerCase(),
      rowVersion: table.rowVersion,
      // V1-RMD-135: the table's real running total.
      amount: table.currentOrderTotal || 0,
      // foundations §0.2: which actions are valid is the server's answer.
      allowedCommands: table.allowedCommands || []
    }));
    renderTables();
  }

  // The bill is always the server's answer, never a local accumulation.
  async function loadOrder(tableId) {
    const result = await api(apiUrl(`/orders/table/${tableId}`));
    state.order = result.ok ? result.data : null;
    return result;
  }

  async function loadPending() {
    const result = await api(apiUrl('/orders/pending'));
    state.pending = result.ok && Array.isArray(result.data) ? result.data : [];
    renderPendingBanner();
  }

  // ══ Tables ═════════════════════════════════════════════════════════

  function renderZones() {
    el.zoneChips.innerHTML = state.zones.map((zone) => `
      <button type="button" class="chip" data-zone="${escapeHtml(zone.id)}"
              aria-pressed="${state.activeZone === zone.id}">${escapeHtml(zone.name)}</button>`).join('');
  }

  function renderTables() {
    const visible = state.activeZone === 'all'
      ? state.tables
      : state.tables.filter((table) => table.zoneId === state.activeZone);

    if (visible.length === 0) {
      el.tablesGrid.innerHTML = '<div class="empty">Bu bölgede masa yok.</div>';
      return;
    }

    el.tablesGrid.innerHTML = visible.map((table) => {
      const status = TABLE_STATUS[table.status] || { label: table.status, cls: '' };
      // V1-RMD-168: found by the 2026-09-10 Garson audit (foundations.md
      // §0.2) — this used to read `table.amount > 0`, deriving whether a
      // table is occupied from money instead of the server's own status
      // field. A table with an open check whose current total happens to
      // be zero (every line comped, or a Draft round not yet priced) would
      // wrongly look empty; the server already says "Occupied" directly
      // (set when an order attaches to the table, V1-ORD-006) and that is
      // what "does this table already have an order to add to" actually
      // means, not its running total.
      const busy = table.status === 'occupied';
      // A table already carrying an order gets a shortcut straight to the
      // menu; tapping the table itself opens its bill.
      const quick = busy
        ? `<button type="button" class="table-quick" data-quick="${escapeHtml(table.id)}"
                   aria-label="${escapeHtml(table.number)} masasına ürün ekle">
             <svg class="icon" aria-hidden="true"><use href="#ico-add"/></svg>
           </button>`
        : '';
      return `
        <div class="table-cell${busy ? ' has-quick' : ''}">
          <button type="button" class="table ${status.cls}" data-table="${escapeHtml(table.id)}">
            <span class="table-top">
              <span class="table-number">${escapeHtml(table.number)}</span>
              <span class="table-seats">
                <svg class="icon" aria-hidden="true"><use href="#ico-seats"/></svg>${escapeHtml(table.seats)}
              </span>
            </span>
            <span class="tagrow"><span class="tag tag-status">${escapeHtml(status.label)}</span></span>
            <span class="table-amount${busy ? '' : ' is-empty'}">${busy ? formatMoney(table.amount) : 'Boş'}</span>
          </button>
          ${quick}
        </div>`;
    }).join('');
  }

  // ══ Opening a table ════════════════════════════════════════════════

  async function openTable(tableId, goStraightToMenu) {
    const table = state.tables.find((candidate) => candidate.id === tableId);
    if (!table) return;

    // An unsent round belongs to the table it was composed for, and it is
    // kept there. The previous version showed a toast saying the round was
    // still held and then deleted it on the very next line — nine items
    // typed for table 5 vanished the moment the waiter glanced at table 7,
    // while the screen claimed otherwise.
    if (state.table && state.table.id !== tableId) {
      if (state.draft.length > 0) {
        state.draftsByTable.set(state.table.id, {
          number: state.table.number,
          lines: state.draft
        });
        toast(`${state.table.number} masasının gönderilmemiş turu saklandı.`, { warning: true });
      } else {
        state.draftsByTable.delete(state.table.id);
      }
    }
    if (!state.table || state.table.id !== tableId) {
      const held = state.draftsByTable.get(tableId);
      state.draft = held ? held.lines : [];
      state.draftsByTable.delete(tableId);
    }
    persistDraftsByTable();

    state.table = table;
    el.menuTableName.textContent = `${table.number} masası`;
    el.billTitle.textContent = `${table.number} masası`;

    await loadOrder(tableId);
    renderBill();

    // The locked flow: an occupied table opens its bill, an empty one opens
    // the menu. The bill sheet is never raised over the menu - that covers the
    // very screen the waiter came to use.
    const empty = !state.order || activeItems().length === 0;
    if (empty || goStraightToMenu) {
      closeBill();
      showScreen('menu');
      renderProducts();
    } else {
      showScreen('tables');
      openBill();
    }
  }

  function activeItems() {
    if (!state.order || !state.order.items) return [];
    // A cancelled or wasted line stays in the DTO; it is history, not a bill
    // line the waiter can still act on.
    return state.order.items.filter((item) => item.status !== 'Cancelled' && item.status !== 'Waste');
  }

  // ══ Menu ═══════════════════════════════════════════════════════════

  function renderCategories() {
    const all = [{ id: 'all', name: 'Tümü' }].concat(state.categories);
    el.categoryChips.innerHTML = all.map((category) => `
      <button type="button" class="chip" data-category="${escapeHtml(category.id)}"
              aria-pressed="${state.activeCategory === category.id}">${escapeHtml(category.name)}</button>`).join('');
  }

  function draftQuantityOf(productId) {
    return state.draft
      .filter((line) => line.productId === productId)
      .reduce((sum, line) => sum + line.quantity, 0);
  }

  function renderProducts() {
    const query = state.search.trim().toLocaleLowerCase('tr-TR');
    const visible = state.products.filter((product) => {
      const matchesCategory = state.activeCategory === 'all' || product.categoryCode === state.activeCategory;
      const matchesQuery = !query || product.name.toLocaleLowerCase('tr-TR').includes(query);
      return matchesCategory && matchesQuery;
    });

    if (visible.length === 0) {
      el.productList.innerHTML = '<div class="empty">Aramanıza uyan ürün yok.</div>';
      return;
    }

    let html = '';
    let lastCategory = null;
    const grouped = state.activeCategory === 'all' && !query;
    for (const product of visible) {
      if (grouped && product.categoryCode !== lastCategory) {
        lastCategory = product.categoryCode;
        html += `<div class="cat-label">${escapeHtml(product.categoryName || product.categoryCode)}</div>`;
      }
      const inDraft = draftQuantityOf(product.id);
      const hasOptions = product.modifierGroups.length > 0;
      html += `
        <div class="product-row">
          <button type="button" class="product" data-product="${escapeHtml(product.id)}">
            <span class="product-main">
              <span class="product-name">${escapeHtml(product.name)}</span>
              ${hasOptions ? '<span class="product-meta"><span class="product-options">Seçenekli</span></span>' : ''}
            </span>
            <span class="product-price">${formatMoney(product.price)}</span>
            ${inDraft > 0 ? `<span class="product-count">${escapeHtml(formatQuantity(inDraft))}</span>` : ''}
          </button>
          <button type="button" class="product-half" data-half="${escapeHtml(product.id)}"
                  aria-label="${escapeHtml(product.name)} — miktar seç">½</button>
        </div>`;
    }
    el.productList.innerHTML = html;
  }

  // ══ The draft round ════════════════════════════════════════════════

  function addToDraft(product, quantity, modifiers, note) {
    // Lines that are identical in every respect merge; anything with its own
    // options or note stays its own line so the kitchen ticket reads right.
    const plain = (!modifiers || modifiers.length === 0) && !note;
    if (plain) {
      const existing = state.draft.find((line) =>
        line.productId === product.id && line.modifiers.length === 0 && !line.note);
      if (existing) {
        existing.quantity = Math.round((existing.quantity + quantity) * 1000) / 1000;
        afterDraftChange();
        return;
      }
    }
    state.draft.push({
      id: randomUUID(),
      productId: product.id,
      name: product.name,
      price: product.price,
      quantity,
      modifiers: modifiers || [],
      note: note || ''
    });
    afterDraftChange();
  }

  // How many of each chosen option the server will record for a line of this
  // size (V1-RMD-150's own rule, applied when the request carries no count).
  // Used only to show a total before sending; the sent line always renders
  // the server's own numbers.
  function modifierCountFor(quantity) {
    return Math.max(1, Math.ceil(quantity));
  }

  function lineExtras(line) {
    const count = modifierCountFor(line.quantity);
    return line.modifiers.reduce((sum, modifier) => sum + modifier.priceDelta * count, 0);
  }

  function draftTotal() {
    return state.draft.reduce((sum, line) => sum + line.price * line.quantity + lineExtras(line), 0);
  }

  function afterDraftChange() {
    const count = state.draft.reduce((sum, line) => sum + line.quantity, 0);
    el.cartCount.textContent = formatQuantity(count);
    el.cartTotal.textContent = formatMoney(draftTotal());
    const empty = state.draft.length === 0;
    el.btnSendFromMenu.disabled = empty;
    el.btnSendFromBill.disabled = empty;
    renderProducts();
    renderBill();

    // V1-RMD-169: keeps the CURRENT table's own unsent round in the same
    // held-drafts map (and so in localStorage) it lands in the moment the
    // waiter steps away — every keystroke while composing is already
    // durable, not only the state as of the last table switch.
    if (state.table) {
      if (state.draft.length > 0) {
        state.draftsByTable.set(state.table.id, { number: state.table.number, lines: state.draft });
      } else {
        state.draftsByTable.delete(state.table.id);
      }
      persistDraftsByTable();
    }
  }

  // ══ The bill ═══════════════════════════════════════════════════════

  // "Turu tekrarla" is the last round the server itself recorded, grouped by
  // the createdAt the DTO now carries (V1-RMD-146) - not a guess kept on this
  // device.
  function lastRound() {
    const items = activeItems();
    if (items.length === 0) return null;
    const newest = items.reduce((latest, item) =>
      (!latest || item.createdAt > latest ? item.createdAt : latest), null);
    if (!newest) return null;
    const round = items.filter((item) => item.createdAt === newest);
    return round.length > 0 ? round : null;
  }

  function renderQuickSend() {
    if (state.draft.length > 0) return '';
    const round = lastRound();
    if (!round) return '';
    const total = round.reduce((sum, item) => sum + item.totalPrice, 0);
    const names = round.map((item) => `${formatQuantity(item.quantity)}× ${item.productName}`).join(', ');
    return `
      <div class="group-label">Hızlı gönder</div>
      <div class="quick">
        <button type="button" class="quick-btn is-primary" data-quick-repeat="1">
          <span class="title">Turu tekrarla</span>
          <span class="sub">${escapeHtml(formatMoney(total))}</span>
        </button>
        <div class="quick-detail">${escapeHtml(names)}</div>
      </div>`;
  }

  function renderBill() {
    if (!state.table) {
      el.billBody.innerHTML = '<div class="empty">Bir masaya dokunun.</div>';
      el.billTotal.textContent = formatMoney(0);
      el.billSub.textContent = '';
      el.btnAddItems.hidden = true;
      el.btnMoveTable.hidden = true;
      el.btnSendToCashier.hidden = true;
      return;
    }
    el.btnAddItems.hidden = false;

    const sent = activeItems();
    // foundations §0.1: the money on screen is the server's own figure, not a
    // sum this client recomputes. Adding up the line prices ignored anything
    // applied at order level — a comped line or a bill discount — so the
    // table tile (which does use the server's number) and the bill footer
    // disagreed, and the waiter quoted the wrong one to the guest. Only the
    // unsent round, which the server has not seen yet, is estimated here.
    const sentTotal = state.order ? state.order.totalAmount : 0;
    el.billTotal.textContent = formatMoney(sentTotal + draftTotal());
    el.billSub.textContent = state.order
      ? `${sent.length} kalem • ${formatClock(state.order.createdAt)}`
      : 'Açık sipariş yok';
    el.menuTableSub.textContent = el.billSub.textContent;

    // Transfer is offered only when the server's own AllowedCommands says so.
    el.btnMoveTable.hidden = state.table.allowedCommands.indexOf('Transfer') < 0;
    // V1-ORD-006: only an open check that has actually been ordered can go to
    // the till — there is nothing to send from an empty table, and an unsent
    // round would be left behind.
    el.btnSendToCashier.hidden = !state.order || sent.length === 0 || state.draft.length > 0;

    let html = renderQuickSend();

    if (state.draft.length > 0) {
      html += '<div class="group-label"><span>Yeni tur</span><span>gönderilmedi</span></div>';
      html += state.draft.map(renderDraftLine).join('');
    }

    // V1-RMD-166: found by the 2026-09-10 Garson audit — every item on
    // state.order used to render under the "Gönderildi" header just for
    // being non-cancelled, even one whose own kitchenState is NotSent (the
    // server persisted it, e.g. table-draft succeeded, but it was never
    // actually fired to the kitchen - the exact case V1-RMD-164 fixed the
    // correction path for). The section header said "Sent" while the
    // line's own badge said "Not sent", directly contradicting it. Split
    // into two groups instead of one.
    const awaitingDispatch = sent.filter((item) => (item.kitchenState || 'NotSent') === 'NotSent');
    const dispatched = sent.filter((item) => (item.kitchenState || 'NotSent') !== 'NotSent');

    if (awaitingDispatch.length > 0) {
      html += '<div class="group-label"><span>Gönderilmeyi bekliyor</span><span>gönderilmedi</span></div>';
      html += awaitingDispatch.map(renderSentLine).join('');
    }

    if (dispatched.length > 0) {
      // V1-RMD-167: found in independent review of V1-RMD-166 — sentTotal
      // is the server's total for the WHOLE order (every active item,
      // dispatched or not; §0.1 above is exactly why this never recomputes
      // it from lines). Printing it next to "Gönderildi" was correct only
      // when every active item is dispatched; the moment a table also has
      // an awaitingDispatch line, this label summed to more than the lines
      // shown under it — the same contradiction this split was meant to
      // remove, moved from the badge text into the money. No server figure
      // exists for "dispatched-only total", and one is never invented
      // client-side, so the label is shown only when it is truly this
      // group's total.
      const dispatchedLabel = awaitingDispatch.length === 0 ? escapeHtml(formatMoney(sentTotal)) : '';
      html += `<div class="group-label"><span>Gönderildi</span><span>${dispatchedLabel}</span></div>`;
      html += dispatched.map(renderSentLine).join('');
    }

    if (!html) html = '<div class="empty">Bu masada henüz sipariş yok.<br>Ürün ekleyerek başlayın.</div>';
    el.billBody.innerHTML = html;
  }

  function renderDraftLine(line) {
    const extras = lineExtras(line);
    const count = modifierCountFor(line.quantity);
    // A count is shown only where it changes what is charged. A free
    // instruction ("az pişmiş") never reads as "2× az pişmiş".
    const chips = line.modifiers.map((modifier) => `
      <span class="chip-mod${modifier.priceDelta > 0 ? ' is-paid' : ''}">
        ${modifier.priceDelta > 0 && count > 1 ? `${escapeHtml(formatQuantity(count))}× ` : ''}${escapeHtml(modifier.name)}
      </span>`).join('');
    return `
      <div class="line">
        <div class="line-main">
          <div class="line-top">
            <span class="line-name">${escapeHtml(line.name)}</span>
            <span class="line-total">${escapeHtml(formatMoney(line.price * line.quantity + extras))}</span>
          </div>
          <div class="line-unit">${escapeHtml(formatQuantity(line.quantity))} × ${escapeHtml(formatMoney(line.price))}</div>
          ${chips ? `<div class="line-chips">${chips}</div>` : ''}
          <input class="note" type="text" maxlength="200" data-note="${escapeHtml(line.id)}"
                 value="${escapeHtml(line.note)}" placeholder="Not (az pişmiş, acısız…)"
                 aria-label="${escapeHtml(line.name)} için not">
        </div>
        <div class="line-side">
          <div class="stepper">
            <button type="button" data-step="-" data-line="${escapeHtml(line.id)}" aria-label="Azalt">−</button>
            <span class="qty">${escapeHtml(formatQuantity(line.quantity))}</span>
            <button type="button" data-step="+" data-line="${escapeHtml(line.id)}" aria-label="Artır">+</button>
          </div>
        </div>
      </div>`;
  }

  function renderSentLine(item) {
    const kitchen = KITCHEN_STATE[(item.kitchenState || '').toLowerCase()] || null;
    const chips = (item.modifiers || []).map((modifier) => `
      <span class="chip-mod${modifier.priceDelta > 0 ? ' is-paid' : ''}">
        ${modifier.quantity > 1 ? `${escapeHtml(formatQuantity(modifier.quantity))}× ` : ''}${escapeHtml(modifier.name)}
      </span>`).join('');

    // V1-RMD-143: how many more of this product the mapped stock could still
    // cover. Null means the product is not stock-tracked at all - which is not
    // the same as none left, so nothing is shown.
    const stock = item.availableStockQuantity !== null && item.availableStockQuantity !== undefined
      ? `<span class="product-stock${item.availableStockQuantity <= 0 ? ' is-out' : (item.availableStockQuantity < 5 ? ' is-low' : '')}">
           Kalan ${escapeHtml(formatQuantity(item.availableStockQuantity))}
         </span>`
      : '';

    // V1-RMD-154/155: which of the two void paths this line belongs to.
    //
    // A line that has not gone to the kitchen voids for free. One that has
    // needs the grant-gated path, which cancels the kitchen ticket and gives
    // the stock back — and for a waiter raises a manager approval. Offering
    // no button at all (which is what V1-RMD-154 left, because that endpoint
    // had no client) meant a wrongly-sent dish could not be cancelled at all.
    //
    // V1-RMD-168: found by the 2026-09-10 Garson audit (foundations.md
    // §0.2, "which action is valid is the server's answer") — this used to
    // derive both flags itself from kitchenState alone, missing the Status
    // half of each real eligibility check
    // (ItemExceptionHandler.VoidItemAsync, SentItemVoidStore.VoidAsync).
    // canVoid/canVoidSent are now the server's own computed fields; the
    // client only shows or hides a button, never re-derives whether the
    // click would succeed.
    const canVoid = item.canVoid;
    const canVoidSent = item.canVoidSent;

    return `
      <div class="line">
        <div class="line-main">
          <div class="line-top">
            <span class="line-name">${escapeHtml(item.productName)}</span>
            <span class="line-total">${escapeHtml(formatMoney(item.totalPrice))}</span>
          </div>
          <div class="line-unit">${escapeHtml(formatQuantity(item.quantity))} × ${escapeHtml(formatMoney(item.unitPrice))}${
            item.createdAt ? ` • ${escapeHtml(formatClock(item.createdAt))}` : ''}</div>
          ${chips ? `<div class="line-chips">${chips}</div>` : ''}
          <div class="line-chips">
            ${kitchen ? `<span class="kitchen-state ${kitchen.cls}"><span class="dot"></span>${escapeHtml(kitchen.label)}</span>` : ''}
            ${stock}
          </div>
          ${item.specialInstructions ? `<div class="line-unit">${escapeHtml(item.specialInstructions)}</div>` : ''}
        </div>
        <div class="line-side">
          ${canVoid ? `<button type="button" class="btn-void" data-void="${escapeHtml(item.itemId)}">İptal</button>` : ''}
          ${canVoidSent ? `<button type="button" class="btn-void" data-void-sent="${escapeHtml(item.itemId)}">İptal iste</button>` : ''}
        </div>
      </div>`;
  }

  // ══ Quantity and options sheet ═════════════════════════════════════

  function openProductSheet(product, presetQuantity) {
    state.optionsContext = {
      product,
      quantity: presetQuantity || 1,
      chosen: new Map()
    };

    openOptions('product', product.name, '', productSheetHtml(), 'Adisyona ekle', 'Tutar', '');
    updateProductSheetTotal();
  }

  function productSheetHtml() {
    const context = state.optionsContext;
    const quantities = [0.5, 1, 1.5, 2, 3];
    let html = `
      <div class="optgroup">
        <div class="optgroup-head"><span class="optgroup-name">Miktar</span>
          <span class="optgroup-rule">yarım porsiyon 0,5</span></div>
        <div class="qty-row">
          ${quantities.map((quantity) => `
            <button type="button" class="qty-quick" data-qty="${quantity}"
                    aria-pressed="${context.quantity === quantity}">${escapeHtml(formatQuantity(quantity))}</button>`).join('')}
        </div>
      </div>`;

    for (const group of context.product.modifierGroups) {
      const single = group.selectionType === 'Single';
      const required = group.minSelections > 0;
      const rule = required
        ? (single ? 'zorunlu — bir tane seçin' : `zorunlu — en az ${group.minSelections}`)
        : (single ? 'bir tane seçilebilir' : `en fazla ${group.maxSelections}`);
      html += `
        <div class="optgroup" data-group="${escapeHtml(group.modifierGroupId)}">
          <div class="optgroup-head">
            <span class="optgroup-name">${escapeHtml(group.name)}</span>
            <span class="optgroup-rule${required ? ' is-required' : ''}">${escapeHtml(rule)}</span>
          </div>
          <div class="opts">
            ${group.modifiers.map((modifier) => `
              <button type="button" class="opt" data-modifier="${escapeHtml(modifier.modifierId)}"
                      data-group="${escapeHtml(group.modifierGroupId)}" data-single="${single}"
                      aria-pressed="${context.chosen.has(modifier.modifierId)}">
                <span class="opt-box${single ? ' is-round' : ''}">
                  <svg class="icon" aria-hidden="true"><use href="#ico-check"/></svg>
                </span>
                <span class="opt-name">${escapeHtml(modifier.name)}</span>
                <span class="opt-delta${modifier.priceDelta ? '' : ' is-free'}">${
                  modifier.priceDelta ? `+${escapeHtml(formatMoney(modifier.priceDelta))}` : 'ücretsiz'}</span>
              </button>`).join('')}
          </div>
        </div>`;
    }

    html += `
      <div class="optgroup">
        <div class="optgroup-head"><span class="optgroup-name">Not</span></div>
        <input class="note" type="text" maxlength="200" data-product-note
               placeholder="Mutfağa not (az pişmiş, soğansız…)" aria-label="Mutfağa not">
      </div>`;
    return html;
  }

  function chosenModifiers() {
    const context = state.optionsContext;
    return Array.from(context.chosen.values());
  }

  function updateProductSheetTotal() {
    const context = state.optionsContext;
    const count = modifierCountFor(context.quantity);
    const extras = chosenModifiers().reduce((sum, modifier) => sum + modifier.priceDelta * count, 0);
    el.optionsFootValue.textContent = formatMoney(context.product.price * context.quantity + extras);
  }

  // ══ Sending ════════════════════════════════════════════════════════

  // V1-RMD-160: waiterName and createdAt used to be sent here but nothing
  // on the server ever read either (found by the 2026-09-10 Garson audit).
  // The real actor is already attributed server-side via
  // Order.ServingUserId, from the session this request already carries;
  // the real timestamp is server-authoritative (DateTimeOffset.UtcNow at
  // the point the draft is created), same as everywhere else in this
  // system — a client clock is never the source of truth for it. Removed
  // rather than wired in.
  function draftToPayload() {
    return {
      id: randomUUID(),
      tableId: state.table.id,
      tableNumber: state.table.number,
      items: state.draft.map((line) => ({
        // A stable per-line id makes a retried draft submission idempotent
        // server-side instead of appending a duplicate line.
        id: line.id,
        productId: line.productId,
        productName: line.name,
        quantity: line.quantity,
        unitPrice: line.price,
        // V1-RMD-147: ids only. The price of an option is the catalog's
        // answer and the count is the server's own rule (V1-RMD-150 leaves
        // the field optional for exactly that) - neither is this client's to
        // assert.
        modifiers: line.modifiers.map((modifier) => ({ modifierId: modifier.modifierId })),
        specialInstructions: line.note || null
      }))
    };
  }

  // V1-RMD-163: found by the 2026-09-10 Garson audit — an
  // X-Idempotency-Key header used to be sent here too, carrying the exact
  // same value as payload.id in the body. No endpoint anywhere ever reads
  // that header (grep confirmed); the real, working idempotency
  // protection is payload.id itself, which the server persists as
  // Order.SourceReferenceId behind a partial unique index (V1-RMD-123).
  // Removed the header as a pointless duplicate rather than wiring up a
  // second mechanism for the same value.
  async function postOrder(payload) {
    const draft = await api(apiUrl('/orders/table-draft'), { method: 'POST', body: payload });
    if (!draft.ok) return draft;

    // The draft alone never reaches the kitchen; the submit is what dispatches
    // it. The operation id identifies THIS ROUND: `payload.id` is generated
    // once per round and resent unchanged on every retry of it, so a retry
    // replays and the next round is a new operation.
    //
    // It used to be `${orderId}:submit`, which was right only while one order
    // meant one submission. Once a check started taking a second round
    // (V1-ORD-006) every round on that check reused the same key, and the
    // second one came back 409 IDEMPOTENCY_KEY_REUSED — the food never
    // reached the kitchen.
    return api(apiUrl(`/orders/${draft.data.orderId}/submit-draft`), {
      method: 'POST',
      headers,
      body: {
        orderId: draft.data.orderId,
        expectedRowVersion: draft.data.rowVersion,
        operationId: `${draft.data.orderId}:${payload.id}`
      }
    });
  }

  async function sendDraft() {
    if (state.draft.length === 0 || !state.table || state.sendInFlight) return;
    state.sendInFlight = true;
    el.btnSendFromMenu.disabled = true;
    el.btnSendFromBill.disabled = true;

    const payload = draftToPayload();
    const tableNumber = state.table.number;
    try {
      if (!state.isOnline) {
        queueOrder(payload);
        state.draft = [];
        state.draftEpoch += 1;
        afterDraftChange();
        toast(`Bağlantı yok — ${tableNumber} siparişi kuyruğa alındı.`, { warning: true });
        return;
      }

      const result = await postOrder(payload);
      if (result.ok) {
        state.draft = [];
        state.draftEpoch += 1;
        await loadOrder(state.table.id);
        await loadTables();
        afterDraftChange();
        showScreen('tables');
        toast(`${tableNumber} siparişi mutfağa gönderildi.`);
      } else if (result.status >= 400 && result.status < 500) {
        // A rejected order is kept on screen so nothing typed is lost.
        toast(result.message, { warning: true });
      } else {
        queueOrder(payload);
        state.draft = [];
        state.draftEpoch += 1;
        afterDraftChange();
        toast(`Sunucuya ulaşılamadı — ${tableNumber} siparişi kuyruğa alındı.`, { warning: true });
      }
    } finally {
      state.sendInFlight = false;
      afterDraftChange();
    }
  }

  // ══ Held drafts (unsent rounds, per table) ═══════════════════════════

  // V1-RMD-169: see the field's own comment on state.draftsByTable. Stored
  // as an array of [tableId, {number, lines}] pairs since a Map is not
  // directly JSON-serializable.
  function loadDraftsByTable() {
    try {
      const raw = localStorage.getItem('alkaros_waiter_drafts_by_table');
      return raw ? new Map(JSON.parse(raw)) : new Map();
    } catch {
      // Corrupt/foreign localStorage content must never crash startup -
      // worst case the held drafts are gone, same as before this fix.
      return new Map();
    }
  }

  function persistDraftsByTable() {
    localStorage.setItem(
      'alkaros_waiter_drafts_by_table',
      JSON.stringify([...state.draftsByTable]));
  }

  // ══ Offline queue ══════════════════════════════════════════════════

  function persistQueue() {
    localStorage.setItem('alkaros_waiter_offline_queue', JSON.stringify(state.offlineQueue));
    localStorage.setItem('alkaros_waiter_failed_orders', JSON.stringify(state.failedOrders));
    renderRibbon();
  }

  function queueOrder(payload) {
    state.offlineQueue.push(Object.assign({}, payload, { queuedAt: new Date().toISOString() }));
    persistQueue();
    scheduleQueueRetry();
  }

  // The queue used to be flushed only by the browser's `online` event and by
  // start(). But the commonest way into the queue is not going offline at
  // all — it is the server answering 5xx while the network is perfectly up,
  // and in that state `online` never fires. A round queued that way sat in
  // localStorage for the rest of the shift and the kitchen never saw it.
  //
  // So the queue now retries itself, backing off so a server that is down
  // does not get hammered, and stops as soon as the queue empties.
  const QUEUE_RETRY_MIN_MS = 15000;
  const QUEUE_RETRY_MAX_MS = 5 * 60 * 1000;
  let queueRetryTimer = null;
  let queueRetryDelay = QUEUE_RETRY_MIN_MS;
  let flushInFlight = false;

  function scheduleQueueRetry() {
    window.clearTimeout(queueRetryTimer);
    if (state.offlineQueue.length === 0) {
      queueRetryDelay = QUEUE_RETRY_MIN_MS;
      return;
    }
    queueRetryTimer = window.setTimeout(() => {
      queueRetryDelay = Math.min(queueRetryDelay * 2, QUEUE_RETRY_MAX_MS);
      void flushQueue();
    }, queueRetryDelay);
  }

  async function flushQueue() {
    if (state.offlineQueue.length === 0 || !state.isOnline) return;
    // Two overlapping flushes would send the same payload twice. The server
    // is idempotent on the submission id, so this is a courtesy rather than
    // the last line of defence — but it also keeps the ribbon honest.
    if (flushInFlight) return;
    flushInFlight = true;

    try {
      for (const payload of state.offlineQueue.slice()) {
        const result = await postOrder(payload);
        if (result.ok) {
          state.offlineQueue = state.offlineQueue.filter((queued) => queued.id !== payload.id);
          // A success means the server is back; drop the backoff.
          queueRetryDelay = QUEUE_RETRY_MIN_MS;
        } else if (result.status === 429) {
          // Rate limited while draining a long queue. Retryable, and filing
          // it as permanently failed would destroy the round.
          break;
        } else if (result.status >= 400 && result.status < 500) {
          // A 4xx will never succeed on retry, but the order is never destroyed:
          // it moves to the failed list and the ribbon keeps saying so.
          state.offlineQueue = state.offlineQueue.filter((queued) => queued.id !== payload.id);
          state.failedOrders.push(Object.assign({}, payload, { rejectedAt: new Date().toISOString(), error: result.message }));
          console.error('Order rejected by server:', payload.id, result.status, result.message);
        } else {
          // Temporary failure: keep the rest queued and stop trying for now.
          break;
        }
      }
      persistQueue();
      if (state.table) { await loadOrder(state.table.id); renderBill(); }
      await loadTables();
    } finally {
      flushInFlight = false;
      scheduleQueueRetry();
    }
  }

  // ══ Voiding a line ═════════════════════════════════════════════════

  // Keyed by item id so a retry after approval reuses the same request.
  const pendingVoidKeys = new Map();

  // V1-RMD-155: the grant-gated path for a line the kitchen already has.
  // Unlike the free void it may not resolve immediately — a waiter's request
  // goes to a manager and comes back 202 Pending. The idempotency key is
  // generated once per attempt and kept, so retrying after the manager
  // approves resolves to that same request instead of opening a second one.
  function openVoidSentSheet(itemId) {
    const item = activeItems().find((candidate) => candidate.itemId === itemId);
    if (!item || !state.order) return;

    state.optionsContext = {
      itemId,
      reason: null,
      idempotencyKey: pendingVoidKeys.get(itemId) || randomUUID()
    };

    const kitchen = KITCHEN_STATE[(item.kitchenState || '').toLowerCase()];
    const body = `
      <div class="callout">
        <svg class="icon" aria-hidden="true"><use href="#ico-alert"/></svg>
        <span>Bu ürün mutfağa gitti${kitchen ? ` (${escapeHtml(kitchen.label)})` : ''}.
        İptali yönetici onayına gidebilir. Onaylanırsa mutfak bileti de iptal edilir
        ve stok geri alınır.</span>
      </div>
      <div class="opts">
        ${VOID_REASONS.map((reason) => `
          <button type="button" class="opt" data-reason="${escapeHtml(reason.code)}" aria-pressed="false">
            <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-check"/></svg></span>
            <span class="opt-name">${escapeHtml(reason.label)}</span>
          </button>`).join('')}
      </div>`;

    openOptions('void-sent', 'Mutfaktaki ürünü iptal et',
      `${formatQuantity(item.quantity)} × ${item.productName}`, body, 'İptal iste', '', '');
    el.optionsConfirm.className = 'btn btn-danger';
    el.optionsConfirm.disabled = true;
  }

  async function confirmVoidSent() {
    const context = state.optionsContext;
    if (!context || !context.reason || !state.order) return;
    el.optionsConfirm.disabled = true;

    const result = await api(
      apiUrl(`/orders/${state.order.orderId}/items/${context.itemId}/void-sent`),
      {
        method: 'POST',
        body: {
          idempotencyKey: context.idempotencyKey,
          expectedRowVersion: state.order.rowVersion,
          reasonCode: context.reason
        }
      });

    if (!result.ok) {
      toast(result.message, { warning: true });
      el.optionsConfirm.disabled = false;
      return;
    }

    closeOptions();
    if (result.data && result.data.status === 'Pending') {
      // Keep the key: the same request has to be resent once a manager
      // resolves it, or a second grant would be raised for one decision.
      pendingVoidKeys.set(context.itemId, context.idempotencyKey);
      toast('İptal yönetici onayına gönderildi.', { warning: true });
      return;
    }

    pendingVoidKeys.delete(context.itemId);
    await loadOrder(state.table.id);
    await loadTables();
    renderBill();
    const restored = result.data && result.data.stockRestored;
    toast(restored ? 'Ürün iptal edildi, stok geri alındı.' : 'Ürün iptal edildi.');
  }

  function openVoidSheet(itemId) {
    const item = activeItems().find((candidate) => candidate.itemId === itemId);
    if (!item || !state.order) return;

    state.optionsContext = { itemId, reason: null };
    const body = `
      <div class="callout">
        <svg class="icon" aria-hidden="true"><use href="#ico-alert"/></svg>
        <span>İptal geri alınamaz ve kaydı tutulur. Bir gerekçe seçin.</span>
      </div>
      <div class="opts">
        ${VOID_REASONS.map((reason) => `
          <button type="button" class="opt" data-reason="${escapeHtml(reason.code)}" aria-pressed="false">
            <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-check"/></svg></span>
            <span class="opt-name">${escapeHtml(reason.label)}</span>
          </button>`).join('')}
      </div>`;
    openOptions('void', 'Kalemi iptal et', `${formatQuantity(item.quantity)} × ${item.productName}`,
      body, 'İptal et', '', '');
    el.optionsConfirm.className = 'btn btn-danger';
    el.optionsConfirm.disabled = true;
  }

  async function confirmVoid() {
    const context = state.optionsContext;
    if (!context || !context.reason || !state.order) return;
    el.optionsConfirm.disabled = true;
    const result = await api(
      apiUrl(`/orders/${state.order.orderId}/items/${context.itemId}/void`),
      { method: 'POST', body: { expectedRowVersion: state.order.rowVersion, reasonCode: context.reason } });

    if (!result.ok) {
      toast(result.message, { warning: true });
      el.optionsConfirm.disabled = false;
      return;
    }
    closeOptions();
    await loadOrder(state.table.id);
    await loadTables();
    renderBill();
    toast('Kalem iptal edildi.');
  }

  // ══ Guest orders waiting for confirmation ══════════════════════════

  function renderPendingBanner() {
    const count = state.pending.length;
    el.pendingBanner.hidden = count === 0;
    if (count > 0) {
      const first = state.pending[0];
      el.pendingTitle.textContent = count === 1
        ? `${first.tableNumber} masası sipariş verdi`
        : `${count} masa sipariş verdi`;
      el.pendingSub.textContent = count === 1
        ? `${first.itemCount} kalem • ${formatMoney(first.total)} • onayınızı bekliyor`
        : 'Onayınızı bekliyor';
    }
    measureChrome();
  }

  function openPendingSheet() {
    if (state.pending.length === 0) return;
    const body = state.pending.map((order) => `
      <div class="line">
        <div class="line-main">
          <div class="line-top">
            <span class="line-name">${escapeHtml(order.tableNumber)} masası</span>
            <span class="line-total">${escapeHtml(formatMoney(order.total))}</span>
          </div>
          <div class="line-unit">${escapeHtml(order.itemCount)} kalem • ${escapeHtml(formatClock(order.createdAt))}</div>
        </div>
        <div class="line-side">
          <button type="button" class="btn btn-send btn-compact" data-accept="${escapeHtml(order.orderId)}">Onayla</button>
          <button type="button" class="btn-void" data-reject="${escapeHtml(order.orderId)}">Reddet</button>
        </div>
      </div>`).join('');
    openOptions('pending', 'Misafir siparişi', 'QR ile verilen siparişler onayınızı bekliyor', body, '', '', '');
    el.optionsConfirm.hidden = true;
  }

  // Accept and reject both need the order's current row version, which the
  // summary does not carry - so it is read first rather than guessed.
  async function resolvePending(orderId, accept) {
    const detail = await api(apiUrl(`/orders/${orderId}`));
    if (!detail.ok) { toast(detail.message, { warning: true }); return; }

    const body = accept
      ? { expectedRowVersion: detail.data.rowVersion, notes: null }
      : { expectedRowVersion: detail.data.rowVersion, reason: 'Garson reddetti' };
    const result = await api(apiUrl(`/orders/${orderId}/${accept ? 'accept' : 'reject'}`), { method: 'POST', body });

    if (!result.ok) { toast(result.message, { warning: true }); return; }
    toast(accept
      ? `${detail.data.tableNumber} siparişi mutfağa gönderildi.`
      : `${detail.data.tableNumber} siparişi reddedildi.`);

    await loadPending();
    await loadTables();
    if (state.pending.length === 0) closeOptions(); else openPendingSheet();
    if (state.table) { await loadOrder(state.table.id); renderBill(); }
  }

  // ══ Sending the check to the cashier ═══════════════════════════════
  // V1-ORD-006. The party has eaten and is walking to the till; the table has
  // to be free for the next one before they get there. The check keeps its own
  // identity and waits at the cashier — it is not closed and nothing is paid
  // here.

  function openSendToCashierSheet() {
    if (!state.table || !state.order) return;

    const total = activeItems().reduce((sum, item) => sum + item.totalPrice, 0);
    const body = `
      <div class="callout">
        <svg class="icon" aria-hidden="true"><use href="#ico-alert"/></svg>
        <span>Hesap kasaya gider ve masa yeni müşteriye açılır. Bu adım ödeme almaz.</span>
      </div>
      <div class="line">
        <div class="line-main">
          <div class="line-top">
            <span class="line-name">${escapeHtml(state.table.number)} masası</span>
            <span class="line-total">${escapeHtml(formatMoney(total))}</span>
          </div>
          <div class="line-unit">${escapeHtml(activeItems().length)} kalem</div>
        </div>
      </div>`;

    state.optionsContext = { orderId: state.order.orderId, tableId: state.table.id };
    openOptions('cashier', 'Hesabı kasaya gönder', '', body, 'Kasaya gönder', '', '');
  }

  async function confirmSendToCashier() {
    const context = state.optionsContext;
    if (!context) return;
    el.optionsConfirm.disabled = true;

    const result = await api(apiUrl(`/orders/${context.orderId}/send-to-cashier`), {
      method: 'POST',
      body: { tableId: context.tableId }
    });
    if (!result.ok) {
      toast(result.message, { warning: true });
      el.optionsConfirm.disabled = false;
      return;
    }

    const tableNumber = state.table.number;
    closeOptions();
    closeBill();
    state.table = null;
    state.order = null;
    state.draft = [];
    state.draftEpoch += 1;
    await loadTables();
    afterDraftChange();
    showScreen('tables');
    toast(result.data.alreadySent
      ? `${tableNumber} hesabı zaten kasaya gönderilmişti.`
      : `${tableNumber} hesabı kasaya gönderildi, masa boşaldı.`);
  }

  // ══ Failed / queued orders ═══════════════════════════════════════════

  // V1-RMD-171: found by the 2026-09-10 Garson audit — the ribbon's own
  // "N hatalı" count was the whole story: no table, no content, and no
  // way to clear it. This opens a real, reachable list of both the still-
  // retrying (offline queue) and the permanently rejected (failed) rounds,
  // with what each one actually contained and a way to dismiss a
  // permanently-failed one once the waiter has dealt with it by hand
  // (re-entering it, or telling the guest).
  function openFailedOrdersSheet() {
    const queued = state.offlineQueue.map((payload) => queuedOrderRow(payload, false));
    const failed = state.failedOrders.map((payload) => queuedOrderRow(payload, true));
    const rows = queued.concat(failed);

    const body = rows.length === 0
      ? '<div class="empty">Bekleyen veya hatalı sipariş yok.</div>'
      : `<div class="opts">${rows.join('')}</div>`;

    openOptions(
      'failed-orders',
      'Bekleyen ve hatalı siparişler',
      queued.length > 0
        ? `${queued.length} sunucuya ulaşmayı bekliyor, gönderilmemiş değil.`
        : '',
      body,
      failed.length > 0 ? 'Hatalı olanların tümünü temizle' : '',
      '', '');
    el.optionsConfirm.className = 'btn btn-danger';
    el.optionsConfirm.hidden = failed.length === 0;
  }

  function queuedOrderRow(payload, isFailed) {
    const itemCount = (payload.items || []).reduce((sum, item) => sum + (item.quantity || 0), 0);
    const itemNames = (payload.items || []).map((item) => item.name || item.productName).join(', ');
    return `
      <div class="opt" style="cursor:default">
        <span class="opt-box is-round">
          <svg class="icon" aria-hidden="true"><use href="#ico-${isFailed ? 'alert' : 'bell'}"/></svg>
        </span>
        <span class="opt-name">
          ${escapeHtml(payload.tableNumber || '?')} masası — ${escapeHtml(formatQuantity(itemCount))} kalem
          <span class="line-unit">${escapeHtml(itemNames)}</span>
          ${isFailed && payload.error ? `<span class="line-unit">${escapeHtml(payload.error)}</span>` : ''}
        </span>
        ${isFailed
          ? `<button type="button" class="btn-void" data-dismiss-failed="${escapeHtml(payload.id)}">Sil</button>`
          : ''}
      </div>`;
  }

  function dismissFailedOrder(payloadId) {
    state.failedOrders = state.failedOrders.filter((payload) => payload.id !== payloadId);
    persistQueue();
    openFailedOrdersSheet();
  }

  function clearAllFailedOrders() {
    state.failedOrders = [];
    persistQueue();
    closeOptions();
    toast('Hatalı siparişler temizlendi.');
  }

  // ══ Moving a table ═════════════════════════════════════════════════

  function openTransferSheet() {
    if (!state.table) return;
    const targets = state.tables.filter((table) =>
      table.id !== state.table.id && table.status === 'available');

    const body = targets.length === 0
      ? '<div class="empty">Şu anda boş masa yok.</div>'
      : `<div class="opts">${targets.map((table) => `
          <button type="button" class="opt" data-target="${escapeHtml(table.id)}" aria-pressed="false">
            <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-check"/></svg></span>
            <span class="opt-name">${escapeHtml(table.number)} masası</span>
            <span class="opt-code">${escapeHtml(table.seats)} kişilik</span>
          </button>`).join('')}</div>`;

    state.optionsContext = { targetId: null };
    openOptions('transfer', 'Masa değiştir',
      `${state.table.number} masasındaki sipariş ve hesap taşınır`, body, 'Taşı', '', '');
    el.optionsConfirm.className = 'btn btn-primary';
    el.optionsConfirm.disabled = true;
  }

  async function confirmTransfer() {
    const context = state.optionsContext;
    if (!context || !context.targetId) return;
    const target = state.tables.find((table) => table.id === context.targetId);
    if (!target) return;

    el.optionsConfirm.disabled = true;
    const result = await api(apiUrl('/table-management/transfers'), {
      method: 'POST',
      body: {
        sourceTableId: state.table.id,
        expectedSourceRowVersion: state.table.rowVersion,
        targetTableId: target.id,
        expectedTargetRowVersion: target.rowVersion,
        reason: 'Misafir masa değiştirdi'
      }
    });

    if (!result.ok) {
      toast(result.message, { warning: true });
      el.optionsConfirm.disabled = false;
      return;
    }
    const from = state.table.number;
    closeOptions();
    await loadTables();
    const moved = state.tables.find((table) => table.id === target.id);
    if (moved) await openTable(moved.id, false);
    toast(`${from} masası ${target.number} masasına taşındı.`);
  }

  // ══ Web Push ═══════════════════════════════════════════════════════
  // V1-WTR-011. SignalR only reaches a device whose app is open; a plated
  // dish is announced exactly when it is not. The server does the encryption
  // (RFC 8291), so all this side does is subscribe and hand the browser's own
  // subscription over.

  function pushSupported() {
    return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
  }

  // iOS grants Web Push only to a PWA installed on the home screen. Detecting
  // it lets the screen say why instead of failing silently.
  function iosNeedsInstall() {
    const isIos = /iPad|iPhone|iPod/.test(navigator.userAgent);
    const standalone = window.matchMedia('(display-mode: standalone)').matches
      || window.navigator.standalone === true;
    return isIos && !standalone;
  }

  async function currentPushSubscription() {
    if (!pushSupported() || !navigator.serviceWorker.controller) {
      const registration = await navigator.serviceWorker.getRegistration();
      if (!registration) return null;
      return registration.pushManager.getSubscription();
    }
    const registration = await navigator.serviceWorker.ready;
    return registration.pushManager.getSubscription();
  }

  async function refreshPushState() {
    try {
      state.pushEnabled = (await currentPushSubscription()) !== null;
    } catch {
      state.pushEnabled = false;
    }
  }

  async function enablePush() {
    if (!pushSupported()) {
      toast('Bu tarayıcı arka plan bildirimini desteklemiyor.', { warning: true });
      return;
    }
    if (iosNeedsInstall()) {
      toast('Önce uygulamayı ana ekrana ekleyin; iPhone bildirimi yalnız öyle veriyor.', { warning: true });
      return;
    }
    if (!window.isSecureContext) {
      toast('Arka plan bildirimi için güvenli bağlantı (HTTPS) gerekli.', { warning: true });
      return;
    }
    // V1-RMD-169: found by the 2026-09-10 Garson audit — if
    // registerOfflineWorker() already failed (state.offlineDisabled), no
    // service worker will ever activate, so the `navigator.serviceWorker.ready`
    // await further down used to hang forever: no error, no toast, the
    // waiter tapping "bildirimleri aç" just got a screen that never
    // responded again.
    if (state.offlineDisabled) {
      toast('Arka plan bildirimi kurulamadı: çevrimdışı mod kapalı.', { warning: true });
      return;
    }

    // The browser requires this to come from a user gesture, which is why it
    // lives behind a button in the profile sheet and not in start().
    let permission;
    try {
      permission = await Notification.requestPermission();
    } catch {
      permission = 'denied';
    }
    if (permission !== 'granted') {
      toast('Bildirim izni verilmedi.', { warning: true });
      return;
    }

    const key = await api(apiUrl('/push/public-key'));
    if (!key.ok) { toast(key.message, { warning: true }); return; }

    try {
      // V1-RMD-169: a bare `await navigator.serviceWorker.ready` still has
      // no timeout of its own — if registration reports success but the
      // worker somehow never actually activates (a genuinely broken state,
      // distinct from the registerOfflineWorker() failure already checked
      // above), this raced it against a bound instead of hanging silently
      // forever with the sheet stuck open.
      const registration = await Promise.race([
        navigator.serviceWorker.ready,
        new Promise((_, reject) => window.setTimeout(
          () => reject(new Error('service-worker-timeout')), 10000))
      ]);
      const subscription = await registration.pushManager.subscribe({
        // Chrome refuses a subscription that could be silent, and every
        // notification this app sends is shown anyway.
        userVisibleOnly: true,
        applicationServerKey: base64UrlToBytes(key.data.publicKey)
      });

      const payload = subscription.toJSON();
      const saved = await api(apiUrl('/push/subscriptions'), {
        method: 'POST',
        body: {
          endpoint: payload.endpoint,
          p256dh: payload.keys.p256dh,
          auth: payload.keys.auth
        }
      });
      if (!saved.ok) {
        // Registering the subscription is what makes it reachable; a browser
        // subscription the server does not know about is worse than none.
        await subscription.unsubscribe();
        toast(saved.message, { warning: true });
        return;
      }
      state.pushEnabled = true;
      toast('Uygulama kapalıyken de bildirim gelecek.');
    } catch {
      toast('Bildirim aboneliği kurulamadı.', { warning: true });
    }
  }

  async function unsubscribePush() {
    try {
      const subscription = await currentPushSubscription();
      if (subscription) {
        await api(apiUrl(`/push/subscriptions?endpoint=${encodeURIComponent(subscription.endpoint)}`),
          { method: 'DELETE' });
        await subscription.unsubscribe();
      }
    } catch {
      // Nothing to undo beyond the local flag; the server drops a dead
      // endpoint on its own the next time it sends (RFC 8030 §7.3).
    }
    state.pushEnabled = false;
  }

  async function disablePush() {
    await unsubscribePush();
    toast('Arka plan bildirimi kapatıldı.');
  }

  function base64UrlToBytes(value) {
    const padded = (value + '='.repeat((4 - (value.length % 4)) % 4))
      .replace(/-/g, '+').replace(/_/g, '/');
    const binary = window.atob(padded);
    return Uint8Array.from(binary, (character) => character.charCodeAt(0));
  }

  // ══ Full screen ════════════════════════════════════════════════════
  // Semih's own "tam ekranda çıkmayı zorlaştırmak". A web page cannot pin
  // itself the way Android Ekran Sabitleme or iOS Rehberli Erişim can - that
  // is an operating-system setting. What it can do is take the whole screen,
  // keep it awake, and make leaving cost something: if a PIN is set, dropping
  // out of full screen locks the device.

  function isFullscreen() {
    return document.fullscreenElement !== null && document.fullscreenElement !== undefined;
  }

  async function requestWakeLock() {
    if (!('wakeLock' in navigator)) return;
    try {
      state.wakeLock = await navigator.wakeLock.request('screen');
    } catch {
      // Denied, or the tab is not visible. The screen simply dims as usual.
      state.wakeLock = null;
    }
  }

  async function releaseWakeLock() {
    try { if (state.wakeLock) await state.wakeLock.release(); }
    catch { /* already gone */ }
    state.wakeLock = null;
  }

  async function toggleFullscreen() {
    try {
      if (isFullscreen()) {
        await document.exitFullscreen();
        await releaseWakeLock();
        return;
      }
      await document.documentElement.requestFullscreen({ navigationUI: 'hide' });
      await requestWakeLock();
      // Landscape and portrait are both legitimate on a tablet, so the
      // orientation is deliberately left unlocked (V1-RMD-145 removed the
      // lock from the manifest for the same reason).
    } catch {
      toast('Tam ekrana geçilemedi.', { warning: true });
    }
  }

  function onFullscreenChange() {
    if (isFullscreen()) return;
    void releaseWakeLock();
    // Leaving full screen is how someone gets out of the app. With a PIN set
    // that hands them the lock screen instead of the order list.
    if (state.pinArmed && !state.locked) lockScreen();
  }

  // ══ Profile, PIN and the kiosk lock ════════════════════════════════

  function openProfileSheet() {
    void refreshPushState().then(() => {
      const body = `
        <div class="opts">
          <button type="button" class="opt" data-profile="fullscreen">
            <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-expand"/></svg></span>
            <span class="opt-name">${isFullscreen() ? 'Tam ekrandan çık' : 'Tam ekran'}</span>
          </button>
          <button type="button" class="opt" data-profile="push">
            <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-bell"/></svg></span>
            <span class="opt-name">${state.pushEnabled
              ? 'Arka plan bildirimini kapat'
              : 'Uygulama kapalıyken de bildir'}</span>
          </button>
          <button type="button" class="opt" data-profile="lock">
            <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-lock"/></svg></span>
            <span class="opt-name">${state.pinArmed ? 'Ekranı şimdi kilitle' : 'Ekran kilidini kur'}</span>
          </button>
          ${state.pinArmed ? `
          <button type="button" class="opt" data-profile="pin-off">
            <span class="opt-box is-round"></span>
            <span class="opt-name">Ekran kilidini kaldır</span>
          </button>` : ''}
          <button type="button" class="opt" data-profile="signout">
            <span class="opt-box is-round"></span>
            <span class="opt-name">Oturumu kapat</span>
          </button>
        </div>`;
      openOptions('profile', (state.user && state.user.displayName) || 'Personel',
        'Bu cihaz için ayarlar', body, '', '', '');
      el.optionsConfirm.hidden = true;
    });
  }

  // Setting a PIN needs the current password on top of the session, so a
  // device left unlocked on a table cannot have one planted on it.
  function openPinSheet(removing) {
    state.optionsContext = { removing: !!removing };
    const body = `
      <div class="callout">
        <svg class="icon" aria-hidden="true"><use href="#ico-alert"/></svg>
        <span>${removing
          ? 'Kilidi kaldırmak için şifrenizi doğrulayın.'
          : 'Ekran 3 dakika boştayken kilitlenir. Açmak için bu PIN yeterlidir; şifreniz istenmez.'}</span>
      </div>
      <div class="optgroup">
        <div class="optgroup-head"><span class="optgroup-name">Şifreniz</span></div>
        <input class="note" type="password" data-pin-password autocomplete="current-password" aria-label="Şifreniz">
      </div>
      ${removing ? '' : `
      <div class="optgroup">
        <div class="optgroup-head"><span class="optgroup-name">Yeni PIN</span>
          <span class="optgroup-rule">en az 4 rakam</span></div>
        <input class="note" type="password" inputmode="numeric" data-pin-code
               autocomplete="one-time-code" aria-label="Yeni PIN">
      </div>`}`;
    openOptions('pin', removing ? 'Kilidi kaldır' : 'Ekran kilidi', '', body,
      removing ? 'Kaldır' : 'Kur', '', '');
    el.optionsConfirm.className = removing ? 'btn btn-danger' : 'btn btn-primary';
    el.optionsConfirm.disabled = false;
  }

  async function confirmPin() {
    const removing = state.optionsContext && state.optionsContext.removing;
    const password = el.optionsBody.querySelector('[data-pin-password]').value || '';
    const codeField = el.optionsBody.querySelector('[data-pin-code]');
    const code = codeField ? (codeField.value || '') : null;

    if (!password) { toast('Şifrenizi girin.', { warning: true }); return; }
    if (!removing && (code.length < 4 || !/^\d+$/.test(code))) {
      toast('PIN en az 4 rakam olmalı.', { warning: true });
      return;
    }

    el.optionsConfirm.disabled = true;
    const result = await api(`/api/v1/auth/pin?terminalId=${state.terminalId}`, {
      method: 'POST',
      body: { currentPassword: password, pin: removing ? null : code }
    });
    if (!result.ok) {
      toast(result.message, { warning: true });
      el.optionsConfirm.disabled = false;
      return;
    }

    // A local flag only decides whether this device arms its idle timer; the
    // server stays the authority on whether the PIN itself is valid.
    state.pinArmed = !removing;
    localStorage.setItem('alkaros_waiter_pin_armed', state.pinArmed ? '1' : '0');
    closeOptions();
    toast(removing ? 'Ekran kilidi kaldırıldı.' : 'Ekran kilidi kuruldu.');
  }

  let idleTimer = null;

  function resetIdleTimer() {
    window.clearTimeout(idleTimer);
    if (!state.pinArmed || state.locked || el.loginOverlay.hidden === false) return;
    idleTimer = window.setTimeout(lockScreen, IDLE_LOCK_MS);
  }

  function lockScreen() {
    if (!state.pinArmed || state.locked) return;
    state.locked = true;
    state.pinBuffer = '';
    el.lockSub.textContent = 'PIN kodunuzu girin';
    el.pinDots.classList.remove('is-wrong');
    renderPinDots();
    el.lockOverlay.hidden = false;
    // Deliberately no Escape-to-close and no data-pin keys reachable from
    // outside: this overlay exists specifically so a keyboard cannot walk
    // around it.
    trapBackgroundExcept(el.lockOverlay);
    el.pinKeys.querySelector('[data-pin="1"]')?.focus();
  }

  function renderPinDots() {
    el.pinDots.innerHTML = Array.from({ length: Math.max(4, state.pinBuffer.length) },
      (unused, index) => `<span class="${index < state.pinBuffer.length ? 'is-filled' : ''}"></span>`).join('');
  }

  function renderPinPad() {
    const keys = ['1', '2', '3', '4', '5', '6', '7', '8', '9'];
    el.pinKeys.innerHTML = keys.map((key) =>
      `<button type="button" class="pin-key" data-pin="${key}">${key}</button>`).join('')
      + '<button type="button" class="pin-key" data-pin="del" aria-label="Sil">⌫</button>'
      + '<button type="button" class="pin-key" data-pin="0">0</button>'
      + '<button type="button" class="pin-key" data-pin="ok" aria-label="Aç">✓</button>';
  }

  async function submitPin() {
    if (state.pinBuffer.length < 4) return;
    const result = await api(`/api/v1/auth/unlock?terminalId=${state.terminalId}`, {
      method: 'POST',
      body: { pin: state.pinBuffer }
    });

    if (result.ok) {
      state.locked = false;
      state.pinBuffer = '';
      el.lockOverlay.hidden = true;
      releaseTrap();
      resetIdleTimer();
      return;
    }

    if (result.status === 409) {
      // The server says this account has no PIN, so the flag on this device
      // is stale - drop the lock rather than trapping the waiter behind it.
      state.pinArmed = false;
      localStorage.setItem('alkaros_waiter_pin_armed', '0');
      state.locked = false;
      el.lockOverlay.hidden = true;
      releaseTrap();
      toast('Bu hesapta PIN tanımlı değil, kilit kaldırıldı.', { warning: true });
      return;
    }

    state.pinBuffer = '';
    renderPinDots();
    el.pinDots.classList.add('is-wrong');
    window.setTimeout(() => el.pinDots.classList.remove('is-wrong'), 400);
    el.lockSub.textContent = result.status === 423
      ? 'Çok fazla deneme. Kullanıcı adı ve şifreyle girin.'
      : 'PIN hatalı, tekrar deneyin.';
    if (result.status === 423) showLogin();
  }

  // ══ Events ═════════════════════════════════════════════════════════

  function bindEvents() {
    // V1-RMD-172: the sheet is a permanent DOM node CSS moves off-screen,
    // so before it has ever been opened once it would otherwise still be
    // a live tab stop.
    el.optionsSheet.inert = true;
    el.loginForm.addEventListener('submit', submitLogin);
    el.btnProfile.addEventListener('click', openProfileSheet);

    window.addEventListener('online', () => {
      state.isOnline = true;
      renderRibbon();
      void flushQueue();
      // V1-RMD-171: if the app started offline (hasValidSession() could
      // not reach the server, see its own comment), state.user was never
      // populated. Backfilling it once the network is actually back is
      // display-only (name/initials in the header) - every permission
      // check already re-resolves against state.capabilities on its own
      // next read, and a session that turns out to have truly expired
      // still gets caught by api()'s own 401 handler on the very next
      // real call.
      if (!state.user) {
        void hasValidSession().then(async () => {
          // The very first load(s) after start() ran offline came back
          // empty (each already falls back gracefully rather than
          // throwing) - now that the network is actually back, load the
          // real data once instead of leaving the screen looking empty
          // until the waiter manually reloads the page.
          await loadZones();
          await loadCatalog();
          await loadTables();
          await loadPending();
          renderZones();
          renderCategories();
          renderProducts();
          renderBill();
          afterDraftChange();
        });
      }
    });
    window.addEventListener('offline', () => { state.isOnline = false; renderRibbon(); });
    window.addEventListener('resize', measureChrome);

    ['pointerdown', 'keydown'].forEach((event) =>
      document.addEventListener(event, resetIdleTimer, { passive: true }));

    document.addEventListener('fullscreenchange', onFullscreenChange);
    // A screen wake lock is dropped whenever the tab is hidden and is not
    // restored on its own, so it is re-taken when the app comes back.
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState !== 'visible') return;
      if (isFullscreen() && !state.wakeLock) void requestWakeLock();
      // Coming back to the app is the moment a waiter would expect a stuck
      // round to go out, and a backgrounded PWA's timers may have been
      // throttled to nothing while it was away.
      void flushQueue();
    });

    el.zoneChips.addEventListener('click', (event) => {
      const chip = event.target.closest('[data-zone]');
      if (!chip) return;
      state.activeZone = chip.dataset.zone;
      renderZones();
      renderTables();
    });

    el.tablesGrid.addEventListener('click', (event) => {
      const quick = event.target.closest('[data-quick]');
      if (quick) { void openTable(quick.dataset.quick, true); return; }
      const table = event.target.closest('[data-table]');
      if (table) void openTable(table.dataset.table, false);
    });

    el.btnMenuBack.addEventListener('click', () => { showScreen('tables'); });
    el.btnOpenBill.addEventListener('click', openBill);
    el.billClose.addEventListener('click', closeBill);
    el.billBackdrop.addEventListener('click', closeBill);
    el.optionsBackdrop.addEventListener('click', closeOptions);
    el.optionsClose.addEventListener('click', closeOptions);
    // V1-RMD-172: found by the 2026-09-10 Garson audit — none of the
    // sheets responded to Escape at all. The PIN lock is deliberately
    // excluded (Escape must never be a way out of it) and the bill sheet
    // is a fixed column on tablet rather than a dismissable modal, so
    // only the options sheet closes here.
    document.addEventListener('keydown', (event) => {
      if (event.key === 'Escape' && el.optionsSheet.classList.contains('is-open')) {
        closeOptions();
      }
    });

    el.btnAddItems.addEventListener('click', () => {
      if (!state.table) return;
      showScreen('menu');
      renderProducts();
      closeBill();
    });
    el.btnMoveTable.addEventListener('click', openTransferSheet);
    el.btnSendToCashier.addEventListener('click', openSendToCashierSheet);
    el.btnSendFromMenu.addEventListener('click', sendDraft);
    el.btnSendFromBill.addEventListener('click', sendDraft);
    el.pendingBanner.addEventListener('click', openPendingSheet);
    el.ribbonQueue.addEventListener('click', openFailedOrdersSheet);

    el.productSearch.addEventListener('input', (event) => {
      state.search = event.target.value;
      renderProducts();
    });

    el.categoryChips.addEventListener('click', (event) => {
      const chip = event.target.closest('[data-category]');
      if (!chip) return;
      state.activeCategory = chip.dataset.category;
      renderCategories();
      renderProducts();
    });

    el.productList.addEventListener('click', (event) => {
      const half = event.target.closest('[data-half]');
      if (half) {
        const product = state.products.find((candidate) => candidate.id === half.dataset.half);
        if (product) openProductSheet(product, 0.5);
        return;
      }
      const button = event.target.closest('[data-product]');
      if (!button) return;
      const product = state.products.find((candidate) => candidate.id === button.dataset.product);
      if (!product) return;

      // A product with options cannot be added blind - the sheet asks first.
      if (product.modifierGroups.length > 0) { openProductSheet(product, 1); return; }
      // V1-RMD-173: found by the 2026-09-10 Garson audit — addToDraft()
      // runs afterDraftChange(), which re-renders the whole product list
      // (draftQuantityOf(product.id) changed, so every row's own markup
      // does too). `button` is the OLD node by the time addToDraft()
      // returns - it has already been removed from the document and
      // replaced by a fresh one from the same innerHTML rewrite. Adding
      // the animation class to it did nothing visible, and if this
      // button held keyboard focus (a Bluetooth keyboard, or Tab
      // navigation), that focus silently fell back to <body> the instant
      // the old node was discarded. Re-finding the real, current button
      // by the one thing that still identifies it (the product id) fixes
      // both: the animation plays where the waiter is actually looking,
      // and focus follows onto the new node instead of vanishing.
      addToDraft(product, 1, [], '');
      const freshButton = el.productList.querySelector(`[data-product="${CSS.escape(product.id)}"]`);
      if (freshButton) {
        freshButton.classList.add('just-added');
        window.setTimeout(() => freshButton.classList.remove('just-added'), 400);
        if (document.activeElement === document.body || document.activeElement === button) {
          freshButton.focus();
        }
      }
    });

    el.billBody.addEventListener('click', (event) => {
      const repeat = event.target.closest('[data-quick-repeat]');
      if (repeat) { repeatLastRound(); return; }

      const step = event.target.closest('[data-step]');
      if (step) {
        const line = state.draft.find((candidate) => candidate.id === step.dataset.line);
        if (!line) return;
        const delta = step.dataset.step === '+' ? 0.5 : -0.5;
        line.quantity = Math.round((line.quantity + delta) * 1000) / 1000;
        if (line.quantity <= 0) {
          const removed = line;
          const index = state.draft.indexOf(line);
          // The undo has to remember which table it belonged to. The toast
          // lives five seconds — long enough to send the round or walk to
          // another table — and the closure used to read state.draft at click
          // time, so a late tap dropped the line into whatever round was on
          // screen by then, or resurrected it after the round had been sent.
          const ownerTableId = state.table.id;
          const epoch = state.draftEpoch;
          state.draft.splice(index, 1);
          toast(`${removed.name} çıkarıldı.`, {
            undo: () => {
              if (!state.table || state.table.id !== ownerTableId || state.draftEpoch !== epoch) {
                toast(`${removed.name} geri alınamadı, masa değişti.`, { warning: true });
                return;
              }
              removed.quantity = 0.5;
              state.draft.splice(Math.min(index, state.draft.length), 0, removed);
              afterDraftChange();
            }
          });
        }
        afterDraftChange();
        return;
      }

      const voidButton = event.target.closest('[data-void]');
      if (voidButton) { openVoidSheet(voidButton.dataset.void); return; }

      const voidSentButton = event.target.closest('[data-void-sent]');
      if (voidSentButton) openVoidSentSheet(voidSentButton.dataset.voidSent);
    });

    el.billBody.addEventListener('input', (event) => {
      const field = event.target.closest('[data-note]');
      if (!field) return;
      const line = state.draft.find((candidate) => candidate.id === field.dataset.note);
      if (line) line.note = field.value;
    });

    el.optionsBody.addEventListener('click', onOptionsBodyClick);
    el.optionsConfirm.addEventListener('click', onOptionsConfirm);

    el.pinKeys.addEventListener('click', (event) => {
      const key = event.target.closest('[data-pin]');
      if (!key) return;
      const value = key.dataset.pin;
      if (value === 'del') state.pinBuffer = state.pinBuffer.slice(0, -1);
      else if (value === 'ok') { void submitPin(); return; }
      else if (state.pinBuffer.length < 12) state.pinBuffer += value;
      renderPinDots();
    });
  }

  function onOptionsBodyClick(event) {
    const quantity = event.target.closest('[data-qty]');
    if (quantity && state.optionsMode === 'product') {
      state.optionsContext.quantity = Number(quantity.dataset.qty);
      el.optionsBody.querySelectorAll('[data-qty]').forEach((button) => {
        button.setAttribute('aria-pressed', String(Number(button.dataset.qty) === state.optionsContext.quantity));
      });
      updateProductSheetTotal();
      return;
    }

    const modifier = event.target.closest('[data-modifier]');
    if (modifier && state.optionsMode === 'product') {
      toggleModifier(modifier);
      return;
    }

    const reason = event.target.closest('[data-reason]');
    if (reason && (state.optionsMode === 'void' || state.optionsMode === 'void-sent')) {
      state.optionsContext.reason = reason.dataset.reason;
      el.optionsBody.querySelectorAll('[data-reason]').forEach((button) => {
        button.setAttribute('aria-pressed', String(button === reason));
      });
      el.optionsConfirm.disabled = false;
      return;
    }

    const dismissFailed = event.target.closest('[data-dismiss-failed]');
    if (dismissFailed && state.optionsMode === 'failed-orders') {
      dismissFailedOrder(dismissFailed.dataset.dismissFailed);
      return;
    }

    const target = event.target.closest('[data-target]');
    if (target && state.optionsMode === 'transfer') {
      state.optionsContext.targetId = target.dataset.target;
      el.optionsBody.querySelectorAll('[data-target]').forEach((button) => {
        button.setAttribute('aria-pressed', String(button === target));
      });
      el.optionsConfirm.disabled = false;
      return;
    }

    const accept = event.target.closest('[data-accept]');
    if (accept) { void resolvePending(accept.dataset.accept, true); return; }
    const reject = event.target.closest('[data-reject]');
    if (reject) { void resolvePending(reject.dataset.reject, false); return; }

    const profile = event.target.closest('[data-profile]');
    if (profile) {
      const action = profile.dataset.profile;
      if (action === 'signout') { closeOptions(); void signOut(); }
      else if (action === 'fullscreen') { closeOptions(); void toggleFullscreen(); }
      else if (action === 'push') { closeOptions(); void (state.pushEnabled ? disablePush() : enablePush()); }
      else if (action === 'pin-off') openPinSheet(true);
      else if (state.pinArmed) { closeOptions(); lockScreen(); }
      else openPinSheet(false);
    }
  }

  function toggleModifier(button) {
    const context = state.optionsContext;
    const modifierId = button.dataset.modifier;
    const groupId = button.dataset.group;
    const group = context.product.modifierGroups.find((candidate) => candidate.modifierGroupId === groupId);
    const modifier = group.modifiers.find((candidate) => candidate.modifierId === modifierId);

    if (context.chosen.has(modifierId)) {
      context.chosen.delete(modifierId);
    } else {
      if (button.dataset.single === 'true') {
        for (const other of group.modifiers) context.chosen.delete(other.modifierId);
      } else if (group.maxSelections > 0) {
        const chosenInGroup = group.modifiers.filter((candidate) => context.chosen.has(candidate.modifierId)).length;
        if (chosenInGroup >= group.maxSelections) {
          toast(`${group.name} için en fazla ${group.maxSelections} seçim yapılabilir.`, { warning: true });
          return;
        }
      }
      // No quantity is stored or sent. V1-RMD-150 makes it optional exactly so
      // the server can apply its own rule - the ceiling of the line quantity -
      // and this screen has no reason to override it: the waiter never asked
      // for a specific count. The sent line then renders the count the server
      // actually recorded.
      context.chosen.set(modifierId, {
        modifierId,
        name: modifier.name,
        priceDelta: modifier.priceDelta
      });
    }

    el.optionsBody.querySelectorAll('[data-modifier]').forEach((candidate) => {
      candidate.setAttribute('aria-pressed', String(context.chosen.has(candidate.dataset.modifier)));
    });
    updateProductSheetTotal();
  }

  function onOptionsConfirm() {
    if (state.optionsMode === 'product') {
      const context = state.optionsContext;
      // A mandatory group with nothing chosen is a UX check only - the server
      // stays the authority (foundations §0.4).
      for (const group of context.product.modifierGroups) {
        if (group.minSelections > 0) {
          const chosen = group.modifiers.filter((modifier) => context.chosen.has(modifier.modifierId)).length;
          if (chosen < group.minSelections) {
            toast(`${group.name} seçimi zorunlu.`, { warning: true });
            return;
          }
        }
      }
      const noteField = el.optionsBody.querySelector('[data-product-note]');
      addToDraft(context.product, context.quantity, chosenModifiers(), noteField ? noteField.value.trim() : '');
      closeOptions();
      return;
    }
    if (state.optionsMode === 'void') { void confirmVoid(); return; }
    if (state.optionsMode === 'void-sent') { void confirmVoidSent(); return; }
    if (state.optionsMode === 'cashier') { void confirmSendToCashier(); return; }
    if (state.optionsMode === 'transfer') { void confirmTransfer(); return; }
    if (state.optionsMode === 'pin') { void confirmPin(); return; }
    if (state.optionsMode === 'failed-orders') { clearAllFailedOrders(); }
  }

  function repeatLastRound() {
    const round = lastRound();
    if (!round) return;
    for (const item of round) {
      const product = state.products.find((candidate) => candidate.id === item.productId);
      if (!product) {
        toast(`${item.productName} artık menüde yok, atlandı.`, { warning: true });
        continue;
      }
      const modifiers = (item.modifiers || []).map((modifier) => ({
        modifierId: modifier.modifierId,
        name: modifier.name,
        priceDelta: modifier.priceDelta
      }));
      addToDraft(product, item.quantity, modifiers, item.specialInstructions || '');
    }
    openBill();
  }

  // ══ Live updates ═══════════════════════════════════════════════════

  let hub = null;

  function connectHub() {
    if (hub || typeof signalR === 'undefined') return;
    hub = new signalR.HubConnectionBuilder()
      .withUrl(`/hubs/waiter-order-status?terminalId=${state.terminalId}`)
      .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    // V1-WTR-009. Nothing in this system records which waiter serves which
    // table, so the hub broadcasts to every device; the payload carries the
    // table and each device decides what to show.
    hub.on('OrderItemReady', (payload) => {
      const name = (payload && payload.productName) || 'Bir ürün';
      toast(`${name} hazır.`);
      // Same tag the service worker's push handler uses, so an online device
      // that receives both channels shows one notification, not two.
      notify('Sipariş hazır', `${name} hazır`, 'alkaros-order-ready');
    });

    // V1-RMD-149: before this existed a guest could order from the QR menu and
    // nobody found out.
    hub.on('OrderPendingConfirmation', (payload) => {
      if (!payload) return;
      if (!state.pending.some((order) => order.orderId === payload.orderId)) {
        state.pending.push({
          orderId: payload.orderId,
          tableId: payload.tableId,
          tableNumber: payload.tableNumber,
          itemCount: payload.itemCount,
          total: payload.total,
          createdAt: payload.submittedAt
        });
      }
      renderPendingBanner();
      notify('Misafir siparişi', `${payload.tableNumber} masası sipariş verdi`, 'alkaros-pending-order');
    });

    // Best effort: a deployment without kitchen live-sync simply never sends
    // anything, and automatic reconnect covers a transient failure.
    hub.start().catch(() => {});
  }

  function notify(title, body, tag) {
    if (!window.Notification || Notification.permission !== 'granted') return;
    try { new Notification(title, { body, tag: tag || 'alkaros-waiter', lang: 'tr' }); }
    catch { /* some mobile browsers throw here; the in-page toast covers it */ }
  }

  // ══ Start ══════════════════════════════════════════════════════════

  async function start() {
    renderRibbon();
    await loadZones();
    await loadCatalog();
    await loadTables();
    await loadPending();
    renderZones();
    renderCategories();
    renderProducts();
    renderBill();
    afterDraftChange();
    registerOfflineWorker();
    connectHub();
    resetIdleTimer();
    void refreshPushState();
    void flushQueue();
  }

  async function init() {
    bindEvents();
    renderPinPad();
    renderRibbon();

    // Tables, catalog and orders are all session-scoped: without one there is
    // nothing to load, only a wall of 401s. A genuinely offline reload is
    // not the same as "not logged in" - see hasValidSession's own comment.
    const session = await hasValidSession();
    if (session === 'no') {
      showLogin();
      return;
    }
    await start();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
