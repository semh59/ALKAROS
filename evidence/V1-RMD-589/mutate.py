import shutil, subprocess, filecmp, os
R = "D:/PROJECT/ALKAROS"
STORE = f"{R}/src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs"
DISP = f"{R}/src/Host/Experience/KitchenOperations/KitchenPrintDispatchHostedService.cs"
ENDP = f"{R}/src/Host/Experience/KitchenOperations/KitchenOperationsEndpoints.cs"
REPO = f"{R}/src/Modules/Kitchen/Routing/PostgresPrinterRepository.cs"
PRN = f"{R}/src/Modules/Kitchen/Routing/Printer.cs"
TCP = f"{R}/src/Modules/Kitchen/PrintQueue/TcpEscPosPrinterTransport.cs"
HTTP = ("tests/Host/Experience/KitchenOperations", "FullyQualifiedName~APrinter|FullyQualifiedName~AReprintIsSent|FullyQualifiedName~EveryPrivate")
ROUTING = ("tests/Modules/Kitchen/Routing", None)
PQ = ("tests/Modules/Kitchen/PrintQueue", None)
NL = chr(10)
M = [
 ("S1 10/8 range", STORE, "b[0] == 10 ||", "b[0] == 11 ||", HTTP),
 ("S2 172 lower bound", STORE, "b[1] is >= 16 and <= 31", "b[1] is >= 17 and <= 31", HTTP),
 ("S3 172 upper bound", STORE, "b[1] is >= 16 and <= 31", "b[1] is >= 16 and <= 30", HTTP),
 ("S4 192.168 second octet", STORE, "b[0] == 192 && b[1] == 168", "b[0] == 192 && b[1] >= 168", HTTP),
 ("S5 192.168 first octet", STORE, "b[0] == 192 && b[1] == 168", "b[0] >= 192 && b[1] == 168", HTTP),
 ("S6 dotted count", STORE, "text.Count(c => c == '.') != 3", "text.Count(c => c == '.') > 3", HTTP),
 ("S7 port lower bound", STORE, "port is >= 1 and <= 65535", "port is >= 0 and <= 65535", HTTP),
 ("S8 port upper bound", STORE, "port is >= 1 and <= 65535", "port is >= 1 and <= 65536", HTTP),
 ("S9 profile case sensitive", STORE, "ignoreCase: false", "ignoreCase: true", HTTP),
 ("S10 Default clears profile", STORE, 'profileName == "Default"', 'profileName == "Defaultx"', HTTP),
 ("S11 replay ignores name", STORE, "existing.Name == name && ", "", HTTP),
 ("S12 replay ignores station", STORE, "existing.StationId == stationId && ", "", HTTP),
 ("S13 replay ignores address", STORE, "existing.IpAddress == ipAddress", "true", HTTP),
 ("S14 replay ignores port", STORE, "existing.Port == port && ", "", HTTP),
 ("S15 replay ignores profile", STORE, "existing.Profile == profile", "true", HTTP),
 ("S16 update drops profile", STORE, "request.Profile is null ? current.Profile : ParseProfile(request.Profile));", "ParseProfile(request.Profile));", HTTP),
 ("S17 update drops address", STORE, "request.IpAddress is null ? current.IpAddress : RequirePrivateAddress(request.IpAddress)", "current.IpAddress", HTTP),
 ("S18 reprint ignores profile", STORE, "payload, printer.Profile, cancellationToken", "payload, null, cancellationToken", HTTP),
 ("S19 dispatch ignores profile", DISP, "job.Payload, printer.Profile, ct", "job.Payload, null, ct", HTTP),
 ("S20 dto drops profile", STORE, "value.IpAddress, value.Port, value.Profile?.ToString()", "value.IpAddress, value.Port, null", HTTP),
 ("S21 create needs only read access", ENDP, "await authorizer.RequirePermissionAsync(" + NL + "                context, terminalId, RoutingMutationPermission, cancellationToken);" + NL + "            return Results.Ok(await store.CreatePrinterAsync", "await authorizer.RequireReadAsync(context, terminalId, cancellationToken);" + NL + "            return Results.Ok(await store.CreatePrinterAsync", HTTP),
 ("S22 repository drops profile", REPO, 'cmd.Parameters.AddWithValue("profile", (object?)printer.Profile?.ToString() ?? DBNull.Value);', 'cmd.Parameters.AddWithValue("profile", DBNull.Value);', HTTP),
 ("S23 repository reads no profile", REPO, "profile: reader.IsDBNull(8) ? null : Enum.Parse<PrinterProfile>(reader.GetString(8)));", "profile: null);", ROUTING),
 ("S24 entity drops profile", PRN, "        Profile = profile;\n", "", ROUTING),
 ("S25 deactivate drops profile", PRN, "updatedAt: timestamp ?? DateTimeOffset.UtcNow,\n            profile: Profile);", "updatedAt: timestamp ?? DateTimeOffset.UtcNow);", ROUTING),
 ("S26 transport ignores call profile", TCP, "profile ?? _profile", "_profile", PQ),
]
M = [m for m in M if m[0].split()[0] in os.environ.get("ONLY", m[0].split()[0]).split(",")]
env = dict(os.environ, PATH=os.path.expanduser("~/.dotnet") + os.pathsep + os.environ["PATH"], ALKAROS_TEST_PG_PORT="55432", ALKAROS_TEST_PG_PASSWORD="postgres")
files = {m[1] for m in M}
orig = {f: os.path.join(os.environ["TEMP"], "v589." + os.path.basename(f) + ".orig") for f in files}
for f in files:
    t = open(f, encoding="utf-8", newline="").read()
    shutil.copy(f, orig[f])
log = open(f"{R}/evidence/V1-RMD-589/mutations" + ("-rerun" if "ONLY" in os.environ else "") + ".log", "w", encoding="utf-8")
surv = 0
for name, f, a, b, (proj, flt) in M:
    text = open(orig[f], encoding="utf-8", newline="").read()
    nl = "\r\n" if "\r\n" in text else "\n"
    a = a.replace("\n", nl); b = b.replace("\n", nl)
    assert text.count(a) >= 1, name
    open(f, "w", encoding="utf-8", newline="").write(text.replace(a, b, 1))
    cmd = [os.path.expanduser("~/.dotnet/dotnet.exe"), "test", f"{R}/{proj}"] + (["--filter", flt] if flt else [])
    r = subprocess.run(cmd, capture_output=True, text=True, env=env, encoding="utf-8", errors="replace")
    out = r.stdout + r.stderr
    v = "COMPILE-ERROR" if "error CS" in out else ("KILLED" if r.returncode != 0 else "SURVIVED")
    surv += v == "SURVIVED"
    shutil.copy(orig[f], f)
    log.write(f"{name}: {v} restored={filecmp.cmp(f, orig[f], shallow=False)}\n"); log.flush()
log.write(f"survivors: {surv}\n")
