// ALKAROS Waiter PWA — the one generic bottom-sheet every screen builds a
// specific sheet on top of (V1-WTR-041, step 5/? of
// docs/engineering/garson-refactor-plan.md's Section 2). Every future
// sheets/*.js module (void-comp, transfer, help-request, party-size,
// pending-orders, failed-orders, profile, pin) calls openOptions()/
// closeOptions() rather than touching the sheet's DOM directly — extracted
// now, ahead of any of those, so each can depend on this instead of on
// waiter-app.js itself.

import { state, el } from './state.js';
import { trapBackgroundExcept, releaseTrap } from './auth.js';

let lastOptionsFocus = null;

export function closeOptions() {
  // V1-RMD-178: found by independent review of V1-RMD-173 — Escape (or a
  // backdrop click, reachable if the lock overlay's own layering is ever
  // wrong) used to close the options sheet even while the device was
  // locked, because the check lived only in the keydown handler and only
  // tested the sheet's own is-open class, never state.locked. The single
  // choke point here is the one that actually matters: nothing may close
  // the options sheet while the PIN lock is the active overlay.
  if (state.locked) return;
  el.optionsSheet.classList.remove('is-open');
  el.optionsBackdrop.classList.remove('is-open');
  // V1-RMD-173: this sheet stays in the DOM at all times (CSS moves it
  // off-screen instead of removing it), so a closed sheet is still a
  // tab stop unless told otherwise.
  el.optionsSheet.inert = true;
  releaseTrap();
  if (lastOptionsFocus) { lastOptionsFocus.focus(); lastOptionsFocus = null; }
  state.optionsMode = null;
  state.optionsContext = null;
}

export function openOptions(mode, title, subtitle, bodyHtml, confirmLabel, footLabel, footValue) {
  state.optionsMode = mode;
  el.optionsTitle.textContent = title;
  el.optionsSub.textContent = subtitle || '';
  el.optionsBody.innerHTML = bodyHtml;
  el.optionsFootLabel.textContent = footLabel || '';
  el.optionsFootValue.textContent = footValue || '';
  el.optionsConfirm.textContent = confirmLabel;
  el.optionsConfirm.hidden = !confirmLabel;
  // Reset what the previous caller may have changed, so a danger-styled or
  // disabled button never leaks into the next sheet.
  el.optionsConfirm.className = 'btn btn-primary';
  el.optionsConfirm.disabled = false;
  // V1-RMD-178: found by independent review of V1-RMD-177 — a sheet can
  // repopulate itself while already open (openTransferServerSheet keeps
  // the profile sheet showing during its await, then calls openOptions()
  // again with the staff picker). Capturing focus/trapping again on that
  // second call recorded a node INSIDE the sheet body openOptions was
  // about to wipe via innerHTML above — closeOptions() later tried to
  // refocus a detached node, and silently fell through to <body>. Only a
  // sheet transitioning from closed to open needs a fresh focus snapshot
  // and trap; repopulating an already-open one keeps both.
  const wasAlreadyOpen = el.optionsSheet.classList.contains('is-open');
  el.optionsSheet.inert = false;
  el.optionsSheet.classList.add('is-open');
  el.optionsBackdrop.classList.add('is-open');
  if (!wasAlreadyOpen) {
    lastOptionsFocus = document.activeElement;
    trapBackgroundExcept(el.optionsSheet, el.optionsBackdrop);
  }
  window.setTimeout(() => {
    (el.optionsBody.querySelector('button, input, [tabindex]') || el.optionsConfirm)?.focus();
  }, 0);
}
