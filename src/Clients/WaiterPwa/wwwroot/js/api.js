// ALKAROS Waiter PWA — the one HTTP call every screen goes through
// (V1-WTR-039, step 3b/? of docs/engineering/garson-refactor-plan.md's
// Section 2). Depends on auth.js (the 401 interceptor calls showLogin());
// auth.js has no dependency back on this module, so this stays a plain
// one-directional edge, not a circular import.

import { state } from './state.js';
import { describeHttpFailure } from './util.js';
import { showLogin } from './auth.js';

export function apiUrl(path) {
  return `/api/v1/terminals/${state.terminalId}${path}`;
}

// One shape for every call: never throws, never leaks a browser message.
export async function api(path, options) {
  const config = Object.assign({ credentials: 'include' }, options || {});
  if (config.body !== undefined && typeof config.body !== 'string') {
    config.headers = Object.assign({ 'Content-Type': 'application/json' }, config.headers || {});
    config.body = JSON.stringify(config.body);
  }

  let response;
  try {
    response = await fetch(path, config);
  } catch {
    // A network-level failure throws before a response exists and its
    // message is the browser's own English text ("Failed to fetch").
    return { ok: false, status: 0, offline: true, message: 'Sunucuya ulaşılamadı. Bağlantınızı kontrol edin.' };
  }

  let data = null;
  if (response.status !== 204) {
    try { data = await response.json(); } catch { data = null; }
  }

  if (response.ok) return { ok: true, status: response.status, data, headers: response.headers };

  if (response.status === 401) showLogin();
  return {
    ok: false,
    status: response.status,
    data,
    headers: response.headers,
    // The server's own error.message is already Turkish everywhere this
    // client calls (V1-RMD-127); the dictionary covers anything that is not.
    message: (data && data.error && data.error.message) || describeHttpFailure(response.status)
  };
}
