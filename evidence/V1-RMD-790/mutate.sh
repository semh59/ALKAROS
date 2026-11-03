#!/bin/sh
# usage: mutate.sh <label> <file> <python-old> <python-new> <project-dir> <filter>
# Applies one textual mutation, runs the tests, restores the file byte for byte (cmp) and records the result.
L=$1; F=$2; OLD=$3; NEW=$4; P=$5; FILTER=$6
cd /d/PROJECT/ALKAROS
D=evidence/V1-RMD-790/$L; mkdir -p "$D"
cp "$F" "$D/original.bak"
OLD="$OLD" NEW="$NEW" F="$F" python -c "
import os
p=os.environ['F']; s=open(p,encoding='utf-8',newline='').read()
old=os.environ['OLD']; new=os.environ['NEW']
assert s.count(old)==1, 'pattern count %d' % s.count(old)
open(p,'w',encoding='utf-8',newline='').write(s.replace(old,new))
" || { cp "$D/original.bak" "$F"; echo "PATCH FAILED" > "$D/exit.txt"; exit 1; }
sh evidence/V1-RMD-790/790-run.sh "$L/run" "$P" "$FILTER"
cp "$D/original.bak" "$F"
cmp "$D/original.bak" "$F" && echo "restored-identical" > "$D/restore.txt"
echo "$L: $(cat $D/run/exit.txt) $(tail -1 $D/run/test.log | cut -c1-140)" | tee -a evidence/V1-RMD-790/mutation-summary.txt
