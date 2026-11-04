#!/bin/sh
# usage: mutate.sh <label> <file> <old> <new> <dir> <vitest-args>
L=$1; F=$2; OLD=$3; NEW=$4; P=$5; ARGS=$6
cd /d/PROJECT/ALKAROS
D=evidence/V1-RMD-795/mut/$L; mkdir -p "$D"
cp "$F" "$D/original.bak"
OLD="$OLD" NEW="$NEW" F="$F" python -c "
import os
p=os.environ['F']; s=open(p,encoding='utf-8',newline='').read()
old=os.environ['OLD']; new=os.environ['NEW']
assert s.count(old)==1, 'pattern count %d' % s.count(old)
open(p,'w',encoding='utf-8',newline='').write(s.replace(old,new))
" || { cp "$D/original.bak" "$F"; echo "$L: PATCH FAILED" | tee -a evidence/V1-RMD-795/mutation-summary.txt; exit 1; }
( cd "$P" && corepack pnpm exec vitest run $ARGS > /d/PROJECT/ALKAROS/$D/test.log 2>&1; echo $? > /d/PROJECT/ALKAROS/$D/exit.txt )
cp "$D/original.bak" "$F"
cmp "$D/original.bak" "$F" && echo ok > "$D/restore.txt"
echo "$L: exit=$(cat $D/exit.txt) $(grep -E 'Tests ' $D/test.log | tail -1)" | tee -a evidence/V1-RMD-795/mutation-summary.txt
