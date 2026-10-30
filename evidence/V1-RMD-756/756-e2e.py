import json, os, subprocess, sys, hashlib, pathlib
ROOT = pathlib.Path('D:/project/ALKAROS')
E2E = ROOT / 'tests/E2E/Cashier'
label = sys.argv[1]
specs = sys.argv[2:]
out = ROOT / 'evidence/V1-RMD-756' / label
out.mkdir(parents=True, exist_ok=True)
env_list = json.loads(subprocess.check_output(['docker', 'inspect', 'alkaros-test-pg', '--format', '{{json .Config.Env}}'], text=True))
pw = next(e.split('=', 1)[1] for e in env_list if e.startswith('POSTGRES_PASSWORD='))
dotnet = 'C:/Users/semih/.dotnet/dotnet.exe'
b = subprocess.run([dotnet, 'build', str(ROOT / 'src/Host/ALKAROS.Host.csproj'), '-c', 'Debug', '-v', 'minimal'], capture_output=True, text=True, encoding='utf-8', errors='replace')
(out / 'host-build.log').write_text(b.stdout + b.stderr, encoding='utf-8')
if b.returncode != 0:
    print(json.dumps({'build_exit': b.returncode})); raise SystemExit(1)
env = dict(os.environ, ALKAROS_TEST_PG_PORT='55432', ALKAROS_TEST_PG_PASSWORD=pw)
r = subprocess.run(['npx.cmd', 'playwright', 'test', *specs, '--reporter=list'], cwd=E2E, env=env, capture_output=True, text=True, encoding='utf-8', errors='replace')
log = (r.stdout + r.stderr).replace(pw, '[REDACTED]')
(out / 'playwright.log').write_text(log, encoding='utf-8')
srcs = ['src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js', 'src/Clients/Cashier/wwwroot/payments/cash-session/cash-session.js', *[f'tests/E2E/Cashier/specs/{s}' for s in specs]]
res = {'label': label, 'specs': specs, 'build_exit': 0, 'playwright_exit': r.returncode,
       'summary': [l.strip() for l in log.splitlines() if l.strip().split(' ')[0].isdigit() and (' passed' in l or ' failed' in l or ' flaky' in l or ' skipped' in l)],
       'source_sha256': {s: hashlib.sha256((ROOT / s).read_bytes()).hexdigest() for s in srcs}}
(out / 'result.json').write_text(json.dumps(res, indent=2, ensure_ascii=False), encoding='utf-8')
print(json.dumps(res, ensure_ascii=False))
raise SystemExit(r.returncode)
