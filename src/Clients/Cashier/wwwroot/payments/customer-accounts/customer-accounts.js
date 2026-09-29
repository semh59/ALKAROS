// V1-RMD-443: Cari Hesaplar standalone page - customers, their account statement, cash collection towards the
// account (V14-ACC-005/009 through V1-RMD-442) and the manager's credit limit (V1-RMD-440). Same conventions as
// split-payment.js and cash-session.js: no bundler, IIFE module, credentials:'include' fetch, Turkish messages.
(function () {
  'use strict';

  var money = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' });
  var dateTime = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' });
  var app = document.getElementById('app');

  var state = {
    phase: 'checking',
    terminalId: null,
    cashSessionId: null,
    search: '',
    customers: [],
    selectedId: null,
    statement: null,
    newName: '',
    newPhone: '',
    receiptAmount: '',
    receiptKey: null,
    lastReceipt: null,
    limitDraft: '',
    termDraft: '',
    busy: false,
    error: null,
    notice: null,
  };

  function escapeHtml(value) {
    return String(value == null ? '' : value).replace(/[&<>"']/g, function (ch) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
    });
  }

  function formatMoney(amount) { return money.format(Number(amount || 0)); }

  function describeHttpFailure(status, body) {
    if (body && body.error && body.error.message) return body.error.message;
    if (status === 401) return 'Oturumunuz sona ermiş. Lütfen tekrar giriş yapın.';
    if (status === 403) return 'Bu işlem için yetkiniz yok.';
    if (status === 404) return 'Kayıt bulunamadı.';
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
      if (response.status === 204) return { ok: true, status: response.status, body: null };
      return response.json().catch(function () { return null; }).then(function (body) {
        return { ok: response.ok, status: response.status, body: body };
      });
    });
  }

  function base() { return '/api/v1/terminals/' + state.terminalId; }

  function fail(result) {
    state.busy = false;
    // A dropped cashier session sends the page to the sign-in prompt, from any call.
    if (result.status === 401) state.phase = 'login';
    else state.error = describeHttpFailure(result.status, result.body);
    render();
  }

  // ---- data ---------------------------------------------------------------

  function bootstrap() {
    api('/api/v1/auth/session/current').then(function (result) {
      if (!result.ok) { state.phase = 'login'; render(); return; }
      state.terminalId = result.body.terminalId;
      return Promise.all([
        api(base() + '/cash-sessions/active'),
        loadCustomers(),
      ]).then(function (results) {
        state.cashSessionId = results[0].ok ? results[0].body.cashSessionId : null;
        state.phase = 'ready';
        render();
      });
    }).catch(function () { state.phase = 'login'; render(); });
  }

  function loadCustomers() {
    var query = state.search.trim() ? '?search=' + encodeURIComponent(state.search.trim()) : '';
    return api(base() + '/customers' + query).then(function (result) {
      if (!result.ok) { fail(result); return; }
      state.customers = result.body;
      render();
    });
  }

  // `notice` survives the reload so a confirmation shown by the caller (saved, added) is not wiped by it.
  function openCustomer(customerId, notice) {
    state.selectedId = customerId;
    state.lastReceipt = null;
    state.receiptAmount = '';
    state.receiptKey = null;
    state.error = null;
    state.notice = notice || null;
    return api(base() + '/customers/' + customerId + '/statement').then(function (result) {
      if (!result.ok) { fail(result); return; }
      state.statement = result.body;
      state.limitDraft = String(result.body.customer.creditLimit);
      state.termDraft = result.body.customer.paymentTermDays == null ? '' : String(result.body.customer.paymentTermDays);
      render();
      var heading = document.getElementById('ca-customer-heading');
      if (heading) heading.focus();
    });
  }

  function createCustomer() {
    state.busy = true;
    state.error = null;
    render();
    api(base() + '/customers', {
      method: 'POST',
      body: { Name: state.newName, Phone: state.newPhone || null },
    }).then(function (result) {
      state.busy = false;
      if (!result.ok) { fail(result); return; }
      state.newName = '';
      state.newPhone = '';
      return loadCustomers().then(function () {
        return openCustomer(result.body.customerId, 'Müşteri eklendi: ' + result.body.name);
      });
    }).catch(function () { state.busy = false; state.error = 'Sunucuya ulaşılamadı.'; render(); });
  }

  function receiveCash() {
    var amount = Number(state.receiptAmount);
    if (!(amount > 0)) { state.error = 'Tutar sıfırdan büyük olmalı.'; render(); return; }
    if (amount > state.statement.customer.balance + 0.004) {
      state.error = 'Tahsilat, müşterinin borcunu aşamaz.';
      render();
      return;
    }
    state.busy = true;
    state.error = null;
    // Kept across a network-failure retry so the retry replays instead of collecting twice.
    if (!state.receiptKey) state.receiptKey = crypto.randomUUID();
    api(base() + '/cash-sessions/' + state.cashSessionId + '/account-receipts', {
      method: 'POST',
      body: { CustomerId: state.selectedId, Amount: amount, IdempotencyKey: state.receiptKey },
    }).then(function (result) {
      state.busy = false;
      state.receiptKey = null;
      if (!result.ok) { fail(result); return; }
      var receipt = result.body;
      return Promise.all([loadCustomers(), openCustomer(state.selectedId)]).then(function () {
        state.lastReceipt = receipt;
        render();
      });
    }).catch(function () { state.busy = false; state.error = 'Sunucuya ulaşılamadı; aynı tutarla tekrar deneyebilirsiniz.'; render(); });
  }

  function saveLimit() {
    var limit = Number(state.limitDraft);
    var term = state.termDraft.trim() === '' ? null : Number(state.termDraft);
    state.busy = true;
    state.error = null;
    render();
    api('/api/v1/management/customers/' + state.selectedId + '/credit-terms', {
      method: 'PUT',
      body: { CreditLimit: limit, PaymentTermDays: term },
    }).then(function (result) {
      state.busy = false;
      if (!result.ok) {
        state.error = result.status === 401 || result.status === 403
          ? 'Kredi limitini yalnız yönetici oturumu değiştirebilir. Yönetici olarak giriş yapıp tekrar deneyin.'
          : describeHttpFailure(result.status, result.body);
        render();
        return;
      }
      return Promise.all([loadCustomers(), openCustomer(state.selectedId, 'Kredi limiti kaydedildi.')]).then(render);
    }).catch(function () { state.busy = false; state.error = 'Sunucuya ulaşılamadı.'; render(); });
  }

  // ---- rendering ----------------------------------------------------------

  function alerts() {
    return (state.error
      ? '<div class="sp-alert sp-alert-danger" role="alert"><span class="sp-alert-icon">!</span>' +
        '<div><div class="sp-alert-title">Hata</div><div class="sp-alert-body">' + escapeHtml(state.error) + '</div></div></div>'
      : '') +
      (state.notice ? '<div class="sp-alert-body" role="status">' + escapeHtml(state.notice) + '</div>' : '');
  }

  function renderList() {
    return (
      '<section aria-labelledby="ca-list-heading">' +
      '<h2 class="sp-field-label" id="ca-list-heading">Müşteriler</h2>' +
      '<div class="sp-row">' +
      '<input class="sp-input" type="search" id="ca-search" placeholder="Ad veya telefon" aria-label="Müşteri ara (ad veya telefon)" value="' + escapeHtml(state.search) + '">' +
      '<button class="sp-btn sp-btn-secondary" id="ca-search-submit" type="button">Ara</button>' +
      '</div>' +
      (state.customers.length === 0
        ? '<div class="sp-alert-body" role="status">Müşteri bulunamadı.</div>'
        : '<div class="ca-list" role="list">' + state.customers.map(function (c) {
          var active = c.customerId === state.selectedId;
          return (
            '<div role="listitem"><button type="button" class="sp-customer' + (active ? ' is-active' : '') + '" ' +
            (active ? 'aria-current="true" ' : '') + 'data-customer-id="' + escapeHtml(c.customerId) + '">' +
            '<span class="sp-customer-name">' + escapeHtml(c.name) + (c.phoneMasked ? ' · ' + escapeHtml(c.phoneMasked) : '') + '</span>' +
            '<span class="sp-customer-meta">Borç ' + formatMoney(c.balance) + ' · Limit ' + formatMoney(c.creditLimit) + '</span>' +
            '</button></div>'
          );
        }).join('') + '</div>') +
      '<div class="sp-divider"></div>' +
      '<h2 class="sp-field-label">Yeni müşteri</h2>' +
      '<input class="sp-input" type="text" id="ca-new-name" maxlength="120" placeholder="Ad soyad" aria-label="Yeni müşterinin adı" value="' + escapeHtml(state.newName) + '">' +
      '<input class="sp-input" type="tel" id="ca-new-phone" maxlength="25" placeholder="Telefon (isteğe bağlı)" aria-label="Yeni müşterinin telefonu (isteğe bağlı)" value="' + escapeHtml(state.newPhone) + '">' +
      '<button class="sp-btn sp-btn-secondary" id="ca-create" type="button"' + (state.busy ? ' disabled' : '') + '>Müşteri ekle</button>' +
      '</section>'
    );
  }

  function renderStatement() {
    if (!state.statement) {
      return '<section><p class="sp-subtitle">Ekstreyi görmek için soldan bir müşteri seçin.</p></section>';
    }
    var c = state.statement.customer;
    var disabled = state.busy ? ' disabled' : '';
    var canReceive = state.cashSessionId && c.balance > 0;
    return (
      '<section aria-labelledby="ca-customer-heading">' +
      '<h2 class="sp-title" id="ca-customer-heading" tabindex="-1">' + escapeHtml(c.name) + '</h2>' +
      '<div class="sp-summary-row is-total"><span>Borç</span><span class="value">' + formatMoney(c.balance) + '</span></div>' +
      '<div class="sp-summary-row"><span>Kredi limiti</span><span class="value">' + formatMoney(c.creditLimit) + '</span></div>' +
      '<div class="sp-summary-row"><span>Kullanılabilir</span><span class="value">' + formatMoney(c.availableCredit) + '</span></div>' +
      '<div class="sp-summary-row"><span>Vade</span><span class="value">' + (c.paymentTermDays ? c.paymentTermDays + ' gün' : 'Tanımsız') + '</span></div>' +
      '<div class="sp-divider"></div>' +
      '<div class="sp-field"><span class="sp-field-label">Nakit tahsilat</span>' +
      (state.lastReceipt
        ? '<div class="sp-alert-body" role="status">Tahsil edildi: ' + formatMoney(state.lastReceipt.amount) + ' · makbuz ' +
          escapeHtml(state.lastReceipt.receiptNumber) + ' · kalan borç ' + formatMoney(state.lastReceipt.balanceAfter) + '</div>'
        : '') +
      (!state.cashSessionId
        ? '<div class="sp-alert-body" role="status">Nakit tahsilat için bu terminalde açık bir kasa oturumu gerekir.</div>'
        : c.balance <= 0
          ? '<div class="sp-alert-body" role="status">Müşterinin borcu yok.</div>'
          : '') +
      '<div class="sp-row">' +
      '<input class="sp-input" type="number" step="0.01" min="0.01" id="ca-receipt-amount" placeholder="Tutar" aria-label="Tahsil edilecek tutar" value="' + escapeHtml(state.receiptAmount) + '"' + (canReceive ? '' : ' disabled') + '>' +
      '<button class="sp-btn sp-btn-primary" id="ca-receive" type="button"' + (canReceive ? disabled : ' disabled') + '>Tahsil et</button>' +
      '</div></div>' +
      '<div class="sp-field"><span class="sp-field-label">Kredi limiti ve vade (yönetici)</span>' +
      '<div class="sp-row">' +
      '<input class="sp-input" type="number" step="0.01" min="0" id="ca-limit" aria-label="Kredi limiti (TL)" value="' + escapeHtml(state.limitDraft) + '">' +
      '<input class="sp-input" type="number" step="1" min="1" max="365" id="ca-term" placeholder="Vade (gün)" aria-label="Vade (gün, boş bırakılırsa vade takibi yok)" value="' + escapeHtml(state.termDraft) + '">' +
      '<button class="sp-btn sp-btn-secondary" id="ca-save-limit" type="button"' + disabled + '>Kaydet</button>' +
      '</div></div>' +
      '<div class="sp-divider"></div>' +
      '<h3 class="sp-field-label">Hareketler</h3>' +
      (state.statement.entries.length === 0
        ? '<p class="sp-subtitle">Henüz hareket yok.</p>'
        : '<table class="ca-table"><caption class="sp-field-label" style="text-align:left">Son hareketler</caption>' +
          '<thead><tr><th scope="col">Tarih</th><th scope="col">İşlem</th><th scope="col" class="ca-amount">Tutar</th></tr></thead><tbody>' +
          state.statement.entries.map(function (e) {
            return '<tr><td>' + escapeHtml(dateTime.format(new Date(e.occurredAt))) + '</td><td>' + escapeHtml(e.description) + '</td>' +
              '<td class="ca-amount">' + (e.signedAmount < 0 ? '−' : '+') + formatMoney(Math.abs(e.signedAmount)) + '</td></tr>';
          }).join('') + '</tbody></table>') +
      (state.statement.receipts.length === 0
        ? ''
        : '<h3 class="sp-field-label">Makbuzlar</h3><ul>' + state.statement.receipts.map(function (r) {
          return '<li>' + escapeHtml(r.receiptNumber) + ' · ' + formatMoney(r.amount) + ' · ' + escapeHtml(dateTime.format(new Date(r.issuedAt))) + '</li>';
        }).join('') + '</ul>') +
      '</section>'
    );
  }

  function renderReady() {
    app.innerHTML =
      '<div class="sp-card">' +
      '<div class="ca-header"><a class="ca-back" href="../../index.html">← Kasaya dön</a>' +
      '<span class="sp-eyebrow">Müşteri</span><h1 class="sp-title">Cari Hesaplar</h1></div>' +
      alerts() +
      '<div class="ca-grid">' + renderList() + renderStatement() + '</div>' +
      '</div>';
    bind();
  }

  function renderLogin() {
    app.innerHTML =
      '<div class="sp-card"><div><span class="sp-eyebrow">ALKAROS</span><h1 class="sp-title">Kasa Girişi</h1>' +
      '<p class="sp-subtitle">Oturumunuz sona erdi. Devam etmek için tekrar giriş yapın.</p></div>' +
      '<a class="sp-btn sp-btn-primary" href="/" style="display:inline-flex;align-items:center;justify-content:center;text-decoration:none;">Giriş ekranına dön</a></div>';
  }

  function bind() {
    function on(id, event, handler) {
      var el = document.getElementById(id);
      if (el) el.addEventListener(event, handler);
    }
    on('ca-search', 'input', function () { state.search = this.value; });
    on('ca-search', 'keydown', function (event) { if (event.key === 'Enter') loadCustomers(); });
    on('ca-search-submit', 'click', loadCustomers);
    on('ca-new-name', 'input', function () { state.newName = this.value; });
    on('ca-new-phone', 'input', function () { state.newPhone = this.value; });
    on('ca-create', 'click', createCustomer);
    on('ca-receipt-amount', 'input', function () { state.receiptAmount = this.value; state.receiptKey = null; });
    on('ca-receive', 'click', receiveCash);
    on('ca-limit', 'input', function () { state.limitDraft = this.value; });
    on('ca-term', 'input', function () { state.termDraft = this.value; });
    on('ca-save-limit', 'click', saveLimit);
    document.querySelectorAll('.sp-customer').forEach(function (button) {
      button.addEventListener('click', function () { openCustomer(button.getAttribute('data-customer-id')); });
    });
  }

  function render() {
    app.setAttribute('aria-busy', state.busy ? 'true' : 'false');
    if (state.phase === 'checking') { app.innerHTML = '<div class="sp-loading">Cari hesaplar yükleniyor…</div>'; return; }
    if (state.phase === 'login') { renderLogin(); return; }
    renderReady();
  }

  bootstrap();
})();
