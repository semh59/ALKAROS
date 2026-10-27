// V13-PUI-001: Hesap Ödeme (split payment) standalone sayfası.
// cashier-app.js / cash-session.js'in kendi konvansiyonlarıyla aynı desen:
// bundler yok, IIFE modül, credentials:'include' fetch, Türkçe hata metinleri.
//
// Bu sayfa ?billId=... sorgu parametresiyle açılır. cashier-app.js bugün
// hiçbir Bill kavramı taşımıyor (sipariş girişiyle sınırlı) - cash-session.js
// da aynı şekilde hiçbir yerden bağlantı verilmeden, doğrudan URL ile açılan
// bağımsız bir sayfa; bu görev de aynı, zaten var olan deseni takip ediyor,
// yeni bir sayfa-arası gezinme mekanizması icat etmiyor.
(function () {
  'use strict';

  var money = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' });
  var app = document.getElementById('app');

  var METHOD_LABELS = { Cash: 'Nakit', BankCard: 'Kredi/Banka Kartı', Eft: 'EFT/Havale', Account: 'Hesaba yaz (cari)' };

  var state = {
    phase: 'checking',
    terminalId: null,
    billId: null,
    summary: null,
    cashSessionId: null,
    cashSessionOpen: false,
    selectedMethod: 'Cash',
    splitCount: 2,
    splitLeft: 0,
    savedShares: [],
    amountDraft: '0.00',
    noteDraft: '',
    resolveReason: '',
    userId: null,
    slipDraft: '',
    decisionNote: '',
    // V13-PUI-004: EFT/Havale onay kutusu - kasiyer tutarı işletmenin banka
    // hesap hareketinde GÖRDÜĞÜNÜ işaretlemeden "Ödemeyi Ekle" pasif kalır.
    // Yöntem değiştikçe veya her başarılı gönderimden sonra sıfırlanır -
    // bir sonraki EFT tahsilatı kendi onayını taze istemeli.
    eftConfirmed: false,
    locked: false,
    busy: false,
    error: null,
    lastResult: null,
    // V1-RMD-314 (independent 2026-09-26 audit, finding K3): the tender idempotency key for the CURRENT
    // amount/method attempt - generated once (lazily, in submitTender) and kept across a retry (a network
    // failure where the request may have actually landed server-side), so "tekrar dene" replays instead of
    // minting a fresh key that the server can never recognise as the same attempt. Cleared on success (a
    // genuinely new tender needs a fresh key) and whenever the method or amount actually changes.
    tenderIdempotencyKey: null,
    // V1-RMD-292: billing.bill_adjustments summary/list - discount, voluntary tip and any other fee already
    // applied to this bill (billing/bills/{id}/adjustments, GET only for this task; discount/tip are the only
    // two write endpoints this task adds a client for).
    adjustmentSummary: null,
    adjustmentItems: [],
    discountReason: 'ManagerDiscretion',
    discountCalcType: 'Percentage',
    discountValueDraft: '',
    discountNoteDraft: '',
    discountBusy: false,
    discountConfirm: false,
    discountError: null,
    discountNotice: null,
    tipAmountDraft: '',
    tipNoteDraft: '',
    tipBusy: false,
    tipError: null,
    // V1-RMD-314 (finding K3): same retry-reuses-the-same-key fix as tenderIdempotencyKey above, for the
    // discount/tip apply requests.
    discountIdempotencyKey: null,
    refundRequestKey: null,
    refundRequestSignature: null,
    tipIdempotencyKey: null,
    // V1-RMD-443: "Hesaba yaz" - the customer whose receivable account this bill (or part of it) is written to.
    customerSearch: '',
    customers: null,
    customersLoading: false,
    selectedCustomerId: null,
    // İptal ve iade: kasiyerin gerekçeyle hesabı iptal etmesi, ödemeyi iade istemesi ve (yetkiliyse) isteği onaylaması.
    cancelReason: '',
    refundRequests: null,
    refundReasons: {},
    refundSlips: {},
    refundNotes: {},
    refundBusy: false,
    refundError: null,
    refundNotice: null,
    refundListFailed: false,
    refundAllowance: null,
    billItems: [],
    itemQty: {},
    itemPayment: '',
    itemStock: '',
    itemReason: '',
    confirmKey: null,
    reasonChoices: {},
    tenderedDraft: '',
    changeDue: null,
    receiptError: null,
    receiptBusy: false,
  };

  function escapeHtml(value) {
    return String(value == null ? '' : value).replace(/[&<>"']/g, function (ch) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
    });
  }

  function formatMoney(amount) {
    return money.format(Number(amount || 0));
  }

  function queryParam(name) {
    return new URLSearchParams(window.location.search).get(name);
  }

  function describeHttpFailure(status, body) {
    if (body && body.error && body.error.message) return body.error.message;
    if (status === 401) return 'Oturumunuz sona ermiş. Lütfen tekrar giriş yapın.';
    if (status === 403) return 'Bu işlem için yetkiniz yok.';
    if (status === 404) return 'Kayıt bulunamadı.';
    if (status === 409) return 'İşlem, hesabın güncel durumuyla çakışıyor. Sayfayı yenileyip tekrar deneyin.';
    if (status === 429) return 'Çok fazla istek gönderildi. Lütfen birkaç saniye bekleyin.';
    if (status >= 500) return 'Sunucu tarafında bir sorun oluştu. Lütfen tekrar deneyin.';
    return 'İstek tamamlanamadı.';
  }

  function api(path, options) {
    options = options || {};
    return fetch(path, {
      method: options.method || 'GET',
      credentials: 'include',
      headers: options.body ? Object.assign({ 'Content-Type': 'application/json' }, options.headers) : undefined,
      body: options.body ? JSON.stringify(options.body) : undefined,
      signal: typeof AbortSignal !== 'undefined' && AbortSignal.timeout ? AbortSignal.timeout(15000) : undefined,
    }).then(function (response) {
      // V1-RMD-291: one place catches a dropped session, from ANY call, not
      // just the bootstrap check - before this a 401 from a mid-use action
      // (resolve/claim/decide/tender below) only added an inline message to
      // the already-stale 'ready' screen; nothing sent the cashier back to
      // where they could actually sign in again.
      if (response.status === 401 && state.phase !== 'login') {
        state.phase = 'login';
        render();
      }
      if (response.status === 204) return { ok: true, status: response.status, body: null };
      return response.json().catch(function () { return null; }).then(function (body) {
        return { ok: response.ok, status: response.status, body: body };
      });
    });
  }

  function tendersBase() {
    return '/api/v1/terminals/' + state.terminalId + '/billing/bills/' + state.billId + '/tenders/';
  }

  function billBase() {
    return '/api/v1/terminals/' + state.terminalId + '/billing/bills/' + state.billId;
  }

  // V1-RMD-292: DiscountReasonCatalog's own codes (ALKAROS.Billing.Adjustments) - the server rejects any
  // other value, so this list is exhaustive by construction, not a guess.
  var DISCOUNT_REASON_LABELS = {
    CustomerLoyalty: 'Müşteri sadakati',
    PromotionalOffer: 'Promosyon',
    ServiceRecovery: 'Hizmet telafisi (şikayet/hata düzeltmesi)',
    ManagerDiscretion: 'Yönetici takdiri / düzeltme',
  };

  // AdjustmentType's own enum (ALKAROS.Billing.Adjustments) - every value GET .../adjustments can return.
  var ADJUSTMENT_TYPE_LABELS = {
    DiscountPercentage: 'İndirim (%)',
    DiscountAmount: 'İndirim (tutar)',
    ServiceFee: 'Ücret',
    Kuver: 'Kuver',
    Tip: 'Bahşiş',
    CustomFee: 'Özel ücret',
  };

  function setBusy(value) { state.busy = value; render(); }
  function setError(message) { state.error = message; render(); }

  // V1-RMD-378 (module-by-module UI audit round 2, T7 - rol-arası haberleşme): a card payment
  // "onay bekliyor" needs a SECOND, different manager to approve it (V1-RMD-283's own two-person
  // rule) - but nothing here ever refreshed while this page sat open waiting. A manager watching
  // this exact screen for the other manager's decision (on a different terminal) saw only the
  // stale "Onay bekliyor" state forever, with no way to know it resolved short of manually
  // reloading. Polls only while a real pending confirmation exists - the one moment a SECOND
  // person's action anywhere else in the system can change what this screen should show with no
  // action from whoever is looking at it.
  var pendingConfirmationPollTimer = null;
  // The poll redraws the whole page; put the caret back so a cashier typing a note is not thrown out of the field.
  function renderKeepingFocus() {
    var active = document.activeElement;
    var id = active && app.contains(active) ? active.id : '';
    var start = id && typeof active.selectionStart === 'number' ? active.selectionStart : null;
    var end = id && typeof active.selectionEnd === 'number' ? active.selectionEnd : null;
    render();
    var again = id ? document.getElementById(id) : null;
    if (!again) return;
    again.focus();
    if (start !== null && again.setSelectionRange) again.setSelectionRange(start, end);
  }
  function ensurePendingConfirmationPoll() {
    var pending = state.summary && state.summary.unsettledPayment && state.summary.unsettledPayment.pendingConfirmation;
    if (pending && !pendingConfirmationPollTimer) {
      pendingConfirmationPollTimer = window.setInterval(function () {
        refreshSummary().then(renderKeepingFocus);
      }, 5000);
    } else if (!pending && pendingConfirmationPollTimer) {
      window.clearInterval(pendingConfirmationPollTimer);
      pendingConfirmationPollTimer = null;
    }
  }

  // ---- bootstrap -----------------------------------------------------

  function bootstrap() {
    state.billId = queryParam('billId');
    if (!state.billId) {
      state.phase = 'missingBill';
      render();
      return;
    }
    api('/api/v1/auth/session/current').then(function (result) {
      if (!result.ok) {
        if (result.status === 401 || result.status === 403) {
          state.phase = 'login';
        } else {
          state.error = describeHttpFailure(result.status, result.body);
          state.phase = 'loadError';
        }
        render();
        return;
      }
      state.terminalId = result.body.terminalId;
      state.userId = result.body.userId || null;
      loadEverything();
    }).catch(function () {
      state.error = 'Sunucuya ulaşılamadı. Bağlantınızı kontrol edip yeniden deneyin.';
      state.phase = 'loadError';
      render();
    });
  }

  // The split saved on the POS screen (who owes what) becomes one-tap amounts here; a failure just hides them.
  function loadSavedShares() {
    var designBase = billBase() + '/split-design';
    Promise.all([api(designBase), api(designBase + '/owners')]).then(function (results) {
      if (!results[0].ok || !results[0].body) return;
      var labels = {};
      (results[1].ok && Array.isArray(results[1].body) ? results[1].body : []).forEach(function (owner) {
        labels[owner.ownerId] = owner.label;
      });
      var totals = {};
      var order = [];
      (results[0].body.allocations || []).forEach(function (allocation) {
        var id = allocation.ownerId || allocation.legacyOwnerReference;
        if (!id) return;
        if (!(id in totals)) { totals[id] = 0; order.push(id); }
        totals[id] += Number(allocation.amount) || 0;
      });
      state.savedShares = order.map(function (id) {
        return { label: labels[id] || 'Kişi ' + (order.indexOf(id) + 1), amount: Math.round(totals[id] * 100) / 100 };
      });
      render();
    }).catch(function () { state.savedShares = []; });
  }

  function loadEverything() {
    Promise.all([
      api(tendersBase()),
      api('/api/v1/terminals/' + state.terminalId + '/cash-sessions/active'),
      api(billBase() + '/adjustments'),
    ]).then(function (results) {
      var summaryResult = results[0];
      var cashResult = results[1];
      var adjustmentsResult = results[2];

      if (summaryResult.status === 404) {
        state.phase = 'missingBill';
        render();
        return;
      }
      if (summaryResult.status === 401) {
        state.phase = 'login';
        render();
        return;
      }
      if (!summaryResult.ok) {
        state.error = describeHttpFailure(summaryResult.status, summaryResult.body);
        state.phase = 'loadError';
        render();
        return;
      }
      state.error = null;

      state.summary = summaryResult.body;
      state.cashSessionOpen = cashResult.ok;
      state.cashSessionId = cashResult.ok ? cashResult.body.cashSessionId : null;
      if (!state.cashSessionOpen) state.selectedMethod = 'Eft';
      applyAdjustmentsResult(adjustmentsResult);

      // V1-RMD-258/V13-RMD-002: the server persists a real, unresolved
      // Payment (Pending/Unknown/ReconciliationRequired) for this bill and
      // reports it as `summary.unsettledPayment` - the lock is re-derived
      // from THIS on every load, not tracked purely in this page's own
      // in-memory state, so a page reload after an unresolved BankCard
      // attempt correctly shows the same "manuel mutabakat gerekiyor"
      // banner instead of silently letting a new tender attempt through
      // client-side only to be rejected by the server with a generic error.
      state.locked = !!state.summary.unsettledPayment;
      state.amountDraft = remainingAmount().toFixed(2);
      state.phase = remainingAmount() <= 0.004 ? 'paid' : 'ready';
      // V1-RMD-314: a fresh load re-derives everything from server truth - any in-flight key from before
      // this load is stale.
      state.tenderIdempotencyKey = null;
      loadRefundRequests();
      loadSavedShares();
      render();
    }).catch(function () {
      state.error = 'Hesap bilgisi okunamadı. Bağlantınızı kontrol edin.';
      state.phase = 'loadError';
      render();
    });
  }

  // V1-RMD-298 (independent 2026-09-26 audit, finding K1): fixed. GET .../tenders/ now returns the real,
  // discount/tip-adjusted ceiling (AdjustmentCalculator.Calculate's AdjustedPayableAmount), not the Bill's
  // own never-updated PayableAmount - see DualScreenApplication.Payments.cs. The allocation/closure gates
  // (PaymentAllocationFactory, BillPaymentClosureCalculator, CashTenderHandler, EftTenderHandler) were fixed
  // the same way, so this value is now genuinely authoritative for tendering: a discount/tip now really does
  // change what must be collected and when the bill closes, matching what this page has always shown.
  function remainingAmount() {
    return state.summary ? state.summary.remainingAmount : 0;
  }

  function applyAdjustmentsResult(result) {
    if (!result || !result.ok) return;
    state.adjustmentItems = result.body.adjustments || [];
    state.adjustmentSummary = result.body.summary || null;
  }

  function refreshAdjustments() {
    return api(billBase() + '/adjustments').then(applyAdjustmentsResult);
  }

  // ---- actions ---------------------------------------------------------

  function refreshSummary() {
    return api(tendersBase()).then(function (result) {
      if (!result.ok) return;
      state.summary = result.body;
      // Re-derive the lock from server truth on every refresh, same as
      // loadEverything() does on initial load (V1-RMD-258/V13-RMD-002) -
      // this both sets the lock when a fresh RequiresReconciliation lands,
      // and clears it once a real resolution exists server-side.
      state.locked = !!state.summary.unsettledPayment;
      if (remainingAmount() <= 0.004 && !state.locked) state.phase = 'paid';
    });
  }

  function nextShare() {
    if (!(state.splitLeft > 1)) return remainingAmount();
    return Math.round((remainingAmount() / state.splitLeft) * 100) / 100;
  }

  function submitEqualSplit() {
    var count = Math.max(2, Math.round(Number(state.splitCount) || 2));
    state.splitLeft = count;
    state.amountDraft = nextShare().toFixed(2);
    // V1-RMD-314: a recalculated split amount is a genuinely different tender attempt.
    state.tenderIdempotencyKey = null;
    render();
  }

  // V1-RMD-264: a manager declares the unconfirmed card payment was NOT
  // charged. The server decides who may do this (403 for a plain cashier).
  function resolveNotCharged() {
    var reason = (state.resolveReason || '').trim();
    if (!reason) {
      setError('Gerekçe yazmalısınız.');
      return;
    }
    var unsettled = state.summary && state.summary.unsettledPayment;
    if (!unsettled) return;
    setBusy(true);
    state.error = null;
    api(tendersBase() + 'unsettled/' + unsettled.paymentId + '/not-charged', {
      method: 'POST',
      body: { reason: reason },
    }).then(function (result) {
      state.busy = false;
      if (result.ok) {
        state.resolveReason = '';
        return refreshSummary().then(function () { render(); });
      }
      state.error = result.status === 403
        ? 'Bu işlem için müdür yetkisi gerekir. Yetkili bir kullanıcıyla giriş yapın.'
        : describeHttpFailure(result.status, result.body);
      render();
    }).catch(function () {
      state.busy = false;
      setError('Bağlantı kurulamadı. Tekrar deneyin.');
    });
  }

  function claimCardCharged() {
    var slip = (state.slipDraft || '').trim();
    if (!slip) {
      setError('Fiş numarasını yazın.');
      return;
    }
    var unsettled = state.summary && state.summary.unsettledPayment;
    if (!unsettled) return;
    setBusy(true);
    state.error = null;
    api(tendersBase() + 'unsettled/' + unsettled.paymentId + '/card-charged', {
      method: 'POST',
      body: { slipNumber: slip, note: null },
    }).then(function (result) {
      state.busy = false;
      if (result.ok) {
        state.slipDraft = '';
        return refreshSummary().then(function () { render(); });
      }
      state.error = result.status === 403
        ? 'Bu işlem için müdür yetkisi gerekir. Yetkili bir kullanıcıyla giriş yapın.'
        : describeHttpFailure(result.status, result.body);
      render();
    }).catch(function () {
      state.busy = false;
      setError('Bağlantı kurulamadı. Tekrar deneyin.');
    });
  }

  function decideConfirmation(decision) {
    var pending = state.summary && state.summary.unsettledPayment && state.summary.unsettledPayment.pendingConfirmation;
    if (!pending) return;
    setBusy(true);
    state.error = null;
    api(tendersBase() + 'confirmations/' + pending.confirmationId + '/' + decision, {
      method: 'POST',
      body: { note: (state.decisionNote || '').trim() || null },
    }).then(function (result) {
      state.busy = false;
      if (result.ok) {
        state.decisionNote = '';
        return refreshSummary().then(function () { render(); });
      }
      state.error = result.status === 403 && !(result.body && result.body.error && result.body.error.message)
        ? 'Bu işlem için müdür yetkisi gerekir. Yetkili bir kullanıcıyla giriş yapın.'
        : describeHttpFailure(result.status, result.body);
      render();
    }).catch(function () {
      state.busy = false;
      setError('Bağlantı kurulamadı. Tekrar deneyin.');
    });
  }

  // V1-RMD-292: bills.discount is grant-class (BillingSplitApplication.cs's own comment on the endpoint) -
  // a role holding it outright applies at once (200, Status 'Applied'); a role that does not raises a grant
  // request through the exact same policy/delegation/manager-decision engine authorization-decisions.cs
  // already surfaces (202, Status 'Pending', never silently denied); the policy engine can also refuse it
  // outright (403, code GRANT_DENIED - client-side Turkish override below, since that endpoint's own message
  // is the literal English string "Discount request was denied.", outside this task's Owned surface to fix).
  function submitDiscount() {
    var value = Number(state.discountValueDraft);
    if (!(value > 0)) {
      state.discountError = 'Tutar sıfırdan büyük olmalı.';
      render();
      return;
    }
    // An unintended 100 instead of 10 would close the whole bill with nothing collected: ask once before that.
    var takeOff = state.discountCalcType === 'Percentage' ? remainingAmount() * value / 100 : value;
    if (takeOff >= remainingAmount() - 0.004 && !state.discountConfirm) {
      state.discountConfirm = true;
      state.discountError = null;
      render();
      return;
    }
    state.discountConfirm = false;
    state.discountBusy = true;
    state.discountError = null;
    state.discountNotice = null;
    render();
    // V1-RMD-314: reuse the in-flight key across a retry - see tenderIdempotencyKey's own doc comment.
    if (!state.discountIdempotencyKey) state.discountIdempotencyKey = crypto.randomUUID();
    api(billBase() + '/discount', {
      method: 'POST',
      body: {
        IdempotencyKey: state.discountIdempotencyKey,
        CalculationType: state.discountCalcType,
        Value: value,
        ReasonCode: state.discountReason,
        Notes: state.discountNoteDraft || null,
      },
    }).then(function (result) {
      state.discountBusy = false;
      // V1-RMD-314: any real server response (success OR a definitive rejection) means this key has
      // already been consumed/decided - only a network failure (the catch below) keeps it for a retry.
      if (result.status < 500) state.discountIdempotencyKey = null;
      if (!result.ok) {
        var code = result.body && result.body.error && result.body.error.code;
        state.discountError = code === 'GRANT_DENIED'
          ? 'İndirim talebiniz reddedildi. Yetkili bir kullanıcı uygulayabilir.'
          : describeHttpFailure(result.status, result.body);
        render();
        return;
      }
      state.discountValueDraft = '';
      state.discountNoteDraft = '';
      if (result.body.status === 'Pending') {
        state.discountNotice = 'İndirim talebiniz yönetici onayına gönderildi; onaylanana kadar hesap değişmez.';
        render();
        return;
      }
      state.discountNotice = 'İndirim uygulandı ve kaydedildi.';
      // V1-RMD-298: a discount now genuinely lowers remainingAmount() (server-side ceiling fix) - refresh
      // the tender summary too, not just the adjustments list, and re-derive amountDraft from the new
      // ceiling the same way loadEverything() does on initial load.
      return refreshAdjustments().then(refreshSummary).then(function () {
        state.amountDraft = remainingAmount().toFixed(2);
        render();
      });
    }).catch(function () {
      state.discountBusy = false;
      state.discountError = 'Bağlantı kurulamadı. Tekrar deneyin.';
      render();
    });
  }

  // V1-RMD-292: bills.split is checked directly (RequireMutationAsync) - a voluntary tip is data entry of
  // money already handed over, never a discretionary decision a role might lack the authority for (see the
  // endpoint's own comment); no grant flow here, unlike the discount above.
  function submitTip() {
    var amount = Number(state.tipAmountDraft);
    if (!(amount > 0)) {
      state.tipError = 'Tutar sıfırdan büyük olmalı.';
      render();
      return;
    }
    state.tipBusy = true;
    state.tipError = null;
    render();
    // V1-RMD-314: reuse the in-flight key across a retry - see tenderIdempotencyKey's own doc comment.
    if (!state.tipIdempotencyKey) state.tipIdempotencyKey = crypto.randomUUID();
    api(billBase() + '/tip', {
      method: 'POST',
      body: { IdempotencyKey: state.tipIdempotencyKey, Amount: amount, Notes: state.tipNoteDraft || null },
    }).then(function (result) {
      state.tipBusy = false;
      // V1-RMD-314: any real server response means this key has already been consumed/decided - only a
      // network failure (the catch below) keeps it for a retry.
      if (result.status < 500) state.tipIdempotencyKey = null;
      if (!result.ok) {
        // FORBIDDEN and FEATURE_DISABLED already carry a correct Turkish message from the server
        // (BillingSplitExceptionFilter's own Map) - no client-side override needed here, unlike discount's
        // GRANT_DENIED above.
        state.tipError = describeHttpFailure(result.status, result.body);
        render();
        return;
      }
      state.tipAmountDraft = '';
      state.tipNoteDraft = '';
      // V1-RMD-298: a tip now genuinely raises remainingAmount() (server-side ceiling fix) - same
      // refresh-summary-then-re-derive-amountDraft pattern as submitDiscount above.
      return refreshAdjustments().then(refreshSummary).then(function () {
        state.amountDraft = remainingAmount().toFixed(2);
        render();
      });
    }).catch(function () {
      state.tipBusy = false;
      state.tipError = 'Bağlantı kurulamadı. Tekrar deneyin.';
      render();
    });
  }

  // V1-RMD-443: the till's customer list (V1-RMD-442), filtered server-side by name or phone digits.
  function loadCustomers() {
    state.customersLoading = true;
    render();
    var query = state.customerSearch.trim() ? '?search=' + encodeURIComponent(state.customerSearch.trim()) : '';
    return api('/api/v1/terminals/' + state.terminalId + '/customers' + query).then(function (result) {
      state.customersLoading = false;
      if (!result.ok) {
        state.customers = [];
        state.error = describeHttpFailure(result.status, result.body);
      } else {
        state.customers = result.body;
        if (state.selectedCustomerId && !state.customers.some(function (c) { return c.customerId === state.selectedCustomerId; })) {
          state.selectedCustomerId = null;
        }
      }
      render();
    }).catch(function () {
      state.customersLoading = false;
      setError('Müşteri listesi okunamadı.');
    });
  }

  function selectedCustomer() {
    if (!state.customers || !state.selectedCustomerId) return null;
    return state.customers.filter(function (c) { return c.customerId === state.selectedCustomerId; })[0] || null;
  }

  function submitTender() {
    var amount = Number(state.amountDraft);
    if (!(amount > 0)) {
      setError('Tutar sıfırdan büyük olmalı.');
      return;
    }
    if (amount > remainingAmount() + 0.004) {
      setError('Tutar kalan tutarı aşamaz.');
      return;
    }
    // Savunma amaçlı ikinci kontrol - düğme zaten bu durumda pasif, ama
    // programatik bir tıklama (ör. Enter tuşu) burada da engellenmeli.
    if (state.selectedMethod === 'Eft' && !state.eftConfirmed) {
      setError('Devam etmeden önce tutarı banka hesap hareketinde gördüğünüzü onaylayın.');
      return;
    }
    if (state.selectedMethod === 'Account' && !selectedCustomer()) {
      setError('Hesaba yazmak için bir müşteri seçin.');
      return;
    }

    var tendered = Number(state.tenderedDraft);
    var hasTendered = state.selectedMethod === 'Cash' && String(state.tenderedDraft).trim() !== '' && tendered > 0;
    if (hasTendered && tendered < amount - 0.004) {
      setError('Alınan para tutardan az olamaz.');
      return;
    }

    setBusy(true);
    state.error = null;
    state.changeDue = null;
    // V1-RMD-314: reuse the in-flight key across a retry of this exact attempt - see the field's own
    // doc comment. A brand-new key is minted only when none is pending.
    if (!state.tenderIdempotencyKey) state.tenderIdempotencyKey = crypto.randomUUID();
    var idempotencyKey = state.tenderIdempotencyKey;

    var request = state.selectedMethod === 'Account'
      ? api(billBase() + '/account-charge', {
          method: 'POST',
          body: { CustomerId: state.selectedCustomerId, Amount: amount, IdempotencyKey: idempotencyKey },
        }).then(function (result) {
          if (!result.ok) return result;
          return { ok: true, status: result.status, body: { outcome: 'Approved', approvedAmount: result.body.approvedAmount } };
        })
      : state.selectedMethod === 'Cash'
      ? api('/api/v1/terminals/' + state.terminalId + '/cash-sessions/' + state.cashSessionId + '/cash-tender', {
          method: 'POST',
          body: { BillId: state.billId, AmountDue: amount, TenderedAmount: hasTendered ? tendered : amount, IdempotencyKey: idempotencyKey },
        }).then(function (result) {
          if (!result.ok) return result;
          return {
            ok: true, status: result.status,
            body: { outcome: 'Approved', approvedAmount: result.body.approvedAmount, changeAmount: result.body.changeAmount },
          };
        })
      : api(tendersBase(), {
          method: 'POST',
          body: {
            Method: state.selectedMethod,
            Amount: amount,
            IdempotencyKey: idempotencyKey,
            Note: state.noteDraft || null,
          },
        });

    request.then(function (result) {
      state.busy = false;
      // V1-RMD-314: any real server response (success OR a definitive rejection) means this key has
      // already been consumed/decided - only a network failure (the catch below) keeps it for a retry.
      if (result.status < 500) state.tenderIdempotencyKey = null;
      if (!result.ok) {
        state.error = describeHttpFailure(result.status, result.body);
        // A concurrently-created unresolved Payment (e.g. another tab, or
        // this exact lock having been dropped by a stale reload before this
        // fix) can reject a submit with TENDER_UNSETTLED_PAYMENT_EXISTS -
        // refresh so the real "manuel mutabakat gerekiyor" banner replaces
        // the generic error message instead of leaving the cashier with
        // only an ambiguous failure toast.
        var code = result.body && result.body.error && result.body.error.code;
        if (code === 'TENDER_UNSETTLED_PAYMENT_EXISTS') {
          state.error = null;
          return refreshSummary().then(render);
        }
        render();
        return;
      }
      if (result.body && result.body.outcome === 'Declined') {
        // Ret para kaydı oluşturmaz; yeniden denemede yeni anahtar kullanılır.
        state.tenderIdempotencyKey = null;
        state.error = 'Ödeme reddedildi. Başka bir yöntem deneyin.';
        render();
        return;
      }
      state.lastResult = result.body;
      state.changeDue = hasTendered && result.body && Number(result.body.changeAmount) > 0 ? Number(result.body.changeAmount) : null;
      state.tenderedDraft = '';
      state.noteDraft = '';
      state.eftConfirmed = false;
      if (state.splitLeft > 0) state.splitLeft -= 1;
      if (result.body.outcome === 'RequiresReconciliation') {
        // Bilinmeyen durum kilidi: bu satır onaylanana kadar bu sekmede
        // hesaba yeni bir tahsilat eklenemez - mükerrer tahsilat riskini
        // önler. Gerçek Token/Beko entegrasyonu (V13-HUG-001) gelene kadar
        // manuel mutabakat gerekiyor.
        state.locked = true;
        return refreshSummary().then(render);
      }
      return refreshSummary().then(function () {
        state.amountDraft = nextShare().toFixed(2);
        render();
      });
    }).catch(function () {
      state.busy = false;
      setError('Sunucuya ulaşılamadı. Tahsilat gerçekleşmiş olabilir; hesabı yenileyip kontrol edin.');
    });
  }

  // ---- rendering ---------------------------------------------------------

  // V1-RMD-362 (module-by-module UI audit, 2026-09-27): the same gap class V1-RMD-345 closed for
  // CustomerWeb and V1-RMD-361 closed for cash-session.js, missed on THIS page - #app is already
  // aria-live="polite" (index.html) so a screen reader hears the text, but nothing marked it as
  // specifically an error.
  function errorAlert() {
    if (!state.error) return '';
    return (
      '<div class="sp-alert sp-alert-danger" role="alert">' +
      '<span class="sp-alert-icon">!</span>' +
      '<div><div class="sp-alert-title">Hata</div>' +
      '<div class="sp-alert-body">' + escapeHtml(state.error) + '</div></div>' +
      '</div>'
    );
  }

  function renderMissingBill() {
    app.innerHTML =
      '<div class="sp-card sp-center-text">' +
      '<span class="sp-icon-circle danger">!</span>' +
      '<h1 class="sp-title">Hesap Bulunamadı</h1>' +
      '<p class="sp-subtitle">Bu sayfa geçerli bir hesap kimliği (billId) gerektirir.</p>' +
      '</div>';
  }

  function renderLogin() {
    app.innerHTML =
      '<div class="sp-card">' +
      '<div><span class="sp-eyebrow">ALKAROS</span>' +
      '<h1 class="sp-title">Kasa Girişi</h1>' +
      '<p class="sp-subtitle">Oturumunuz sona erdi. Devam etmek için tekrar giriş yapın.</p></div>' +
      errorAlert() +
      '<a class="sp-btn sp-btn-primary" href="/" style="display:inline-flex;align-items:center;justify-content:center;text-decoration:none;">Giriş ekranına dön</a>' +
      '</div>';
  }

  // V1-RMD-362: three mutually-exclusive buttons that pick ONE value (which the rest of the form's
  // behavior then depends on) had only a visual .is-active class - no ARIA grouping or state at
  // all, unlike Module 1/2's category/direction tabs (a genuinely different pattern: those are
  // VIEWS, this is a VALUE choice) - role="radiogroup"/"radio" + aria-checked is the correct
  // pattern for "exactly one of these applies" rather than role="tab".
  function renderMethodChips() {
    var methods = ['Cash', 'BankCard', 'Eft', 'Account'];
    return (
      '<div class="sp-method-chips" role="radiogroup" aria-label="Ödeme yöntemi">' +
      methods.map(function (method) {
        var disabled = method === 'Cash' && !state.cashSessionOpen;
        var active = state.selectedMethod === method;
        return (
          '<button type="button" class="sp-method-chip' + (active ? ' is-active' : '') + '" ' +
          'role="radio" aria-checked="' + active + '" ' +
          'data-method="' + method + '"' + (disabled ? ' disabled' : '') + '>' +
          escapeHtml(METHOD_LABELS[method]) + (disabled ? ' (kasa kapalı)' : '') + '</button>'
        );
      }).join('') +
      '</div>'
    );
  }

  // V1-RMD-443: the customer picker for "Hesaba yaz". Radio semantics for the same reason as the method chips:
  // exactly one customer applies.
  function renderCustomerPicker() {
    var chosen = selectedCustomer();
    var list = state.customers === null || state.customersLoading
      ? '<div class="sp-alert-body" role="status">Müşteriler yükleniyor…</div>'
      : state.customers.length === 0
        ? '<div class="sp-alert-body" role="status">Eşleşen müşteri yok. Yeni müşteri Cari Hesaplar ekranından eklenir.</div>'
        : '<div class="sp-customer-list" role="radiogroup" aria-label="Müşteri">' +
          state.customers.map(function (c) {
            var active = c.customerId === state.selectedCustomerId;
            return (
              '<button type="button" class="sp-customer' + (active ? ' is-active' : '') + '" role="radio" aria-checked="' + active + '" ' +
              'data-customer-id="' + escapeHtml(c.customerId) + '">' +
              '<span class="sp-customer-name">' + escapeHtml(c.name) + (c.phoneMasked ? ' · ' + escapeHtml(c.phoneMasked) : '') + '</span>' +
              '<span class="sp-customer-meta">Borç ' + formatMoney(c.balance) + ' · Kullanılabilir ' + formatMoney(c.availableCredit) + '</span>' +
              '</button>'
            );
          }).join('') +
          '</div>';
    return (
      '<div class="sp-field"><span class="sp-field-label">Müşteri</span>' +
      '<div class="sp-row">' +
      '<input class="sp-input" type="search" id="customer-search" placeholder="Ad veya telefon" aria-label="Müşteri ara (ad veya telefon)" value="' +
      escapeHtml(state.customerSearch) + '">' +
      '<button class="sp-btn sp-btn-secondary" id="customer-search-submit" type="button">Ara</button>' +
      '</div>' + list +
      (chosen && chosen.creditLimit <= 0
        ? '<div class="sp-alert-body" role="status">Bu müşteri için kredi limiti tanımlı değil; hesaba yazma reddedilir.</div>'
        : '') +
      '</div>'
    );
  }

  function renderAllocationLines() {
    var allocations = state.summary.allocations;
    if (allocations.length === 0) return '';
    return (
      '<div class="sp-field"><span class="sp-field-label">Uygulanan tahsilatlar</span>' +
      allocations.map(function (line) {
        return (
          '<div class="sp-line"><span class="sp-line-method">' + formatMoney(line.amount) + '</span>' +
          '<span class="sp-line-status is-approved">Onaylandı</span></div>'
        );
      }).join('') +
      '</div>'
    );
  }

  // V1-RMD-283: how a manager closes an unconfirmed card payment. "Not charged" needs one authorized person.
  // "Charged" moves money, so it needs TWO: one claims it with the slip number, a different one approves.
  function renderResolution() {
    var pending = state.summary.unsettledPayment && state.summary.unsettledPayment.pendingConfirmation;
    var disabled = state.busy ? ' disabled' : '';
    if (pending) {
      var mine = state.userId && pending.requestedBy === state.userId;
      return (
        '<div class="sp-field" id="pending-confirmation"><span class="sp-field-label">Onay bekliyor: fiş ' +
        escapeHtml(pending.slipNumber) + ' · ' + formatMoney(pending.amount) + '</span>' +
        (mine
          ? '<div class="sp-alert-body">Kart çekildi bildirimini siz yaptınız; ikinci bir yetkilinin onaylaması gerekir.</div>'
          : '<input class="sp-input" type="text" id="decision-note" maxlength="500" placeholder="Not (isteğe bağlı)" aria-label="Onay notu (isteğe bağlı)" value="' +
            escapeHtml(state.decisionNote) + '">' +
            '<button class="sp-btn sp-btn-primary" id="approve-confirmation" type="button"' + disabled + '>Onayla: kart çekildi</button>') +
        '<button class="sp-btn sp-btn-secondary" id="reject-confirmation" type="button"' + disabled + '>' +
        (mine ? 'Bildirimi geri çek' : 'Reddet') + '</button></div>'
      );
    }
    return (
      '<div class="sp-field"><span class="sp-field-label">Yetkili müdür: kart çekildi mi? (fiş numarası ile, ikinci bir yetkili onaylar)</span>' +
      '<input class="sp-input" type="text" id="claim-slip" maxlength="32" placeholder="Fiş numarası" aria-label="Fiş numarası" value="' + escapeHtml(state.slipDraft) + '">' +
      '<button class="sp-btn sp-btn-secondary" id="claim-card-charged" type="button"' + disabled + '>Kart çekildi: onaya gönder</button></div>' +
      '<div class="sp-field"><span class="sp-field-label">Yetkili müdür: kart çekilmedi mi?</span>' +
      '<input class="sp-input" type="text" id="resolve-reason" maxlength="500" ' +
      'placeholder="Gerekçe (zorunlu)" aria-label="Kart çekilmedi gerekçesi (zorunlu)" value="' + escapeHtml(state.resolveReason) + '">' +
      '<button class="sp-btn sp-btn-secondary" id="resolve-not-charged" type="button"' + disabled + '>Kart çekilmedi olarak çöz</button></div>'
    );
  }

  function renderAdjustmentList() {
    if (state.adjustmentItems.length === 0) return '';
    var adjustedTotal = state.adjustmentSummary ? state.adjustmentSummary.adjustedPayableAmount : null;
    return (
      '<div class="sp-field"><span class="sp-field-label">Uygulanan indirim, bahşiş ve ücretler</span>' +
      state.adjustmentItems.map(function (item) {
        var label = ADJUSTMENT_TYPE_LABELS[item.adjustmentType] || item.adjustmentType;
        var sign = item.isDeduction ? '−' : '+';
        return (
          '<div class="sp-line"><span class="sp-line-method">' + escapeHtml(label) +
          (item.reason ? ' · ' + escapeHtml(DISCOUNT_REASON_LABELS[item.reason] || item.reason) : '') + '</span>' +
          '<span class="sp-line-status is-approved">' + sign + formatMoney(item.amount) + '</span></div>'
        );
      }).join('') +
      (adjustedTotal != null
        ? '<div class="sp-line"><span class="sp-line-method">Kaydedilen düzeltmelerle toplam</span>' +
          '<span class="sp-line-status is-approved">' + formatMoney(adjustedTotal) + '</span></div>'
        : '') +
      '</div>'
    );
  }

  function renderDiscountForm() {
    var disabled = state.discountBusy ? ' disabled' : '';
    return (
      '<div class="sp-field"><span class="sp-field-label">İndirim / düzeltme ekle</span>' +
      // V1-RMD-362: a bare .sp-alert-body with no role at all - a screen reader was never told a
      // discount attempt succeeded OR failed, only a sighted cashier watching the screen saw it.
      (state.discountNotice ? '<div class="sp-alert-body" role="status">' + escapeHtml(state.discountNotice) + '</div>' : '') +
      (state.discountError ? '<div class="sp-alert-body" role="alert" style="color:var(--color-danger)">' + escapeHtml(state.discountError) + '</div>' : '') +
      (state.discountConfirm
        ? '<div class="sp-alert-body" role="alert">Bu indirim hesabın kalan tutarının tamamını (' + escapeHtml(formatMoney(remainingAmount())) +
          ') kapatır; hesap tahsilat yapılmadan kapanır. Doğruysa "Evet, indirimi uygula" düğmesine basın.</div>'
        : '') +
      '<select class="sp-input" id="discount-reason" aria-label="İndirim gerekçesi">' +
      Object.keys(DISCOUNT_REASON_LABELS).map(function (code) {
        return '<option value="' + code + '"' + (state.discountReason === code ? ' selected' : '') + '>' +
          escapeHtml(DISCOUNT_REASON_LABELS[code]) + '</option>';
      }).join('') +
      '</select>' +
      '<div class="sp-row">' +
      '<select class="sp-input" id="discount-calc-type" aria-label="İndirim hesaplama türü">' +
      '<option value="Percentage"' + (state.discountCalcType === 'Percentage' ? ' selected' : '') + '>Yüzde (%)</option>' +
      '<option value="FixedAmount"' + (state.discountCalcType === 'FixedAmount' ? ' selected' : '') + '>Tutar (₺)</option>' +
      '</select>' +
      '<input class="sp-input" type="number" step="0.01" min="0.01" id="discount-value" placeholder="Değer" aria-label="İndirim değeri" value="' +
      escapeHtml(state.discountValueDraft) + '">' +
      '</div>' +
      '<input class="sp-input" type="text" id="discount-note" maxlength="500" placeholder="Not (opsiyonel)" aria-label="İndirim notu (opsiyonel)" value="' +
      escapeHtml(state.discountNoteDraft) + '">' +
      '<button class="sp-btn sp-btn-secondary" id="submit-discount" type="button"' + disabled + '>' +
      (state.discountBusy ? 'Gönderiliyor…' : (state.discountConfirm ? 'Evet, indirimi uygula' : 'İndirim uygula')) + '</button>' +
      '</div>'
    );
  }

  function renderTipForm() {
    var disabled = state.tipBusy ? ' disabled' : '';
    return (
      '<div class="sp-field"><span class="sp-field-label">Gönüllü bahşiş ekle</span>' +
      (state.tipError ? '<div class="sp-alert-body" role="alert" style="color:var(--color-danger)">' + escapeHtml(state.tipError) + '</div>' : '') +
      '<div class="sp-row">' +
      '<input class="sp-input" type="number" step="0.01" min="0.01" id="tip-amount" placeholder="Tutar" aria-label="Bahşiş tutarı" value="' +
      escapeHtml(state.tipAmountDraft) + '">' +
      '<input class="sp-input" type="text" id="tip-note" maxlength="500" placeholder="Not (opsiyonel)" aria-label="Bahşiş notu (opsiyonel)" value="' +
      escapeHtml(state.tipNoteDraft) + '">' +
      '</div>' +
      '<button class="sp-btn sp-btn-secondary" id="submit-tip" type="button"' + disabled + '>' +
      (state.tipBusy ? 'Gönderiliyor…' : 'Bahşiş ekle') + '</button>' +
      '</div>'
    );
  }

  // ---- iptal ve iade -------------------------------------------------------

  var REFUND_STATUS_LABELS = {
    Requested: 'Onay bekliyor',
    AwaitingManualRefund: 'Elle iade bekliyor',
    Completed: 'Tamamlandı',
    Rejected: 'Reddedildi',
  };
  var REFUND_METHOD_LABELS = { Cash: 'Nakit', Manual: 'Kart / elle iade' };

  // A retry of the same refund after a lost response reuses its key, so the server returns the stored request
  // instead of paying out twice; the key is dropped once the server answered.
  function requestRefundCall(body) {
    var signature = JSON.stringify(body);
    if (!state.refundRequestKey || state.refundRequestSignature !== signature) {
      state.refundRequestKey = crypto.randomUUID();
      state.refundRequestSignature = signature;
    }
    return api(billBase() + '/refund-requests', {
      method: 'POST', body: body, headers: { 'X-Idempotency-Key': state.refundRequestKey },
    });
  }

  function refundsBase() {
    return '/api/v1/terminals/' + state.terminalId + '/billing/refund-requests';
  }

  // The remaining shift allowance is a convenience: when it cannot be read it is simply not shown.
  function loadRefundAllowance() {
    return api('/api/v1/terminals/' + state.terminalId + '/billing/refund-allowance').then(function (result) {
      state.refundAllowance = result.ok && result.body && result.body.hasOpenSession ? result.body : null;
      render();
    }).catch(function () {
      state.refundAllowance = null;
    });
  }

  // Item lines come from the receipt summary; without them only whole-payment refunds are offered.
  function loadBillItems() {
    return api(billBase() + '/receipt').then(function (result) {
      state.billItems = result.ok && result.body && Array.isArray(result.body.items) ? result.body.items : [];
      render();
    }).catch(function () {
      state.billItems = [];
    });
  }

  function loadRefundRequests() {
    loadRefundAllowance();
    loadBillItems();
    return api(refundsBase()).then(function (result) {
      // Yetkisi olmayan kullanıcı için liste sessizce boş kalır; işlemler zaten sunucuda 403 ile reddedilir.
      state.refundListFailed = !result.ok && result.status !== 403;
      state.refundRequests = result.ok && Array.isArray(result.body)
        ? result.body.filter(function (row) { return row.billId === state.billId; })
        : [];
      render();
    }).catch(function () {
      state.refundListFailed = true;
      state.refundRequests = [];
      render();
    });
  }

  function openRefundFor(paymentId) {
    return (state.refundRequests || []).some(function (row) {
      return row.paymentId === paymentId && (row.status === 'Requested' || row.status === 'AwaitingManualRefund');
    });
  }

  var REASON_PRESETS = [
    'Müşteri vazgeçti', 'Yanlış sipariş', 'Mutfak hatası', 'Ürün sorunlu', 'İkram', 'Yanlış tutar girildi',
  ];
  var REASON_OTHER = 'Diğer';

  // Hazır gerekçe listesi; "Diğer" seçilirse serbest metin alanı açılır.
  // key: cancel | refund:<paymentId> | note:<requestId>
  function reasonValue(key) {
    if (key === 'cancel') return state.cancelReason;
    if (key === 'items') return state.itemReason;
    if (key.indexOf('refund:') === 0) return state.refundReasons[key.slice(7)] || '';
    return state.refundNotes[key.slice(5)] || '';
  }

  function setReasonValue(key, value) {
    if (key === 'cancel') state.cancelReason = value;
    else if (key === 'items') state.itemReason = value;
    else if (key.indexOf('refund:') === 0) state.refundReasons[key.slice(7)] = value;
    else state.refundNotes[key.slice(5)] = value;
  }

  function renderReasonField(key, label) {
    var choice = state.reasonChoices[key] || '';
    var textAttr = key === 'cancel' ? 'id="cancel-reason"'
      : key === 'items' ? 'data-item-reason="1"'
      : key.indexOf('refund:') === 0 ? 'data-refund-reason="' + escapeHtml(key.slice(7)) + '"'
      : 'data-refund-note="' + escapeHtml(key.slice(5)) + '"';
    var options = '<option value="">Gerekçe seçin</option>' +
      REASON_PRESETS.concat([REASON_OTHER]).map(function (name) {
        return '<option value="' + escapeHtml(name) + '"' + (choice === name ? ' selected' : '') + '>' + escapeHtml(name) + '</option>';
      }).join('');
    return (
      '<select class="sp-input" data-reason-select="' + escapeHtml(key) + '" aria-label="' + escapeHtml(label) + '"' +
      (state.refundBusy ? ' disabled' : '') + '>' + options + '</select>' +
      (choice === REASON_OTHER
        ? '<input class="sp-input" type="text" maxlength="500" ' + textAttr + ' aria-label="Gerekçeyi yazın" ' +
          'placeholder="Gerekçeyi yazın" value="' + escapeHtml(reasonValue(key)) + '">'
        : '')
    );
  }

  function onReasonChange(event) {
    var el = event.target;
    if (el.getAttribute && el.getAttribute('data-item-payment') !== null) {
      state.itemPayment = el.value;
      render();
      return;
    }
    if (el.getAttribute && el.getAttribute('data-item-stock') !== null) {
      state.itemStock = el.value;
      render();
      return;
    }
    var key = el.getAttribute && el.getAttribute('data-reason-select');
    if (!key) return;
    state.reasonChoices[key] = el.value;
    setReasonValue(key, el.value === REASON_OTHER ? '' : el.value);
    render();
  }

  // Yıkıcı eylemler iki adımlı: ilk dokunuş "Emin misiniz?" gösterir, ikincisi çalıştırır.
  function confirmButtons(action, id, label, confirmLabel, disabledAttr, variant) {
    var key = action + ':' + id;
    var attrs = ' data-refund-action="' + action + '"' + (id ? ' data-request-id="' + id + '"' : '');
    if (state.confirmKey === key) {
      return (
        '<span class="sp-alert-body" role="alert">Emin misiniz? Bu işlem geri alınamaz.</span>' +
        '<button class="sp-btn sp-btn-danger" type="button"' + attrs + disabledAttr + '>' + confirmLabel + '</button>' +
        '<button class="sp-btn sp-btn-secondary" type="button" data-refund-action="dismiss">Vazgeç</button>'
      );
    }
    return '<button class="sp-btn ' + variant + '" type="button"' + attrs + disabledAttr + '>' + label + '</button>';
  }

  function renderCancelSection() {
    if (state.summary.allocations.length > 0 || state.locked) return '';
    var disabled = state.refundBusy ? ' disabled' : '';
    return (
      '<div class="sp-field" id="cancel-section"><span class="sp-field-label">Hesabı iptal et</span>' +
      renderReasonField('cancel', 'İptal gerekçesi') +
      confirmButtons('cancel', '', 'Hesabı iptal et', 'Evet, hesabı iptal et', disabled || (state.cancelReason.trim() ? '' : ' disabled'), 'sp-btn-secondary') +
      '</div>'
    );
  }

  function parseQuantity(value) {
    var text = String(value || '').trim().replace(',', '.');
    if (!text) return null;
    var parsed = Number(text);
    return isFinite(parsed) && parsed >= 0 ? parsed : NaN;
  }

  function itemLeft(item) {
    return item.quantity - (item.refundedQuantity || 0);
  }

  function itemPayments() {
    return state.summary.allocations.filter(function (line) {
      return (line.paymentStatus === 'Approved' || line.paymentStatus === 'PartiallyRefunded') && !openRefundFor(line.paymentId);
    });
  }

  function refundableItems() {
    if (state.refundListFailed || itemPayments().length === 0) return [];
    return state.billItems.filter(function (item) { return itemLeft(item) > 0; });
  }

  // Selected lines, or null when any typed quantity is not a number within what is left.
  function chosenItems() {
    var chosen = [];
    var valid = true;
    refundableItems().forEach(function (item) {
      var quantity = parseQuantity(state.itemQty[item.billItemId]);
      if (quantity === null || quantity === 0) return;
      if (isNaN(quantity) || quantity > itemLeft(item)) {
        valid = false;
        return;
      }
      chosen.push({ billItemId: item.billItemId, quantity: quantity, restoreStock: state.itemStock === 'yes' });
    });
    return valid ? chosen : null;
  }

  function itemsReady() {
    var chosen = chosenItems();
    return !!chosen && chosen.length > 0 && !!state.itemPayment && !!state.itemStock && !!state.itemReason.trim();
  }

  function renderItemRefund() {
    var items = refundableItems();
    if (items.length === 0) return '';
    var disabled = state.refundBusy ? ' disabled' : '';
    var quantities = items.map(function (item) {
      return (
        '<label class="sp-field"><span class="sp-field-label">' + escapeHtml(item.name) + ' · kalan ' + itemLeft(item) +
        ' · iade adedi</span>' +
        '<input class="sp-input" type="text" inputmode="decimal" data-item-qty="' + escapeHtml(item.billItemId) + '" value="' +
        escapeHtml(state.itemQty[item.billItemId] || '') + '"' + disabled + '></label>'
      );
    }).join('');
    var paymentOptions = '<option value="">Ödeme seçin</option>' + itemPayments().map(function (line) {
      return '<option value="' + escapeHtml(line.paymentId) + '"' + (state.itemPayment === line.paymentId ? ' selected' : '') + '>' +
        'Ödeme · ' + escapeHtml(formatMoney(line.amount)) + '</option>';
    }).join('');
    var stockOptions =
      '<option value="">Seçin</option>' +
      '<option value="yes"' + (state.itemStock === 'yes' ? ' selected' : '') + '>Evet, stoğa geri dönsün</option>' +
      '<option value="no"' + (state.itemStock === 'no' ? ' selected' : '') + '>Hayır, stoğa dönmesin</option>';
    return (
      '<div class="sp-field" id="item-refund"><span class="sp-field-label">Ürün bazlı iade</span>' + quantities +
      '<select class="sp-input" data-item-payment="1" aria-label="İadenin düşeceği ödeme"' + disabled + '>' + paymentOptions + '</select>' +
      '<select class="sp-input" data-item-stock="1" aria-label="Stoğa geri dönüş"' + disabled + '>' + stockOptions + '</select>' +
      renderReasonField('items', 'İade gerekçesi') +
      '<button class="sp-btn sp-btn-secondary" type="button" data-refund-action="request-items"' +
      (state.refundBusy || !itemsReady() ? ' disabled' : '') + '>Seçilen ürünleri iade et</button></div>'
    );
  }

  function renderRefundLines() {
    var lines = state.summary.allocations.filter(function (line) {
      return line.paymentStatus === 'Approved' && !openRefundFor(line.paymentId);
    });
    if (lines.length === 0) return '';
    if (state.refundListFailed) {
      return '<div class="sp-alert-body" role="status">İade istekleri okunamadı. İade istemeden önce sayfayı yenileyin.</div>';
    }
    var disabled = state.refundBusy ? ' disabled' : '';
    return (
      '<div class="sp-field"><span class="sp-field-label">İade iste</span>' +
      lines.map(function (line) {
        var id = escapeHtml(line.paymentId);
        return (
          '<div class="sp-line"><span class="sp-line-method">' + formatMoney(line.amount) + '</span>' +
          renderReasonField('refund:' + line.paymentId, 'İade gerekçesi (' + formatMoney(line.amount) + ')') +
          '<button class="sp-btn sp-btn-secondary" type="button" data-refund-action="request" data-payment-id="' + id + '"' +
          (disabled || (String(state.refundReasons[line.paymentId] || '').trim() ? '' : ' disabled')) + '>İade iste</button></div>'
        );
      }).join('') +
      '</div>'
    );
  }

  function renderRefundRequests() {
    var rows = state.refundRequests || [];
    if (rows.length === 0) return '';
    var disabled = state.refundBusy ? ' disabled' : '';
    return (
      '<div class="sp-field"><span class="sp-field-label">İade istekleri</span>' +
      rows.map(function (row) {
        var id = escapeHtml(row.requestId);
        var actions = '';
        if (row.status === 'Requested') {
          actions =
            renderReasonField('note:' + row.requestId, 'Ret gerekçesi') +
            confirmButtons('approve', id, 'Onayla', 'Evet, iadeyi onayla', disabled, 'sp-btn-primary') +
            '<button class="sp-btn sp-btn-secondary" type="button" data-refund-action="reject" data-request-id="' + id + '"' +
            (disabled || (String(state.refundNotes[row.requestId] || '').trim() ? '' : ' disabled')) + '>Reddet</button>';
        } else if (row.status === 'AwaitingManualRefund') {
          actions =
            '<input class="sp-input" type="text" maxlength="50" data-refund-slip="' + id + '" aria-label="İade fiş numarası" ' +
            'placeholder="Fiş numarası" value="' + escapeHtml(state.refundSlips[row.requestId] || '') + '">' +
            '<button class="sp-btn sp-btn-primary" type="button" data-refund-action="complete" data-request-id="' + id + '"' +
            (disabled || (String(state.refundSlips[row.requestId] || '').trim() ? '' : ' disabled')) + '>İadeyi tamamla</button>';
        }
        return (
          '<div class="sp-line"><span class="sp-line-method">' + formatMoney(row.amount) + ' · ' +
          escapeHtml(REFUND_METHOD_LABELS[row.method] || 'Bilinmiyor') + '</span>' +
          '<span class="sp-line-status">' + escapeHtml(REFUND_STATUS_LABELS[row.status] || 'Bilinmiyor') + '</span>' +
          '<span class="sp-alert-body">' + escapeHtml(row.reason) + '</span>' + actions + '</div>'
        );
      }).join('') +
      '</div>'
    );
  }

  function renderAllowanceLine() {
    var shift = state.refundAllowance;
    if (!shift || !(shift.allowance > 0)) return '';
    return '<div class="sp-alert-body" id="refund-allowance">Kalan vardiya iade bütçeniz: <strong>' +
      escapeHtml(formatMoney(shift.remaining)) + '</strong></div>';
  }

  function renderRefundSection(includeCancel) {
    return (
      (state.refundError
        ? '<div class="sp-alert sp-alert-danger" role="alert"><span class="sp-alert-icon">!</span>' +
          '<div><div class="sp-alert-title">Hata</div><div class="sp-alert-body">' + escapeHtml(state.refundError) + '</div></div></div>'
        : '') +
      (state.refundNotice
        ? '<div class="sp-alert-body" role="status">' + escapeHtml(state.refundNotice) + '</div>'
        : '') +
      renderAllowanceLine() +
      renderRefundLines() + renderItemRefund() + renderRefundRequests() + (includeCancel ? renderCancelSection() : '')
    );
  }

  function refundAction(action, element) {
    var requestId = element.getAttribute('data-request-id');
    if (action === 'reload') {
      state.error = null;
      state.phase = 'checking';
      render();
      loadEverything();
      return;
    }
    if (action === 'dismiss') {
      state.confirmKey = null;
      render();
      return;
    }
    if (action === 'cancel' || action === 'approve') {
      var key = action + ':' + (requestId || '');
      if (state.confirmKey !== key) {
        state.confirmKey = key;
        render();
        return;
      }
    }
    state.confirmKey = null;
    var call;
    if (action === 'cancel') {
      call = api(billBase() + '/cancel', { method: 'POST', body: { reason: state.cancelReason } });
    } else if (action === 'request') {
      var paymentId = element.getAttribute('data-payment-id');
      call = requestRefundCall({ paymentId: paymentId, reason: state.refundReasons[paymentId] || '' });
    } else if (action === 'request-items') {
      call = requestRefundCall({ paymentId: state.itemPayment, reason: state.itemReason, items: chosenItems() || [] });
    } else if (action === 'approve') {
      call = api(refundsBase() + '/' + requestId + '/approve', { method: 'POST' });
    } else if (action === 'reject') {
      call = api(refundsBase() + '/' + requestId + '/reject', {
        method: 'POST', body: { note: state.refundNotes[requestId] || '' },
      });
    } else {
      call = api(refundsBase() + '/' + requestId + '/complete', {
        method: 'POST', body: { slipNumber: state.refundSlips[requestId] || '', note: null },
      });
    }
    state.refundBusy = true;
    state.refundError = null;
    state.refundNotice = null;
    render();
    call.then(function (result) {
      state.refundBusy = false;
      if (result.ok) state.refundRequestKey = null;
      if (!result.ok) {
        state.refundError = describeHttpFailure(result.status, result.body);
        render();
        if (result.status === 409) loadEverything();
        return;
      }
      if (action === 'cancel' || (result.body && result.body.billCancelled)) {
        state.phase = 'cancelled';
        render();
        return;
      }
      var asksRefund = action === 'request' || action === 'request-items';
      var fromAllowance = asksRefund && result.body && result.body.approvedFromAllowance;
      state.refundNotice = fromAllowance
        ? (result.body.status === 'Completed'
          ? 'İade vardiya bütçenizden tamamlandı.'
          : 'İade vardiya bütçenizden ayrıldı; fiş numarasını girerek tamamlayın.')
        : asksRefund ? 'İade isteği gönderildi; onay bekliyor.'
        : action === 'reject' ? 'İade isteği reddedildi.'
        : action === 'approve' ? 'İade isteği onaylandı.' : 'İade tamamlandı.';
      if (action === 'request-items') {
        if (result.body && result.body.amount != null) {
          state.refundNotice += ' İade tutarı: ' + formatMoney(result.body.amount);
        }
        state.itemQty = {};
        state.itemPayment = '';
        state.itemStock = '';
        state.itemReason = '';
        delete state.reasonChoices.items;
      }
      loadEverything();
    }).catch(function () {
      state.refundBusy = false;
      state.refundError = 'Sunucuya ulaşılamadı.';
      render();
    });
  }

  function onRefundClick(event) {
    var printButton = event.target.closest ? event.target.closest('[data-receipt-action="print"]') : null;
    if (printButton) {
      if (!printButton.disabled) printReceipt();
      return;
    }
    var element = event.target.closest ? event.target.closest('[data-refund-action]') : null;
    if (element && !element.disabled) refundAction(element.getAttribute('data-refund-action'), element);
  }

  function toggleItemsButton() {
    var button = app.querySelector('[data-refund-action="request-items"]');
    if (button) button.disabled = state.refundBusy || !itemsReady();
  }

  function onRefundInput(event) {
    var el = event.target;
    var filled = !!String(el.value || '').trim();
    function toggle(selector) {
      var button = app.querySelector(selector);
      if (button) button.disabled = state.refundBusy || !filled;
    }
    if (el.id === 'cancel-reason') {
      state.cancelReason = el.value;
      toggle('[data-refund-action="cancel"]');
      return;
    }
    var reasonFor = el.getAttribute && el.getAttribute('data-refund-reason');
    if (reasonFor) {
      state.refundReasons[reasonFor] = el.value;
      toggle('[data-refund-action="request"][data-payment-id="' + reasonFor + '"]');
      return;
    }
    var qtyFor = el.getAttribute && el.getAttribute('data-item-qty');
    if (qtyFor) {
      state.itemQty[qtyFor] = el.value;
      toggleItemsButton();
      return;
    }
    if (el.getAttribute && el.getAttribute('data-item-reason') !== null) {
      state.itemReason = el.value;
      toggleItemsButton();
      return;
    }
    var slipFor = el.getAttribute && el.getAttribute('data-refund-slip');
    if (slipFor) {
      state.refundSlips[slipFor] = el.value;
      toggle('[data-refund-action="complete"][data-request-id="' + slipFor + '"]');
      return;
    }
    var noteFor = el.getAttribute && el.getAttribute('data-refund-note');
    if (noteFor) {
      state.refundNotes[noteFor] = el.value;
      toggle('[data-refund-action="reject"][data-request-id="' + noteFor + '"]');
    }
  }

  function renderChangeDue() {
    if (!(state.changeDue > 0)) return '';
    return (
      '<div class="sp-change-due" role="status" id="change-due">' +
      '<span>Para üstü</span><strong>' + formatMoney(state.changeDue) + '</strong></div>'
    );
  }

  // Alınan para girildikçe sayfayı yeniden çizmeden önizlemeyi ve düğmeyi günceller.
  function updateChangePreview() {
    var preview = document.getElementById('change-preview');
    var amount = Number(state.amountDraft);
    var tendered = Number(state.tenderedDraft);
    var filled = String(state.tenderedDraft).trim() !== '' && tendered > 0;
    var short = filled && tendered < amount - 0.004;
    if (preview) {
      preview.textContent = !filled ? ''
        : short ? 'Alınan para tutardan az.'
        : 'Para üstü: ' + formatMoney(Math.max(0, tendered - amount));
      preview.className = 'sp-change-preview' + (short ? ' is-short' : '');
    }
  }

  // Bilgi fişi: mali olmayan hesap özeti; sunucudan okunur, #receipt-print içine yazılır ve tarayıcı yazdırma penceresi açılır.
  var RECEIPT_METHODS = { Cash: 'Nakit', Account: 'Cari hesap', Card: 'Banka kartı', Manual: 'EFT / diğer' };
  var RECEIPT_ADJUSTMENTS = {
    DiscountPercentage: 'İndirim', DiscountAmount: 'İndirim', ServiceFee: 'Hizmet bedeli',
    Kuver: 'Kuver', Tip: 'Bahşiş', CustomFee: 'Ek ücret',
  };
  var RECEIPT_PAYMENT_STATUS = { Refunded: 'İade edildi', PartiallyRefunded: 'Kısmen iade edildi' };

  // Net amount kept after refunds; shown only when something was paid back.
  function receiptNetRow(receipt) {
    var refunded = receipt.payments.reduce(function (sum, payment) { return sum + (payment.refundedAmount || 0); }, 0);
    if (!(refunded > 0)) return '';
    var paid = receipt.payments.reduce(function (sum, payment) { return sum + payment.amount; }, 0);
    return '<tr class="sp-receipt-total"><td>Net tahsilat</td><td>' + formatMoney(paid - refunded) + '</td></tr>';
  }

  // KDV dökümü: oran başına matrah ve KDV; toplam satırı ödenecek tutardaki KDV'dir.
  function receiptTaxHtml(receipt) {
    var lines = receipt.taxLines || [];
    if (!lines.length) return '';
    return '<table>' +
      lines.map(function (line) {
        return '<tr><td>KDV %' + escapeHtml(Number(line.ratePercent).toLocaleString('tr-TR')) +
          ' (matrah ' + formatMoney(line.net) + ')</td><td>' + formatMoney(line.tax) + '</td></tr>';
      }).join('') +
      '<tr class="sp-receipt-total"><td>Toplam KDV</td><td>' + formatMoney(receipt.taxTotal) + '</td></tr>' +
      '</table>';
  }

  function receiptHtml(receipt) {
    var opened = receipt.openedAt ? new Date(receipt.openedAt).toLocaleString('tr-TR', { timeZone: 'Europe/Istanbul' }) : '';
    return (
      '<h1>Bilgi Fişi</h1>' +
      '<p class="sp-receipt-note">Bu belge mali fiş değildir.</p>' +
      '<p>Hesap: ' + escapeHtml(receipt.billNumber) + '<br>' + escapeHtml(opened) + '</p>' +
      '<table>' +
      receipt.items.map(function (item) {
        return '<tr><td>' + escapeHtml(Number(item.quantity).toLocaleString('tr-TR')) + ' × ' + escapeHtml(item.name) + '</td><td>' + formatMoney(item.amount) + '</td></tr>' +
          (item.lineDiscount > 0
            ? '<tr class="sp-receipt-refund"><td>İndirim / ikram</td><td>-' + formatMoney(item.lineDiscount) + '</td></tr>'
            : '') +
          (item.refundedQuantity > 0
            ? '<tr class="sp-receipt-refund"><td>İade edildi: ' + escapeHtml(Number(item.refundedQuantity).toLocaleString('tr-TR')) + ' adet</td><td></td></tr>'
            : '');
      }).join('') +
      '</table>' +
      '<table>' +
      '<tr><td>Ara toplam</td><td>' + formatMoney(receipt.subtotal) + '</td></tr>' +
      receipt.adjustments.map(function (adjustment) {
        return '<tr><td>' + escapeHtml(RECEIPT_ADJUSTMENTS[adjustment.type] || 'Ayarlama') + '</td><td>' +
          (adjustment.isDeduction ? '-' : '') + formatMoney(adjustment.amount) + '</td></tr>';
      }).join('') +
      '<tr class="sp-receipt-total"><td>Toplam</td><td>' + formatMoney(receipt.payableAmount) + '</td></tr>' +
      '</table>' +
      receiptTaxHtml(receipt) +
      '<table>' +
      receipt.payments.map(function (payment) {
        var status = RECEIPT_PAYMENT_STATUS[payment.status];
        return '<tr><td>' + escapeHtml(RECEIPT_METHODS[payment.method] || 'Diğer') +
          (status ? ' (' + escapeHtml(status) + ')' : '') + '</td><td>' + formatMoney(payment.amount) + '</td></tr>' +
          (payment.refundedAmount > 0
            ? '<tr class="sp-receipt-refund"><td>İade</td><td>-' + formatMoney(payment.refundedAmount) + '</td></tr>'
            : '');
      }).join('') +
      receiptNetRow(receipt) +
      '</table>' +
      '<p class="sp-receipt-note">Teşekkür ederiz.</p>'
    );
  }

  function printReceipt() {
    state.receiptBusy = true;
    state.receiptError = null;
    render();
    api(billBase() + '/receipt').then(function (result) {
      state.receiptBusy = false;
      if (!result.ok || !result.body) {
        state.receiptError = 'Fiş verisi okunamadı. Tekrar deneyin.';
        render();
        return;
      }
      var sheet = document.getElementById('receipt-print');
      if (!sheet) {
        sheet = document.createElement('div');
        sheet.id = 'receipt-print';
        sheet.className = 'sp-receipt';
        document.body.appendChild(sheet);
      }
      sheet.innerHTML = receiptHtml(result.body);
      render();
      window.print();
    }).catch(function () {
      state.receiptBusy = false;
      state.receiptError = 'Fiş verisi okunamadı. Tekrar deneyin.';
      render();
    });
  }

  function renderSavedShares() {
    if (!state.savedShares.length) return '';
    return '<div class="sp-field"><span class="sp-field-label">Kayıtlı dağılım (kişi başı)</span><div class="sp-method-chips">' +
      state.savedShares.map(function (share, index) {
        return '<button type="button" class="sp-method-chip" data-share="' + index + '">' +
          escapeHtml(share.label) + ' · ' + formatMoney(share.amount) + '</button>';
      }).join('') + '</div></div>';
  }

  function renderLoadError() {
    app.innerHTML =
      '<div class="sp-card sp-center-text">' +
      '<span class="sp-icon-circle danger">!</span>' +
      '<h1 class="sp-title">Hesap Okunamadı</h1>' +
      errorAlert() +
      '<button class="sp-btn sp-btn-primary" type="button" data-refund-action="reload">Tekrar dene</button>' +
      '</div>';
  }

  var BACK_LINK =
    '<a class="sp-btn sp-btn-secondary" href="/pending-checks" ' +
    'style="display:inline-flex;align-items:center;justify-content:center;text-decoration:none;">Bekleyen hesaplara dön</a>';

  function renderCancelled() {
    app.innerHTML =
      '<div class="sp-card sp-center-text">' +
      '<span class="sp-icon-circle danger">!</span>' +
      '<h1 class="sp-title">Hesap İptal Edildi</h1>' +
      '<p class="sp-subtitle">Bu hesap iptal edildi. Sipariş için gerekirse yeni hesap açılabilir.</p>' +
      BACK_LINK +
      '</div>';
  }

  function renderReady() {
    var s = state.summary;
    var remaining = remainingAmount();
    app.innerHTML =
      '<div class="sp-card">' +
      '<div><span class="sp-eyebrow">Hesap Ödeme</span>' +
      '<h1 class="sp-title">Tahsilat</h1></div>' +
      errorAlert() +
      (state.locked
        ? '<div class="sp-alert sp-alert-warning" role="alert"><span class="sp-alert-icon">!</span>' +
          '<div><div class="sp-alert-title">Manuel mutabakat gerekiyor</div>' +
          '<div class="sp-alert-body">Son kart tahsilatı otomatik onaylanamadı. ' +
          'Terminal ekranında onay görmediyseniz tahsil edilmiş sayılmaz; bu hesaba yeni bir tahsilat ' +
          'eklemeden önce mutabakat tamamlanmalı. Mutabakatı yalnızca müdür tamamlayabilir; müdüre yönlendirin.</div></div></div>' +
          renderResolution()
        : '') +
      renderChangeDue() +
      '<div class="sp-summary-row is-total"><span>Toplam</span><span class="value">' + formatMoney(s.payableAmount) + '</span></div>' +
      '<div class="sp-summary-row"><span>Tahsil edilen</span><span class="value">' + formatMoney(s.allocatedTotal) + '</span></div>' +
      '<div class="sp-summary-row is-remaining' + (remaining <= 0.004 ? ' is-zero' : '') + '">' +
      '<span>Kalan</span><span class="value">' + formatMoney(remaining) + '</span></div>' +
      '<div class="sp-divider"></div>' +
      renderAdjustmentList() +
      (state.locked ? '' : renderDiscountForm() + renderTipForm()) +
      renderAllocationLines() +
      renderRefundSection(true) +
      (state.locked ? '' :
        '<div class="sp-field"><span class="sp-field-label">Eşit bölüştür</span>' +
        '<div class="sp-row">' +
        '<input class="sp-input" type="number" min="2" step="1" id="split-count" aria-label="Kişi sayısı" value="' + state.splitCount + '">' +
        '<button class="sp-btn sp-btn-secondary" id="apply-split" type="button">Hesapla</button>' +
        '</div></div>' +
        renderSavedShares() +
        '<div class="sp-field"><span class="sp-field-label">Ödeme yöntemi</span>' + renderMethodChips() + '</div>' +
        '<div class="sp-field"><span class="sp-field-label">Tutar</span>' +
        '<div class="sp-amount-field"><input type="number" step="0.01" min="0.01" id="amount-draft" aria-label="Ödeme tutarı" value="' + state.amountDraft + '">' +
        '<span class="sp-amount-suffix">₺</span></div></div>' +
        (state.selectedMethod === 'Cash'
          ? '<div class="sp-field"><span class="sp-field-label">Alınan para <span style="font-weight:400;color:var(--color-text-dim)">(isteğe bağlı)</span></span>' +
            '<div class="sp-amount-field"><input type="number" step="0.01" min="0.01" id="tendered-draft" aria-label="Alınan para (isteğe bağlı)" value="' + escapeHtml(state.tenderedDraft) + '">' +
            '<span class="sp-amount-suffix">₺</span></div>' +
            '<div class="sp-change-preview" id="change-preview" role="status"></div></div>'
          : '') +
        (state.selectedMethod !== 'Cash' && state.selectedMethod !== 'Account'
          ? '<div class="sp-field"><span class="sp-field-label">Not <span style="font-weight:400;color:var(--color-text-dim)">(opsiyonel)</span></span>' +
            '<input class="sp-input" type="text" id="note-draft" aria-label="Ödeme notu (opsiyonel)" value="' + escapeHtml(state.noteDraft) + '"></div>'
          : '') +
        (state.selectedMethod === 'Account' ? renderCustomerPicker() : '') +
        (state.selectedMethod === 'Eft'
          ? '<div class="sp-confirm-row"><input type="checkbox" id="eft-confirm"' + (state.eftConfirmed ? ' checked' : '') + '>' +
            '<label for="eft-confirm">Tutarı işletmenin banka hesap hareketinde gördüm</label></div>'
          : '') +
        '<button class="sp-btn sp-btn-primary" id="submit-tender" ' +
        ((state.busy || (state.selectedMethod === 'Eft' && !state.eftConfirmed)
          || (state.selectedMethod === 'Account' && !selectedCustomer())) ? 'disabled' : '') + '>' +
        (state.busy ? 'Gönderiliyor…' : 'Ödemeyi Ekle') + '</button>') +
      '</div>';

    document.querySelectorAll('.sp-method-chip').forEach(function (button) {
      button.addEventListener('click', function () {
        if (button.disabled) return;
        var method = button.getAttribute('data-method');
        state.selectedMethod = method;
        state.eftConfirmed = false;
        // V1-RMD-314: a different method is a genuinely different tender attempt - never replay the
        // previous method's in-flight key onto this one.
        state.tenderIdempotencyKey = null;
        if (method === 'Account' && state.customers === null) loadCustomers();
        render();
        // render() replaces the whole card's innerHTML, destroying the
        // clicked chip and creating a fresh node in its place - without
        // this, a keyboard/switch-access user's focus silently falls back
        // to <body> after every method selection (same class of bug as
        // WaiterPwa's own re-render-loses-focus fixes: re-find the fresh
        // node by its stable data attribute and re-apply focus to it).
        var refreshedChip = document.querySelector('.sp-method-chip[data-method="' + method + '"]');
        if (refreshedChip) refreshedChip.focus();
      });
    });
    var splitCountInput = document.getElementById('split-count');
    if (splitCountInput) splitCountInput.addEventListener('input', function () { state.splitCount = this.value; });
    document.querySelectorAll('[data-share]').forEach(function (button) {
      button.addEventListener('click', function () {
        var share = state.savedShares[Number(button.getAttribute('data-share'))];
        if (!share) return;
        state.splitLeft = 0;
        state.amountDraft = Math.min(share.amount, remainingAmount()).toFixed(2);
        state.tenderIdempotencyKey = null;
        render();
      });
    });
    var applySplit = document.getElementById('apply-split');
    if (applySplit) applySplit.addEventListener('click', submitEqualSplit);
    var amountInput = document.getElementById('amount-draft');
    if (amountInput) amountInput.addEventListener('input', function () {
      state.amountDraft = this.value;
      // V1-RMD-314: a hand-edited amount is a genuinely different tender attempt - never replay the
      // previous amount's in-flight key onto this one.
      state.tenderIdempotencyKey = null;
    });
    var noteInput = document.getElementById('note-draft');
    if (noteInput) noteInput.addEventListener('input', function () { state.noteDraft = this.value; });
    var tenderedInput = document.getElementById('tendered-draft');
    if (tenderedInput) {
      tenderedInput.addEventListener('input', function () {
        state.tenderedDraft = this.value;
        // Farklı bir tahsilat denemesi; önceki anahtar yeniden kullanılmaz.
        state.tenderIdempotencyKey = null;
        updateChangePreview();
      });
      updateChangePreview();
    }
    if (amountInput) amountInput.addEventListener('input', updateChangePreview);
    var eftConfirmInput = document.getElementById('eft-confirm');
    if (eftConfirmInput) eftConfirmInput.addEventListener('change', function () {
      state.eftConfirmed = this.checked;
      render();
      // Same re-render-destroys-the-node issue as the method chips above -
      // re-find the fresh checkbox by its stable id and restore focus to it.
      var refreshedCheckbox = document.getElementById('eft-confirm');
      if (refreshedCheckbox) refreshedCheckbox.focus();
    });
    var slipInput = document.getElementById('claim-slip');
    if (slipInput) slipInput.addEventListener('input', function () { state.slipDraft = this.value; });
    var claimButton = document.getElementById('claim-card-charged');
    if (claimButton) claimButton.addEventListener('click', claimCardCharged);
    var decisionNoteInput = document.getElementById('decision-note');
    if (decisionNoteInput) decisionNoteInput.addEventListener('input', function () { state.decisionNote = this.value; });
    var approveButton = document.getElementById('approve-confirmation');
    if (approveButton) approveButton.addEventListener('click', function () { decideConfirmation('approve'); });
    var rejectButton = document.getElementById('reject-confirmation');
    if (rejectButton) rejectButton.addEventListener('click', function () { decideConfirmation('reject'); });
    var resolveReasonInput = document.getElementById('resolve-reason');
    if (resolveReasonInput) resolveReasonInput.addEventListener('input', function () { state.resolveReason = this.value; });
    var resolveButton = document.getElementById('resolve-not-charged');
    if (resolveButton) resolveButton.addEventListener('click', resolveNotCharged);
    var submitButton = document.getElementById('submit-tender');
    if (submitButton) submitButton.addEventListener('click', submitTender);
    var discountReasonInput = document.getElementById('discount-reason');
    if (discountReasonInput) discountReasonInput.addEventListener('change', function () { state.discountReason = this.value; });
    var discountCalcTypeInput = document.getElementById('discount-calc-type');
    if (discountCalcTypeInput) discountCalcTypeInput.addEventListener('change', function () { state.discountCalcType = this.value; state.discountConfirm = false; });
    var discountValueInput = document.getElementById('discount-value');
    if (discountValueInput) discountValueInput.addEventListener('input', function () { state.discountValueDraft = this.value; state.discountConfirm = false; });
    var discountNoteInput = document.getElementById('discount-note');
    if (discountNoteInput) discountNoteInput.addEventListener('input', function () { state.discountNoteDraft = this.value; });
    var submitDiscountButton = document.getElementById('submit-discount');
    if (submitDiscountButton) submitDiscountButton.addEventListener('click', submitDiscount);
    var tipAmountInput = document.getElementById('tip-amount');
    if (tipAmountInput) tipAmountInput.addEventListener('input', function () { state.tipAmountDraft = this.value; });
    var tipNoteInput = document.getElementById('tip-note');
    if (tipNoteInput) tipNoteInput.addEventListener('input', function () { state.tipNoteDraft = this.value; });
    var submitTipButton = document.getElementById('submit-tip');
    if (submitTipButton) submitTipButton.addEventListener('click', submitTip);
    var customerSearchInput = document.getElementById('customer-search');
    if (customerSearchInput) {
      customerSearchInput.addEventListener('input', function () { state.customerSearch = this.value; });
      customerSearchInput.addEventListener('keydown', function (event) { if (event.key === 'Enter') loadCustomers(); });
    }
    var customerSearchButton = document.getElementById('customer-search-submit');
    if (customerSearchButton) customerSearchButton.addEventListener('click', loadCustomers);
    document.querySelectorAll('.sp-customer').forEach(function (button) {
      button.addEventListener('click', function () {
        var customerId = button.getAttribute('data-customer-id');
        state.selectedCustomerId = customerId;
        // A different customer is a different charge attempt - never replay the previous one's key.
        state.tenderIdempotencyKey = null;
        render();
        var refreshed = document.querySelector('.sp-customer[data-customer-id="' + customerId + '"]');
        if (refreshed) refreshed.focus();
      });
    });
  }

  function renderPaid() {
    app.innerHTML =
      '<div class="sp-card sp-center-text">' +
      '<span class="sp-icon-circle success">✓</span>' +
      '<h1 class="sp-title">Hesap Ödendi</h1>' +
      '<p class="sp-subtitle">Bu hesabın tamamı tahsil edildi.</p>' +
      renderChangeDue() +
      (state.receiptError
        ? '<div class="sp-alert-body" role="alert">' + escapeHtml(state.receiptError) + '</div>'
        : '') +
      '<button class="sp-btn sp-btn-primary" type="button" id="print-receipt" data-receipt-action="print"' +
      (state.receiptBusy ? ' disabled' : '') + '>' + (state.receiptBusy ? 'Fiş hazırlanıyor…' : 'Fişi yazdır') + '</button>' +
      BACK_LINK +
      '</div>' +
      '<div class="sp-card">' + renderRefundSection(false) + '</div>';
  }

  function render() {
    if (state.phase === 'checking') {
      app.innerHTML = '<div class="sp-loading">Hesap yükleniyor…</div>';
      return;
    }
    if (state.summary) ensurePendingConfirmationPoll();
    app.setAttribute('aria-busy', state.busy ? 'true' : 'false');
    switch (state.phase) {
      case 'missingBill': return renderMissingBill();
      case 'login': return renderLogin();
      case 'loadError': return renderLoadError();
      case 'ready': return renderReady();
      case 'paid': return renderPaid();
      case 'cancelled': return renderCancelled();
      default: return renderReady();
    }
  }

  app.addEventListener('click', onRefundClick);
  app.addEventListener('input', onRefundInput);
  app.addEventListener('change', onReasonChange);

  bootstrap();
})();
