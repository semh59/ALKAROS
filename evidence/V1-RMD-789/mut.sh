#!/bin/sh
# usage: mut.sh <label> <file> <old> <new>  - one textual mutation, vitest, byte-for-byte restore (cmp)
L=$1; F=$2; OLD=$3; NEW=$4
cd /d/PROJECT/ALKAROS
D=evidence/V1-RMD-789/$L; mkdir -p "$D"
cp "$F" "$D/original.bak"
OLD="$OLD" NEW="$NEW" F="$F" python -c "
import os
p=os.environ['F']; s=open(p,encoding='utf-8',newline='').read()
old=os.environ['OLD']; new=os.environ['NEW']
assert s.count(old)==1, 'pattern count %d' % s.count(old)
open(p,'w',encoding='utf-8',newline='').write(s.replace(old,new))
" || { cp "$D/original.bak" "$F"; echo "$L: PATCH FAILED" | tee -a evidence/V1-RMD-789/mutation-summary.txt; exit 1; }
(cd tests/Clients/StaticApps && npx vitest run cash-session-movement > "../../../$D/run.log" 2>&1)
cp "$D/original.bak" "$F"
cmp "$D/original.bak" "$F" && R=restored-identical
echo "$L: $(grep -E 'Tests ' $D/run.log | tr -s ' ') $R" | tee -a evidence/V1-RMD-789/mutation-summary.txt
