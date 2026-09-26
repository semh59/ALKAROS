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
import { isFullscreen } from './js/util.js';
import { state, el, measureChrome, renderRibbon } from './js/state.js';
import { applyUser, releaseTrap, showLogin } from './js/auth.js';
import { apiUrl, api } from './js/api.js';
import { toast } from './js/toast.js';
import { closeOptions } from './js/options-sheet.js';
import {
  requestWakeLock, toggleFullscreen, onFullscreenChange,
  openPinSheet, confirmPin, resetIdleTimer, lockScreen, reapplyPersistedLock, renderPinDots, renderPinPad, submitPin,
} from './js/kiosk-lock.js';
import {
  loadZones, loadTables, renderZones, renderTables, showScreen, openTable,
} from './js/screens/tables.js';
import { openPartySizeSheet, confirmPartySize } from './js/sheets/party-size.js';
import { loadFeatures } from './js/features.js';
import {
  lastRound, renderBill, openBill, closeBill, fireCourse,
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
import { openFailedOrdersSheet, dismissFailedOrder, clearAllFailedOrders } from './js/sheets/failed-orders.js';
import { sendDraft, flushQueue } from './js/offline-queue.js';
import { refreshPushState, enablePush, unsubscribePush, disablePush } from './js/push.js';
import { openProfileSheet, openShiftSummarySheet } from './js/sheets/profile.js';

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
  // renderRibbon/OFFLINE_DISABLED_REASONS moved to js/state.js (V1-WTR-053)
  // — a reader of four different state fields, not specifically an
  // offline-queue concern.

  const MAX_WORKER_REGISTRATION_ATTEMPTS = 5;
  const WORKER_REGISTRATION_RETRY_MS = 2000;
  let workerRegistrationAttempts = 0;

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
    // V1-RMD-330: a registration that fails once (typically the network dropping while sw.js downloads) used
    // to disable offline mode for good. It is retried when the connection returns and after a growing wait,
    // a few times, and a later success clears the "kurulamadı" state.
    workerRegistrationAttempts += 1;
    navigator.serviceWorker.register('./sw.js').then(() => {
      if (state.offlineDisabledReason === 'registration-failed') {
        state.offlineDisabled = false;
        state.offlineDisabledReason = null;
        renderRibbon();
      }
    }).catch((err) => {
      state.offlineDisabled = true;
      state.offlineDisabledReason = 'registration-failed';
      renderRibbon();
      console.warn('Service worker registration failed; offline mode is disabled:', err);
      if (workerRegistrationAttempts >= MAX_WORKER_REGISTRATION_ATTEMPTS) return;
      let timer = 0;
      const retry = () => {
        window.clearTimeout(timer);
        window.removeEventListener('online', retry);
        registerOfflineWorker();
      };
      timer = window.setTimeout(retry, WORKER_REGISTRATION_RETRY_MS * 2 ** (workerRegistrationAttempts - 1));
      window.addEventListener('online', retry);
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
      prepTimeMinutes: product.prepTimeMinutes != null ? product.prepTimeMinutes : null,
      // V1-WTR-054/055: how many more units the mapped stock can still
      // cover, null when the product isn't stock-tracked (unlimited). A
      // product the server computed at 0 or less never appears in `list`
      // at all - the catalog query already dropped it.
      remainingCount: product.remainingCount != null ? product.remainingCount : null
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
              // V1-RMD-188: found by the 2026-09-12 five-agent independent
              // Garson audit — this always said "masa değişti" (table
              // changed), even when the table was still the SAME one and
              // draftEpoch had advanced for the other reason it exists
              // for (see state.js): the round itself was sent or cleared
              // while the toast was still showing. Telling a waiter
              // standing at the same table their table changed is simply
              // wrong; the two cases now get their own accurate message.
              if (!state.table || state.table.id !== ownerTableId) {
                toast(`${removed.name} geri alınamadı, masa değişti.`, { warning: true });
                return;
              }
              if (state.draftEpoch !== epoch) {
                toast(`${removed.name} geri alınamadı, tur değişti.`, { warning: true });
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

  // V1-RMD-285: the connection used to give up for good after five automatic
  // retries (~19 s) and a failed first start() was swallowed, so a waiter
  // whose server blipped at page load never got live updates again. Retry
  // forever with a capped exponential delay, and reload what was missed
  // (pending QR orders) whenever the connection comes back.
  const HUB_RETRY_CAP_MS = 15000;
  const hubRetryDelay = (attempt) => Math.min(HUB_RETRY_CAP_MS, 1000 * 2 ** Math.min(attempt, 4));
  let hubStartAttempt = 0;

  function setLiveState(next) {
    if (state.liveState === next) return;
    state.liveState = next;
    renderRibbon();
  }

  async function catchUpAfterReconnect() {
    try { await loadPending(); } catch { /* the next event or reconnect retries */ }
  }

  function startHub() {
    if (!hub) return;
    hub.start().then(() => {
      hubStartAttempt = 0;
      setLiveState('connected');
      void catchUpAfterReconnect();
    }).catch(() => {
      setLiveState('reconnecting');
      window.setTimeout(startHub, hubRetryDelay(hubStartAttempt++));
    });
  }

  function connectHub() {
    if (hub || typeof signalR === 'undefined') return;
    hub = new signalR.HubConnectionBuilder()
      .withUrl(`/hubs/waiter-order-status?terminalId=${state.terminalId}`)
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (context) => hubRetryDelay(context.previousRetryCount) })
      .configureLogging(signalR.LogLevel.Warning)
      .build();
    hub.onreconnecting(() => setLiveState('reconnecting'));
    hub.onreconnected(() => {
      hubStartAttempt = 0;
      setLiveState('connected');
      void catchUpAfterReconnect();
    });
    // Not reachable with the policy above, kept so a future policy change
    // cannot silently leave the waiter without live updates.
    hub.onclose(() => {
      setLiveState('down');
      window.setTimeout(startHub, hubRetryDelay(hubStartAttempt++));
    });

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

    startHub();
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
    reapplyPersistedLock();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
