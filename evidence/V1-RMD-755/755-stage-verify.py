from pathlib import Path
import subprocess,json,hashlib,sys,datetime
root=Path('D:/project/ALKAROS'); out=root/'evidence/V1-RMD-755'
allowed={'tests/Modules/Billing/SplitDesign/SplitDesignDomainTests.cs','tests/Modules/Billing/SplitDesign/PostgresSplitDesignTests.cs','tests/Host/Experience/Billing/BillingSplitHttpTests.cs','plan/v1/remediation/V1-RMD-753-split-engine-host-billing-test-gaps.md','plan/v1/remediation/V1-RMD-755-bolme-testleri-gercek-kapanis.md'}
files=[p for p in out.rglob('*') if p.is_file() and not any(x in p.relative_to(out).parts for x in ['isolated','artifacts','__pycache__'])]
paths=sorted(allowed|{p.relative_to(root).as_posix() for p in files})
# git add exact paths only: never add isolated copies or pre-existing unrelated evidence.
subprocess.run(['git','add','--',*paths],cwd=root,check=True)
staged=subprocess.check_output(['git','diff','--cached','--name-only','-z'],cwd=root).decode('utf-8').strip('\0').split('\0')
staged=[p for p in staged if p]
outside=[p for p in staged if p not in allowed and not p.startswith('evidence/V1-RMD-755/')]
manifest=[]
for rel in staged:
    if rel == 'evidence/V1-RMD-755/staged-scope-proof.json':
        continue  # A manifest cannot contain its own final hash.
    blob=subprocess.check_output(['git','show',':'+rel],cwd=root)
    manifest.append({'path':rel,'blob_sha256':hashlib.sha256(blob).hexdigest(),'worktree_matches_index':hashlib.sha256((root/rel).read_bytes()).hexdigest()==hashlib.sha256(blob).hexdigest()})
# Compare metadata contract after removing only Status, Assignee, and newly required Blocker section.
rel='plan/v1/remediation/V1-RMD-753-split-engine-host-billing-test-gaps.md'
before=subprocess.check_output(['git','show','4eca31da:'+rel],cwd=root).decode('utf-8').replace('\r\n','\n')
after=(root/rel).read_text(encoding='utf-8')
import re
def contract(s):
    s=re.sub(r'^- (Status|Assignee):.*\n','',s,flags=re.M)
    return re.sub(r'^## Blocker\n.*?(?=^## |\Z)','',s,flags=re.M|re.S)
proof={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'outside':outside,'paths':manifest,'task753_contract_preserved':contract(before)==contract(after),'valid':not outside and all(x['worktree_matches_index'] for x in manifest) and contract(before)==contract(after)}
(out/'staged-scope-proof.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'staged_paths':len(staged),'outside':outside,'task753_contract_preserved':proof['task753_contract_preserved'],'valid':proof['valid']}))
sys.exit(not proof['valid'])
