export ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres
cd /d/PROJECT/ALKAROS
R=evidence/V1-RMD-599/runs
for p in tests/Architecture/ApiConventions tests/Architecture/ModuleBoundaries tests/Host/Errors tests/Host/Observability tests/Host/MigrationComposition; do
  n=$(echo $p | tr / -)
  csproj=$(ls $p/*.csproj | head -1)
  ~/.dotnet/dotnet.exe test $csproj > $R/test-$n.log 2>&1
  echo "$n exit=$?" >> $R/exit-codes.txt
done
