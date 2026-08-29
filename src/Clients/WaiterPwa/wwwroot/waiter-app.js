// ALKAROS Waiter PWA Controller (V1-WTR-006)
(function () {
  'use strict';

  // State
  const state = {
    terminalId: '00000000-0000-0000-0000-000000000001',
    isOnline: navigator.onLine,
    currentUser: JSON.parse(localStorage.getItem('alkaros_waiter_user') || 'null') || {
      name: 'Garson Ahmet',
      role: 'Waiter',
      token: 'mock-session-token'
    },
    activeZone: 'all',
    selectedTable: null,
    cart: [],
    categories: [
      { id: 'cat-1', name: 'Ana Yemekler' },
      { id: 'cat-2', name: 'İçecekler' },
      { id: 'cat-3', name: 'Tatlılar' },
      { id: 'cat-4', name: 'Başlangıçlar' }
    ],
    activeCategory: 'all',
    products: [
      { id: 'p-1', categoryId: 'cat-1', name: 'Izgara Köfte', price: 280, modifiers: ['Az Pişmiş', 'Çok Pişmiş', 'Acılı'] },
      { id: 'p-2', categoryId: 'cat-1', name: 'Tavuk Şiş', price: 240, modifiers: ['Porsiyon', 'Dürüm'] },
      { id: 'p-3', categoryId: 'cat-1', name: 'Kuzu Pirzola', price: 420, modifiers: ['Az Pişmiş', 'Orta Pişmiş'] },
      { id: 'p-4', categoryId: 'cat-2', name: 'Ayran (Köy)', price: 40, modifiers: ['Açık', 'Kapalı'] },
      { id: 'p-5', categoryId: 'cat-2', name: 'Kola / Meşrubat', price: 55, modifiers: ['Zero', 'Normal', 'Buzsuz'] },
      { id: 'p-6', categoryId: 'cat-2', name: 'Türk Kahvesi', price: 50, modifiers: ['Sade', 'Orta', 'Şekerli'] },
      { id: 'p-7', categoryId: 'cat-3', name: 'Fıstıklı Baklava', price: 180, modifiers: ['Dondurmalı', 'Sade'] },
      { id: 'p-8', categoryId: 'cat-3', name: 'Künefe', price: 190, modifiers: ['Dondurmalı', 'Kaymaklı'] },
      { id: 'p-9', categoryId: 'cat-4', name: 'Günün Çorbası', price: 90, modifiers: ['Krutonlu', 'Acılı'] },
      { id: 'p-10', categoryId: 'cat-4', name: 'Mevsim Salata', price: 110, modifiers: ['Narekşili', 'Zeytinyağlı'] }
    ],
    zones: [
      { id: 'all', name: 'Tüm Masalar' },
      { id: 'zone-salon', name: 'Ana Salon' },
      { id: 'zone-bahce', name: 'Bahçe' },
      { id: 'zone-teras', name: 'Teras' }
    ],
    tables: [
      { id: 't-1', number: 'M-01', zoneId: 'zone-salon', status: 'available', seats: 4, amount: 0 },
      { id: 't-2', number: 'M-02', zoneId: 'zone-salon', status: 'occupied', seats: 2, amount: 560 },
      { id: 't-3', number: 'M-03', zoneId: 'zone-salon', status: 'occupied', seats: 6, amount: 1420 },
      { id: 't-4', number: 'M-04', zoneId: 'zone-salon', status: 'reserved', seats: 4, amount: 0 },
      { id: 't-5', number: 'B-01', zoneId: 'zone-bahce', status: 'available', seats: 4, amount: 0 },
      { id: 't-6', number: 'B-02', zoneId: 'zone-bahce', status: 'occupied', seats: 4, amount: 890 },
      { id: 't-7', number: 'B-03', zoneId: 'zone-bahce', status: 'cleaning', seats: 2, amount: 0 },
      { id: 't-8', number: 'T-01', zoneId: 'zone-teras', status: 'available', seats: 6, amount: 0 },
      { id: 't-9', number: 'T-02', zoneId: 'zone-teras', status: 'occupied', seats: 4, amount: 620 }
    ],
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
    btnOpenOrderModal: document.getElementById('btnOpenOrderModal')
  };

  // Initialization
  function init() {
    setupNetworkListeners();
    renderStatusRibbon();
    renderZones();
    renderTables();
    renderCategoryFilters();
    renderProducts();
    bindEvents();
    
    // Register Service Worker
    if ('serviceWorker' in navigator) {
      navigator.serviceWorker.register('./sw.js').catch(err => {
        console.warn('Service worker registration failed:', err);
      });
    }
  }

  // Network & Offline Queue
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
      el.statusText.textContent = 'Çevrimdışı • İşlemler Yerel Kuyruğa Alınıyor';
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
        await postOrderToBackend(item);
        state.offlineQueue = state.offlineQueue.filter(q => q.id !== item.id);
        localStorage.setItem('alkaros_waiter_offline_queue', JSON.stringify(state.offlineQueue));
      } catch (err) {
        console.warn('Queue item sync error, will retry:', err);
        break;
      }
    }
    renderStatusRibbon();
  }

  async function postOrderToBackend(orderPayload) {
    try {
      const response = await fetch(`/api/v1/terminals/${state.terminalId}/kitchen-operations/tickets`, {
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
            <span class="table-capacity">👤 ${table.seats}</span>
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
          <div style="font-size: 0.75rem; color: var(--text-dim); margin-top: 2px;">
            ${prod.modifiers ? prod.modifiers.join(', ') : ''}
          </div>
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
        state.cart = []; // Reset cart for newly selected table
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
          waiterName: state.currentUser.name,
          items: state.cart.map(item => ({
            productId: item.productId,
            name: item.name,
            quantity: item.quantity,
            unitPrice: item.price,
            note: item.note
          })),
          createdAt: new Date().toISOString()
        };

        if (state.isOnline) {
          const ok = await postOrderToBackend(orderPayload);
          if (!ok) {
            queueOrderAction(orderPayload);
          }
        } else {
          queueOrderAction(orderPayload);
        }

        // Update local table state
        state.selectedTable.status = 'occupied';
        state.selectedTable.amount += state.cart.reduce((sum, item) => sum + (item.price * item.quantity), 0);
        state.cart = [];
        
        el.orderModal.style.display = 'none';
        renderTables();
        updateCartTotals();

        alert(`Sipariş mutfağa iletildi! (${state.selectedTable.number})`);
      });
    }
  }

  // Start app on DOMContentLoaded
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
