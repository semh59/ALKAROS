import shutil, subprocess, filecmp, os
root = "D:/PROJECT/ALKAROS"
F = {"s": f"{root}/src/Host/Observability/ServerDetectors/ServerDetectors.cs",
     "e": f"{root}/src/Modules/Observability/Foundation/HealthCheckEnums.cs"}
orig = {k: os.path.join(os.environ["TEMP"], f"v587.{k}.orig") for k in F}
for k in F:
    t = open(F[k], encoding="utf-8").read()
    assert "ELSE 0" not in t and "$1 +" not in t and "if (true)" not in t, k
    shutil.copy(F[k], orig[k])
S1 = 'new("operations.system_health_snapshots", "captured_at", TelemetryRetention.RawEventDays),'
S2 = 'new("observability.notification_queue", "enqueued_at", TelemetryRetention.RawEventDays),'
S3 = 'new("observability.notification_suppressions", "last_sent_at", TelemetryRetention.RawEventDays),'
M = [
 ("M1 snapshots not purged", "s", S1, ""),
 ("M2 queue not purged", "s", S2, ""),
 ("M3 suppressions not purged", "s", S3, ""),
 ("M4 snapshots kept 395 days", "s", S1, S1.replace("RawEventDays", "HourlySummaryDays")),
 ("M5 queue kept 90 days", "s", S2, S2.replace("RawEventDays", "ErrorEventDays")),
 ("M6 health cutoff sign", "s", "$1 - make_interval", "$1 + make_interval"),
 ("M7 health purge skipped", "s", "await PurgeHealthChecksAsync(now, cancellationToken)", "0"),
 ("M8 unknown policy deleted at once", "s", "$\"ELSE {RetentionPolicyCatalog.RetentionDays.Values.Max()}\"", "\"ELSE 0\""),
 ("M9 hot policy 8 days", "e", "[HotOperational7D] = 7", "[HotOperational7D] = 8"),
 ("M10 no case folding", "s", "upper(trim(retention_policy_id))", "trim(retention_policy_id)"),
 ("M11 no trim", "s", "upper(trim(retention_policy_id))", "upper(retention_policy_id)"),
 ("M12 only first batch", "s", "if (affected < batchSize)", "if (true)"),
 ("M13 audit policy 91 days", "e", "[ComplianceAudit90D] = 90", "[ComplianceAudit90D] = 91"),
]
log = open(f"{root}/evidence/V1-RMD-587/mutations.log", "w", encoding="utf-8")
env = dict(os.environ, PATH=os.path.expanduser("~/.dotnet") + os.pathsep + os.environ["PATH"], ALKAROS_TEST_PG_PORT="55432", ALKAROS_TEST_PG_PASSWORD="postgres")
surv = 0
for name, key, a, b in M:
    text = open(orig[key], encoding="utf-8", newline="").read()
    assert a in text, name
    open(F[key], "w", encoding="utf-8", newline="").write(text.replace(a, b, 1))
    r = subprocess.run([os.path.expanduser("~/.dotnet/dotnet.exe"), "test", f"{root}/tests/Host/Observability/ServerDetectors"], capture_output=True, text=True, env=env, encoding="utf-8", errors="replace")
    out = r.stdout + r.stderr
    v = "COMPILE-ERROR" if "error CS" in out else ("KILLED" if r.returncode != 0 else "SURVIVED")
    surv += v == "SURVIVED"
    shutil.copy(orig[key], F[key])
    log.write(f"{name}: {v} restored={filecmp.cmp(F[key], orig[key], shallow=False)}\n"); log.flush()
log.write(f"survivors: {surv}\n")
