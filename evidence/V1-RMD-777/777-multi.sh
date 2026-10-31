#!/bin/sh
# usage: 777-multi.sh <test-project-dir>... ; label = dir path with / replaced by _
for P in "$@"; do
  L=$(echo "$P" | sed 's#^tests/##; s#/#_#g')
  sh D:/PROJECT/ALKAROS/evidence/V1-RMD-777/777-run.sh "$L" "$P"
  echo "$P $(cat D:/PROJECT/ALKAROS/evidence/V1-RMD-777/$L/exit.txt) $(grep -E 'Başarısız!|Başarılı!' D:/PROJECT/ALKAROS/evidence/V1-RMD-777/$L/test.log | tail -1 | sed 's/ - ALKAROS.*//')"
done
