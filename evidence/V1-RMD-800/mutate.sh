#!/bin/sh
# usage: mutate.sh <label> <file> <old-file> <new-file> <test-project> <filter>
# old-file/new-file hold the exact text to replace; the source is restored with cp and verified with cmp.
L=$1; F=$2; OLD=$3; NEW=$4; P=$5; FILTER=$6
R=D:/PROJECT/ALKAROS; D=$R/evidence/V1-RMD-800/mutation-$L; mkdir -p "$D"
cp "$R/$F" "$D/original.bak"
python - "$R/$F" "$OLD" "$NEW" <<'PY'
import sys
p,o,n=sys.argv[1:4]
b=open(p,'rb').read().decode('utf-8'); old=open(o,'rb').read().decode('utf-8'); new=open(n,'rb').read().decode('utf-8')
assert b.count(old)==1, "mutation target not unique"
open(p,'wb').write(b.replace(old,new).encode('utf-8'))
PY
sh $R/evidence/V1-RMD-800/800-run.sh "mutation-$L/run" "$P" "$FILTER"
cp "$D/original.bak" "$R/$F"
cmp "$D/original.bak" "$R/$F" && echo "restored" > "$D/restored.txt"
tail -1 "$D/run/test.log" | cut -c1-160 > "$D/result.txt"
