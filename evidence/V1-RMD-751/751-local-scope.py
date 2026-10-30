from pathlib import Path
import subprocess, json, hashlib, shutil, sys, datetime
root = Path('D:/project/ALKAROS')
out = root / 'evidence/V1-RMD-751'
scratch = Path(__file__).parent
base = '114b8da4c527da6805b34ca89fd770613c5c6c7d'
mode = sys.argv[1]  # 'pre' before Done commit, 'post' after it


def run(args, cwd=root):
    p = subprocess.run(args, cwd=cwd, capture_output=True, text=True, encoding='utf-8', errors='replace')
    return p.returncode, p.stdout + p.stderr


clone = scratch / f'751-scope-clone-{mode}'
if clone.exists():
    shutil.rmtree(clone, onerror=lambda f, p, e: (Path(p).chmod(0o700), f(p)))
rows = []
code, text = run(['git', 'clone', '--shared', '--no-checkout', '-q', str(root), str(clone)], cwd=scratch)
rows.append({'step': 'clone', 'exit_code': code})
code, text = run(['git', 'checkout', '-q', '-b', 'scope751', base], cwd=clone)
rows.append({'step': 'checkout-baseline', 'exit_code': code})
# Replay only the 751-local commits onto the InProgress baseline; other tasks' commits are excluded.
commits = run(['git', 'log', '--format=%H', '--reverse', base + '..HEAD', '--',
               'evidence/V1-RMD-751', 'plan/v1/remediation/V1-RMD-751-indirim-audit-belge-guncelligi.md'])[1].split()
for sha in commits:
    files = run(['git', 'show', '--name-only', '--format=', sha])[1].split()
    foreign = [f for f in files if not (f.startswith('evidence/V1-RMD-751/') or f == 'plan/v1/remediation/V1-RMD-751-indirim-audit-belge-guncelligi.md')]
    code, text = run(['git', '-c', 'user.name=scope-replay', '-c', 'user.email=scope-replay@invalid', 'cherry-pick', '-x', sha], cwd=clone)
    rows.append({'step': 'cherry-pick', 'sha': sha, 'files': len(files), 'foreign_paths_in_commit': foreign, 'exit_code': code})
code, scope = run([sys.executable, '-B', 'tools/task-scope/task_scope_tool.py', '--task-id', 'V1-RMD-751',
                   '--diff-base', base, '--format', 'json'], cwd=clone)
(out / f'task-scope-local-{mode}.log').write_text(scope, encoding='utf-8')
rows.append({'step': 'task-scope-local-range', 'exit_code': code,
             'log_sha256': hashlib.sha256((out / f'task-scope-local-{mode}.log').read_bytes()).hexdigest()})
count = run(['git', 'rev-list', '--count', base + '..HEAD'], cwd=clone)[1].strip()
shutil.rmtree(clone, onerror=lambda f, p, e: (Path(p).chmod(0o700), f(p)))
valid = all(r['exit_code'] == 0 for r in rows) and int(count) > 0 and not any(r.get('foreign_paths_in_commit') for r in rows)
report = {'mode': mode, 'base': base, 'main_head': run(['git', 'rev-parse', 'HEAD'])[1].strip(),
          'replayed_commits': commits, 'local_range_commit_count': int(count), 'steps': rows, 'valid': valid,
          'method': 'Shared scratch clone; 751-only commits cherry-picked onto the InProgress baseline so the range excludes 752-755 commits.',
          'utc': datetime.datetime.now(datetime.timezone.utc).isoformat()}
(out / f'scope-proof-{mode}.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps({k: report[k] for k in ['mode', 'local_range_commit_count', 'valid']}))
sys.exit(0 if valid else 1)
