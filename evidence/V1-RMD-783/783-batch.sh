#!/bin/sh
# usage: 783-batch.sh <tag> <project-dir>... ; runs each project through 783-run.sh and prints the result line
T=$1; shift
for P in "$@"; do
  L=$T-$(echo "$P" | sed 's#^tests/##; s#/#_#g')
  sh D:/PROJECT/ALKAROS/evidence/V1-RMD-783/783-run.sh "$L" "$P" > /dev/null 2>&1
  echo "$L $(cat D:/PROJECT/ALKAROS/evidence/V1-RMD-783/$L/exit.txt) $(grep -E 'Başarısız!|Başarılı!' D:/PROJECT/ALKAROS/evidence/V1-RMD-783/$L/test.log | tail -1 | cut -c1-110)"
done
