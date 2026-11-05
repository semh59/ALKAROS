#!/bin/sh
# usage: mutate.sh <label> <file> <old-file> <new-file> <test-project> <filter>
# old-file/new-file hold the exact text to replace; the source is restored with cp and verified with cmp.
L=$1; F=$2; OLD=$3; NEW=$4; P=$5; FILTER=$6
R=D:/PROJECT/ALKAROS; D=$R/evidence/V1-RMD-802/mutation-$L; mkdir -p "$D"
cp "$R/$F" "$D/original.bak"
python "$R/evidence/V1-RMD-802/replace_once.py" "$R/$F" "$OLD" "$NEW" || { cp "$D/original.bak" "$R/$F"; echo "no mutation applied" > "$D/result.txt"; exit 1; }
sh $R/evidence/V1-RMD-802/802-run.sh "mutation-$L/run" "$P" "$FILTER"
cp "$D/original.bak" "$R/$F"
cmp "$D/original.bak" "$R/$F" && echo "restored" > "$D/restored.txt"
tail -1 "$D/run/test.log" | cut -c1-160 > "$D/result.txt"
