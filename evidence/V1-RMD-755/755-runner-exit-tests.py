from pathlib import Path
import ast,subprocess,sys,json
scratch=Path(__file__).parent;out=Path('D:/project/ALKAROS/evidence/V1-RMD-755');rows=[]
# Decision-level fixtures only: no simulated build is presented as an actual build.
for name,cases in [('755-build-final.py',[
('clean',[{'exit_code':0,'summary':['    0 Uyarı','    0 Hata']}],0),
('failed-build',[{'exit_code':1,'summary':['    0 Uyarı','    0 Hata']}],1),
('warning',[{'exit_code':0,'summary':['    1 Uyarı','    0 Hata']}],1),
('error',[{'exit_code':0,'summary':['    0 Uyarı','    1 Hata']}],1),
('missing-summary',[{'exit_code':0,'summary':[]}],1),
('english-clean',[{'exit_code':0,'summary':['    0 Warning(s)','    0 Error(s)']}],0)]),('755-gates.py',[
('clean',{'rows':[{'exit_code':0}],'proof':{'identical':True}},0),
('failed-gate',{'rows':[{'exit_code':0},{'exit_code':1}],'proof':{'identical':True}},1),
('changed-regression',{'rows':[{'exit_code':0}],'proof':{'identical':False}},1)])]:
    tree=ast.parse((scratch/name).read_text()); decision=ast.Module(body=[n for n in tree.body if (isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='valid' for t in n.targets)) or isinstance(n,ast.Raise)],type_ignores=[])
    source=ast.unparse(decision)
    for label,data,expected in cases:
        init='results='+repr(data) if name.startswith('755-build') else ';'.join(k+'='+repr(v) for k,v in data.items())
        p=subprocess.run([sys.executable,'-c','import re\n'+init+'\n'+source],capture_output=True,text=True)
        rows.append({'script':name,'case':label,'exit_code':p.returncode,'expected':expected,'passed':p.returncode==expected})
report={'tier':'Explicit decision fixtures; not actual failed builds/gates','cases':rows,'all_passed':all(x['passed'] for x in rows)}
(out/'runner-exit-tests.json').write_text(json.dumps(report,indent=2),encoding='utf-8'); print(json.dumps(report));raise SystemExit(0 if report['all_passed'] else 1)
