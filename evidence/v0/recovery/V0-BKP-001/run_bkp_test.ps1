$ErrorActionPreference = 'Stop'
$PGBIN = 'C:\PostgreSQL\18\bin'
$PGDATA = 'C:\Users\semih\AppData\Local\Temp\opencode\bkp_pg18'
$WORK = 'C:\Users\semih\AppData\Local\Temp\opencode\bkp_evidence'
New-Item -ItemType Directory -Force -Path $WORK | Out-Null
$log = "$WORK\transcript.txt"
"=== BKP-001 execution $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ===" | Out-File -FilePath $log -Encoding utf8

function T { param([string]$m) $m | Out-File -FilePath $log -Append -Encoding utf8; Write-Output $m }
function Run { param([string]$label, [string[]]$argsList)
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $exe = $argsList[0]
  $rest = $argsList | Select-Object -Skip 1
  $out = & $exe @rest 2>&1
  $code = $LASTEXITCODE; $sw.Stop()
  T "[$label] exit=$code elapsed=$($sw.Elapsed.TotalSeconds)s"
  $out | ForEach-Object { T "  $_" }
}

try {
  Run 'pg_ctl start' @("$PGBIN\pg_ctl.exe", '-D', $PGDATA, '-o', '-p 5433', '-l', "$WORK\pg.log", '-w', 'start')
  Start-Sleep -Seconds 2

  Run 'createdb seed' @("$PGBIN\createdb.exe", '-h', 'localhost', '-p', '5433', '-U', 'postgres', 'alkaros_bkp_seed')
  $seedSql = @'
CREATE TABLE verification_records (
  id INT PRIMARY KEY,
  code TEXT NOT NULL,
  amount NUMERIC(12,2) NOT NULL,
  occurred_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
INSERT INTO verification_records (id, code, amount) VALUES
  (1, 'V-0001', 12.50),
  (2, 'V-0002', 245.99),
  (3, 'V-0003', 0.00),
  (4, 'V-0004', 9999999.99);
'@
  $seedSql | & "$PGBIN\psql.exe" -h localhost -p 5433 -U postgres -d alkaros_bkp_seed -v ON_ERROR_STOP=1 2>&1 | ForEach-Object { T "  seed: $_" }
  T "seed-table-hash: $(& "$PGBIN\psql.exe" -h localhost -p 5433 -U postgres -d alkaros_bkp_seed -t -A -c "SELECT md5(string_agg(id || '|' || code || '|' || amount::text || '|' || occurred_at::text, ';' ORDER BY id)) FROM verification_records;")"
  T "seed-row-count: $(& "$PGBIN\psql.exe" -h localhost -p 5433 -U postgres -d alkaros_bkp_seed -t -A -c "SELECT count(*) FROM verification_records;")"

  Run 'pg_dump backup' @("$PGBIN\pg_dump.exe", '-h', 'localhost', '-p', '5433', '-U', 'postgres', '-Fc', '-f', "$WORK\alkaros_backup.dump", 'alkaros_bkp_seed')
  $hash = (Get-FileHash -Algorithm SHA256 "$WORK\alkaros_backup.dump").Hash
  T "artifact-sha256: $hash"
  T "artifact-size-bytes: $((Get-Item "$WORK\alkaros_backup.dump").Length)"

  Run 'createdb restore-target' @("$PGBIN\createdb.exe", '-h', 'localhost', '-p', '5433', '-U', 'postgres', 'alkaros_bkp_restore')
  Run 'pg_restore' @("$PGBIN\pg_restore.exe", '-h', 'localhost', '-p', '5433', '-U', 'postgres', '-d', 'alkaros_bkp_restore', "$WORK\alkaros_backup.dump")
  $rh = & "$PGBIN\psql.exe" -h localhost -p 5433 -U postgres -d alkaros_bkp_restore -t -A -c "SELECT md5(string_agg(id || '|' || code || '|' || amount::text || '|' || occurred_at::text, ';' ORDER BY id)) FROM verification_records;"
  $rc = & "$PGBIN\psql.exe" -h localhost -p 5433 -U postgres -d alkaros_bkp_restore -t -A -c "SELECT count(*) FROM verification_records;"
  T "restored-table-hash: $rh"
  T "restored-row-count: $rc"

  $corrupt = [IO.File]::ReadAllBytes("$WORK\alkaros_backup.dump")
  $corrupt[5000] = $corrupt[5000] -bxor 0xFF
  [IO.File]::WriteAllBytes("$WORK\alkaros_corrupt.dump", $corrupt)
  $chash = (Get-FileHash -Algorithm SHA256 "$WORK\alkaros_corrupt.dump").Hash
  T "corrupt-sha256: $chash"
  T "corrupt-checksum-mismatch: $($hash -ne $chash)"
  Run 'createdb corrupt-target' @("$PGBIN\createdb.exe", '-h', 'localhost', '-p', '5433', '-U', 'postgres', 'alkaros_bkp_corrupt')
  Run 'pg_restore corrupted' @("$PGBIN\pg_restore.exe", '-h', 'localhost', '-p', '5433', '-U', 'postgres', '-d', 'alkaros_bkp_corrupt', "$WORK\alkaros_corrupt.dump")

  T "=== DONE ==="
}
catch {
  T "SCRIPT-FAIL: $($_.Exception.Message)"
}
