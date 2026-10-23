import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const catalog = "src/Host/Composition/Errors/ApiErrorCatalog.cs";
const handler = "src/Host/Composition/Errors/ApiExceptionHandler.cs";
const handling = "src/Host/Composition/Errors/ApiErrorHandling.cs";
const app = "src/Host/DualScreen/DualScreenApplication.cs";
const payments = "src/Host/DualScreen/DualScreenApplication.Payments.cs";
const store = "src/Host/DualScreen/DualScreenStore.cs";
const categoryRepo = "src/Modules/Catalog/ProductCatalog/PostgresCategoryRepository.cs";
const productRepo = "src/Modules/Catalog/ProductCatalog/PostgresProductRepository.cs";
const roleRepo = "src/Modules/Identity/Authorization/PostgresRoleRepository.cs";
const suites = {
  err: { project: "tests/Host/Errors/ALKAROS.Host.Errors.Tests.csproj", filter: null },
  host: { project: "tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj", filter: "FullyQualifiedName~DualScreenApiOnlyOriginTests" },
  st: { project: "tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj", filter: "FullyQualifiedName~DualScreenStoreTests" },
  pay: { project: "tests/Host/Experience/PaymentTender/ALKAROS.Host.Experience.PaymentTender.Tests.csproj", filter: null },
  cat: { project: "tests/Modules/Catalog/ProductCatalog/ALKAROS.Catalog.ProductCatalog.Tests.csproj", filter: null },
  idn: { project: "tests/Modules/Identity/Authorization/ALKAROS.Identity.Authorization.Tests.csproj", filter: null },
};
const mutants = [
  { id: "E1", suite: "err", file: catalog, from: "(StatusCodes.Status404NotFound, \"NOT_FOUND\", \"İstenen katalog kaydı bulunamadı.\")", to: "(StatusCodes.Status409Conflict, \"NOT_FOUND\", \"İstenen katalog kaydı bulunamadı.\")" },
  { id: "E2", suite: "err", file: catalog, from: "            typeof(CatalogConcurrencyException),\n", to: "" },
  { id: "E3", suite: "err", file: catalog, from: "state.StartsWith(\"08\", StringComparison.Ordinal)", to: "state.StartsWith(\"0X\", StringComparison.Ordinal)" },
  { id: "E4", suite: "err", file: catalog, from: "state.StartsWith(\"53\", StringComparison.Ordinal)", to: "state.StartsWith(\"5X\", StringComparison.Ordinal)" },
  { id: "E5", suite: "err", file: catalog, from: "state.StartsWith(\"57P0\", StringComparison.Ordinal)", to: "state.StartsWith(\"57X0\", StringComparison.Ordinal)" },
  { id: "E6", suite: "err", file: catalog, from: "state.StartsWith(\"58\", StringComparison.Ordinal)", to: "state.StartsWith(\"5Y\", StringComparison.Ordinal)" },
  { id: "E7", suite: "err", file: catalog, from: "            NpgsqlException => true,", to: "            NpgsqlException => false," },
  { id: "E8", suite: "err", file: catalog, from: "            typeof(LastRoleManagerException),\n", to: "" },
  { id: "E9", suite: "err", file: catalog, from: "            405 => (\"METHOD_NOT_ALLOWED\"", to: "            405 => (\"METHOD_NOT_ALLOWE\"" },
  { id: "R1", suite: "host", file: handler, from: "DatabaseRetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);", to: "(DatabaseRetryAfterSeconds + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);" },
  { id: "R2", suite: "host", file: handler, from: "if (status == StatusCodes.Status503ServiceUnavailable)", to: "if (status == StatusCodes.Status500InternalServerError)" },
  { id: "R3", suite: "err", file: handling, from: "        application.UseStatusCodePages(WriteStatusEnvelopeAsync);\n", to: "" },
  { id: "R4", suite: "err", file: handling, from: "var (code, message) = ApiErrorCatalog.StatusAnswer(status);", to: "var (code, message) = ApiErrorCatalog.StatusAnswer(500);" },
  { id: "M1", suite: "host", file: app, from: "|| template.Contains(\"{**path}\", StringComparison.Ordinal)", to: "" },
  { id: "M2", suite: "host", file: app, from: "context.Response.Headers.Allow = string.Join(\", \", allowed);", to: "" },
  { id: "M3", suite: "host", file: app, from: "if (allowed.Count == 0 || allowed.Contains(context.Request.Method))", to: "if (allowed.Count != 0)" },
  { id: "M5", suite: "host", file: app, from: " || allowed.Contains(context.Request.Method))", to: ")" },
  { id: "M4", suite: "host", file: app, from: "allowed.UnionWith(methods.HttpMethods);", to: "allowed.Add(\"GET\");" },
  { id: "S1", suite: "st", file: store, from: "            throw new DualScreenConflictException(\"Draft order item can no longer be changed.\");", to: "            throw new DualScreenNotFoundException(\"Draft order item can no longer be changed.\");" },
  { id: "S2", suite: "st", file: store, from: "        catch (ArgumentException)\n        {\n            throw new DualScreenNotFoundException", to: "        catch (ArgumentException)\n        {\n            throw new DualScreenConflictException" },
  { id: "P1", suite: "pay", file: payments, from: "amount.ToString(\"N2\", System.Globalization.CultureInfo.GetCultureInfo(\"tr-TR\"))", to: "amount.ToString(\"N2\", System.Globalization.CultureInfo.InvariantCulture)" },
  { id: "P2", suite: "pay", file: payments, from: "TenderMethod.MealCard => \"Yemek kartı\",", to: "TenderMethod.MealCard => \"MealCard\"," },
  { id: "P3", suite: "pay", file: payments, from: "nameof(PaymentStatus.ReconciliationRequired) => \"Mutabakat bekliyor\",", to: "" },
  { id: "P4", suite: "pay", file: payments, from: "nameof(PaymentStatus.Unknown) => \"Sonucu bilinmiyor\",", to: "" },
  { id: "P5", suite: "pay", file: payments, from: "public static string PaymentStatusText(string status)\n        => status switch", to: "public static string PaymentStatusText(string status)\n        => status.Length > 0 ? status : status switch" },
  { id: "P6", suite: "pay", file: payments, from: "message = $\"'{TenderMethodText(notRegistered.Method)}' ödeme yöntemi için", to: "message = $\"'{notRegistered.Method}' ödeme yöntemi için" },
  { id: "C1", suite: "cat", file: categoryRepo, from: "throw new CatalogRecordNotFoundException($\"Category {category.Id} not found.\");", to: "throw new InvalidOperationException($\"Category {category.Id} not found.\");" },
  { id: "C2", suite: "cat", file: productRepo, from: "throw new CatalogConcurrencyException(", to: "throw new InvalidOperationException(" },
  { id: "I1", suite: "idn", file: roleRepo, from: "throw new RoleManagementConflictException($\"Username '{username}' already exists.\");", to: "throw new InvalidOperationException($\"Username '{username}' already exists.\");" },
  { id: "I2", suite: "idn", file: roleRepo, from: "throw new RoleManagementConflictException($\"Role '{role.Code}' already exists.\", ex);", to: "throw new InvalidOperationException($\"Role '{role.Code}' already exists.\", ex);" },
  { id: "I3", suite: "idn", file: "src/Modules/Identity/Authorization/RoleManagementService.cs", from: "throw new RoleManagementConflictException($\"Role '{code}' already exists.\");", to: "throw new InvalidOperationException($\"Role '{code}' already exists.\");" },
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
fs.appendFileSync(path.join(root, "evidence/V1-RMD-693/mutations.log"), log.join("\n") + "\n");
