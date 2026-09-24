// ALKAROS Waiter PWA — session/permission primitives and the shared
// focus-trap stack (V1-WTR-039, step 3a/? of
// docs/engineering/garson-refactor-plan.md's Section 2).
//
// Deliberately scoped to the primitives with no dependency on api.js
// (submitLogin, hasValidSession and init() itself all call api() and stay
// in waiter-app.js for now — moving them here would make this module
// depend on api.js while api.js's own 401 handling depends on showLogin
// here, a circular import this step avoids by simply not reaching for it).
// The PIN-lock screen (openPinSheet, resetIdleTimer, lockScreen,
// renderPinDots/Pad) is its own, larger, more render-heavy concern than
// the plan's file listing implied on inspection — left for its own later
// step instead of folded in here.

import { state, el } from './state.js';

export function applyUser(user) {
  state.user = user;
  state.capabilities = (user && user.capabilities) || [];
  const name = (user && user.displayName) || 'Garson';
  el.userName.textContent = name;
  el.userInitials.textContent = name.trim().charAt(0).toLocaleUpperCase('tr-TR') || '?';
  // V1-RMD-175: found by the 2026-09-10 Garson audit — #userRole was
  // never written to at all, so it stayed on its static "Garson" HTML
  // default no matter who actually signed in (a supervisor's own
  // profile still said "Garson"). /auth/login and /auth/session now
  // both send the real roleName.
  el.userRole.textContent = (user && user.roleName) || 'Garson';
}

export function can(permission) {
  return state.capabilities.indexOf(permission) >= 0;
}

// V1-RMD-173: found by the 2026-09-10 Garson audit — every overlay here
// (PIN lock, login, the options/product/void/transfer sheet) hid the
// rest of the screen visually but left it fully focusable: a Bluetooth
// keyboard's Tab key, or a screen reader's virtual cursor, could still
// reach and activate buttons behind the lock screen — the PIN lock in
// particular is a real security gap, not just an accessibility one.
// `inert` (standard, no polyfill needed at this app's browser baseline)
// makes everything outside the active overlay simultaneously
// unfocusable, unclickable and invisible to assistive tech - the
// platform's own answer to "trap focus", nothing to reimplement by
// hand. `toasts` is deliberately never inert-ed: a toast's own "geri
// al" button must stay reachable no matter what else is open.
// V1-RMD-178: found by independent review of V1-RMD-173 — this used to
// be a single global slot: a second trapBackgroundExcept() call (e.g. the
// PIN lock firing while the options sheet was still open) released the
// FIRST trap's record and replaced it, so releaseTrap() only ever knew
// how to undo the MOST RECENT call. Closing the options sheet (even via
// Escape, now blocked separately above) while the lock overlay was the
// active layer popped the lock's own trap and un-inerted the app behind
// it — the lock stayed visible, but Tab could walk straight through it.
// A real stack fixes this at the root: each push only inerts elements
// that were not ALREADY inert (so a nested trap records nothing new for
// whatever the outer trap already hid) and each pop undoes only what
// that specific push actually did — LIFO, but never double-touches an
// element another still-active layer needs to stay hidden.
const trapStack = [];

// A trap that is stacked on top of another one (the PIN lock or the login
// overlay appearing while the options sheet is open) must also make ITS OWN
// active elements live again: the lower trap already inerted every other
// body child, including this overlay, and "only inert what is not already
// inert" alone never undoes that. Without it the overlay is visible but
// dead - PIN keys and login fields cannot be clicked or focused, so the
// waiter cannot unlock or sign in. Each push therefore records both what it
// newly inerted and what it newly revived, and its pop hands both back.
export function trapBackgroundExcept(...activeElements) {
  const active = new Set(activeElements);
  const newlyInert = Array.from(document.body.children)
    .filter((child) => !active.has(child) && child.id !== 'toasts' && !child.inert);
  const newlyRevived = activeElements.filter((element) => element && element.inert);
  newlyInert.forEach((child) => { child.inert = true; });
  newlyRevived.forEach((element) => { element.inert = false; });
  trapStack.push({ newlyInert, newlyRevived });
}

export function releaseTrap() {
  const record = trapStack.pop();
  if (!record) return;
  record.newlyInert.forEach((child) => { child.inert = false; });
  record.newlyRevived.forEach((element) => { element.inert = true; });
}

export function showLogin() {
  // Found by the WaiterPwa E2E audit (2026-09-12): the FIRST session
  // check on a fresh load (no cookie yet) gets a 401, which api()'s own
  // generic interceptor already answers by calling showLogin() - then
  // init() itself, seeing the same 401 via hasValidSession(), calls
  // showLogin() again unconditionally. Two calls means two
  // trapBackgroundExcept() pushes for the same visible overlay, but
  // submitLogin() only ever calls releaseTrap() once on success - the
  // second (redundant) push was never undone, leaving #screens and
  // everything else permanently inert (uninteractive) after every
  // fresh login until a full page reload. Making this idempotent - a
  // no-op once the overlay is already showing - keeps the push/pop
  // count balanced no matter how many callers see the same 401.
  if (!el.loginOverlay.hidden) return;
  // V1-RMD-180: found by the 2026-09-12 five-agent independent Garson
  // audit — the idempotency guard above only covers "login is already
  // showing". It missed the PIN lock's own transition to login (too many
  // wrong PINs, submitPin()'s 423 branch; the same path any background
  // 401 takes while state.locked is true): the lock overlay's OWN trap
  // (pushed by lockScreen() via trapBackgroundExcept(el.lockOverlay)) was
  // still on the stack, unreleased, when the code below pushed a second
  // trap for the login overlay. submitLogin() only ever pops once on
  // success, so the lock's orphaned entry stayed on the stack forever -
  // exactly the double-push-without-a-matching-pop bug V1-RMD-178's stack
  // rewrite was meant to prevent, just reached through a different door.
  // Releasing the lock's own trap here first keeps every push paired with
  // exactly one pop, no matter which overlay handed off to which.
  if (state.locked) releaseTrap();
  el.loginOverlay.hidden = false;
  el.lockOverlay.hidden = true;
  state.locked = false;
  trapBackgroundExcept(el.loginOverlay);
  el.loginUsername.focus();
}
