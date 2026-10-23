import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const registry = "src/Host/DualScreen/HubSessionRegistry.cs";
const app = "src/Host/DualScreen/DualScreenApplication.cs";
const options = "src/Host/DualScreen/DualScreenOptions.cs";
const displayHub = "src/Host/DualScreen/CustomerDisplayHub.cs";
const waiterHub = "src/Host/Experience/WaiterNotifications/WaiterOrderStatusHub.cs";
const helpHub = "src/Host/Experience/HelpRequests/HelpRequestHub.cs";
const stockHub = "src/Host/Experience/InventoryReporting/LowStockAlertHub.cs";
const health = "src/Host/Experience/Observability/DatabaseHealthProbeHostedService.cs";
const jobs = "src/Host/Experience/SecurityAdministration/Maintenance/MaintenanceJobs.cs";
const psql = "src/Host/Composition/Migrations/PsqlScriptRunner.cs";
const program = "src/Host/Program.cs";
const connection = "src/Host/Experience/SecurityAdministration/HostDatabaseConnection.cs";
const receipt = "src/Host/DualScreen/DualScreenApplication.Receipt.cs";
const payments = "src/Host/DualScreen/DualScreenApplication.Payments.cs";
const credentials = "src/Modules/OnlineOrdering/Credentials/PostgresOnlinePlatformCredentialStore.cs";
const host = "tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj";
const suites = {
  acc: { project: host, filter: "FullyQualifiedName~AccessHardeningTests" },
  st: { project: host, filter: "FullyQualifiedName~TheCustomerDisplayHub" },
  prog: { project: host, filter: "FullyQualifiedName~ProgramArgumentTests" },
  waiter: { project: "tests/Host/Experience/WaiterNotifications/ALKAROS.Host.Experience.WaiterNotifications.Tests.csproj", filter: null },
  help: { project: "tests/Host/Experience/HelpRequests/ALKAROS.Host.Experience.HelpRequests.Tests.csproj", filter: "FullyQualifiedName~HelpRequestHubTests" },
  stock: { project: "tests/Host/Experience/InventoryReporting/ALKAROS.Host.Experience.InventoryReporting.Tests.csproj", filter: "FullyQualifiedName~LowStockAlertHubSessionTests" },
  health: { project: "tests/Host/Experience/Observability/ALKAROS.Host.Experience.Observability.Tests.csproj", filter: "FullyQualifiedName~DatabaseHealthProbeHostedServiceTests" },
  job: { project: "tests/Host/Experience/SecurityAdministration/ALKAROS.Host.Experience.SecurityAdministration.Tests.csproj", filter: "FullyQualifiedName~MaintenanceJobRunnerTests" },
  rcpt: { project: "tests/Host/Experience/PaymentTender/ALKAROS.Host.Experience.PaymentTender.Tests.csproj", filter: "FullyQualifiedName~BillReceiptHttpTests" },
  online: { project: "tests/Host/Experience/OnlineOrdering/ALKAROS.Host.Experience.OnlineOrdering.Tests.csproj", filter: "FullyQualifiedName~OnlinePlatformCredentialHttpTests" },
};
const mutants = [
  { id: "S1", suite: "acc", file: registry, from: "            entry.Context.Abort();\n", to: "" },
  { id: "S2", suite: "acc", file: registry, from: "            if (valid)\n                continue;", to: "            if (!valid)\n                continue;" },
  { id: "S3", suite: "acc", file: registry, from: "            _connections.TryRemove(connectionId, out _);\n            aborted++;", to: "            aborted++;" },
  { id: "S4", suite: "acc", file: registry, from: "exception is Npgsql.NpgsqlException or TimeoutException or InvalidOperationException", to: "exception is Npgsql.NpgsqlException or TimeoutException" },
  { id: "S5", suite: "acc", file: registry, from: "        catch (Exception exception) when (exception is not OperationCanceledException)", to: "        catch (OperationCanceledException exception) when (exception is not null)" },
  { id: "S6", suite: "acc", file: registry, from: "    public void Release(string connectionId) => _connections.TryRemove(connectionId, out _);", to: "    public void Release(string connectionId) => _connections.TryRemove(\"x\", out _);" },
  { id: "T1", suite: "st", file: displayHub, from: "        _sessions.Track(Context, async cancellationToken => await _store.AuthenticateDisplayAsync(rawToken, cancellationToken) is not null);\n", to: "" },
  { id: "T2", suite: "waiter", file: waiterHub, from: "        _sessions.Track(Context, async cancellationToken =>\n            await _store.AuthenticateCashierAsync(rawToken, terminalId, cancellationToken) is not null);\n", to: "" },
  { id: "T3", suite: "help", file: helpHub, from: "        _sessions.Track(Context, async cancellationToken => await ManagementSessionLookup.ResolveActorAsync(\n            _dataSource, rawToken, allowSupervisor: true, cancellationToken) is not null);\n", to: "" },
  { id: "T4", suite: "stock", file: stockHub, from: "        _sessions.Track(Context, async cancellationToken => await ManagementSessionLookup.ResolveActorAsync(\n            _dataSource, rawToken, allowSupervisor: true, cancellationToken) is not null);\n", to: "" },
  { id: "T5", suite: "help", file: helpHub, from: "        _recipients.Disconnected(Context.ConnectionId);\n        _sessions.Release(Context.ConnectionId);", to: "        _recipients.Disconnected(Context.ConnectionId);" },
  { id: "T6", suite: "stock", file: stockHub, from: "        _sessions.Release(Context.ConnectionId);\n", to: "" },
  { id: "R1", suite: "acc", file: app, from: "if (policy is null && context.Request.Path.StartsWithSegments(\"/api\"", to: "if (policy is not null && context.Request.Path.StartsWithSegments(\"/api\"" },
  { id: "R2", suite: "acc", file: app, from: "if (policy == QrOrderPolicyName)", to: "if (policy == \"unused\")" },
  { id: "R3", suite: "acc", file: app, from: "? Math.Min(configured, MaxLoginRateLimitPermits)", to: "? configured" },
  { id: "R4", suite: "acc", file: app, from: "FixedWindow($\"{ClientPartition(context)}:api-default\", apiPermits)", to: "FixedWindow($\"{ClientPartition(context)}:api-default\", apiPermits + 1)" },
  { id: "R5", suite: "acc", file: app, from: "FixedWindow($\"{ClientPartition(context)}:qr-order-address\", qrOrderPermits)", to: "FixedWindow($\"{ClientPartition(context)}:qr-order-address\", qrOrderPermits + 1)" },
  { id: "R6", suite: "acc", file: app, from: "rateLimiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>\n                GlobalPartition(context, options.ApiRateLimitPermits, options.QrOrderAddressPermits));\n", to: "" },
  { id: "H1", suite: "health", file: health, from: "error = exception.GetType().Name,", to: "error = exception.Message," },
  { id: "H2", suite: "health", file: health, from: "return (HealthStatus.Unhealthy, FailureDetails(ex));", to: "return (HealthStatus.Unhealthy, ex.Message);" },
  { id: "J1", suite: "job", file: jobs, from: "            LogManualRun(_logger, name, actorId, null);\n", to: "" },
  { id: "J2", suite: "job", file: jobs, from: "return RunAsync(name, actorId.ToString(\"D\"), cancellationToken);", to: "return RunAsync(name, MaintenanceRunContext.Timer, cancellationToken);" },
  { id: "J3", suite: "job", file: jobs, from: "                    scope.ServiceProvider.GetRequiredService<MaintenanceRunContext>().TriggeredBy = triggeredBy;\n", to: "" },
  { id: "P1", suite: "acc", file: psql, from: "&& uri.UserInfo.Split(':', 2) is [_, { Length: > 0 }];", to: "&& false;" },
  { id: "P2", suite: "prog", file: program, from: "        if (PsqlScriptRunner.UrlCarriesPassword(databaseUrl))", to: "        if (false && PsqlScriptRunner.UrlCarriesPassword(databaseUrl))" },
  { id: "P3", suite: "acc", file: psql, from: "startInfo.Environment[\"PGPASSWORD\"] = options.Password;", to: "startInfo.Environment[\"PGPASSWORD\"] = \"x\";" },
  { id: "O1", suite: "acc", file: options, from: "    public override string ToString() => $\"{nameof(DualScreenOptions)} {{ Url = {Url} }}\";", to: "" },
  { id: "O2", suite: "acc", file: connection, from: "    public override string ToString() => nameof(HostDatabaseConnection);", to: "" },
  { id: "O3", suite: "acc", file: psql, from: "    public override string ToString() => nameof(PsqlOptions);", to: "" },
  { id: "B1", suite: "rcpt", file: receipt, from: "await RequireCashierPermissionAsync(\n                    context, terminalId, store, authorization, ApplicationPermissions.PaymentsTake, cancellationToken);", to: "await RequireCashierAsync(context, terminalId, store, cancellationToken);" },
  { id: "B2", suite: "rcpt", file: payments, from: "            await RequireCashierPermissionAsync(\n                context, terminalId, store, authorization, ApplicationPermissions.PaymentsTake, cancellationToken);\n\n            var bill = await billRepository.GetByIdAsync(billId, cancellationToken);\n            if (bill is null)\n                return Results.NotFound();\n\n            var allocations", to: "            await RequireCashierAsync(context, terminalId, store, cancellationToken);\n\n            var bill = await billRepository.GetByIdAsync(billId, cancellationToken);\n            if (bill is null)\n                return Results.NotFound();\n\n            var allocations" },
  { id: "U1", suite: "online", file: credentials, from: "name.Contains('.', StringComparison.Ordinal)\n            && !name", to: "!name" },
  { id: "U2", suite: "online", file: credentials, from: "            && !name.EndsWith(\".internal\", StringComparison.OrdinalIgnoreCase)\n", to: "" },
  { id: "U3", suite: "online", file: credentials, from: "(bytes[0] == 172 && bytes[1] is >= 16 and <= 31)", to: "(bytes[0] == 172 && bytes[1] is >= 17 and <= 31)" },
  { id: "U4", suite: "online", file: credentials, from: "                || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)\n", to: "" },
  { id: "U5", suite: "online", file: credentials, from: "if (platform.Field(name)!.IsUrl && !IsPublicHost(new Uri(value).Host))", to: "if (platform.Field(name)!.IsUrl && false)" },
  { id: "U6", suite: "online", file: credentials, from: "address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address", to: "address" },
];
const only = process.argv[2] ? process.argv[2].split(",") : null;
const log = [];
for (const m of mutants) {
  if (only && !only.includes(m.id)) continue;
  const full = path.join(root, m.file);
  const orig = fs.readFileSync(full, "utf8");
  const crlf = orig.includes("\r\n");
  const normalized = crlf ? orig.replace(/\r\n/g, "\n") : orig;
  if (!normalized.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); console.log(log.at(-1)); continue; }
  fs.writeFileSync(full, crlf ? normalized.replace(m.from, () => m.to).replace(/\n/g, "\r\n") : normalized.replace(m.from, () => m.to));
  let out;
  try {
    const suite = suites[m.suite];
    const filter = suite.filter ? ` --filter "${suite.filter}"` : "";
    out = execSync(`"${process.env.USERPROFILE}/.dotnet/dotnet.exe" test ${suite.project}${filter}`, {
      cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 550000,
      env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" },
    });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  const verdict = /error CS|error CA/.test(out) ? "BUILD-ERROR" : /Başarısız!|FAILED/.test(out) ? "KILLED" : "SURVIVED";
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-696/mutations.log"), log.join("\n") + "\n");
