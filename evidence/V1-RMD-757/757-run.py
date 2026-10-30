import json, os, subprocess, hashlib, pathlib, sys
ROOT = pathlib.Path('D:/project/ALKAROS')
OUT = ROOT / 'evidence/V1-RMD-757'
DOTNET = 'C:/Users/semih/.dotnet/dotnet.exe'
RC = 'src/Host/DualScreen/DualScreenApplication.Receipt.cs'
SP = 'src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js'
TRACKED = [RC, SP, 'tests/Host/Experience/PaymentTender/BillReceiptHttpTests.cs',
           'tests/Clients/StaticApps/split-payment-receipt.test.js', 'tests/Clients/StaticApps/split-payment-item-refund.test.js']
env_list = json.loads(subprocess.check_output(['docker', 'inspect', 'alkaros-test-pg', '--format', '{{json .Config.Env}}'], text=True))
pw = next(e.split('=', 1)[1] for e in env_list if e.startswith('POSTGRES_PASSWORD='))
ENV = dict(os.environ, ALKAROS_TEST_PG_PORT='55432', ALKAROS_TEST_PG_PASSWORD=pw)
sha = lambda rel: hashlib.sha256((ROOT / rel).read_bytes()).hexdigest()


def run(args, cwd, log):
    r = subprocess.run(args, cwd=cwd, env=ENV, capture_output=True, text=True, encoding='utf-8', errors='replace')
    text = (r.stdout + r.stderr).replace(pw, '[REDACTED]')
    log.write_text(text, encoding='utf-8')
    return r.returncode, text


def suite(folder):
    folder.mkdir(parents=True, exist_ok=True)
    hc, ht = run([DOTNET, 'test', str(ROOT / 'tests/Host/Experience/PaymentTender'), '--filter',
                  'FullyQualifiedName~BillReceiptHttpTests', '--logger', 'trx', '--results-directory', str(folder)],
                 ROOT, folder / 'host-test.log')
    cc, ct = run(['npx.cmd', 'vitest', 'run', 'split-payment-receipt.test.js', 'split-payment-item-refund.test.js'],
                 ROOT / 'tests/Clients/StaticApps', folder / 'client-test.log')
    pick = lambda t, keys: [l.strip() for l in t.splitlines() if any(k in l for k in keys)][:6]
    return {'host_exit': hc, 'host_summary': pick(ht, ['Başarısız!', 'Başarılı!', 'Failed!', 'Passed!']),
            'host_failures': pick(ht, ['[FAIL]', 'Assert.', 'KeyNotFoundException']),
            'client_exit': cc, 'client_summary': pick(ct, ['Tests ', 'Test Files']),
            'client_failures': pick(ct, ['FAIL ', 'AssertionError'])}


mode = sys.argv[1]
if mode == 'red':
    # Production files at the InProgress baseline, tests as written now.
    saved = {rel: (ROOT / rel).read_bytes() for rel in (RC, SP)}
    try:
        for rel in saved:
            (ROOT / rel).write_bytes(subprocess.check_output(['git', 'show', f'HEAD:{rel}'], cwd=ROOT))
        res = suite(OUT / 'red') | {'source_sha256': {rel: sha(rel) for rel in TRACKED}}
    finally:
        for rel, data in saved.items():
            (ROOT / rel).write_bytes(data)
    ok = res['host_exit'] != 0 and res['client_exit'] != 0
elif mode == 'green':
    b = run([DOTNET, 'build', str(ROOT / 'src/Host/ALKAROS.Host.csproj'), '-c', 'Debug', '-v', 'minimal'], ROOT,
            (OUT / 'host-build.log'))
    res = suite(OUT / 'green') | {'build_exit': b[0], 'source_sha256': {rel: sha(rel) for rel in TRACKED}}
    clean = any(k in b[1] for k in ('0 Uyarı', '0 Warning')) and any(k in b[1] for k in ('0 Hata', '0 Error'))
    ok = b[0] == 0 and clean and res['host_exit'] == 0 and res['client_exit'] == 0
    res['build_clean'] = clean
else:
    mutants = [
        ('server-counts-pending-refunds', RC, "WHERE r.bill_id = @bill AND r.status = 'Completed'",
         "WHERE r.bill_id = @bill AND r.status IN ('Requested', 'AwaitingManualRefund', 'Completed')", 'host'),
        ('client-condition-uses-refundedQuantity', SP, 'item.completedRefundedQuantity > 0', 'item.refundedQuantity > 0', 'client'),
        ('client-prints-refundedQuantity', SP, 'Number(item.completedRefundedQuantity)', 'Number(item.refundedQuantity)', 'client'),
    ]
    before = {rel: sha(rel) for rel in (RC, SP)}
    originals = {rel: (ROOT / rel).read_bytes() for rel in (RC, SP)}
    rows = []
    try:
        for name, rel, old, new, side in mutants:
            text = originals[rel].decode('utf-8')
            assert text.count(old) == 1, name
            (ROOT / rel).write_bytes(text.replace(old, new).encode('utf-8'))
            r = suite(OUT / 'mutations' / name)
            (ROOT / rel).write_bytes(originals[rel])
            exit_code = r[f'{side}_exit']
            assertions = [l for l in r[f'{side}_failures'] if 'Assert' in l]
            rows.append({'mutant': name, 'file': rel, 'side': side, 'exit': exit_code, 'assertions': assertions,
                         'killed': exit_code != 0 and bool(assertions)})
    finally:
        for rel, data in originals.items():
            (ROOT / rel).write_bytes(data)
    res = {'mutants': rows, 'sources_restored': before == {rel: sha(rel) for rel in (RC, SP)}}
    ok = res['sources_restored'] and len(rows) == len(mutants) and all(r['killed'] for r in rows)
res['passed'] = ok
(OUT / f'{mode}.json').write_text(json.dumps(res, indent=2, ensure_ascii=False), encoding='utf-8')
print(json.dumps(res, ensure_ascii=False))
raise SystemExit(0 if ok else 1)
