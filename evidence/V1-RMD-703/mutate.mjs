import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const store = `${root}/src/Host/Experience/Catalog/CatalogManagementStore.cs`;
const repo = `${root}/src/Modules/Catalog/Pricing/PostgresPricingRepository.cs`;
const sweep = `${root}/src/Host/Experience/Catalog/CatalogPriceRecomputeHostedService.cs`;
const log = `${root}/evidence/V1-RMD-703/mutations.log`;
const cmd = `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres ~/.dotnet/dotnet.exe test tests/Host/Experience/Catalog/ALKAROS.Host.Experience.Catalog.Tests.csproj`;

const replace = (from, to) => (s) => {
  if (!s.includes(from)) return null;
  return s.replace(from, to);
};
const afterAnchor = (anchor, from, to) => (s) => {
  const a = s.indexOf(anchor);
  if (a < 0) return null;
  const i = s.indexOf(from, a);
  if (i < 0) return null;
  return s.slice(0, i) + to + s.slice(i + from.length);
};

const mutants = [
  { id: "M1", file: store, mutate: replace("_prices.AddReplacingEarlierAsync(", "_prices.AddAsync("), note: "price added without closing the predecessor" },
  { id: "M2", file: repo, mutate: replace("SET effective_to = @effective_from", "SET effective_to = effective_to"), note: "predecessor not closed" },
  { id: "M3", file: store, mutate: replace('if (normalized != "TRY")', 'if (normalized != "XXX")'), note: "non-TRY sale price accepted" },
  { id: "M4", file: store, mutate: replace("(@current_price::numeric IS NOT NULL OR EXISTS (", "(TRUE OR EXISTS ("), note: "seed price cleared by first future price" },
  { id: "M5", file: sweep, mutate: afterAnchor("AND EXISTS (", "pp.effective_from <= now()", "pp.effective_from <= now() + interval '10 years'"), note: "sweep clears seed price before first dated price starts" },
  { id: "M6", file: sweep, mutate: afterAnchor("AND EXISTS (", "pp.currency_code = 'TRY'", "pp.currency_code <> 'XXX'"), note: "sweep counts non-TRY rows" },
];

const only = process.argv.slice(2);
for (const m of mutants) {
  if (only.length && !only.includes(m.id)) continue;
  const original = readFileSync(m.file, "utf8");
  const mutated = m.mutate(original);
  if (mutated === null) { appendFileSync(log, `${m.id} NOT-APPLIED ${m.note}\n`); continue; }
  writeFileSync(m.file, mutated);
  let result;
  try {
    execSync(cmd, { cwd: root, shell: "bash", stdio: "pipe", timeout: 600000 });
    result = "SURVIVED";
  } catch (e) {
    const out = `${e.stdout}${e.stderr}`;
    result = /error CS|error CA/.test(out) ? "BUILD-ERROR" : "KILLED";
  } finally {
    writeFileSync(m.file, original);
  }
  appendFileSync(log, `${m.id} ${result} restored=${readFileSync(m.file, "utf8") === original} ${m.note}\n`);
}
