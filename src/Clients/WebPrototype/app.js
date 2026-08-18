/**
 * ALKAROS V1 — Kurumsal Restoran Yönetimi, Salon/Masa Düzeni ve POS Motoru
 * Kapsamlı ve Eksiksiz Event Entegrasyonu (Tüm Eksiklikler Giderildi):
 * 1. Station Filters (İstasyon Filtreleri: Sıcak, Bar, Soğuk) Dinleyicileri Bağlandı
 * 2. Mutfak Fiş Sayacı (#badge-kitchen-count & #stat-kitchen-pending) Dinamik Senkronize Edildi
 * 3. Garson Arama (#input-wtr-search) ve Oturum Kilitleme (#btn-waiter-lock) Bağlandı
 * 4. Garson Kategori Çipleri (#wtr-cat-chips) ve Fiş Durumu Akışı (#wtr-status-feed) Eklendi
 * 5. Garson Sepeti (+ / - / Sil) Mobil Kontrolleri Entegre Edildi
 * 6. Gerçek Hayat Stres ve Kaos Testleri (Concurrency, 86'd, Split Bill, Printer Failover, Offline Sync)
 */

(function () {
  'use strict';

  // --- 1. DEFAULT DATA REPOSITORY ---

  const INITIAL_TABLES = [
    { id: 'tbl-1', number: 'S-01', section: 'Salon', occupancy: 'available', opBadge: null, capacity: 4, billAmount: 0.00, waiter: null, minutes: null, previousDrinks: [] },
    { id: 'tbl-2', number: 'S-02', section: 'Salon', occupancy: 'occupied', opBadge: 'cooking', capacity: 4, billAmount: 485.00, waiter: 'Mehmet K.', minutes: 35, previousDrinks: [{ name: 'Ayran 300ml', price: 30.00 }, { name: 'Coca Cola 330ml', price: 45.00 }] },
    { id: 'tbl-3', number: 'S-03', section: 'Salon', occupancy: 'occupied', opBadge: null, capacity: 6, billAmount: 1250.00, waiter: 'Can T.', minutes: 12, previousDrinks: [{ name: 'Ayran 300ml', price: 30.00 }] },
    { id: 'tbl-4', number: 'S-04', section: 'Salon', occupancy: 'occupied', opBadge: 'bill-requested', capacity: 4, billAmount: 820.00, waiter: 'Mehmet K.', minutes: 58, previousDrinks: [] },
    { id: 'tbl-5', number: 'S-05', section: 'Salon', occupancy: 'reserved', opBadge: null, capacity: 4, billAmount: 0.00, waiter: null, minutes: null, note: '19:30 - 4 Kişi' },
    { id: 'tbl-6', number: 'S-06', section: 'Salon', occupancy: 'occupied', opBadge: 'ready', capacity: 2, billAmount: 310.00, waiter: 'Mehmet K.', minutes: 22, previousDrinks: [{ name: 'Su 0.5L', price: 15.00 }] },
    { id: 'tbl-7', number: 'S-07', section: 'Salon', occupancy: 'available', opBadge: null, capacity: 2, billAmount: 0.00, waiter: null, minutes: null },
    { id: 'tbl-8', number: 'S-08', section: 'Salon', occupancy: 'available', opBadge: null, capacity: 6, billAmount: 0.00, waiter: null, minutes: null },
    { id: 'tbl-9', number: 'B-01', section: 'Bahçe', occupancy: 'occupied', opBadge: 'cooking', capacity: 4, billAmount: 290.00, waiter: 'Can T.', minutes: 18, previousDrinks: [{ name: 'Türk Kahvesi', price: 30.00 }] },
    { id: 'tbl-10', number: 'B-02', section: 'Bahçe', occupancy: 'available', opBadge: null, capacity: 4, billAmount: 0.00, waiter: null, minutes: null },
    { id: 'tbl-11', number: 'B-03', section: 'Bahçe', occupancy: 'available', opBadge: null, capacity: 6, billAmount: 0.00, waiter: null, minutes: null },
    { id: 'tbl-12', number: 'B-04', section: 'Bahçe', occupancy: 'occupied', opBadge: null, capacity: 2, billAmount: 175.00, waiter: 'Mehmet K.', minutes: 40, previousDrinks: [] },
    { id: 'tbl-13', number: 'T-01', section: 'Teras', occupancy: 'available', opBadge: null, capacity: 4, billAmount: 0.00, waiter: null, minutes: null },
    { id: 'tbl-14', number: 'T-02', section: 'Teras', occupancy: 'occupied', opBadge: 'bill-requested', capacity: 4, billAmount: 640.00, waiter: 'Can T.', minutes: 50, previousDrinks: [] }
  ];

  const INITIAL_PRODUCTS = [
    {
      id: 'p1', name: 'Alkaros Burger (200g)', category: 'Burgerler', defaultCourse: 'Ana Yemek', price: 240.00, station: 'hot', allergen: 'Gluten, Süt', is86: false,
      modifierGroups: [
        { id: 'mg_done', title: 'Pişme Derecesi', required: true, type: 'single', options: [{ name: 'Az Pişmiş', price: 0 }, { name: 'Orta Pişmiş', price: 0, default: true }, { name: 'Çok Pişmiş', price: 0 }] },
        { id: 'mg_ext', title: 'Ekstra Malzemeler', required: false, type: 'multi', options: [{ name: 'Ekstra Cheddar', price: 30.00 }, { name: 'Karamelize Soğan', price: 20.00 }, { name: 'Duble Köfte (+150g)', price: 90.00 }] }
      ]
    },
    {
      id: 'p2', name: 'Cheese Burger', category: 'Burgerler', defaultCourse: 'Ana Yemek', price: 220.00, station: 'hot', allergen: 'Gluten, Süt', is86: false,
      modifierGroups: [
        { id: 'mg_done', title: 'Pişme Derecesi', required: true, type: 'single', options: [{ name: 'Orta Pişmiş', price: 0, default: true }, { name: 'Çok Pişmiş', price: 0 }] },
        { id: 'mg_ext', title: 'Ekstralar', required: false, type: 'multi', options: [{ name: 'Füme Kaburga', price: 45.00 }, { name: 'Jalapeno Biber', price: 15.00 }] }
      ]
    },
    {
      id: 'p3', name: 'Bonfile Izgara (250g)', category: 'Ana Yemek', defaultCourse: 'Ana Yemek', price: 450.00, station: 'hot', allergen: 'Gluten-Free', is86: false,
      modifierGroups: [
        { id: 'mg_done', title: 'Et Pişme', required: true, type: 'single', options: [{ name: 'Az Pişmiş', price: 0 }, { name: 'Orta (Medium)', price: 0, default: true }, { name: 'İyi Pişmiş', price: 0 }] },
        { id: 'mg_sauce', title: 'Şef Sosu', required: true, type: 'single', options: [{ name: 'Trüflü Mantar Sosu', price: 40.00, default: true }, { name: 'Taze Karabiber Sosu', price: 35.00 }] }
      ]
    },
    {
      id: 'p4', name: 'Köfte Porsiyon (Izgara)', category: 'Ana Yemek', defaultCourse: 'Ana Yemek', price: 280.00, station: 'hot', allergen: 'Gluten', is86: false,
      modifierGroups: [
        { id: 'mg_garnish', title: 'Garnitür', required: false, type: 'multi', options: [{ name: 'Bol Köz Biber & Domates', price: 15.00 }, { name: 'Sumaklı Soğan', price: 10.00 }] }
      ]
    },
    {
      id: 'p5', name: 'Patates Tava', category: 'Başlangıçlar', defaultCourse: 'Başlangıç', price: 85.00, station: 'hot', allergen: 'Vegan', is86: false,
      modifierGroups: [
        { id: 'mg_dip', title: 'Yan Sos', required: false, type: 'multi', options: [{ name: 'Trüflü Mayonez', price: 20.00 }, { name: 'Cajun Baharatı', price: 10.00 }] }
      ]
    },
    {
      id: 'p6', name: 'Coca Cola 330ml', category: 'İçecekler', defaultCourse: 'Başlangıç', price: 45.00, station: 'bar', allergen: 'Vegan', is86: false,
      modifierGroups: [{ id: 'mg_ice', title: 'Buz / Limon', required: false, type: 'multi', options: [{ name: 'Buzsuz', price: 0 }, { name: 'Limon Dilimli', price: 0 }] }]
    },
    {
      id: 'p7', name: 'Ayran 300ml', category: 'İçecekler', defaultCourse: 'Başlangıç', price: 30.00, station: 'bar', allergen: 'Süt', is86: false,
      modifierGroups: []
    },
    {
      id: 'p8', name: 'Su 0.5L', category: 'İçecekler', defaultCourse: 'Başlangıç', price: 15.00, station: 'bar', allergen: 'Vegan', is86: false,
      modifierGroups: []
    },
    {
      id: 'p9', name: 'Çikolatalı Sufle', category: 'Tatlılar', defaultCourse: 'Tatlı', price: 120.00, station: 'cold', allergen: 'Yumurta, Süt', is86: false,
      modifierGroups: [{ id: 'mg_icecream', title: 'Dondurma', required: false, type: 'single', options: [{ name: 'Vanilyalı Dondurma Ekle', price: 35.00 }, { name: 'Sade', price: 0, default: true }] }]
    },
    {
      id: 'p10', name: 'Fırın Sütlaç', category: 'Tatlılar', defaultCourse: 'Tatlı', price: 95.00, station: 'cold', allergen: 'Süt', is86: false,
      modifierGroups: [{ id: 'mg_nut', title: 'Fındık İsteği', required: false, type: 'single', options: [{ name: 'Kavrulmuş Fındıklı', price: 15.00, default: true }, { name: 'Sade', price: 0 }] }]
    }
  ];

  const INITIAL_TICKETS = [
    { id: '1042', table: 'Masa S-02', time: '12 dk önce', station: 'hot', items: [{ name: '1x Alkaros Burger', status: 'cooking' }, { name: '1x Patates Tava', status: 'ready' }] },
    { id: '1043', table: 'Masa B-01', time: '5 dk önce', station: 'bar', items: [{ name: '2x İçecek', status: 'pending' }] }
  ];

  const INITIAL_PRINTERS = [
    { id: 'prn-1', name: 'Mutfak Sıcak Yazıcısı', ip: '192.168.1.200', status: 'online', queueCount: 0 },
    { id: 'prn-2', name: 'İçecek / Bar Yazıcısı', ip: '192.168.1.201', status: 'paper_out', queueCount: 2, issue: 'Kağıt Bitti / Beklemede' },
    { id: 'prn-3', name: 'Mutfak Soğuk/Tatlı Yazıcısı', ip: '192.168.1.202', status: 'online', queueCount: 0 }
  ];

  const INITIAL_NOTIFICATIONS = [
    { id: 'notif-1', time: '14:28', text: 'Masa S-06: 1x Köfte Porsiyon HAZIR — Servis Bekliyor!', unread: true }
  ];

  // --- 2. APPLICATION STATE ---

  const state = {
    theme: localStorage.getItem('alkaros_theme') || 'light',
    currentView: 'cashier',
    isOnline: true,
    isLocked: false,

    // Tables & Floor
    tables: [...INITIAL_TABLES],
    selectedTable: null,
    activeSectionFilter: 'Tümü',
    activeStatusFilter: 'all',
    searchTableQuery: '',

    // Catalog & Menu Management
    products: [...INITIAL_PRODUCTS],
    activeCategory: 'Tümü',
    searchProductQuery: '',
    menuMgmtCatFilter: 'Tümü',

    // Order Entry
    activeSeat: 'shared',
    activeCart: [],
    activeDiscount: 0,
    activeModifierProduct: null,
    editingCartIndex: null,
    selectedQuickTags: [],
    selectedCartItemIndex: null,

    // Operations & Printers
    activeStationFilter: 'all',
    tickets: [...INITIAL_TICKETS],
    printers: [...INITIAL_PRINTERS],
    auditLogs: [
      '[14:28:10] Sistem: Bar Yazıcısı kağıt sonu algılandı. Fiş #1043 beklemede.',
      '[14:29:00] Kasiyer Ahmet Y.: Fiş #1043 Sıcak Yazıcısına yönlendirildi.'
    ],

    // Split Bill State
    splitActiveSeat: '1',

    // Waiter PWA
    wtrSectionFilter: 'Tümü',
    wtrCatFilter: 'Tümü',
    wtrSearchQuery: '',
    wtrActiveTable: null,
    wtrCart: [],
    wtrOfflineQueue: [],
    notifications: [...INITIAL_NOTIFICATIONS],

    // PIN Lockout
    enteredPin: '',
    failedPinAttempts: 0,
    cooldownRemaining: 0
  };

  // --- 3. HELPERS ---

  const formatTL = (val) => {
    return Number(val || 0).toLocaleString('tr-TR', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }) + ' TL';
  };

  const showToast = (message, type = 'success') => {
    const container = document.getElementById('toast-container');
    if (!container) return;
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.innerHTML = `<span>${message}</span>`;
    container.appendChild(toast);
    setTimeout(() => {
      toast.style.animation = 'slideInRight 200ms ease reverse';
      setTimeout(() => toast.remove(), 200);
    }, 3200);
  };

  const getDistinctSections = () => {
    const set = new Set(['Tümü']);
    state.tables.forEach(t => set.add(t.section));
    return Array.from(set);
  };

  const getDistinctCategories = () => {
    const set = new Set(['Tümü']);
    state.products.forEach(p => set.add(p.category));
    return Array.from(set);
  };

  // --- 4. RENDERERS ---

  // 4.1 Floor Sections & Cashier Tables
  function renderFloorSections() {
    const container = document.getElementById('floor-section-chips');
    const wtrContainer = document.getElementById('wtr-section-chips');
    const sections = getDistinctSections();

    if (container) {
      container.innerHTML = sections.map(sec => {
        const count = sec === 'Tümü' ? state.tables.length : state.tables.filter(t => t.section === sec).length;
        const isActive = state.activeSectionFilter === sec;
        return `
          <button type="button" class="chip ${isActive ? 'active' : ''}" data-section="${sec}">
            ${sec} <span class="chip-count">(${count})</span>
          </button>
        `;
      }).join('');

      container.querySelectorAll('.chip').forEach(chip => {
        chip.addEventListener('click', () => {
          container.querySelectorAll('.chip').forEach(c => c.classList.remove('active'));
          chip.classList.add('active');
          state.activeSectionFilter = chip.dataset.section;
          renderCashierTables();
        });
      });
    }

    if (wtrContainer) {
      wtrContainer.innerHTML = sections.map(sec => {
        const isActive = state.wtrSectionFilter === sec;
        return `
          <button type="button" class="wtr-chip ${isActive ? 'active' : ''}" data-wtr-section="${sec}">${sec}</button>
        `;
      }).join('');

      wtrContainer.querySelectorAll('.wtr-chip').forEach(chip => {
        chip.addEventListener('click', () => {
          wtrContainer.querySelectorAll('.wtr-chip').forEach(c => c.classList.remove('active'));
          chip.classList.add('active');
          state.wtrSectionFilter = chip.dataset.wtrSection;
          renderWaiterSurface();
        });
      });
    }
  }

  function renderCashierTables() {
    const grid = document.getElementById('cashier-table-grid');
    const emptyState = document.getElementById('cashier-empty-search');
    if (!grid) return;

    let filtered = state.tables.filter(t => {
      const matchSection = state.activeSectionFilter === 'Tümü' || t.section === state.activeSectionFilter;
      const matchStatus = state.activeStatusFilter === 'all' || 
        (state.activeStatusFilter === 'bill-requested' && t.opBadge === 'bill-requested') ||
        (state.activeStatusFilter === 'cooking' && t.opBadge === 'cooking');
      const matchSearch = !state.searchTableQuery || 
        t.number.toLowerCase().includes(state.searchTableQuery.toLowerCase()) ||
        (t.waiter && t.waiter.toLowerCase().includes(state.searchTableQuery.toLowerCase()));
      return matchSection && matchStatus && matchSearch;
    });

    if (filtered.length === 0) {
      grid.style.display = 'none';
      if (emptyState) emptyState.style.display = 'flex';
    } else {
      grid.style.display = 'grid';
      if (emptyState) emptyState.style.display = 'none';
    }

    grid.innerHTML = filtered.map(t => {
      let occupClass = t.occupancy === 'occupied' ? 'occupied' : t.occupancy === 'reserved' ? 'reserved' : 'available';
      let occupText = t.occupancy === 'occupied' ? 'Dolu' : t.occupancy === 'reserved' ? 'Rezerve' : 'Boş';

      let actionBadgeHtml = '';
      if (t.occupancy === 'occupied' && t.opBadge) {
        if (t.opBadge === 'bill-requested') {
          actionBadgeHtml = `<div class="table-action-badge bill-requested"><svg class="icon" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg> Hesap İstendi</div>`;
        } else if (t.opBadge === 'cooking') {
          actionBadgeHtml = `<div class="table-action-badge kitchen-cooking"><svg class="icon" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg> Hazırlanıyor</div>`;
        } else if (t.opBadge === 'ready') {
          actionBadgeHtml = `<div class="table-action-badge kitchen-ready"><svg class="icon" viewBox="0 0 24 24"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg> Servise Hazır</div>`;
        }
      }

      let timerBarHtml = '';
      if (t.occupancy === 'occupied' && t.minutes) {
        const pct = Math.min(100, (t.minutes / 60) * 100);
        let barClass = 'table-elapsed-fill';
        if (t.minutes >= 45) barClass += ' alert';
        else if (t.minutes >= 25) barClass += ' warning';
        timerBarHtml = `
          <div class="table-elapsed-bar" title="${t.minutes} dakikadır açık">
            <div class="${barClass}" style="width: ${pct}%"></div>
          </div>
        `;
      }

      const btnLabel = t.occupancy === 'available' 
        ? 'Sipariş Aç' 
        : t.occupancy === 'reserved' 
          ? 'Misafiri Oturt >' 
          : 'Masayı Aç >';

      return `
        <div class="table-card" data-table-id="${t.id}">
          <div class="table-card-top">
            <span class="table-number">Masa ${t.number}</span>
            <span class="occupancy-pill ${occupClass}">${occupText}</span>
          </div>
          ${timerBarHtml}
          <div class="table-card-body">
            ${t.occupancy === 'available' 
              ? `<div class="meta-row"><svg class="icon" style="width:14px;height:14px" viewBox="0 0 24 24"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/></svg> Kapasite: ${t.capacity} Kişi (${t.section})</div>` 
              : t.occupancy === 'reserved'
                ? `<div class="meta-row"><strong style="color:var(--badge-reserv-text)">${t.note || '19:30 - 4 Kişi'}</strong></div>`
                : `<div class="meta-row"><svg class="icon" style="width:14px;height:14px" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg> ${t.minutes} dk • Garson: ${t.waiter || 'Mehmet K.'}</div>
                   <div class="table-amount num-val">${formatTL(t.billAmount)}</div>`
            }
            ${actionBadgeHtml}
          </div>
          <button type="button" class="table-card-btn">${btnLabel}</button>
        </div>
      `;
    }).join('');

    const openBillsCount = state.tables.filter(t => t.occupancy === 'occupied').length;
    const statTotal = document.getElementById('stat-total-tables');
    const statOpen = document.getElementById('stat-open-bills');
    const statPending = document.getElementById('stat-kitchen-pending');
    const badgeKitchen = document.getElementById('badge-kitchen-count');

    if (statTotal) statTotal.textContent = `${state.tables.length} Masa`;
    if (statOpen) statOpen.textContent = `${openBillsCount} Masa`;
    if (statPending) statPending.textContent = `${state.tickets.length} Fiş`;
    if (badgeKitchen) badgeKitchen.textContent = state.tickets.length;

    const countBillReq = state.tables.filter(t => t.opBadge === 'bill-requested').length;
    const countCooking = state.tables.filter(t => t.opBadge === 'cooking').length;
    const elBillReq = document.getElementById('count-bill-req');
    const elCooking = document.getElementById('count-cooking');
    if (elBillReq) elBillReq.textContent = countBillReq;
    if (elCooking) elCooking.textContent = countCooking;
  }

  // 4.2 POS Catalog Navigation & Products
  function renderPOSCatalog() {
    const tabsContainer = document.getElementById('pos-category-tabs');
    const categories = getDistinctCategories();

    if (tabsContainer) {
      tabsContainer.innerHTML = categories.map(cat => `
        <button type="button" class="cat-tab ${state.activeCategory === cat ? 'active' : ''}" data-category="${cat}">${cat}</button>
      `).join('');

      tabsContainer.querySelectorAll('.cat-tab').forEach(tab => {
        tab.addEventListener('click', () => {
          tabsContainer.querySelectorAll('.cat-tab').forEach(t => t.classList.remove('active'));
          tab.classList.add('active');
          state.activeCategory = tab.dataset.category;
          renderPOSProducts();
        });
      });
    }

    renderPOSProducts();
  }

  function renderPOSProducts() {
    const grid = document.getElementById('pos-product-grid');
    if (!grid) return;

    let filtered = state.products.filter(p => {
      const matchCat = state.activeCategory === 'Tümü' || p.category === state.activeCategory;
      const matchSearch = !state.searchProductQuery || p.name.toLowerCase().includes(state.searchProductQuery.toLowerCase());
      return matchCat && matchSearch;
    });

    grid.innerHTML = filtered.map(p => {
      let allergenPill = '';
      if (p.allergen) {
        allergenPill = `<span class="allergen-tag" style="font-size:10px;font-weight:600;padding:2px 6px;border-radius:4px;background:var(--color-surface-active);color:var(--color-text-muted)">${p.allergen}</span>`;
      }

      return `
        <div class="product-card ${p.is86 ? 'is-86' : ''}" data-prod-id="${p.id}">
          <div class="prod-name">${p.name}</div>
          <div style="display:flex;justify-content:space-between;align-items:center;margin-top:6px">
            <div class="prod-price num-val">${formatTL(p.price)}</div>
            ${allergenPill}
          </div>
        </div>
      `;
    }).join('');
  }

  // 4.3 Menu Management Table
  function renderMenuManagement() {
    const chipsContainer = document.getElementById('menu-category-chips');
    const tbody = document.getElementById('menu-mgmt-tbody');
    const categories = getDistinctCategories();

    if (chipsContainer) {
      chipsContainer.innerHTML = categories.map(cat => `
        <button type="button" class="chip ${state.menuMgmtCatFilter === cat ? 'active' : ''}" data-menu-cat="${cat}">${cat}</button>
      `).join('');

      chipsContainer.querySelectorAll('.chip').forEach(chip => {
        chip.addEventListener('click', () => {
          chipsContainer.querySelectorAll('.chip').forEach(c => c.classList.remove('active'));
          chip.classList.add('active');
          state.menuMgmtCatFilter = chip.dataset.menuCat;
          renderMenuManagement();
        });
      });
    }

    if (tbody) {
      let filtered = state.products.filter(p => {
        return state.menuMgmtCatFilter === 'Tümü' || p.category === state.menuMgmtCatFilter;
      });

      tbody.innerHTML = filtered.map((p, idx) => `
        <tr class="${p.is86 ? 'row-86' : ''}">
          <td><strong>${p.name}</strong> ${p.is86 ? '<span style="color:#DC2626;font-size:11px;font-weight:700">[86\'d TÜKENDİ]</span>' : ''}</td>
          <td><span class="occupancy-pill available">${p.category}</span></td>
          <td class="num-val"><strong>${formatTL(p.price)}</strong></td>
          <td>${p.station === 'hot' ? 'Sıcak Mutfak' : p.station === 'bar' ? 'Bar & İçecek' : 'Soğuk / Tatlı'}</td>
          <td>${p.defaultCourse}</td>
          <td>${p.allergen ? `<span class="allergen-tag" style="font-size:11px;font-weight:600;padding:2px 6px;border-radius:4px;background:var(--color-surface-active)">${p.allergen}</span>` : '—'}</td>
          <td style="display:flex;gap:6px">
            <button type="button" class="cart-item-actions-btn btn-toggle-86" data-prod-id="${p.id}">${p.is86 ? 'Satışa Aç' : '86\'d (Tükendi)'}</button>
            <button type="button" class="cart-item-actions-btn btn-delete-product" data-prod-idx="${idx}" style="color:var(--color-danger)">Sil</button>
          </td>
        </tr>
      `).join('');

      tbody.querySelectorAll('.btn-delete-product').forEach(btn => {
        btn.addEventListener('click', () => {
          const idx = parseInt(btn.dataset.prodIdx, 10);
          const deleted = state.products.splice(idx, 1)[0];
          showToast(`${deleted.name} menüden kaldırıldı.`, 'warning');
          renderMenuManagement();
          renderPOSCatalog();
        });
      });

      tbody.querySelectorAll('.btn-toggle-86').forEach(btn => {
        btn.addEventListener('click', () => {
          const p = state.products.find(item => item.id === btn.dataset.prodId);
          if (p) {
            p.is86 = !p.is86;
            showToast(`${p.name} durumu: ${p.is86 ? '86\'d (Tükendi)' : 'Tekrar Satışta'}`, p.is86 ? 'warning' : 'success');
            renderMenuManagement();
            renderPOSCatalog();
            renderWaiterSurface();
          }
        });
      });
    }
  }

  // 4.4 Cart Draft Grouped by Coursing
  function renderCart() {
    const list = document.getElementById('pos-cart-items');
    const badge = document.getElementById('cart-item-count');
    const subtotalEl = document.getElementById('cart-subtotal');
    const discountRow = document.getElementById('row-discount');
    const discountEl = document.getElementById('cart-discount');
    const taxEl = document.getElementById('cart-tax');
    const grandTotalEl = document.getElementById('cart-grand-total');
    const submitBtn = document.getElementById('btn-pos-submit-order');

    if (!list) return;

    if (state.activeCart.length === 0) {
      list.innerHTML = `
        <div class="cart-empty-placeholder">
          <p>Henüz ürün seçilmedi.</p>
          <span>Soldaki menüden ürün ekleyiniz.</span>
        </div>
      `;
      if (badge) badge.textContent = '0 Kalem';
      if (subtotalEl) subtotalEl.textContent = '0,00 TL';
      if (discountRow) discountRow.style.display = 'none';
      if (taxEl) taxEl.textContent = '0,00 TL';
      if (grandTotalEl) grandTotalEl.textContent = '0,00 TL';
      if (submitBtn) submitBtn.disabled = true;
      return;
    }

    const courses = [
      { key: 'Başlangıç', title: '1. AŞAMA: BAŞLANGIÇ & İÇECEK', cssClass: 'starter' },
      { key: 'Ana Yemek', title: '2. AŞAMA: ANA YEMEKLER', cssClass: 'main' },
      { key: 'Tatlı', title: '3. AŞAMA: TATLI & KAHVE', cssClass: 'dessert' }
    ];

    let subtotal = 0;
    let fullHtml = '';

    courses.forEach(course => {
      const itemsInCourse = state.activeCart
        .map((item, originalIndex) => ({ item, originalIndex }))
        .filter(({ item }) => item.course === course.key);

      if (itemsInCourse.length > 0) {
        fullHtml += `
          <div class="course-group">
            <div class="course-header ${course.cssClass}">
              <span>${course.title}</span>
              <span style="font-size:11px;font-weight:600">${itemsInCourse.length} Kalem</span>
            </div>
        `;

        itemsInCourse.forEach(({ item, originalIndex }) => {
          const itemTotal = item.unitPrice * item.quantity;
          subtotal += itemTotal;

          const modDetails = [];
          if (item.selectedOptions && item.selectedOptions.length) {
            item.selectedOptions.forEach(opt => modDetails.push(`${opt.group}: ${opt.name}`));
          }
          if (item.quickTags && item.quickTags.length) modDetails.push(`Etiket: ${item.quickTags.join(', ')}`);
          if (item.note) modDetails.push(`Not: "${item.note}"`);

          const seatLabel = item.seat === 'shared' ? 'Ortaya' : `Koltuk ${item.seat}`;

          fullHtml += `
            <div class="cart-item-row" data-cart-index="${originalIndex}">
              <div class="cart-item-main btn-edit-cart-item" data-index="${originalIndex}" title="Düzenlemek için tıklayın">
                <div>
                  <span class="item-seat-badge">${seatLabel}</span>
                  <span class="cart-item-title">${item.isComplimentary ? '<strong style="color:#059669">[İkram] </strong>' : ''}${item.name}</span>
                </div>
                <span class="cart-item-price num-val">${formatTL(itemTotal)}</span>
              </div>
              ${modDetails.length ? `<div class="cart-item-modifiers btn-edit-cart-item" data-index="${originalIndex}">${modDetails.join('<br>')}</div>` : ''}
              <div class="cart-item-controls">
                <div class="qty-control">
                  <button type="button" class="btn-qty btn-qty-dec" data-index="${originalIndex}">-</button>
                  <span class="qty-num num-val">${item.quantity}</span>
                  <button type="button" class="btn-qty btn-qty-inc" data-index="${originalIndex}">+</button>
                </div>
                <div style="display:flex;gap:6px">
                  <button type="button" class="cart-item-actions-btn btn-item-manage" data-index="${originalIndex}">İkram / İptal</button>
                  <button type="button" class="btn-clear-cart btn-cart-remove" data-index="${originalIndex}">
                    <svg class="icon" viewBox="0 0 24 24"><path d="M3 6h18"/><path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"/></svg>
                  </button>
                </div>
              </div>
            </div>
          `;
        });

        fullHtml += `</div>`;
      }
    });

    list.innerHTML = fullHtml;

    let discountAmount = 0;
    if (state.activeDiscount > 0) {
      if (state.activeDiscount < 1) {
        discountAmount = subtotal * state.activeDiscount;
      } else {
        discountAmount = state.activeDiscount;
      }
      if (discountRow) {
        discountRow.style.display = 'flex';
        discountEl.textContent = `-${formatTL(discountAmount)}`;
      }
    } else {
      if (discountRow) discountRow.style.display = 'none';
    }

    const discountedSubtotal = Math.max(0, subtotal - discountAmount);
    const tax = discountedSubtotal * 0.10;
    const total = discountedSubtotal;

    if (badge) badge.textContent = `${state.activeCart.length} Kalem`;
    if (subtotalEl) subtotalEl.textContent = formatTL(subtotal);
    if (taxEl) taxEl.textContent = formatTL(tax);
    if (grandTotalEl) grandTotalEl.textContent = formatTL(total);
    if (submitBtn) submitBtn.disabled = !state.isOnline;
  }

  // 4.5 Dynamic Universal Modifier Modal
  function openDynamicModifierModal(prod, existingItem = null) {
    if (prod.is86) {
      showToast(`DİKKAT: ${prod.name} mutfakta tükendi (86'd)! Sipariş alınamaz.`, 'error');
      return;
    }

    state.activeModifierProduct = prod;
    state.selectedQuickTags = existingItem ? [...(existingItem.quickTags || [])] : [];

    const modal = document.getElementById('modal-modifier');
    const titleEl = document.getElementById('mod-title');
    const priceEl = document.getElementById('mod-price');
    const container = document.getElementById('mod-dynamic-options-container');

    if (titleEl) titleEl.textContent = existingItem ? `${prod.name} (Düzenle)` : prod.name;
    if (priceEl) priceEl.textContent = formatTL(prod.price);

    let bodyHtml = '';

    // 1. Coursing Selector
    bodyHtml += `
      <div class="option-group">
        <label class="group-label">Servis Aşaması</label>
        <div class="radio-pill-group">
          <label class="radio-pill"><input type="radio" name="courseType" value="Başlangıç" ${existingItem ? (existingItem.course === 'Başlangıç' ? 'checked' : '') : (prod.defaultCourse === 'Başlangıç' ? 'checked' : '')}><span>Başlangıç</span></label>
          <label class="radio-pill"><input type="radio" name="courseType" value="Ana Yemek" ${existingItem ? (existingItem.course === 'Ana Yemek' ? 'checked' : '') : (prod.defaultCourse === 'Ana Yemek' ? 'checked' : '')}><span>Ana Yemek</span></label>
          <label class="radio-pill"><input type="radio" name="courseType" value="Tatlı" ${existingItem ? (existingItem.course === 'Tatlı' ? 'checked' : '') : (prod.defaultCourse === 'Tatlı' ? 'checked' : '')}><span>Tatlı & Kahve</span></label>
        </div>
      </div>
    `;

    // 2. Modifier Groups
    const groups = prod.modifierGroups || [];
    groups.forEach(g => {
      bodyHtml += `
        <div class="option-group">
          <label class="group-label">${g.title} ${g.required ? '<span style="color:#DC2626;font-size:10px">(Zorunlu)</span>' : ''}</label>
      `;

      if (g.type === 'single') {
        bodyHtml += `
          <div class="radio-pill-group">
            ${g.options.map(opt => {
              const isChecked = existingItem 
                ? existingItem.selectedOptions?.some(o => o.group === g.title && o.name === opt.name)
                : opt.default;
              return `
                <label class="radio-pill">
                  <input type="radio" name="group_${g.id}" value="${opt.name}" data-group-title="${g.title}" data-extra-price="${opt.price}" ${isChecked ? 'checked' : ''}>
                  <span>${opt.name} ${opt.price > 0 ? '(+' + formatTL(opt.price) + ')' : ''}</span>
                </label>
              `;
            }).join('')}
          </div>
        `;
      } else {
        bodyHtml += `
          <div class="checkbox-list">
            ${g.options.map(opt => {
              const isChecked = existingItem 
                ? existingItem.selectedOptions?.some(o => o.group === g.title && o.name === opt.name)
                : false;
              return `
                <label class="check-row">
                  <input type="checkbox" name="group_${g.id}" value="${opt.name}" data-group-title="${g.title}" data-extra-price="${opt.price}" ${isChecked ? 'checked' : ''}>
                  <span class="check-text">${opt.name}</span>
                  <span class="check-price">${opt.price > 0 ? '+' + formatTL(opt.price) : 'Ücretsiz'}</span>
                </label>
              `;
            }).join('')}
          </div>
        `;
      }
      bodyHtml += `</div>`;
    });

    // 3. Quick Tags & Notes
    bodyHtml += `
      <div class="option-group">
        <label class="group-label">Hızlı Hazırlık Etiketleri</label>
        <div class="radio-pill-group">
          <button type="button" class="quick-tag-btn" data-tag="Sos Ayrı">Sos Ayrı</button>
          <button type="button" class="quick-tag-btn" data-tag="Buzsuz">Buzsuz</button>
          <button type="button" class="quick-tag-btn" data-tag="Tuzsuz">Tuzsuz</button>
          <button type="button" class="quick-tag-btn" data-tag="Ayrı Tabak">Ayrı Tabak</button>
          <button type="button" class="quick-tag-btn" data-tag="Çok Sıcak">Çok Sıcak</button>
        </div>
      </div>
      <div class="option-group">
        <label class="group-label" for="mod-special-note">Özel Sipariş Notu</label>
        <input type="text" id="mod-special-note" placeholder="Örn. Şef Notu..." value="${existingItem?.note || ''}" class="form-input" autocomplete="off">
      </div>
    `;

    container.innerHTML = bodyHtml;

    container.querySelectorAll('input[type="radio"], input[type="checkbox"]').forEach(inp => {
      inp.addEventListener('change', updateModifierLivePrice);
    });

    container.querySelectorAll('.quick-tag-btn').forEach(btn => {
      const tag = btn.dataset.tag;
      if (state.selectedQuickTags.includes(tag)) btn.classList.add('active');

      btn.addEventListener('click', () => {
        btn.classList.toggle('active');
        if (btn.classList.contains('active')) {
          state.selectedQuickTags.push(tag);
        } else {
          state.selectedQuickTags = state.selectedQuickTags.filter(t => t !== tag);
        }
      });
    });

    const confirmBtn = document.getElementById('btn-confirm-modifier');
    if (confirmBtn) {
      confirmBtn.innerHTML = existingItem 
        ? `Güncelle ve Kaydet (<span id="mod-final-price">${formatTL(prod.price)}</span>)`
        : `Sepete Ekle (<span id="mod-final-price">${formatTL(prod.price)}</span>)`;
    }

    updateModifierLivePrice();
    if (modal) modal.style.display = 'flex';
  }

  function updateModifierLivePrice() {
    if (!state.activeModifierProduct) return;
    let extraTotal = 0;
    const container = document.getElementById('mod-dynamic-options-container');
    if (container) {
      container.querySelectorAll('input:checked').forEach(inp => {
        extraTotal += parseFloat(inp.dataset.extraPrice || 0);
      });
    }

    const currentTotal = state.activeModifierProduct.price + extraTotal;
    const finalPriceEl = document.getElementById('mod-final-price');
    const priceSubtitleEl = document.getElementById('mod-price');
    if (finalPriceEl) finalPriceEl.textContent = formatTL(currentTotal);
    if (priceSubtitleEl) priceSubtitleEl.textContent = formatTL(currentTotal);
  }

  // 4.6 80mm ESC/POS Thermal Slip Simulation
  function renderThermalSlip() {
    const container = document.getElementById('thermal-slip-content');
    const subTitle = document.getElementById('thermal-slip-subtitle');
    if (!container || !state.selectedTable) return;

    if (subTitle) subTitle.textContent = `Masa ${state.selectedTable.number} (${state.selectedTable.section})`;

    const items = state.activeCart.length > 0 ? state.activeCart : [
      { name: 'Alkaros Burger', unitPrice: 240.00, quantity: 1, seat: '1' },
      { name: 'Patates Tava', unitPrice: 85.00, quantity: 1, seat: 'shared' },
      { name: 'Coca Cola 330ml', unitPrice: 45.00, quantity: 2, seat: 'shared' }
    ];

    const subtotal = items.reduce((sum, i) => sum + (i.unitPrice * i.quantity), 0);
    const tax = subtotal * 0.10;
    const total = subtotal;

    container.innerHTML = `
      <div class="slip-paper">
        <div class="slip-header">
          <div class="slip-brand">*** ALKAROS RESTORAN ***</div>
          <div class="slip-meta">Masa: ${state.selectedTable.number} | Garson: ${state.selectedTable.waiter || 'Mehmet K.'}</div>
          <div class="slip-meta">Tarih: ${new Date().toLocaleDateString('tr-TR')} ${new Date().toLocaleTimeString('tr-TR')}</div>
          <div class="slip-divider">------------------------------------------</div>
        </div>
        <div class="slip-items">
          ${items.map(i => `
            <div class="slip-line">
              <span>${i.quantity}x ${i.name}</span>
              <span class="num-val">${formatTL(i.unitPrice * i.quantity)}</span>
            </div>
          `).join('')}
        </div>
        <div class="slip-divider">------------------------------------------</div>
        <div class="slip-totals">
          <div class="slip-line"><span>ARA TOPLAM:</span><span>${formatTL(subtotal)}</span></div>
          <div class="slip-line"><span>KDV (%10):</span><span>${formatTL(tax)}</span></div>
          <div class="slip-line slip-grand-total"><strong>GENEL TOPLAM:</strong><strong>${formatTL(total)}</strong></div>
        </div>
        <div class="slip-footer">
          <div class="slip-divider">------------------------------------------</div>
          <div>BU BİR ÖN ADİSYON BİLGİ FİŞİDİR</div>
          <div>MALİ DEĞERİ YOKTUR</div>
          <div class="slip-barcode">||| | ||||| ||| |||| |||| |||</div>
        </div>
      </div>
    `;
  }

  // 4.7 Split Bill Modal Renderer
  function renderSplitBill() {
    const list = document.getElementById('split-items-list');
    const payEl = document.getElementById('split-paying-amount');
    const remainEl = document.getElementById('split-remaining-amount');
    const titleEl = document.getElementById('split-bill-subtitle');

    if (!state.selectedTable) return;
    if (titleEl) titleEl.textContent = `Masa ${state.selectedTable.number} (${formatTL(state.selectedTable.billAmount)})`;

    let payAmount = 0;
    let items = [];

    if (state.splitActiveSeat === '1') {
      payAmount = 240.00;
      items = [{ name: '1x Alkaros Burger (200g)', price: 240.00 }];
    } else if (state.splitActiveSeat === '2') {
      payAmount = 160.00;
      items = [{ name: '1x Bonfile Kısmi Porsiyon', price: 160.00 }];
    } else {
      payAmount = 85.00;
      items = [{ name: '1x Patates Tava (Ortaya)', price: 85.00 }];
    }

    const remaining = Math.max(0, (state.selectedTable.billAmount || 485.00) - payAmount);

    if (list) {
      list.innerHTML = items.map(i => `
        <div class="split-item-row">
          <span>${i.name}</span>
          <span class="num-val">${formatTL(i.price)}</span>
        </div>
      `).join('');
    }

    if (payEl) payEl.textContent = formatTL(payAmount);
    if (remainEl) remainEl.textContent = formatTL(remaining);
  }

  // 4.8 Operations & Printers
  function renderOperations() {
    const feed = document.getElementById('ops-tickets-feed');
    const printersList = document.getElementById('ops-printers-list');
    const auditBox = document.getElementById('ops-audit-log');
    const ticketCountEl = document.getElementById('ops-ticket-count');

    if (feed) {
      let filtered = state.tickets.filter(t => {
        return state.activeStationFilter === 'all' || t.station === state.activeStationFilter;
      });

      if (ticketCountEl) ticketCountEl.textContent = `${filtered.length} Fiş`;

      feed.innerHTML = filtered.map(ticket => `
        <div class="ticket-card">
          <div class="ticket-top">
            <span>Fiş #${ticket.id} — ${ticket.table}</span>
            <span class="meta-row">${ticket.time}</span>
          </div>
          <div class="ticket-items-list">
            ${ticket.items.map(i => {
              let badgeHtml = '';
              if (i.status === 'cooking') badgeHtml = '<span class="occupancy-pill" style="background:var(--badge-cooking-bg);color:var(--badge-cooking-text)">Hazırlanıyor</span>';
              else if (i.status === 'ready') badgeHtml = '<span class="occupancy-pill" style="background:var(--badge-ready-bg);color:var(--badge-ready-text)">Hazır</span>';
              else badgeHtml = '<span class="occupancy-pill" style="background:var(--color-surface-active);color:var(--color-text-muted)">Bekliyor</span>';
              return `<div class="ticket-item-row"><span>${i.name}</span>${badgeHtml}</div>`;
            }).join('')}
          </div>
        </div>
      `).join('');
    }

    if (printersList) {
      printersList.innerHTML = state.printers.map(p => `
        <div class="printer-card ${p.status === 'paper_out' ? 'warning' : ''}">
          <div class="printer-info">
            <span class="printer-name">${p.name}</span>
            <span class="printer-ip">IP: ${p.ip} • Durum: <strong>${p.status === 'paper_out' ? p.issue : 'Çevrimiçi'}</strong></span>
          </div>
          <div style="display:flex;gap:6px;align-items:center">
            ${p.status === 'paper_out' 
              ? `<button type="button" class="btn-primary-sm btn-reroute-printer" data-prn-id="${p.id}">Kuyruğu Yönlendir</button>`
              : `<span class="occupancy-pill available">Normal</span>`
            }
          </div>
        </div>
      `).join('');

      printersList.querySelectorAll('.btn-reroute-printer').forEach(btn => {
        btn.addEventListener('click', () => {
          const prn = state.printers.find(p => p.id === btn.dataset.prnId);
          if (prn) {
            prn.status = 'online';
            prn.queueCount = 0;
            state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] Acil Yönlendirme: ${prn.name} kuyruğundaki 2 fiş Sıcak Mutfak Yazıcısına başarıyla aktarıldı.`);
            showToast(`${prn.name} kuyruğu Sıcak Mutfak Yazıcısına yönlendirildi.`);
            renderOperations();
          }
        });
      });
    }

    if (auditBox) {
      auditBox.innerHTML = state.auditLogs.map(l => `<div class="log-entry">${l}</div>`).join('');
    }
  }

  // 4.9 Waiter Surface
  function renderWaiterSurface() {
    const grid = document.getElementById('waiter-tables-container');
    const catChips = document.getElementById('wtr-cat-chips');
    const productList = document.getElementById('wtr-product-list');
    const cartContainer = document.getElementById('wtr-cart-items');
    const wtrTotal = document.getElementById('wtr-cart-total');
    const wtrCount = document.getElementById('wtr-cart-count');
    const wtrBtnPrice = document.getElementById('wtr-btn-price');
    const statusFeed = document.getElementById('wtr-status-feed');
    const notifFeed = document.getElementById('wtr-notif-feed');
    const unreadDot = document.getElementById('wtr-unread-dot');

    if (grid) {
      let filtered = state.tables.filter(t => {
        const matchSection = state.wtrSectionFilter === 'Tümü' || t.section === state.wtrSectionFilter;
        const matchSearch = !state.wtrSearchQuery || t.number.toLowerCase().includes(state.wtrSearchQuery.toLowerCase());
        return matchSection && matchSearch;
      });

      grid.innerHTML = filtered.map(t => {
        let occupClass = t.occupancy === 'occupied' ? 'occupied' : t.occupancy === 'reserved' ? 'reserved' : 'available';
        let occupText = t.occupancy === 'occupied' ? 'Dolu' : t.occupancy === 'reserved' ? 'Rezerve' : 'Boş';
        return `
          <div class="table-card" data-wtr-table-id="${t.id}">
            <div class="table-card-top">
              <span class="table-number">Masa ${t.number}</span>
              <span class="occupancy-pill ${occupClass}">${occupText}</span>
            </div>
            <div class="table-card-body">
              ${t.occupancy === 'occupied' ? `<div class="table-amount num-val">${formatTL(t.billAmount)}</div>` : `<div class="meta-row">${t.capacity} Kişi (${t.section})</div>`}
            </div>
            <button type="button" class="table-card-btn">${t.occupancy === 'available' ? 'Sipariş Aç' : t.occupancy === 'reserved' ? 'Misafiri Oturt >' : 'Masayı Aç >'}</button>
          </div>
        `;
      }).join('');
    }

    if (catChips) {
      const categories = getDistinctCategories();
      catChips.innerHTML = categories.map(cat => `
        <button type="button" class="wtr-chip ${state.wtrCatFilter === cat ? 'active' : ''}" data-wtr-cat="${cat}">${cat}</button>
      `).join('');

      catChips.querySelectorAll('.wtr-chip').forEach(chip => {
        chip.addEventListener('click', () => {
          catChips.querySelectorAll('.wtr-chip').forEach(c => c.classList.remove('active'));
          chip.classList.add('active');
          state.wtrCatFilter = chip.dataset.wtrCat;
          renderWaiterSurface();
        });
      });
    }

    if (productList) {
      let filteredProds = state.products.filter(p => {
        return state.wtrCatFilter === 'Tümü' || p.category === state.wtrCatFilter;
      });

      productList.innerHTML = filteredProds.map(p => `
        <div class="product-card ${p.is86 ? 'is-86' : ''}" data-wtr-prod-id="${p.id}">
          <div class="prod-name">${p.name}</div>
          <div class="prod-price num-val">${formatTL(p.price)}</div>
        </div>
      `).join('');
    }

    if (cartContainer) {
      let total = 0;
      if (state.wtrCart.length === 0) {
        cartContainer.innerHTML = '<span style="font-size:12px;color:var(--color-text-dim)">Sepet boş</span>';
      } else {
        cartContainer.innerHTML = state.wtrCart.map((item, idx) => {
          total += item.unitPrice * item.quantity;
          return `
            <div style="display:flex;justify-content:space-between;align-items:center;font-size:13px;padding:4px 0;border-bottom:1px solid var(--color-border)">
              <div>
                <span>${item.name}</span>
                <span class="num-val" style="display:block;font-size:11px;color:var(--color-text-dim)">${formatTL(item.unitPrice * item.quantity)}</span>
              </div>
              <div style="display:flex;align-items:center;gap:4px">
                <button type="button" class="btn-qty btn-wtr-qty-dec" data-wtr-idx="${idx}">-</button>
                <span style="font-weight:700;font-size:12px;min-width:18px;text-align:center">${item.quantity}</span>
                <button type="button" class="btn-qty btn-wtr-qty-inc" data-wtr-idx="${idx}">+</button>
                <button type="button" class="btn-clear-cart btn-wtr-remove" data-wtr-idx="${idx}" style="margin-left:4px">
                  <svg class="icon" viewBox="0 0 24 24"><path d="M18 6 6 18"/><path d="m6 6 12 12"/></svg>
                </button>
              </div>
            </div>
          `;
        }).join('');

        cartContainer.querySelectorAll('.btn-wtr-qty-inc').forEach(btn => {
          btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const idx = parseInt(btn.dataset.wtrIdx, 10);
            state.wtrCart[idx].quantity += 1;
            renderWaiterSurface();
          });
        });

        cartContainer.querySelectorAll('.btn-wtr-qty-dec').forEach(btn => {
          btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const idx = parseInt(btn.dataset.wtrIdx, 10);
            if (state.wtrCart[idx].quantity > 1) {
              state.wtrCart[idx].quantity -= 1;
            } else {
              state.wtrCart.splice(idx, 1);
            }
            renderWaiterSurface();
          });
        });

        cartContainer.querySelectorAll('.btn-wtr-remove').forEach(btn => {
          btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const idx = parseInt(btn.dataset.wtrIdx, 10);
            state.wtrCart.splice(idx, 1);
            renderWaiterSurface();
          });
        });
      }
      if (wtrTotal) wtrTotal.textContent = formatTL(total);
      if (wtrCount) wtrCount.textContent = state.wtrCart.length;
      if (wtrBtnPrice) wtrBtnPrice.textContent = formatTL(total);
    }

    if (statusFeed) {
      statusFeed.innerHTML = state.tickets.map(t => `
        <div class="ticket-card" style="margin-bottom:10px">
          <div class="ticket-top">
            <span>Fiş #${t.id} — ${t.table}</span>
            <span class="meta-row">${t.time}</span>
          </div>
          <div class="ticket-items-list">
            ${t.items.map(i => `
              <div class="ticket-item-row">
                <span>${i.name}</span>
                <span class="occupancy-pill" style="background:${i.status === 'cooking' ? 'var(--badge-cooking-bg)' : i.status === 'ready' ? 'var(--badge-ready-bg)' : 'var(--color-surface-active)'};color:${i.status === 'cooking' ? 'var(--badge-cooking-text)' : i.status === 'ready' ? 'var(--badge-ready-text)' : 'var(--color-text-muted)'}">
                  ${i.status === 'cooking' ? 'Hazırlanıyor' : i.status === 'ready' ? 'Servise Hazır' : 'Bekliyor'}
                </span>
              </div>
            `).join('')}
          </div>
        </div>
      `).join('');
    }

    if (notifFeed) {
      const unreadCount = state.notifications.filter(n => n.unread).length;
      if (unreadDot) unreadDot.style.display = unreadCount > 0 ? 'block' : 'none';

      notifFeed.innerHTML = state.notifications.map((n, i) => `
        <div class="wtr-notif-card ${n.unread ? 'unread' : ''}">
          <div>
            <div style="font-weight:600;font-size:13px">${n.text}</div>
            <div class="meta-row">${n.time}</div>
          </div>
          <button type="button" class="btn-secondary-sm btn-deliver-notif" data-notif-idx="${i}">Teslim Ettim</button>
        </div>
      `).join('');
    }
  }

  // --- 5. EVENT ATTACHMENTS ---

  function setupEvents() {
    // 5.1 Main Cashier Navigation Tabs
    const tabTables = document.getElementById('tab-cui-tables');
    const tabMenu = document.getElementById('tab-cui-menu');
    const tabOps = document.getElementById('tab-cui-operations');

    const viewTables = document.getElementById('cui-view-tables');
    const viewOrder = document.getElementById('cui-view-order-entry');
    const viewMenu = document.getElementById('cui-view-menu');
    const viewOps = document.getElementById('cui-view-operations');

    const resetViews = () => {
      [viewTables, viewOrder, viewMenu, viewOps].forEach(v => { if (v) v.style.display = 'none'; });
      [tabTables, tabMenu, tabOps].forEach(t => { if (t) t.classList.remove('active'); });
    };

    if (tabTables) {
      tabTables.addEventListener('click', () => {
        resetViews();
        tabTables.classList.add('active');
        viewTables.style.display = 'flex';
        renderCashierTables();
      });
    }

    if (tabMenu) {
      tabMenu.addEventListener('click', () => {
        resetViews();
        tabMenu.classList.add('active');
        viewMenu.style.display = 'flex';
        renderMenuManagement();
      });
    }

    if (tabOps) {
      tabOps.addEventListener('click', () => {
        resetViews();
        tabOps.classList.add('active');
        viewOps.style.display = 'flex';
        renderOperations();
      });
    }

    // 5.2 Station Filters in Operations Tab
    const stationFilters = document.getElementById('station-filters');
    if (stationFilters) {
      stationFilters.querySelectorAll('button[data-station]').forEach(btn => {
        btn.addEventListener('click', () => {
          stationFilters.querySelectorAll('button').forEach(b => b.classList.remove('active'));
          btn.classList.add('active');
          state.activeStationFilter = btn.dataset.station;
          renderOperations();
        });
      });
    }

    // 5.3 Add Table Modal (İşletmeci Masa Ekleme)
    const btnOpenAddTable = document.getElementById('btn-open-add-table');
    const modalAddTable = document.getElementById('modal-add-table');
    const btnCloseAddTable = document.getElementById('btn-close-add-table');
    const btnCancelAddTable = document.getElementById('btn-cancel-add-table');
    const btnConfirmAddTable = document.getElementById('btn-confirm-add-table');

    if (btnOpenAddTable) {
      btnOpenAddTable.addEventListener('click', () => {
        document.getElementById('input-table-number').value = '';
        if (modalAddTable) modalAddTable.style.display = 'flex';
      });
    }
    if (btnCloseAddTable) btnCloseAddTable.addEventListener('click', () => modalAddTable.style.display = 'none');
    if (btnCancelAddTable) btnCancelAddTable.addEventListener('click', () => modalAddTable.style.display = 'none');
    if (btnConfirmAddTable) {
      btnConfirmAddTable.addEventListener('click', () => {
        const number = document.getElementById('input-table-number')?.value.trim();
        const section = document.getElementById('select-table-section')?.value || 'Salon';
        const capacity = parseInt(document.querySelector('input[name="tableCapacity"]:checked')?.value || 4, 10);

        if (!number) {
          showToast('Lütfen masa numarası giriniz!', 'warning');
          return;
        }

        const newTable = {
          id: 'tbl-' + Date.now(),
          number: number.toUpperCase(),
          section,
          occupancy: 'available',
          opBadge: null,
          capacity,
          billAmount: 0.00,
          waiter: null,
          minutes: null,
          previousDrinks: []
        };

        state.tables.push(newTable);
        state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] Yeni Masa: Masa ${newTable.number} (${section} - ${capacity} Kişilik) eklendi.`);
        showToast(`Masa ${newTable.number} (${section}) başarıyla eklendi.`);

        if (modalAddTable) modalAddTable.style.display = 'none';
        renderFloorSections();
        renderCashierTables();
        renderWaiterSurface();
      });
    }

    // 5.4 Add Product Modal (İşletmeci Menüye Ürün Ekleme)
    const btnOpenAddProduct = document.getElementById('btn-open-add-product');
    const modalAddProduct = document.getElementById('modal-add-product');
    const btnCloseAddProduct = document.getElementById('btn-close-add-product');
    const btnCancelAddProduct = document.getElementById('btn-cancel-add-product');
    const btnConfirmAddProduct = document.getElementById('btn-confirm-add-product');

    if (btnOpenAddProduct) {
      btnOpenAddProduct.addEventListener('click', () => {
        document.getElementById('input-product-name').value = '';
        document.getElementById('input-product-category').value = '';
        document.getElementById('input-product-price').value = '';
        document.getElementById('input-product-allergen').value = '';
        if (modalAddProduct) modalAddProduct.style.display = 'flex';
      });
    }
    if (btnCloseAddProduct) btnCloseAddProduct.addEventListener('click', () => modalAddProduct.style.display = 'none');
    if (btnCancelAddProduct) btnCancelAddProduct.addEventListener('click', () => modalAddProduct.style.display = 'none');
    if (btnConfirmAddProduct) {
      btnConfirmAddProduct.addEventListener('click', () => {
        const name = document.getElementById('input-product-name')?.value.trim();
        const category = document.getElementById('input-product-category')?.value.trim() || 'Genel';
        const price = parseFloat(document.getElementById('input-product-price')?.value || 0);
        const station = document.getElementById('select-product-station')?.value || 'hot';
        const defaultCourse = document.querySelector('input[name="prodCourse"]:checked')?.value || 'Ana Yemek';
        const allergen = document.getElementById('input-product-allergen')?.value.trim() || null;

        if (!name || price <= 0) {
          showToast('Lütfen geçerli ürün adı ve fiyat giriniz!', 'warning');
          return;
        }

        const newProd = {
          id: 'p_' + Date.now(),
          name,
          category,
          price,
          station,
          defaultCourse,
          allergen,
          is86: false,
          modifierGroups: []
        };

        state.products.push(newProd);
        state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] Menü Ekleme: ${newProd.name} (${category} - ${formatTL(price)}) eklendi.`);
        showToast(`${newProd.name} menüye eklendi.`);

        if (modalAddProduct) modalAddProduct.style.display = 'none';
        renderMenuManagement();
        renderPOSCatalog();
        renderWaiterSurface();
      });
    }

    // 5.5 Table Card Click -> Open POS
    const tableGrid = document.getElementById('cashier-table-grid');
    if (tableGrid) {
      tableGrid.addEventListener('click', (e) => {
        const card = e.target.closest('.table-card');
        if (!card) return;
        const tableId = card.dataset.tableId;
        const table = state.tables.find(t => t.id === tableId);
        if (!table) return;

        if (table.occupancy === 'reserved') {
          table.occupancy = 'occupied';
          table.waiter = 'Mehmet K.';
          table.minutes = 1;
          showToast(`Masa ${table.number} misafiri oturtuldu. Sipariş girişi açıldı.`);
        }

        state.selectedTable = table;
        state.activeCart = [];
        state.activeDiscount = 0;

        viewTables.style.display = 'none';
        viewOrder.style.display = 'flex';
        
        const titleEl = document.getElementById('pos-active-table-title');
        if (titleEl) {
          titleEl.innerHTML = `Masa ${table.number} <span class="table-section-tag">(${table.section})</span>`;
        }

        renderPOSCatalog();
        renderCart();
      });
    }

    // 5.6 Back to Tables from POS
    const btnBack = document.getElementById('btn-pos-back-to-tables');
    if (btnBack) {
      btnBack.addEventListener('click', () => {
        viewOrder.style.display = 'none';
        viewTables.style.display = 'flex';
        renderCashierTables();
      });
    }

    // 5.7 Table Status Quick Filters
    document.querySelectorAll('.status-quick-filters .filter-tag-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        document.querySelectorAll('.status-quick-filters .filter-tag-btn').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        state.activeStatusFilter = btn.dataset.statusFilter;
        renderCashierTables();
      });
    });

    // 5.8 Table Search (Cashier & Waiter)
    const searchInput = document.getElementById('input-search-tables');
    const clearSearchBtn = document.getElementById('btn-clear-table-search');
    if (searchInput) {
      searchInput.addEventListener('input', (e) => {
        state.searchTableQuery = e.target.value.trim();
        if (clearSearchBtn) clearSearchBtn.style.display = state.searchTableQuery ? 'flex' : 'none';
        renderCashierTables();
      });
    }
    if (clearSearchBtn) {
      clearSearchBtn.addEventListener('click', () => {
        if (searchInput) searchInput.value = '';
        state.searchTableQuery = '';
        clearSearchBtn.style.display = 'none';
        renderCashierTables();
      });
    }

    const wtrSearchInput = document.getElementById('input-wtr-search');
    if (wtrSearchInput) {
      wtrSearchInput.addEventListener('input', (e) => {
        state.wtrSearchQuery = e.target.value.trim();
        renderWaiterSurface();
      });
    }

    // 5.9 Product Search
    const prodSearchInput = document.getElementById('input-search-products');
    if (prodSearchInput) {
      prodSearchInput.addEventListener('input', (e) => {
        state.searchProductQuery = e.target.value.trim();
        renderPOSProducts();
      });
    }

    // 5.10 Seat Selector
    document.querySelectorAll('.seat-chip').forEach(chip => {
      chip.addEventListener('click', () => {
        document.querySelectorAll('.seat-chip').forEach(c => c.classList.remove('active'));
        chip.classList.add('active');
        state.activeSeat = chip.dataset.seat;
      });
    });

    // 5.11 Clear Cart Button
    const btnClearCartTop = document.getElementById('btn-clear-cart');
    if (btnClearCartTop) {
      btnClearCartTop.addEventListener('click', () => {
        if (state.activeCart.length > 0) {
          state.activeCart = [];
          state.activeDiscount = 0;
          renderCart();
          showToast('Sepet temizlendi.');
        }
      });
    }

    // 5.12 Repeat Round
    const btnRepeatRound = document.getElementById('btn-action-repeat-round');
    if (btnRepeatRound) {
      btnRepeatRound.addEventListener('click', () => {
        if (!state.selectedTable) return;
        const drink = state.products.find(p => p.category.includes('İçecek')) || state.products[0];
        state.activeCart.push({
          id: 'rep_' + Math.random().toString(36).substring(2, 7),
          name: drink.name,
          unitPrice: drink.price,
          quantity: 2,
          course: drink.defaultCourse || 'Başlangıç',
          seat: state.activeSeat,
          selectedOptions: [],
          quickTags: [],
          note: 'Tur Tekrarı',
          isComplimentary: false
        });
        renderCart();
        showToast(`Masa ${state.selectedTable.number} içecek turu kopyalandı! (2x ${drink.name})`);
      });
    }

    // 5.13 Custom Item Modal (+ Açık Kalem Ekle)
    const btnCustomItem = document.getElementById('btn-action-custom-item');
    const modalCustom = document.getElementById('modal-custom-item');
    const btnCloseCustom = document.getElementById('btn-close-custom-modal');
    const btnCancelCustom = document.getElementById('btn-cancel-custom');
    const btnConfirmCustom = document.getElementById('btn-confirm-custom');

    if (btnCustomItem) {
      btnCustomItem.addEventListener('click', () => {
        document.getElementById('input-custom-name').value = '';
        document.getElementById('input-custom-price').value = '';
        if (modalCustom) modalCustom.style.display = 'flex';
      });
    }
    if (btnCloseCustom) btnCloseCustom.addEventListener('click', () => modalCustom.style.display = 'none');
    if (btnCancelCustom) btnCancelCustom.addEventListener('click', () => modalCustom.style.display = 'none');
    if (btnConfirmCustom) {
      btnConfirmCustom.addEventListener('click', () => {
        const name = document.getElementById('input-custom-name')?.value.trim();
        const price = parseFloat(document.getElementById('input-custom-price')?.value || 0);
        const course = document.querySelector('input[name="customCourse"]:checked')?.value || 'Ana Yemek';

        if (!name || price <= 0) {
          showToast('Lütfen geçerli bir ürün adı ve fiyat giriniz!', 'warning');
          return;
        }

        const customItem = {
          id: 'custom_' + Date.now(),
          name: `[Özel] ${name}`,
          unitPrice: price,
          quantity: 1,
          course: course,
          seat: state.activeSeat,
          selectedOptions: [],
          quickTags: [],
          note: 'Menü Dışı Açık Kalem',
          isComplimentary: false
        };

        state.activeCart.push(customItem);
        showToast(`${customItem.name} sepete eklendi.`);
        if (modalCustom) modalCustom.style.display = 'none';
        renderCart();
      });
    }

    // 5.14 80mm ESC/POS Thermal Slip
    const btnPrintPrebill = document.getElementById('btn-action-print-prebill');
    const modalThermal = document.getElementById('modal-thermal-slip');
    const btnCloseThermal = document.getElementById('btn-close-thermal');
    const btnCloseThermalBtn = document.getElementById('btn-close-thermal-btn');
    const btnPrintHardware = document.getElementById('btn-print-hardware');

    if (btnPrintPrebill) {
      btnPrintPrebill.addEventListener('click', () => {
        if (!state.selectedTable) return;
        renderThermalSlip();
        if (modalThermal) modalThermal.style.display = 'flex';
      });
    }
    if (btnCloseThermal) btnCloseThermal.addEventListener('click', () => modalThermal.style.display = 'none');
    if (btnCloseThermalBtn) btnCloseThermalBtn.addEventListener('click', () => modalThermal.style.display = 'none');
    if (btnPrintHardware) {
      btnPrintHardware.addEventListener('click', () => {
        state.selectedTable.opBadge = 'bill-requested';
        showToast(`Masa ${state.selectedTable.number} ön adisyon fişi yazıcıya iletildi.`);
        if (modalThermal) modalThermal.style.display = 'none';
        viewOrder.style.display = 'none';
        viewTables.style.display = 'flex';
        renderCashierTables();
      });
    }

    // 5.15 Split Bill Button & Modal
    const btnSplitBill = document.getElementById('btn-action-split-bill');
    const modalSplitBill = document.getElementById('modal-split-bill');
    const btnCloseSplitBill = document.getElementById('btn-close-split-bill');
    const btnCancelSplitBill = document.getElementById('btn-cancel-split-bill');
    const btnConfirmSplitPayment = document.getElementById('btn-confirm-split-payment');

    if (btnSplitBill) {
      btnSplitBill.addEventListener('click', () => {
        if (!state.selectedTable) return;
        renderSplitBill();
        if (modalSplitBill) modalSplitBill.style.display = 'flex';
      });
    }
    if (btnCloseSplitBill) btnCloseSplitBill.addEventListener('click', () => modalSplitBill.style.display = 'none');
    if (btnCancelSplitBill) btnCancelSplitBill.addEventListener('click', () => modalSplitBill.style.display = 'none');

    document.querySelectorAll('.split-seat-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        document.querySelectorAll('.split-seat-btn').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        state.splitActiveSeat = btn.dataset.splitSeat;
        const confirmBtn = document.getElementById('btn-confirm-split-payment');
        if (confirmBtn) {
          confirmBtn.textContent = `${btn.textContent} Ödemesini Al ve Kapat`;
        }
        renderSplitBill();
      });
    });

    if (btnConfirmSplitPayment) {
      btnConfirmSplitPayment.addEventListener('click', () => {
        const payAmount = state.splitActiveSeat === '1' ? 240.00 : state.splitActiveSeat === '2' ? 160.00 : 85.00;
        const payMethod = document.querySelector('input[name="splitPaymentMethod"]:checked')?.value || 'Kredi Kartı';

        if (state.selectedTable) {
          state.selectedTable.billAmount = Math.max(0, (state.selectedTable.billAmount || 485.00) - payAmount);
          state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] Parçalı Tahsilat: Masa ${state.selectedTable.number} - Koltuk ${state.splitActiveSeat} için ${formatTL(payAmount)} ödendi (${payMethod}).`);
          showToast(`Koltuk ${state.splitActiveSeat} tahsilatı alındı (${formatTL(payAmount)}). Kalan: ${formatTL(state.selectedTable.billAmount)}`);
        }

        if (modalSplitBill) modalSplitBill.style.display = 'none';
        renderCart();
        renderCashierTables();
      });
    }

    // 5.16 Table Transfer Modal
    const btnTransfer = document.getElementById('btn-action-transfer-table');
    const modalTransfer = document.getElementById('modal-transfer-table');
    const btnCloseTransfer = document.getElementById('btn-close-transfer-modal');
    const btnCancelTransfer = document.getElementById('btn-cancel-transfer');
    const btnConfirmTransfer = document.getElementById('btn-confirm-transfer');
    const selectTarget = document.getElementById('select-target-table');

    if (btnTransfer) {
      btnTransfer.addEventListener('click', () => {
        if (!state.selectedTable) return;
        const availTables = state.tables.filter(t => t.occupancy === 'available' && t.id !== state.selectedTable.id);
        if (availTables.length === 0) {
          showToast('Taşıma yapılacak boş masa bulunamadı!', 'warning');
          return;
        }
        if (selectTarget) {
          selectTarget.innerHTML = availTables.map(t => `<option value="${t.id}">Masa ${t.number} (${t.section})</option>`).join('');
        }
        const titleEl = document.getElementById('transfer-source-title');
        if (titleEl) titleEl.textContent = `Kaynak: Masa ${state.selectedTable.number} (${formatTL(state.selectedTable.billAmount)})`;
        if (modalTransfer) modalTransfer.style.display = 'flex';
      });
    }
    if (btnCloseTransfer) btnCloseTransfer.addEventListener('click', () => modalTransfer.style.display = 'none');
    if (btnCancelTransfer) btnCancelTransfer.addEventListener('click', () => modalTransfer.style.display = 'none');
    if (btnConfirmTransfer) {
      btnConfirmTransfer.addEventListener('click', () => {
        const targetId = selectTarget?.value;
        const targetTable = state.tables.find(t => t.id === targetId);
        if (targetTable && state.selectedTable) {
          targetTable.occupancy = 'occupied';
          targetTable.billAmount = state.selectedTable.billAmount;
          targetTable.waiter = state.selectedTable.waiter;
          targetTable.minutes = state.selectedTable.minutes;
          targetTable.opBadge = state.selectedTable.opBadge;

          state.selectedTable.occupancy = 'available';
          state.selectedTable.billAmount = 0.00;
          state.selectedTable.waiter = null;
          state.selectedTable.minutes = null;
          state.selectedTable.opBadge = null;

          state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] Masa Transferi: Masa ${state.selectedTable.number} -> Masa ${targetTable.number} aktarıldı.`);
          showToast(`Masa ${state.selectedTable.number} başarıyla Masa ${targetTable.number}'e taşındı!`);
          
          if (modalTransfer) modalTransfer.style.display = 'none';
          viewOrder.style.display = 'none';
          viewTables.style.display = 'flex';
          renderCashierTables();
        }
      });
    }

    // 5.17 Table Merge Modal
    const btnMerge = document.getElementById('btn-action-merge-table');
    const modalMerge = document.getElementById('modal-merge-table');
    const btnCloseMerge = document.getElementById('btn-close-merge-modal');
    const btnCancelMerge = document.getElementById('btn-cancel-merge');
    const btnConfirmMerge = document.getElementById('btn-confirm-merge');
    const selectMergeTarget = document.getElementById('select-merge-target-table');

    if (btnMerge) {
      btnMerge.addEventListener('click', () => {
        if (!state.selectedTable) return;
        const otherOccupied = state.tables.filter(t => t.occupancy === 'occupied' && t.id !== state.selectedTable.id);
        if (otherOccupied.length === 0) {
          showToast('Birleştirilecek başka açık/dolu masa bulunamadı!', 'warning');
          return;
        }
        if (selectMergeTarget) {
          selectMergeTarget.innerHTML = otherOccupied.map(t => `<option value="${t.id}">Masa ${t.number} (${t.section} - ${formatTL(t.billAmount)})</option>`).join('');
        }
        const titleEl = document.getElementById('merge-source-title');
        if (titleEl) titleEl.textContent = `Ana Masa: Masa ${state.selectedTable.number}`;
        if (modalMerge) modalMerge.style.display = 'flex';
      });
    }
    if (btnCloseMerge) btnCloseMerge.addEventListener('click', () => modalMerge.style.display = 'none');
    if (btnCancelMerge) btnCancelMerge.addEventListener('click', () => modalMerge.style.display = 'none');
    if (btnConfirmMerge) {
      btnConfirmMerge.addEventListener('click', () => {
        const targetId = selectMergeTarget?.value;
        const targetTable = state.tables.find(t => t.id === targetId);
        if (targetTable && state.selectedTable) {
          state.selectedTable.billAmount = (state.selectedTable.billAmount || 0) + (targetTable.billAmount || 0);

          targetTable.occupancy = 'available';
          targetTable.billAmount = 0.00;
          targetTable.waiter = null;
          targetTable.minutes = null;
          targetTable.opBadge = null;

          state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] Masa Birleştirme: Masa ${targetTable.number} -> Masa ${state.selectedTable.number} ile birleştirildi.`);
          showToast(`Masa ${targetTable.number} adisyonu Masa ${state.selectedTable.number} ile birleştirildi!`);

          if (modalMerge) modalMerge.style.display = 'none';
          renderCart();
        }
      });
    }

    // 5.18 Discount Modal
    const btnAddDiscount = document.getElementById('btn-cart-add-discount');
    const modalDiscount = document.getElementById('modal-discount');
    const btnCloseDiscount = document.getElementById('btn-close-discount-modal');
    const btnCancelDiscount = document.getElementById('btn-cancel-discount');
    const btnConfirmDiscount = document.getElementById('btn-confirm-discount');

    if (btnAddDiscount) {
      btnAddDiscount.addEventListener('click', () => {
        if (modalDiscount) modalDiscount.style.display = 'flex';
      });
    }
    if (btnCloseDiscount) btnCloseDiscount.addEventListener('click', () => modalDiscount.style.display = 'none');
    if (btnCancelDiscount) btnCancelDiscount.addEventListener('click', () => modalDiscount.style.display = 'none');
    if (btnConfirmDiscount) {
      btnConfirmDiscount.addEventListener('click', () => {
        const rateVal = parseFloat(document.querySelector('input[name="discountRate"]:checked')?.value || 10);
        const reason = document.getElementById('select-discount-reason')?.value;

        if (rateVal === 50) {
          state.activeDiscount = 50.00;
        } else {
          state.activeDiscount = rateVal / 100;
        }

        state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] İndirim: Masa ${state.selectedTable.number} için ${rateVal === 50 ? '50 TL Sabit' : '%' + rateVal} uygulandı (${reason}).`);
        showToast('Yetkili indirimi adisyona uygulandı.');

        if (modalDiscount) modalDiscount.style.display = 'none';
        renderCart();
      });
    }

    // 5.19 Product Grid Click -> Modifier Modal
    const prodGrid = document.getElementById('pos-product-grid');
    if (prodGrid) {
      prodGrid.addEventListener('click', (e) => {
        const card = e.target.closest('.product-card');
        if (!card) return;
        const prodId = card.dataset.prodId;
        const prod = state.products.find(p => p.id === prodId);
        if (!prod) return;

        state.editingCartIndex = null;
        openDynamicModifierModal(prod);
      });
    }

    // 5.20 Dynamic Modifier Sheet Confirm Button
    const btnConfirmMod = document.getElementById('btn-confirm-modifier');
    const btnCloseMod = document.getElementById('btn-close-modifier');
    const btnCancelMod = document.getElementById('btn-cancel-modifier');
    const modModal = document.getElementById('modal-modifier');

    const closeModModal = () => {
      if (modModal) modModal.style.display = 'none';
      state.activeModifierProduct = null;
      state.editingCartIndex = null;
    };

    if (btnCloseMod) btnCloseMod.addEventListener('click', closeModModal);
    if (btnCancelMod) btnCancelMod.addEventListener('click', closeModModal);

    if (btnConfirmMod) {
      btnConfirmMod.addEventListener('click', () => {
        if (!state.activeModifierProduct) return;

        const courseEl = document.querySelector('#modal-modifier input[name="courseType"]:checked');
        const course = courseEl ? courseEl.value : 'Ana Yemek';

        const selectedOptions = [];
        let extraTotal = 0;

        const container = document.getElementById('mod-dynamic-options-container');
        if (container) {
          container.querySelectorAll('input:checked').forEach(inp => {
            if (inp.name !== 'courseType') {
              const optName = inp.value;
              const group = inp.dataset.groupTitle || 'Seçenek';
              const price = parseFloat(inp.dataset.extraPrice || 0);
              extraTotal += price;
              selectedOptions.push({ group, name: optName, price });
            }
          });
        }

        const note = document.getElementById('mod-special-note')?.value.trim() || null;

        if (state.editingCartIndex !== null) {
          const existingItem = state.activeCart[state.editingCartIndex];
          existingItem.unitPrice = state.activeModifierProduct.price + extraTotal;
          existingItem.course = course;
          existingItem.selectedOptions = selectedOptions;
          existingItem.quickTags = [...state.selectedQuickTags];
          existingItem.note = note;
          showToast(`${existingItem.name} detayları güncellendi.`);
        } else {
          const cartItem = {
            id: state.activeModifierProduct.id,
            name: state.activeModifierProduct.name,
            unitPrice: state.activeModifierProduct.price + extraTotal,
            quantity: 1,
            course,
            seat: state.activeSeat,
            selectedOptions,
            quickTags: [...state.selectedQuickTags],
            note,
            isComplimentary: false
          };
          state.activeCart.push(cartItem);
          showToast(`${cartItem.name} sepete eklendi.`);
        }

        closeModModal();
        renderCart();
      });
    }

    // 5.21 Cart Item Actions
    const cartItemsList = document.getElementById('pos-cart-items');
    const modalItemAction = document.getElementById('modal-item-action');
    const btnCloseItemAction = document.getElementById('btn-close-item-action');
    const btnCancelItemAction = document.getElementById('btn-cancel-item-action');
    const btnConfirmItemAction = document.getElementById('btn-confirm-item-action');

    if (cartItemsList) {
      cartItemsList.addEventListener('click', (e) => {
        const editTrigger = e.target.closest('.btn-edit-cart-item');
        const incBtn = e.target.closest('.btn-qty-inc');
        const decBtn = e.target.closest('.btn-qty-dec');
        const removeBtn = e.target.closest('.btn-cart-remove');
        const manageBtn = e.target.closest('.btn-item-manage');

        if (editTrigger) {
          const idx = parseInt(editTrigger.dataset.index, 10);
          const item = state.activeCart[idx];
          if (!item) return;

          state.editingCartIndex = idx;
          const prod = state.products.find(p => p.name === item.name || p.id === item.id) || { id: item.id, name: item.name, price: item.unitPrice, modifierGroups: [] };
          openDynamicModifierModal(prod, item);
        } else if (incBtn) {
          const idx = parseInt(incBtn.dataset.index, 10);
          state.activeCart[idx].quantity += 1;
          renderCart();
        } else if (decBtn) {
          const idx = parseInt(decBtn.dataset.index, 10);
          if (state.activeCart[idx].quantity > 1) {
            state.activeCart[idx].quantity -= 1;
          } else {
            state.activeCart.splice(idx, 1);
          }
          renderCart();
        } else if (removeBtn) {
          const idx = parseInt(removeBtn.dataset.index, 10);
          state.activeCart.splice(idx, 1);
          renderCart();
        } else if (manageBtn) {
          const idx = parseInt(manageBtn.dataset.index, 10);
          state.selectedCartItemIndex = idx;
          const item = state.activeCart[idx];
          const subTitle = document.getElementById('item-action-subtitle');
          if (subTitle) subTitle.textContent = `${item.quantity}x ${item.name}`;
          if (modalItemAction) modalItemAction.style.display = 'flex';
        }
      });
    }

    if (btnCloseItemAction) btnCloseItemAction.addEventListener('click', () => modalItemAction.style.display = 'none');
    if (btnCancelItemAction) btnCancelItemAction.addEventListener('click', () => modalItemAction.style.display = 'none');
    if (btnConfirmItemAction) {
      btnConfirmItemAction.addEventListener('click', () => {
        if (state.selectedCartItemIndex !== null && state.activeCart[state.selectedCartItemIndex]) {
          const actionType = document.querySelector('input[name="itemActionType"]:checked')?.value;
          const reason = document.getElementById('select-item-reason')?.value;
          const item = state.activeCart[state.selectedCartItemIndex];

          if (actionType === 'complimentary') {
            item.unitPrice = 0.00;
            item.isComplimentary = true;
            state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] İkram: Masa ${state.selectedTable.number} -> ${item.name} ikram edildi (${reason}).`);
            showToast(`${item.name} ikram olarak güncellendi.`);
          } else if (actionType === 'void') {
            state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] İptal: Masa ${state.selectedTable.number} -> ${item.name} iptal edildi (${reason}).`);
            state.activeCart.splice(state.selectedCartItemIndex, 1);
            showToast(`${item.name} iptal edildi.`);
          }
        }
        if (modalItemAction) modalItemAction.style.display = 'none';
        renderCart();
      });
    }

    // 5.22 Submit POS Order
    const btnSubmit = document.getElementById('btn-pos-submit-order');
    if (btnSubmit) {
      btnSubmit.addEventListener('click', () => {
        if (!state.isOnline) {
          showToast('Sunucu bağlantısı olmadan sipariş iletilemez!', 'error');
          return;
        }
        if (state.activeCart.length === 0) return;

        const idempotencyKey = 'ord_' + Math.random().toString(36).substring(2, 11);
        btnSubmit.disabled = true;
        btnSubmit.innerHTML = `<span>⏳ İletiliyor (${idempotencyKey.substring(0, 8)})...</span>`;

        setTimeout(() => {
          if (state.selectedTable) {
            const tbl = state.tables.find(t => t.id === state.selectedTable.id);
            if (tbl) {
              tbl.occupancy = 'occupied';
              tbl.opBadge = 'cooking';
              tbl.waiter = 'Ahmet Y.';
              tbl.minutes = 1;
              const cartSum = state.activeCart.reduce((sum, i) => sum + (i.unitPrice * i.quantity), 0);
              tbl.billAmount = (tbl.billAmount || 0) + cartSum;
            }
          }

          state.tickets.unshift({
            id: String(1045 + state.tickets.length),
            table: `Masa ${state.selectedTable.number}`,
            time: 'Yeni',
            station: 'hot',
            items: state.activeCart.map(i => ({ name: `${i.quantity}x ${i.name} [${i.course}]`, status: 'cooking' }))
          });

          state.notifications.unshift({
            id: 'notif_' + Date.now(),
            time: new Date().toLocaleTimeString('tr-TR').substring(0, 5),
            text: `Masa ${state.selectedTable.number}: ${state.activeCart.length} Kalem mutfağa iletildi!`,
            unread: true
          });

          state.activeCart = [];
          state.activeDiscount = 0;
          showToast(`Sipariş mutfağa iletildi! Masa ${state.selectedTable.number} güncellendi.`);
          
          viewOrder.style.display = 'none';
          viewTables.style.display = 'flex';
          renderCashierTables();
          renderOperations();
        }, 600);
      });
    }

    // 5.23 Chaos Center Suite Triggers
    const btnOpenChaos = document.getElementById('btn-open-chaos');
    const modalChaos = document.getElementById('modal-chaos-center');
    const btnCloseChaos = document.getElementById('btn-close-chaos');
    const btnCancelChaos = document.getElementById('btn-cancel-chaos');

    if (btnOpenChaos) btnOpenChaos.addEventListener('click', () => modalChaos.style.display = 'flex');
    if (btnCloseChaos) btnCloseChaos.addEventListener('click', () => modalChaos.style.display = 'none');
    if (btnCancelChaos) btnCancelChaos.addEventListener('click', () => modalChaos.style.display = 'none');

    // Chaos Scenario 1: Concurrency Conflict
    const btnRunConcurrency = document.getElementById('btn-run-chaos-concurrency');
    const modalConflict = document.getElementById('modal-concurrency-conflict');
    const btnCloseConflict = document.getElementById('btn-close-conflict');
    const btnConflictAbort = document.getElementById('btn-conflict-abort');
    const btnConflictMerge = document.getElementById('btn-conflict-merge');

    if (btnRunConcurrency) {
      btnRunConcurrency.addEventListener('click', () => {
        modalChaos.style.display = 'none';
        const tbl = state.tables.find(t => t.number === 'S-02');
        if (tbl) {
          state.selectedTable = tbl;
        }
        if (modalConflict) modalConflict.style.display = 'flex';
      });
    }
    if (btnCloseConflict) btnCloseConflict.addEventListener('click', () => modalConflict.style.display = 'none');
    if (btnConflictAbort) {
      btnConflictAbort.addEventListener('click', () => {
        const tbl = state.tables.find(t => t.number === 'S-02');
        if (tbl) tbl.billAmount = 575.00;
        showToast('Masa S-02 güncel sunucu verisiyle (575 TL) yenilendi.');
        modalConflict.style.display = 'none';
        renderCashierTables();
      });
    }
    if (btnConflictMerge) {
      btnConflictMerge.addEventListener('click', () => {
        const tbl = state.tables.find(t => t.number === 'S-02');
        if (tbl) tbl.billAmount = 575.00;
        state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] Concurrency Uzlaşması: Masa S-02 (575 TL) başarıyla senkronize edildi.`);
        showToast('Eşzamanlı değişiklikler başarıyla birleştirildi.');
        modalConflict.style.display = 'none';
        renderCashierTables();
      });
    }

    // Chaos Scenario 2: 86'd Out-of-Stock
    const btnRun86 = document.getElementById('btn-run-chaos-86');
    if (btnRun86) {
      btnRun86.addEventListener('click', () => {
        modalChaos.style.display = 'none';
        const bonfile = state.products.find(p => p.id === 'p3');
        if (bonfile) {
          bonfile.is86 = !bonfile.is86;
          state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] ŞEF ALARMI: ${bonfile.name} durumu -> ${bonfile.is86 ? '86\'d (TÜKENDİ)' : 'Satışta'}.`);
          showToast(`ŞEF ALARMI: ${bonfile.name} ${bonfile.is86 ? 'tükendi (86\'d)!' : 'tekrar satışa açıldı.'}`, bonfile.is86 ? 'error' : 'success');
          renderPOSProducts();
          renderMenuManagement();
          renderWaiterSurface();
        }
      });
    }

    // Chaos Scenario 3: Printer Paper Out
    const btnRunPrinter = document.getElementById('btn-run-chaos-printer');
    if (btnRunPrinter) {
      btnRunPrinter.addEventListener('click', () => {
        modalChaos.style.display = 'none';
        const prn = state.printers.find(p => p.id === 'prn-2');
        if (prn) {
          prn.status = 'paper_out';
          prn.issue = 'Kağıt Bitti / Beklemede';
          state.auditLogs.unshift(`[${new Date().toLocaleTimeString('tr-TR')}] Donanım Uyarısı: Bar Yazıcısı kağıt sonu alarmı tetiklendi.`);
          showToast('UYARI: Bar Yazıcısı kağıt sonu alarmı! Mutfak sekmesini kontrol ediniz.', 'error');
          renderOperations();
        }
      });
    }

    // Chaos Scenario 4: Split Bill
    const btnRunSplit = document.getElementById('btn-run-chaos-split');
    if (btnRunSplit) {
      btnRunSplit.addEventListener('click', () => {
        modalChaos.style.display = 'none';
        const tbl = state.tables.find(t => t.number === 'S-02');
        if (tbl) {
          state.selectedTable = tbl;
          renderSplitBill();
          if (modalSplitBill) modalSplitBill.style.display = 'flex';
        }
      });
    }

    // Chaos Scenario 5: Offline Waiter Mutation & Resync
    const btnRunOfflineSync = document.getElementById('btn-run-chaos-offline-sync');
    if (btnRunOfflineSync) {
      btnRunOfflineSync.addEventListener('click', () => {
        modalChaos.style.display = 'none';
        document.querySelectorAll('.proto-btn[data-view]').forEach(b => b.classList.remove('active'));
        const wtrBtn = document.getElementById('btn-view-waiter-phone');
        if (wtrBtn) wtrBtn.classList.add('active');
        state.currentView = 'waiter-phone';

        const cashierSurf = document.getElementById('surface-cashier');
        const waiterSurf = document.getElementById('surface-waiter');
        const waiterFrame = document.getElementById('waiter-device-frame');
        cashierSurf.style.display = 'none';
        waiterSurf.style.display = 'flex';
        waiterFrame.className = 'device-frame phone-mode';

        state.isOnline = false;
        updateNetworkUI();

        state.wtrActiveTable = state.tables.find(t => t.number === 'B-01');
        state.wtrCart = [
          { id: 'p1', name: 'Alkaros Burger (200g)', unitPrice: 240.00, quantity: 1 },
          { id: 'p7', name: 'Ayran 300ml', unitPrice: 30.00, quantity: 2 }
        ];

        document.getElementById('wtr-view-tables').style.display = 'none';
        document.getElementById('wtr-view-order').style.display = 'flex';
        const nameEl = document.getElementById('wtr-active-table-name');
        if (nameEl) nameEl.textContent = `Masa B-01 (Bahçe - Offline)`;

        renderWaiterSurface();
        showToast('Bahçede Wi-Fi koptu! Çevrimdışı sipariş modundasınız. Sipariş verip ağı açtığınızda test edebilirsiniz.', 'warning');
      });
    }

    // 5.24 Simulator Controls (Görünüm, Tema, Ağ)
    document.querySelectorAll('.proto-btn[data-view]').forEach(btn => {
      btn.addEventListener('click', () => {
        document.querySelectorAll('.proto-btn[data-view]').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        state.currentView = btn.dataset.view;

        const cashierSurf = document.getElementById('surface-cashier');
        const waiterSurf = document.getElementById('surface-waiter');
        const waiterFrame = document.getElementById('waiter-device-frame');

        if (state.currentView === 'cashier') {
          cashierSurf.style.display = 'flex';
          waiterSurf.style.display = 'none';
        } else if (state.currentView === 'waiter-phone') {
          cashierSurf.style.display = 'none';
          waiterSurf.style.display = 'flex';
          waiterFrame.className = 'device-frame phone-mode';
        } else if (state.currentView === 'waiter-tablet') {
          cashierSurf.style.display = 'none';
          waiterSurf.style.display = 'flex';
          waiterFrame.className = 'device-frame tablet-mode';
        }
      });
    });

    const themeBtn = document.getElementById('btn-theme-toggle');
    if (themeBtn) {
      themeBtn.addEventListener('click', () => {
        state.theme = state.theme === 'light' ? 'dark' : 'light';
        document.documentElement.setAttribute('data-theme', state.theme);
        localStorage.setItem('alkaros_theme', state.theme);
      });
    }

    const netBtn = document.getElementById('btn-sim-network');
    const bannerRetryBtn = document.getElementById('btn-banner-retry');

    const updateNetworkUI = () => {
      const banner = document.getElementById('network-outage-banner');
      const labelNet = document.getElementById('label-network');
      const cuiNetDot = document.getElementById('cashier-net-status');
      const wtrPill = document.getElementById('waiter-net-pill');
      const wtrOffBar = document.getElementById('waiter-offline-bar');

      if (!state.isOnline) {
        if (labelNet) labelNet.textContent = 'Ağ: Kesildi (Offline)';
        if (netBtn) netBtn.classList.add('active-danger');
        if (banner) banner.style.display = 'flex';
        if (cuiNetDot) cuiNetDot.className = 'connection-status offline';
        if (wtrPill) wtrPill.innerHTML = '<span class="dot" style="background:#DC2626"></span><span>Çevrimdışı</span>';
        if (wtrOffBar) wtrOffBar.style.display = 'flex';
        showToast('Ağ bağlantısı koptu! Sistem çevrimdışı moda geçti.', 'warning');
      } else {
        if (labelNet) labelNet.textContent = 'Ağ: Çevrimiçi';
        if (netBtn) netBtn.classList.remove('active-danger');
        if (banner) banner.style.display = 'none';
        if (cuiNetDot) cuiNetDot.className = 'connection-status';
        if (wtrPill) wtrPill.innerHTML = '<span class="dot"></span><span>Çevrimiçi</span>';
        if (wtrOffBar) wtrOffBar.style.display = 'none';

        if (state.wtrOfflineQueue.length > 0) {
          const count = state.wtrOfflineQueue.length;
          showToast(`Ağ bağlantısı kuruldu! ${count} çevrimdışı sipariş mutfağa iletildi.`, 'success');
          state.wtrOfflineQueue = [];
          const qCountEl = document.getElementById('waiter-queue-count');
          if (qCountEl) qCountEl.textContent = '0';
        } else {
          showToast('Ağ bağlantısı kuruldu.', 'success');
        }
      }
      renderCart();
    };

    if (netBtn) {
      netBtn.addEventListener('click', () => {
        state.isOnline = !state.isOnline;
        updateNetworkUI();
      });
    }

    if (bannerRetryBtn) {
      bannerRetryBtn.addEventListener('click', () => {
        state.isOnline = true;
        updateNetworkUI();
      });
    }

    // 5.25 PIN Lockout Keypad & Multi-Trigger
    const lockBtn = document.getElementById('btn-sim-lock');
    const cashierLockBtn = document.getElementById('btn-cashier-lock');
    const waiterLockBtn = document.getElementById('btn-waiter-lock');
    const lockModal = document.getElementById('modal-lockout');
    const keypad = document.getElementById('keypad-grid');

    const openLockModal = () => {
      state.isLocked = true;
      state.enteredPin = '';
      updatePinDots();
      if (lockModal) lockModal.style.display = 'flex';
    };

    if (lockBtn) lockBtn.addEventListener('click', openLockModal);
    if (cashierLockBtn) cashierLockBtn.addEventListener('click', openLockModal);
    if (waiterLockBtn) waiterLockBtn.addEventListener('click', openLockModal);

    const updatePinDots = () => {
      for (let i = 1; i <= 4; i++) {
        const dot = document.getElementById(`dot-${i}`);
        if (dot) dot.classList.toggle('filled', i <= state.enteredPin.length);
      }
    };

    if (keypad) {
      keypad.addEventListener('click', (e) => {
        if (state.cooldownRemaining > 0) return;
        const keyBtn = e.target.closest('.key-btn');
        if (!keyBtn) return;
        const key = keyBtn.dataset.key;

        if (key === 'C') {
          state.enteredPin = '';
          updatePinDots();
        } else if (key === 'OK' || state.enteredPin.length === 3) {
          if (key !== 'OK') state.enteredPin += key;
          updatePinDots();

          if (state.enteredPin === '1234') {
            state.isLocked = false;
            state.failedPinAttempts = 0;
            if (lockModal) lockModal.style.display = 'none';
            showToast('Oturum kilidi açıldı.');
          } else {
            showToast('Hatalı PIN! (Demo PIN: 1234)', 'error');
            state.enteredPin = '';
            setTimeout(updatePinDots, 300);
          }
        } else if (state.enteredPin.length < 4) {
          state.enteredPin += key;
          updatePinDots();
        }
      });
    }

    // 5.26 Waiter Surface Actions
    document.querySelectorAll('.waiter-bottom-nav .wtr-nav-item').forEach(item => {
      item.addEventListener('click', () => {
        document.querySelectorAll('.waiter-bottom-nav .wtr-nav-item').forEach(i => i.classList.remove('active'));
        item.classList.add('active');
        const target = item.dataset.wtrTarget;
        
        document.getElementById('wtr-view-tables').style.display = target === 'tables' ? 'flex' : 'none';
        document.getElementById('wtr-view-status').style.display = target === 'status' ? 'flex' : 'none';
        document.getElementById('wtr-view-notifications').style.display = target === 'notifications' ? 'flex' : 'none';
        document.getElementById('wtr-view-order').style.display = 'none';

        if (target === 'notifications') {
          state.notifications.forEach(n => n.unread = false);
          const dot = document.getElementById('wtr-unread-dot');
          if (dot) dot.style.display = 'none';
        }
        renderWaiterSurface();
      });
    });

    const wtrGrid = document.getElementById('waiter-tables-container');
    const wtrViewTables = document.getElementById('wtr-view-tables');
    const wtrViewOrder = document.getElementById('wtr-view-order');
    const btnWtrBack = document.getElementById('btn-wtr-back-tables');
    const btnWtrReqBill = document.getElementById('btn-wtr-request-bill');

    if (wtrGrid) {
      wtrGrid.addEventListener('click', (e) => {
        const card = e.target.closest('.table-card');
        if (!card) return;
        const tblId = card.dataset.wtrTableId;
        const tbl = state.tables.find(t => t.id === tblId);
        if (!tbl) return;

        if (tbl.occupancy === 'reserved') {
          tbl.occupancy = 'occupied';
          tbl.waiter = 'Mehmet K.';
          tbl.minutes = 1;
          showToast(`Masa ${tbl.number} misafiri oturtuldu.`);
        }

        state.wtrActiveTable = tbl;
        state.wtrCart = [];

        wtrViewTables.style.display = 'none';
        wtrViewOrder.style.display = 'flex';
        const nameEl = document.getElementById('wtr-active-table-name');
        if (nameEl) nameEl.textContent = `Masa ${tbl.number} (${tbl.section})`;
        renderWaiterSurface();
      });
    }

    if (btnWtrBack) {
      btnWtrBack.addEventListener('click', () => {
        wtrViewOrder.style.display = 'none';
        wtrViewTables.style.display = 'flex';
        renderWaiterSurface();
      });
    }

    if (btnWtrReqBill) {
      btnWtrReqBill.addEventListener('click', () => {
        if (!state.wtrActiveTable) return;
        state.wtrActiveTable.opBadge = 'bill-requested';
        showToast(`Masa ${state.wtrActiveTable.number} için hesap talebi kasaya iletildi.`);
        wtrViewOrder.style.display = 'none';
        wtrViewTables.style.display = 'flex';
        renderWaiterSurface();
        renderCashierTables();
      });
    }

    const wtrCartToggle = document.getElementById('wtr-cart-toggle');
    const wtrCartTray = document.getElementById('wtr-cart-tray');
    if (wtrCartToggle && wtrCartTray) {
      wtrCartToggle.addEventListener('click', () => {
        wtrCartTray.classList.toggle('expanded');
      });
    }

    const wtrProdList = document.getElementById('wtr-product-list');
    if (wtrProdList) {
      wtrProdList.addEventListener('click', (e) => {
        const card = e.target.closest('.product-card');
        if (!card) return;
        const prodId = card.dataset.wtrProdId;
        const prod = state.products.find(p => p.id === prodId);
        if (!prod) return;

        if (prod.is86) {
          showToast(`DİKKAT: ${prod.name} tükendi (86'd)! Sipariş edilemez.`, 'error');
          return;
        }

        const existing = state.wtrCart.find(i => i.id === prod.id);
        if (existing) {
          existing.quantity += 1;
        } else {
          state.wtrCart.push({ id: prod.id, name: prod.name, unitPrice: prod.price, quantity: 1 });
        }
        renderWaiterSurface();
        showToast(`${prod.name} sepete eklendi.`);
      });
    }

    const btnWtrSubmit = document.getElementById('btn-wtr-submit-order');
    if (btnWtrSubmit) {
      btnWtrSubmit.addEventListener('click', () => {
        if (state.wtrCart.length === 0) return;

        if (!state.isOnline) {
          const queueItem = {
            opId: 'wtr_op_' + Math.random().toString(36).substring(2, 9),
            table: state.wtrActiveTable.number,
            items: [...state.wtrCart]
          };
          state.wtrOfflineQueue.push(queueItem);
          const qCountEl = document.getElementById('waiter-queue-count');
          if (qCountEl) qCountEl.textContent = state.wtrOfflineQueue.length;

          showToast('Çevrimdışı: Sipariş cihaz kuyruğuna alındı. Ağ gelince iletilecek.', 'warning');
          state.wtrCart = [];
          wtrViewOrder.style.display = 'none';
          wtrViewTables.style.display = 'flex';
          renderWaiterSurface();
        } else {
          if (state.wtrActiveTable) {
            state.wtrActiveTable.occupancy = 'occupied';
            state.wtrActiveTable.opBadge = 'cooking';
            state.wtrActiveTable.waiter = 'Mehmet K.';
            state.wtrActiveTable.minutes = 1;
            const sum = state.wtrCart.reduce((s, i) => s + (i.unitPrice * i.quantity), 0);
            state.wtrActiveTable.billAmount = (state.wtrActiveTable.billAmount || 0) + sum;
          }

          state.tickets.unshift({
            id: String(1045 + state.tickets.length),
            table: `Masa ${state.wtrActiveTable.number}`,
            time: 'Yeni',
            station: 'hot',
            items: state.wtrCart.map(i => ({ name: `${i.quantity}x ${i.name}`, status: 'cooking' }))
          });

          showToast(`Masa ${state.wtrActiveTable.number} siparişi mutfağa gönderildi!`);
          state.wtrCart = [];
          wtrViewOrder.style.display = 'none';
          wtrViewTables.style.display = 'flex';
          renderWaiterSurface();
          renderCashierTables();
          renderOperations();
        }
      });
    }

    const notifFeed = document.getElementById('wtr-notif-feed');
    if (notifFeed) {
      notifFeed.addEventListener('click', (e) => {
        const btn = e.target.closest('.btn-deliver-notif');
        if (!btn) return;
        const idx = parseInt(btn.dataset.notifIdx, 10);
        state.notifications.splice(idx, 1);
        showToast('Yemek teslim edildi olarak işaretlendi.');
        renderWaiterSurface();
      });
    }

    // 5.27 Clock Loop
    setInterval(() => {
      const clock = document.getElementById('cashier-clock');
      if (clock) {
        const now = new Date();
        clock.textContent = now.toLocaleDateString('tr-TR') + ' ' + now.toLocaleTimeString('tr-TR');
      }
    }, 1000);
  }

  // --- 6. INITIALIZATION ---

  document.addEventListener('DOMContentLoaded', () => {
    document.documentElement.setAttribute('data-theme', state.theme);
    renderFloorSections();
    renderCashierTables();
    renderPOSCatalog();
    renderMenuManagement();
    renderOperations();
    renderWaiterSurface();
    setupEvents();
  });

})();
