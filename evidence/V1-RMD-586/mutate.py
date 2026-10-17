import shutil, subprocess, filecmp, os
root = "D:/PROJECT/ALKAROS/src/Clients/PosTerminal"
F = {
 "m": f"{root}/src/features/kitchen-operations/models.ts",
 "k": f"{root}/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx",
 "s": f"{root}/src/features/system-health/SystemHealthWorkspace.tsx",
}
orig = {k: os.path.join(os.environ["TEMP"], f"v586.{k}.orig") for k in F}
for k in F:
    t = open(F[k], encoding="utf-8").read()
    assert "false &&" not in t and "120_000" not in t and "!isHealth" not in t, k
for k in F: shutil.copy(F[k], orig[k])
M = [
 ("M1 boundary exclusive", "m", "now - captured > HEALTH", "now - captured >= HEALTH"),
 ("M2 unreadable date not stale", "m", "Number.isNaN(captured) || now", "false || now"),
 ("M3 threshold", "m", "180_000", "120_000"),
 ("M4 unreadable message", "m", "return Number.isNaN(captured)\n    ?", "return false\n    ?"),
 ("M5 age sign", "m", "now - captured > HEALTH", "captured - now > HEALTH"),
 ("M6 system-health marker off", "s", "health && isHealthSnapshotStale(health.capturedAt) &&", "health && false &&"),
 ("M7 kitchen marker off", "k", "health && isHealthSnapshotStale(health.capturedAt) &&", "health && false &&"),
 ("M8 system-health inverted", "s", "health && isHealthSnapshotStale(health.capturedAt) &&", "health && !isHealthSnapshotStale(health.capturedAt) &&"),
 ("M9 kitchen inverted", "k", "health && isHealthSnapshotStale(health.capturedAt) &&", "health && !isHealthSnapshotStale(health.capturedAt) &&"),
 ("M10 title text", "m", "", ""),
]
M = M[:-1]
log = open("D:/PROJECT/ALKAROS/evidence/V1-RMD-586/mutations.log", "w", encoding="utf-8")
surv = 0
for name, key, a, b in M:
    text = open(orig[key], encoding="utf-8", newline="").read()
    nl = "\r\n" if "\r\n" in text else "\n"
    a = a.replace("\n", nl)
    assert a in text, name
    open(F[key], "w", encoding="utf-8", newline="").write(text.replace(a, b.replace("\n", nl), 1))
    r = subprocess.run("npx vitest run src/features/kitchen-operations src/features/system-health", cwd=root, shell=True, capture_output=True, text=True, encoding="utf-8", errors="replace")
    v = "KILLED" if r.returncode != 0 else "SURVIVED"
    surv += v == "SURVIVED"
    shutil.copy(orig[key], F[key])
    log.write(f"{name}: {v} restored={filecmp.cmp(F[key], orig[key], shallow=False)}\n"); log.flush()
log.write(f"survivors: {surv}\n")
