#!/bin/bash
# usage: mutw.sh name file old new
cd /d/PROJECT/ALKAROS/src/Clients/PosTerminal
n=$1; f=$2
cp "$f" /tmp/mut812.bak
python - "$f" "$3" "$4" <<'PY'
import sys
p,o,n=sys.argv[1:4]
b=open(p,'rb').read().decode('utf-8')
assert o in b,o
open(p,'wb').write(b.replace(o,n,1).encode('utf-8'))
PY
node_modules/.bin/vitest run src/routes/CustomerDisplay.test.tsx > /tmp/m812_$n.log 2>&1
r=$?
cp /tmp/mut812.bak "$f"; cmp "$f" /tmp/mut812.bak && echo "$n exit=$r (non-zero=killed) restored" | tee -a /d/PROJECT/ALKAROS/evidence/V1-RMD-812/mut/results.txt
