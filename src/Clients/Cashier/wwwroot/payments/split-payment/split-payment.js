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

  var METHOD_LABELS = { Cash: 'Nakit', BankCard: 'Kredi/Banka Kartı', Eft: 'EFT/Havale' };

  var state = {
    phase: 'checking',
    terminalId: null,
    billId: null,
    summary: null,
    cashSessionId: null,
    cashSessionOpen: false,
    selectedMethod: 'Cash',
    splitCount: 2,
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
    discountError: null,
    discountNotice: null,
    tipAmountDraft: '',
    tipNoteDraft: '',
    tipBusy: false,
    tipError: null,
    // V1-RMD-314 (finding K3): same retry-reuses-the-same-key fix as tenderIdempotencyKey above, for the
    // discount/tip apply requests.
    discountIdempotencyKey: null,
    tipIdempotencyKey: null,
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
      headers: options.body ? { 'Content-Type': 'application/json' } : undefined,
      body: options.body ? JSON.stringify(options.body) : undefined,
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
        state.phase = 'login';
        render();
        return;
      }
      state.terminalId = result.body.terminalId;
      state.userId = result.body.userId || null;
      loadEverything();
    }).catch(function () {
      state.phase = 'login';
      render();
    });
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
      if (!summaryResult.ok) {
        state.phase = 'login';
        render();
        return;
      }

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
      render();
    }).catch(function () {
      setError('Hesap bilgisi okunamadı. Bağlantınızı kontrol edin.');
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

  function submitEqualSplit() {
    var count = Math.max(2, Math.round(Number(state.splitCount) || 2));
    var perLine = Math.round((remainingAmount() / count) * 100) / 100;
    state.amountDraft = perLine.toFixed(2);
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
      state.discountIdempotencyKey = null;
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
      state.tipIdempotencyKey = null;
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

    setBusy(true);
    state.error = null;
    // V1-RMD-314: reuse the in-flight key across a retry of this exact attempt - see the field's own
    // doc comment. A brand-new key is minted only when none is pending.
    if (!state.tenderIdempotencyKey) state.tenderIdempotencyKey = crypto.randomUUID();
    var idempotencyKey = state.tenderIdempotencyKey;

    var request = state.selectedMethod === 'Cash'
      ? api('/api/v1/terminals/' + state.terminalId + '/cash-sessions/' + state.cashSessionId + '/cash-tender', {
          method: 'POST',
          body: { BillId: state.billId, AmountDue: amount, TenderedAmount: amount, IdempotencyKey: idempotencyKey },
        }).then(function (result) {
          if (!result.ok) return result;
          return { ok: true, status: result.status, body: { outcome: 'Approved', approvedAmount: result.body.approvedAmount } };
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
      state.tenderIdempotencyKey = null;
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
      state.lastResult = result.body;
      state.noteDraft = '';
      state.eftConfirmed = false;
      if (result.body.outcome === 'RequiresReconciliation') {
        // Bilinmeyen durum kilidi: bu satır onaylanana kadar bu sekmede
        // hesaba yeni bir tahsilat eklenemez - mükerrer tahsilat riskini
        // önler. Gerçek Token/Beko entegrasyonu (V13-HUG-001) gelene kadar
        // manuel mutabakat gerekiyor.
        state.locked = true;
        return refreshSummary().then(render);
      }
      return refreshSummary().then(render);
    }).catch(function () {
      state.busy = false;
      setError('Sunucuya ulaşılamadı.');
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
    var methods = ['Cash', 'BankCard', 'Eft'];
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
      (state.discountBusy ? 'Gönderiliyor…' : 'İndirim uygula') + '</button>' +
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
          '<div class="sp-alert-body">Son kart tahsilatı otomatik onaylanamadı. Bu hesaba yeni bir tahsilat ' +
          'eklemeden önce mutabakat tamamlanmalı.</div></div></div>' +
          renderResolution()
        : '') +
      '<div class="sp-summary-row is-total"><span>Toplam</span><span class="value">' + formatMoney(s.payableAmount) + '</span></div>' +
      '<div class="sp-summary-row"><span>Tahsil edilen</span><span class="value">' + formatMoney(s.allocatedTotal) + '</span></div>' +
      '<div class="sp-summary-row is-remaining' + (remaining <= 0.004 ? ' is-zero' : '') + '">' +
      '<span>Kalan</span><span class="value">' + formatMoney(remaining) + '</span></div>' +
      '<div class="sp-divider"></div>' +
      renderAdjustmentList() +
      (state.locked ? '' : renderDiscountForm() + renderTipForm()) +
      renderAllocationLines() +
      (state.locked ? '' :
        '<div class="sp-field"><span class="sp-field-label">Eşit bölüştür</span>' +
        '<div class="sp-row">' +
        '<input class="sp-input" type="number" min="2" step="1" id="split-count" aria-label="Kişi sayısı" value="' + state.splitCount + '">' +
        '<button class="sp-btn sp-btn-secondary" id="apply-split" type="button">Hesapla</button>' +
        '</div></div>' +
        '<div class="sp-field"><span class="sp-field-label">Ödeme yöntemi</span>' + renderMethodChips() + '</div>' +
        '<div class="sp-field"><span class="sp-field-label">Tutar</span>' +
        '<div class="sp-amount-field"><input type="number" step="0.01" min="0.01" id="amount-draft" aria-label="Ödeme tutarı" value="' + state.amountDraft + '">' +
        '<span class="sp-amount-suffix">₺</span></div></div>' +
        (state.selectedMethod !== 'Cash'
          ? '<div class="sp-field"><span class="sp-field-label">Not <span style="font-weight:400;color:var(--color-text-dim)">(opsiyonel)</span></span>' +
            '<input class="sp-input" type="text" id="note-draft" aria-label="Ödeme notu (opsiyonel)" value="' + escapeHtml(state.noteDraft) + '"></div>'
          : '') +
        (state.selectedMethod === 'Eft'
          ? '<div class="sp-confirm-row"><input type="checkbox" id="eft-confirm"' + (state.eftConfirmed ? ' checked' : '') + '>' +
            '<label for="eft-confirm">Tutarı işletmenin banka hesap hareketinde gördüm</label></div>'
          : '') +
        '<button class="sp-btn sp-btn-primary" id="submit-tender" ' +
        ((state.busy || (state.selectedMethod === 'Eft' && !state.eftConfirmed)) ? 'disabled' : '') + '>' +
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
    if (discountCalcTypeInput) discountCalcTypeInput.addEventListener('change', function () { state.discountCalcType = this.value; });
    var discountValueInput = document.getElementById('discount-value');
    if (discountValueInput) discountValueInput.addEventListener('input', function () { state.discountValueDraft = this.value; });
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
  }

  function renderPaid() {
    app.innerHTML =
      '<div class="sp-card sp-center-text">' +
      '<span class="sp-icon-circle success">✓</span>' +
      '<h1 class="sp-title">Hesap Ödendi</h1>' +
      '<p class="sp-subtitle">Bu hesabın tamamı tahsil edildi.</p>' +
      '</div>';
  }

  function render() {
    if (state.phase === 'checking') {
      app.innerHTML = '<div class="sp-loading">Hesap yükleniyor…</div>';
      return;
    }
    app.setAttribute('aria-busy', state.busy ? 'true' : 'false');
    switch (state.phase) {
      case 'missingBill': return renderMissingBill();
      case 'login': return renderLogin();
      case 'ready': return renderReady();
      case 'paid': return renderPaid();
      default: return renderReady();
    }
  }

  bootstrap();
})();
