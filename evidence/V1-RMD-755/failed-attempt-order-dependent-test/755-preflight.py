from pathlib import Path
import subprocess, json, hashlib, sys
root=Path('D:/project/ALKAROS'); scratch=Path(__file__).parent
snapshot=scratch/'755-start-snapshot.json'
def state():
    raw=subprocess.check_output(['git','status','--porcelain=v1','-z','--untracked-files=all'],cwd=root)
    parts=raw.decode('utf-8').split('\0'); result={}; i=0
    while i < len(parts):
        row=parts[i]; i+=1
        if not row: continue
        status=row[:2]; path=row[3:]; old=None
        if 'R' in status or 'C' in status: old=parts[i]; i+=1
        p=root/path
        result[path]={'status':status,'sha256':hashlib.sha256(p.read_bytes()).hexdigest() if p.is_file() else None,'old':old}
    return result
if __name__=='__main__':
    if not snapshot.exists():
        baseline={'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'paths':state(),
          'status_short':subprocess.check_output(['git','status','--short'],cwd=root,text=True),
          'diff_names':subprocess.check_output(['git','diff','--name-only'],cwd=root,text=True)}
        snapshot.write_text(json.dumps(baseline,ensure_ascii=False,indent=2),encoding='utf-8')
        print('snapshot saved',len(baseline['paths']))
    else:
        baseline=json.loads(snapshot.read_text(encoding='utf-8')); now=state()
        allowed={'tests/Modules/Billing/SplitDesign/SplitDesignDomainTests.cs','tests/Modules/Billing/SplitDesign/PostgresSplitDesignTests.cs','tests/Host/Experience/Billing/BillingSplitHttpTests.cs',
          'plan/v1/remediation/V1-RMD-753-split-engine-host-billing-test-gaps.md','plan/v1/remediation/V1-RMD-755-bolme-testleri-gercek-kapanis.md'}
        changed=[p for p in sorted(set(now)|set(baseline['paths'])) if now.get(p)!=baseline['paths'].get(p)]
        outside=[p for p in changed if p not in allowed and not p.startswith('evidence/V1-RMD-755/')]
        print(json.dumps({'delta':changed,'outside':outside,'valid':not outside},ensure_ascii=False))
        sys.exit(bool(outside))
