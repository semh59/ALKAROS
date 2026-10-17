import shutil, subprocess, filecmp, sys, os
root = "D:/PROJECT/ALKAROS"
src = f"{root}/src/Host/Observability/OutageWatchdog/OutageWatchdogHost.cs"
orig = os.path.join(os.environ["TEMP"], "OutageWatchdogHost.orig")
shutil.copy(src, orig)
text = open(orig, encoding="utf-8", newline="").read()
M = [
 ("M1 notice prefix", 'KRITIK: {notice.Layer}', 'ALARM: {notice.Layer}'),
 ("M2 accepted only when Sent", '.Outcome == NotificationOutcome.Sent', '.Outcome != NotificationOutcome.Sent'),
 ("M3 forwarded proto value", 'TryAddWithoutValidation("X-Forwarded-Proto", "https")', 'TryAddWithoutValidation("X-Forwarded-Proto", "http")'),
 ("M4 default interval", 'var seconds = 15;', 'var seconds = 16;'),
 ("M5 default failures", 'var failures = 2;', 'var failures = 3;'),
 ("M6 interval lower bound", 'if (seconds < 1 || failures < 1)', 'if (seconds < 0 || failures < 1)'),
 ("M7 failures lower bound", 'if (seconds < 1 || failures < 1)', 'if (seconds < 1 || failures < 0)'),
 ("M8 https scheme allowed", 'url.Scheme is not ("http" or "https")', 'url.Scheme is not "http"'),
 ("M9 utc conversion", 'notice.At.UtcDateTime', 'notice.At.DateTime'),
 ("M10 blank buffer", 'string.IsNullOrWhiteSpace(buffer)', 'buffer is null'),
 ("M11 alarm threshold ignored", 'options.FailuresBeforeAlarm);', '2);'),
 ("M12 buffer path ignored", 'new LocalFileBuffer(options.BufferPath)', 'new LocalFileBuffer(Path.GetTempFileName())'),
 ("M13 probe url ignored", 'HttpProbe(probeClient, options.HealthUrl)', 'HttpProbe(probeClient, new Uri("http://127.0.0.1:1/"))'),
 ("M14 missing value check", 'if (i + 1 >= args.Length)', 'if (i + 2 >= args.Length)'),
 ("M15 unknown option accepted", 'default: throw new ArgumentException($"Unknown option {args[i]}.");', 'default: break;'),
]
log = open(f"{root}/evidence/V1-RMD-585/mutations.log", "w", encoding="utf-8")
env = dict(os.environ, PATH=os.path.expanduser("~/.dotnet") + os.pathsep + os.environ["PATH"])
survivors = 0
for name, a, b in M:
    assert text.count(a) >= 1, name
    open(src, "w", encoding="utf-8", newline="").write(text.replace(a, b, 1))
    r = subprocess.run([os.path.expanduser("~/.dotnet/dotnet.exe"), "test", f"{root}/tests/Host/Observability/OutageWatchdog", "-v", "q"], capture_output=True, text=True, env=env, encoding="utf-8", errors="replace")
    out = r.stdout + r.stderr
    if "error CS" in out:
        verdict = "COMPILE-ERROR"
    elif r.returncode != 0:
        verdict = "KILLED"
    else:
        verdict = "SURVIVED"; survivors += 1
    shutil.copy(orig, src)
    same = filecmp.cmp(src, orig, shallow=False)
    last = [l for l in out.splitlines() if "Başarı" in l or "Passed" in l or "Failed" in l][-1:] 
    log.write(f"{name}: {verdict} restored={same} {last}\n"); log.flush()
log.write(f"survivors: {survivors}\n")
