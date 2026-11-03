#!/bin/sh
cd /d/PROJECT/ALKAROS
J=src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
M="sh evidence/V1-RMD-789/mut.sh"
$M u1-no-reason-check $J "if (!notes) {" "if (false) {"
$M u2-threshold-ge $J "amount > threshold;" "amount >= threshold;"
$M u3-no-drawer-block $J "(state.busy || exceedsDrawer ? 'disabled' : '')" "(state.busy ? 'disabled' : '')"
$M u4-notes-not-in-key $J "JSON.stringify({ Direction: movementBody.Direction, Amount: movementBody.Amount, Notes: notes })" "JSON.stringify({ Direction: movementBody.Direction, Amount: movementBody.Amount })"
$M u5-no-draft-capture $J "    captureDrafts();
    if (state.phase === 'checking') {" "    if (state.phase === 'checking') {"
$M u6-zero-no-prompt $J "if (amount === 0) {" "if (false) {"
$M u7-own-approve-shown $J "(own && !state.movements.selfApprovalEnabled)" "(false)"
$M u8-approve-without-permission $J "return state.capabilities.indexOf('cash.session.override') >= 0;" "return true;"
$M u9-raw-message $J "if (code && ERROR_TEXT[code]) return ERROR_TEXT[code];" "if (body && body.error && body.error.message) return body.error.message;"
$M u10-close-no-reread $J "      loadActiveSession();
    });
  }

  function rereadExpectedCash" "      render();
    });
  }

  function rereadExpectedCash"
$M u11-close-enabled-without-expected $J "(state.busy || !hasExpected ? 'disabled' : '')" "(state.busy ? 'disabled' : '')"
$M u12-202-says-saved $J "state.notice = result.status === 202" "state.notice = false"
$M u13-no-self-approval-mark $J "(m.selfApproved ? 'kendi onayı' : 'onaylayan: ' + escapeHtml(m.approvedByName))" "('onaylayan: ' + escapeHtml(m.approvedByName))"
$M u14-no-list-reload $J "      state.phase = 'open';
      return loadMovements();" "      state.phase = 'open';
      render();"
$M n1-key-kept-after-decision $J "if (result.status < 500) state.cashMovementIdempotencyKey = null;" ""
$M n2-amount-not-in-key $J "Amount: movementBody.Amount, Notes: notes })" "Notes: notes })"
$M n3-direction-not-in-key $J "JSON.stringify({ Direction: movementBody.Direction, Amount" "JSON.stringify({ Amount"
$M n4-in-needs-approval $J "var needsApproval = !isIn && threshold" "var needsApproval = threshold"
$M n5-in-drawer-limit $J "var exceedsDrawer = !isIn && expected" "var exceedsDrawer = expected"
$M n6-request-reason-unescaped $J "'<div class=\"cs-note\">' + escapeHtml(r.reason)" "'<div class=\"cs-note\">' + r.reason"
$M n7-movement-note-unescaped $J "escapeHtml(m.notes)" "m.notes"
$M n8-summary-note-unescaped $J "escapeHtml(state.drafts.notes.trim()) + '</span></div>'" "state.drafts.notes.trim() + '</span></div>'"
$M n9-no-truncation-note $J "(state.movements.movementsTruncated ?" "(false ?"
$M n10-no-list-error $J "state.movementsError = !result.ok;" "state.movementsError = false;"
$M n11-no-decided-notice $J "(DECIDED_NOTICE[decided] || saved)" "saved"
$M n12-start-count-not-guarded $J "(state.busy || hasPending ? 'disabled' : '')" "(state.busy ? 'disabled' : '')"
$M n13-no-decision-confirm $J "if (action === 'cancel') {" "if (true) {"
$M n14-reopen-keeps-old-state $J "      resetForNewSession();
      state.closeResult = null;" "      state.closeResult = null;"
$M n15-open-keeps-old-state $J "        resetForNewSession();
        state.phase = 'open';" "        state.phase = 'open';"
$M n16-expected-network-stuck $J "        .catch(function () { return { ok: false }; })
" ""
$M n17-no-notice-on-closing $J "errorAlert() +
      noticeAlert() +
      '<div class=\"cs-summary-row\"><span>Beklenen</span>" "errorAlert() +
      '<div class=\"cs-summary-row\"><span>Beklenen</span>"
$M n18-denom-not-locked $J "type=\"number\" min=\"0\" step=\"1\" inputmode=\"numeric\"' + (state.zeroCountPrompt ? ' disabled' : '')" "type=\"number\" min=\"0\" step=\"1\" inputmode=\"numeric\"' + ('')"
$M n19-capture-always $J "if (renderedKey !== screenKey()) return;" ""
$M n20-toggle-loses-drafts $J "      captureDrafts();
      state.countMode =" "      state.countMode ="
$M n21-no-error-focus $J "if (alertBox) alertBox.focus({ preventScroll: true });" ""
$M n22-no-step-focus $J "if (lastFocusedPhase !== renderedKey) {" "if (lastFocusedPhase !== state.phase) {"
$M n23-same-button-label $J "(needsApproval ? 'Onaya gönder' : 'Onayla ve kaydet')" "'Onayla ve kaydet'"
$M n25-no-maybe-saved-text $J "Kayıt gitmiş olabilir; nakit hareketlerini kontrol edin, yoksa aynı işlemi tekrar deneyin." "x"
$M n26-no-maybe-decided-text $J "Karar gitmiş olabilir; nakit hareketlerini kontrol edin." "x"
$M n27-chip-no-fill $J "notes.value = chip.dataset.reason;" ""
$M n28-refresh-no-reload $J "loadMovements().then(function () { setBusy(false); });" "setBusy(false);"
echo done > evidence/V1-RMD-789/chain.done
