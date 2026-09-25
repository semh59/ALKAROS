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
    ]).then(function (results) {
      var summaryResult = results[0];
      var cashResult = results[1];

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
      render();
    }).catch(function () {
      setError('Hesap bilgisi okunamadı. Bağlantınızı kontrol edin.');
    });
  }

  function remainingAmount() {
    return state.summary ? state.summary.remainingAmount : 0;
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
    var idempotencyKey = crypto.randomUUID();

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

  function errorAlert() {
    if (!state.error) return '';
    return (
      '<div class="sp-alert sp-alert-danger">' +
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

  function renderMethodChips() {
    var methods = ['Cash', 'BankCard', 'Eft'];
    return (
      '<div class="sp-method-chips">' +
      methods.map(function (method) {
        var disabled = method === 'Cash' && !state.cashSessionOpen;
        var active = state.selectedMethod === method;
        return (
          '<button type="button" class="sp-method-chip' + (active ? ' is-active' : '') + '" ' +
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
          : '<input class="sp-input" type="text" id="decision-note" maxlength="500" placeholder="Not (isteğe bağlı)" value="' +
            escapeHtml(state.decisionNote) + '">' +
            '<button class="sp-btn sp-btn-primary" id="approve-confirmation" type="button"' + disabled + '>Onayla: kart çekildi</button>') +
        '<button class="sp-btn sp-btn-secondary" id="reject-confirmation" type="button"' + disabled + '>' +
        (mine ? 'Bildirimi geri çek' : 'Reddet') + '</button></div>'
      );
    }
    return (
      '<div class="sp-field"><span class="sp-field-label">Yetkili müdür: kart çekildi mi? (fiş numarası ile, ikinci bir yetkili onaylar)</span>' +
      '<input class="sp-input" type="text" id="claim-slip" maxlength="32" placeholder="Fiş numarası" value="' + escapeHtml(state.slipDraft) + '">' +
      '<button class="sp-btn sp-btn-secondary" id="claim-card-charged" type="button"' + disabled + '>Kart çekildi: onaya gönder</button></div>' +
      '<div class="sp-field"><span class="sp-field-label">Yetkili müdür: kart çekilmedi mi?</span>' +
      '<input class="sp-input" type="text" id="resolve-reason" maxlength="500" ' +
      'placeholder="Gerekçe (zorunlu)" value="' + escapeHtml(state.resolveReason) + '">' +
      '<button class="sp-btn sp-btn-secondary" id="resolve-not-charged" type="button"' + disabled + '>Kart çekilmedi olarak çöz</button></div>'
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
        ? '<div class="sp-alert sp-alert-warning"><span class="sp-alert-icon">!</span>' +
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
      renderAllocationLines() +
      (state.locked ? '' :
        '<div class="sp-field"><span class="sp-field-label">Eşit bölüştür</span>' +
        '<div class="sp-row">' +
        '<input class="sp-input" type="number" min="2" step="1" id="split-count" value="' + state.splitCount + '">' +
        '<button class="sp-btn sp-btn-secondary" id="apply-split" type="button">Hesapla</button>' +
        '</div></div>' +
        '<div class="sp-field"><span class="sp-field-label">Ödeme yöntemi</span>' + renderMethodChips() + '</div>' +
        '<div class="sp-field"><span class="sp-field-label">Tutar</span>' +
        '<div class="sp-amount-field"><input type="number" step="0.01" min="0.01" id="amount-draft" value="' + state.amountDraft + '">' +
        '<span class="sp-amount-suffix">₺</span></div></div>' +
        (state.selectedMethod !== 'Cash'
          ? '<div class="sp-field"><span class="sp-field-label">Not <span style="font-weight:400;color:var(--color-text-dim)">(opsiyonel)</span></span>' +
            '<input class="sp-input" type="text" id="note-draft" value="' + escapeHtml(state.noteDraft) + '"></div>'
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
    if (amountInput) amountInput.addEventListener('input', function () { state.amountDraft = this.value; });
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
