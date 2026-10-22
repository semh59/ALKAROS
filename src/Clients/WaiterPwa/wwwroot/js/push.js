// ALKAROS Waiter PWA — Web Push (V1-WTR-051, step 15/? of
// docs/engineering/garson-refactor-plan.md's Section 2). V1-WTR-011.
// SignalR only reaches a device whose app is open; a plated dish is
// announced exactly when it is not. The server does the encryption
// (RFC 8291), so all this side does is subscribe and hand the browser's
// own subscription over.
//
// Fully self-contained (only state, toast, api/apiUrl and browser APIs) —
// despite the plan's own note that this and offline-queue.js are the
// two riskiest remaining modules, push.js itself turned out to have no
// dependency on anything not already extracted; the risk the plan is
// warning about belongs entirely to offline-queue.js.

import { state } from './state.js';
import { toast } from './toast.js';
import { apiUrl, api } from './api.js';

// Remembers that this person turned notifications on, so a subscription the browser dropped (it rotates them)
// or the server pruned is re-registered the next time the app opens instead of silently staying off.
const PUSH_WANTED_KEY = 'alkaros_waiter_push_wanted';

function setPushWanted(wanted) {
  try {
    if (wanted) localStorage.setItem(PUSH_WANTED_KEY, '1');
    else localStorage.removeItem(PUSH_WANTED_KEY);
  } catch { /* private mode: the flag is only a convenience */ }
}

function pushWanted() {
  try { return localStorage.getItem(PUSH_WANTED_KEY) === '1'; } catch { return false; }
}

export function pushSupported() {
  return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
}

// iOS grants Web Push only to a PWA installed on the home screen. Detecting
// it lets the screen say why instead of failing silently.
export function iosNeedsInstall() {
  const isIos = /iPad|iPhone|iPod/.test(navigator.userAgent);
  const standalone = window.matchMedia('(display-mode: standalone)').matches
    || window.navigator.standalone === true;
  return isIos && !standalone;
}

async function currentPushSubscription() {
  if (!pushSupported() || !navigator.serviceWorker.controller) {
    const registration = await navigator.serviceWorker.getRegistration();
    if (!registration) return null;
    return registration.pushManager.getSubscription();
  }
  const registration = await navigator.serviceWorker.ready;
  return registration.pushManager.getSubscription();
}

export async function refreshPushState() {
  try {
    state.pushEnabled = (await currentPushSubscription()) !== null;
  } catch {
    state.pushEnabled = false;
  }
}

function base64UrlToBytes(value) {
  const padded = (value + '='.repeat((4 - (value.length % 4)) % 4))
    .replace(/-/g, '+').replace(/_/g, '/');
  const binary = window.atob(padded);
  return Uint8Array.from(binary, (character) => character.charCodeAt(0));
}

export async function enablePush() {
  if (!pushSupported()) {
    toast('Bu tarayıcı arka plan bildirimini desteklemiyor.', { warning: true });
    return;
  }
  if (iosNeedsInstall()) {
    toast('Önce uygulamayı ana ekrana ekleyin; iPhone bildirimi yalnız öyle veriyor.', { warning: true });
    return;
  }
  if (!window.isSecureContext) {
    toast('Arka plan bildirimi için güvenli bağlantı (HTTPS) gerekli.', { warning: true });
    return;
  }
  // V1-RMD-170: found by the 2026-09-10 Garson audit — if
  // registerOfflineWorker() already failed (state.offlineDisabled), no
  // service worker will ever activate, so the `navigator.serviceWorker.ready`
  // await further down used to hang forever: no error, no toast, the
  // waiter tapping "bildirimleri aç" just got a screen that never
  // responded again.
  if (state.offlineDisabled) {
    toast('Arka plan bildirimi kurulamadı: çevrimdışı mod kapalı.', { warning: true });
    return;
  }

  // The browser requires this to come from a user gesture, which is why it
  // lives behind a button in the profile sheet and not in start().
  let permission;
  try {
    permission = await Notification.requestPermission();
  } catch {
    permission = 'denied';
  }
  if (permission !== 'granted') {
    // 'denied' is a browser setting the waiter has to change; 'default' means the prompt was only dismissed.
    toast(permission === 'denied'
      ? 'Bildirim izni kapalı. Tarayıcı ayarlarından bu site için bildirimi açın.'
      : 'Bildirim izni verilmedi. Tekrar deneyip "İzin ver"i seçin.', { warning: true });
    return;
  }

  if (await subscribeAndRegister(true)) toast('Uygulama kapalıyken de bildirim gelecek.');
}

// Re-registers this device when the person had notifications on: after a sign-out that did not complete, or when the
// browser or the server no longer holds the subscription. Never asks for permission and never shows a message.
export async function restorePush(force = false) {
  if (!pushSupported() || !window.isSecureContext || state.offlineDisabled) return;
  if (Notification.permission !== 'granted') return;
  if (!force && !pushWanted() && !state.pushEnabled) return;
  await subscribeAndRegister(false);
}

async function subscribeAndRegister(announce) {
  const key = await api(apiUrl('/push/public-key'));
  if (!key.ok) { if (announce) toast(key.message, { warning: true }); return false; }

  try {
    // V1-RMD-170: a bare `await navigator.serviceWorker.ready` still has
    // no timeout of its own — if registration reports success but the
    // worker somehow never actually activates (a genuinely broken state,
    // distinct from the registerOfflineWorker() failure already checked
    // above), this raced it against a bound instead of hanging silently
    // forever with the sheet stuck open.
    const registration = await Promise.race([
      navigator.serviceWorker.ready,
      new Promise((_, reject) => window.setTimeout(
        () => reject(new Error('service-worker-timeout')), 10000))
    ]);
    const subscription = await registration.pushManager.subscribe({
      // Chrome refuses a subscription that could be silent, and every
      // notification this app sends is shown anyway.
      userVisibleOnly: true,
      applicationServerKey: base64UrlToBytes(key.data.publicKey)
    });

    const payload = subscription.toJSON();
    const saved = await api(apiUrl('/push/subscriptions'), {
      method: 'POST',
      body: {
        endpoint: payload.endpoint,
        p256dh: payload.keys.p256dh,
        auth: payload.keys.auth
      }
    });
    if (!saved.ok) {
      // Registering the subscription is what makes it reachable; a browser
      // subscription the server does not know about is worse than none.
      await subscription.unsubscribe();
      if (announce) toast(saved.message, { warning: true });
      return false;
    }
    state.pushEnabled = true;
    setPushWanted(true);
    return true;
  } catch {
    if (announce) toast('Bildirim aboneliği kurulamadı.', { warning: true });
    return false;
  }
}

export async function unsubscribePush() {
  try {
    const subscription = await currentPushSubscription();
    if (subscription) {
      await api(apiUrl(`/push/subscriptions?endpoint=${encodeURIComponent(subscription.endpoint)}`),
        { method: 'DELETE' });
      await subscription.unsubscribe();
    }
  } catch {
    // Nothing to undo beyond the local flag; the server drops a dead
    // endpoint on its own the next time it sends (RFC 8030 §7.3).
  }
  state.pushEnabled = false;
  setPushWanted(false);
}

export async function disablePush() {
  await unsubscribePush();
  toast('Arka plan bildirimi kapatıldı.');
}
