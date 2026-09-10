// ALKAROS Waiter PWA Service Worker (V1-WTR-006, V1-WTR-011)
// v3: the shell was rewritten in V1-WTR-010 and the brand asset is new, so a
// device holding the v2 cache must not keep serving the old screen.
const CACHE_NAME = 'alkaros-waiter-v3';
const ASSETS_TO_CACHE = [
  './',
  './index.html',
  './waiter-app.css',
  './waiter-app.js',
  './manifest.json',
  './vendor/signalr.min.js',
  './brand/alkaros-logo-on-dark.png',
  './icon-192.png'
];

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

  event.respondWith(
    caches.match(event.request).then((cachedResponse) => {
      if (cachedResponse) {
        return cachedResponse;
      }
      return fetch(event.request).then((networkResponse) => {
        if (networkResponse && networkResponse.status === 200) {
          const responseToCache = networkResponse.clone();
          caches.open(CACHE_NAME).then((cache) => {
            cache.put(event.request, responseToCache);
          });
        }
        return networkResponse;
      }).catch(() => {
        if (event.request.headers.get('accept')?.includes('text/html')) {
          return caches.match('./index.html');
        }
      });
    })
  );
});
