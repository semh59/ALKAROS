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
    ticketItems: [],
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
    waiterLoads: {}
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
    dispatchWaiterSelect: document.getElementById('dispatchWaiterSelect')
  };

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
          remainingCount: p.remainingCount != null ? p.remainingCount : null
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
    if (!el.categoryTabs) return;
    el.categoryTabs.innerHTML = state.categories.map(c => `
      <button type="button" class="tab-chip ${state.activeCategory === c.id ? 'active' : ''}" data-cat-id="${escapeHtml(c.id)}">
        ${escapeHtml(c.name)}
      </button>
    `).join('');
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
      const matchSearch = !query || p.name.toLowerCase().includes(query) || (p.code && p.code.includes(query));
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
          <div class="item-sub">${formatMoney(item.price)} × ${item.quantity} = ${formatMoney(item.price * item.quantity)}</div>
          <input class="item-note-input" type="text" maxlength="200" placeholder="Not (örn. az, acısız)" value="${escapeHtml(item.note || '')}" data-id="${escapeHtml(item.id)}" aria-label="${escapeHtml(item.name)} özel talimat" />
        </div>
        <div class="item-actions">
          <button type="button" class="btn-micro" data-action="dec" data-id="${escapeHtml(item.id)}">−</button>
          <span class="ticket-row-qty">${item.quantity}</span>
          <button type="button" class="btn-micro" data-action="inc" data-id="${escapeHtml(item.id)}">+</button>
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

  function addProductToTicket(product) {
    const existing = state.ticketItems.find(i => i.productId === product.id && !i.note);
    if (existing) {
      existing.quantity += 1;
    } else {
      state.ticketItems.push({
        id: crypto.randomUUID(),
        productId: product.id,
        name: product.name,
        price: product.price,
        quantity: 1,
        note: ''
      });
    }
    renderTicket();
  }

  async function dispatchOrderToKitchen() {
    if (state.ticketItems.length === 0) return;
    if (state.catalogStatus !== 'ready') {
      alert('Katalog sunucudan alınamadığı için sipariş gönderilemiyor.');
      return;
    }
    // Defense in depth: the button is already disabled offline
    // (updateDispatchAvailability), but a click that lands in the gap
    // between an 'offline' event and its disabled re-render must not start
    // the three-request dispatch sequence with no retry/idempotency queue
    // behind it.
    if (!state.isOnline) {
      alert('Bağlantı yok. Sipariş gönderilemiyor.');
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
        specialInstructions: item.note && item.note.trim() ? item.note.trim() : null
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
        alert(describeHttpFailure(draftResponse.status));
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
        alert('Sunucuya ulaşılamadı. Sipariş iletilemedi.');
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
        alert(describeHttpFailure(submitResponse.status));
        return;
      }

      const total = state.ticketItems.reduce((sum, item) => sum + (item.price * item.quantity), 0);
      const itemCount = state.ticketItems.reduce((sum, item) => sum + item.quantity, 0);
      state.shiftTotalOrders += 1;
      state.ticketItems = [];
      renderTicket();
      alert(`Sipariş mutfağa iletildi. (${itemCount} kalem, ${formatMoney(total)})`);
    } catch {
      alert('Sunucuya ulaşılamadı. Sipariş iletilemedi.');
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
    localStorage.setItem('alkaros_cashier_parked', JSON.stringify(state.parkedTickets));
    state.ticketItems = [];
    renderTicket();
    updateParkBadge();
  }

  function recallParkedTicket(parkedId) {
    const index = state.parkedTickets.findIndex(p => p.id === parkedId);
    if (index < 0) return;

    if (state.ticketItems.length > 0) {
      const confirmReplace = confirm(
        'Mevcut sepette ürünler var. Bekletilen fişi yüklemek mevcut sepeti değiştirecektir. Devam etmek istiyor musunuz?'
      );
      if (!confirmReplace) return;
    }

    state.ticketItems = state.parkedTickets[index].items;
    state.parkedTickets.splice(index, 1);
    localStorage.setItem('alkaros_cashier_parked', JSON.stringify(state.parkedTickets));
    renderTicket();
    updateParkBadge();
    if (el.parkedModal) el.parkedModal.hidden = true;
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
      el.ticketItemsStream.addEventListener('input', (e) => {
        const field = e.target.closest('.item-note-input');
        if (!field) return;
        const index = state.ticketItems.findIndex(i => i.id === field.dataset.id);
        if (index >= 0) state.ticketItems[index].note = field.value;
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
        if (el.parkedModal) el.parkedModal.hidden = false;
      });
    }

    // Close Parked Modal
    if (el.btnCloseParkedModal) {
      el.btnCloseParkedModal.addEventListener('click', () => {
        if (el.parkedModal) el.parkedModal.hidden = true;
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
