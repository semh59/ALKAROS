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
import {
  escapeHtml, formatMoney,
  randomUUID, isFullscreen,
} from './js/util.js';
import { state, el, measureChrome } from './js/state.js';
import { applyUser, releaseTrap, showLogin } from './js/auth.js';
import { apiUrl, api } from './js/api.js';
import { toast } from './js/toast.js';
import { openOptions, closeOptions } from './js/options-sheet.js';
import {
  requestWakeLock, toggleFullscreen, onFullscreenChange,
  openPinSheet, confirmPin, resetIdleTimer, lockScreen, renderPinDots, renderPinPad, submitPin,
} from './js/kiosk-lock.js';
import {
  loadZones, loadTables, renderZones, renderTables, showScreen, openTable,
} from './js/screens/tables.js';
import { openPartySizeSheet } from './js/sheets/party-size.js';
import { loadFeatures, featureEnabled } from './js/features.js';
import {
  lastRound, renderBill, openBill, closeBill, loadOrder,
} from './js/sheets/bill.js';
import { renderCategories, renderProducts, addToDraft, afterDraftChange } from './js/screens/menu.js';
import {
  openProductSheet, chosenModifiers, updateProductSheetTotal, toggleModifier,
} from './js/sheets/product-sheet.js';
import {
  openVoidSheet, confirmVoid, openVoidSentSheet, confirmVoidSent, openCompSheet, confirmComp,
} from './js/sheets/void-comp.js';
import { openHelpRequestSheet, confirmHelpRequest } from './js/sheets/help-request.js';
import {
  openTransferSheet, confirmTransfer, openTransferServerSheet, confirmTransferServer,
} from './js/sheets/transfer.js';
import {
  loadPending, renderPendingBanner, openPendingSheet, resolvePendingGuarded,
  openSendToCashierSheet, confirmSendToCashier,
} from './js/sheets/pending-orders.js';
import { openFailedOrdersSheet } from './js/sheets/failed-orders.js';

(function () {
  'use strict';

  // ══ Turkish dictionaries ═══════════════════════════════════════════
  // Enum values arrive from the server in English and are never printed raw.
  // (TABLE_STATUS moved to js/screens/tables.js, KITCHEN_STATE to
  // js/sheets/bill.js, VOID_REASONS/COMP_REASONS to js/sheets/void-comp.js,
  // each's own only reader.)

  // ══ Sign-in and session ════════════════════════════════════════════

  // V1-RMD-172: found by the 2026-09-10 Garson audit — a reload while
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
  //
  // V1-RMD-178: found by independent review — the code did not actually
  // match that last sentence. api() only sets `offline` on a network-level
  // failure (fetch itself threw); a response that DID come back but with a
  // transient server error (500/503, or 429 from this route's own rate
  // limit) fell through the same `!result.ok` branch as a real 401/403 and
  // was treated as "not logged in" — the exact login-overlay-while-still-
  // valid bug this task exists to prevent, just triggered by a busy server
  // instead of a dead network. Only 401/403 are a real "the session is
  // gone"; every other failure (including a genuine network drop) means
  // "cannot tell" and must not force a re-login.
  async function hasValidSession() {
    const result = await api(`/api/v1/auth/session?terminalId=${state.terminalId}`);
    if (result.ok) {
      applyUser(result.data);
      return 'yes';
    }
    if (result.status === 401 || result.status === 403) return 'no';
    return 'offline';
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
      // V1-RMD-172: found by the 2026-09-10 Garson audit — this said
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

  // V1-RMD-172: one accurate Turkish sentence per real reason offline mode
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

  // V1-RMD-175: found by the 2026-09-10 Garson audit — the catalog was
  // fetched once at start() and never again for the rest of the shift.
  // Tables/pending orders get refetched on every action that touches them,
  // but nothing a waiter does naturally re-triggers a catalog reload, so a
  // manager changing a price or marking something out mid-shift never
  // reached an already-open app. catalogLoadedAt lets the visibilitychange
  // handler below refresh it opportunistically without hammering the
  // server every time the tab briefly regains focus.
  let catalogLoadedAt = 0;
  const CATALOG_REFRESH_INTERVAL_MS = 15 * 60 * 1000;

  async function loadCatalog() {
    // V1-RMD-129: categories are derived from this one response's own
    // categoryCode/categoryName. There is no separate categories endpoint a
    // waiter session may call.
    const result = await fetchWholeCatalogAsync();
    if (!result.ok) {
      // V1-RMD-178: found by independent review of V1-RMD-176 — this
      // unconditional clear was harmless while loadCatalog() only ran at
      // start()/coming back online (nothing worked yet to destroy). Once
      // V1-RMD-176 started calling it opportunistically mid-shift
      // (refreshCatalogIfStaleAsync), the exact same line meant one
      // transient failure (a 429, a dropped connection) on an ordinary
      // background refresh blanked a working menu the waiter was actively
      // using. Only the very first load has nothing to protect; any later
      // one (catalogLoadedAt already set) leaves the last good catalog in
      // place and simply tries again on the next opportunity.
      if (catalogLoadedAt === 0) {
        state.products = [];
        state.categories = [];
      }
      return;
    }
    catalogLoadedAt = Date.now();
    const list = result.items;
    state.products = list.map((product) => ({
      id: product.productId,
      name: product.name,
      price: product.unitPrice || 0,
      categoryCode: product.categoryCode,
      categoryName: product.categoryName,
      // V1-RMD-148: the option groups the server says this product has. The
      // client never invents one and never prices one.
      modifierGroups: product.modifierGroups || [],
      // V1-WTR-017: manager-entered estimate, minutes. null when never set -
      // such a product is excluded from the pre-send delay check, not
      // treated as zero.
      prepTimeMinutes: product.prepTimeMinutes != null ? product.prepTimeMinutes : null
    }));

    const seen = new Map();
    for (const product of list) {
      if (product.categoryCode && !seen.has(product.categoryCode)) {
        seen.set(product.categoryCode, product.categoryName || product.categoryCode);
      }
    }
    state.categories = Array.from(seen, ([id, name]) => ({ id, name }));
  }

  async function refreshCatalogIfStaleAsync() {
    if (!state.isOnline) return;
    if (Date.now() - catalogLoadedAt < CATALOG_REFRESH_INTERVAL_MS) return;
    await loadCatalog();
    renderCategories();
    renderProducts();
    // V1-WTR-016: a held draft's price-changed badges (renderDraftLine)
    // read straight from state.products, so they only actually appear
    // once this runs - without it a stale price could sit unflagged until
    // some unrelated action happened to call renderBill() next.
    renderBill();
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
  function draftToPayload(items) {
    return {
      id: randomUUID(),
      tableId: state.table.id,
      tableNumber: state.table.number,
      // V1-WTR-015: only takes effect server-side on this table's FIRST
      // round (CreateTableDraftRequest's own doc comment) - sending it on
      // every later round too is harmless, not a correction.
      partySize: state.draftPartySize || null,
      items: (items || state.draft).map((line) => ({
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
        specialInstructions: line.note || null,
        // V1-WTR-022: the server only ever trusts this once it re-validates
        // the seat actually belongs to this table (OrderManagementStore) -
        // a stale/foreign id here is simply ignored server-side, never an
        // error this client needs to pre-check.
        seatId: line.seatId || null,
        courseNumber: line.courseNumber || null
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
    // Found by the WaiterPwa E2E audit (2026-09-12): a bare `headers,`
    // shorthand property here referenced no variable in scope at all - a
    // leftover from V1-RMD-163's removal of a duplicate X-Idempotency-Key
    // header (that removal deleted the header's VALUE but missed this one
    // remaining reference to it). Evaluating the request options object
    // threw `ReferenceError: headers is not defined` synchronously, before
    // fetch() ever ran - table-draft always succeeded, but submit-draft
    // never even attempted, silently. Every single "Gönder" click was
    // broken: the order never reached the kitchen, with no error shown to
    // the waiter (sendDraft()'s try/finally has no catch, so the thrown
    // error just propagated out of the click handler unseen). No unit or
    // HTTP test catches this class of bug - none of them execute this
    // actual browser script; only running it in a real browser does.
    return api(apiUrl(`/orders/${draft.data.orderId}/submit-draft`), {
      method: 'POST',
      body: {
        orderId: draft.data.orderId,
        expectedRowVersion: draft.data.rowVersion,
        operationId: `${draft.data.orderId}:${payload.id}`
      }
    });
  }

  // V1-TBL-010 follow-up (Semih, 2026-09-12: "başka iş yükü artışı yaptığımız
  // ne varsa bul"): V1-WTR-017's "hafif gecikme kontrolü" used to sit here,
  // intercepting BOTH send buttons with a forced "gönder birlikte / ayrı
  // gönder" choice whenever a round's items had prep-time estimates ≥10
  // minutes apart - a very common shape (any starter + main combination).
  // Removed outright rather than tuned: V1-WTR-025's course system already
  // solves the same "food shouldn't all arrive unevenly" problem, more
  // deliberately (a waiter who cares assigns courses; FireRound/FireCourse
  // stagger the actual kitchen dispatch) and without ever touching
  // prepTimeMinutes - the two mechanisms were solving the same problem
  // twice, and this one fired on every send regardless of whether the
  // waiter had already handled staggering via courses. Send is a single
  // action again, exactly like every other quick-add path.
  async function sendDraft() {
    if (state.draft.length === 0 || !state.table || state.sendInFlight) return;
    state.sendInFlight = true;
    el.btnSendFromMenu.disabled = true;
    el.btnSendFromBill.disabled = true;

    const targetItems = state.draft;
    const payload = draftToPayload(targetItems);
    const tableNumber = state.table.number;
    try {
      if (!state.isOnline) {
        queueOrder(payload);
        removeSentDraftLines(targetItems);
        afterDraftChange();
        toast(`Bağlantı yok — ${tableNumber} siparişi kuyruğa alındı.`, { warning: true });
        return;
      }

      const result = await postOrder(payload);
      if (result.ok) {
        removeSentDraftLines(targetItems);
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
        removeSentDraftLines(targetItems);
        afterDraftChange();
        toast(`Sunucuya ulaşılamadı — ${tableNumber} siparişi kuyruğa alındı.`, { warning: true });
      }
    } finally {
      state.sendInFlight = false;
      afterDraftChange();
    }
  }

  function removeSentDraftLines(sentItems) {
    const sentIds = new Set(sentItems.map((line) => line.id));
    state.draft = state.draft.filter((line) => !sentIds.has(line.id));
    state.draftEpoch += 1;
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

  // ══ Party size (kaç kişi) ═══════════════════════════════════════════
  // openPartySizeSheet/partySizeSheetHtml moved to js/sheets/party-size.js
  // (V1-WTR-044). confirmPartySize stays here — it calls renderBill(),
  // which is not yet its own module.

  function confirmPartySize() {
    const context = state.optionsContext;
    if (!context) return;
    state.draftPartySize = context.partySize;
    closeOptions();
    renderBill();
  }

  // V1-WTR-025: calls in one Held course — the explicit "ateşle" action
  // the full course model needs once the table is ready for it. No
  // confirmation sheet (unlike void/comp): this is routine kitchen
  // dispatch, the same class of action as submit-draft, not a discretionary
  // exception.
  async function fireCourse(courseNumber) {
    if (!state.order) return;
    const result = await api(apiUrl(`/orders/${state.order.orderId}/fire-course`), {
      method: 'POST',
      body: { courseNumber }
    });
    if (!result.ok) { toast(result.message, { warning: true }); return; }
    toast(`${courseNumber}. kurs mutfağa ateşlendi.`);
    await loadOrder(state.table.id);
    renderBill();
  }

  // ══ Failed / queued orders ═══════════════════════════════════════════
  // openFailedOrdersSheet/queuedOrderRow moved to js/sheets/failed-orders.js
  // (V1-WTR-050). dismissFailedOrder/clearAllFailedOrders stay here — both
  // call persistQueue(), which is not yet its own module.

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
    // V1-RMD-170: found by the 2026-09-10 Garson audit — if
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
      // V1-RMD-170: a bare `await navigator.serviceWorker.ready` still has
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

  // ══ Profile, PIN and the kiosk lock ════════════════════════════════
  // (Full screen/wake lock and the PIN kiosk lock itself moved to
  // js/kiosk-lock.js — V1-WTR-042. openProfileSheet/openShiftSummarySheet
  // stay here for now, their own future module.)

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
          <button type="button" class="opt" data-profile="transfer-server">
            <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-move"/></svg></span>
            <span class="opt-name">Açık masaları devret</span>
          </button>
          ${featureEnabled('shiftSummary') ? `
          <button type="button" class="opt" data-profile="shift-summary">
            <span class="opt-box is-round"><svg class="icon" aria-hidden="true"><use href="#ico-seats"/></svg></span>
            <span class="opt-name">Vardiya özetim</span>
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

  // V1-WTR-021: garson-karsilastirma idea #9, "yalnızca kendine görünen
  // vardiya özeti". The server already scopes /my-shift-summary to the
  // calling session (RequireCashierSessionAsync) - there is no manager
  // view or other-waiter lookup to accidentally build here.
  async function openShiftSummarySheet() {
    openOptions('shift-summary', 'Vardiya özetim', 'Bugün (UTC gün başlangıcından beri)',
      '<div class="empty">Yükleniyor…</div>', '', '', '');
    el.optionsConfirm.hidden = true;

    const result = await api(apiUrl('/orders/my-shift-summary'));
    if (!result.ok) {
      el.optionsBody.innerHTML = `<div class="callout">
        <svg class="icon" aria-hidden="true"><use href="#ico-alert"/></svg>
        <span>${escapeHtml(result.message || 'Vardiya özeti alınamadı.')}</span>
      </div>`;
      return;
    }

    const summary = result.data;
    el.optionsBody.innerHTML = `
      <div class="opts">
        <div class="shift-summary-row">
          <span class="shift-summary-label">Satış toplamım</span>
          <span class="shift-summary-value">${formatMoney(summary.salesTotal)}</span>
        </div>
        <div class="shift-summary-row">
          <span class="shift-summary-label">Kullandığım ikram bütçesi</span>
          <span class="shift-summary-value">${formatMoney(summary.compUsed)}</span>
        </div>
        <div class="shift-summary-row">
          <span class="shift-summary-label">Bahşiş havuzu payım</span>
          <span class="shift-summary-value">${formatMoney(summary.tipPoolShare)}</span>
        </div>
      </div>
      <p class="shift-summary-note">
        Bahşiş havuzu payı: bugün toplanan ${formatMoney(summary.tipPoolTotal)} bahşiş,
        bugün en az bir sipariş alan ${summary.waitersWorkedToday} garson arasında eşit bölünür.
      </p>`;
  }

  // ══ Events ═════════════════════════════════════════════════════════

  function bindEvents() {
    // V1-RMD-173: the sheet is a permanent DOM node CSS moves off-screen,
    // so before it has ever been opened once it would otherwise still be
    // a live tab stop.
    el.optionsSheet.inert = true;
    el.loginForm.addEventListener('submit', submitLogin);
    el.btnProfile.addEventListener('click', openProfileSheet);

    window.addEventListener('online', () => {
      state.isOnline = true;
      renderRibbon();
      void flushQueue();
      // V1-RMD-172: if the app started offline (hasValidSession() could
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
      // V1-RMD-175: same "coming back to the app" moment, opportunistically
      // catches up a catalog that has sat unrefreshed since start().
      void refreshCatalogIfStaleAsync();
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
    // V1-RMD-173: found by the 2026-09-10 Garson audit — none of the
    // sheets responded to Escape at all. The PIN lock is deliberately
    // excluded (Escape must never be a way out of it — enforced inside
    // closeOptions() itself as of V1-RMD-178, not just here) and the bill
    // sheet is a fixed column on tablet rather than a dismissable modal,
    // so only the options sheet closes here.
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
    el.btnPartySize.addEventListener('click', openPartySizeSheet);
    el.btnHelpRequest.addEventListener('click', openHelpRequestSheet);
    el.btnMoveTable.addEventListener('click', openTransferSheet);
    el.btnSendToCashier.addEventListener('click', openSendToCashierSheet);
    el.btnSendFromMenu.addEventListener('click', () => void sendDraft());
    el.btnSendFromBill.addEventListener('click', () => void sendDraft());
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
      // V1-RMD-174: found by the 2026-09-10 Garson audit — addToDraft()
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

      // V1-WTR-016: found in "Yeni fikirler" ideation (Katman A, madde 4) —
      // a draft line's price is cached at the moment it was added; if the
      // catalog refreshes in the background (V1-RMD-176) while it still
      // sits unsent, the price on screen can go stale. The server always
      // re-resolves the real price from the catalog at submit time
      // regardless (OrderManagementStore.CreateTableDraftAsync never
      // trusts the client's own unitPrice) — so this was never a money
      // bug, only a "the waiter got surprised by the real total" one.
      const updatePrice = event.target.closest('[data-update-price]');
      if (updatePrice) {
        const line = state.draft.find((candidate) => candidate.id === updatePrice.dataset.updatePrice);
        const product = line && state.products.find((candidate) => candidate.id === line.productId);
        if (line && product) {
          line.price = product.price;
          afterDraftChange();
        }
        return;
      }

      const voidButton = event.target.closest('[data-void]');
      if (voidButton) { openVoidSheet(voidButton.dataset.void); return; }

      const voidSentButton = event.target.closest('[data-void-sent]');
      if (voidSentButton) { openVoidSentSheet(voidSentButton.dataset.voidSent); return; }

      const compButton = event.target.closest('[data-comp]');
      if (compButton) { openCompSheet(compButton.dataset.comp); return; }

      const fireCourseButton = event.target.closest('[data-fire-course]');
      if (fireCourseButton) void fireCourse(Number(fireCourseButton.dataset.fireCourse));
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

    const seat = event.target.closest('[data-seat]');
    if (seat && state.optionsMode === 'product') {
      state.optionsContext.seatId = seat.dataset.seat || null;
      el.optionsBody.querySelectorAll('[data-seat]').forEach((button) => {
        button.setAttribute('aria-pressed', String((button.dataset.seat || null) === state.optionsContext.seatId));
      });
      return;
    }

    const course = event.target.closest('[data-course]');
    if (course && state.optionsMode === 'product') {
      state.optionsContext.courseNumber = course.dataset.course ? Number(course.dataset.course) : null;
      el.optionsBody.querySelectorAll('[data-course]').forEach((button) => {
        button.setAttribute('aria-pressed',
          String((button.dataset.course ? Number(button.dataset.course) : null) === state.optionsContext.courseNumber));
      });
      return;
    }

    const modifier = event.target.closest('[data-modifier]');
    if (modifier && state.optionsMode === 'product') {
      toggleModifier(modifier);
      return;
    }

    const partyStep = event.target.closest('[data-party-step]');
    if (partyStep && state.optionsMode === 'party-size') {
      const delta = partyStep.dataset.partyStep === '+' ? 1 : -1;
      state.optionsContext.partySize = Math.min(50, Math.max(1, state.optionsContext.partySize + delta));
      const valueEl = el.optionsBody.querySelector('#partySizeValue');
      if (valueEl) valueEl.textContent = String(state.optionsContext.partySize);
      return;
    }

    const reason = event.target.closest('[data-reason]');
    if (reason && (state.optionsMode === 'void' || state.optionsMode === 'void-sent' || state.optionsMode === 'comp' || state.optionsMode === 'help-request')) {
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
    if (target && (state.optionsMode === 'transfer' || state.optionsMode === 'transfer-server')) {
      state.optionsContext.targetId = target.dataset.target;
      el.optionsBody.querySelectorAll('[data-target]').forEach((button) => {
        button.setAttribute('aria-pressed', String(button === target));
      });
      el.optionsConfirm.disabled = false;
      return;
    }

    // V1-RMD-175: found by the 2026-09-10 Garson audit — these two buttons
    // had no double-tap guard; a fast double-tap fired two concurrent
    // resolvePending() calls for the same order (the server's own
    // row_version check would reject the second as a conflict, but the
    // waiter still saw a spurious error toast from what looked like one
    // tap). The pair is disabled together since accepting/rejecting either
    // one settles the same order.
    const accept = event.target.closest('[data-accept]');
    if (accept) { void resolvePendingGuarded(accept, accept.dataset.accept, true); return; }
    const reject = event.target.closest('[data-reject]');
    if (reject) { void resolvePendingGuarded(reject, reject.dataset.reject, false); return; }

    const profile = event.target.closest('[data-profile]');
    if (profile) {
      const action = profile.dataset.profile;
      if (action === 'signout') { closeOptions(); void signOut(); }
      else if (action === 'fullscreen') { closeOptions(); void toggleFullscreen(); }
      else if (action === 'push') { closeOptions(); void (state.pushEnabled ? disablePush() : enablePush()); }
      else if (action === 'pin-off') openPinSheet(true);
      else if (action === 'transfer-server') void openTransferServerSheet();
      else if (action === 'shift-summary') void openShiftSummarySheet();
      else if (state.pinArmed) { closeOptions(); lockScreen(); }
      else openPinSheet(false);
    }
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
      addToDraft(context.product, context.quantity, chosenModifiers(), noteField ? noteField.value.trim() : '', context.seatId, context.courseNumber);
      closeOptions();
      return;
    }
    if (state.optionsMode === 'void') { void confirmVoid(); return; }
    if (state.optionsMode === 'void-sent') { void confirmVoidSent(); return; }
    if (state.optionsMode === 'comp') { void confirmComp(); return; }
    if (state.optionsMode === 'help-request') { void confirmHelpRequest(); return; }
    if (state.optionsMode === 'party-size') { confirmPartySize(); return; }
    if (state.optionsMode === 'cashier') { void confirmSendToCashier(); return; }
    if (state.optionsMode === 'transfer') { void confirmTransfer(); return; }
    if (state.optionsMode === 'transfer-server') { void confirmTransferServer(); return; }
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
    await loadFeatures();
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
    // V1-WTR-019: the age badge is computed client-side from the already-
    // loaded openedAt, so ticking it forward needs only a re-render, not a
    // re-fetch - cheap even when the tables screen is not the one showing.
    window.setInterval(renderTables, 60000);
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
