/* ALKAROS browser telemetry for the plain-script apps (cashier, waiter). One file, copied into both apps.
 * It reports uncaught errors, failed network calls and clicks with fixed keys only: never a message, a stack,
 * visible text or a typed value. Interactions wait in a small queue of their own so they cannot crowd out orders. */
(function () {
  'use strict';

  var ERROR_ENDPOINT = '/api/v1/telemetry/browser-errors';
  var INTERACTION_ENDPOINT = '/api/v1/telemetry/interactions';
  var STORAGE_KEY = 'alkaros.telemetry.interactions.v1';
  var MAX_PER_ERROR = 3;
  var MAX_ERRORS = 20;
  var MAX_QUEUE = 200;
  var BATCH = 50;
  var MAX_STORED_CHARS = 20000;
  var FLUSH_MS = 10000;
  var KEY_SHAPE = /^[a-z0-9][a-z0-9._:-]*(@\/[a-z0-9._:/-]*)?$/;
  var INTERACTIVE = "button, a[href], [role='button'], [role='tab'], input[type='submit'], input[type='button'], [data-telemetry-id]";

  function isKey(key) { return typeof key === 'string' && key.length <= 64 && KEY_SHAPE.test(key); }

  function routeTemplate(path) {
    var templated = String(path).split(/[?#]/)[0]
      .replace(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi, ':id')
      .replace(/\d+/g, ':n').toLowerCase().replace(/[^a-z0-9._:/-]/g, '-');
    return templated.charAt(0) === '/' ? templated : '/' + templated;
  }

  /** data-telemetry-id, then the element id or an action attribute (code constants), then a route-level key. */
  function deriveKey(element, route) {
    var marked = element.closest('[data-telemetry-id]');
    var explicit = marked && marked.getAttribute('data-telemetry-id');
    if (isKey(explicit)) return explicit;
    var id = element.getAttribute('id');
    if (id && isKey('id:' + id.toLowerCase())) return 'id:' + id.toLowerCase();
    var actions = ['data-refund-action', 'data-receipt-action', 'data-action'];
    for (var i = 0; i < actions.length; i += 1) {
      var value = element.getAttribute(actions[i]);
      if (value && isKey(actions[i].replace('data-', '') + ':' + value.toLowerCase())) return actions[i].replace('data-', '') + ':' + value.toLowerCase();
    }
    return ('auto:' + element.tagName.toLowerCase() + '@' + route).slice(0, 64);
  }

  function newId() {
    if (window.crypto && window.crypto.randomUUID) return window.crypto.randomUUID();
    return 'e' + Date.now().toString(36) + Math.random().toString(36).slice(2, 10);
  }

  function install(options) {
    options = options || {};
    var send = options.send || function (url, body) {
      return !!(navigator.sendBeacon && navigator.sendBeacon(url, new Blob([body], { type: 'application/json' })));
    };
    var storage = options.storage || window.localStorage;
    var now = options.now || function () { return Date.now(); };
    var afterPaint = options.afterPaint || function (callback) { window.requestAnimationFrame(function () { window.setTimeout(callback, 0); }); };
    var originalFetch = window.fetch;
    var queue = [];
    var errorCounts = {};
    var errorTotal = 0;
    var errorsSeen = 0;
    var cleanups = [];

    function persist() {
      try {
        var text = JSON.stringify(queue);
        while (text.length > MAX_STORED_CHARS && queue.length > 0) {
          queue = queue.slice(Math.ceil(queue.length / 4));
          text = JSON.stringify(queue);
        }
        storage.setItem(STORAGE_KEY, text);
      } catch (failure) {
        // A full or blocked storage must never reach the app: drop our own queue so the app's writes keep working.
        queue = [];
        try { storage.removeItem(STORAGE_KEY); } catch (ignored) { queue = []; }
      }
    }

    try {
      var restored = JSON.parse(storage.getItem(STORAGE_KEY) || '[]');
      if (Array.isArray(restored)) queue = restored.slice(-MAX_QUEUE);
    } catch (failure) {
      queue = [];
    }

    function reportError(kind, dedupeKey) {
      var key = kind + ':' + dedupeKey;
      var seen = errorCounts[key] || 0;
      if (seen >= MAX_PER_ERROR || errorTotal >= MAX_ERRORS) return false;
      errorCounts[key] = seen + 1;
      errorTotal += 1;
      return send(ERROR_ENDPOINT, JSON.stringify({ eventId: newId(), kind: kind, route: window.location.pathname }));
    }

    function record(item) {
      queue.push(item);
      if (queue.length > MAX_QUEUE) queue = queue.slice(queue.length - MAX_QUEUE);
      persist();
    }

    function flush() {
      while (queue.length > 0) {
        var batch = queue.slice(0, BATCH);
        var at = now();
        var items = batch.map(function (item) {
          return {
            key: item.key, kind: item.kind, ageMs: Math.max(0, at - item.atMs), durationMs: item.durationMs,
            longTasks: 0, shiftMilli: 0, outcome: item.outcome
          };
        });
        if (!send(INTERACTION_ENDPOINT, JSON.stringify({ items: items }))) return false;
        queue = queue.slice(batch.length);
        persist();
      }
      return true;
    }

    function on(target, type, handler, capture) {
      target.addEventListener(type, handler, !!capture);
      cleanups.push(function () { target.removeEventListener(type, handler, !!capture); });
    }

    on(window, 'error', function (event) {
      errorsSeen += 1;
      reportError('js_error', String(event.filename) + ':' + event.lineno + ':' + event.colno);
    });
    on(window, 'unhandledrejection', function (event) {
      errorsSeen += 1;
      reportError('unhandled_rejection', (event.reason && event.reason.name) || 'rejection');
    });

    if (typeof originalFetch === 'function') {
      window.fetch = function (input, init) {
        var url = typeof input === 'string' ? input : (input && input.url) || '';
        var result = originalFetch.call(window, input, init);
        var watched = url.indexOf('/api/') === 0 && url.indexOf('/api/v1/telemetry/') !== 0;
        if (watched) {
          result.then(null, function () { reportError('network_error', routeTemplate(url)); });
        }
        return result;
      };
      cleanups.push(function () { window.fetch = originalFetch; });
    }

    on(window, 'click', function (event) {
      var target = event.target && event.target.closest ? event.target.closest(INTERACTIVE) : null;
      if (!target) return;
      var key = deriveKey(target, routeTemplate(window.location.pathname));
      var started = event.timeStamp;
      var wall = now();
      var errorsBefore = errorsSeen;
      afterPaint(function () {
        record({
          key: key, kind: 'click', atMs: wall, durationMs: Math.max(0, Math.round(window.performance.now() - started)),
          outcome: errorsSeen > errorsBefore ? 'error' : 'ok'
        });
      });
    }, true);

    var timer = window.setInterval(flush, FLUSH_MS);
    cleanups.push(function () { window.clearInterval(timer); });
    on(document, 'visibilitychange', function () { if (document.visibilityState === 'hidden') flush(); });
    on(window, 'pagehide', flush);

    return {
      flush: flush,
      size: function () { return queue.length; },
      uninstall: function () { cleanups.forEach(function (cleanup) { cleanup(); }); }
    };
  }

  window.AlkarosTelemetry = { install: install, deriveKey: deriveKey, routeTemplate: routeTemplate };
  if (!window.__ALKAROS_TELEMETRY_MANUAL) install();
})();
