// ALKAROS Cashier Quick Order Controller (V1-CUI-005 / V1-RMD-051)
(function () {
  'use strict';

  // V1-RMD-238: this used to serialize a text node (`div.textContent = …;
  // return div.innerHTML`), which escapes only & < > — a quote passed
  // through untouched. Several call sites below put user-entered text (an
  // item note, a waiter's display name) inside double-quoted attributes
  // (`value="${escapeHtml(item.note)}"`), so a note like `" onmouseover="…`
  // closed the attribute and injected an event handler running in the
  // cashier's own session — the exact same defect WaiterPwa's `js/util.js`
  // already found and fixed for the same reason; that fix never propagated
  // here. Mirrors WaiterPwa's HTML_ESCAPES map.
  const HTML_ESCAPES = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };

  function escapeHtml(str) {
    if (str === null || str === undefined) return '';
    return String(str).replace(/[&<>"']/g, (character) => HTML_ESCAPES[character]);
  }

  function formatMoney(amount) {
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(amount || 0);
  }

  // V1-CUI-010: the same "Kalan N" badge Garson's util.js renders
  // (renderStockBadge, V1-WTR-056) for the shared /catalog endpoint's
  // remainingCount — reimplemented here because Cashier and WaiterPwa are
  // separate apps with no shared JS module, but the class names
  // (.product-stock/.is-low) and wording match on purpose. null means the
  // product is not stock-tracked at all - not the same as none left - so
  // nothing renders. A zero-or-less product never reaches this catalog at
  // all (V1-WTR-054 drops it), so there is no "is-out" case here either.
  function renderStockBadge(remainingCount) {
    if (remainingCount === null || remainingCount === undefined) return '';
    const cssClass = remainingCount < 5 ? ' is-low' : '';
    return `<div class="product-stock${cssClass}">Kalan ${escapeHtml(Number(remainingCount).toLocaleString('tr-TR'))}</div>`;
  }

  // UI_STYLE_GUIDE §3: raw HTTP status codes are never shown to the user.
  function describeHttpFailure(status) {
    if (status === 400) return 'İstek doğrulanamadı. Lütfen ürün ve masa bilgilerini kontrol edin.';
    if (status === 401) return 'Oturum geçersiz veya süresi doldu. Lütfen yeniden giriş yapın.';
    if (status === 403) return 'Bu işlem için yetkiniz yok.';
    if (status === 404) return 'İlgili kayıt bulunamadı.';
    if (status === 409) return 'Sipariş başka bir işlem tarafından değiştirildi. Lütfen tekrar deneyin.';
    if (status >= 500) return 'Sunucu hatası oluştu. Lütfen tekrar deneyin.';
    return 'İstek sunucu tarafından reddedildi. Lütfen tekrar deneyin.';
  }

  // V1-RMD-232: found by an independent audit (2026-09-17) — this ran
  // unguarded at module load, before DOMContentLoaded; a corrupted
  // localStorage value for this key would throw synchronously and crash
  // the whole module, leaving the cashier screen entirely nonfunctional
  // with no Turkish error shown at all. Falls back to an empty list and
  // logs the bad value instead.
  function loadParkedTickets() {
    try {
      return JSON.parse(localStorage.getItem('alkaros_cashier_parked') || '[]');
    } catch (parseError) {
      console.error('Bekletilen fiş verisi bozuk, boş liste ile devam ediliyor:', parseError);
      return [];
    }
  }

  // V1-RMD-350 (independent 2026-09-26 audit, düşük seviye bulgu): the write
  // side of the same key had no such guard - a private tab or a full quota
  // would throw synchronously and abort parkCurrentTicket()/
  // recallParkedTicket() mid-way, same crash risk V1-RMD-232 already fixed
  // for the read side above.
  function saveParkedTickets() {
    try {
      localStorage.setItem('alkaros_cashier_parked', JSON.stringify(state.parkedTickets));
    } catch (writeError) {
      console.error('Bekletilen fiş listesi kaydedilemedi:', writeError);
    }
  }

  // V1-RMD-360 (module-by-module UI audit, 2026-09-27): the ACTIVE ticket (unlike a parked
  // one) had no persistence at all - a tab crash, an accidental refresh, or the kiosk browser
  // restarting mid-order (a real operational event: a Windows update reboot, a browser crash)
  // silently discarded every item the cashier had already entered, with no recovery, while an
  // explicitly parked ticket already survived exactly that. Mirrors loadParkedTickets/
  // saveParkedTickets' own try/catch shape. Cleared on shift close alongside the parked-ticket
  // key (cash-session.js) for the same vardiya-boundary reasoning as V1-RMD-343.
  function loadActiveTicket() {
    try {
      const saved = JSON.parse(localStorage.getItem('alkaros_cashier_active_ticket') || '[]');
      return Array.isArray(saved) ? saved : [];
    } catch (parseError) {
      console.error('Aktif fiş verisi bozuk, boş sepetle devam ediliyor:', parseError);
      return [];
    }
  }

  function saveActiveTicket() {
    try {
      localStorage.setItem('alkaros_cashier_active_ticket', JSON.stringify(state.ticketItems));
    } catch (writeError) {
      console.error('Aktif fiş kaydedilemedi:', writeError);
    }
  }

  // State
  const state = {
    // Terminal identity is resolved from the authenticated cashier session
    // cookie at startup; there is no hardcoded terminal id.
    terminalId: null,
    sessionStatus: 'loading', // 'loading' | 'ready' | 'unauthorized'
    cashierName: 'Kasiyer',
    registerNumber: 'KASA-01',
    shiftTotalOrders: 0,
    activeCategory: 'all',

    // Active Ticket / Basket
    ticketItems: loadActiveTicket(),
    parkedTickets: loadParkedTickets(),

    // Catalog is loaded from the authoritative endpoint only. There is no
    // hardcoded fallback: if the catalog cannot be loaded the terminal fails
    // closed and order dispatch is disabled.
    catalogStatus: 'loading', // 'loading' | 'ready' | 'error'
    categories: [{ id: 'all', name: 'Tüm Ürünler' }],
    products: [],
    dispatchInFlight: false,
    isOnline: navigator.onLine,

    // V1-RMD-205: who runs this order to the customer. null (the default,
    // "Ben") means the cashier themselves — nothing extra sent, the server
    // already attributes it to them. Populated from /orders/staff, with
    // /orders/suggested-waiter's pick pre-selected when available.
    waiterOptions: [],
    selectedWaiterUserId: null,

    // V1-RMD-212: userId -> current open-table count, from
    // /orders/waiter-load. Missing entries render with no count (never "0
    // masa" - a waiter this map has nothing for is just unmeasured, not
    // confirmed idle).
    waiterLoads: {},

    // V1-RMD-376: { product, chosen: { [groupId]: Set<modifierId> } } while
    // the modifier modal is open, null otherwise.
    modifierContext: null
  };

  // DOM Elements
  const el = {
    categoryTabs: document.getElementById('categoryTabs'),
    productMatrix: document.getElementById('productMatrix'),
    ticketItemsStream: document.getElementById('ticketItemsStream'),
    grandTotalAmount: document.getElementById('grandTotalAmount'),
    searchInput: document.getElementById('searchInput'),
    btnDispatchOrder: document.getElementById('btnDispatchOrder'),
    btnClearTicket: document.getElementById('btnClearTicket'),
    btnParkTicket: document.getElementById('btnParkTicket'),
    btnRecallTicket: document.getElementById('btnRecallTicket'),
    parkCountBadge: document.getElementById('parkCountBadge'),
    parkedModal: document.getElementById('parkedModal'),
    parkedList: document.getElementById('parkedList'),
    btnCloseParkedModal: document.getElementById('btnCloseParkedModal'),
    connectivityPill: document.getElementById('connectivityPill'),
    connectivityLabel: document.getElementById('connectivityLabel'),
    dispatchHint: document.getElementById('dispatchHint'),
    dispatchWaiterSelect: document.getElementById('dispatchWaiterSelect'),
    toastRegion: document.getElementById('toastRegion'),
    confirmModal: document.getElementById('confirmModal'),
    confirmModalMessage: document.getElementById('confirmModalMessage'),
    btnConfirmModalAccept: document.getElementById('btnConfirmModalAccept'),
    btnConfirmModalCancel: document.getElementById('btnConfirmModalCancel'),
    modifierModal: document.getElementById('modifierModal'),
    modifierGroupsList: document.getElementById('modifierGroupsList'),
    btnCloseModifierModal: document.getElementById('btnCloseModifierModal'),
    btnModifierCancel: document.getElementById('btnModifierCancel'),
    btnModifierConfirm: document.getElementById('btnModifierConfirm')
  };

  // V1-RMD-360 (module-by-module UI audit, 2026-09-27): #parkedModal/#confirmModal already had
  // role="dialog"/"alertdialog" aria-modal="true" markup, but nothing actually moved keyboard
  // focus INTO either on open (a screen reader user, and a sighted keyboard user, stayed on
  // whatever was focused behind the overlay), nothing closed them on Escape (the one key every
  // OS/browser dialog convention trains people to reach for), and nothing restored focus to
  // whatever opened them on close (keyboard navigation resumed at the top of the page instead of
  // where the user left off). Shared helper: focuses the first real control inside the dialog,
  // traps Tab/Shift+Tab within it (the WAI-ARIA dialog pattern - a Tab that would leave the
  // dialog wraps to its other end instead of reaching page content behind the overlay), closes
  // on Escape, and restores focus on any close path (Escape, Cancel, backdrop-adjacent buttons).
  function trapModalFocus(modalEl, onClose) {
    const focusable = Array.from(
      modalEl.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])')
    ).filter((node) => !node.disabled && node.offsetParent !== null);
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const previouslyFocused = document.activeElement;
    if (first) first.focus({ preventScroll: true });

    const onKeydown = (e) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        cleanup();
        onClose();
        return;
      }
      if (e.key !== 'Tab' || focusable.length === 0) return;
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    };
    const cleanup = () => {
      modalEl.removeEventListener('keydown', onKeydown);
      if (previouslyFocused && typeof previouslyFocused.focus === 'function') {
        previouslyFocused.focus({ preventScroll: true });
      }
    };
    modalEl.addEventListener('keydown', onKeydown);
    return cleanup;
  }

  // V1-RMD-355 (independent 2026-09-26 audit, a low-severity finding): replaces the native confirm() dialog
  // used by recallParkedTicket - an unstylable, un-brandable browser box that also can't be driven from an
  // automated E2E test the way an in-app modal can. Same self-contained resolve-on-click shape as the existing
  // V1-RMD-253 alert()->toast replacement above. Only one confirmation can be pending at a time (matches this
  // screen's own single-cashier, single-flow-at-a-time usage - the modal already blocks the whole screen).
  function showConfirmModal(message) {
    return new Promise((resolve) => {
      if (!el.confirmModal || !el.confirmModalMessage || !el.btnConfirmModalAccept || !el.btnConfirmModalCancel) {
        // Markup missing (should not happen in production) - never leave the caller hanging.
        resolve(false);
        return;
      }

      el.confirmModalMessage.textContent = message;
      el.confirmModal.hidden = false;

      const settle = (result) => {
        el.confirmModal.hidden = true;
        releaseFocusTrap();
        el.btnConfirmModalAccept.removeEventListener('click', onAccept);
        el.btnConfirmModalCancel.removeEventListener('click', onCancel);
        resolve(result);
      };
      const onAccept = () => settle(true);
      const onCancel = () => settle(false);
      // V1-RMD-360: Escape is treated the same as clicking "Vazgeç" - a confirm dialog's
      // safe/no-op answer.
      const releaseFocusTrap = trapModalFocus(el.confirmModal, () => settle(false));

      el.btnConfirmModalAccept.addEventListener('click', onAccept);
      el.btnConfirmModalCancel.addEventListener('click', onCancel);
    });
  }

  // V1-RMD-253: replaces native alert() in the dispatch flow — a blocking
  // dialog the cashier had to manually dismiss on every single order,
  // including a successful one. Mirrors foundations.md §5.3's toast
  // pattern (self-dismissing, 3-5s) already used by WaiterPwa/PosTerminal.
  // textContent (not innerHTML) needs no HTML escaping — every message
  // passed here is a fixed Turkish string, never user-entered text.
  function showToast(message, tone) {
    if (!el.toastRegion) return;
    const node = document.createElement('div');
    node.className = `toast toast--${tone}`;
    node.setAttribute('role', tone === 'error' ? 'alert' : 'status');
    node.textContent = message;
    el.toastRegion.appendChild(node);
    window.setTimeout(() => node.remove(), tone === 'error' ? 6000 : 5000);
  }

  // Found by an independent audit (2026-09-07): this badge was static
  // markup — always "Çevrimiçi" regardless of the terminal's actual
  // connection, with no navigator.onLine check or online/offline listener
  // anywhere in this file (unlike WaiterPwa's status ribbon). A LAN outage
  // left the kiosk falsely reporting itself online. This only makes the
  // badge truthful; DESIGN.md's "kiosk goes read-only on LAN outage"
  // protocol is a separate, larger product decision, not addressed here.
  function updateConnectivityBadge() {
    const online = navigator.onLine;
    if (el.connectivityPill) {
      el.connectivityPill.classList.toggle('session-pill--online', online);
      el.connectivityPill.classList.toggle('session-pill--offline', !online);
    }
    if (el.connectivityLabel) el.connectivityLabel.textContent = online ? 'Çevrimiçi' : 'Çevrimdışı';
  }

  // DESIGN.md's "kiosk goes read-only on LAN outage" protocol, decided
  // narrowly: the kiosk's only real network write is dispatch (table-draft
  // -> submit-draft -> send-to-cashier, three sequential requests with no
  // retry/idempotency queue behind them, unlike WaiterPwa's offline-queue.js
  // — a drop mid-sequence would leave an inconsistent half-sent order). So
  // only dispatch locks offline; building/editing the ticket, park/recall
  // and catalog browsing stay local-only and keep working (they never touch
  // the network). A cashier session's own expiry is intentionally NOT
  // re-checked here: it is only ever known by asking the server, which is
  // exactly what offline means we cannot do — forcing a lockout offline
  // would freeze every local-only action above for no security benefit
  // (the one real risk, dispatch, is already blocked), and the next real
  // request after reconnect still gets a normal 401 if the session did
  // expire.
  function onConnectivityChange() {
    state.isOnline = navigator.onLine;
    updateConnectivityBadge();
    updateDispatchAvailability();
  }

  async function init() {
    renderCategoryTabs();
    renderProducts();
    renderTicket();
    updateParkBadge();
    bindEvents();
    updateDispatchAvailability();
    updateConnectivityBadge();
    window.addEventListener('online', onConnectivityChange);
    window.addEventListener('offline', onConnectivityChange);

    // V1-RMD-360 (module-by-module UI audit, 2026-09-27, saha gerçekliği): a real terminal's
    // barcode scanner types into whatever field has focus - it is not a separate device with its
    // own target. Nothing here ever focused the search box, so the FIRST scan of a shift (or of
    // any moment the cashier had clicked elsewhere) landed nowhere and was silently lost. Only
    // done once at boot, not stolen back on every render - a cashier who has since clicked into
    // the ticket panel to edit a note, say, must keep that focus.
    if (el.searchInput) el.searchInput.focus({ preventScroll: true });

    const sessionOk = await bootstrapSession();
    if (sessionOk) {
      await loadCatalog();
      await loadWaiterOptions();
    } else {
      state.catalogStatus = 'error';
      renderProducts();
      updateDispatchAvailability();
    }
  }

  async function bootstrapSession() {
    try {
      const response = await fetch('/api/v1/auth/session/current', { credentials: 'include' });
      if (!response.ok) throw new Error(`session ${response.status}`);
      const session = await response.json();
      if (!session || typeof session.terminalId !== 'string' || !session.terminalId) {
        throw new Error('session missing terminal');
      }
      state.terminalId = session.terminalId;
      state.sessionStatus = 'ready';
      if (typeof session.displayName === 'string' && session.displayName.trim()) {
        state.cashierName = session.displayName.trim();
      }
      const label = document.getElementById('cashierNameLabel');
      if (label) label.textContent = state.cashierName;
      return true;
    } catch {
      state.sessionStatus = 'unauthorized';
      const label = document.getElementById('cashierNameLabel');
      if (label) label.textContent = 'Oturum gerekli';
      return false;
    }
  }

  // V1-RMD-163: found by the 2026-09-10 Garson audit — the catalog
  // endpoint paginates at 1000 rows and answers an X-Next-Cursor header
  // whenever more remain, but this used to fetch one page and stop. A
  // restaurant with more than 1000 rows (products, once every
  // size/variant is its own row) would silently lose everything past the
  // first page — no error, nothing telling the cashier a product is
  // missing. Follows the cursor until the server stops sending one.
  async function fetchWholeCatalogAsync() {
    const items = [];
    let cursor = null;
    for (;;) {
      const url = new URL(`/api/v1/terminals/${state.terminalId}/catalog`, window.location.origin);
      if (cursor) url.searchParams.set('cursor', cursor);
      const response = await fetch(url, { credentials: 'include' });
      if (!response.ok) throw new Error(`catalog ${response.status}`);
      const payload = await response.json();
      const pageItems = Array.isArray(payload) ? payload : payload.products || [];
      items.push(...pageItems);
      cursor = response.headers.get('X-Next-Cursor');
      if (!cursor) break;
    }
    return items;
  }

  async function loadCatalog() {
    try {
      const items = await fetchWholeCatalogAsync();
      const products = items
        .map(p => ({
          id: p.productId || p.id,
          // V1-RMD-227: the real CatalogProductDto field is categoryCode,
          // not categoryId - every product silently fell into
          // 'uncategorized' and never matched a real category tab.
          categoryId: p.categoryCode || 'uncategorized',
          categoryName: p.categoryName || 'Diğer',
          code: p.sku || p.code || '',
          name: p.productName || p.name || '',
          // V1-RMD-227: the real field is unitPrice - the two field names
          // this used to read never existed on the response, so every
          // product showed ₺0,00.
          price: Number(p.unitPrice ?? 0),
          // V1-CUI-010: how many more units the mapped stock can still
          // cover, null when the product isn't stock-tracked (unlimited).
          remainingCount: p.remainingCount != null ? p.remainingCount : null,
          // V1-RMD-376 (module-by-module UI audit round 2, P1 - rakip
          // karşılaştırması): the server's own CatalogModifierGroupDto doc
          // comment says mandatory-group enforcement is deliberately left to
          // the client - this field existed on the response the whole time,
          // WaiterPwa's own catalog mapping already reads it, but this
          // screen dropped it entirely, so a product with a required option
          // group (e.g. "Boy: Küçük/Orta/Büyük") could be added with no way
          // to ever record which one, silently, with no error from either
          // side.
          modifierGroups: Array.isArray(p.modifierGroups) ? p.modifierGroups : []
        }))
        .filter(p => p.id && p.name);
      if (products.length === 0) throw new Error('catalog empty');

      const categoryMap = new Map();
      for (const product of products) {
        if (!categoryMap.has(product.categoryId)) categoryMap.set(product.categoryId, product.categoryName);
      }
      state.products = products;
      state.categories = [{ id: 'all', name: 'Tüm Ürünler' }, ...[...categoryMap].map(([id, name]) => ({ id, name }))];
      state.catalogStatus = 'ready';
    } catch {
      state.catalogStatus = 'error';
      state.products = [];
    }
    renderCategoryTabs();
    renderProducts(el.searchInput ? el.searchInput.value : '');
    updateDispatchAvailability();
  }

  // V1-RMD-205: best-effort. Neither call ever blocks or fails dispatch —
  // an empty list just means the picker only ever offers "Ben", the same
  // as before this existed. V1-RMD-211: the two GET requests are
  // independent (neither's outcome affects how the other is made or
  // read), so they run in parallel rather than one after the other.
  async function loadWaiterOptions() {
    const loadStaff = (async () => {
      try {
        const response = await fetch(`/api/v1/terminals/${state.terminalId}/orders/staff`, { credentials: 'include' });
        if (response.ok) {
          const staff = await response.json();
          state.waiterOptions = Array.isArray(staff)
            ? staff.map(s => ({ userId: s.userId, displayName: s.displayName }))
            : [];
        }
      } catch {
        state.waiterOptions = [];
      }
    })();

    const loadSuggestion = (async () => {
      try {
        const response = await fetch(`/api/v1/terminals/${state.terminalId}/orders/suggested-waiter`, { credentials: 'include' });
        if (response.status === 200) {
          const suggestion = await response.json();
          if (suggestion && suggestion.userId) state.selectedWaiterUserId = suggestion.userId;
        }
      } catch {
        // No suggestion available - the picker stays on "Ben".
      }
    })();

    // V1-RMD-212: a third independent, self-error-swallowing GET - missing
    // it just means the picker shows names with no load count, same as
    // before this existed.
    const loadWaiterLoads = (async () => {
      try {
        const response = await fetch(`/api/v1/terminals/${state.terminalId}/orders/waiter-load`, { credentials: 'include' });
        if (response.ok) {
          const loads = await response.json();
          state.waiterLoads = Array.isArray(loads)
            ? Object.fromEntries(loads.map(w => [w.userId, w.activeLoad]))
            : {};
        }
      } catch {
        state.waiterLoads = {};
      }
    })();

    await Promise.all([loadStaff, loadSuggestion, loadWaiterLoads]);

    renderWaiterOptions();
  }

  function renderWaiterOptions() {
    if (!el.dispatchWaiterSelect) return;
    const options = ['<option value="">Ben (' + escapeHtml(state.cashierName) + ')</option>'];
    for (const waiter of state.waiterOptions) {
      const selected = waiter.userId === state.selectedWaiterUserId ? ' selected' : '';
      const load = state.waiterLoads[waiter.userId];
      const loadSuffix = typeof load === 'number' ? ` (${load} masa)` : '';
      options.push(`<option value="${escapeHtml(waiter.userId)}"${selected}>${escapeHtml(waiter.displayName)}${loadSuffix}</option>`);
    }
    el.dispatchWaiterSelect.innerHTML = options.join('');
    // The suggestion may name a waiter fetched after this render call in a
    // slow-network interleaving; re-apply the selection explicitly rather
    // than relying on the `selected` attribute already being in the markup
    // the browser parsed.
    el.dispatchWaiterSelect.value = state.selectedWaiterUserId || '';
  }

  function updateDispatchAvailability() {
    if (!el.btnDispatchOrder) return;
    const offline = !state.isOnline;
    el.btnDispatchOrder.disabled = state.catalogStatus !== 'ready' || offline;
    if (el.dispatchHint) {
      el.dispatchHint.hidden = !offline;
      if (offline) el.dispatchHint.textContent = 'Bağlantı yok — sipariş mutfağa gönderilemiyor. Sepeti düzenlemeye devam edebilirsiniz.';
    }
  }

  function renderCategoryTabs() {
    // V1-RMD-360 (module-by-module UI audit, 2026-09-27): the nav wrapper already carried
    // aria-label="Ürün Kategorileri", but the buttons inside had no role/state at all - a screen
    // reader announced each as a plain, unrelated button, with no way to tell which category (if
    // any) is currently selected. role="tablist"/"tab" + aria-selected is the standard WAI-ARIA
    // tabs pattern for exactly this "one of several mutually exclusive views" shape.
    if (el.categoryTabs) el.categoryTabs.setAttribute('role', 'tablist');
    if (!el.categoryTabs) return;
    el.categoryTabs.innerHTML = state.categories.map(c => {
      const selected = state.activeCategory === c.id;
      return `
      <button type="button" class="tab-chip ${selected ? 'active' : ''}" role="tab" aria-selected="${selected}" data-cat-id="${escapeHtml(c.id)}">
        ${escapeHtml(c.name)}
      </button>
    `;
    }).join('');
  }

  function renderProducts(searchQuery = '') {
    if (!el.productMatrix) return;
    if (state.sessionStatus === 'unauthorized') {
      el.productMatrix.innerHTML = '<div class="pos-catalog-state pos-catalog-state--error">Kasiyer oturumu doğrulanamadı. Sipariş girişi kapalı; yeniden giriş yapın.</div>';
      return;
    }
    if (state.catalogStatus === 'loading') {
      el.productMatrix.innerHTML = '<div class="pos-catalog-state">Katalog yükleniyor…</div>';
      return;
    }
    if (state.catalogStatus === 'error') {
      el.productMatrix.innerHTML = '<div class="pos-catalog-state pos-catalog-state--error">Katalog sunucudan alınamadı. Sipariş girişi kapalı; bağlantıyı kontrol edip sayfayı yenileyin.</div>';
      return;
    }
    const query = searchQuery.trim().toLowerCase();
    const filtered = state.products.filter(p => {
      const matchCat = state.activeCategory === 'all' || p.categoryId === state.activeCategory;
      // V1-RMD-376 (module-by-module UI audit round 2, P2 - saha gerçekliği): `query` is already
      // lowercased above, but `p.code` never was - SKUs in this catalog are conventionally
      // uppercase ("E2E-KASA-DUSUK"), so a case-sensitive .includes() here never matched a
      // lowercase search at all, silently, for any code-based search (a cashier typing a SKU by
      // hand, or a scanner sending it in a different case than the catalog record).
      const matchSearch = !query || p.name.toLowerCase().includes(query) || (p.code && p.code.toLowerCase().includes(query));
      return matchCat && matchSearch;
    });

    el.productMatrix.innerHTML = filtered.map(prod => `
      <div class="pos-product-card" data-product-id="${escapeHtml(prod.id)}" tabindex="0" role="button">
        <div class="product-badge">${escapeHtml(prod.code || '')}</div>
        <div class="pos-product-name">${escapeHtml(prod.name)}</div>
        <div class="pos-product-price">${formatMoney(prod.price)}</div>
        ${renderStockBadge(prod.remainingCount)}
      </div>
    `).join('');
  }

  function renderTicket() {
    // V1-RMD-360: every mutation to state.ticketItems (add/inc/dec/del/clear/dispatch/park/
    // recall) already calls renderTicket() right after, so hooking the save in here - rather
    // than at each call site separately - keeps the active ticket persisted without a
    // per-call-site save that could be missed on a future change.
    saveActiveTicket();
    if (!el.ticketItemsStream) return;
    if (state.ticketItems.length === 0) {
      el.ticketItemsStream.innerHTML = '<div class="ticket-empty">Sepet boş. Ürün seçin.</div>';
      updateTotal();
      return;
    }

    el.ticketItemsStream.innerHTML = state.ticketItems.map(item => `
      <div class="ticket-row">
        <div class="item-meta">
          <div class="item-title">${escapeHtml(item.name)}</div>
          ${item.modifiers && item.modifiers.length > 0
            ? `<div class="item-modifiers">${escapeHtml(item.modifiers.map(m => m.name).join(', '))}</div>`
            : ''}
          <div class="item-sub">${formatMoney(item.price)} × ${item.quantity} = ${formatMoney(item.price * item.quantity)}</div>
          <input class="item-note-input" type="text" maxlength="200" placeholder="Not (örn. az, acısız)" value="${escapeHtml(item.note || '')}" data-id="${escapeHtml(item.id)}" aria-label="${escapeHtml(item.name)} özel talimat" />
        </div>
        <div class="item-actions">
          <button type="button" class="btn-micro" data-action="dec" data-id="${escapeHtml(item.id)}" aria-label="${escapeHtml(item.name)} adedini azalt">−</button>
          <span class="ticket-row-qty">${item.quantity}</span>
          <button type="button" class="btn-micro" data-action="inc" data-id="${escapeHtml(item.id)}" aria-label="${escapeHtml(item.name)} adedini artır">+</button>
          <button type="button" class="btn-micro btn-del" data-action="del" data-id="${escapeHtml(item.id)}" title="Sil" aria-label="Sil"><svg class="icon" aria-hidden="true"><use href="#ico-close"/></svg></button>
        </div>
      </div>
    `).join('');

    updateTotal();
  }

  function updateTotal() {
    // İkram/comp düğmesi kaldırıldı (bağımsız denetimde bulundu, 2026-09-05):
    // burada işaretlenen bir kalem ekranda ₺0 gösteriliyordu ama sunucu
    // (OrderManagementStore.CreateOrUpdateTableDraftAsync) kalıcı fiyatı her
    // zaman katalogdan hesaplıyor — istemcinin gönderdiği unitPrice hiç
    // okunmuyor. Sonuç: müşteri her zaman tam fiyattan faturalanıyordu,
    // ekran "ücretsiz" derken. Gerçek, yetkilendirilmiş ikram akışı
    // (bills.comp grant'i, POST .../items/{itemId}/comp) yalnızca zaten
    // gönderilmiş bir sipariş üzerinde çalışır; bu ekran siparişi tek
    // seferde oluşturduğu için o akışa bağlanamaz. Toplam artık her zaman
    // gerçekte faturalanacak tutarı gösterir.
    const total = state.ticketItems.reduce((sum, item) => sum + (item.price * item.quantity), 0);
    if (el.grandTotalAmount) el.grandTotalAmount.textContent = formatMoney(total);
  }

  function updateParkBadge() {
    if (!el.parkCountBadge) return;
    el.parkCountBadge.textContent = state.parkedTickets.length;
    el.parkCountBadge.style.display = state.parkedTickets.length > 0 ? 'inline-block' : 'none';
  }

  function addProductToTicket(product, modifiers = []) {
    // V1-RMD-376: a line with modifiers is never merged into a plain one (or
    // a differently-modified one) of the same product - mirrors WaiterPwa's
    // own addToDraft, which keeps a modified line distinct for the same
    // reason: two "Kahve, sütlü" and one "Kahve, sade" are three different
    // things the kitchen needs to see separately, not one line of three.
    const existing = modifiers.length === 0
      ? state.ticketItems.find(i => i.productId === product.id && !i.note && (!i.modifiers || i.modifiers.length === 0))
      : null;
    if (existing) {
      existing.quantity += 1;
    } else {
      const extra = modifiers.reduce((sum, m) => sum + (m.priceDelta || 0), 0);
      state.ticketItems.push({
        id: crypto.randomUUID(),
        productId: product.id,
        name: product.name,
        price: product.price + extra,
        quantity: 1,
        note: '',
        modifiers
      });
    }
    renderTicket();
  }

  async function dispatchOrderToKitchen() {
    if (state.ticketItems.length === 0) return;
    if (state.catalogStatus !== 'ready') {
      showToast('Katalog sunucudan alınamadığı için sipariş gönderilemiyor.', 'error');
      return;
    }
    // Defense in depth: the button is already disabled offline
    // (updateDispatchAvailability), but a click that lands in the gap
    // between an 'offline' event and its disabled re-render must not start
    // the three-request dispatch sequence with no retry/idempotency queue
    // behind it.
    if (!state.isOnline) {
      showToast('Bağlantı yok. Sipariş gönderilemiyor.', 'error');
      return;
    }
    // Found by an independent audit (2026-09-05): nothing stopped a second
    // click (double-tap, or a click while a slow request is still in
    // flight) from sending the same ticket twice. table-draft now checks
    // the body's own `id` for a resend of the SAME payload (V1-RMD-123),
    // but this click builds a fresh orderPayload — and a fresh id — every
    // time, so that check cannot help a second click; this guard remains
    // the only real protection against one.
    if (state.dispatchInFlight) return;
    state.dispatchInFlight = true;
    if (el.btnDispatchOrder) el.btnDispatchOrder.disabled = true;

    // V1-RMD-160: waiterName used to be sent here but nothing on the
    // server ever read it (found by the 2026-09-10 Garson audit) — the
    // real, authenticated actor is already attributed server-side via
    // Order.ServingUserId, from the session this request already carries.
    // Removed rather than wired in; a free-text field the client could set
    // to anything would only be a weaker, spoofable duplicate of that.
    const orderPayload = {
      id: crypto.randomUUID(),
      tableId: '00000000-0000-0000-0000-000000000001',
      tableNumber: 'KASA-1',
      // V1-RMD-205: unset (the "Ben" default) sends nothing, which is
      // exactly what the server already does without this field - the
      // cashier themselves becomes ServingUserId.
      assignedWaiterUserId: state.selectedWaiterUserId || undefined,
      items: state.ticketItems.map(item => ({
        // Stable per-line id (already used for local cart tracking) makes a
        // retried draft submission idempotent server-side instead of
        // appending a duplicate line on every retry.
        id: item.id,
        productId: item.productId,
        name: item.name,
        productName: item.name,
        quantity: item.quantity,
        unitPrice: item.price,
        specialInstructions: item.note && item.note.trim() ? item.note.trim() : null,
        // V1-RMD-376: the server recomputes the real price/name from the
        // catalog for each modifierId regardless (OrderItemModifierDto's own
        // doc comment) - only the id is a real input.
        modifiers: (item.modifiers || []).map(m => ({ modifierId: m.modifierId }))
      }))
    };

    // V1-RMD-167: found in independent review of V1-RMD-157 — the release
    // call used to live inline between the submit fetch and its ok check,
    // so it only ran when the submit CALL ITSELF completed (whether it
    // then succeeded or failed). If table-draft attached the order but
    // then draftResponse.json() failed to parse, or the submit fetch
    // itself threw (a dropped connection), execution jumped straight to
    // the outer catch and skipped release entirely — leaving KASA-1
    // attached to a half-finished order for the next customer to merge
    // into, exactly the bug V1-RMD-157 exists to close. `draft` is now
    // captured outside the try/catch and the release is attempted in
    // `finally` whenever an orderId was ever obtained, regardless of what
    // happened afterward.
    let draft = null;
    try {
      // V1-RMD-163: found by the 2026-09-10 Garson audit — an
      // X-Idempotency-Key header used to be sent here too, carrying the
      // exact same value as orderPayload.id in the body below. No endpoint
      // anywhere ever reads that header (grep confirmed); the real,
      // working idempotency protection is orderPayload.id itself, which
      // the server persists as Order.SourceReferenceId behind a partial
      // unique index (V1-RMD-123). Removed the header as a pointless
      // duplicate rather than wiring up a second mechanism for the same
      // value.
      const draftResponse = await fetch(`/api/v1/terminals/${state.terminalId}/orders/table-draft`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        credentials: 'include',
        body: JSON.stringify(orderPayload)
      });

      if (!draftResponse.ok) {
        showToast(describeHttpFailure(draftResponse.status), 'error');
        return;
      }

      try {
        draft = await draftResponse.json();
      } catch (parseError) {
        // V1-RMD-232: found by an independent audit (2026-09-17) — the
        // fetch above already returned 2xx, meaning table-draft attached
        // this order to KASA-1 server-side, even though reading its
        // response body then failed (a dropped connection mid-body, a
        // malformed response). Before this, `draft` stayed null, so the
        // `finally` below's `if (draft && draft.orderId)` never fired and
        // KASA-1 stayed attached for the next customer to merge into —
        // exactly the bug V1-RMD-167 exists to close, just from a
        // different failure point. Query KASA-1's own active order back so
        // `finally` can still release it.
        console.error('table-draft yanıtı çözülemedi:', parseError);
        try {
          const recoveryResponse = await fetch(
            `/api/v1/terminals/${state.terminalId}/orders/table/${orderPayload.tableId}`,
            { credentials: 'include' });
          if (recoveryResponse.ok) {
            draft = await recoveryResponse.json();
          }
        } catch (recoveryError) {
          console.error('KASA-1 kurtarma sorgusu başarısız:', recoveryError);
        }
        showToast('Sunucuya ulaşılamadı. Sipariş iletilemedi.', 'error');
        return;
      }
      // Found by an independent audit (2026-09-06): the draft above was
      // never followed by a submit call, so the order stayed in Draft
      // forever and was never dispatched to the kitchen even though the
      // user was told it had been sent.
      //
      // operationId is deterministic (not a fresh UUID per attempt): a
      // retry of submitting this exact order (e.g. the response was lost
      // to a dropped connection) must reuse the same idempotency key so
      // the server replays instead of rejecting it as a duplicate.
      const submitResponse = await fetch(`/api/v1/terminals/${state.terminalId}/orders/${draft.orderId}/submit-draft`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({
          orderId: draft.orderId,
          expectedRowVersion: draft.rowVersion,
          operationId: `${draft.orderId}:submit`
        })
      });

      if (!submitResponse.ok) {
        showToast(describeHttpFailure(submitResponse.status), 'error');
        return;
      }

      const total = state.ticketItems.reduce((sum, item) => sum + (item.price * item.quantity), 0);
      const itemCount = state.ticketItems.reduce((sum, item) => sum + item.quantity, 0);
      state.shiftTotalOrders += 1;
      state.ticketItems = [];
      renderTicket();
      showToast(`Sipariş mutfağa iletildi. (${itemCount} kalem, ${formatMoney(total)})`, 'success');
      // V1-RMD-360: the moment a dispatch succeeds is also the moment the NEXT customer's order
      // starts - the same "scanner needs a focused field" reasoning as init()'s own focus call.
      if (el.searchInput) el.searchInput.focus({ preventScroll: true });
    } catch {
      showToast('Sunucuya ulaşılamadı. Sipariş iletilemedi.', 'error');
    } finally {
      // V1-RMD-157: KASA-1 is a fixed, shared endpoint for unrelated
      // walk-up customers, not a real table — a successful table-draft
      // above attached this order to it. Whatever happened afterward
      // (submit succeeded, failed, or threw), release the table now so
      // the next customer's dispatch never merges into this one (the
      // exact cross-customer merge the audit found). A failed release
      // here is logged only — it must never block or change the outcome
      // already shown to the cashier.
      if (draft && draft.orderId) {
        try {
          await fetch(`/api/v1/terminals/${state.terminalId}/orders/${draft.orderId}/send-to-cashier`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({ tableId: orderPayload.tableId })
          });
        } catch (releaseError) {
          console.error('KASA-1 serbest bırakılamadı:', releaseError);
        }
      }
      state.dispatchInFlight = false;
      updateDispatchAvailability();
    }
  }

  function parkCurrentTicket() {
    if (state.ticketItems.length === 0) return;
    state.parkedTickets.push({
      id: crypto.randomUUID(),
      parkedAt: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }),
      items: [...state.ticketItems]
    });
    saveParkedTickets();
    state.ticketItems = [];
    renderTicket();
    updateParkBadge();
  }

  async function recallParkedTicket(parkedId) {
    const index = state.parkedTickets.findIndex(p => p.id === parkedId);
    if (index < 0) return;

    if (state.ticketItems.length > 0) {
      const confirmReplace = await showConfirmModal(
        'Mevcut sepette ürünler var. Bekletilen fişi yüklemek mevcut sepeti değiştirecektir. Devam etmek istiyor musunuz?'
      );
      if (!confirmReplace) return;
    }

    state.ticketItems = state.parkedTickets[index].items;
    state.parkedTickets.splice(index, 1);
    saveParkedTickets();
    renderTicket();
    updateParkBadge();
    closeParkedModal();
  }

  // V1-RMD-360: one close path shared by the X button, a successful recall, and Escape (via
  // trapModalFocus's onClose) - releasing the focus trap is idempotent (see trapModalFocus's own
  // cleanup), so calling this from more than one of those paths in a row is safe.
  let closeParkedModalFocusTrap = () => {};
  function closeParkedModal() {
    if (el.parkedModal) el.parkedModal.hidden = true;
    closeParkedModalFocusTrap();
  }

  function renderParkedModal() {
    if (!el.parkedList) return;
    if (state.parkedTickets.length === 0) {
      el.parkedList.innerHTML = '<div class="parked-empty">Bekletilen fiş yok.</div>';
      return;
    }

    el.parkedList.innerHTML = state.parkedTickets.map(p => {
      const count = p.items.reduce((s, i) => s + i.quantity, 0);
      const total = p.items.reduce((s, i) => s + (i.price * i.quantity), 0);
      return `
        <div class="parked-card" data-parked-id="${escapeHtml(p.id)}">
          <div>
            <div class="parked-card__time">Bekletme Saati: ${escapeHtml(p.parkedAt)}</div>
            <div class="parked-card__meta">${count} Kalem • Toplam: ${formatMoney(total)}</div>
          </div>
          <button type="button" class="btn-micro btn-micro--recall" data-action="recall" data-id="${escapeHtml(p.id)}">Geri Yükle</button>
        </div>
      `;
    }).join('');
  }

  // V1-RMD-376 (module-by-module UI audit round 2): opens the option picker for a product that
  // has at least one modifier group. Mirrors WaiterPwa's product-sheet.js openProductSheet/
  // toggleModifier at the level this quick-order screen actually needs - one quantity (always
  // 1, incremented afterward like every other line here), no seat/course assignment (Cashier has
  // no seats and no course structure, unlike a table order).
  let closeModifierModalFocusTrap = () => {};

  function openModifierModal(product) {
    state.modifierContext = { product, chosen: {} };
    for (const group of product.modifierGroups) {
      state.modifierContext.chosen[group.modifierGroupId] = new Set();
    }
    renderModifierGroups();
    if (el.modifierModal) {
      el.modifierModal.hidden = false;
      closeModifierModalFocusTrap();
      closeModifierModalFocusTrap = trapModalFocus(el.modifierModal, closeModifierModal);
    }
  }

  function closeModifierModal() {
    if (el.modifierModal) el.modifierModal.hidden = true;
    closeModifierModalFocusTrap();
    state.modifierContext = null;
  }

  function modifierConfirmIsAllowed() {
    const context = state.modifierContext;
    if (!context) return false;
    return context.product.modifierGroups.every((group) => {
      if (group.minSelections <= 0) return true;
      return (context.chosen[group.modifierGroupId] || new Set()).size >= group.minSelections;
    });
  }

  function renderModifierGroups() {
    const context = state.modifierContext;
    if (!el.modifierGroupsList || !context) return;
    el.modifierGroupsList.innerHTML = context.product.modifierGroups.map((group) => {
      const single = group.selectionType === 'Single';
      const required = group.minSelections > 0;
      const rule = required
        ? (single ? 'zorunlu — bir tane seçin' : `zorunlu — en az ${group.minSelections}`)
        : (single ? 'bir tane seçilebilir' : `en fazla ${group.maxSelections || group.modifiers.length}`);
      const options = group.modifiers.map((modifier) => {
        const checked = context.chosen[group.modifierGroupId].has(modifier.modifierId);
        const priceLabel = modifier.priceDelta ? `+${formatMoney(modifier.priceDelta)}` : 'ücretsiz';
        return `
          <label class="modifier-option">
            <input type="${single ? 'radio' : 'checkbox'}" name="modifier-group-${escapeHtml(group.modifierGroupId)}"
                   data-group-id="${escapeHtml(group.modifierGroupId)}" data-modifier-id="${escapeHtml(modifier.modifierId)}"
                   data-single="${single}" ${checked ? 'checked' : ''} />
            <span class="modifier-option__name">${escapeHtml(modifier.name)}</span>
            <span class="modifier-option__price">${escapeHtml(priceLabel)}</span>
          </label>`;
      }).join('');
      return `
        <div class="modifier-group">
          <div class="modifier-group__head">
            <span class="modifier-group__name">${escapeHtml(group.name)}</span>
            <span class="modifier-group__rule${required ? ' modifier-group__rule--required' : ''}">${escapeHtml(rule)}</span>
          </div>
          ${options}
        </div>`;
    }).join('');
    if (el.btnModifierConfirm) el.btnModifierConfirm.disabled = !modifierConfirmIsAllowed();
  }

  function onModifierOptionChange(input) {
    const context = state.modifierContext;
    if (!context) return;
    const groupId = input.dataset.groupId;
    const modifierId = input.dataset.modifierId;
    const chosen = context.chosen[groupId];
    if (input.dataset.single === 'true') {
      chosen.clear();
      chosen.add(modifierId);
    } else if (input.checked) {
      chosen.add(modifierId);
    } else {
      chosen.delete(modifierId);
    }
    renderModifierGroups();
  }

  function confirmModifierSelection() {
    const context = state.modifierContext;
    if (!context || !modifierConfirmIsAllowed()) return;
    const chosenModifiers = [];
    for (const group of context.product.modifierGroups) {
      for (const modifierId of context.chosen[group.modifierGroupId]) {
        const modifier = group.modifiers.find((candidate) => candidate.modifierId === modifierId);
        if (modifier) chosenModifiers.push({ modifierId: modifier.modifierId, name: modifier.name, priceDelta: modifier.priceDelta || 0 });
      }
    }
    addProductToTicket(context.product, chosenModifiers);
    closeModifierModal();
  }

  function bindEvents() {
    // Category Tabs
    if (el.categoryTabs) {
      el.categoryTabs.addEventListener('click', (e) => {
        const btn = e.target.closest('.tab-chip');
        if (!btn) return;
        state.activeCategory = btn.dataset.catId;
        renderCategoryTabs();
        renderProducts(el.searchInput ? el.searchInput.value : '');
      });
    }

    // V1-RMD-205: waiter picker
    if (el.dispatchWaiterSelect) {
      el.dispatchWaiterSelect.addEventListener('change', (e) => {
        state.selectedWaiterUserId = e.target.value || null;
      });
    }

    // Search Input
    if (el.searchInput) {
      // V1-RMD-360 (module-by-module UI audit, 2026-09-27): re-rendered the whole product grid
      // on every single keystroke, with no debounce - a barcode scanner "types" a whole SKU in
      // well under 100ms, so a scan used to trigger one full re-render per character instead of
      // one for the finished value; a manual typist got the same on a slower kiosk. 120ms is
      // short enough that a human typist still sees results feel instant, long enough that a
      // scanner's whole burst collapses into a single render.
      let searchDebounceTimer = null;
      el.searchInput.addEventListener('input', (e) => {
        const value = e.target.value;
        window.clearTimeout(searchDebounceTimer);
        // V1-RMD-376 (module-by-module UI audit round 2, P2/P1 - saha gerçekliği/rakip
        // karşılaştırması): a barcode scanner "types" the SKU then stops - nothing here ever
        // acted on that, so the scanned text just sat in the box requiring the cashier to look
        // down and tap the matching card by hand on every single scan, real POS registers
        // (Toast/Square) auto-add the moment a scan resolves to exactly one SKU and clear the
        // field for the next one. Checked once the debounce settles (the same "typing paused"
        // moment that already told a human search apart from a scan's own burst).
        searchDebounceTimer = window.setTimeout(() => {
          const trimmed = value.trim();
          const exactMatch = trimmed && state.products.find(p => p.code && p.code.toLowerCase() === trimmed.toLowerCase());
          if (exactMatch) {
            el.searchInput.value = '';
            renderProducts('');
            if (exactMatch.modifierGroups.length > 0) openModifierModal(exactMatch);
            else addProductToTicket(exactMatch);
            return;
          }
          renderProducts(value);
        }, 120);
      });
    }

    // Product Click -> Add to Ticket (or open the option picker first - V1-RMD-376)
    if (el.productMatrix) {
      el.productMatrix.addEventListener('click', (e) => {
        const card = e.target.closest('.pos-product-card');
        if (!card) return;
        const prodId = card.dataset.productId;
        const prod = state.products.find(p => p.id === prodId);
        if (!prod) return;
        if (prod.modifierGroups.length > 0) openModifierModal(prod);
        else addProductToTicket(prod);
      });

      // V1-RMD-356 (independent 2026-09-26 audit, a low-severity finding): the card already carries
      // tabindex="0" role="button" (a keyboard user CAN tab to it), but nothing ever handled Enter/Space -
      // the two keys every screen reader and keyboard-only user expects to activate a focused button. Mirrors
      // the click handler above exactly rather than calling .click() (avoids depending on synthetic-event
      // bubbling back into this same delegated listener).
      el.productMatrix.addEventListener('keydown', (e) => {
        if (e.key !== 'Enter' && e.key !== ' ') return;
        const card = e.target.closest('.pos-product-card');
        if (!card) return;
        e.preventDefault();
        const prodId = card.dataset.productId;
        const prod = state.products.find(p => p.id === prodId);
        if (!prod) return;
        if (prod.modifierGroups.length > 0) openModifierModal(prod);
        else addProductToTicket(prod);
      });
    }

    // V1-RMD-376: modifier modal wiring.
    if (el.modifierGroupsList) {
      el.modifierGroupsList.addEventListener('change', (e) => {
        const input = e.target.closest('input[data-modifier-id]');
        if (input) onModifierOptionChange(input);
      });
    }
    if (el.btnModifierConfirm) el.btnModifierConfirm.addEventListener('click', confirmModifierSelection);
    if (el.btnModifierCancel) el.btnModifierCancel.addEventListener('click', closeModifierModal);
    if (el.btnCloseModifierModal) el.btnCloseModifierModal.addEventListener('click', closeModifierModal);

    // Ticket Actions (Inc / Dec / Free / Del)
    if (el.ticketItemsStream) {
      el.ticketItemsStream.addEventListener('input', (e) => {
        const field = e.target.closest('.item-note-input');
        if (!field) return;
        const index = state.ticketItems.findIndex(i => i.id === field.dataset.id);
        if (index >= 0) {
          state.ticketItems[index].note = field.value;
          // V1-RMD-360: does not call renderTicket() (typing would lose focus/caret position
          // on a re-render of the whole stream), so it needs its own explicit persist call.
          saveActiveTicket();
        }
      });
      el.ticketItemsStream.addEventListener('click', (e) => {
        const btn = e.target.closest('.btn-micro');
        if (!btn) return;
        const action = btn.dataset.action;
        const itemId = btn.dataset.id;
        const index = state.ticketItems.findIndex(i => i.id === itemId);
        if (index < 0) return;

        if (action === 'inc') {
          state.ticketItems[index].quantity += 1;
        } else if (action === 'dec') {
          state.ticketItems[index].quantity -= 1;
          if (state.ticketItems[index].quantity <= 0) {
            state.ticketItems.splice(index, 1);
          }
        } else if (action === 'del') {
          state.ticketItems.splice(index, 1);
        }
        renderTicket();
      });
    }

    // Clear Ticket
    if (el.btnClearTicket) {
      el.btnClearTicket.addEventListener('click', () => {
        if (state.ticketItems.length === 0) return;
        // V1-RMD-360 (module-by-module UI audit, 2026-09-27): this is the single most
        // destructive, completely irreversible action on this screen (unlike "Beklet", it
        // has no undo - the sepet is gone) and, unlike the LESS destructive "Geri Yükle"
        // (V1-RMD-355 already gave THAT one a confirmation), it had none at all. A single
        // stray tap during a busy shift silently discards every item already entered.
        void (async () => {
          const confirmed = await showConfirmModal(
            'Fişteki tüm ürünler silinecek. Bu işlem geri alınamaz. Devam etmek istiyor musunuz?'
          );
          if (!confirmed) return;
          state.ticketItems = [];
          renderTicket();
        })();
      });
    }

    // Dispatch Order to Kitchen
    if (el.btnDispatchOrder) {
      el.btnDispatchOrder.addEventListener('click', () => {
        void dispatchOrderToKitchen();
      });
    }

    // Park Ticket
    if (el.btnParkTicket) {
      el.btnParkTicket.addEventListener('click', () => {
        parkCurrentTicket();
      });
    }

    // Open Parked Modal
    if (el.btnRecallTicket) {
      el.btnRecallTicket.addEventListener('click', () => {
        renderParkedModal();
        if (el.parkedModal) {
          el.parkedModal.hidden = false;
          // V1-RMD-360: same focus-trap/Escape treatment as #confirmModal. The trap is
          // re-established on every open (renderParkedModal() just replaced the list's
          // innerHTML, so its own recall buttons are fresh DOM nodes each time).
          closeParkedModalFocusTrap();
          closeParkedModalFocusTrap = trapModalFocus(el.parkedModal, closeParkedModal);
        }
      });
    }

    // Close Parked Modal
    if (el.btnCloseParkedModal) {
      el.btnCloseParkedModal.addEventListener('click', closeParkedModal);
    }

    // Recall inside Modal
    if (el.parkedList) {
      el.parkedList.addEventListener('click', (e) => {
        const btn = e.target.closest('[data-action="recall"]');
        if (!btn) return;
        recallParkedTicket(btn.dataset.id);
      });
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
