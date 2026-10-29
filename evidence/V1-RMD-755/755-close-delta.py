from pathlib import Path
import subprocess,json,hashlib,sys,datetime
root=Path('D:/project/ALKAROS'); out=root/'evidence/V1-RMD-755'
allowed={'tests/Modules/Billing/SplitDesign/SplitDesignDomainTests.cs','tests/Modules/Billing/SplitDesign/PostgresSplitDesignTests.cs','tests/Host/Experience/Billing/BillingSplitHttpTests.cs','plan/v1/remediation/V1-RMD-753-split-engine-host-billing-test-gaps.md','plan/v1/remediation/V1-RMD-755-bolme-testleri-gercek-kapanis.md'}
def permit(p):return p in allowed or p.startswith('evidence/V1-RMD-755/')
def git(*args):return subprocess.check_output(['git',*args],cwd=root)
proof=json.loads((out/'full-preflight-preservation.json').read_text(encoding='utf-8'))
baseline=proof['baseline_worktree']; now=set(git('ls-files','--cached','--others','--exclude-standard','-z').decode().strip('\0').split('\0')); outside=[]
for rel in sorted(set(baseline)|now):
    if permit(rel):continue
    old=baseline.get(rel); p=root/rel
    if old is None:
        outside.append({'path':rel,'reason':'new unapproved path'});continue
    if old.get('kind')=='file':
        digest=hashlib.sha256(p.read_bytes()).hexdigest() if p.is_file() else None
        if digest!=old['sha256']:outside.append({'path':rel,'reason':'modified or deleted baseline file'})
def index_rows(rows):return {r.split('\t',1)[1]:r.split('\t',1)[0] for r in rows if r}
before=index_rows(proof['baseline_index']);current=index_rows(git('ls-files','--stage','-z').decode().split('\0'))
index_outside=[rel for rel in set(before)|set(current) if before.get(rel)!=current.get(rel) and not permit(rel)]
staged=git('diff','--cached','--name-status','--find-renames','-z').decode().split('\0')
report={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'outside_worktree':outside,'outside_index':sorted(index_outside),'staged_name_status_z':staged,'valid':not outside and not index_outside,'baseline_unrelated_count':sum(not permit(p) for p in baseline),'current_governed_count':len(now),'head':git('rev-parse','HEAD').decode().strip()}
(out/'closing-delta-proof.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8'); print(json.dumps({k:v for k,v in report.items() if k!='staged_name_status_z'}));sys.exit(not report['valid'])
