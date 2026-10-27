import fs from 'node:fs';
import { execSync } from 'node:child_process';
const REPO = 'src/Modules/Billing/SplitDesign/PostgresSplitDesignRepository.cs';
const dn = `${process.env.HOME}/.dotnet/dotnet.exe`;
const M = [
  ['M1-allocation-tie-breaker-removed', REPO, 'ORDER BY created_at ASC, bill_allocation_id ASC', 'ORDER BY created_at ASC',
    `${dn} test tests/Host/Experience/Billing -nologo --filter FullyQualifiedName~AllocationsSavedTogether`],
];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' };
for (const [id, file, from, to, cmd] of M) {
  const orig = fs.readFileSync(file, 'utf8');
  if (!orig.includes(from)) { fs.appendFileSync('evidence/V1-RMD-728/mutations.log', `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.replace(from, to));
  let verdict;
  try { execSync(cmd, { stdio: 'pipe', env, timeout: 600000 }); verdict = 'SURVIVED'; }
  catch (e) { verdict = /error CS|error CA/.test(String(e.stdout)) ? 'BUILD-ERROR' : 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync('evidence/V1-RMD-728/mutations.log', `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync('evidence/V1-RMD-728/mutations.log', 'DONE\n');
