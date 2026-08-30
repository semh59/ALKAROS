// ALKAROS Waiter PWA Controller (V1-WTR-008 / V1-RMD-051)
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

  // State
  const state = {
    terminalId: '00000000-0000-0000-0000-000000000001',
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
    offlineQueue: JSON.parse(localStorage.getItem('alkaros_waiter_offline_queue') || '[]')
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
    btnRefreshTables: document.getElementById('btnRefreshTables')
  };

  // Initialization
  async function init() {
    setupNetworkListeners();
    renderStatusRibbon();
    bindEvents();

    // Fetch initial data from Host API
    await loadInitialData();

    // Register Service Worker
    if ('serviceWorker' in navigator) {
      navigator.serviceWorker.register('./sw.js').catch(err => {
        console.warn('Service worker registration failed:', err);
      });
    }
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
    if (state.isOnline) {
      el.statusRibbon.className = 'status-ribbon online';
      el.statusText.textContent = 'Çevrimiçi • Canlı Bağlantı';
    } else {
      el.statusRibbon.className = 'status-ribbon offline';
      el.statusText.textContent = 'Çevrimdışı • İşlemler Güvenli Kuyrukta';
    }
    if (el.queueCount) {
      el.queueCount.textContent = state.offlineQueue.length > 0 ? `(${state.offlineQueue.length} bekleyen)` : '';
    }
  }

  function queueOrderAction(action) {
    state.offlineQueue.push({
      ...action,
      id: action.id || crypto.randomUUID(),
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
          // 4xx client errors should not block queue indefinitely
          console.warn('Order rejected by server (client error):', item.id, result.status);
          state.offlineQueue = state.offlineQueue.filter(q => q.id !== item.id);
          localStorage.setItem('alkaros_waiter_offline_queue', JSON.stringify(state.offlineQueue));
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
      'X-Idempotency-Key': orderPayload.id || crypto.randomUUID()
    };
    if (state.sessionToken) {
      headers['Authorization'] = `Bearer ${state.sessionToken}`;
    }

    try {
      const response = await fetch(`/api/v1/terminals/${state.terminalId}/orders/table-draft`, {
        method: 'POST',
        headers,
        credentials: 'include',
        body: JSON.stringify(orderPayload)
      });
      return {
        success: response.ok,
        status: response.status,
        isClientError: response.status >= 400 && response.status < 500
      };
    } catch {
      return { success: false, status: 0, isNetworkError: true };
    }
  }

  // Load Data from Host API
  async function loadInitialData() {
    try {
      // 1. Fetch Zones
      const zonesRes = await fetch(`/api/v1/terminals/${state.terminalId}/table-management/zones`, { credentials: 'include' });
      if (zonesRes.ok) {
        const zonesData = await zonesRes.json();
        const list = Array.isArray(zonesData) ? zonesData : zonesData.zones || [];
        state.zones = [{ id: 'all', name: 'Tüm Masalar' }, ...list.map(z => ({ id: z.zoneId || z.id, name: z.zoneName || z.name }))];
      } else {
        state.zones = [{ id: 'all', name: 'Tüm Masalar' }];
      }

      // 2. Fetch Categories & Products
      const [catRes, prodRes] = await Promise.all([
        fetch(`/api/v1/terminals/${state.terminalId}/catalog?category=all`, { credentials: 'include' }).catch(() => null),
        fetch(`/api/v1/terminals/${state.terminalId}/catalog`, { credentials: 'include' }).catch(() => null)
      ]);

      if (catRes && catRes.ok) {
        const catData = await catRes.json();
        const list = Array.isArray(catData) ? catData : catData.categories || [];
        state.categories = list.map(c => ({ id: c.categoryId || c.id, name: c.categoryName || c.name }));
      } else {
        state.categories = [];
      }

      if (prodRes && prodRes.ok) {
        const prodData = await prodRes.json();
        const list = Array.isArray(prodData) ? prodData : prodData.products || [];
        state.products = list.map(p => ({
          id: p.productId || p.id,
          categoryId: p.categoryId,
          name: p.productName || p.name,
          price: p.currentPrice || p.price || 0
        }));
      } else {
        state.products = [];
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
            <span class="table-capacity">👤 ${escapeHtml(table.seats || 4)}</span>
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
      const matchCat = state.activeCategory === 'all' || p.categoryId === state.activeCategory;
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
      el.cartItemsList.innerHTML = '<div style="color: var(--text-dim); text-align: center; padding: 24px 0;">Henüz ürün eklenmedi.</div>';
      updateCartTotals();
      return;
    }

    el.cartItemsList.innerHTML = state.cart.map(item => `
      <div class="cart-item">
        <div class="cart-item-info">
          <div class="cart-item-name">${escapeHtml(item.name)}</div>
          <div style="font-size: 0.8rem; color: var(--text-muted);">${formatMoney(item.price)}</div>
          ${item.note ? `<div class="cart-item-note">Not: ${escapeHtml(item.note)}</div>` : ''}
        </div>
        <div class="quantity-stepper">
          <button type="button" class="btn-step" data-action="dec" data-id="${escapeHtml(item.id)}">−</button>
          <span style="font-weight: 800; min-width: 24px; text-align: center;">${item.quantity}</span>
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
        el.orderDrawer.style.display = 'flex';
        el.selectedTableLabel.textContent = `Masa: ${state.selectedTable.number} (${getStatusLabel(state.selectedTable.status)})`;
      } else {
        el.orderDrawer.style.display = 'none';
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
        el.orderModal.style.display = 'flex';
        renderCategoryFilters();
        renderProducts();
        renderCart();
      });
    }

    // Close Order Modal
    if (el.btnCloseModal) {
      el.btnCloseModal.addEventListener('click', () => {
        el.orderModal.style.display = 'none';
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
            id: crypto.randomUUID(),
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

        const orderId = crypto.randomUUID();
        const orderPayload = {
          id: orderId,
          tableId: state.selectedTable.id,
          tableNumber: state.selectedTable.number,
          waiterName: state.currentUser?.name || 'Garson',
          items: state.cart.map(item => ({
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
            el.orderModal.style.display = 'none';
            renderTables();
            updateCartTotals();
            alert(`Sipariş mutfağa iletildi! (${state.selectedTable.number})`);
          } else if (res.isClientError) {
            alert(`Sipariş iletilemedi (Hata: ${res.status}). Lütfen masa ve ürün bilgilerini kontrol edin.`);
          } else {
            // Server error / network failure: queue order and notify without claiming success
            queueOrderAction(orderPayload);
            state.cart = [];
            el.orderModal.style.display = 'none';
            updateCartTotals();
            alert(`Sunucuya ulaşılamadı. Sipariş çevrimdışı kuyruğa alındı. (${state.selectedTable.number})`);
          }
        } else {
          queueOrderAction(orderPayload);
          state.cart = [];
          el.orderModal.style.display = 'none';
          updateCartTotals();
          alert(`Çevrimdışı mod: Sipariş yerel kuyruğa kaydedildi. Bağlantı gelince iletilecek.`);
        }
      });
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
