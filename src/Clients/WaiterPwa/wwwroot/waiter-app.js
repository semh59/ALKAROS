// ALKAROS Waiter PWA Controller (V1-WTR-008 / V1-RMD-051 / V1-RMD-066 / V1-RMD-129)
// Authoritative Endpoints: /orders/table-draft, /table-management/zones, /table-management/tables, /catalog
// V1-RMD-129: /catalog-management/categories dropped from this list — it requires the
// manager cookie (CatalogManagerEndpointFilter), which a plain waiter session never
// has, and was never actually called correctly anyway. Categories are derived from
// the /catalog product response's own categoryCode/categoryName instead.
(function () {
  'use strict';

  // Utility: HTML escaping to prevent XSS injection
  function escapeHtml(str) {
    if (str === null || str === undefined) return '';
    const text = String(str);
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }

  function formatMoney(amount) {
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(amount || 0);
  }

  // UI_STYLE_GUIDE §3: raw HTTP status codes are never shown to the user.
  function describeHttpFailure(status) {
    if (status === 400) return 'İstek doğrulanamadı. Lütfen masa ve ürün bilgilerini kontrol edin.';
    if (status === 401) return 'Oturum geçersiz veya süresi doldu. Lütfen yeniden giriş yapın.';
    if (status === 403) return 'Bu işlem için yetkiniz yok.';
    if (status === 404) return 'İlgili kayıt bulunamadı.';
    if (status === 409) return 'Sipariş başka bir işlem tarafından değiştirildi. Lütfen tekrar deneyin.';
    if (status >= 500) return 'Sunucu hatası oluştu. Lütfen tekrar deneyin.';
    return 'İstek sunucu tarafından reddedildi. Lütfen tekrar deneyin.';
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

  // State
  const state = {
    terminalId: deviceTerminalId(),
    isOnline: navigator.onLine,
    sessionToken: sessionStorage.getItem('alkaros_waiter_token') || '',
    currentUser: JSON.parse(sessionStorage.getItem('alkaros_waiter_user') || 'null'),
    activeZone: 'all',
    selectedTable: null,
    cart: [],
    categories: [],
    products: [],
    zones: [],
    tables: [],
    activeCategory: 'all',
    offlineQueue: JSON.parse(localStorage.getItem('alkaros_waiter_offline_queue') || '[]'),
    failedOrders: JSON.parse(localStorage.getItem('alkaros_waiter_failed_orders') || '[]'),
    dispatchInFlight: false
  };

  // DOM Elements
  const el = {
    statusRibbon: document.getElementById('statusRibbon'),
    statusText: document.getElementById('statusText'),
    queueCount: document.getElementById('queueCount'),
    zoneList: document.getElementById('zoneList'),
    tablesGrid: document.getElementById('tablesGrid'),
    orderDrawer: document.getElementById('orderDrawer'),
    selectedTableLabel: document.getElementById('selectedTableLabel'),
    cartItemCount: document.getElementById('cartItemCount'),
    cartTotalAmount: document.getElementById('cartTotalAmount'),
    orderModal: document.getElementById('orderModal'),
    modalTableTitle: document.getElementById('modalTableTitle'),
    modalProductSearch: document.getElementById('modalProductSearch'),
    categoryFilterBar: document.getElementById('categoryFilterBar'),
    productGrid: document.getElementById('productGrid'),
    cartItemsList: document.getElementById('cartItemsList'),
    btnSendKitchen: document.getElementById('btnSendKitchen'),
    btnCloseModal: document.getElementById('btnCloseModal'),
    btnOpenOrderModal: document.getElementById('btnOpenOrderModal'),
    btnRefreshTables: document.getElementById('btnRefreshTables'),
    btnStaffProfile: document.getElementById('btnStaffProfile'),
    loginOverlay: document.getElementById('loginOverlay'),
    loginForm: document.getElementById('loginForm'),
    loginUsername: document.getElementById('loginUsername'),
    loginPassword: document.getElementById('loginPassword'),
    loginError: document.getElementById('loginError'),
    loginSubmit: document.getElementById('loginSubmit')
  };

  // Initialization
  async function init() {
    setupNetworkListeners();
    renderStatusRibbon();
    bindEvents();
    bindAuthEvents();

    // The tables/catalog/order endpoints are cashier-session scoped. Without a
    // valid session for this device's terminal id, show the sign-in form and
    // stop - loading would only 401.
    if (!(await hasValidSession())) {
      showLogin();
      return;
    }

    // Fetch initial data from Host API
    await loadInitialData();

    // Register Service Worker. Offline queueing depends on it, so a failure or
    // an insecure context (plain HTTP over a LAN IP) is surfaced to the user
    // rather than silently swallowed.
    registerOfflineWorker();
    connectOrderReadyHub();
  }

  // Staff sign-in
  function bindAuthEvents() {
    if (el.loginForm) el.loginForm.addEventListener('submit', submitLogin);
    if (el.btnStaffProfile) {
      el.btnStaffProfile.addEventListener('click', () => {
        if (window.confirm('Oturumu kapatmak istiyor musunuz?')) void signOut();
      });
    }
  }

  function showLogin() {
    if (!el.loginOverlay) return;
    el.loginOverlay.hidden = false;
    if (el.loginUsername) el.loginUsername.focus();
  }

  async function hasValidSession() {
    try {
      const res = await fetch(`/api/v1/auth/session?terminalId=${state.terminalId}`, { credentials: 'include' });
      if (!res.ok) return false;
      state.currentUser = await res.json();
      return true;
    } catch {
      return false;
    }
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
      let res;
      try {
        res = await fetch('/api/v1/auth/login', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify({ terminalId: state.terminalId, username, password })
        });
      } catch {
        // Found by an independent audit (2026-09-06): a network-level
        // failure (offline, DNS, TLS) throws before a response exists, and
        // its message is the browser's own English text (e.g. "Failed to
        // fetch") - that must never reach the user directly.
        throw new Error('Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.');
      }
      if (!res.ok) {
        let message = 'Kullanıcı adı veya şifre hatalı.';
        try { const body = await res.json(); message = body?.error?.message || message; } catch { /* non-json */ }
        throw new Error(message);
      }
      state.currentUser = await res.json();
      el.loginPassword.value = '';
      el.loginOverlay.hidden = true;
      await loadInitialData();
      registerOfflineWorker();
      connectOrderReadyHub();
    } catch (err) {
      el.loginError.textContent = err && err.message ? err.message : 'Giriş başarısız.';
      el.loginError.hidden = false;
    } finally {
      el.loginSubmit.disabled = false;
      el.loginSubmit.textContent = 'Giriş yap';
    }
  }

  async function signOut() {
    try {
      await fetch(`/api/v1/auth/logout?terminalId=${state.terminalId}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: '{}'
      });
    } catch { /* ignore - reload still clears the view */ }
    window.location.reload();
  }

  function registerOfflineWorker() {
    if (!window.isSecureContext || !('serviceWorker' in navigator)) {
      state.offlineDisabled = true;
      renderStatusRibbon();
      console.warn('Offline mode disabled: a secure context (HTTPS or localhost) is required.');
      return;
    }
    navigator.serviceWorker.register('./sw.js').catch(err => {
      state.offlineDisabled = true;
      renderStatusRibbon();
      console.warn('Service worker registration failed; offline mode is disabled:', err);
    });
  }

  // Network & Reliable Offline Queue
  function setupNetworkListeners() {
    window.addEventListener('online', () => {
      state.isOnline = true;
      renderStatusRibbon();
      flushOfflineQueue();
    });

    window.addEventListener('offline', () => {
      state.isOnline = false;
      renderStatusRibbon();
    });
  }

  function renderStatusRibbon() {
    if (!el.statusRibbon) return;
    if (state.offlineDisabled) {
      el.statusRibbon.className = 'status-ribbon offline';
      el.statusText.textContent = 'Çevrimdışı mod kapalı • Güvenli bağlantı (HTTPS) gerekli';
    } else if (state.isOnline) {
      el.statusRibbon.className = 'status-ribbon online';
      el.statusText.textContent = 'Çevrimiçi • Canlı Bağlantı';
    } else {
      el.statusRibbon.className = 'status-ribbon offline';
      el.statusText.textContent = 'Çevrimdışı • İşlemler Güvenli Kuyrukta';
    }
    if (el.queueCount) {
      const pending = state.offlineQueue.length;
      const failed = state.failedOrders ? state.failedOrders.length : 0;
      let text = '';
      if (pending > 0) text += `(${pending} bekleyen)`;
      if (failed > 0) text += ` [${failed} hatalı işlem]`;
      el.queueCount.textContent = text;
    }
  }

  function queueOrderAction(action) {
    state.offlineQueue.push({
      ...action,
      id: action.id || randomUUID(),
      timestamp: new Date().toISOString()
    });
    localStorage.setItem('alkaros_waiter_offline_queue', JSON.stringify(state.offlineQueue));
    renderStatusRibbon();
  }

  async function flushOfflineQueue() {
    if (state.offlineQueue.length === 0 || !state.isOnline) return;

    const itemsToSync = [...state.offlineQueue];
    for (const item of itemsToSync) {
      try {
        const result = await postOrderToBackend(item);
        if (result.success) {
          // Authoritative 2xx acknowledgment: ONLY remove when actually succeeded
          state.offlineQueue = state.offlineQueue.filter(q => q.id !== item.id);
          localStorage.setItem('alkaros_waiter_offline_queue', JSON.stringify(state.offlineQueue));
        } else if (result.isClientError) {
          // 4xx client errors: preserve in failedOrders so unsubmitted orders are never destroyed
          console.error('Order rejected by server (validation/client error):', item.id, result.status, result.errorMessage);
          state.offlineQueue = state.offlineQueue.filter(q => q.id !== item.id);
          state.failedOrders = state.failedOrders || [];
          state.failedOrders.push({
            ...item,
            rejectedAt: new Date().toISOString(),
            status: result.status,
            error: result.errorMessage || 'Sunucu doğrulama hatası (4xx)'
          });
          localStorage.setItem('alkaros_waiter_offline_queue', JSON.stringify(state.offlineQueue));
          localStorage.setItem('alkaros_waiter_failed_orders', JSON.stringify(state.failedOrders));
        } else {
          // Server error 5xx or offline: keep in queue and stop retry loop
          console.warn('Server temporary error during queue flush, keeping in queue:', item.id);
          break;
        }
      } catch (err) {
        console.warn('Network error during queue flush, keeping items in queue:', err);
        break;
      }
    }
    renderStatusRibbon();
  }

  async function postOrderToBackend(orderPayload) {
    const headers = {
      'Content-Type': 'application/json',
      'X-Idempotency-Key': orderPayload.id || randomUUID()
    };
    if (state.sessionToken) {
      headers['Authorization'] = `Bearer ${state.sessionToken}`;
    }

    try {
      const draftResponse = await fetch(`/api/v1/terminals/${state.terminalId}/orders/table-draft`, {
        method: 'POST',
        headers,
        credentials: 'include',
        body: JSON.stringify(orderPayload)
      });
      if (!draftResponse.ok) {
        let errorData = null;
        try { errorData = await draftResponse.json(); } catch { /* ignore non-json error */ }
        return {
          success: false,
          status: draftResponse.status,
          isClientError: draftResponse.status >= 400 && draftResponse.status < 500,
          errorMessage: errorData?.error?.message || errorData?.message || describeHttpFailure(draftResponse.status)
        };
      }

      const draft = await draftResponse.json();
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
        headers,
        credentials: 'include',
        body: JSON.stringify({
          orderId: draft.orderId,
          expectedRowVersion: draft.rowVersion,
          operationId: `${draft.orderId}:submit`
        })
      });
      if (!submitResponse.ok) {
        let errorData = null;
        try { errorData = await submitResponse.json(); } catch { /* ignore non-json error */ }
        return {
          success: false,
          status: submitResponse.status,
          isClientError: submitResponse.status >= 400 && submitResponse.status < 500,
          errorMessage: errorData?.error?.message || errorData?.message || describeHttpFailure(submitResponse.status)
        };
      }

      return { success: true, status: submitResponse.status, isClientError: false, errorMessage: null };
    } catch {
      return { success: false, status: 0, isNetworkError: true, errorMessage: 'Ağ bağlantısı kurulamadı.' };
    }
  }

  // Load Data from Host API
  async function loadInitialData() {
    try {
      // 1. Fetch Zones
      const zonesRes = await fetch(`/api/v1/terminals/${state.terminalId}/table-management/zones`, { credentials: 'include' });
      if (zonesRes.status === 401) {
        showLogin();
        return;
      }
      if (zonesRes.ok) {
        const zonesData = await zonesRes.json();
        const list = Array.isArray(zonesData) ? zonesData : zonesData.zones || [];
        state.zones = [{ id: 'all', name: 'Tüm Masalar' }, ...list.map(z => ({ id: z.zoneId || z.id, name: z.zoneName || z.name }))];
      } else {
        state.zones = [{ id: 'all', name: 'Tüm Masalar' }];
      }

      // 2. Fetch Products — categories are derived from this same response.
      // V1-RMD-129: found by an independent audit (2026-09-09) — the old
      // code called catalog?category=all as if it were a distinct
      // categories endpoint and parsed a "{ categories: [...] }" shape
      // /catalog never returns (it returns CatalogPage: { items,
      // nextCursor }, the same shape as the real product fetch below), so
      // state.categories was always empty and the category filter bar
      // never rendered. It also read products as p.productName/p.currentPrice/
      // p.categoryId, none of which exist on the real CatalogProductDto
      // (productId/name/unitPrice/categoryCode) — prices always showed
      // 0,00₺ and the category filter matched nothing. Categories now come
      // straight from the one products response's own categoryCode/
      // categoryName fields, so there is no second endpoint to drift out
      // of sync with the real contract.
      const prodRes = await fetch(`/api/v1/terminals/${state.terminalId}/catalog`, { credentials: 'include' }).catch(() => null);

      if (prodRes && prodRes.ok) {
        const prodData = await prodRes.json();
        const list = Array.isArray(prodData) ? prodData : prodData.items || [];
        state.products = list.map(p => ({
          id: p.productId || p.id,
          categoryCode: p.categoryCode,
          name: p.name,
          price: p.unitPrice || 0
        }));

        const seenCategories = new Map();
        for (const p of list) {
          if (p.categoryCode && !seenCategories.has(p.categoryCode)) {
            seenCategories.set(p.categoryCode, p.categoryName || p.categoryCode);
          }
        }
        state.categories = [...seenCategories].map(([id, name]) => ({ id, name }));
      } else {
        state.products = [];
        state.categories = [];
      }

      // 3. Fetch Tables
      await loadTables();
    } catch (err) {
      console.warn('Host API unreachable:', err);
      state.zones = [{ id: 'all', name: 'Tüm Masalar' }];
      state.categories = [];
      state.products = [];
      state.tables = [];
      if (el.statusText) el.statusText.textContent = 'Bağlantı kesildi — Çevrimdışı';
      if (el.statusRibbon) el.statusRibbon.className = 'status-ribbon offline';
    }

    renderZones();
    renderTables();
    renderCategoryFilters();
    renderProducts();
  }

  async function loadTables() {
    try {
      const res = await fetch(`/api/v1/terminals/${state.terminalId}/table-management/tables`, { credentials: 'include' });
      if (res.ok) {
        const data = await res.json();
        const list = Array.isArray(data) ? data : data.tables || [];
        state.tables = list.map(t => ({
          id: t.tableId || t.id,
          number: t.tableNumber || t.number,
          seats: t.capacity || t.seats || 4,
          zoneId: t.zoneId,
          status: (t.currentStatus || t.status || 'available').toLowerCase(),
          amount: t.currentAmount || t.amount || 0
        }));
      } else {
        state.tables = [];
      }
    } catch {
      state.tables = [];
    }
    renderTables();
  }

  // Render Functions
  function renderZones() {
    if (!el.zoneList) return;
    el.zoneList.innerHTML = state.zones.map(zone => `
      <button type="button" class="zone-chip ${state.activeZone === zone.id ? 'active' : ''}" data-zone-id="${escapeHtml(zone.id)}">
        ${escapeHtml(zone.name)}
      </button>
    `).join('');
  }

  function renderTables() {
    if (!el.tablesGrid) return;
    const filtered = state.activeZone === 'all'
      ? state.tables
      : state.tables.filter(t => t.zoneId === state.activeZone);

    el.tablesGrid.innerHTML = filtered.map(table => {
      const isSelected = state.selectedTable?.id === table.id;
      return `
        <div class="table-card ${escapeHtml(table.status)} ${isSelected ? 'selected' : ''}" data-table-id="${escapeHtml(table.id)}" tabindex="0" role="button">
          <div class="table-header-row">
            <span class="table-number">${escapeHtml(table.number)}</span>
            <span class="table-capacity"><svg class="icon" aria-hidden="true"><use href="#ico-user"/></svg> ${escapeHtml(table.seats || 4)}</span>
          </div>
          <span class="table-status-tag ${escapeHtml(table.status)}">${escapeHtml(getStatusLabel(table.status))}</span>
          <div class="table-amount">${table.amount > 0 ? formatMoney(table.amount) : 'Boş'}</div>
        </div>
      `;
    }).join('');
  }

  function getStatusLabel(status) {
    switch (status) {
      case 'available': return 'Boş';
      case 'occupied': return 'Dolu';
      case 'reserved': return 'Rezerve';
      case 'cleaning': return 'Temizlik';
      default: return status;
    }
  }

  function renderCategoryFilters() {
    if (!el.categoryFilterBar) return;
    const allCategories = [{ id: 'all', name: 'Tümü' }, ...state.categories];
    el.categoryFilterBar.innerHTML = allCategories.map(cat => `
      <button type="button" class="zone-chip ${state.activeCategory === cat.id ? 'active' : ''}" data-cat-id="${escapeHtml(cat.id)}">
        ${escapeHtml(cat.name)}
      </button>
    `).join('');
  }

  function renderProducts(searchQuery = '') {
    if (!el.productGrid) return;
    const query = searchQuery.trim().toLowerCase();
    const filtered = state.products.filter(p => {
      const matchCat = state.activeCategory === 'all' || p.categoryCode === state.activeCategory;
      const matchSearch = !query || p.name.toLowerCase().includes(query);
      return matchCat && matchSearch;
    });

    el.productGrid.innerHTML = filtered.map(prod => `
      <div class="product-card" data-product-id="${escapeHtml(prod.id)}" role="button" tabindex="0">
        <div>
          <div class="product-name">${escapeHtml(prod.name)}</div>
        </div>
        <div class="product-price">${formatMoney(prod.price)}</div>
      </div>
    `).join('');
  }

  function renderCart() {
    if (!el.cartItemsList) return;
    if (state.cart.length === 0) {
      el.cartItemsList.innerHTML = '<div class="cart-empty">Henüz ürün eklenmedi.</div>';
      updateCartTotals();
      return;
    }

    el.cartItemsList.innerHTML = state.cart.map(item => `
      <div class="cart-item">
        <div class="cart-item-info">
          <div class="cart-item-name">${escapeHtml(item.name)}</div>
          <div class="cart-item-price">${formatMoney(item.price)}</div>
          <input class="cart-item-note-input" type="text" maxlength="200" placeholder="Not (örn. az, acısız, ekmek ayrı)" value="${escapeHtml(item.note || '')}" data-id="${escapeHtml(item.id)}" aria-label="${escapeHtml(item.name)} özel talimat" />
        </div>
        <div class="quantity-stepper">
          <button type="button" class="btn-step" data-action="dec" data-id="${escapeHtml(item.id)}">−</button>
          <span class="cart-item-qty">${item.quantity}</span>
          <button type="button" class="btn-step" data-action="inc" data-id="${escapeHtml(item.id)}">+</button>
        </div>
      </div>
    `).join('');

    updateCartTotals();
  }

  function updateCartTotals() {
    const totalItems = state.cart.reduce((sum, item) => sum + item.quantity, 0);
    const totalAmount = state.cart.reduce((sum, item) => sum + (item.price * item.quantity), 0);

    if (el.cartItemCount) el.cartItemCount.textContent = `${totalItems} Ürün`;
    if (el.cartTotalAmount) el.cartTotalAmount.textContent = formatMoney(totalAmount);

    if (el.orderDrawer) {
      if (state.selectedTable) {
        el.orderDrawer.hidden = false;
        el.selectedTableLabel.textContent = `Masa: ${state.selectedTable.number} (${getStatusLabel(state.selectedTable.status)})`;
      } else {
        el.orderDrawer.hidden = true;
      }
    }
  }

  // Event Handlers & Binding
  function bindEvents() {
    // Zone Filter Click
    if (el.zoneList) {
      el.zoneList.addEventListener('click', (e) => {
        const btn = e.target.closest('.zone-chip');
        if (!btn) return;
        state.activeZone = btn.dataset.zoneId;
        renderZones();
        renderTables();
      });
    }

    // Refresh Tables Button
    if (el.btnRefreshTables) {
      el.btnRefreshTables.addEventListener('click', async () => {
        await loadTables();
      });
    }

    // Table Selection Click
    if (el.tablesGrid) {
      el.tablesGrid.addEventListener('click', (e) => {
        const card = e.target.closest('.table-card');
        if (!card) return;
        const tableId = card.dataset.tableId;
        const table = state.tables.find(t => t.id === tableId);
        if (!table) return;

        state.selectedTable = table;
        renderTables();
        updateCartTotals();
      });
    }

    // Open Order Modal
    if (el.btnOpenOrderModal) {
      el.btnOpenOrderModal.addEventListener('click', () => {
        if (!state.selectedTable) return;
        el.modalTableTitle.textContent = `${state.selectedTable.number} — Sipariş Al`;
        el.orderModal.hidden = false;
        renderCategoryFilters();
        renderProducts();
        renderCart();
      });
    }

    // Close Order Modal
    if (el.btnCloseModal) {
      el.btnCloseModal.addEventListener('click', () => {
        el.orderModal.hidden = true;
      });
    }

    // Product Search Input
    if (el.modalProductSearch) {
      el.modalProductSearch.addEventListener('input', (e) => {
        renderProducts(e.target.value);
      });
    }

    // Category Filter in Modal
    if (el.categoryFilterBar) {
      el.categoryFilterBar.addEventListener('click', (e) => {
        const btn = e.target.closest('.zone-chip');
        if (!btn) return;
        state.activeCategory = btn.dataset.catId;
        renderCategoryFilters();
        renderProducts(el.modalProductSearch ? el.modalProductSearch.value : '');
      });
    }

    // Product Select Click -> Add to Cart
    if (el.productGrid) {
      el.productGrid.addEventListener('click', (e) => {
        const card = e.target.closest('.product-card');
        if (!card) return;
        const prodId = card.dataset.productId;
        const prod = state.products.find(p => p.id === prodId);
        if (!prod) return;

        const existing = state.cart.find(i => i.productId === prod.id);
        if (existing) {
          existing.quantity += 1;
        } else {
          state.cart.push({
            id: randomUUID(),
            productId: prod.id,
            name: prod.name,
            price: prod.price,
            quantity: 1,
            note: ''
          });
        }
        renderCart();
      });
    }

    // Cart Item Stepper (Inc / Dec)
    if (el.cartItemsList) {
      el.cartItemsList.addEventListener('input', (e) => {
        const field = e.target.closest('.cart-item-note-input');
        if (!field) return;
        const index = state.cart.findIndex(i => i.id === field.dataset.id);
        if (index >= 0) state.cart[index].note = field.value;
      });
      el.cartItemsList.addEventListener('click', (e) => {
        const btn = e.target.closest('.btn-step');
        if (!btn) return;
        const action = btn.dataset.action;
        const itemId = btn.dataset.id;
        const index = state.cart.findIndex(i => i.id === itemId);
        if (index < 0) return;

        if (action === 'inc') {
          state.cart[index].quantity += 1;
        } else if (action === 'dec') {
          state.cart[index].quantity -= 1;
          if (state.cart[index].quantity <= 0) {
            state.cart.splice(index, 1);
          }
        }
        renderCart();
      });
    }

    // Submit Order to Kitchen
    if (el.btnSendKitchen) {
      el.btnSendKitchen.addEventListener('click', async () => {
        if (state.cart.length === 0 || !state.selectedTable) return;
        // Found by an independent audit (2026-09-05): nothing stopped a
        // second click (double-tap, or a click while the fetch above is
        // still in flight) from sending the same cart twice. This handler
        // is async but was never guarded against re-entrancy.
        if (state.dispatchInFlight) return;
        state.dispatchInFlight = true;
        el.btnSendKitchen.disabled = true;

        try {
          const orderId = randomUUID();
          const orderPayload = {
            id: orderId,
            tableId: state.selectedTable.id,
            tableNumber: state.selectedTable.number,
            waiterName: state.currentUser?.name || 'Garson',
            items: state.cart.map(item => ({
              // Stable per-line id (already used for local cart tracking)
              // makes a retried draft submission idempotent server-side
              // instead of appending a duplicate line on every retry — the
              // offline queue in particular resends on an ambiguous
              // (dropped-connection) failure.
              id: item.id,
              productId: item.productId,
              name: item.name,
              productName: item.name,
              quantity: item.quantity,
              unitPrice: item.price,
              specialInstructions: item.note
            })),
            createdAt: new Date().toISOString()
          };

          if (state.isOnline) {
            const res = await postOrderToBackend(orderPayload);
            if (res.success) {
              // Authoritative server success
              state.selectedTable.status = 'occupied';
              state.selectedTable.amount += state.cart.reduce((sum, item) => sum + (item.price * item.quantity), 0);
              state.cart = [];
              el.orderModal.hidden = true;
              renderTables();
              updateCartTotals();
              alert(`Sipariş mutfağa iletildi! (${state.selectedTable.number})`);
            } else if (res.isClientError) {
              alert(res.errorMessage || describeHttpFailure(res.status));
            } else {
              // Server error / network failure: queue order and notify without claiming success
              queueOrderAction(orderPayload);
              state.cart = [];
              el.orderModal.hidden = true;
              updateCartTotals();
              alert(`Sunucuya ulaşılamadı. Sipariş çevrimdışı kuyruğa alındı. (${state.selectedTable.number})`);
            }
          } else {
            queueOrderAction(orderPayload);
            state.cart = [];
            el.orderModal.hidden = true;
            updateCartTotals();
            alert(`Çevrimdışı mod: Sipariş yerel kuyruğa kaydedildi. Bağlantı gelince iletilecek.`);
          }
        } finally {
          state.dispatchInFlight = false;
          el.btnSendKitchen.disabled = false;
        }
      });
    }
  }

  // Order-ready notifications (V1-WTR-009). Only reachable when a
  // deployment has turned on kitchen live-sync (kitchen.live_sync_enabled,
  // V1-SET-002); if it is off the hub connects but the server simply never
  // sends anything. There is no waiter-to-table assignment tracked anywhere
  // in this system, so every connected device gets every "ready" event —
  // targeted delivery is a follow-on once that assignment exists.
  let orderReadyConnection = null;

  function connectOrderReadyHub() {
    if (orderReadyConnection || typeof signalR === 'undefined') return;
    orderReadyConnection = new signalR.HubConnectionBuilder()
      .withUrl(`/hubs/waiter-order-status?terminalId=${state.terminalId}`)
      .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();
    orderReadyConnection.on('OrderItemReady', showOrderReadyBanner);
    orderReadyConnection.start().catch(() => {
      // Best-effort: no live-sync deployment, or a transient network issue.
      // Automatic reconnect (above) keeps trying; nothing to surface here
      // that the existing offline ribbon does not already say.
    });
  }

  function showOrderReadyBanner(payload) {
    const productName = escapeHtml(payload && payload.productName || 'Bir ürün');
    const banner = document.createElement('div');
    banner.className = 'order-ready-banner';
    banner.setAttribute('role', 'status');
    banner.textContent = `${productName} hazır`;
    document.body.appendChild(banner);
    window.setTimeout(() => banner.remove(), 8000);

    if (window.Notification && Notification.permission === 'granted') {
      try { new Notification('Sipariş hazır', { body: `${productName} hazır`, tag: 'alkaros-order-ready' }); }
      catch { /* Notification constructor can throw on some mobile browsers; the in-page banner already covers it. */ }
    } else if (window.Notification && Notification.permission === 'default') {
      Notification.requestPermission().catch(() => { /* ignore - stays in-page-only */ });
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
