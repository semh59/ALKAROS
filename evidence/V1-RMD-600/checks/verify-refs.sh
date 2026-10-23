#!/bin/sh
# findings.json içindeki file:line başvurularının dosya ve satır varlığını sınar.
cd "$(dirname "$0")/../../.." || exit 1
fail=0
grep -o '"[A-Za-z0-9_./-]*\.[a-z]*:[0-9]*"' evidence/V1-RMD-600/findings.json | tr -d '"' | while IFS=: read -r f l; do
  if [ ! -f "$f" ]; then echo "MISSING $f"; exit 1; fi
  n=$(wc -l < "$f")
  if [ "$l" -gt "$((n + 1))" ]; then echo "LINE-OUT-OF-RANGE $f:$l ($n)"; exit 1; fi
  echo "OK $f:$l"
done
echo "exit $?"
