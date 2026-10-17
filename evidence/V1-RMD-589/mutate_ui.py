import shutil, subprocess, filecmp, os
R = "D:/PROJECT/ALKAROS/src/Clients/PosTerminal"
WS = f"{R}/src/features/kitchen-operations/KitchenOperationsWorkspace.tsx"
API = f"{R}/src/features/kitchen-operations/kitchenApi.ts"
MOD = f"{R}/src/features/kitchen-operations/models.ts"
ROUTE = f"{R}/src/routes/workspace.tsx"
M = [
 ("U1 default port", WS, "port: printer?.port ?? 9100", "port: printer?.port ?? 9101"),
 ("U2 default brand", WS, 'printer?.profile as KitchenPrinterProfile | null | undefined) ?? "Default"', 'printer?.profile as KitchenPrinterProfile | null | undefined) ?? "Epson"'),
 ("U3 new printer inactive", WS, "isActive: printer?.isActive ?? true", "isActive: printer?.isActive ?? false"),
 ("U4 add button without permission check", WS, '{canManageRouting && onSavePrinter && !printerForm && <Button', '{onSavePrinter && !printerForm && <Button'),
 ("U5 edit button without permission check", WS, '{canManageRouting && onSavePrinter && <Button data-telemetry-id="b.kitchen-operations.kitchenoperationsworkspace.26"', '{onSavePrinter && <Button data-telemetry-id="b.kitchen-operations.kitchenoperationsworkspace.26"'),
 ("U6 address not required", WS, "|| !printerForm.input.ipAddress.trim()", ""),
 ("U7 name not required", WS, "|| !printerForm.input.name.trim()", ""),
 ("U8 save does not close the form", WS, "setPrinterForm(null);\n      setPrinterFeedback({ tone: \"success\"", "setPrinterFeedback({ tone: \"success\""),
 ("U9 create uses PUT", API, 'method: "POST",\n        body: JSON.stringify({ name: input.name', 'method: "PUT",\n        body: JSON.stringify({ name: input.name'),
 ("U10 edit uses POST", API, 'method: "PUT",\n        body: JSON.stringify(input),', 'method: "POST",\n        body: JSON.stringify(input),'),
 ("U11 validation text", MOD, 'error.code === "VALIDATION_FAILED"', 'error.code === "VALIDATION_FAILEDx"'),
 ("U12 duplicate text", MOD, 'error.code === "DUPLICATE_RESOURCE"', 'error.code === "DUPLICATE_RESOURCEx"'),
 ("U13 forbidden text", MOD, "error.status === 403", "error.status === 404"),
 ("U14 page does not reload after save", ROUTE, "await client.savePrinter(printerId, input); await load();", "await client.savePrinter(printerId, input);"),
 ("U15 save handler ignores permission", ROUTE, "onSavePrinter={canManageRouting && client ?", "onSavePrinter={client ?"),
]
M = [m for m in M if m[0].split()[0] in os.environ.get("ONLY", m[0].split()[0]).split(",")]
files = {m[1] for m in M}
orig = {f: os.path.join(os.environ["TEMP"], "v589ui." + os.path.basename(f) + ".orig") for f in files}
for f in files:
    shutil.copy(f, orig[f])
log = open("D:/PROJECT/ALKAROS/evidence/V1-RMD-589/mutations-ui" + ("-rerun" if "ONLY" in os.environ else "") + ".log", "w", encoding="utf-8")
surv = 0
for name, f, a, b in M:
    text = open(orig[f], encoding="utf-8", newline="").read()
    nl = "\r\n" if "\r\n" in text else "\n"
    a = a.replace("\n", nl); b = b.replace("\n", nl)
    assert text.count(a) == 1, name
    open(f, "w", encoding="utf-8", newline="").write(text.replace(a, b, 1))
    r = subprocess.run("npx vitest run src/features/kitchen-operations src/routes", cwd=R, shell=True, capture_output=True, text=True, encoding="utf-8", errors="replace")
    v = "KILLED" if r.returncode != 0 else "SURVIVED"
    surv += v == "SURVIVED"
    shutil.copy(orig[f], f)
    log.write(f"{name}: {v} restored={filecmp.cmp(f, orig[f], shallow=False)}\n"); log.flush()
log.write(f"survivors: {surv}\n")
