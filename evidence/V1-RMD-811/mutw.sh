#!/bin/bash
# usage: mutw.sh name file 'python-replace-old' 'new'
cd /d/PROJECT/ALKAROS/src/Clients/PosTerminal
n=$1; f=$2; old=$3; new=$4
cp "$f" /tmp/mut_orig.bak
python - "$f" "$old" "$new" <<'PY'
import sys
p,o,n=sys.argv[1:4]
b=open(p,'rb').read().decode('utf-8')
assert o in b,o
open(p,'wb').write(b.replace(o,n,1).encode('utf-8'))
PY
node_modules/.bin/vitest run src/routes/CustomerDisplay.test.tsx src/api.test.ts > /tmp/mut_$n.log 2>&1
r=$?
cp /tmp/mut_orig.bak "$f"
cmp "$f" /tmp/mut_orig.bak && echo "$n exit=$r (non-zero = killed) restored" | tee -a /d/PROJECT/ALKAROS/evidence/V1-RMD-811/mut/w2.result
