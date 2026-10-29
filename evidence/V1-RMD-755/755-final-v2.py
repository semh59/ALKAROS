from pathlib import Path
import subprocess,shutil,json,sys,os,xml.etree.ElementTree as ET
root=Path('D:/project/ALKAROS');out=root/'evidence/V1-RMD-755';scratch=Path(__file__).parent
archive=out/'failed-attempt-order-dependent-test';archive.mkdir(exist_ok=False)
for p in list(out.iterdir()):
    if p.is_file():shutil.copy2(p,archive/p.name)
    elif p.name.endswith('-results'):shutil.copytree(p,archive/p.name)
# Prove verifier exits nonzero against retained real failing suite.
r=subprocess.run([sys.executable,'-B',str(scratch/'755-verify-evidence.py')],cwd=root)
if r.returncode!=1:raise SystemExit('Verifier failed its negative check')
commands=['755-runtime.py','755-mutations.py']; handles=[];procs=[]
for n in commands:
    h=(out/(n+'.runner.log')).open('w',encoding='utf-8');handles.append(h)
    procs.append(subprocess.Popen([sys.executable,'-B',str(scratch/n)],cwd=root,stdout=h,stderr=subprocess.STDOUT))
runner_codes=[]
for n,p in zip(commands,procs):
    code=p.wait();runner_codes.append(code);print(json.dumps({'script':n,'exit_code':code}),flush=True)
for h in handles:h.close()
# Read helper JSON results to ensure inner failures are caught even if subprocess exit was 0.
allcodes=[]
for name,results_file in [('755-build-final.py',out/'build-results.json'),('755-verify-evidence.py',None),('755-gates.py',out/'gate-results.json')]:
    p=subprocess.run([sys.executable,'-B',str(scratch/name)],cwd=root);allcodes.append(p.returncode)
    print(json.dumps({'script':name,'exit_code':p.returncode}),flush=True)
    if results_file and results_file.exists():
        data=json.loads(results_file.read_text(encoding='utf-8'))
        if name=='755-build-final.py':
            import re
            for row in data:
                if row['exit_code']!=0 or not re.search(r'^\s*0 (?:Warning\(s\)|Uyarı)\s*$', '\n'.join(row['summary']), re.M) or not re.search(r'^\s*0 (?:Error\(s\)|Hata)\s*$', '\n'.join(row['summary']), re.M):
                    allcodes.append(1);break
        elif name=='755-gates.py':
            for row in data:
                if row['exit_code']!=0:
                    allcodes.append(1);break
            # Also check regression preservation if present in same file
            if 'regression_preserved' in data and not data['regression_preserved']:
                allcodes.append(1)
# Repeated actual DB regression after rebuilding current sources, never stale assembly.
conf=json.loads(subprocess.check_output(['docker','inspect','alkaros-test-pg'],text=True))[0];settings=dict(s.split('=',1) for s in conf['Config']['Env'] if '=' in s)
dotnet='C:/Users/semih/.dotnet/dotnet.exe';env=os.environ.copy();env['PATH']=str(Path(dotnet).parent)+os.pathsep+env['PATH'];env['DOTNET_ROOT']=str(Path(dotnet).parent)
env.update(ALKAROS_TEST_PG_HOST='localhost',ALKAROS_TEST_PG_PORT=conf['NetworkSettings']['Ports']['5432/tcp'][0]['HostPort'],ALKAROS_TEST_PG_USER=settings.get('POSTGRES_USER','postgres'))
if settings.get('POSTGRES_PASSWORD'):env['ALKAROS_TEST_PG_PASSWORD']=settings['POSTGRES_PASSWORD']
rows=[]
for i in range(20):
    d=out/'order-regression-results'/str(i+1);d.mkdir(parents=True,exist_ok=True)
    args=[dotnet,'test','tests/Modules/Billing/SplitDesign/ALKAROS.Billing.SplitDesign.Tests.csproj','-c','Debug','--no-build','--artifacts-path',str(out/'artifacts'),'--filter','FullyQualifiedName~OperationalReplaceRejectsStaleAllocationSetWithoutPartialMutation|FullyQualifiedName~OperationalReplaceValidatesSeatAgainstBillTable','--results-directory',str(d),'--logger','trx;LogFileName=run.trx']
    with (d/'run.log').open('w',encoding='utf-8') as f:p=subprocess.run(args,cwd=root,env=env,stdout=f,stderr=subprocess.STDOUT)
    tree=ET.parse(d/'run.trx');c=tree.find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters').attrib
    valid=p.returncode==0 and c['executed']=='2' and c['passed']=='2'
    rows.append({'iteration':i+1,'command':args,'exit_code':p.returncode,'counters':c,'valid':valid})
    if not valid:break
(out/'order-regression-results.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
valid=len(rows)==20 and all(r['valid'] for r in rows) and not any(runner_codes+allcodes)
print(json.dumps({'order_repeats':len(rows),'all_passed':valid}),flush=True)
raise SystemExit(0 if valid else 1)
