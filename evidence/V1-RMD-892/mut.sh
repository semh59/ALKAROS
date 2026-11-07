#!/bin/bash
# usage: mut.sh <id> <csproj> <filter> <name> <file> <sed-expr>
id=$1; proj=$2; filter=$3; name=$4; file=$5; expr=$6
cd /d/PROJECT/ALKAROS
ev=evidence/V1-RMD-$id
cp "$file" "$ev/.orig.bak"
sed -i "$expr" "$file"
if cmp -s "$file" "$ev/.orig.bak"; then echo "$name: MUTATION NOT APPLIED" >> $ev/mutations.txt; rm "$ev/.orig.bak"; exit 1; fi
/c/Users/semih/AppData/Local/Temp/claude/D--PROJECT-ALKAROS/a5c6723b-554e-46fe-8e41-06d38dd3db51/scratchpad/t.sh "$proj" "$ev/mut-$name.log" "$filter"
cp "$ev/.orig.bak" "$file"
if cmp "$file" "$ev/.orig.bak"; then R=restored-identical; rm "$ev/.orig.bak"; else R=RESTORE-FAILED; fi
echo "$name: $(grep -E 'Başarısız!|Başarılı!|error CS' $ev/mut-$name.log | head -1 | cut -c1-160) $(grep testexit $ev/mut-$name.log) $R" >> $ev/mutations.txt
