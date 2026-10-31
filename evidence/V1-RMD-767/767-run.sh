#!/bin/sh
# usage: 767-run.sh <label>
L=$1; D=D:/PROJECT/ALKAROS/evidence/V1-RMD-767/$L; mkdir -p "$D"
"C:/Users/semih/.dotnet/dotnet.exe" test D:/PROJECT/ALKAROS/tests/BuildingBlocks/Security/SensitiveData --results-directory "$D" --logger "trx" > "$D/test.log" 2>&1
echo "exit=$?" > "$D/exit.txt"
