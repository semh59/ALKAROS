// ALKAROS Cashier Quick POS Controller (V1-CUI-004)
(function () {
  'use strict';

  // State
  const state = {
    terminalId: '00000000-0000-0000-0000-000000000001',
    cashierName: 'Kasiyer Zeynep',
    registerNumber: 'KASA-01',
    openingFloat: 1000.00,
    shiftCashSales: 0.00,
    shiftTotalTickets: 0,
    activeCategory: 'all',
    
    // Active Ticket / Basket
    ticketItems: [],
    receivedCash: 0,
    parkedTickets: JSON.parse(localStorage.getItem('alkaros_cashier_parked') || '[]'),
    
    // Catalog
    categories: [
      { id: 'all', name: 'Tüm Ürünler' },
      { id: 'cat-fast', name: '⚡ Hızlı Satış' },
      { id: 'cat-drink', name: 'İçecekler' },
      { id: 'cat-food', name: 'Yiyecekler' },
      { id: 'cat-dessert', name: 'Tatlılar' }
    ],
    products: [
      { id: 'cp-1', categoryId: 'cat-fast', code: '101', name: 'Çay', price: 20 },
      { id: 'cp-2', categoryId: 'cat-fast', code: '102', name: 'Filtre Kahve', price: 65 },
      { id: 'cp-3', categoryId: 'cat-fast', code: '103', name: 'Su 0.5L', price: 15 },
      { id: 'cp-4', categoryId: 'cat-drink', code: '201', name: 'Ayran', price: 40 },
      { id: 'cp-5', categoryId: 'cat-drink', code: '202', name: 'Kola / Meşrubat', price: 55 },
      { id: 'cp-6', categoryId: 'cat-drink', code: '203', name: 'Taze Portakal Suyu', price: 85 },
      { id: 'cp-7', categoryId: 'cat-food', code: '301', name: 'Tost (Kaşarlı)', price: 110 },
      { id: 'cp-8', categoryId: 'cat-food', code: '302', name: 'Tost (Karışık)', price: 130 },
      { id: 'cp-9', categoryId: 'cat-food', code: '303', name: 'Günün Sandviçi', price: 140 },
      { id: 'cp-10', categoryId: 'cat-food', code: '304', name: 'Hamburger Menü', price: 260 },
      { id: 'cp-11', categoryId: 'cat-dessert', code: '401', name: 'Cheesecake', price: 140 },
      { id: 'cp-12', categoryId: 'cat-dessert', code: '402', name: 'Kruvasan', price: 95 }
    ]
  };

  // DOM Elements
  const el = {
    categoryTabs: document.getElementById('categoryTabs'),
    productMatrix: document.getElementById('productMatrix'),
    ticketItemsStream: document.getElementById('ticketItemsStream'),
    grandTotalAmount: document.getElementById('grandTotalAmount'),
    searchInput: document.getElementById('searchInput'),
    btnPayCash: document.getElementById('btnPayCash'),
    btnClearTicket: document.getElementById('btnClearTicket'),
    btnParkTicket: document.getElementById('btnParkTicket'),
    btnRecallTicket: document.getElementById('btnRecallTicket'),
    parkCountBadge: document.getElementById('parkCountBadge'),
    changeModal: document.getElementById('changeModal'),
    changeDueText: document.getElementById('changeDueText'),
    btnCloseChangeModal: document.getElementById('btnCloseChangeModal'),
    parkedModal: document.getElementById('parkedModal'),
    parkedList: document.getElementById('parkedList'),
    btnCloseParkedModal: document.getElementById('btnCloseParkedModal'),
    quickCashButtons: document.querySelectorAll('.btn-cash-chip')
  };

  function init() {
    renderCategoryTabs();
    renderProducts();
    renderTicket();
    updateParkBadge();
    bindEvents();
  }

  // Render Functions
  function renderCategoryTabs() {
    if (!el.categoryTabs) return;
    el.categoryTabs.innerHTML = state.categories.map(cat => `
      <button type="button" class="category-tab-btn ${state.activeCategory === cat.id ? 'active' : ''}" data-cat-id="${cat.id}">
        ${cat.name}
      </button>
    `).join('');
  }

  function renderProducts(searchQuery = '') {
    if (!el.productMatrix) return;
    const query = searchQuery.trim().toLowerCase();
    const filtered = state.products.filter(p => {
      const matchCat = state.activeCategory === 'all' || p.categoryId === state.activeCategory;
      const matchSearch = !query || p.name.toLowerCase().includes(query) || p.code.includes(query);
      return matchCat && matchSearch;
    });

    el.productMatrix.innerHTML = filtered.map(prod => `
      <div class="pos-product-card" data-product-id="${prod.id}" role="button" tabindex="0">
        <div>
          <span style="font-size: 0.7rem; color: var(--text-dim); font-family: var(--font-mono); font-weight: 700;">#${prod.code}</span>
          <div class="pos-product-name">${prod.name}</div>
        </div>
        <div class="pos-product-price">${formatMoney(prod.price)}</div>
      </div>
    `).join('');
  }

  function renderTicket() {
    if (!el.ticketItemsStream) return;
    if (state.ticketItems.length === 0) {
      el.ticketItemsStream.innerHTML = `
        <div style="text-align: center; color: var(--text-dim); padding: 40px 0;">
          <div style="font-size: 2rem; margin-bottom: 8px;">🧾</div>
          <div>Satış için ürün seçin veya barkod girin</div>
        </div>
      `;
      updateTotals();
      return;
    }

    el.ticketItemsStream.innerHTML = state.ticketItems.map(item => {
      const itemTotal = item.isComplimentary ? 0 : (item.price * item.quantity);
      return `
        <div class="ticket-item-row ${item.isComplimentary ? 'complimentary' : ''}">
          <div class="ticket-item-desc">
            <div class="ticket-item-name">${item.name}</div>
            <div class="ticket-item-qty">${item.quantity} x ${formatMoney(item.price)}</div>
          </div>
          <div class="ticket-item-total">${formatMoney(itemTotal)}</div>
          <div class="ticket-item-controls">
            <button type="button" class="btn-qty" data-action="dec" data-id="${item.id}">−</button>
            <button type="button" class="btn-qty" data-action="inc" data-id="${item.id}">+</button>
            <button type="button" class="btn-qty" data-action="ikram" data-id="${item.id}" title="İkram" style="font-size: 0.75rem;">🎁</button>
          </div>
        </div>
      `;
    }).join('');

    updateTotals();
  }

  function updateTotals() {
    const total = state.ticketItems.reduce((sum, item) => {
      return item.isComplimentary ? sum : sum + (item.price * item.quantity);
    }, 0);

    if (el.grandTotalAmount) {
      el.grandTotalAmount.textContent = formatMoney(total);
    }
  }

  function updateParkBadge() {
    if (!el.parkCountBadge) return;
    el.parkCountBadge.textContent = state.parkedTickets.length > 0 ? `(${state.parkedTickets.length})` : '';
  }

  function formatMoney(amount) {
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(amount);
  }

  // Actions
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

  function completeCashSale(receivedAmount) {
    const total = state.ticketItems.reduce((sum, item) => item.isComplimentary ? sum : sum + (item.price * item.quantity), 0);
    if (total <= 0) return;

    const actualReceived = receivedAmount >= total ? receivedAmount : total;
    const changeDue = actualReceived - total;

    state.shiftCashSales += total;
    state.shiftTotalTickets += 1;
    state.ticketItems = [];

    renderTicket();

    // Show Change Modal
    if (el.changeDueText && el.changeModal) {
      el.changeDueText.textContent = formatMoney(changeDue);
      el.changeModal.style.display = 'flex';
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

  // Event Listeners
  function bindEvents() {
    // Category Tabs
    if (el.categoryTabs) {
      el.categoryTabs.addEventListener('click', (e) => {
        const btn = e.target.closest('.category-tab-btn');
        if (!btn) return;
        state.activeCategory = btn.dataset.catId;
        renderCategoryTabs();
        renderProducts(el.searchInput?.value || '');
      });
    }

    // Search
    if (el.searchInput) {
      el.searchInput.addEventListener('input', (e) => {
        renderProducts(e.target.value);
      });
    }

    // Product Click
    if (el.productMatrix) {
      el.productMatrix.addEventListener('click', (e) => {
        const card = e.target.closest('.pos-product-card');
        if (!card) return;
        const prod = state.products.find(p => p.id === card.dataset.productId);
        if (prod) addProductToTicket(prod);
      });
    }

    // Ticket Steppers & Ikram
    if (el.ticketItemsStream) {
      el.ticketItemsStream.addEventListener('click', (e) => {
        const btn = e.target.closest('.btn-qty');
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
        } else if (action === 'ikram') {
          state.ticketItems[index].isComplimentary = !state.ticketItems[index].isComplimentary;
        }
        renderTicket();
      });
    }

    // Clear Ticket
    if (el.btnClearTicket) {
      el.btnClearTicket.addEventListener('click', () => {
        if (state.ticketItems.length === 0) return;
        if (confirm('Adisyon temizlensin mi?')) {
          state.ticketItems = [];
          renderTicket();
        }
      });
    }

    // Park Ticket
    if (el.btnParkTicket) {
      el.btnParkTicket.addEventListener('click', () => {
        parkCurrentTicket();
      });
    }

    // Recall Modal
    if (el.btnRecallTicket) {
      el.btnRecallTicket.addEventListener('click', () => {
        if (state.parkedTickets.length === 0) {
          alert('Bekleyen fiş bulunmuyor.');
          return;
        }
        if (el.parkedList) {
          el.parkedList.innerHTML = state.parkedTickets.map(pt => {
            const amount = pt.items.reduce((sum, item) => item.isComplimentary ? sum : sum + (item.price * item.quantity), 0);
            return `
              <div style="display: flex; align-items: center; justify-content: space-between; padding: 10px 0; border-bottom: 1px solid var(--border-color);">
                <div>
                  <div style="font-weight: 800;">Fiş (${pt.parkedAt})</div>
                  <div style="font-size: 0.8rem; color: var(--text-muted);">${pt.items.length} Kalem • ${formatMoney(amount)}</div>
                </div>
                <button type="button" class="btn-ticket-action" data-recall-id="${pt.id}" style="background: var(--accent-primary); color: white;">Aç</button>
              </div>
            `;
          }).join('');
        }
        if (el.parkedModal) el.parkedModal.style.display = 'flex';
      });
    }

    if (el.parkedList) {
      el.parkedList.addEventListener('click', (e) => {
        const btn = e.target.closest('[data-recall-id]');
        if (!btn) return;
        recallParkedTicket(btn.dataset.recallId);
      });
    }

    if (el.btnCloseParkedModal) {
      el.btnCloseParkedModal.addEventListener('click', () => {
        if (el.parkedModal) el.parkedModal.style.display = 'none';
      });
    }

    // Quick Cash Bill Chips (50, 100, 200, 500, Tam)
    el.quickCashButtons.forEach(btn => {
      btn.addEventListener('click', () => {
        const val = btn.dataset.cashValue;
        const total = state.ticketItems.reduce((sum, item) => item.isComplimentary ? sum : sum + (item.price * item.quantity), 0);
        if (total <= 0) return;

        if (val === 'exact') {
          completeCashSale(total);
        } else {
          completeCashSale(parseFloat(val));
        }
      });
    });

    // Pay Full Cash
    if (el.btnPayCash) {
      el.btnPayCash.addEventListener('click', () => {
        const total = state.ticketItems.reduce((sum, item) => item.isComplimentary ? sum : sum + (item.price * item.quantity), 0);
        if (total <= 0) return;
        completeCashSale(total);
      });
    }

    // Close Change Modal
    if (el.btnCloseChangeModal) {
      el.btnCloseChangeModal.addEventListener('click', () => {
        if (el.changeModal) el.changeModal.style.display = 'none';
      });
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
