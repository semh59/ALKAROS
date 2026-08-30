// ALKAROS Cashier Quick Order Controller (V1-CUI-005 / V1-RMD-051)
(function () {
  'use strict';

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
    cashierName: 'Kasiyer Zeynep',
    registerNumber: 'KASA-01',
    shiftTotalOrders: 0,
    activeCategory: 'all',

    // Active Ticket / Basket
    ticketItems: [],
    parkedTickets: JSON.parse(localStorage.getItem('alkaros_cashier_parked') || '[]'),

    // Catalog with standard Guid IDs
    categories: [
      { id: 'all', name: 'Tüm Ürünler' },
      { id: 'cat-fast', name: '⚡ Hızlı Satış' },
      { id: 'cat-drink', name: 'İçecekler' },
      { id: 'cat-food', name: 'Yiyecekler' },
      { id: 'cat-dessert', name: 'Tatlılar' }
    ],
    products: [
      { id: '00000000-0000-0000-0000-000000000101', categoryId: 'cat-fast', code: '101', name: 'Çay', price: 20 },
      { id: '00000000-0000-0000-0000-000000000102', categoryId: 'cat-fast', code: '102', name: 'Filtre Kahve', price: 65 },
      { id: '00000000-0000-0000-0000-000000000103', categoryId: 'cat-fast', code: '103', name: 'Su 0.5L', price: 15 },
      { id: '00000000-0000-0000-0000-000000000201', categoryId: 'cat-drink', code: '201', name: 'Ayran', price: 40 },
      { id: '00000000-0000-0000-0000-000000000202', categoryId: 'cat-drink', code: '202', name: 'Kola / Meşrubat', price: 55 },
      { id: '00000000-0000-0000-0000-000000000203', categoryId: 'cat-drink', code: '203', name: 'Taze Portakal Suyu', price: 85 },
      { id: '00000000-0000-0000-0000-000000000301', categoryId: 'cat-food', code: '301', name: 'Tost (Kaşarlı)', price: 110 },
      { id: '00000000-0000-0000-0000-000000000302', categoryId: 'cat-food', code: '302', name: 'Tost (Karışık)', price: 130 },
      { id: '00000000-0000-0000-0000-000000000303', categoryId: 'cat-food', code: '303', name: 'Günün Sandviçi', price: 140 },
      { id: '00000000-0000-0000-0000-000000000304', categoryId: 'cat-food', code: '304', name: 'Hamburger Menü', price: 260 },
      { id: '00000000-0000-0000-0000-000000000401', categoryId: 'cat-dessert', code: '401', name: 'Cheesecake', price: 140 },
      { id: '00000000-0000-0000-0000-000000000402', categoryId: 'cat-dessert', code: '402', name: 'Kruvasan', price: 95 }
    ]
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
    btnCloseParkedModal: document.getElementById('btnCloseParkedModal')
  };

  async function init() {
    renderCategoryTabs();
    renderProducts();
    renderTicket();
    updateParkBadge();
    bindEvents();

    // Try dynamic catalog loading
    await loadDynamicCatalog();
  }

  async function loadDynamicCatalog() {
    try {
      const [catRes, prodRes] = await Promise.all([
        fetch(`/api/v1/terminals/${state.terminalId}/catalog?category=all`, { credentials: 'include' }).catch(() => null),
        fetch(`/api/v1/terminals/${state.terminalId}/catalog`, { credentials: 'include' }).catch(() => null)
      ]);

      if (catRes && catRes.ok && prodRes && prodRes.ok) {
        const catData = await catRes.json();
        const prodData = await prodRes.json();
        const cats = Array.isArray(catData) ? catData : catData.categories || [];
        const prods = Array.isArray(prodData) ? prodData : prodData.products || [];

        if (cats.length > 0 && prods.length > 0) {
          state.categories = [{ id: 'all', name: 'Tüm Ürünler' }, ...cats.map(c => ({ id: c.categoryId || c.id, name: c.categoryName || c.name }))];
          state.products = prods.map((p, idx) => ({
            id: p.productId || p.id,
            categoryId: p.categoryId,
            code: String(100 + idx + 1),
            name: p.productName || p.name,
            price: p.currentPrice || p.price || 0
          }));
          renderCategoryTabs();
          renderProducts();
        }
      }
    } catch {
      // Gracefully use default fallback catalog
    }
  }

  function renderCategoryTabs() {
    if (!el.categoryTabs) return;
    el.categoryTabs.innerHTML = state.categories.map(c => `
      <button type="button" class="tab-chip ${state.activeCategory === c.id ? 'active' : ''}" data-cat-id="${escapeHtml(c.id)}">
        ${escapeHtml(c.name)}
      </button>
    `).join('');
  }

  function renderProducts(searchQuery = '') {
    if (!el.productMatrix) return;
    const query = searchQuery.trim().toLowerCase();
    const filtered = state.products.filter(p => {
      const matchCat = state.activeCategory === 'all' || p.categoryId === state.activeCategory;
      const matchSearch = !query || p.name.toLowerCase().includes(query) || (p.code && p.code.includes(query));
      return matchCat && matchSearch;
    });

    el.productMatrix.innerHTML = filtered.map(prod => `
      <div class="pos-product-card" data-product-id="${escapeHtml(prod.id)}" tabindex="0" role="button">
        <div class="product-badge">${escapeHtml(prod.code || '')}</div>
        <div class="product-name">${escapeHtml(prod.name)}</div>
        <div class="product-price">${formatMoney(prod.price)}</div>
      </div>
    `).join('');
  }

  function renderTicket() {
    if (!el.ticketItemsStream) return;
    if (state.ticketItems.length === 0) {
      el.ticketItemsStream.innerHTML = '<div style="color: var(--text-dim); text-align: center; padding: 32px 0;">Sepet boş. Ürün seçin.</div>';
      updateTotal();
      return;
    }

    el.ticketItemsStream.innerHTML = state.ticketItems.map(item => `
      <div class="ticket-row ${item.isComplimentary ? 'complimentary' : ''}">
        <div class="item-meta">
          <div class="item-title">${escapeHtml(item.name)} ${item.isComplimentary ? '<span class="badge-free">İKRAM</span>' : ''}</div>
          <div class="item-sub">${formatMoney(item.price)} × ${item.quantity} = ${formatMoney(item.isComplimentary ? 0 : item.price * item.quantity)}</div>
        </div>
        <div class="item-actions">
          <button type="button" class="btn-micro" data-action="dec" data-id="${escapeHtml(item.id)}">−</button>
          <span style="font-weight: 800; min-width: 20px; text-align: center;">${item.quantity}</span>
          <button type="button" class="btn-micro" data-action="inc" data-id="${escapeHtml(item.id)}">+</button>
          <button type="button" class="btn-micro" data-action="comp" data-id="${escapeHtml(item.id)}" title="İkram">🎁</button>
          <button type="button" class="btn-micro btn-del" data-action="del" data-id="${escapeHtml(item.id)}" title="Sil">✕</button>
        </div>
      </div>
    `).join('');

    updateTotal();
  }

  function updateTotal() {
    const total = state.ticketItems.reduce((sum, item) => item.isComplimentary ? sum : sum + (item.price * item.quantity), 0);
    if (el.grandTotalAmount) el.grandTotalAmount.textContent = formatMoney(total);
  }

  function updateParkBadge() {
    if (!el.parkCountBadge) return;
    el.parkCountBadge.textContent = state.parkedTickets.length;
    el.parkCountBadge.style.display = state.parkedTickets.length > 0 ? 'inline-block' : 'none';
  }

  function addProductToTicket(product) {
    const existing = state.ticketItems.find(i => i.productId === product.id && !i.isComplimentary);
    if (existing) {
      existing.quantity += 1;
    } else {
      state.ticketItems.push({
        id: crypto.randomUUID(),
        productId: product.id,
        name: product.name,
        price: product.price,
        quantity: 1,
        isComplimentary: false
      });
    }
    renderTicket();
  }

  async function dispatchOrderToKitchen() {
    if (state.ticketItems.length === 0) return;

    const orderPayload = {
      id: crypto.randomUUID(),
      tableId: '00000000-0000-0000-0000-000000000001',
      tableNumber: 'KASA-1',
      waiterName: state.cashierName || 'Kasiyer',
      items: state.ticketItems.map(item => ({
        productId: item.productId,
        name: item.name,
        productName: item.name,
        quantity: item.quantity,
        unitPrice: item.isComplimentary ? 0 : item.price,
        specialInstructions: item.isComplimentary ? 'İkram — kasiyer onaylı sıfır fiyat' : null
      }))
    };

    try {
      const response = await fetch(`/api/v1/terminals/${state.terminalId}/orders/table-draft`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Idempotency-Key': orderPayload.id
        },
        credentials: 'include',
        body: JSON.stringify(orderPayload)
      });

      if (response.ok) {
        const total = state.ticketItems.reduce((sum, item) => item.isComplimentary ? sum : sum + (item.price * item.quantity), 0);
        const itemCount = state.ticketItems.reduce((sum, item) => sum + item.quantity, 0);
        state.shiftTotalOrders += 1;
        state.ticketItems = [];
        renderTicket();
        alert(`Sipariş başarıyla sunucuya iletildi. (${itemCount} kalem, ${formatMoney(total)})`);
      } else {
        alert(`Sipariş sunucu tarafından reddedildi (Hata: ${response.status}). Lütfen tekrar deneyin.`);
      }
    } catch {
      alert('Sunucuya ulaşılamadı. Sipariş iletilemedi.');
    }
  }

  function parkCurrentTicket() {
    if (state.ticketItems.length === 0) return;
    state.parkedTickets.push({
      id: crypto.randomUUID(),
      parkedAt: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }),
      items: [...state.ticketItems]
    });
    localStorage.setItem('alkaros_cashier_parked', JSON.stringify(state.parkedTickets));
    state.ticketItems = [];
    renderTicket();
    updateParkBadge();
  }

  function recallParkedTicket(parkedId) {
    const index = state.parkedTickets.findIndex(p => p.id === parkedId);
    if (index < 0) return;
    state.ticketItems = state.parkedTickets[index].items;
    state.parkedTickets.splice(index, 1);
    localStorage.setItem('alkaros_cashier_parked', JSON.stringify(state.parkedTickets));
    renderTicket();
    updateParkBadge();
    if (el.parkedModal) el.parkedModal.style.display = 'none';
  }

  function renderParkedModal() {
    if (!el.parkedList) return;
    if (state.parkedTickets.length === 0) {
      el.parkedList.innerHTML = '<div style="text-align: center; color: var(--text-dim); padding: 24px;">Bekletilen fiş yok.</div>';
      return;
    }

    el.parkedList.innerHTML = state.parkedTickets.map(p => {
      const count = p.items.reduce((s, i) => s + i.quantity, 0);
      const total = p.items.reduce((s, i) => i.isComplimentary ? s : s + (i.price * i.quantity), 0);
      return `
        <div class="parked-card" data-parked-id="${escapeHtml(p.id)}">
          <div>
            <div style="font-weight: 700;">Bekletme Saati: ${escapeHtml(p.parkedAt)}</div>
            <div style="font-size: 0.85rem; color: var(--text-dim);">${count} Kalem • Toplam: ${formatMoney(total)}</div>
          </div>
          <button type="button" class="btn-micro" style="padding: 6px 12px; font-weight: 700;" data-action="recall" data-id="${escapeHtml(p.id)}">Geri Yükle</button>
        </div>
      `;
    }).join('');
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

    // Search Input
    if (el.searchInput) {
      el.searchInput.addEventListener('input', (e) => {
        renderProducts(e.target.value);
      });
    }

    // Product Click -> Add to Ticket
    if (el.productMatrix) {
      el.productMatrix.addEventListener('click', (e) => {
        const card = e.target.closest('.pos-product-card');
        if (!card) return;
        const prodId = card.dataset.productId;
        const prod = state.products.find(p => p.id === prodId);
        if (prod) addProductToTicket(prod);
      });
    }

    // Ticket Actions (Inc / Dec / Free / Del)
    if (el.ticketItemsStream) {
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
        } else if (action === 'comp') {
          state.ticketItems[index].isComplimentary = !state.ticketItems[index].isComplimentary;
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
        state.ticketItems = [];
        renderTicket();
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
        if (el.parkedModal) el.parkedModal.style.display = 'flex';
      });
    }

    // Close Parked Modal
    if (el.btnCloseParkedModal) {
      el.btnCloseParkedModal.addEventListener('click', () => {
        if (el.parkedModal) el.parkedModal.style.display = 'none';
      });
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
