// V13-PUI-002: Kasa Oturumu (aç/say/kapat/mutabakat + nakit giriş-çıkış)
// standalone sayfası. cashier-app.js'in kendi konvansiyonlarıyla aynı
// desen: bundler yok, IIFE modül, credentials:'include' fetch, Türkçe
// hata metinleri.
(function () {
  'use strict';

  var TERMINAL_ID_KEY = 'alkaros.terminal-id';
  var app = document.getElementById('app');
  var money = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' });

  var state = {
    phase: 'checking',
    terminalId: null,
    displayName: null,
    session: null,
    suggestedOpeningBalance: null,
    lastCountedAmount: null,
    expectedCash: null,
    varianceExceeded: false,
    closeResult: null,
    movementDirection: 'in',
    busy: false,
    error: null,
  };

  function escapeHtml(value) {
    return String(value == null ? '' : value).replace(/[&<>"']/g, function (ch) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
    });
  }

  function formatMoney(amount) {
    return money.format(Number(amount || 0));
  }

  function savedTerminalId() {
    var existing = null;
    try {
      existing = window.localStorage.getItem(TERMINAL_ID_KEY);
    } catch (err) {
      existing = null;
    }
    if (existing) return existing;
    var generated = (window.crypto && window.crypto.randomUUID)
      ? window.crypto.randomUUID()
      : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
          var r = (Math.random() * 16) | 0;
          var v = c === 'x' ? r : (r & 0x3) | 0x8;
          return v.toString(16);
        });
    try {
      window.localStorage.setItem(TERMINAL_ID_KEY, generated);
    } catch (err) {
      // localStorage kullanılamıyor olabilir (gizli sekme); oturum
      // boyunca bellekte tutmak, çökmekten iyidir.
    }
    return generated;
  }

  function describeHttpFailure(status, body) {
    if (body && body.error && body.error.message) return body.error.message;
    if (status === 401) return 'Oturumunuz sona ermiş. Lütfen tekrar giriş yapın.';
    if (status === 403) return 'Bu işlem için yetkiniz yok.';
    if (status === 404) return 'Kayıt bulunamadı.';
    if (status === 409) return 'İşlem, kasanın güncel durumuyla çakışıyor. Sayfayı yenileyip tekrar deneyin.';
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

  function cashSessionsBase() {
    return '/api/v1/terminals/' + state.terminalId + '/cash-sessions';
  }

  function setBusy(value) {
    state.busy = value;
    render();
  }

  function setError(message) {
    state.error = message;
    render();
  }

  // ---- bootstrap -----------------------------------------------------

  function bootstrap() {
    state.terminalId = savedTerminalId();
    api('/api/v1/auth/session/current').then(function (result) {
      if (!result.ok) {
        state.phase = 'login';
        render();
        return;
      }
      // Bu sayfa kendi terminalId'sini yerel olarak üretir, ama başka bir
      // sekmede (ör. cashier-app.js) aynı çerezle zaten giriş yapılmış
      // olabilir - o durumda çerezin bağlı olduğu gerçek terminalId'yi
      // kullanmak zorunludur, yoksa RequireCashierAsync eşleşmez.
      state.terminalId = result.body.terminalId;
      state.displayName = result.body.displayName;
      loadActiveSession();
    }).catch(function () {
      state.phase = 'login';
      render();
    });
  }

  function loadActiveSession() {
    api(cashSessionsBase() + '/active').then(function (result) {
      if (result.status === 404) {
        state.session = null;
        state.phase = 'noSession';
        return api(cashSessionsBase() + '/suggested-opening-balance').then(function (suggestion) {
          state.suggestedOpeningBalance = suggestion.ok
            ? suggestion.body.suggestedOpeningBalance
            : null;
          render();
        });
      }
      if (!result.ok) {
        state.phase = 'login';
        render();
        return;
      }
      state.session = result.body;
      state.phase = state.session.status === 'Counting' ? 'counting' : 'open';
      render();
    }).catch(function () {
      setError('Kasa durumu okunamadı. Bağlantınızı kontrol edin.');
    });
  }

  // ---- actions ---------------------------------------------------------

  function submitLogin(username, password) {
    setBusy(true);
    api('/api/v1/auth/login', {
      method: 'POST',
      body: { Username: username, Password: password, TerminalId: state.terminalId },
    }).then(function (result) {
      state.busy = false;
      if (!result.ok) {
        state.error = describeHttpFailure(result.status, result.body);
        render();
        return;
      }
      state.terminalId = result.body.terminalId;
      state.displayName = result.body.displayName;
      state.error = null;
      loadActiveSession();
    }).catch(function () {
      state.busy = false;
      setError('Sunucuya ulaşılamadı.');
    });
  }

  function submitOpen(openingBalance) {
    setBusy(true);
    api(cashSessionsBase(), { method: 'POST', body: { OpeningBalance: openingBalance } })
      .then(function (result) {
        state.busy = false;
        if (result.status === 409) {
          // Aktif oturum burada BİLEREK hemen okunmaz - çakışma ekranı
          // kullanıcı "Mevcut Oturuma Git"e tıklayana kadar sahnede kalmalı.
          // Daha önce burada eşzamansız loadActiveSession() çağrısı vardı;
          // o istek kullanıcı hiçbir şeye tıklamadan tamamlanıp state.phase'i
          // sessizce 'open'a çeviriyordu - E2E testinde çakışma ekranının
          // tıklanabilir kalmadan kaybolduğu bulundu.
          state.phase = 'conflict';
          state.error = null;
          render();
          return;
        }
        if (!result.ok) {
          state.error = describeHttpFailure(result.status, result.body);
          render();
          return;
        }
        state.session = result.body;
        state.error = null;
        state.phase = 'open';
        render();
      }).catch(function () {
        state.busy = false;
        setError('Sunucuya ulaşılamadı.');
      });
  }

  function submitCashMovement(direction, amount, notes) {
    setBusy(true);
    api(cashSessionsBase() + '/' + state.session.cashSessionId + '/cash-movements', {
      method: 'POST',
      // V1-RMD-241: the endpoint now requires a key and rejects a second
      // insert under the same one — this makes a would-be duplicate (a
      // race just under the button's own busy-guard, a browser/proxy
      // resend of the same request) return the original row instead of
      // posting the movement twice.
      body: { Direction: direction === 'in' ? 'In' : 'Out', Amount: amount, Notes: notes || null, IdempotencyKey: crypto.randomUUID() },
    }).then(function (result) {
      state.busy = false;
      if (!result.ok) {
        state.error = describeHttpFailure(result.status, result.body);
        render();
        return;
      }
      state.error = null;
      state.phase = 'open';
      render();
    }).catch(function () {
      state.busy = false;
      setError('Sunucuya ulaşılamadı.');
    });
  }

  function startCounting() {
    setBusy(true);
    api(cashSessionsBase() + '/' + state.session.cashSessionId + '/start-count', { method: 'POST' })
      .then(function (result) {
        state.busy = false;
        if (!result.ok) {
          state.error = describeHttpFailure(result.status, result.body);
          render();
          return;
        }
        state.session = result.body;
        state.error = null;
        state.phase = 'counting';
        render();
      }).catch(function () {
        state.busy = false;
        setError('Sunucuya ulaşılamadı.');
      });
  }

  function submitCount(countedAmount, notes) {
    setBusy(true);
    api(cashSessionsBase() + '/' + state.session.cashSessionId + '/counts', {
      method: 'POST',
      body: { CountedAmount: countedAmount, Notes: notes || null },
    }).then(function (result) {
      if (!result.ok) {
        state.busy = false;
        state.error = describeHttpFailure(result.status, result.body);
        render();
        return;
      }
      state.lastCountedAmount = countedAmount;
      state.varianceExceeded = false;
      state.error = null;
      // CashSessionSnapshot.ExpectedCash yalnız /close'un kendi yazma
      // yolunda tazelenir - kapatmadan önce hâlâ açılıştaki eski değeri
      // taşır, bu yüzden gerçek beklenen tutar ayrı bir önizleme
      // uç noktasından okunur (bkz. GET .../expected-cash'in kendi
      // doc-comment'i).
      return api(cashSessionsBase() + '/' + state.session.cashSessionId + '/expected-cash')
        .then(function (expected) {
          state.busy = false;
          state.expectedCash = expected.ok ? expected.body.expectedCash : null;
          state.phase = 'closing';
          render();
        });
    }).catch(function () {
      state.busy = false;
      setError('Sunucuya ulaşılamadı.');
    });
  }

  function submitClose(isSupervisorOverride, overrideReason) {
    setBusy(true);
    api(cashSessionsBase() + '/' + state.session.cashSessionId + '/close', {
      method: 'POST',
      body: {
        ActualCash: state.lastCountedAmount,
        IsSupervisorOverride: !!isSupervisorOverride,
        OverrideReason: overrideReason || null,
      },
    }).then(function (result) {
      state.busy = false;
      // Fark toleransı (varianceTolerance) tamamen backend'in kendi
      // politikasıdır (CashSessionPolicy.ValidateCanCloseSession) - burada
      // sabit bir eşik kopyalanmaz ("backend akıllı, frontend aptal", aynı
      // ilke bu task'ın kendi Goal'ının açılış-önerisi kararında da var).
      // Sunucu CASH_VARIANCE_THRESHOLD_EXCEEDED ile 409 dönerse süpervizör
      // onay UI'ı ancak o zaman gösterilir.
      if (result.status === 409 && result.body && result.body.error
          && result.body.error.code === 'CASH_VARIANCE_THRESHOLD_EXCEEDED') {
        state.varianceExceeded = true;
        state.error = result.body.error.message;
        render();
        return;
      }
      if (!result.ok) {
        state.error = describeHttpFailure(result.status, result.body);
        render();
        return;
      }
      state.session = result.body.session;
      state.closeResult = result.body;
      state.error = null;
      state.phase = 'closed';
      render();
    }).catch(function () {
      state.busy = false;
      setError('Sunucuya ulaşılamadı.');
    });
  }

  // Mutabakat (V13-CSH-004'ün /reconcile uç noktası) bu ekranda BİLEREK
  // yok: Goal metni yalnız "açma, sayma, kapatma ve fark teyit akışı"nı
  // kapsıyor, Out of scope de "mutabakat kontrol paneli"ni ayrıca dışarıda
  // bırakıyor - tekil-oturum mutabakatı ayrı bir task'ın kapsamı.

  // ---- rendering ---------------------------------------------------------

  function errorAlert() {
    if (!state.error) return '';
    return (
      '<div class="cs-alert cs-alert-danger">' +
      '<span class="cs-alert-icon">!</span>' +
      '<div><div class="cs-alert-title">Hata</div>' +
      '<div class="cs-alert-body">' + escapeHtml(state.error) + '</div></div>' +
      '</div>'
    );
  }

  function renderLogin() {
    app.innerHTML =
      '<div class="cs-card">' +
      '<div><span class="cs-eyebrow">ALKAROS</span>' +
      '<h1 class="cs-title">Kasa Girişi</h1>' +
      '<p class="cs-subtitle">Kasa oturumunu yönetmek için giriş yapın.</p></div>' +
      errorAlert() +
      '<form id="login-form" class="cs-field">' +
      '<label class="cs-field-label">Kullanıcı adı</label>' +
      '<input class="cs-input" type="text" id="username" autocomplete="username" required>' +
      '<label class="cs-field-label">Şifre</label>' +
      '<input class="cs-input" type="password" id="password" autocomplete="current-password" required>' +
      '<button class="cs-btn cs-btn-primary" type="submit" ' + (state.busy ? 'disabled' : '') + '>' +
      (state.busy ? 'Giriş yapılıyor…' : 'Giriş Yap') + '</button>' +
      '</form></div>';
    var form = document.getElementById('login-form');
    form.addEventListener('submit', function (event) {
      event.preventDefault();
      var username = document.getElementById('username').value.trim();
      var password = document.getElementById('password').value;
      if (!username || !password) return;
      submitLogin(username, password);
    });
  }

  function renderNoSession() {
    var suggested = state.suggestedOpeningBalance;
    app.innerHTML =
      '<div class="cs-card">' +
      '<div><span class="cs-eyebrow">Kasa Açılışı</span>' +
      '<h1 class="cs-title">Vardiyayı Başlat</h1>' +
      '<p class="cs-subtitle">Çekmecede bulunan başlangıç tutarını girin.</p></div>' +
      errorAlert() +
      '<label class="cs-field-label">Açılış Tutarı</label>' +
      '<div class="cs-amount-field"><input type="number" step="0.01" min="0" id="opening-balance" ' +
      'value="' + (suggested != null ? Number(suggested).toFixed(2) : '0.00') + '">' +
      '<span class="cs-amount-suffix">₺</span></div>' +
      (suggested != null
        ? '<span class="cs-field-hint">Önerilen tutar bir önceki kapanıştan alındı.</span>'
        : '') +
      '<button class="cs-btn cs-btn-primary" id="open-btn" ' + (state.busy ? 'disabled' : '') + '>' +
      (state.busy ? 'Açılıyor…' : 'Kasayı Aç') + '</button>' +
      '</div>';
    document.getElementById('open-btn').addEventListener('click', function () {
      var value = Number(document.getElementById('opening-balance').value);
      if (!(value >= 0)) {
        setError('Geçerli bir tutar girin.');
        return;
      }
      submitOpen(value);
    });
  }

  function renderConflict() {
    app.innerHTML =
      '<div class="cs-card cs-center-text">' +
      '<span class="cs-icon-circle danger">!</span>' +
      '<h1 class="cs-title">Bu Terminalde Zaten Açık Bir Kasa Var</h1>' +
      '<p class="cs-subtitle">Yeni bir oturum açmadan önce mevcut oturuma devam edin.</p>' +
      errorAlert() +
      '<button class="cs-btn cs-btn-primary" id="go-active">Mevcut Oturuma Git</button>' +
      '</div>';
    document.getElementById('go-active').addEventListener('click', function () {
      state.error = null;
      loadActiveSession();
    });
  }

  function renderOpen() {
    var s = state.session;
    app.innerHTML =
      '<div class="cs-card">' +
      '<div class="cs-header">' +
      '<div><span class="cs-eyebrow">Kasa Oturumu</span>' +
      '<h1 class="cs-title">' + escapeHtml(state.displayName || '') + '</h1></div>' +
      '<span class="cs-status-pill is-open"><span class="cs-status-dot"></span>Açık</span>' +
      '</div>' +
      errorAlert() +
      '<div class="cs-grid-2">' +
      '<div class="cs-stat"><div class="cs-stat-label">Açılış Tutarı</div>' +
      '<div class="cs-stat-value">' + formatMoney(s.openingBalance) + '</div></div>' +
      '<div class="cs-stat"><div class="cs-stat-label">Açılış Saati</div>' +
      '<div class="cs-stat-value">' + new Date(s.openedAt).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }) + '</div></div>' +
      '</div>' +
      '<div class="cs-divider"></div>' +
      '<div class="cs-row">' +
      '<button class="cs-btn cs-btn-secondary" id="cash-in">Nakit Giriş</button>' +
      '<button class="cs-btn cs-btn-secondary" id="cash-out">Nakit Çıkış</button>' +
      '</div>' +
      '<button class="cs-btn cs-btn-primary" id="start-count" ' + (state.busy ? 'disabled' : '') + '>' +
      (state.busy ? 'Bekleyin…' : 'Sayıma Başla') + '</button>' +
      '</div>';
    document.getElementById('cash-in').addEventListener('click', function () {
      state.movementDirection = 'in';
      state.phase = 'movement';
      state.error = null;
      render();
    });
    document.getElementById('cash-out').addEventListener('click', function () {
      state.movementDirection = 'out';
      state.phase = 'movement';
      state.error = null;
      render();
    });
    document.getElementById('start-count').addEventListener('click', startCounting);
  }

  function renderMovement() {
    var isIn = state.movementDirection === 'in';
    app.innerHTML =
      '<div class="cs-card">' +
      '<div><span class="cs-eyebrow">Kasa Hareketi</span>' +
      '<h1 class="cs-title">' + (isIn ? 'Nakit Giriş' : 'Nakit Çıkış') + '</h1></div>' +
      '<div class="cs-tabs">' +
      '<button class="cs-tab' + (isIn ? ' is-active' : '') + '" id="tab-in" type="button">Giriş</button>' +
      '<button class="cs-tab' + (!isIn ? ' is-active' : '') + '" id="tab-out" type="button">Çıkış</button>' +
      '</div>' +
      errorAlert() +
      '<label class="cs-field-label">Tutar</label>' +
      '<div class="cs-amount-field"><input type="number" step="0.01" min="0.01" id="movement-amount" value="0.00">' +
      '<span class="cs-amount-suffix">₺</span></div>' +
      '<label class="cs-field-label">Açıklama <span style="font-weight:400;color:var(--color-text-dim)">(opsiyonel)</span></label>' +
      '<textarea class="cs-textarea" id="movement-notes" placeholder="Örn. banka için para çekildi"></textarea>' +
      '<div class="cs-row">' +
      '<button class="cs-btn cs-btn-secondary" id="movement-cancel">Vazgeç</button>' +
      '<button class="cs-btn ' + (isIn ? 'cs-btn-success' : 'cs-btn-danger') + '" id="movement-submit" ' +
      (state.busy ? 'disabled' : '') + '>' + (state.busy ? 'Kaydediliyor…' : (isIn ? 'Girişi Kaydet' : 'Çıkışı Kaydet')) + '</button>' +
      '</div></div>';
    document.getElementById('tab-in').addEventListener('click', function () {
      state.movementDirection = 'in';
      render();
    });
    document.getElementById('tab-out').addEventListener('click', function () {
      state.movementDirection = 'out';
      render();
    });
    document.getElementById('movement-cancel').addEventListener('click', function () {
      state.error = null;
      state.phase = 'open';
      render();
    });
    document.getElementById('movement-submit').addEventListener('click', function () {
      var amount = Number(document.getElementById('movement-amount').value);
      var notes = document.getElementById('movement-notes').value.trim();
      if (!(amount > 0)) {
        setError('Tutar sıfırdan büyük olmalı.');
        return;
      }
      submitCashMovement(state.movementDirection, amount, notes);
    });
  }

  function renderCounting() {
    app.innerHTML =
      '<div class="cs-card">' +
      '<div><span class="cs-eyebrow">Sayım</span>' +
      '<h1 class="cs-title">Çekmecedeki Nakdi Sayın</h1>' +
      '<p class="cs-subtitle">Elinizdeki gerçek nakit tutarını girin.</p></div>' +
      errorAlert() +
      '<label class="cs-field-label">Sayılan Tutar</label>' +
      '<div class="cs-amount-field"><input type="number" step="0.01" min="0" id="counted-amount" value="0.00">' +
      '<span class="cs-amount-suffix">₺</span></div>' +
      '<label class="cs-field-label">Not <span style="font-weight:400;color:var(--color-text-dim)">(opsiyonel)</span></label>' +
      '<textarea class="cs-textarea" id="count-notes"></textarea>' +
      '<button class="cs-btn cs-btn-primary" id="submit-count" ' + (state.busy ? 'disabled' : '') + '>' +
      (state.busy ? 'Kaydediliyor…' : 'Sayımı Kaydet') + '</button>' +
      '</div>';
    document.getElementById('submit-count').addEventListener('click', function () {
      var amount = Number(document.getElementById('counted-amount').value);
      var notes = document.getElementById('count-notes').value.trim();
      if (!(amount >= 0)) {
        setError('Geçerli bir tutar girin.');
        return;
      }
      submitCount(amount, notes);
    });
  }

  function renderClosing() {
    var expected = state.expectedCash;
    var counted = state.lastCountedAmount;
    var hasExpected = expected != null;
    var difference = hasExpected ? counted - expected : null;
    var exceeded = state.varianceExceeded;
    app.innerHTML =
      '<div class="cs-card">' +
      '<div><span class="cs-eyebrow">Kapatma</span>' +
      '<h1 class="cs-title">Fark Teyidi</h1></div>' +
      errorAlert() +
      '<div class="cs-summary-row"><span>Beklenen</span><span class="value">' +
      (hasExpected ? formatMoney(expected) : '—') + '</span></div>' +
      '<div class="cs-summary-row"><span>Sayılan</span><span class="value">' + formatMoney(counted) + '</span></div>' +
      (hasExpected
        ? '<div class="cs-summary-row"><span>Fark</span><span class="value" style="color:' +
          (exceeded ? 'var(--color-danger)' : 'var(--color-success)') + '">' + formatMoney(difference) + '</span></div>'
        : '') +
      (exceeded
        ? '<div class="cs-alert cs-alert-danger"><span class="cs-alert-icon">!</span>' +
          '<div><div class="cs-alert-title">Fark tolerans sınırını aşıyor</div>' +
          '<div class="cs-alert-body">Devam etmek için süpervizör onayı ve açıklama gerekir.</div></div></div>' +
          '<label class="cs-field-label">Süpervizör Açıklaması</label>' +
          '<textarea class="cs-textarea" id="override-reason"></textarea>'
        : '') +
      '<button class="cs-btn cs-btn-primary" id="submit-close" ' + (state.busy ? 'disabled' : '') + '>' +
      (state.busy ? 'Kapatılıyor…' : 'Kasayı Kapat') + '</button>' +
      '</div>';
    document.getElementById('submit-close').addEventListener('click', function () {
      var overrideReasonEl = document.getElementById('override-reason');
      var overrideReason = overrideReasonEl ? overrideReasonEl.value.trim() : '';
      if (exceeded && !overrideReason) {
        setError('Fark tolerans sınırını aşıyor; bir açıklama girin.');
        return;
      }
      submitClose(exceeded, overrideReason);
    });
  }

  function renderClosed() {
    var s = state.session;
    var difference = state.closeResult ? state.closeResult.difference : 0;
    app.innerHTML =
      '<div class="cs-card cs-center-text">' +
      '<span class="cs-icon-circle success">✓</span>' +
      '<div><h1 class="cs-title">Kasa Kapatıldı</h1>' +
      '<p class="cs-subtitle">Vardiya özeti kaydedildi. Bu oturum artık salt-okunur.</p></div>' +
      '<div style="text-align:left;width:100%">' +
      '<div class="cs-summary-row"><span>Açılış · Kapanış</span><span class="value">' +
      new Date(s.openedAt).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }) + ' – ' +
      (s.closedAt ? new Date(s.closedAt).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }) : '—') +
      '</span></div>' +
      '<div class="cs-divider"></div>' +
      '<div class="cs-summary-row"><span>Sayılan</span><span class="value">' + formatMoney(state.lastCountedAmount) + '</span></div>' +
      '<div class="cs-summary-row"><span>Fark</span><span class="value" style="color:' +
      (Math.abs(difference) > 0.005 ? 'var(--color-danger)' : 'var(--color-success)') + '">' +
      formatMoney(difference) + '</span></div>' +
      '</div>' +
      '<button class="cs-btn cs-btn-primary" id="reopen" style="width:100%">Yeni Vardiya Aç</button>' +
      '</div>';
    document.getElementById('reopen').addEventListener('click', function () {
      state.session = null;
      state.closeResult = null;
      state.lastCountedAmount = null;
      state.expectedCash = null;
      state.varianceExceeded = false;
      state.error = null;
      loadActiveSession();
    });
  }

  function render() {
    if (state.phase === 'checking') {
      app.innerHTML = '<div class="cs-loading">Oturum kontrol ediliyor…</div>';
      return;
    }
    app.setAttribute('aria-busy', state.busy ? 'true' : 'false');
    switch (state.phase) {
      case 'login': return renderLogin();
      case 'noSession': return renderNoSession();
      case 'conflict': return renderConflict();
      case 'open': return renderOpen();
      case 'movement': return renderMovement();
      case 'counting': return renderCounting();
      case 'closing': return renderClosing();
      case 'closed': return renderClosed();
      default: return renderOpen();
    }
  }

  bootstrap();
})();
