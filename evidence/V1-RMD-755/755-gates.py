from pathlib import Path
import subprocess,json,hashlib,datetime
root=Path('D:/project/ALKAROS');out=root/'evidence/V1-RMD-755';rows=[]
for name,args in [('plan',['python','-B','tools/plan-audit/plan_audit_tool.py','validate']),('consistency',['python','-B','tools/consistency-audit/consistency_audit.py']),('whitespace',['git','-c','core.whitespace=cr-at-eol','diff','--check']),('scope-pending-delta',['python','-B','C:/Users/semih/AppData/Local/hermes/profiles/alkaros/cache/scratch/755-preflight.py'])]:
    log=out/(name+'.log')
    with log.open('w',encoding='utf-8') as f:p=subprocess.run(args,cwd=root,stdout=f,stderr=subprocess.STDOUT)
    rows.append({'name':name,'command':args,'exit_code':p.returncode,'sha256':hashlib.sha256(log.read_bytes()).hexdigest(),'utc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
# Protect the committed 754 regression method byte-for-byte (newlines excluded only from source decoding).
rel='tests/Host/Experience/Billing/BillingSplitHttpTests.cs';baseline=subprocess.check_output(['git','show','4eca31da:'+rel],cwd=root).decode('utf-8').replace('\r\n','\n');now=(root/rel).read_text(encoding='utf-8')
method='    [Fact]\n    public async Task ATipAndAnEqualDiscountThatKeepThePayableButLowerTheTaxRefuseAnItemSplitWith409()'
def extract(s):
    start=s.index(method);end=s.index('\n    }\n',start)+7;return s[start:end]
old=extract(baseline);new=extract(now)
proof={'baseline_method_sha256':hashlib.sha256(old.encode()).hexdigest(),'current_method_sha256':hashlib.sha256(new.encode()).hexdigest(),'identical':old==new}
(out/'754-regression-preservation.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
(out/'gate-results.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
print(json.dumps({'gates':[(r['name'],r['exit_code']) for r in rows],'regression_preserved':proof['identical']}))
raise SystemExit(0 if all(row['exit_code'] == 0 for row in rows) and proof['identical'] else 1)
