import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const up = "database/migrations/V1/V1-RMD-698/201-schema-consistency-and-lookup-indexes.up.sql";
const down = "database/migrations/V1/V1-RMD-698/201-schema-consistency-and-lookup-indexes.down.sql";
const store = "src/Host/DualScreen/DualScreenStore.cs";
const ssStore = "src/Host/DualScreen/DualScreenStore.Screensaver.cs";
const ssApp = "src/Host/DualScreen/DualScreenApplication.Screensaver.cs";
const manifest = "src/Host/Composition/Migrations/MigrationManifest.cs";
const attrs = ".gitattributes";
const tests = "tests/Host/MigrationComposition/Migrations/MigrationFileAndSchemaTests.cs";
const host = "tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj";
const suites = {
  schema: { project: host, filter: "FullyQualifiedName~MigrationSchemaTests|FullyQualifiedName~MigrationFileLineEndingTests" },
  screensaver: { project: host, filter: "FullyQualifiedName~DualScreenScreensaverTests" },
  catalog: { project: host, filter: "FullyQualifiedName~TheCatalogIsOrdered" },
  manifest: { project: host, filter: "FullyQualifiedName~ManifestTests" },
};
const mutants = [
  { id: "M1", suite: "schema", file: up, from: "        CHECK (content_type IN ('image/png', 'image/jpeg', 'image/webp', 'video/mp4')),\n", to: "" },
  { id: "M2", suite: "schema", file: up, from: "CHECK (octet_length(content) BETWEEN 1 AND 20971520);", to: "CHECK (octet_length(content) BETWEEN 1 AND 20971521);" },
  { id: "M3", suite: "schema", file: up, from: "ALTER COLUMN row_version TYPE BIGINT;", to: "ALTER COLUMN row_version TYPE INTEGER;" },
  { id: "M4", suite: "schema", file: up, from: "ADD CONSTRAINT pairing_requests_expiry_check CHECK (expires_at > created_at)", to: "ADD CONSTRAINT pairing_requests_expiry_check CHECK (expires_at >= created_at)" },
  { id: "M5", suite: "schema", file: up, from: "ADD CONSTRAINT display_sessions_expiry_check CHECK (expires_at > created_at)", to: "ADD CONSTRAINT display_sessions_expiry_check CHECK (expires_at > created_at - interval '1 day')" },
  { id: "M6", suite: "schema", file: up, from: "    WHERE active_order_id IS NOT NULL;", to: "    WHERE active_order_id IS NULL;" },
  { id: "M7", suite: "schema", file: up, from: "(source_reference_id, source_reference_type);", to: "(source_reference_type, source_reference_id);" },
  { id: "M8", suite: "schema", file: down, from: "DROP INDEX IF EXISTS customer_account.ix_account_transactions_source_reference;\n", to: "" },
  { id: "M9", suite: "schema", file: down, from: "ALTER COLUMN row_version TYPE INTEGER;", to: "ALTER COLUMN row_version TYPE BIGINT;" },
  { id: "M10", suite: "schema", file: attrs, from: "database/migrations/** -text", to: "database/migrations/** text" },
  { id: "M11", suite: "schema", file: tests, from: "        \"V11/V11-RCP-002/070-recipe-cost-snapshots.up.sql\",\n", to: "" },
  { id: "M12", suite: "catalog", file: store, from: "p.name COLLATE \"tr-TR-x-icu\",\n                      p.sku", to: "p.name COLLATE \"C\",\n                      p.sku" },
  { id: "M13", suite: "catalog", file: store, from: "                     p.name COLLATE \"tr-TR-x-icu\",\n                     p.sku COLLATE \"tr-TR-x-icu\",", to: "                     p.name COLLATE \"C\",\n                     p.sku COLLATE \"tr-TR-x-icu\","},
  { id: "M14", suite: "screensaver", file: ssApp, from: "Headers.IfNoneMatch.Contains(currentETag)", to: "Headers.IfNoneMatch.Contains(\"x\")" },
  { id: "M15", suite: "screensaver", file: ssStore, from: "public static string ETagOf(DateTimeOffset updatedAt) => $\"\\\"{updatedAt.UtcTicks:x}\\\"\";", to: "public static string ETagOf(DateTimeOffset updatedAt) => \"\\\"fixed\\\"\";" },
  { id: "M16", suite: "screensaver", file: ssStore, from: "\"SELECT updated_at FROM customer_display.screensaver_images WHERE id = 1;\"", to: "\"SELECT updated_at FROM customer_display.screensaver_images WHERE id = 1 AND octet_length(content) > 0;\"" },
  { id: "M17", suite: "manifest", file: manifest, from: "PhaseBMax = \"201\"", to: "PhaseBMax = \"200\"" },
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
    out = execSync(`"${process.env.USERPROFILE}/.dotnet/dotnet.exe" test ${suite.project} --filter "${suite.filter}"`, {
      cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 550000,
      env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" },
    });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  const verdict = /error CS|error CA/.test(out) ? "BUILD-ERROR" : /Başarısız!|FAILED/.test(out) ? "KILLED" : "SURVIVED";
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-698/mutations.log"), log.join("\n") + "\n");
