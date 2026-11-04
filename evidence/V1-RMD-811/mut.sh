#!/bin/sh
# usage: mut.sh <label> <file> <old> <new> <test-project-dir> <filter>
# Applies one mutation, runs the tests, always restores the original and verifies it with cmp.
L=$1; F=$2; OLD=$3; NEW=$4; P=$5; FILTER=$6
R=D:/PROJECT/ALKAROS
M=$R/evidence/V1-RMD-811/mut; mkdir -p "$M"
cp "$R/$F" "$M/$L.orig"
python -I -c "
import sys
p,old,new=sys.argv[1:4]
b=open(p,'rb').read().decode('utf-8')
assert b.count(old)==1, 'pattern count %d' % b.count(old)
open(p,'wb').write(b.replace(old,new).encode('utf-8'))
" "$R/$F" "$OLD" "$NEW" || { cp "$M/$L.orig" "$R/$F"; echo "MUTATION NOT APPLIED" > "$M/$L.result"; exit 2; }
sh $R/evidence/V1-RMD-811/run.sh "mut-$L" "$P" "$FILTER"
cp "$M/$L.orig" "$R/$F"
cmp "$M/$L.orig" "$R/$F" && echo "restored-ok" > "$M/$L.restore"; rm -f "$M/$L.orig"
echo "$(cat $R/evidence/V1-RMD-811/mut-$L/exit.txt) $(grep -E 'Başarılı!|Başarısız!' $R/evidence/V1-RMD-811/mut-$L/test.log | cut -c1-120) $(grep -c 'error CS' $R/evidence/V1-RMD-811/mut-$L/test.log) compile-errors" > "$M/$L.result"
cat "$M/$L.result"
