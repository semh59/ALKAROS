import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const app = "src/Host/DualScreen/DualScreenApplication.cs";
const proj = "tests/Host/Experience/Composition/ALKAROS.Host.Experience.Composition.Tests.csproj";
const mutants = [
  { id: "M1", from: "                    || FromRelayNetwork(context, nfcTrustedNetworks);", to: "                    ;" },
  { id: "M2", from: "                    || path.StartsWithSegments(\"/hubs\", StringComparison.Ordinal);", to: "                    ;" },
  { id: "M3", from: "                || FromRelayNetwork(context, options.NfcTrustedNetworks);", to: "                || false;" },
  { id: "M4", from: "context.Items[PeerAddressItem] = context.Connection.RemoteIpAddress;", to: "context.Items[PeerAddressItem] = IPAddress.None;" },
  { id: "M5", from: "context.Response.Headers[\"X-Content-Type-Options\"] = \"nosniff\";\n            context.Response.Headers[\"Referrer-Policy\"] = \"no-referrer\";\n            await next();\n        });\n        app.Use(async (context, next) =>\n        {\n            var insecure", to: "context.Response.Headers[\"Referrer-Policy\"] = \"no-referrer\";\n            await next();\n        });\n        app.Use(async (context, next) =>\n        {\n            var insecure" },
  { id: "M6", from: "frame-ancestors 'none'; object-src 'none'; base-uri 'none'\";\n            context.Response.Headers[\"X-Content", to: "object-src 'none'; base-uri 'none'\";\n            context.Response.Headers[\"X-Content" },
  { id: "M7", from: "nfcLoopbackOriginTrusted || nfcTrustedNetworks is { Count: > 0 })", to: "nfcLoopbackOriginTrusted)" },
  { id: "M8", from: "&& networks.Any(network => network.Contains(peer));", to: "&& networks.Any(network => !network.Contains(peer));" },
  { id: "M9", from: "&& context.Items[PeerAddressItem] is IPAddress peer", to: "&& context.Connection.RemoteIpAddress is IPAddress peer" },
];
const only = process.argv[2] ? process.argv[2].split(",") : null;
const log = [];
for (const m of mutants) {
  if (only && !only.includes(m.id)) continue;
  const full = path.join(root, app);
  const orig = fs.readFileSync(full, "utf8");
  const crlf = orig.includes("\r\n");
  const normalized = crlf ? orig.replace(/\r\n/g, "\n") : orig;
  if (!normalized.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); console.log(log.at(-1)); continue; }
  const mutated = normalized.replace(m.from, () => m.to);
  fs.writeFileSync(full, crlf ? mutated.replace(/\n/g, "\r\n") : mutated);
  let out;
  try {
    out = execSync(`"${process.env.USERPROFILE}/.dotnet/dotnet.exe" test ${proj} --filter "FullyQualifiedName~TunnelRouteIsolationTests"`, {
      cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 500000,
      env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" },
    });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  const verdict = /error CS|error CA/.test(out) ? "BUILD-ERROR" : /Başarısız!|FAILED/.test(out) ? "KILLED" : "SURVIVED";
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-688/mutations.log"), log.join("\n") + "\n");
