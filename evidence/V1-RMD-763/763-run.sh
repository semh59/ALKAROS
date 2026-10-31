#!/bin/sh
# usage: 763-run.sh <label>
L=$1; D=D:/PROJECT/ALKAROS/evidence/V1-RMD-763/$L; mkdir -p "$D"
PW=$(docker inspect alkaros-test-pg --format '{{json .Config.Env}}' | python -c "import json,sys;print(next(e.split('=',1)[1] for e in json.load(sys.stdin) if e.startswith('POSTGRES_PASSWORD=')))")
export ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=$PW
"C:/Users/semih/.dotnet/dotnet.exe" test D:/PROJECT/ALKAROS/tests/Modules/Identity/Authentication --results-directory "$D" --logger "trx" > "$D/test.log" 2>&1
echo "exit=$?" > "$D/exit.txt"
sed -i "s/$PW/[REDACTED]/g" "$D/test.log"
