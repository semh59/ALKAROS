#!/bin/bash
cd /d/PROJECT/ALKAROS
D=evidence/V1-RMD-821; OUT=$D/mutation-summary.txt; : > $OUT
CT=src/Clients/PosTerminal/src/routes/Cashier.tsx
PC=src/Clients/PosTerminal/src/features/pending-checks
CA=src/Clients/Cashier/wwwroot/payments/customer-accounts/customer-accounts.js
run_static() { (cd tests/Clients/StaticApps && TZ=UTC timeout 250 npx vitest run "$@" 2>&1 | grep -E "Tests " ); }
run_pos() { (cd src/Clients/PosTerminal && TZ=UTC timeout 250 node_modules/.bin/vitest run "$@" 2>&1 | grep -E "Tests " ); }
m() { id=$1; file=$2; old=$3; new=$4; runner=$5; t=$6
  cp "$file" $D/mut/$id.bak
  python - "$file" "$old" "$new" <<'PY'
import sys
p,old,new=sys.argv[1:4]
s=open(p,encoding='utf-8',newline='').read()
assert old in s
open(p,'w',encoding='utf-8',newline='').write(s.replace(old,new,1))
PY
  if [ $? -ne 0 ]; then echo "$id PATTERN-MISSING" >> $OUT; cp $D/mut/$id.bak "$file"; return; fi
  r=$($runner $t)
  cp $D/mut/$id.bak "$file"
  if cmp -s "$file" $D/mut/$id.bak; then c=restored; else c=NOT-RESTORED; fi
  echo "$id | $r | $c" >> $OUT; }
m M1 $CT 'disabled={busy}
                onClick={() => {
                  if (product' 'disabled={busy || !activeOrder?.editable}
                onClick={() => {
                  if (product' run_pos src/routes/Cashier.first-touch.test.tsx
m M2 $CT 'if (addingProduct.current) return;' '' run_pos src/routes/Cashier.first-touch.test.tsx
m M3 $CT 'if (!reused) await reloadOrder().catch(() => undefined);' '' run_pos src/routes/Cashier.first-touch.test.tsx
m M4 $CT 'const reused = activeOrder?.editable ? activeOrder : null;' 'const reused = null;' run_pos src/routes/Cashier.first-touch.test.tsx
m M5 $PC/PendingChecksWorkspace.tsx ' && check.tableNumber !== "—"' '' run_pos src/features/pending-checks
m M6 $PC/pendingChecksApi.ts '    if (response.status === 401) notifySessionExpired();
    throw new PendingChecksApiError(
      response.status,' '    throw new PendingChecksApiError(
      response.status,' run_pos src/features/pending-checks
m M7 $PC/pendingChecksApi.ts 'recallFailureText[payload?.error?.code ?? ""] ?? "Hesap masaya geri gönderilemedi."' 'payload?.error?.code ?? "Hesap masaya geri gönderilemedi."' run_pos src/features/pending-checks
m M8 src/Clients/PosTerminal/src/format.ts 'minute: "2-digit", timeZone: venueTimeZone });

export const formatDateTime' 'minute: "2-digit" });

export const formatDateTime' run_pos src/format.test.ts
m M9 $CA "if (state.limitDraft.trim() === '') {" "if (false) {" run_static customer-accounts-flows.test.js
m M10 $CA '(!state.canManageCredit ?' '(false ?' run_static customer-accounts-flows.test.js
m M11 $CA "    state.error = null;
    render();
    // Kept across" "    state.error = null;
    // Kept across" run_static customer-accounts-flows.test.js
m M12 $CA "if (code && SERVER_TEXT_CODES.indexOf(code) !== -1 && body.error.message)" "if (body && body.error && body.error.message)" run_static customer-accounts-flows.test.js
m M13 $CA ", timeZone: 'Europe/Istanbul' });" " });" run_static customer-accounts-flows.test.js
m M14 src/Clients/Cashier/wwwroot/cashier-app.js ", timeZone: 'Europe/Istanbul' })," " })," run_static customer-accounts-flows.test.js
m M15 src/Clients/PosTerminal/src/routes/workspace.tsx 'setLastUpdated(formatClockTime(new Date()));' 'setLastUpdated(new Date().toLocaleTimeString("tr-TR"));' run_pos src/routes/workspace.test.tsx
cat $OUT
