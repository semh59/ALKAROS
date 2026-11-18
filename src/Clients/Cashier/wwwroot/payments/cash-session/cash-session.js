// V13-PUI-002: Kasa Oturumu (aç/say/kapat/mutabakat + nakit giriş-çıkış)
// standalone sayfası. cashier-app.js'in kendi konvansiyonlarıyla aynı
// desen: bundler yok, IIFE modül, credentials:'include' fetch, Türkçe
// hata metinleri.
(function () {
  'use strict';

  var TERMINAL_ID_KEY = 'alkaros.terminal-id';
  var app = document.getElementById('app');
  // The payment page sends the cashier here with the page to come back to; only a same-origin relative path is followed
  // (one leading slash, no "//", backslash, space or control character), anything else would be an open redirect.
  var returnTo = (function () {
    var value = new URLSearchParams(window.location.search).get('returnTo');
    return value && /^\/(?![\/\\])[^\\\s\x00-\x1f]*$/.test(value) ? value : null;
  })();
  if (returnTo) {
    var backLink = document.querySelector('.cs-back');
    backLink.setAttribute('href', returnTo);
    backLink.textContent = /^\/tables(?:[?#]|$)/.test(returnTo) ? '← Masalara dön' : /^\/(?:[?#]|$)/.test(returnTo) ? '← Satış ekranına dön' : '← Hesaba dön';
  }
  // The sale screen's "Satış yok" link lands on the reason form here; the form itself stays on this page, where the
  // server's reason and audit rules apply. It opens once, as soon as the drawer status says it can be used.
  var openNoSaleForm = new URLSearchParams(window.location.search).get('open') === 'no-sale';
  var money = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' });

  var state = {
    phase: 'checking',
    terminalId: null,
    displayName: null,
    session: null,
    // The name of the cashier who opened a session that is not the signed-in user's; null until read.
    ownerName: null,
    suggestedOpeningBalance: null,
    lastCountedAmount: null,
    expectedCash: null,
    // What the server reports next to the expected cash: the tolerance and how much business is still open.
    tolerance: null,
    openBills: 0,
    unsettledPayments: 0,
    varianceExceeded: false,
    startCountConfirm: false,
    closeResult: null,
    movementDirection: 'in',
    movementStep: 'form',
    summaryExpected: null,
    // Typed-but-unsent values live in state, not only in the DOM: every re-render (busy, error,
    // notice) rebuilds the screen, and the fields must come back exactly as the cashier left them.
    drafts: { amount: '', notes: '', counted: '', countNotes: '', explanation: '', managerUsername: '', acknowledged: false, noSaleNote: '' },
    zeroCountPrompt: false,
    pendingDecision: null,
    approveHere: { id: null, name: '', error: null },
    userId: null,
    capabilities: [],
    movements: null,
    movementsError: false,
    // The physical drawer: what the server says about it (null = no button), and the no-sale form in progress.
    drawer: null,
    noSaleReason: null,
    noSaleKey: null,
    noSaleSignature: null,
    notice: null,
    busy: false,
    error: null,
    // V1-RMD-314 (independent 2026-09-26 audit, finding K3): the cash-movement idempotency key for the
    // CURRENT attempt - generated once (lazily, in submitCashMovement) and kept across a retry (a network
    // failure where the request may have actually landed server-side), same fix/reasoning as
    // split-payment.js's tenderIdempotencyKey. Cleared on any real server response (success or a
    // definitive rejection); only a network failure keeps it for a retry. V1-RMD-756: bound to the body it
    // was minted for - an edited movement after a network failure gets a new key, never the old record.
    cashMovementIdempotencyKey: null,
    cashMovementRequestSignature: null,
    // V1-RMD-377 (module-by-module UI audit round 2, P1 - rakip karşılaştırması): counts of each
    // banknote/coin while the denomination-based counter is in use; countMode toggles between it
    // and the original single free-typed field (kept for the real edge case a kupür sayımı cannot
    // cover - foreign currency, a torn/taped note, etc.).
    countMode: 'denomination',
    denominationCounts: {},
  };

  // V1-RMD-377: current TRY banknotes/coins actually found in a till - not every coin ISO 4217
  // still mints (1/5 kuruş are not realistically handled at a register), same "practical, not
  // exhaustive" judgement call as this file's own OCCUPANCY_WARNING_MINUTES-style constants
  // elsewhere in this codebase.
  var DENOMINATIONS = [200, 100, 50, 20, 10, 5, 1, 0.5];

  function escapeHtml(value) {
    return String(value == null ? '' : value).replace(/[&<>"']/g, function (ch) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
    });
  }

  function formatMoney(amount) {
    return money.format(Number(amount || 0));
  }

  function newUuid() {
    return (window.crypto && window.crypto.randomUUID)
      ? window.crypto.randomUUID()
      : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
          var r = (Math.random() * 16) | 0;
          var v = c === 'x' ? r : (r & 0x3) | 0x8;
          return v.toString(16);
        });
  }

  function savedTerminalId() {
    var existing = null;
    try {
      existing = window.localStorage.getItem(TERMINAL_ID_KEY);
    } catch (err) {
      existing = null;
    }
    if (existing) return existing;
    var generated = newUuid();
    try {
      window.localStorage.setItem(TERMINAL_ID_KEY, generated);
    } catch (err) {
      // localStorage kullanılamıyor olabilir (gizli sekme); oturum
      // boyunca bellekte tutmak, çökmekten iyidir.
    }
    return generated;
  }

  // The server's own message is never shown: every code the drawer screen can meet has its Turkish text here.
  var NOT_OWNED_MOVEMENT_TEXT = 'Nakit giriş ve çıkış yalnız kendi kasa oturumunuza yazılır; sahibi kapatınca kendi kasa oturumunuzu açın.';
  var ERROR_TEXT = {
    CASH_OUT_EXCEEDS_DRAWER: 'Çıkış tutarı kasadaki nakdi aşıyor.',
    CASH_MOVEMENT_REASON_INVALID: 'Açıklama zorunludur ve en çok 500 karakter olabilir.',
    CASH_MOVEMENT_REQUESTS_PENDING: 'Onay bekleyen nakit çıkış talebi var; önce karara bağlayın veya geri çekin.',
    CASH_MOVEMENT_SELF_APPROVAL: 'Kendi çıkış talebinizi onaylayamazsınız; başka bir yönetici onaylamalı.',
    CASH_MOVEMENT_REQUEST_NOT_OWN_SESSION: 'Bu çıkış talebi, talep sahibinin kendi kasa oturumunda açılmamış; onaylanamaz. Reddedin; talep sahibi kendi oturumundan yeniden istesin.',
    CASH_MOVEMENT_REQUEST_STATE: 'Bu çıkış talebi artık beklemiyor; liste yenilendi.',
    CASH_SESSION_NOT_OWNED: NOT_OWNED_MOVEMENT_TEXT,
    CASH_DRAWER_DISABLED: 'Para çekmecesi bağlantısı kapalı; yönetici Ayarlar ekranından açabilir.',
    CASH_DRAWER_REASON_INVALID: 'Bir gerekçe seçin; “Diğer” için not yazın (en çok 200 karakter).',
    CASH_DRAWER_SESSION_NOT_OPEN: 'Kasa oturumu kapanmış; satışsız açma yalnız açık veya sayımdaki kasada yapılır.',
    CASH_MOVEMENT_REQUEST_NOT_FOUND: 'Çıkış talebi bulunamadı.',
    IDEMPOTENCY_KEY_REUSED: 'Bu işlem farklı bir nakit hareketi için zaten kaydedilmiş. Sayfayı yenileyip tekrar deneyin.',
    CLOSED_CASH_SESSION: 'Kasa oturumu açık değil.',
    ACTIVE_CASH_SESSION_EXISTS: 'Bu terminalde zaten açık bir kasa oturumu var.',
    INVALID_CASH_SESSION_STATE: 'Kasa oturumu bu işlem için uygun durumda değil.',
    CASH_SESSION_NOT_FOUND: 'Kasa oturumu bulunamadı.',
    CASH_VARIANCE_THRESHOLD_EXCEEDED: 'Fark tolerans sınırını aşıyor; yönetici onayı gerekiyor.',
    MANAGER_CREDENTIAL_INVALID: 'Yönetici adı veya şifre hatalı.',
    MANAGER_LOCKED: 'Bu yönetici hesabı geçici olarak kilitlendi; en geç 15 dakika sonra açılır. Yönetici kendi ekranından onaylayabilir.',
    TOO_MANY_ATTEMPTS: 'Çok fazla hatalı deneme yapıldı. En geç 15 dakika sonra tekrar deneyin ya da yönetici kendi ekranından onaylasın.',
    SELF_APPROVAL_NOT_ALLOWED: 'Kendi talebinizi onaylayamazsınız; yöneticinin kendi hesabıyla girmesi gerekir.',
    APPROVER_LACKS_PERMISSION: 'Bu yönetici bu işlemi onaylama yetkisine sahip değil.',
    CASH_SESSION_OWNER_REQUIRED: 'Bu kasa oturumunu yalnız oturumu açan kasiyer veya bir yönetici sayabilir ve kapatabilir.',
    CASH_COUNT_REQUIRED: 'Kasa sayımı kaydedilmeden kasa kapatılamaz; önce parayı sayıp kaydedin.',
    CASH_COUNT_MISMATCH: 'Kapanışta girilen tutar kayıtlı son sayımla aynı olmalı; gerekirse yeniden sayın.',
    CASH_DIFFERENCE_EXPLANATION_REQUIRED: 'Kasada fark var; 1 ile 500 karakter arasında bir açıklama yazın.',
    CASH_CLOSE_UNSETTLED_NOT_ACKNOWLEDGED: 'İşletmede ödenmemiş hesap veya sonucu belirsiz ödeme var; bunları bilerek kapatmak için onay kutusunu işaretleyin.',
    CASH_RECONCILE_SELF_NOT_ALLOWED: 'Oturumu açan veya kapatan kişi kendi oturumunu mutabık edemez; başka bir yönetici onaylamalı.',
    INVALID_CREDENTIALS: 'Kullanıcı adı veya şifre hatalı.',
    TERMINAL_KEY_MISMATCH: 'Bu terminal başka bir tarayıcıya bağlı. Kasa terminalinin kendi tarayıcısından giriş yapın ya da bir yönetici Güvenlik ekranından terminali sıfırlasın.',
    FORBIDDEN: 'Bu işlem için yetkiniz yok.',
    VALIDATION_FAILED: 'Girilen bilgiler geçersiz. Alanları kontrol edin.',
  };

  function describeHttpFailure(status, body) {
    var code = body && body.error && body.error.code;
    if (code && ERROR_TEXT[code]) return ERROR_TEXT[code];
    if (status === 401) return 'Oturumunuz sona ermiş. Lütfen tekrar giriş yapın.';
    if (status === 403) return 'Bu işlem için yetkiniz yok.';
    if (status === 404) return 'Kayıt bulunamadı.';
    if (status === 409) return 'İşlem, kasanın güncel durumuyla çakışıyor. Sayfayı yenileyip tekrar deneyin.';
    if (status === 429) return 'Çok fazla istek gönderildi. Lütfen birkaç saniye bekleyin.';
    if (status >= 500) return 'Sunucu tarafında bir sorun oluştu. Lütfen tekrar deneyin.';
    return 'İstek tamamlanamadı.';
  }

  var TIME_FORMAT = { hour: '2-digit', minute: '2-digit', timeZone: 'Europe/Istanbul' };

  // AbortSignal.timeout is missing in older browsers; without a signal a hung connection would never end.
  function timeoutSignal(ms) {
    if (typeof AbortSignal !== 'undefined' && AbortSignal.timeout) return AbortSignal.timeout(ms);
    if (typeof AbortController === 'undefined') return undefined;
    var controller = new AbortController();
    setTimeout(function () { controller.abort(); }, ms);
    return controller.signal;
  }

  function api(path, options) {
    options = options || {};
    return fetch(path, {
      method: options.method || 'GET',
      credentials: 'include',
      headers: options.body ? { 'Content-Type': 'application/json' } : undefined,
      body: options.body ? JSON.stringify(options.body) : undefined,
      signal: timeoutSignal(15000),
    }).then(function (response) {
      // V1-RMD-291: one place catches a dropped session, from ANY call, not
      // just the bootstrap check - before this a 401 from a mid-use action
      // only added an inline message to the already-stale screen; nothing
      // sent the cashier back to the real login form this page already has.
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

  function cashSessionsBase() {
    return '/api/v1/terminals/' + state.terminalId + '/cash-sessions';
  }

  function setBusy(value) {
    state.busy = value;
    render();
  }

  // Nothing used to move KEYBOARD focus to the new screen's own primary field -
  // opening the cash-drawer count, say, left focus wherever the PREVIOUS screen's now-gone button
  // used to be. On a touchscreen kiosk with no physical keyboard, this also means the numeric pad
  // never pops up on its own; the cashier must tap the amount field manually first, every time.
  // Skipped only for a busy-state re-render of the SAME screen (setBusy toggles state.busy and
  // re-renders the current phase as-is) - stealing focus back from a field the cashier is still
  // typing into, just because the button they have not clicked yet got disabled, would be worse
  // than doing nothing.
  function focusPrimaryField() {
    // The no-sale form starts on its reasons: a note field first would raise the on-screen keyboard over them.
    var target = app.querySelector('[data-no-sale-reason]') || app.querySelector('input:not(:disabled), textarea:not(:disabled)');
    if (!target) target = app.querySelector('#cash-in:not(:disabled)');
    if (!target) target = app.querySelector('button:not(:disabled)');
    if (target) target.focus({ preventScroll: true });
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
        if (result.status === 401 || result.status === 403) {
          state.phase = 'login';
        } else {
          state.error = describeHttpFailure(result.status, result.body);
          state.phase = 'loadError';
        }
        render();
        return;
      }
      // Bu sayfa kendi terminalId'sini yerel olarak üretir, ama başka bir
      // sekmede (ör. cashier-app.js) aynı çerezle zaten giriş yapılmış
      // olabilir - o durumda çerezin bağlı olduğu gerçek terminalId'yi
      // kullanmak zorunludur, yoksa RequireCashierAsync eşleşmez.
      state.terminalId = result.body.terminalId;
      state.displayName = result.body.displayName;
      state.userId = result.body.userId;
      state.capabilities = result.body.capabilities || [];
      loadActiveSession();
    }).catch(function () {
      state.error = 'Bağlantı kesildi. Oturum durumu okunamadı; bağlantınızı kontrol edip yeniden deneyin.';
      state.phase = 'loadError';
      render();
    });
  }

  function loadActiveSession() {
    api(cashSessionsBase() + '/active').then(function (result) {
      if (result.status === 404) {
        state.session = null;
        state.phase = 'noSession';
        applyOpenNoSaleForm();
        return api(cashSessionsBase() + '/suggested-opening-balance').then(function (suggestion) {
          state.suggestedOpeningBalance = suggestion.ok
            ? suggestion.body.suggestedOpeningBalance
            : null;
          render();
        });
      }
      if (!result.ok) {
        if (result.status === 401) {
          state.phase = 'login';
          render();
        } else {
          failActiveSessionRead(describeHttpFailure(result.status, result.body));
        }
        return;
      }
      state.session = result.body;
      var counting = state.session.status === 'Counting';
      if (!counting) state.phase = 'open';
      return (ownsSession() ? Promise.resolve() : loadOwnerName()).then(function () {
        return counting ? resumeCounting() : Promise.all([loadMovements(), loadDrawer()]);
      });
    }).catch(function () {
      failActiveSessionRead('Bağlantı kesildi. Kasa durumu okunamadı; bağlantınızı kontrol edip yeniden deneyin.');
    });
  }

  // A reload while counting must not forget the count that was recorded: a blank form would ask for it again and the
  // server would record the second entry as a correction. The expected cash is kept only with a recorded count, so the
  // count screen of an uncounted session stays blind.
  function resumeCounting() {
    if (!mayActForOwner()) return showCounting();
    return api(movementsBase() + '/expected-cash').then(function (expected) {
      if (!expected.ok) {
        failActiveSessionRead('Kayıtlı sayım okunamadı. Bağlantınızı kontrol edip Tekrar dene düğmesine basın; sürerse yöneticiye haber verin.');
        return;
      }
      if (expected.body.lastCountedAmount == null) return showCounting();
      state.lastCountedAmount = expected.body.lastCountedAmount;
      applyExpected(expected);
      // The no-sale link is answered on the count screen it landed on, as before.
      state.phase = 'counting';
      applyOpenNoSaleForm();
      state.phase = 'closing';
      state.notice = state.notice || 'Kayıtlı sayımınızla devam ediyorsunuz. Yeniden saymak için Sayımı düzelt düğmesini kullanın; her yeni sayım düzeltme olarak kaydedilir.';
      render();
    });
  }

  function showCounting() {
    state.phase = 'counting';
    applyOpenNoSaleForm();
    render();
  }

  // On first load there is no screen to keep, so the failure gets its own screen with a retry; a re-read
  // after an action keeps the screen the cashier is on and shows the failure on it.
  function failActiveSessionRead(message) {
    if (state.phase !== 'checking') {
      setError(message);
      return;
    }
    state.error = message;
    state.phase = 'loadError';
    render();
  }

  function movementsBase() {
    return cashSessionsBase() + '/' + state.session.cashSessionId;
  }

  // The list never blocks the screen: a failed read leaves the open-session actions usable and says so.
  function loadMovements() {
    return api(movementsBase() + '/cash-movements').then(function (result) {
      state.movements = result.ok ? result.body : null;
      state.movementsError = !result.ok;
      render();
    }).catch(function () {
      state.movements = null;
      state.movementsError = true;
      render();
    });
  }

  // The "no sale" button is optional: any failure to read the drawer status simply leaves it out.
  function loadDrawer() {
    return api(movementsBase() + '/cash-drawer').then(function (result) {
      state.drawer = result.ok && result.body ? result.body : null;
      applyOpenNoSaleForm();
      render();
    }).catch(function () {
      state.drawer = null;
      if (applyOpenNoSaleForm()) render();
    });
  }

  // The form is asked for once: the parameter leaves the address so a reload does not open it again, and whatever stops the
  // form from opening is said in words, because the cashier tapped a link and expects it.
  function applyOpenNoSaleForm() {
    if (!openNoSaleForm) return false;
    openNoSaleForm = false;
    var address = new URL(window.location.href);
    address.searchParams.delete('open');
    window.history.replaceState(null, '', address.pathname + address.search + address.hash);
    if (canOpenDrawerWithoutSale()) {
      state.noSaleKey = null;
      state.phase = 'nosale';
    } else {
      state.error = noSaleUnavailableText();
    }
    return true;
  }

  function noSaleUnavailableText() {
    if (!state.session) return 'Satışsız çekmece açmak için önce kasa oturumu açın.';
    if (state.phase === 'counting') return 'Kasa sayımı sürerken bu ekrandan satışsız çekmece açılamaz.';
    if (!state.drawer) return 'Çekmece durumu okunamadı. Sayfayı yenileyip yeniden deneyin.';
    if (!state.drawer.enabled) return 'Bu kasada satışsız çekmece açma kapalı.';
    if (!state.drawer.canKick) return 'Satışsız çekmece açma yetkiniz yok.';
    return 'Kasa oturumu başka kullanıcıda; satışsız çekmeceyi yalnız oturum sahibi açabilir.';
  }

  function canOpenDrawerWithoutSale() {
    return !!(state.drawer && state.drawer.enabled && state.drawer.canKick && ownsSession());
  }

  function submitNoSale() {
    captureDrafts();
    // A line break typed into the note is a space: the server refuses control characters.
    var note = (state.drafts.noSaleNote || '').replace(/\s+/g, ' ').trim();
    if (!state.noSaleReason) return setError('Bir gerekçe seçin.');
    if (state.noSaleReason === 'Other' && !note) return setError('“Diğer” için not yazın.');
    if (note.length > 200) return setError('Not en çok 200 karakter olabilir.');
    var body = { ReasonCode: state.noSaleReason, Note: note || null };
    // A retry of the same request keeps its key (the server then answers with the opening it already made); an edited one gets a new key.
    var signature = JSON.stringify(body);
    if (!state.noSaleKey || state.noSaleSignature !== signature) {
      state.noSaleKey = newUuid();
      state.noSaleSignature = signature;
    }
    body.IdempotencyKey = state.noSaleKey;
    state.error = null;
    setBusy(true);
    api(movementsBase() + '/cash-drawer/no-sale', { method: 'POST', body: body }).then(function (result) {
      state.busy = false;
      if (result.status < 500) state.noSaleKey = null;
      if (!result.ok) {
        var errorCode = result.body && result.body.error && result.body.error.code;
        if (errorCode === 'CASH_DRAWER_DISABLED' || errorCode === 'CASH_DRAWER_SESSION_NOT_OPEN') {
          state.drawer = null;
          state.phase = 'open';
        }
        state.error = describeHttpFailure(result.status, result.body);
        render();
        return;
      }
      var answer = result.body || {};
      if (state.drawer) state.drawer.noSaleCount = answer.noSaleCount;
      var warning = answer.warning ? ' ' + answer.warning : '';
      state.noSaleReason = null;
      state.drafts.noSaleNote = '';
      state.phase = 'open';
      if (answer.delivered) {
        state.error = null;
        state.notice = 'Çekmece açıldı.' + warning;
      } else {
        state.notice = null;
        state.error = 'Çekmece açılamadı; yazıcı bağlantısını kontrol edin.' + warning;
      }
      render();
    }).catch(function () {
      state.busy = false;
      setError('Bağlantı kesildi. Çekmece açılmış olabilir; kontrol edin, açılmadıysa yeniden deneyin.');
    });
  }

  // A new shift must not inherit the previous one's list, requests, drafts or count.
  function resetForNewSession() {
    state.startCountConfirm = false;
    state.movements = null;
    state.movementsError = false;
    state.notice = null;
    state.drafts = { amount: '', notes: '', counted: '', countNotes: '', explanation: '', managerUsername: '', acknowledged: false, noSaleNote: '' };
    state.drawer = null;
    state.noSaleReason = null;
    state.noSaleKey = null;
    state.denominationCounts = {};
    state.zeroCountPrompt = false;
    state.pendingDecision = null;
    state.movementStep = 'form';
    state.summaryExpected = null;
  }

  function canApprove() {
    return state.capabilities.indexOf('cash.session.override') >= 0;
  }

  function ownsSession() {
    var s = state.session;
    return !(s && s.cashierUserId && s.cashierUserId !== state.userId);
  }

  // The owner or a manager counts and closes the drawer; anyone else is told whose it is and who may.
  function mayActForOwner() {
    return ownsSession() || canApprove();
  }

  function sessionOwnerName() {
    return ownsSession() ? state.displayName : (state.ownerName || 'Başka bir kullanıcı');
  }

  function loadOwnerName() {
    return api(cashSessionsBase() + '/active/owner').then(function (result) {
      state.ownerName = result.ok ? result.body.displayName : null;
    }).catch(function () {
      state.ownerName = null;
    });
  }

  function ownerNotice() {
    if (ownsSession()) return '';
    var rule = canApprove()
      ? 'Yönetici olarak sizin de sayıp kapatma yetkiniz var.'
      : 'Sayma ve kapatma için oturumu açan kasiyer ya da bir yönetici gerekir; kendi oturumunuzu o kapanınca açabilirsiniz.';
    return '<div class="cs-alert cs-alert-warning" id="owner-notice"><span class="cs-alert-icon">!</span>' +
      '<div class="cs-alert-body">Bu kasa oturumu ' + escapeHtml(sessionOwnerName()) + ' adına açık. ' + rule + '</div></div>';
  }

  // The reason sits next to the button it switches off (docs/CASHIER_UI_RULES.md 3).
  function blockedCountHint() {
    return '<p class="cs-field-hint" id="owner-hint">Bu düğme sizde pasif: sayımı ' + escapeHtml(sessionOwnerName()) + ' ya da bir yönetici yapabilir.</p>';
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
      state.error = null;
      bootstrap();
    }).catch(function () {
      state.busy = false;
      setError('Bağlantı kesildi. Giriş yapılamadı; bağlantınızı kontrol edip tekrar deneyin.');
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
        if (returnTo) {
          window.location.assign(returnTo);
          return;
        }
        state.session = result.body;
        state.error = null;
        state.phase = 'open';
        return loadMovements();
      }).catch(function () {
        state.busy = false;
        setError('Bağlantı kesildi. Kasa açılmış olabilir; sayfayı yenileyip kasa durumunu kontrol edin, açıksa o oturumla devam edin.');
      });
  }

  var DECIDED_NOTICE = {
    Approved: 'Çıkış onaylandı ve kasadan düşüldü.',
    Rejected: 'Bu çıkış talebi daha önce reddedilmişti.',
    Cancelled: 'Bu çıkış talebi daha önce geri çekilmişti.',
  };

  function submitCashMovement(direction, amount, notes) {
    setBusy(true);
    // The server compares direction, amount and the trimmed reason on a replay, so the key is bound to all three:
    // a retry of the same body keeps it, an edited body gets a new one.
    var movementBody = { Direction: direction === 'in' ? 'In' : 'Out', Amount: amount, Notes: notes };
    var movementSignature = JSON.stringify({ Direction: movementBody.Direction, Amount: movementBody.Amount, Notes: notes });
    if (!state.cashMovementIdempotencyKey || state.cashMovementRequestSignature !== movementSignature) {
      state.cashMovementIdempotencyKey = newUuid();
      state.cashMovementRequestSignature = movementSignature;
    }
    movementBody.IdempotencyKey = state.cashMovementIdempotencyKey;
    api(movementsBase() + '/cash-movements', { method: 'POST', body: movementBody }).then(function (result) {
      state.busy = false;
      // Any real server response (success or a definitive rejection) means this key has already been
      // consumed/decided - only a network failure (the catch below) keeps it for a retry.
      if (result.status < 500) state.cashMovementIdempotencyKey = null;
      if (!result.ok) {
        state.error = describeHttpFailure(result.status, result.body);
        state.movementStep = 'form';
        render();
        return;
      }
      state.error = null;
      var decided = result.body && result.body.status;
      var saved = (direction === 'in' ? 'Giriş kaydedildi' : 'Çıkış kaydedildi') + ' (' + formatMoney(amount) + ').';
      state.notice = result.status === 202
        ? 'Çıkış yönetici onayı bekliyor. Onaylanana kadar kasadan düşmez.'
        : (DECIDED_NOTICE[decided] || saved);
      state.drafts.amount = '';
      state.drafts.notes = '';
      state.movementStep = 'form';
      state.phase = 'open';
      return loadMovements();
    }).catch(function () {
      state.busy = false;
      setError('Bağlantı kesildi. Kayıt gitmiş olabilir; nakit hareketlerini kontrol edin, yoksa aynı işlemi tekrar deneyin.');
    });
  }

  function openMovementSummary() {
    setBusy(true);
    Promise.all([
      api(movementsBase() + '/expected-cash'),
      api(movementsBase() + '/cash-movements'),
    ]).then(function (results) {
      state.busy = false;
      state.summaryExpected = results[0].ok ? results[0].body.expectedCash : null;
      if (results[1].ok) state.movements = results[1].body;
      state.movementStep = 'summary';
      state.error = null;
      render();
    }).catch(function () {
      state.busy = false;
      setError('Bağlantı kesildi. Hareket henüz kaydedilmedi; bağlantınızı kontrol edip tekrar deneyin.');
    });
  }

  function decideRequest(requestId, action, doneText) {
    setBusy(true);
    api(movementsBase() + '/cash-movement-requests/' + requestId + '/' + action, { method: 'POST', body: {} })
      .then(function (result) {
        state.busy = false;
        state.error = result.ok ? null : describeHttpFailure(result.status, result.body);
        state.notice = result.ok ? doneText : null;
        return loadMovements();
      }).catch(function () {
        state.busy = false;
        setError('Bağlantı kesildi. Karar gitmiş olabilir; nakit hareketlerini kontrol edin.');
      });
  }

  // A manager at the till approves with their own sign-in; the password is read from the field when pressed and never stored.
  function approveHere(requestId) {
    var name = document.getElementById('approver-name-' + requestId).value.trim();
    var secret = document.getElementById('approver-secret-' + requestId).value;
    if (!name || !secret) {
      state.approveHere = { id: requestId, name: name, error: 'Yönetici adı ve şifresini girin.' };
      render();
      return;
    }
    setBusy(true);
    api(movementsBase() + '/cash-movement-requests/' + requestId + '/approve', {
      method: 'POST', body: { ManagerUsername: name, ManagerPassword: secret },
    }).then(function (result) {
      state.busy = false;
      state.approveHere = result.ok
        ? { id: null, name: '', error: null }
        : { id: requestId, name: name, error: describeHttpFailure(result.status, result.body) };
      state.error = null;
      state.notice = result.ok ? DECISION_NOTICE.approve : null;
      return loadMovements();
    }).catch(function () {
      state.busy = false;
      state.approveHere = { id: requestId, name: name, error: 'Bağlantı kesildi. Onay gitmiş olabilir; nakit hareketlerini kontrol edin.' };
      render();
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
        state.notice = null;
        state.phase = 'counting';
        render();
      }).catch(function () {
        state.busy = false;
        setError('Bağlantı kesildi. Sayım başlamış olabilir; sayfayı yenileyip kasa durumunu kontrol edin.');
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
      state.zeroCountPrompt = false;
      state.notice = null;
      state.error = null;
      // CashSessionSnapshot.ExpectedCash yalnız /close'un kendi yazma
      // yolunda tazelenir - kapatmadan önce hâlâ açılıştaki eski değeri
      // taşır, bu yüzden gerçek beklenen tutar ayrı bir önizleme
      // uç noktasından okunur (bkz. GET .../expected-cash'in kendi
      // doc-comment'i).
      return api(cashSessionsBase() + '/' + state.session.cashSessionId + '/expected-cash')
        .catch(function () { return { ok: false }; })
        .then(function (expected) {
          state.busy = false;
          applyExpected(expected);
          state.phase = 'closing';
          render();
        });
    }).catch(function () {
      state.busy = false;
      setError('Bağlantı kesildi. Sayımınız kaydedilmemiş olabilir; sayımı tekrar gönderin (en son sayım geçerlidir).');
    });
  }

  function applyExpected(expected) {
    var body = expected.ok ? expected.body : null;
    state.expectedCash = body ? body.expectedCash : null;
    state.tolerance = body && body.varianceTolerance != null ? body.varianceTolerance : null;
    state.openBills = body && body.openBillCount ? body.openBillCount : 0;
    state.unsettledPayments = body && body.unsettledPaymentCount ? body.unsettledPaymentCount : 0;
  }

  function submitClose(isSupervisorOverride, explanation, manager) {
    setBusy(true);
    api(cashSessionsBase() + '/' + state.session.cashSessionId + '/close', {
      method: 'POST',
      body: {
        ActualCash: state.lastCountedAmount,
        IsSupervisorOverride: !!isSupervisorOverride,
        // One typed text serves both: the manager's reason and the explanation of the difference.
        OverrideReason: isSupervisorOverride ? explanation || null : null,
        DifferenceExplanation: explanation || null,
        AcknowledgeUnsettled: !!state.drafts.acknowledged,
        ManagerUsername: manager ? manager.username : undefined,
        ManagerPassword: manager ? manager.password : undefined,
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
        // The manager block below is the one warning; an error box with the same text would say it twice.
        state.varianceExceeded = true;
        state.error = null;
        render();
        return;
      }
      if (!result.ok) {
        state.error = describeHttpFailure(result.status, result.body);
        if (result.body && result.body.error && result.body.error.code === 'CASH_CLOSE_UNSETTLED_NOT_ACKNOWLEDGED') {
          // Something opened after the counts were read: read them again so the warning and the box appear.
          api(movementsBase() + '/expected-cash').then(function (expected) {
            applyExpected(expected);
            render();
          });
        }
        render();
        return;
      }
      state.session = result.body.session;
      state.closeResult = result.body;
      state.error = null;
      state.phase = 'closed';
      // V1-RMD-343 (independent 2026-09-26 audit, orta seviye bulgu): a
      // parked (beklet) cart is per-terminal and per-shift by intent (a
      // ticket set aside mid-service), but cashier-app.js's own
      // alkaros_cashier_parked key had no vardiya boundary at all - a
      // cart parked before this close stayed recallable by whoever opens
      // the NEXT shift on this same terminal. Closing the shift is exactly
      // that boundary.
      try {
        window.localStorage.removeItem('alkaros_cashier_parked');
        // V1-RMD-360 (module-by-module UI audit, 2026-09-27): the active (unparked) ticket
        // gained its own persistence the same day - same vardiya boundary applies to it as to
        // a parked one, for the same reason V1-RMD-343 already gave above.
        window.localStorage.removeItem('alkaros_cashier_active_ticket');
        window.localStorage.removeItem('alkaros_cashier_pending_dispatch');
      } catch (err) {
        // localStorage kullanılamıyor olabilir (gizli sekme); vardiya
        // kapanışını bu yüzden engellemek anlamsız.
      }
      render();
    }).catch(function () {
      // The close may have landed before the connection dropped: say so and read the real state again.
      state.busy = false;
      state.error = null;
      state.notice = 'Bağlantı kesildi. Kasa kapanmış olabilir; güncel durum yeniden okundu.';
      loadActiveSession();
    });
  }

  function rereadExpectedCash() {
    setBusy(true);
    api(movementsBase() + '/expected-cash').then(function (expected) {
      state.busy = false;
      applyExpected(expected);
      state.error = expected.ok ? null : 'Beklenen tutar okunamadı. Bağlantınızı kontrol edip tekrar deneyin.';
      render();
    }).catch(function () {
      state.busy = false;
      setError('Bağlantı kesildi. Beklenen tutar okunamadı; bağlantınızı kontrol edip tekrar deneyin.');
    });
  }

  // Mutabakat (V13-CSH-004'ün /reconcile uç noktası) bu ekranda BİLEREK
  // yok: Goal metni yalnız "açma, sayma, kapatma ve fark teyit akışı"nı
  // kapsıyor, Out of scope de "mutabakat kontrol paneli"ni ayrıca dışarıda
  // bırakıyor - tekil-oturum mutabakatı ayrı bir task'ın kapsamı.

  // ---- rendering ---------------------------------------------------------

  // Only the alert and the notice are live regions (role="alert" / role="status"); the whole page is not, so a
  // screen reader announces the message and not every re-render of the screen.
  function errorAlert() {
    if (!state.error) return '';
    return (
      '<div class="cs-alert cs-alert-danger" role="alert" tabindex="-1">' +
      '<span class="cs-alert-icon">!</span>' +
      '<div><div class="cs-alert-title">Hata</div>' +
      '<div class="cs-alert-body">' + escapeHtml(state.error) + '</div></div>' +
      '</div>'
    );
  }

  function noticeAlert() {
    if (!state.notice) return '';
    return '<div class="cs-alert cs-alert-success" role="status"><span class="cs-alert-icon">✓</span>' +
      '<div class="cs-alert-body" id="cs-notice">' + escapeHtml(state.notice) + '</div></div>';
  }

  function renderLogin() {
    app.innerHTML =
      '<div class="cs-card">' +
      '<div><span class="cs-eyebrow">ALKAROS</span>' +
      '<h1 class="cs-title">Kasa Girişi</h1>' +
      '<p class="cs-subtitle">Kasa oturumunu yönetmek için giriş yapın.</p></div>' +
      errorAlert() +
      '<form id="login-form" class="cs-field">' +
      '<label class="cs-field-label" for="username">Kullanıcı adı</label>' +
      '<input class="cs-input" type="text" id="username" autocomplete="username" required>' +
      '<label class="cs-field-label" for="password">Şifre</label>' +
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
      noticeAlert() +
      '<label class="cs-field-label" for="opening-balance">Açılış Tutarı</label>' +
      '<label class="cs-amount-field"><input type="number" step="0.01" min="0" id="opening-balance" ' +
      'value="' + (suggested != null ? Number(suggested).toFixed(2) : '0.00') + '">' +
      '<span class="cs-amount-suffix" aria-hidden="true">₺</span></label>' +
      '<span class="cs-field-hint" id="opening-hint">' + (suggested != null
        ? 'Önerilen tutar: ' + escapeHtml(formatMoney(suggested)) + ' (son kapanışta sayılan nakit). Çekmecedeki parayı sayıp gerekirse değiştirin.'
        : 'Önceki kapanıştan öneri yok; çekmecedeki parayı sayıp yazın.') + '</span>' +
      '<button class="cs-btn cs-btn-primary" id="open-btn" ' + (state.busy ? 'disabled' : '') + '>' +
      (state.busy ? 'Açılıyor…' : 'Kasayı Aç') + '</button>' +
      '</div>';
    var openBtn = document.getElementById('open-btn');
    openBtn.addEventListener('click', function () {
      var value = Number(document.getElementById('opening-balance').value);
      if (!(value >= 0)) {
        setError('Geçerli bir tutar girin.');
        return;
      }
      submitOpen(value);
    });
    document.getElementById('opening-balance').addEventListener('keydown', function (event) {
      if (event.key === 'Enter') openBtn.click();
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
    var hasPending = !!(state.movements && state.movements.requests.length);
    // The question stays only while nothing else on the screen changed under it: any busy moment or new pending request drops it.
    var asking = state.startCountConfirm && !state.busy && !hasPending && mayActForOwner();
    state.startCountConfirm = asking;
    app.innerHTML =
      '<div class="cs-card">' +
      '<div class="cs-header">' +
      '<div><span class="cs-eyebrow">Kasa Oturumu</span>' +
      '<h1 class="cs-title">' + escapeHtml(sessionOwnerName() || '') + '</h1></div>' +
      '<span class="cs-status-pill is-open"><span class="cs-status-dot"></span>Açık</span>' +
      '</div>' +
      errorAlert() +
      noticeAlert() +
      ownerNotice() +
      '<div class="cs-grid-2">' +
      '<div class="cs-stat"><div class="cs-stat-label">Açılış Tutarı</div>' +
      '<div class="cs-stat-value">' + formatMoney(s.openingBalance) + '</div></div>' +
      '<div class="cs-stat"><div class="cs-stat-label">Açılış Saati</div>' +
      '<div class="cs-stat-value">' + new Date(s.openedAt).toLocaleTimeString('tr-TR', TIME_FORMAT) + '</div></div>' +
      '</div>' +
      '<div class="cs-divider"></div>' +
      renderPendingRequests() +
      '<div class="cs-row">' +
      '<button class="cs-btn cs-btn-secondary" id="cash-in"' + (ownsSession() ? '' : ' disabled aria-describedby="movement-owner-hint"') + '>Nakit Giriş</button>' +
      '<button class="cs-btn cs-btn-secondary" id="cash-out"' + (ownsSession() ? '' : ' disabled aria-describedby="movement-owner-hint"') + '>Nakit Çıkış</button>' +
      '</div>' +
      renderNoSaleButton() +
      (ownsSession() ? '' : '<p class="cs-field-hint" id="movement-owner-hint">' + NOT_OWNED_MOVEMENT_TEXT + '</p>') +
      renderMovementList() +
      '<button class="cs-btn cs-btn-ghost" id="refresh-movements" type="button" ' + (state.busy ? 'disabled' : '') + '>Listeyi yenile</button>' +
      (!mayActForOwner()
        ? blockedCountHint()
        : hasPending
        ? '<p class="cs-field-hint" id="pending-hint">Onay bekleyen çıkış varken sayıma geçilemez; önce karara bağlayın veya geri çekin.</p>'
        : '<p class="cs-field-hint" id="count-hint">Sayım, kasa kapanışının ilk adımıdır; başlatınca geri dönülmez, sayımdan sonra kasa kapatılır.</p>') +
      (asking
        ? '<div class="cs-alert cs-alert-warning" id="start-count-warning" role="alert"><span class="cs-alert-icon">!</span>' +
          '<div class="cs-alert-body">Sayıma başlayınca kasada yeni satış ve nakit hareketi yapılamaz. Devam edilsin mi?</div></div>' +
          '<button class="cs-btn cs-btn-primary" id="start-count-confirm" type="button">Evet, sayıma başla</button>' +
          '<button class="cs-btn cs-btn-secondary" id="start-count-cancel" type="button">Vazgeç</button>'
        : '<button class="cs-btn cs-btn-primary" id="start-count" ' + (state.busy || hasPending || !mayActForOwner() ? 'disabled' : '') + '>' +
          (state.busy ? 'Bekleyin…' : 'Kasayı Kapat: Sayıma Başla') + '</button>') +
      '</div>';
    ['in', 'out'].forEach(function (direction) {
      document.getElementById('cash-' + direction).addEventListener('click', function () {
        if (state.movementDirection !== direction) {
          state.drafts.amount = '';
          state.drafts.notes = '';
        }
        state.movementDirection = direction;
        state.movementStep = 'form';
        state.phase = 'movement';
        state.error = null;
        state.notice = null;
        render();
      });
    });
    var noSale = document.getElementById('no-sale');
    if (noSale) {
      noSale.addEventListener('click', function () {
        state.error = null;
        state.notice = null;
        state.noSaleKey = null;
        state.phase = 'nosale';
        render();
      });
    }
    var startButton = document.getElementById('start-count');
    if (startButton) {
      startButton.addEventListener('click', function () {
        state.startCountConfirm = true;
        render();
      });
    } else {
      document.getElementById('start-count-confirm').addEventListener('click', function () {
        state.startCountConfirm = false;
        startCounting();
      });
      document.getElementById('start-count-cancel').addEventListener('click', function () {
        state.startCountConfirm = false;
        render();
      });
    }
    document.getElementById('refresh-movements').addEventListener('click', function () {
      state.error = null;
      state.pendingDecision = null;
      setBusy(true);
      Promise.all([loadMovements(), loadDrawer()]).then(function () { setBusy(false); });
    });
    // Approving moves money and rejecting cannot be undone, so both ask once more; withdrawing one's own request does not.
    Array.prototype.forEach.call(app.querySelectorAll('[data-request-action]'), function (button) {
      button.addEventListener('click', function () {
        var action = button.dataset.requestAction;
        var id = button.dataset.requestId;
        if (action === 'cancel') {
          decideRequest(id, action, DECISION_NOTICE[action]);
          return;
        }
        state.pendingDecision = { id: id, action: action };
        render();
      });
    });
    Array.prototype.forEach.call(app.querySelectorAll('[data-request-confirm]'), function (button) {
      button.addEventListener('click', function () {
        var decision = state.pendingDecision;
        state.pendingDecision = null;
        decideRequest(decision.id, decision.action, DECISION_NOTICE[decision.action]);
      });
    });
    Array.prototype.forEach.call(app.querySelectorAll('[data-approve-here]'), function (button) {
      button.addEventListener('click', function () { approveHere(button.dataset.approveHere); });
    });
    var back = document.getElementById('request-back');
    if (back) {
      back.addEventListener('click', function () {
        state.pendingDecision = null;
        render();
      });
    }
  }

  var DECISION_NOTICE = {
    approve: 'Çıkış onaylandı ve kasadan düşüldü.',
    reject: 'Çıkış talebi reddedildi.',
    cancel: 'Çıkış talebi geri çekildi.',
  };

  function requestButton(action, label, cssClass, request) {
    var who = request.requestedByName ? ', ' + request.requestedByName : '';
    return '<button class="cs-btn ' + cssClass + '" type="button" data-request-action="' + action +
      '" data-request-id="' + request.id + '" aria-label="' + label + ': ' + escapeHtml(formatMoney(request.amount) + who) + '"' +
      (state.busy ? ' disabled' : '') + '>' + label + '</button>';
  }

  function renderApproveHere(request) {
    var shown = state.approveHere.id === request.id ? state.approveHere : { name: '', error: null };
    var disabled = state.busy ? ' disabled' : '';
    return '<div><span class="cs-field-label">Yönetici burada onaylasın</span>' +
      (shown.error ? '<div class="cs-field-hint" role="alert">' + escapeHtml(shown.error) + '</div>' : '') +
      '<label class="cs-field-label" for="approver-name-' + request.id + '">Yönetici kullanıcı adı</label>' +
      '<input class="cs-input" type="text" id="approver-name-' + request.id + '" autocomplete="off" autocapitalize="none" spellcheck="false" maxlength="64" value="' +
      escapeHtml(shown.name) + '"' + disabled + '>' +
      '<label class="cs-field-label" for="approver-secret-' + request.id + '">Yönetici şifresi</label>' +
      '<input class="cs-input" type="password" id="approver-secret-' + request.id + '" autocomplete="new-password" maxlength="256"' + disabled + '>' +
      '<button class="cs-btn cs-btn-secondary" type="button" data-approve-here="' + request.id + '"' + disabled + '>Yönetici onayı ver</button></div>';
  }

  function renderPendingRequests() {
    var requests = state.movements ? state.movements.requests : [];
    if (!requests.length) return '';
    var rows = requests.map(function (r) {
      var own = r.requestedBy === state.userId;
      var buttons = '';
      var asking = state.pendingDecision && state.pendingDecision.id === r.id ? state.pendingDecision.action : null;
      if (asking) {
        buttons = '<span class="cs-field-hint" id="request-confirm-text">' +
          (asking === 'approve'
            ? formatMoney(r.amount) + ' kasadan düşülecek. Onaylıyor musunuz?'
            : 'Talep reddedilecek ve geri alınamaz. Reddediyor musunuz?') + '</span>' +
          '<button class="cs-btn ' + (asking === 'approve' ? 'cs-btn-success' : 'cs-btn-danger') + '" type="button" data-request-confirm="1"' +
          (state.busy ? ' disabled' : '') + '>' + (asking === 'approve' ? 'Evet, onayla' : 'Evet, reddet') + '</button>' +
          '<button class="cs-btn cs-btn-secondary" type="button" id="request-back">Hayır</button>';
      } else {
        if (canApprove()) {
          buttons += (own && !state.movements.selfApprovalEnabled)
            ? '<span class="cs-field-hint">Kendi talebinizi onaylayamazsınız; başka bir yönetici onaylamalı.</span>'
            : requestButton('approve', 'Onayla', 'cs-btn-success', r);
          buttons += requestButton('reject', 'Reddet', 'cs-btn-danger', r);
        }
        if (own) buttons += requestButton('cancel', 'Talebi geri çek', 'cs-btn-secondary', r);
      }
      return '<div class="cs-request" role="listitem">' +
        '<div class="cs-summary-row"><span>' + escapeHtml(r.requestedByName || '') + ' · ' +
        new Date(r.requestedAt).toLocaleTimeString('tr-TR', TIME_FORMAT) + '</span>' +
        '<span class="value">' + formatMoney(r.amount) + '</span></div>' +
        '<div class="cs-note">' + escapeHtml(r.reason) + '</div>' +
        '<div class="cs-row">' + buttons + '</div>' +
        (own && !canApprove() && !asking ? renderApproveHere(r) : '') + '</div>';
    }).join('');
    return '<div id="pending-requests" role="list" aria-label="Onay bekleyen çıkışlar"><div class="cs-eyebrow">Onay bekleyen çıkışlar</div>' + rows + '</div>';
  }

  function renderMovementList() {
    if (state.movementsError) {
      return '<p class="cs-field-hint" id="movement-list-error">Nakit hareketleri okunamadı.</p>';
    }
    if (!state.movements) return '';
    var entries = state.movements.movements;
    if (!entries.length) return '<p class="cs-field-hint" id="movement-list">Bu oturumda nakit giriş veya çıkış yok.</p>';
    var rows = entries.map(function (m) {
      var isIn = m.direction === 'In';
      var who = escapeHtml(m.recordedByName || '');
      if (m.approvedByName) {
        who += ' · ' + (m.selfApproved ? 'kendi onayı' : 'onaylayan: ' + escapeHtml(m.approvedByName));
      }
      return '<div class="cs-movement ' + (isIn ? 'is-in' : 'is-out') + '" role="listitem">' +
        '<div class="cs-summary-row"><span>' + new Date(m.occurredAt).toLocaleTimeString('tr-TR', TIME_FORMAT) + ' · ' +
        (isIn ? 'Giriş' : 'Çıkış') + '</span><span class="value">' + (isIn ? '+' : '-') + formatMoney(m.amount) + '</span></div>' +
        (m.notes ? '<div class="cs-note">' + escapeHtml(m.notes) + '</div>' : '') +
        '<div class="cs-field-hint">' + who + '</div></div>';
    }).join('');
    return '<div id="movement-list" role="list" aria-label="Nakit hareketleri"><div class="cs-eyebrow">Nakit hareketleri</div>' + rows +
      (state.movements.movementsTruncated ? '<p class="cs-field-hint">Yalnız son 200 hareket gösteriliyor.</p>' : '') + '</div>';
  }

  var OUT_REASONS = ['Banka', 'Gider', 'Tedarikçi', 'Bozuk para'];
  var IN_REASONS = ['Bozuk para', 'Banka', 'Takviye'];

  function renderNoSaleButton() {
    if (!canOpenDrawerWithoutSale()) return '';
    var count = state.drawer.noSaleCount;
    return '<div class="cs-row"><button class="cs-btn cs-btn-secondary" id="no-sale" type="button">Satış yok</button></div>' +
      (count > 0 ? '<p class="cs-field-hint" id="no-sale-count">Bu vardiyada satışsız açma: ' + count + '</p>' : '');
  }

  function renderNoSale() {
    var reasons = (state.drawer && state.drawer.reasons) || [];
    var chosen = reasons.filter(function (reason) { return reason.code === state.noSaleReason; })[0];
    app.innerHTML =
      '<div class="cs-card">' +
      '<div><span class="cs-eyebrow">Para Çekmecesi</span><h1 class="cs-title">Satış yok</h1></div>' +
      errorAlert() +
      '<p class="cs-field-hint">Para işlemi olmadan çekmece açılır; gerekçe zorunludur ve kayda geçer.</p>' +
      '<form id="no-sale-form" novalidate>' +
      '<span class="cs-field-label" id="no-sale-reason-label">Gerekçe</span>' +
      '<div class="cs-chips" role="group" aria-labelledby="no-sale-reason-label">' + reasons.map(function (reason) {
        return '<button class="cs-chip" type="button" data-no-sale-reason="' + escapeHtml(reason.code) + '" aria-pressed="' +
          (reason.code === state.noSaleReason) + '">' + escapeHtml(reason.name) + '</button>';
      }).join('') + '</div>' +
      (chosen ? '<p class="cs-field-hint" id="no-sale-chosen">Seçilen gerekçe: ' + escapeHtml(chosen.name) + '</p>' : '') +
      '<label class="cs-field-label" for="no-sale-note">' + (state.noSaleReason === 'Other' ? 'Not (zorunlu)' : 'Not') + '</label>' +
      '<textarea class="cs-textarea" id="no-sale-note" maxlength="200">' + escapeHtml(state.drafts.noSaleNote) + '</textarea>' +
      '<div class="cs-row">' +
      '<button class="cs-btn cs-btn-secondary" id="no-sale-cancel" type="button">Vazgeç</button>' +
      '<button class="cs-btn cs-btn-primary" id="no-sale-submit" type="submit" ' + (state.busy ? 'disabled' : '') + '>' +
      (state.busy ? 'Açılıyor…' : 'Çekmeceyi aç') + '</button>' +
      '</div></form></div>';
    Array.prototype.forEach.call(app.querySelectorAll('[data-no-sale-reason]'), function (chip) {
      chip.addEventListener('click', function () {
        state.noSaleReason = chip.dataset.noSaleReason;
        render();
        var pressed = app.querySelector('[data-no-sale-reason="' + state.noSaleReason + '"]');
        if (pressed) pressed.focus({ preventScroll: true });
      });
    });
    document.getElementById('no-sale-cancel').addEventListener('click', function () {
      state.noSaleReason = null;
      state.noSaleKey = null;
      state.drafts.noSaleNote = '';
      state.error = null;
      state.phase = 'open';
      render();
    });
    document.getElementById('no-sale-form').addEventListener('submit', function (event) {
      event.preventDefault();
      submitNoSale();
    });
  }

  function renderMovement() {
    var isIn = state.movementDirection === 'in';
    if (state.movementStep === 'summary') {
      renderMovementSummary(isIn);
      return;
    }
    app.innerHTML =
      '<div class="cs-card">' +
      '<div><span class="cs-eyebrow">Kasa Hareketi</span>' +
      '<h1 class="cs-title">' + (isIn ? 'Nakit Giriş' : 'Nakit Çıkış') + '</h1></div>' +
      errorAlert() +
      '<form id="movement-form" novalidate>' +
      '<label class="cs-field-label" for="movement-amount">Tutar</label>' +
      '<label class="cs-amount-field"><input type="number" inputmode="decimal" step="0.01" min="0.01" id="movement-amount" placeholder="0,00" value="' +
      escapeHtml(state.drafts.amount) + '">' +
      '<span class="cs-amount-suffix" aria-hidden="true">₺</span></label>' +
      '<label class="cs-field-label" for="movement-notes">Açıklama</label>' +
      '<div class="cs-chips">' + (isIn ? IN_REASONS : OUT_REASONS).map(function (reason) {
        return '<button class="cs-chip" type="button" data-reason="' + escapeHtml(reason) + '">' + escapeHtml(reason) + '</button>';
      }).join('') + '</div>' +
      '<textarea class="cs-textarea" id="movement-notes" maxlength="500" required placeholder="Örn. banka için para çekildi">' +
      escapeHtml(state.drafts.notes) + '</textarea>' +
      '<div class="cs-row">' +
      '<button class="cs-btn cs-btn-secondary" id="movement-cancel" type="button">Vazgeç</button>' +
      '<button class="cs-btn ' + (isIn ? 'cs-btn-success' : 'cs-btn-danger') + '" id="movement-submit" type="submit" ' +
      (state.busy ? 'disabled' : '') + '>' + (state.busy ? 'Hazırlanıyor…' : 'Devam') + '</button>' +
      '</div></form></div>';
    Array.prototype.forEach.call(app.querySelectorAll('[data-reason]'), function (chip) {
      chip.addEventListener('click', function () {
        var notes = document.getElementById('movement-notes');
        notes.value = chip.dataset.reason;
        state.drafts.notes = notes.value;
        notes.focus();
      });
    });
    document.getElementById('movement-cancel').addEventListener('click', function () {
      state.drafts.amount = '';
      state.drafts.notes = '';
      state.error = null;
      state.phase = 'open';
      render();
    });
    document.getElementById('movement-form').addEventListener('submit', function (event) {
      event.preventDefault();
      captureDrafts();
      var amount = Number(state.drafts.amount);
      var notes = state.drafts.notes.trim();
      if (!(amount > 0)) {
        setError('Tutar sıfırdan büyük olmalı.');
        return;
      }
      if (!notes) {
        setError('Açıklama zorunludur.');
        return;
      }
      if (notes.length > 500) {
        setError('Açıklama en çok 500 karakter olabilir.');
        return;
      }
      openMovementSummary();
    });
  }

  function renderMovementSummary(isIn) {
    var amount = Number(state.drafts.amount);
    // Blind count: the expected cash only feeds the guard below and is never shown before the count is sent.
    var expected = state.summaryExpected;
    var threshold = state.movements ? state.movements.approvalThreshold : null;
    var needsApproval = !isIn && threshold != null && amount > threshold;
    var exceedsDrawer = !isIn && expected != null && amount > expected;
    app.innerHTML =
      '<div class="cs-card">' +
      '<div><span class="cs-eyebrow">Kasa Hareketi</span>' +
      '<h1 class="cs-title">Kaydetmeden Önce Kontrol Edin</h1></div>' +
      errorAlert() +
      '<div class="cs-summary-row"><span>Yön</span><span class="value">' + (isIn ? 'Nakit Giriş' : 'Nakit Çıkış') + '</span></div>' +
      '<div class="cs-summary-row"><span>Tutar</span><span class="value">' + formatMoney(amount) + '</span></div>' +
      '<div class="cs-summary-row"><span>Açıklama</span><span class="value">' + escapeHtml(state.drafts.notes.trim()) + '</span></div>' +
      (needsApproval
        ? '<div class="cs-alert cs-alert-danger" role="status" id="approval-warning"><span class="cs-alert-icon">!</span>' +
          '<div class="cs-alert-body">Yönetici onayı gerekecek. Onaylanana kadar bu tutar kasadan düşmez.</div></div>'
        : '') +
      (exceedsDrawer
        ? '<div class="cs-alert cs-alert-danger" role="alert" id="drawer-warning"><span class="cs-alert-icon">!</span>' +
          '<div class="cs-alert-body">Çıkış tutarı kasadaki beklenen nakdi aşıyor; kaydedilemez.</div></div>'
        : '') +
      '<div class="cs-row">' +
      '<button class="cs-btn cs-btn-secondary" id="movement-edit">Düzelt</button>' +
      '<button class="cs-btn ' + (isIn ? 'cs-btn-success' : 'cs-btn-danger') + '" id="movement-confirm" ' +
      (state.busy || exceedsDrawer ? 'disabled' : '') + '>' +
      (state.busy ? 'Kaydediliyor…' : (needsApproval ? 'Onaya gönder' : 'Onayla ve kaydet')) + '</button>' +
      '</div></div>';
    document.getElementById('movement-edit').addEventListener('click', function () {
      state.movementStep = 'form';
      state.error = null;
      render();
    });
    document.getElementById('movement-confirm').addEventListener('click', function () {
      submitCashMovement(state.movementDirection, amount, state.drafts.notes.trim());
    });
  }

  // V1-RMD-377 (module-by-module UI audit round 2, P1 - rakip karşılaştırması): a real
  // competitor register (Toast/Square/Clover) counts a drawer by kupür (banknote/coin), not one
  // free-typed number the cashier has to mentally add up themselves - one keystroke error in
  // that mental math used to become an unexplained variance at close with no way to trace which
  // denomination it came from.
  function denominationTotal() {
    var total = 0;
    for (var i = 0; i < DENOMINATIONS.length; i++) {
      var d = DENOMINATIONS[i];
      total += d * Math.max(0, Math.floor(Number(state.denominationCounts[d]) || 0));
    }
    return Math.round(total * 100) / 100;
  }

  function denominationLabel(d) {
    return d >= 1 ? (d + ' ₺') : (Math.round(d * 100) + ' kr');
  }

  function renderDenominationRows() {
    var rows = DENOMINATIONS.map(function (d) {
      var count = state.denominationCounts[d] || 0;
      return (
        '<div class="cs-denom-row">' +
        '<label class="cs-denom-label" for="denom-' + d + '">' + denominationLabel(d) + '</label>' +
        '<input class="cs-denom-count" type="number" min="0" step="1" inputmode="numeric"' + (state.zeroCountPrompt ? ' disabled' : '') + ' ' +
        'id="denom-' + d + '" data-denom="' + d + '" value="' + count + '">' +
        '<span class="cs-denom-subtotal">' + formatMoney(d * count) + '</span>' +
        '</div>'
      );
    }).join('');
    return (
      '<div class="cs-denom-list">' + rows + '</div>' +
      '<div class="cs-summary-row cs-denom-total"><span>Toplam</span>' +
      '<span class="value" id="denom-total-value">' + formatMoney(denominationTotal()) + '</span></div>'
    );
  }

  function renderCounting() {
    var isDenom = state.countMode === 'denomination';
    app.innerHTML =
      '<div class="cs-card">' +
      '<div><span class="cs-eyebrow">Sayım</span>' +
      '<h1 class="cs-title">Çekmecedeki Nakdi Sayın</h1>' +
      '<p class="cs-subtitle">' +
      (isDenom
        ? 'Her kupürden kaç adet olduğunu girin, toplam otomatik hesaplanır.'
        : 'Elinizdeki gerçek nakit tutarını girin.') +
      '</p></div>' +
      errorAlert() +
      noticeAlert() +
      ownerNotice() +
      (isDenom
        ? renderDenominationRows()
        : ('<label class="cs-field-label" for="counted-amount">Sayılan Tutar</label>' +
           '<label class="cs-amount-field"><input type="number" inputmode="decimal" step="0.01" min="0" id="counted-amount"' +
           (state.zeroCountPrompt ? ' disabled' : '') + ' value="' + escapeHtml(state.drafts.counted || '0.00') + '">' +
           '<span class="cs-amount-suffix" aria-hidden="true">₺</span></label>')) +
      '<label class="cs-field-label" for="count-notes">Not <span style="font-weight:400;color:var(--color-text-dim)">(isteğe bağlı)</span></label>' +
      '<textarea class="cs-textarea" id="count-notes"' + (state.zeroCountPrompt ? ' disabled' : '') + '>' + escapeHtml(state.drafts.countNotes) + '</textarea>' +
      '<button class="cs-btn cs-btn-ghost" id="toggle-count-mode" type="button"' + (state.zeroCountPrompt ? ' disabled' : '') + '>' +
      (isDenom ? 'Kupürüm yok, tek tutar gireceğim' : 'Kupür bazlı saymaya dön') +
      '</button>' +
      (state.zeroCountPrompt
        ? '<div class="cs-alert cs-alert-danger" role="alert" id="zero-count-prompt"><span class="cs-alert-icon">!</span>' +
          '<div class="cs-alert-body">Sayım 0 ₺ - çekmece boş mu?</div></div>' +
          '<div class="cs-row">' +
          '<button class="cs-btn cs-btn-secondary" id="zero-cancel" type="button">Hayır, düzelt</button>' +
          '<button class="cs-btn cs-btn-primary" id="zero-confirm" type="button" ' + (state.busy ? 'disabled' : '') + '>Evet, çekmece boş</button>' +
          '</div>'
        : (mayActForOwner() ? '' : blockedCountHint()) +
          '<button class="cs-btn cs-btn-primary" id="submit-count" ' + (state.busy || !mayActForOwner() ? 'disabled' : '') + '>' +
          (state.busy ? 'Kaydediliyor…' : 'Sayımı Kaydet') + '</button>') +
      '</div>';

    if (isDenom) {
      var inputs = app.querySelectorAll('.cs-denom-count');
      for (var i = 0; i < inputs.length; i++) {
        inputs[i].addEventListener('input', function (event) {
          var denom = Number(event.target.dataset.denom);
          var count = Math.max(0, Math.floor(Number(event.target.value) || 0));
          state.denominationCounts[denom] = count;
          // Targeted DOM updates, not a full render() - the same reasoning
          // cashier-app.js's own item-note-input already documents: a
          // re-render mid-keystroke would drop focus/caret out of the very
          // field the cashier is still typing into.
          var subtotalEl = event.target.parentElement.querySelector('.cs-denom-subtotal');
          if (subtotalEl) subtotalEl.textContent = formatMoney(denom * count);
          var totalEl = document.getElementById('denom-total-value');
          if (totalEl) totalEl.textContent = formatMoney(denominationTotal());
        });
      }
    }
    document.getElementById('toggle-count-mode').addEventListener('click', function () {
      captureDrafts();
      state.countMode = isDenom ? 'manual' : 'denomination';
      state.error = null;
      render();
    });
    function currentCount() {
      captureDrafts();
      return isDenom ? denominationTotal() : Number(state.drafts.counted);
    }
    function sendCount() {
      var amount = currentCount();
      if (!(amount >= 0)) {
        setError('Geçerli bir tutar girin.');
        return;
      }
      submitCount(amount, state.drafts.countNotes.trim());
    }
    if (state.zeroCountPrompt) {
      document.getElementById('zero-cancel').addEventListener('click', function () {
        state.zeroCountPrompt = false;
        render();
      });
      document.getElementById('zero-confirm').addEventListener('click', sendCount);
      return;
    }
    document.getElementById('submit-count').addEventListener('click', function () {
      var amount = currentCount();
      if (amount === 0) {
        state.zeroCountPrompt = true;
        render();
        return;
      }
      sendCount();
    });
  }

  var DIFFERENCE_REASONS = ['Para üstü yanlış verildi', 'Sayım hatası', 'Kasadan çıkış kaydedilmedi', 'Bahşiş ayrıldı'];

  function renderClosing() {
    var expected = state.expectedCash;
    var counted = state.lastCountedAmount;
    var hasExpected = expected != null;
    var difference = hasExpected ? counted - expected : null;
    var exceeded = state.varianceExceeded;
    var hasDifference = hasExpected && difference !== 0;
    var unsettled = state.openBills > 0 || state.unsettledPayments > 0;
    var acknowledged = !!state.drafts.acknowledged;
    var closeDisabled = state.busy || !hasExpected || (unsettled && !acknowledged);
    app.innerHTML =
      '<div class="cs-card">' +
      '<div><span class="cs-eyebrow">Kapatma</span>' +
      '<h1 class="cs-title">Fark Teyidi</h1></div>' +
      errorAlert() +
      noticeAlert() +
      '<div class="cs-summary-row"><span>Beklenen</span><span class="value">' +
      (hasExpected ? formatMoney(expected) : '—') + '</span></div>' +
      '<div class="cs-summary-row"><span>Sayılan</span><span class="value">' + formatMoney(counted) + '</span></div>' +
      (hasExpected
        ? '<div class="cs-summary-row"><span>Fark</span><span class="value" style="color:' +
          (exceeded ? 'var(--color-danger)' : 'var(--color-success)') + '">' + formatMoney(difference) + '</span></div>'
        : '') +
      (state.tolerance != null
        ? '<p class="cs-field-hint" id="close-tolerance">Kapanışta kabul edilen fark: en çok ' + formatMoney(state.tolerance) +
          ' (yönetici ayarı); fazlası yönetici onayı ister.</p>'
        : '') +
      (unsettled
        ? '<div class="cs-alert cs-alert-warning" id="unsettled-warning" role="alert"><span class="cs-alert-icon">!</span>' +
          '<div><div class="cs-alert-title">İşletmede açık işlem var</div>' +
          '<div class="cs-alert-body">' + state.openBills + ' açık hesap veya sipariş, ' + state.unsettledPayments +
          ' sonucu belirsiz ödeme var. Kasayı kapatmak bunları çözmez.</div></div></div>' +
          '<label class="cs-check" for="ack-unsettled"><input type="checkbox" id="ack-unsettled" ' + (acknowledged ? 'checked' : '') +
          '><span>Bunları bilerek kapatıyorum</span></label>'
        : '') +
      (exceeded
        ? '<div class="cs-alert cs-alert-danger" role="alert"><span class="cs-alert-icon">!</span>' +
          '<div><div class="cs-alert-title">Fark tolerans sınırını aşıyor</div>' +
          '<div class="cs-alert-body">Devam etmek için açıklama yazın; yönetici yanınızda kendi kullanıcı adı ve şifresini girsin.</div></div></div>' +
          '<label class="cs-field-label" for="override-reason">Yönetici Açıklaması</label>' +
          '<textarea class="cs-textarea" id="override-reason" maxlength="500">' + escapeHtml(state.drafts.explanation) + '</textarea>' +
          '<label class="cs-field-label" for="manager-username">Yönetici kullanıcı adı</label>' +
          '<input class="cs-input" type="text" id="manager-username" autocomplete="off" autocapitalize="none" spellcheck="false" maxlength="64" value="' +
          escapeHtml(state.drafts.managerUsername) + '">' +
          '<label class="cs-field-label" for="manager-password">Yönetici şifresi</label>' +
          '<input class="cs-input" type="password" id="manager-password" autocomplete="new-password" maxlength="256">' +
          '<p class="cs-field-hint">Yönetici hesabıyla kendiniz kapatıyorsanız bu iki alanı boş bırakın.</p>'
        : hasDifference
          ? '<label class="cs-field-label" for="difference-explanation">Fark Açıklaması (zorunlu)</label>' +
            '<textarea class="cs-textarea" id="difference-explanation" maxlength="500">' + escapeHtml(state.drafts.explanation) + '</textarea>'
          : '') +
      (hasDifference && !exceeded
        ? '<div class="cs-row" role="group" aria-label="Hazır gerekçeler">' + DIFFERENCE_REASONS.map(function (reason) {
            return '<button class="cs-btn cs-btn-secondary" type="button" data-reason-preset>' + reason + '</button>';
          }).join('') + '</div>'
        : '') +
      (hasExpected
        ? ''
        : '<p class="cs-field-hint" id="expected-missing">Beklenen tutar okunamadı; kasayı kapatmak için yeniden okuyun.</p>' +
          '<button class="cs-btn cs-btn-secondary" id="reread-expected" type="button" ' + (state.busy ? 'disabled' : '') + '>Beklenen tutarı yeniden oku</button>') +
      '<button class="cs-btn cs-btn-primary" id="submit-close" ' + (closeDisabled ? 'disabled' : '') + '>' +
      (state.busy ? 'Kapatılıyor…' : 'Kasayı Kapat') + '</button>' +
      '<button class="cs-btn cs-btn-secondary" id="recount" type="button" ' + (state.busy ? 'disabled' : '') + '>Sayımı düzelt</button>' +
      '</div>';
    document.getElementById('recount').addEventListener('click', function () {
      state.phase = 'counting';
      state.drafts.acknowledged = false;
      state.varianceExceeded = false;
      state.error = null;
      state.notice = null;
      render();
    });
    document.querySelectorAll('[data-reason-preset]').forEach(function (button) {
      button.addEventListener('click', function () {
        var field = document.getElementById('difference-explanation');
        field.value = button.textContent;
        field.focus();
      });
    });
    var reread = document.getElementById('reread-expected');
    if (reread) reread.addEventListener('click', rereadExpectedCash);
    var ack = document.getElementById('ack-unsettled');
    if (ack) {
      ack.addEventListener('change', function () {
        state.drafts.acknowledged = ack.checked;
        render();
        document.getElementById('ack-unsettled').focus();
      });
    }
    document.getElementById('submit-close').addEventListener('click', function () {
      var field = document.getElementById('override-reason') || document.getElementById('difference-explanation');
      var explanation = field ? field.value.trim() : '';
      if (exceeded && !explanation) {
        setError('Fark tolerans sınırını aşıyor; bir açıklama girin.');
        return;
      }
      if (hasDifference && !explanation) {
        setError('Kasada fark var; kapatmak için fark açıklaması yazın.');
        return;
      }
      var username = exceeded ? document.getElementById('manager-username').value.trim() : '';
      // The password lives only in its field: never in state, so the next render empties it.
      var password = exceeded ? document.getElementById('manager-password').value : '';
      if (!username !== !password) {
        setError('Yönetici kullanıcı adı ve şifresi birlikte girilmelidir.');
        return;
      }
      state.drafts.explanation = explanation;
      submitClose(exceeded, explanation, username ? { username: username, password: password } : null);
    });
  }

  // Shown only when the drawer was used in this shift, so a till without a drawer sees nothing about it.
  function closedDrawerRows() {
    var drawer = state.closeResult && state.closeResult.drawer;
    if (!drawer || !(drawer.saleOpenCount + drawer.noSaleCount + drawer.noSaleKickFailedCount + drawer.saleKickFailedCount > 0)) return '';
    // A cashier's failed no-sale attempt and a printer that did not answer after a cash payment are two different counts.
    return '<div class="cs-summary-row" id="closed-drawer-no-sale"><span>Satışsız çekmece açma</span><span class="value">' + drawer.noSaleCount + '</span></div>' +
      (drawer.noSaleKickFailedCount > 0
        ? '<div class="cs-summary-row" id="closed-drawer-failed"><span>Açılamayan satışsız çekmece denemesi</span><span class="value">' + drawer.noSaleKickFailedCount + '</span></div>'
        : '') +
      (drawer.saleKickFailedCount > 0
        ? '<div class="cs-summary-row" id="closed-drawer-sale-failed"><span>Ödeme sırasında açılamayan çekmece</span><span class="value">' + drawer.saleKickFailedCount + '</span></div>'
        : '');
  }

  // Only a count that was replaced gets a line: a first count that stands needs none.
  function closedCountRows() {
    var count = state.closeResult && state.closeResult.count;
    if (!count || !(count.correctionCount > 0)) return '';
    return '<div class="cs-summary-row" id="closed-count-first"><span>İlk sayım</span><span class="value">' + formatMoney(count.firstCountedAmount) + '</span></div>' +
      '<div class="cs-summary-row" id="closed-count-corrections"><span>Sayım düzeltildi</span><span class="value">' + count.correctionCount + ' kez</span></div>';
  }

  // The shift is over: the cashier leaves this terminal, the next one signs in. A failed sign-out is said, never hidden.
  function signOut() {
    state.error = null;
    setBusy(true);
    api('/api/v1/auth/logout?terminalId=' + encodeURIComponent(state.terminalId), { method: 'POST' }).then(function (result) {
      state.busy = false;
      // A session that already ended is a finished sign-out.
      if (!result.ok && result.status !== 401) {
        state.error = 'Çıkış yapılamadı. Tekrar deneyin.';
        render();
        return;
      }
      window.location.assign('/');
    }).catch(function () {
      state.busy = false;
      setError('Çıkış yapılamadı. Tekrar deneyin.');
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
      errorAlert() +
      '<div style="text-align:left;width:100%">' +
      '<div class="cs-summary-row"><span>Açılış · Kapanış</span><span class="value">' +
      new Date(s.openedAt).toLocaleTimeString('tr-TR', TIME_FORMAT) + ' – ' +
      (s.closedAt ? new Date(s.closedAt).toLocaleTimeString('tr-TR', TIME_FORMAT) : '—') +
      '</span></div>' +
      '<div class="cs-divider"></div>' +
      '<div class="cs-summary-row"><span>Sayılan</span><span class="value">' + formatMoney(state.lastCountedAmount) + '</span></div>' +
      '<div class="cs-summary-row"><span>Fark</span><span class="value" style="color:' +
      (Math.abs(difference) > 0.005 ? 'var(--color-danger)' : 'var(--color-success)') + '">' +
      formatMoney(difference) + '</span></div>' +
      closedCountRows() +
      closedDrawerRows() +
      '</div>' +
      '<button class="cs-btn cs-btn-primary" id="cs-exit" style="width:100%"' + (state.busy ? ' disabled' : '') + '>Çıkış yap</button>' +
      '<button class="cs-btn cs-btn-secondary" id="reopen" style="width:100%"' + (state.busy ? ' disabled' : '') + '>Yeni Vardiya Aç</button>' +
      '</div>';
    document.getElementById('cs-exit').addEventListener('click', signOut);
    document.getElementById('reopen').addEventListener('click', function () {
      state.session = null;
      resetForNewSession();
      state.closeResult = null;
      state.lastCountedAmount = null;
      state.expectedCash = null;
      state.varianceExceeded = false;
      state.error = null;
      loadActiveSession();
    });
  }

  // V1-RMD-361: render() is the ONE place that both knows the current phase and can compare it
  // against the phase that was on screen a moment ago - the right single point to decide whether
  // this call is a genuine screen change (focus the new screen) or just a busy-state re-render of
  // the SAME screen (leave focus exactly where the cashier put it).
  var lastFocusedPhase = null;
  var renderedKey = null;
  var lastFocusedError = null;

  function renderLoadError() {
    app.innerHTML =
      '<div class="cs-card cs-center-text">' +
      '<h1 class="cs-title">Kasa Bilgisi Okunamadı</h1>' +
      '<div class="cs-alert cs-alert-danger" role="alert"><span class="cs-alert-icon">!</span><div class="cs-alert-body">' + escapeHtml(state.error) + '</div></div>' +
      '<button class="cs-btn cs-btn-primary" type="button" id="cs-reload">Tekrar dene</button>' +
      '</div>';
    document.getElementById('cs-reload').addEventListener('click', function () { window.location.reload(); });
  }

  function screenKey() {
    return [state.phase, state.movementStep, state.zeroCountPrompt, state.countMode].join(':');
  }

  function captureDrafts() {
    // The screen still in the DOM is the previous one when the phase just changed; its fields are not drafts.
    if (renderedKey !== screenKey()) return;
    [['movement-amount', 'amount'], ['movement-notes', 'notes'], ['counted-amount', 'counted'],
      ['count-notes', 'countNotes'], ['override-reason', 'explanation'], ['manager-username', 'managerUsername'],
      ['difference-explanation', 'explanation'], ['no-sale-note', 'noSaleNote']].forEach(function (pair) {
      var element = document.getElementById(pair[0]);
      if (element && app.contains(element)) state.drafts[pair[1]] = element.value;
    });
  }

  function render() {
    captureDrafts();
    if (state.phase === 'checking') {
      app.innerHTML = '<div class="cs-loading">Oturum kontrol ediliyor…</div>';
      renderedKey = null;
      return;
    }
    app.setAttribute('aria-busy', state.busy ? 'true' : 'false');
    switch (state.phase) {
      case 'login': renderLogin(); break;
      case 'loadError': renderLoadError(); break;
      case 'noSession': renderNoSession(); break;
      case 'conflict': renderConflict(); break;
      case 'open': renderOpen(); break;
      case 'movement': renderMovement(); break;
      case 'nosale': renderNoSale(); break;
      case 'counting': renderCounting(); break;
      case 'closing': renderClosing(); break;
      case 'closed': renderClosed(); break;
      default: renderOpen(); break;
    }
    renderedKey = screenKey();
    if (lastFocusedPhase !== renderedKey) {
      lastFocusedPhase = renderedKey;
      focusPrimaryField();
    }
    if (state.error && state.error !== lastFocusedError) {
      var alertBox = app.querySelector('.cs-alert-danger');
      if (alertBox) alertBox.focus({ preventScroll: true });
    }
    lastFocusedError = state.error;
  }

  bootstrap();
})();
