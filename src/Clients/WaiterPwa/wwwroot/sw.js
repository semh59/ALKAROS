// ALKAROS Waiter PWA Service Worker (V1-WTR-006, V1-WTR-011, V1-WTR-037,
// V1-WTR-038, V1-WTR-039, V1-WTR-040, V1-WTR-041, V1-WTR-042, V1-WTR-043,
// V1-WTR-044, V1-WTR-045, V1-WTR-046, V1-WTR-047, V1-WTR-048)
// v15: openTable() finally moved into js/screens/tables.js (bill.js and
// menu.js now both exist); js/sheets/transfer.js split off.
const CACHE_NAME = 'alkaros-waiter-v15';
const ASSETS_TO_CACHE = [
  './',
  './index.html',
  './waiter-app.css',
  './waiter-app.js',
  './js/util.js',
  './js/state.js',
  './js/auth.js',
  './js/api.js',
  './js/toast.js',
  './js/options-sheet.js',
  './js/kiosk-lock.js',
  './js/features.js',
  './js/screens/tables.js',
  './js/screens/menu.js',
  './js/sheets/party-size.js',
  './js/sheets/bill.js',
  './js/sheets/product-sheet.js',
  './js/sheets/void-comp.js',
  './js/sheets/help-request.js',
  './js/sheets/transfer.js',
  './manifest.json',
  './vendor/signalr.min.js',
  './brand/alkaros-logo-on-dark.png',
  './icon-192.png'
];

// V1-RMD-178: rejects with the same shape a network-level fetch() failure
// already produces, so the caller's existing .catch(...) fallback handles
// both "no connection" and "connected but nothing is answering" alike.
function fetchWithTimeout(request, timeoutMs) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('fetch timed out')), timeoutMs);
    fetch(request).then(
      (response) => { clearTimeout(timer); resolve(response); },
      (error) => { clearTimeout(timer); reject(error); });
  });
}

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => {
      return cache.addAll(ASSETS_TO_CACHE);
    }).then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) => {
      return Promise.all(
        keys.map((key) => {
          if (key !== CACHE_NAME) {
            return caches.delete(key);
          }
        })
      );
    }).then(() => self.clients.claim())
  );
});

// V1-WTR-011: what reaches the device when the app is not open.
//
// SignalR only ever reached a device whose app was in the foreground and
// connected, which is not the state a waiter's phone is in while they are
// carrying plates. The server encrypts this payload per RFC 8291
// (WebPushCrypto); by the time it arrives the browser has already decrypted
// it, so the body below is the plain WebPushMessage.
self.addEventListener('push', (event) => {
  let message = null;
  try {
    message = event.data ? event.data.json() : null;
  } catch {
    // A push with no payload, or one this version does not understand.
    message = null;
  }

  const title = (message && message.title) || 'ALKAROS';
  const body = (message && message.body) || 'Yeni bir bildirim var.';
  // The tag is shared with the in-page handler, so a device that is online
  // and gets both the SignalR event and the push shows one notification
  // rather than two.
  const tag = (message && message.tag) || 'alkaros-waiter';

  event.waitUntil(self.registration.showNotification(title, {
    body,
    tag,
    renotify: true,
    icon: './icon-192.png',
    badge: './icon-192.png',
    lang: 'tr',
    data: { url: (message && message.url) || './index.html' }
  }));
});

self.addEventListener('notificationclick', (event) => {
  event.notification.close();
  const target = (event.notification.data && event.notification.data.url) || './index.html';

  // Focus the app if it is already open anywhere rather than starting a
  // second copy of it.
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((windows) => {
      for (const client of windows) {
        if ('focus' in client) return client.focus();
      }
      return self.clients.openWindow(target);
    })
  );
});

self.addEventListener('fetch', (event) => {
  // Only cache GET requests for static assets
  if (event.request.method !== 'GET') return;

  const url = new URL(event.request.url);
  if (url.pathname.startsWith('/api/')) {
    // API calls are network-first, fallback handled by app queue
    return;
  }

  // V1-RMD-169: found by the 2026-09-10 Garson audit — cache-first meant a
  // deploy that changed waiter-app.js/waiter-app.css/index.html but left
  // sw.js itself byte-identical (CACHE_NAME not bumped, an easy step to
  // forget — the shell files and this file are edited independently, no
  // build step ties them together) was undetectable: the browser only
  // re-runs `install` when sw.js's own bytes change, so the stale cached
  // shell would be served forever with nothing telling anyone it was
  // stale. Network-first for the app shell fixes this without depending
  // on remembering to bump anything: an online device always gets the
  // real, current file; the cache exists purely as the offline fallback
  // it was always meant to be (the whole reason this app has an offline
  // queue). A background cache.put still runs on every successful fetch
  // so the offline fallback itself stays reasonably fresh too.
  //
  // V1-RMD-178: found by independent review — plain fetch() has no timeout
  // race. The failure mode that actually dominates a restaurant floor is
  // not "no network" (that rejects fast, .catch already handles it) but
  // "associated to a Wi-Fi AP with no real uplink" — TCP hangs rather than
  // resets, and the shell load stalled for the OS connect timeout instead
  // of painting instantly from cache like it should. A short race falls
  // back to the same cached-shell path a hard failure already uses.
  event.respondWith(
    fetchWithTimeout(event.request, 3000).then((networkResponse) => {
      if (networkResponse && networkResponse.status === 200) {
        const responseToCache = networkResponse.clone();
        caches.open(CACHE_NAME).then((cache) => {
          cache.put(event.request, responseToCache);
        });
      }
      return networkResponse;
    }).catch(() => {
      return caches.match(event.request).then((cachedResponse) => {
        if (cachedResponse) {
          return cachedResponse;
        }
        if (event.request.headers.get('accept')?.includes('text/html')) {
          return caches.match('./index.html');
        }
      });
    })
  );
});
