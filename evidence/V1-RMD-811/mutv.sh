#!/bin/sh
# usage: mutv.sh <label> <file> <old> <new> <test files...>
L=$1; F=$2; OLD=$3; NEW=$4; shift 4
R=D:/PROJECT/ALKAROS; M=$R/evidence/V1-RMD-811/mut; mkdir -p "$M"
cp "$R/$F" "$M/$L.orig"
python -I -c "
import sys
p,old,new=sys.argv[1:4]
b=open(p,'rb').read().decode('utf-8')
assert b.count(old)==1,'count %d'%b.count(old)
open(p,'wb').write(b.replace(old,new).encode('utf-8'))
" "$R/$F" "$OLD" "$NEW" || { cp "$M/$L.orig" "$R/$F"; rm -f "$M/$L.orig"; echo NOT-APPLIED; exit 2; }
(cd $R/src/Clients/PosTerminal && timeout 120 node_modules/.bin/vitest run "$@" > "$M/$L.log" 2>&1)
cp "$M/$L.orig" "$R/$F"; cmp "$M/$L.orig" "$R/$F" && rm -f "$M/$L.orig"
echo "$L: $(grep -E 'Tests ' $M/$L.log | tr -s ' ')"
