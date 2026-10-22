#!/bin/bash
# Her ham rapordaki dosya:satır alıntısının SHA'da var olduğunu denetler (dosya var ve satır <= satır sayısı).
cd /d/PROJECT/ALKAROS
total=0; ok=0; bad=0
for f in evidence/V1-RMD-599/agents/*.md; do
  grep -oE '[A-Za-z0-9_./-]+\.(cs|ts|tsx|js|sql|yaml|yml|md|sh|json|conf):[0-9]+' "$f" | sort -u | while IFS=: read -r p l; do
    echo "$p:$l"
  done
done | sort -u > /tmp/cites.txt
while IFS=: read -r p l; do
  total=$((total+1))
  full=$(git ls-files "*$p" | head -1)
  if [ -n "$full" ] && [ "$(wc -l < "$full")" -ge "$l" ]; then ok=$((ok+1)); else bad=$((bad+1)); echo "YOK: $p:$l"; fi
done < /tmp/cites.txt
echo "toplam=$total var=$ok yok=$bad"
