from pathlib import Path
import hashlib,json,re,subprocess
ROOT=Path(__file__).resolve().parents[4]
HERE=Path(__file__).resolve().parent
RENAME='df26505dc3c912eda01436112f362d31b8105f3f'
BASELINE=HERE/'belge-dogrulama-v3.log'

def require(condition,message):
    if not condition:raise SystemExit(f'FAIL: {message}')

def lines(path):return (ROOT/path).read_text(encoding='utf-8-sig').splitlines()

def validate():
    pairs=[('evidence/V11-INV-001/closure-evidence.txt','057-stock-movements',[21,22,25,61,62],'database/migrations/V11/V11-INV-001/060-stock-movements','STK031_BELGE_DUZELTMESI_V3.md'),('evidence/V11-INV-002/closure-evidence.txt','058-stock-balances',[19,20,23,62,63],'database/migrations/V11/V11-INV-002/061-stock-balances','STK032_BELGE_DUZELTMESI_V3.md')]
    paths=set()
    for original,old,line_numbers,current,artifact in pairs:
        require([n for n,line in enumerate(lines(original),1) if re.search(rf'\b{old[:3]}\b|{old}',line)]==line_numbers,f'{original} stale reference lines')
        text=(HERE/artifact).read_text(encoding='utf-8')
        require(RENAME in text and '2026-09-06' in text,f'{artifact} provenance')
        for suffix in ['.up.sql','.down.sql']:
            require(current+suffix in text and (ROOT/(current+suffix)).is_file(),current+suffix)
            paths.add(current+suffix)
        paths.add(original)
    require(subprocess.run(['git','cat-file','-e',RENAME+'^{commit}'],cwd=ROOT).returncode==0,'rename commit')
    old='evidence/V15-KVK-002/README.md'
    task='plan/v1/remediation/V1-RMD-481-audit-log-ten-year-disposal.md'
    readme='evidence/V1-RMD-481/README.md'
    require('V1-RMD-481` olarak planlandı' in lines(old)[25],'GYZ-017 source line 26')
    require(lines(task)[3]=='- Status: Done','V1-RMD-481 status line 4')
    logs=sorted(set(re.findall(r'`([\w.-]+\.log)`','\n'.join(lines(readme)))))
    require(len(logs)==5 and all((ROOT/'evidence/V1-RMD-481'/p).is_file() for p in logs),'V1-RMD-481 logs')
    text=(HERE/'GYZ017_BELGE_DUZELTMESI_V3.md').read_text(encoding='utf-8')
    for token in ['src/Modules/Audit/PartitionDisposal/**','audit.partition.disposed','privacy.anonymization_events','hukuk']:require(token in text,token)
    require('STK-009 karar engelli' in (HERE/'STK032_BELGE_DUZELTMESI_V3.md').read_text(encoding='utf-8'),'STK-009 state')
    paths.update([old,task,readme])
    index=(HERE/'BELGE_DUZELTMELERI_V3.md').read_text(encoding='utf-8')
    links=re.findall(r'\]\(([^)]+)\)',index)
    require(len(links)==3 and all((HERE/t).is_file() for t in links),'index links')
    hashes={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in sorted(paths)}
    require(hashes==json.loads(BASELINE.read_text(encoding='utf-8'))['source_hashes'],'source hashes differ from recorded baseline')
    return {'result':'PASS','correction_notes':3,'links_checked':3,'stale_lines_checked':10,'rmd481_logs_checked':len(logs),'tier':'source/document link validation only; not runtime or defect closure','source_hashes':hashes}

if __name__=='__main__':
    print(json.dumps(validate(),ensure_ascii=False,indent=2))
