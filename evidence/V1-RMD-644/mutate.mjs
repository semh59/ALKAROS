// Mutation run for V1-RMD-644: apply one mutant, run the Audit tests, restore, verify identical.
import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { isDeepStrictEqual } from 'node:util';

const ROOT = 'D:/PROJECT/ALKAROS';
const SAN = `${ROOT}/src/Modules/Audit/EventStore/IAuditSanitizer.cs`;
const EVT = `${ROOT}/src/Modules/Audit/EventStore/AuditEvent.cs`;
const LOG = `${ROOT}/evidence/V1-RMD-644/mutations.log`;
const TEST = `${ROOT}/tests/Modules/Audit/EventStore/ALKAROS.Audit.EventStore.Tests.csproj`;
writeFileSync(LOG, '');
const files = { [SAN]: join(tmpdir(), 'san.orig'), [EVT]: join(tmpdir(), 'evt.orig') };
for (const [f, o] of Object.entries(files)) copyFileSync(f, o);

const tokens = ['"password"', '"passwords"', '"passphrase"', '"pin"', '"pins"', '"secret"', '"secrets"', '"token"', '"tokens"', '"cvv"', '"cvc"', '"pan"', '"pans"', '"salt"', '"jwt"', '"pwd"', '"otp"', '"iban"', '"authorization"', '"bearer"', '"cookie"', '"cookies"', '"credential"', '"credentials"', '"tckn"'];
const compounds = ['"cardnumber"', '"creditcard"', '"cardno"', '"apikey"', '"accesstoken"', '"refreshtoken"', '"authtoken"', '"idtoken"', '"jwttoken"', '"secretkey"', '"privatekey"', '"connectionstring"', '"pincode"'];
const mutants = [];
const BS = String.fromCharCode(92);
const dropEntry = (src, t) => { const i = src.indexOf(t, src.indexOf("SensitiveKeyTokens =")); if (i < 0) throw new Error("no entry " + t); const after = src.slice(i + t.length, i + t.length + 2); if (after === ", ") return src.slice(0, i) + src.slice(i + t.length + 2); const before = src.slice(i - 2, i); if (before === ", ") return src.slice(0, i - 2) + src.slice(i + t.length); throw new Error("cannot drop " + t); };
for (const t of [...tokens, ...compounds]) mutants.push([SAN, "drop " + t, (s) => dropEntry(s, t)]);
const rep = (file, label, from, to) => mutants.push([file, label, (s) => { if (!s.includes(from)) throw new Error('no match: ' + label); return s.replace(from, to); }]);
rep(SAN, 'catch drops ArgumentException', 'ex is JsonException or ArgumentException', 'ex is JsonException');
rep(SAN, 'root string branch disabled', 'rootValue.TryGetValue(out string? rootText))', 'rootValue.TryGetValue(out string? rootText) && false)');
rep(SAN, 'quote close not appended', 'if (closed)\n                result.Append(first);', 'if (closed && !closed)\n                result.Append(first);');
rep(SAN, 'escape skip removed', "== '" + BS + BS + "' ? 2 : 1", "== '" + BS + BS + "' ? 1 : 1");
rep(SAN, 'unquoted stops only at comma', " or '\"' or '}' or ']' or '" + BS + "r' or '" + BS + "n'", "");
rep(SAN, 'unquoted ignores newline', " or '" + BS + "r' or '" + BS + "n'", "");
rep(SAN, 'empty value marker', 'if (end > start)', 'if (end >= start)');
rep(SAN, 'sensitive check inverted', 'if (IsSensitiveKey(match.Groups["key"].Value))', 'if (!IsSensitiveKey(match.Groups["key"].Value))');
rep(SAN, 'camel split removed', 'foreach (var token in CamelCaseBoundary.Split(part))', 'foreach (var token in new[] { part })');
rep(EVT, 'no UTC normalisation', 'occurredAt?.ToUniversalTime() ?? DateTimeOffset.UtcNow', 'occurredAt ?? DateTimeOffset.UtcNow');
rep(EVT, 'event name limit 129', 'EventName.Length > 128', 'EventName.Length > 129');
rep(EVT, 'aggregate type limit 65', 'AggregateType.Length > 64', 'AggregateType.Length > 65');
rep(EVT, 'actor type limit 33', 'ActorType.Length > 32', 'ActorType.Length > 33');
rep(EVT, 'correlation limit 129', 'CorrelationId.Length > 128', 'CorrelationId.Length > 129');
rep(EVT, 'causation check removed', 'CausationId is { Length: > 128 }', 'false');

let survived = 0;
for (const [file, label, fn] of mutants) {
  let applied;
  try {
    const original = readFileSync(files[file], 'utf8');
    applied = fn(original);
    if (applied === original) { appendFileSync(LOG, `NOOP   ${label}\n`); continue; }
    writeFileSync(file, applied);
    let out = '';
    try { out = execFileSync(`${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`, ['test', TEST, '--nologo'], { encoding: 'utf8', env: { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' }, stdio: ['ignore', 'pipe', 'pipe'] }); }
    catch (e) { out = String(e.stdout || '') + String(e.stderr || ''); }
    const killed = /Başarısız!|error CS/.test(out);
    const compileErr = /error CS/.test(out);
    if (!killed) survived++;
    appendFileSync(LOG, `${killed ? (compileErr ? 'COMPILE' : 'KILLED ') : 'SURVIVED'} ${label}\n`);
  } catch (e) { appendFileSync(LOG, `ERROR  ${label}: ${e.message}\n`); }
  finally { copyFileSync(files[file], file); }
}
for (const [f, o] of Object.entries(files)) appendFileSync(LOG, `restore ${f.split('/').pop()}: ${isDeepStrictEqual(readFileSync(f), readFileSync(o)) ? 'identical' : 'DIFFERENT'}\n`);
appendFileSync(LOG, `mutants: ${mutants.length}, survived: ${survived}\n`);
