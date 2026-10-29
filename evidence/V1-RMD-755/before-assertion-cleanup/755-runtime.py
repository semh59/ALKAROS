from pathlib import Path
import os, subprocess, json, hashlib, xml.etree.ElementTree as ET
root=Path('D:/project/ALKAROS'); out=root/'evidence/V1-RMD-755'; dotnet='C:/Users/semih/.dotnet/dotnet.exe'
c=json.loads(subprocess.check_output(['docker','inspect','alkaros-test-pg'],text=True))[0]
settings=dict(s.split('=',1) for s in c['Config']['Env'] if '=' in s)
env=os.environ.copy(); env['PATH']=str(Path(dotnet).parent)+os.pathsep+env['PATH'];env['DOTNET_ROOT']=str(Path(dotnet).parent)
env.update(ALKAROS_TEST_PG_HOST='localhost',ALKAROS_TEST_PG_PORT=c['NetworkSettings']['Ports']['5432/tcp'][0]['HostPort'],ALKAROS_TEST_PG_USER=settings.get('POSTGRES_USER','postgres'))
if settings.get('POSTGRES_PASSWORD'):env['ALKAROS_TEST_PG_PASSWORD']=settings['POSTGRES_PASSWORD']
results=[]
for name,project in [('splitdesign','tests/Modules/Billing/SplitDesign/ALKAROS.Billing.SplitDesign.Tests.csproj'),('host-billing','tests/Host/Experience/Billing/ALKAROS.Host.Experience.Billing.Tests.csproj')]:
    log=out/(name+'-full.log'); args=[dotnet,'test',project,'-c','Debug','--artifacts-path',str(out/'artifacts'),'--results-directory',str(out/(name+'-results')),'--logger','trx;LogFileName=full.trx']
    with log.open('w',encoding='utf-8') as f: p=subprocess.run(args,cwd=root,env=env,stdout=f,stderr=subprocess.STDOUT)
    trx=out/(name+'-results/full.trx'); counters=None; testnames=[]
    if trx.exists():
        tree=ET.parse(trx); ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
        counters=tree.find('.//t:Counters',ns).attrib
        testnames=[{'name':e.attrib['testName'],'outcome':e.attrib['outcome']} for e in tree.findall('.//t:UnitTestResult',ns)]
    row={'name':name,'command':args,'exit_code':p.returncode,'log_sha256':hashlib.sha256(log.read_bytes()).hexdigest(),'counters':counters,'tests':testnames}
    results.append(row); (out/'execution-results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in row.items() if k not in ['tests','command']},ensure_ascii=False),flush=True)
    if p.returncode: print(log.read_text(encoding='utf-8',errors='replace')[-6000:],flush=True)
