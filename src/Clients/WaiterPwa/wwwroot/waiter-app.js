// ALKAROS Waiter PWA Controller (V1-WTR-008 - Real API & Reliable Queue)
(function () {
  'use strict';

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
    el.queueCount.textContent = state.offlineQueue.length > 0 ? `(${state.offlineQueue.length} bekleyen)` : '';
  }

  function queueOrderAction(action) {
    state.offlineQueue.push({
      ...action,
      id: crypto.randomUUID(),
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
        const ok = await postOrderToBackend(item);
        if (ok) {
          // Authoritative 2xx acknowledgment: ONLY remove when actually succeeded!
          state.offlineQueue = state.offlineQueue.filter(q => q.id !== item.id);
          localStorage.setItem('alkaros_waiter_offline_queue', JSON.stringify(state.offlineQueue));
        } else {
          // If server returned non-2xx, keep in queue and stop retry loop
          console.warn('Server rejected order item, keeping in queue for review:', item.id);
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
    try {
      const response = await fetch(`/api/v1/terminals/${state.terminalId}/orders/table-draft`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Idempotency-Key': orderPayload.id
        },
        body: JSON.stringify(orderPayload)
      });
      return response.ok;
    } catch {
      return false;
    }
  }

  // Load Data from Host API
  async function loadInitialData() {
    try {
      // 1. Fetch Zones
      const zonesRes = await fetch(`/api/v1/terminals/${state.terminalId}/table-management/zones`);
      if (zonesRes.ok) {
        const zonesData = await zonesRes.json();
        state.zones = [{ id: 'all', name: 'Tüm Masalar' }, ...(zonesData.zones || zonesData || [])];
      } else {
        state.zones = [{ id: 'all', name: 'Tüm Masalar' }];
      }

      // 2. Fetch Categories & Catalog
      const catRes = await fetch(`/api/v1/terminals/${state.terminalId}/catalog-management/categories`);
      if (catRes.ok) {
        const catData = await catRes.json();
        state.categories = catData.categories || catData || [];
      } else {
        state.categories = [];
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
      const res = await fetch(`/api/v1/terminals/${state.terminalId}/table-management/tables`);
      if (res.ok) {
        const data = await res.json();
        state.tables = data.tables || data || [];
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
      <button type="button" class="zone-chip ${state.activeZone === zone.id ? 'active' : ''}" data-zone-id="${zone.id}">
        ${zone.name}
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
        <div class="table-card ${table.status} ${isSelected ? 'selected' : ''}" data-table-id="${table.id}" tabindex="0" role="button">
          <div class="table-header-row">
            <span class="table-number">${table.number}</span>
            <span class="table-capacity">👤 ${table.seats || 4}</span>
          </div>
          <span class="table-status-tag ${table.status}">${getStatusLabel(table.status)}</span>
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
      <button type="button" class="zone-chip ${state.activeCategory === cat.id ? 'active' : ''}" data-cat-id="${cat.id}">
        ${cat.name}
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
      <div class="product-card" data-product-id="${prod.id}" role="button" tabindex="0">
        <div>
          <div class="product-name">${prod.name}</div>
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
          <div class="cart-item-name">${item.name}</div>
          <div style="font-size: 0.8rem; color: var(--text-muted);">${formatMoney(item.price)}</div>
          ${item.note ? `<div class="cart-item-note">Not: ${item.note}</div>` : ''}
        </div>
        <div class="quantity-stepper">
          <button type="button" class="btn-step" data-action="dec" data-id="${item.id}">−</button>
          <span style="font-weight: 800; min-width: 24px; text-align: center;">${item.quantity}</span>
          <button type="button" class="btn-step" data-action="inc" data-id="${item.id}">+</button>
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

  function formatMoney(amount) {
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(amount);
  }

  // Event Handlers
  function bindEvents() {
    // Refresh Tables Button
    if (el.btnRefreshTables) {
      el.btnRefreshTables.addEventListener('click', () => {
        loadTables();
      });
    }

    // Zone Selection
    if (el.zoneList) {
      el.zoneList.addEventListener('click', (e) => {
        const btn = e.target.closest('.zone-chip');
        if (!btn) return;
        state.activeZone = btn.dataset.zoneId;
        renderZones();
        renderTables();
      });
    }

    // Table Selection
    if (el.tablesGrid) {
      el.tablesGrid.addEventListener('click', (e) => {
        const card = e.target.closest('.table-card');
        if (!card) return;
        const tableId = card.dataset.tableId;
        state.selectedTable = state.tables.find(t => t.id === tableId);
        state.cart = [];
        renderTables();
        updateCartTotals();
      });
    }

    // Open Order Modal
    if (el.btnOpenOrderModal) {
      el.btnOpenOrderModal.addEventListener('click', () => {
        if (!state.selectedTable) return;
        el.modalTableTitle.textContent = `${state.selectedTable.number} Sipariş Ekle`;
        el.orderModal.style.display = 'flex';
        renderCart();
      });
    }

    // Close Order Modal
    if (el.btnCloseModal) {
      el.btnCloseModal.addEventListener('click', () => {
        el.orderModal.style.display = 'none';
      });
    }

    // Category Filter in Modal
    if (el.categoryFilterBar) {
      el.categoryFilterBar.addEventListener('click', (e) => {
        const btn = e.target.closest('.zone-chip');
        if (!btn) return;
        state.activeCategory = btn.dataset.catId;
        renderCategoryFilters();
        renderProducts(el.modalProductSearch?.value || '');
      });
    }

    // Product Search
    if (el.modalProductSearch) {
      el.modalProductSearch.addEventListener('input', (e) => {
        renderProducts(e.target.value);
      });
    }

    // Add Product to Cart
    if (el.productGrid) {
      el.productGrid.addEventListener('click', (e) => {
        const card = e.target.closest('.product-card');
        if (!card) return;
        const productId = card.dataset.productId;
        const product = state.products.find(p => p.id === productId);
        if (!product) return;

        const existing = state.cart.find(item => item.productId === productId);
        if (existing) {
          existing.quantity += 1;
        } else {
          state.cart.push({
            id: crypto.randomUUID(),
            productId: product.id,
            name: product.name,
            price: product.price,
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

        const orderPayload = {
          tableId: state.selectedTable.id,
          tableNumber: state.selectedTable.number,
          waiterName: state.currentUser?.name || 'Garson',
          items: state.cart.map(item => ({
            productId: item.productId,
            name: item.name,
            quantity: item.quantity,
            unitPrice: item.price,
            specialInstructions: item.note
          })),
          createdAt: new Date().toISOString()
        };

        if (state.isOnline) {
          const ok = await postOrderToBackend(orderPayload);
          if (ok) {
            // Authoritative server success
            state.selectedTable.status = 'occupied';
            state.selectedTable.amount += state.cart.reduce((sum, item) => sum + (item.price * item.quantity), 0);
            state.cart = [];
            el.orderModal.style.display = 'none';
            renderTables();
            updateCartTotals();
            alert(`Sipariş mutfağa iletildi! (${state.selectedTable.number})`);
          } else {
            // Server error: queue order and notify without claiming success
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
