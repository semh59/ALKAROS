#!/bin/bash
# usage: mutate.sh  (runs from repo root). Each mutant: backup, mutate, run the named test, restore, cmp.
cd /d/PROJECT/ALKAROS
OUT=evidence/V1-RMD-820/mutation-summary.txt; : > $OUT
SP=src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js
CS=src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js
F=src/Clients/PosTerminal/src/features/cash-session-status
run_static() { (cd tests/Clients/StaticApps && timeout 250 npx vitest run "$@" 2>&1 | grep -E "Tests " ); }
run_pos() { (cd src/Clients/PosTerminal && timeout 250 node_modules/.bin/vitest run "$@" 2>&1 | grep -E "Tests " ); }
m() { # id file old new runner testfile
  id=$1; file=$2; old=$3; new=$4; runner=$5; t=$6
  cp "$file" evidence/V1-RMD-820/mut/$id.bak
  python - "$file" "$old" "$new" <<'PY'
import sys
p,old,new=sys.argv[1:4]
s=open(p,encoding='utf-8',newline='').read()
assert old in s, 'pattern missing'
open(p,'w',encoding='utf-8',newline='').write(s.replace(old,new,1))
PY
  if [ $? -ne 0 ]; then echo "$id PATTERN-MISSING" >> $OUT; cp evidence/V1-RMD-820/mut/$id.bak "$file"; return; fi
  r=$($runner $t)
  cp evidence/V1-RMD-820/mut/$id.bak "$file"
  if cmp -s "$file" evidence/V1-RMD-820/mut/$id.bak; then c=restored; else c=NOT-RESTORED; fi
  echo "$id | $r | $c" >> $OUT
}
m M1 $CS "if (state.phase !== 'checking') {" "if (true) {" run_static cash-session-load-errors.test.js
m M2 $SP "result.status === 404 ? 'closed' : 'unknown'" "'closed'" run_static split-payment-cash-state.test.js
m M3 $SP "state.selectedMethod = null;" "state.selectedMethod = 'Eft';" run_static split-payment-cash-state.test.js
m M4 $SP "index.html\">Kas" "x\">Kas" run_static split-payment-cash-state.test.js
m M5 $SP "if (summaryReadsInFlight > 0) return;" "" run_static split-payment-refresh-guard.test.js
m M6 $SP "if (mine !== summaryReadSequence) return true;" "" run_static split-payment-refresh-guard.test.js
m M7 $SP "state.busy || state.summaryStale ||" "state.busy ||" run_static split-payment-refresh-guard.test.js
m M8 $SP "var FIELD_KEYS = ['data-refund-reason', 'data-refund-note', 'data-item-qty'," "var FIELD_KEYS = [" run_static split-payment-refresh-guard.test.js
m M9 $F/cashSessionStatusApi.ts '  return "unknown";
}' '  return "closed";
}' run_pos src/features/cash-session-status
m M10 $F/CashSessionStatus.tsx 'window.addEventListener("focus", refresh);' '' run_pos src/features/cash-session-status
m M11 src/Clients/PosTerminal/src/routes/Cashier.tsx 'session === "ready" && capabilities.includes("cash.drawer")' 'session === "ready"' run_pos src/features/cash-session-status
m M12 src/Clients/PosTerminal/src/routes/workspace.tsx '{cashSession && <CashSessionBand status={cashSession} />}' '' run_pos src/routes/workspace.test.tsx
m M13 $F/cashSessionStatusApi.ts 'if (response.status === 401) notifySessionExpired();' '' run_pos src/features/cash-session-status
cat $OUT
