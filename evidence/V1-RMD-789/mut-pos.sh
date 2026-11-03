#!/bin/sh
# usage: mut-pos.sh <label> <old> <new>  - one mutation of models.ts, PosTerminal settings tests, cmp restore
L=$1; OLD=$2; NEW=$3
cd /d/PROJECT/ALKAROS
F=src/Clients/PosTerminal/src/features/management-settings-history/models.ts
D=evidence/V1-RMD-789/$L; mkdir -p "$D"
cp "$F" "$D/original.bak"
OLD="$OLD" NEW="$NEW" F="$F" python -c "
import os
p=os.environ['F']; s=open(p,encoding='utf-8',newline='').read()
old=os.environ['OLD']; new=os.environ['NEW']
assert s.count(old)==1, 'pattern count %d' % s.count(old)
open(p,'w',encoding='utf-8',newline='').write(s.replace(old,new))
" || { cp "$D/original.bak" "$F"; echo "$L: PATCH FAILED" | tee -a evidence/V1-RMD-789/mutation-summary.txt; exit 1; }
(cd src/Clients/PosTerminal && npx vitest run src/features/management-settings-history > "../../../$D/run.log" 2>&1)
cp "$D/original.bak" "$F"
cmp "$D/original.bak" "$F" && R=restored-identical
echo "$L: $(grep -E 'Tests ' $D/run.log | tr -s ' ') $R" | tee -a evidence/V1-RMD-789/mutation-summary.txt
