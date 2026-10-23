import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const program = "src/Host/Program.cs";
const store = "src/Host/Experience/CustomerAccounts/CustomerAccountsStore.cs";
const endpoint = "src/Host/Experience/CustomerAccounts/DualScreenApplication.CustomerAccounts.cs";
const upSql = "database/migrations/V1/V1-RMD-692/200-handoff-note-uniqueness-and-retention-indexes.up.sql";
const hostTests = "tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj";
const accountTests = "tests/Host/Experience/CustomerAccounts/ALKAROS.Host.Experience.CustomerAccounts.Tests.csproj";
const suites = {
  hk: { project: hostTests, filter: "FullyQualifiedName~HousekeepingTests" },
  kv: { project: hostTests, filter: "FullyQualifiedName~KvkkRetentionTests" },
  db: { project: hostTests, filter: "FullyQualifiedName~HousekeepingTests" },
  ca: { project: accountTests, filter: null },
};
const mutants = [
  { id: "H1", suite: "hk", file: program, from: "            if (deleted < HousekeepingBatchSize)\n                return total;", to: "            return total;" },
  { id: "H2", suite: "hk", file: program, from: "\"notifications.serving_handoff_notes\",\n            \"created_at < now()", to: "\"notifications.serving_handoff_notes\",\n            \"created_at > now()" },
  { id: "H3", suite: "hk", file: program, from: "            + \"OR user_id IN (SELECT user_id FROM identity.users WHERE NOT active)\",", to: "            + \"\"," },
  { id: "H4", suite: "hk", file: program, from: "\"COALESCE(last_success_at, created_at) < now()", to: "\"last_success_at < now()" },
  { id: "H5", suite: "hk", file: program, from: "\"created_at < now() - make_interval(days => @days)\",\n            helpRequestDays);", to: "\"created_at < now() - make_interval(days => @days)\",\n            notificationDays);" },
  { id: "H6", suite: "hk", file: program, from: " OR (consumed_at IS NOT NULL AND consumed_at < now() - make_interval(days => @days))\",", to: "\"," },
  { id: "H7", suite: "hk", file: program, from: "$\"handoff_notes={handoffNotesDeleted} ", to: "$\"handoff_note={handoffNotesDeleted} " },
  { id: "H8", suite: "hk", file: program, from: "\"customer_display.display_sessions\",\n            \"expires_at < now() - make_interval(days => @days) OR (revoked_at IS NOT NULL AND revoked_at < now() - make_interval(days => @days))\",\n            graceDays);", to: "\"customer_display.display_sessions\",\n            \"expires_at < now() - make_interval(days => @days)\",\n            graceDays);" },
  { id: "H9", suite: "hk", file: program, from: "if (!int.TryParse(args[++i], out notificationDays) || notificationDays < 1)", to: "if (!int.TryParse(args[++i], out notificationDays) || notificationDays < 0)" },
  { id: "S1", suite: "kv", file: program, from: "password_hash = '!kvkk-retention-disabled', pin_hash = NULL,", to: "password_hash = '!kvkk-retention-disabled'," },
  { id: "S3", suite: "kv", file: program, from: "SET note = @m WHERE (from_user_id", to: "SET note = note WHERE (from_user_id" },
  { id: "S4", suite: "kv", file: program, from: "UPDATE notifications.serving_handoff_notes SET note = @m WHERE (from_user_id = @subject OR to_user_id = @subject)", to: "UPDATE notifications.serving_handoff_notes SET note = @m WHERE (from_user_id = @subject)" },
  { id: "S5", suite: "kv", file: program, from: "\"DELETE FROM notifications.push_subscriptions WHERE user_id = @subject;\"", to: "\"DELETE FROM notifications.push_subscriptions WHERE user_id = @subject AND FALSE;\"" },
  { id: "A1", suite: "ca", file: endpoint, from: "eventName: \"customer.tax_identity_changed\"", to: "eventName: \"customer.changed\"" },
  { id: "A2", suite: "ca", file: endpoint, from: "                        actorId: principal.UserId,\n", to: "" },
  { id: "A3", suite: "ca", file: endpoint, from: "kind = updated.PreviousKind", to: "kind = (string?)null" },
  { id: "A4", suite: "ca", file: endpoint, from: "kind = updated.NewKind", to: "kind = (string?)null" },
  { id: "A5", suite: "ca", file: endpoint, from: "actorId: principal.UserId,", to: "actorId: Guid.Empty," },
  { id: "A6", suite: "ca", file: store, from: "profile.TaxIdentity?.Kind.ToString(), taxIdentity?.Kind.ToString()", to: "taxIdentity?.Kind.ToString(), profile.TaxIdentity?.Kind.ToString()" },
  { id: "D1", suite: "db", file: upSql, from: "(newer.created_at, newer.handoff_note_id) > (n.created_at, n.handoff_note_id)", to: "(newer.created_at, newer.handoff_note_id) < (n.created_at, n.handoff_note_id)" },
  { id: "D2", suite: "db", file: upSql, from: "CREATE UNIQUE INDEX IF NOT EXISTS ux_serving_handoff_notes_one_pending", to: "CREATE INDEX IF NOT EXISTS ux_serving_handoff_notes_one_pending" },
  { id: "D3", suite: "db", file: upSql, from: "        PERFORM pg_advisory_xact_lock(hashtextextended(NEW.to_user_id::text, 692));\n", to: "" },
  { id: "D4", suite: "db", file: upSql, from: "    IF NEW.consumed_at IS NULL THEN", to: "    IF TRUE THEN" },
  { id: "D5", suite: "db", file: upSql, from: "FOREIGN KEY (table_id) REFERENCES table_mgmt.tables(table_id) ON DELETE RESTRICT;", to: "FOREIGN KEY (table_id) REFERENCES table_mgmt.tables(table_id) ON DELETE CASCADE;" },
  { id: "D6", suite: "db", file: upSql, from: "FOREIGN KEY (requested_by_user_id) REFERENCES identity.users(user_id) ON DELETE RESTRICT;", to: "FOREIGN KEY (requested_by_user_id) REFERENCES identity.users(user_id) ON DELETE CASCADE;" },
  { id: "D7", suite: "db", file: upSql, from: "    BEFORE INSERT ON notifications.serving_handoff_notes", to: "    BEFORE UPDATE ON notifications.serving_handoff_notes" },
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
fs.appendFileSync(path.join(root, "evidence/V1-RMD-692/mutations.log"), log.join("\n") + "\n");
