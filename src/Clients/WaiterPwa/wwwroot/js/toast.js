// ALKAROS Waiter PWA — the toast notification (V1-WTR-040, step 4/? of
// docs/engineering/garson-refactor-plan.md's Section 2). Not named in the
// plan's own file listing, but genuinely self-contained (only touches
// el.toasts and escapeHtml) and a dependency almost every screen/sheet
// module still to come will need — extracted now as a leaf module rather
// than forced into an unrelated one later.

import { el } from './state.js';
import { escapeHtml } from './util.js';

// V1-RMD-166: found by the 2026-09-10 Garson audit — the toast count was
// unbounded, so a round of several items (one toast each, e.g. a
// nine-line table) stacked up and covered the screen. An undo toast is
// never force-closed early — cutting it short would silently take away
// the one chance to undo a removal — so the cap only ever prunes plain
// (non-undo) toasts, oldest first.
const MAX_VISIBLE_TOASTS = 4;

export function toast(text, options) {
  const settings = options || {};
  while (el.toasts.children.length >= MAX_VISIBLE_TOASTS) {
    const oldest = [...el.toasts.children].find((child) => !child.dataset.hasUndo);
    if (!oldest) break;
    oldest.remove();
  }

  const node = document.createElement('div');
  node.className = 'toast';
  if (settings.undo) node.dataset.hasUndo = 'true';
  // V1-RMD-363 (module-by-module UI audit): #toasts only ever had a bare
  // aria-live="polite" wrapper - it announces new children, but with no
  // role a screen reader has no way to tell an ordinary "eklendi" toast
  // apart from a real failure worth stopping for. A warning toast (the
  // same call sites cash-session.js/split-payment.js already mark with
  // role="alert") gets that role here too; a plain toast gets the milder
  // role="status", matching Cashier's own discount-success notice.
  node.setAttribute('role', settings.warning ? 'alert' : 'status');
  node.innerHTML = `
    <span class="toast-mark${settings.warning ? ' is-warning' : ''}">
      <svg class="icon" aria-hidden="true"><use href="#ico-${settings.warning ? 'alert' : 'check'}"/></svg>
    </span>
    <span class="toast-text">${escapeHtml(text)}</span>
    ${settings.undo ? '<button type="button" class="toast-undo">Geri al</button>' : ''}`;

  const close = () => { window.clearTimeout(timer); node.remove(); };
  // An undo needs time to be noticed and used (an accidental send, a slip of the thumb).
  const timer = window.setTimeout(close, settings.undo ? 10000 : settings.warning ? 6000 : 5000);
  if (settings.undo) {
    node.querySelector('.toast-undo').addEventListener('click', () => { close(); settings.undo(); });
  }
  el.toasts.appendChild(node);
}
