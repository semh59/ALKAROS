import json, os, subprocess, hashlib, pathlib, shutil
ROOT = pathlib.Path('D:/project/ALKAROS')
E2E = ROOT / 'tests/E2E/Cashier'
OUT = ROOT / 'evidence/V1-RMD-756/mutations'
OUT.mkdir(parents=True, exist_ok=True)
SP = ROOT / 'src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js'
CS = ROOT / 'src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js'
mutants = [
    ('discount-signature-ignored', SP, "state.discountRequestSignature !== discountSignature", "false", '18-discount-and-tip.spec.js', 'indirim'),
    ('tip-signature-ignored', SP, "state.tipRequestSignature !== tipSignature", "false", '18-discount-and-tip.spec.js', 'bahşiş: aynı'),
    ('movement-signature-ignored', CS, "state.cashMovementRequestSignature !== movementSignature", "false", '05-cash-session-lifecycle.spec.js', 'kasa hareketi'),
    ('discount-key-always-new', SP, "if (!state.discountIdempotencyKey || state.discountRequestSignature !== discountSignature)", "if (true)", '18-discount-and-tip.spec.js', 'indirim'),
    ('tip-key-always-new', SP, "if (!state.tipIdempotencyKey || state.tipRequestSignature !== tipSignature)", "if (true)", '18-discount-and-tip.spec.js', 'bahşiş: aynı'),
    ('movement-key-always-new', CS, "if (!state.cashMovementIdempotencyKey || state.cashMovementRequestSignature !== movementSignature)", "if (true)", '05-cash-session-lifecycle.spec.js', 'kasa hareketi'),
]
env_list = json.loads(subprocess.check_output(['docker', 'inspect', 'alkaros-test-pg', '--format', '{{json .Config.Env}}'], text=True))
pw = next(e.split('=', 1)[1] for e in env_list if e.startswith('POSTGRES_PASSWORD='))
env = dict(os.environ, ALKAROS_TEST_PG_PORT='55432', ALKAROS_TEST_PG_PASSWORD=pw)
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
originals = {p: p.read_bytes() for p in (SP, CS)}
before = {str(p): sha(p) for p in originals}
results = []
try:
    for name, path, old, new, spec, grep in mutants:
        text = originals[path].decode('utf-8')
        assert text.count(old) == 1, name
        path.write_bytes(text.replace(old, new).encode('utf-8'))
        r = subprocess.run(['npx.cmd', 'playwright', 'test', spec, '-g', f'ağ hatasından sonra {grep}', '--reporter=list'],
                           cwd=E2E, env=env, capture_output=True, text=True, encoding='utf-8', errors='replace')
        path.write_bytes(originals[path])
        log = (r.stdout + r.stderr).replace(pw, '[REDACTED]')
        (OUT / f'{name}.log').write_text(log, encoding='utf-8')
        assertion = [l.strip() for l in log.splitlines() if 'Error: expect(' in l]
        killed = r.returncode != 0 and bool(assertion) and ' 1 passed' not in log
        results.append({'mutant': name, 'file': str(path.relative_to(ROOT)), 'exit': r.returncode, 'assertion': assertion[:2], 'killed': killed})
finally:
    for p, b in originals.items():
        p.write_bytes(b)
after = {str(p): sha(p) for p in originals}
res = {'mutants': results, 'all_killed': all(r['killed'] for r in results) and len(results) == len(mutants),
       'sources_restored': before == after, 'source_sha256': after}
(OUT / 'result.json').write_text(json.dumps(res, indent=2, ensure_ascii=False), encoding='utf-8')
print(json.dumps(res, ensure_ascii=False))
raise SystemExit(0 if res['all_killed'] and res['sources_restored'] else 1)
