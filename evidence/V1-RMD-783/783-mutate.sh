#!/bin/sh
# usage: 783-mutate.sh <label> <source-file> <sed-expression> <test-project-dir> [test-filter]
# Applies one sed mutation, runs the tests, restores the source from a copy and checks the hash is the original one.
L=$1; F=$2; EXPR=$3; P=$4; FILTER=$5
ROOT=D:/PROJECT/ALKAROS
D=$ROOT/evidence/V1-RMD-783/mutation-$L; mkdir -p "$D"
BAK=$D/original.bak
cp "$ROOT/$F" "$BAK"
BEFORE=$(sha256sum "$ROOT/$F" | cut -d' ' -f1)
sed -i "$EXPR" "$ROOT/$F"
if [ "$(sha256sum "$ROOT/$F" | cut -d' ' -f1)" = "$BEFORE" ]; then cp "$BAK" "$ROOT/$F"; rm -f "$BAK"; echo "mutation $L did not change the file" > "$D/exit.txt"; echo "$L NOT-APPLIED"; exit 2; fi
diff "$BAK" "$ROOT/$F" > "$D/mutation.diff"
sh "$ROOT/evidence/V1-RMD-783/783-run.sh" "mutation-$L" "$P" "$FILTER" > /dev/null 2>&1
cp "$BAK" "$ROOT/$F"
if [ "$(sha256sum "$ROOT/$F" | cut -d' ' -f1)" = "$BEFORE" ]; then echo "restored=hash-identical $BEFORE" >> "$D/exit.txt"; else echo RESTORE-FAILED >> "$D/exit.txt"; fi
rm -f "$BAK"
echo "$L $(tr '\n' ' ' < "$D/exit.txt") $(grep -E 'Başarısız!|Başarılı!' "$D/test.log" | tail -1 | cut -c1-90) errors=$(grep -c 'error CS' "$D/test.log")"
