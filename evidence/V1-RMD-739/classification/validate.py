from pathlib import Path
import collections
import csv
import hashlib
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
CODES = {'EZ', 'TE', 'KN', 'BD', 'PO', 'UI', 'VD', 'GY', 'DY', 'MK', 'BK'}
QUALIFICATIONS = {'aday', 'belirsiz', 'bilgi', 'bulgu-degil'}

def load(name):
    return json.loads((HERE / name).read_text(encoding='utf-8-sig'))

def line(source):
    path, number = source.rsplit(':', 1)
    lines = (ROOT / path).read_text(encoding='utf-8-sig').splitlines()
    assert 0 < int(number) <= len(lines), source
    return lines[int(number) - 1]

main = load('ANA_KAYITLAR.json')
supplement = load('EK_KAYITLAR.json')
summary = load('SAYIM.json')
closure = load('KAPANIS_ESLESMESI.json')
assert len(main) == 1020
assert {r['id'] for r in main} == {f'F{n:04d}' for n in range(1, 1021)}
all_rows = main + supplement
assert len({r['id'] for r in all_rows}) == len(all_rows)
for row in all_rows:
    assert row['category'] in CODES, row['id']
    assert row['qualification'] in QUALIFICATIONS, row['id']
    assert all(c in CODES for c in row.get('secondary', [])), row['id']
    assert row['text'] == line(row['source']), row['id']
    expected = 'kritik' if row['qualification'] in {'aday', 'belirsiz'} else 'uygulanmaz'
    assert row['priority'] == expected, row['id']
assert sum('bulgu değil' in r['text'].lower() for r in main) == 3
po_path = 'evidence/V1-RMD-739/findings/po-questions.md'
po_sources = {f'{po_path}:{n}' for n, text in enumerate((ROOT / po_path).read_text(encoding='utf-8').splitlines(), 1) if text.startswith('- ')}
assert len(po_sources) == 47
assert po_sources <= {r['source'] for r in supplement}
ui_paths = {r['source'].rsplit(':', 1)[0] for r in supplement if '/ui/' in r['source']}
assert len(ui_paths) == 7
for path in ui_paths:
    for number, text in enumerate((ROOT / path).read_text(encoding='utf-8').splitlines(), 1):
        if text.startswith('- ') or re.match(r'\d+\. ', text) or text.startswith(('Düşük:', 'Düşük (özet):', 'Doğrulanamadı:', 'Bakılmadı:', 'Hiç değinilmemiş:')):
            assert f'{path}:{number}' in {r['source'] for r in supplement}, f'Missing UI source: {path}:{number}'
assert len(closure['complete']) == len({r['source'] for r in closure['complete']}) == 16
assert sum(r['closure_status'] == 'kayitli-kanitla-tam-kapanis-eslesmesi' for r in main) == 16
assert sum(r['closure_status'] == 'kismi-kalan-kusur-var' for r in main) == 29
expected_closed = {'evidence/V1-RMD-739/findings/' + item['source']: item['task'] for item in closure['complete']}
actual_closed = {r['source']: r['closure_task'] for r in main if r['closure_status'] == 'kayitli-kanitla-tam-kapanis-eslesmesi'}
assert actual_closed == expected_closed
expected_partial = {'evidence/V1-RMD-739/findings/' + item['source']: item['task'] for item in closure['partial']}
span = closure['partial_range']
for number in range(span['first_line'], span['last_line'] + 1):
    expected_partial[f'evidence/V1-RMD-739/findings/{span["file"]}:{number}'] = span['task']
assert {r['source']: r['closure_task'] for r in main if r['closure_status'] == 'kismi-kalan-kusur-var'} == expected_partial
for name, rows in [('main', main), ('supplement', supplement)]:
    assert summary[f'{name}_records'] == len(rows)
    assert summary[f'{name}_categories'] == dict(collections.Counter(r['category'] for r in rows))
    assert summary[f'{name}_qualifications'] == dict(collections.Counter(r['qualification'] for r in rows))
packages = load('DUZELTME_PAKETLERI.json')
package_records = [identifier for package in packages for identifier in package['records']]
assert len(package_records) == len(set(package_records)) == len(all_rows)
assert set(package_records) == {r['id'] for r in all_rows}
assert len(packages) == summary['routing_packages']
package_map = {p['id']: p for p in packages}
for row in all_rows:
    package = package_map[row['work_package']]
    assert row['id'] in package['records']
    assert row['category'] == package['category']
    assert row['routing_domain'] == package['domain_hint']
    assert package['status'] == 'planlama-adayi-uygulama-gorevi-degil'
links = load('KOK_NEDEN_BAGLARI.json')
assert len(links) == summary['root_cause_links']
for link in links:
    assert len(link['records']) == len(set(link['records'])) >= 2
    assert set(link['records']) <= {r['id'] for r in all_rows}
for source in summary['sources']:
    assert hashlib.sha256((ROOT / source['path']).read_bytes()).hexdigest() == source['sha256'], source['path']
with (HERE / 'TUM_KAYITLAR.csv').open(encoding='utf-8-sig', newline='') as stream:
    csv_rows = list(csv.DictReader(stream))
assert len(csv_rows) == len(all_rows)
assert {r['id'] for r in csv_rows} == {r['id'] for r in all_rows}
json_by_id = {r['id']: r for r in all_rows}
for csv_row in csv_rows:
    source = json_by_id[csv_row['id']]
    expected = {key: ';'.join(source.get(key, [])) if key == 'secondary' else str(source.get(key, '')) for key in csv_row}
    assert csv_row == expected, csv_row['id']
corrections = load('SINIFLAMA_DUZELTMELERI.json')
assert len(corrections) == len({r['id'] for r in corrections}) == summary['parent_review_corrections']
for correction in corrections:
    row = json_by_id[correction['id']]
    for key in ['category', 'secondary', 'qualification', 'reason']:
        assert row[key] == correction[key], (row['id'], key)
for name, expected in [('TARAMA_ADAYLARI.json', 522), ('KANIT_ENVANTERI.json', 974), ('BAYAT_BELGE_ADAYLARI.json', 42)]:
    records = load(name)
    assert len(records) == len({r['id'] for r in records}) == expected
    for record in records:
        line(record['source'])
changed = subprocess.check_output(['git', 'status', '--porcelain=v1', '-uall'], cwd=ROOT, text=True)
allowed_metadata = 'plan/v1/remediation/V1-RMD-739-bitmis-gorev-dogrulama-programi.md'
paths = []
for status in changed.splitlines():
    path = status[3:].replace('\\', '/')
    assert ' -> ' not in path, status
    assert path == allowed_metadata or path.startswith('evidence/V1-RMD-739/classification/'), status
    paths.append(path)
metadata_diff = subprocess.check_output(['git', 'diff', '--unified=0', '--', allowed_metadata], cwd=ROOT, text=True)
for text in metadata_diff.splitlines():
    if text.startswith(('+', '-')) and not text.startswith(('+++', '---')):
        assert text[1:].startswith('- Assignee: '), text
print(json.dumps({'result': 'PASS', 'main': len(main), 'supplement': len(supplement), 'po_source_rows': len(po_sources), 'ui_reports': len(ui_paths), 'full_closure_matches': 16, 'partial_closure_matches': 29, 'scan_matches': 522, 'flagged_tasks': 974, 'stale_paths': 42, 'allowed_changed_paths': paths}, ensure_ascii=False, indent=2))
