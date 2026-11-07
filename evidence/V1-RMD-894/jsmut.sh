#!/bin/bash
# usage: jsmut.sh <id> <testfilter> <name> <file> <sed-expr>
id=$1; filter=$2; name=$3; file=$4; expr=$5
cd /d/PROJECT/ALKAROS
ev=evidence/V1-RMD-$id
cp "$file" "$ev/.orig.bak"
sed -i "$expr" "$file"
if cmp -s "$file" "$ev/.orig.bak"; then echo "$name: MUTATION NOT APPLIED" >> $ev/mutations.txt; rm "$ev/.orig.bak"; exit 1; fi
(cd tests/Clients/StaticApps && timeout 170 npx vitest run $filter > /d/PROJECT/ALKAROS/$ev/mut-$name.log 2>&1; echo "vitestexit=$?" >> /d/PROJECT/ALKAROS/$ev/mut-$name.log)
cp "$ev/.orig.bak" "$file"
if cmp "$file" "$ev/.orig.bak"; then R=restored-identical; rm "$ev/.orig.bak"; else R=RESTORE-FAILED; fi
echo "$name: $(grep -E 'Tests ' $ev/mut-$name.log | head -1) $(grep vitestexit $ev/mut-$name.log) $R" >> $ev/mutations.txt
