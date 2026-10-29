from pathlib import Path
import subprocess, json, shutil, hashlib, os, xml.etree.ElementTree as ET, datetime
root=Path('D:/project/ALKAROS'); out=root/'evidence/V1-RMD-755'; sandbox=out/'isolated'; sandbox.mkdir(exist_ok=True)
paths=subprocess.check_output(['git','ls-files','-z'],cwd=root).decode().split('\0')
selected=[p for p in paths if p and (p.startswith(('src/','tests/','database/','build/','.config/')) or '/' not in p) and not p.endswith('.partial')]
for rel in selected:
    p=root/rel
    if not p.is_file(): continue
    target=sandbox/rel; target.parent.mkdir(exist_ok=True,parents=True); shutil.copy2(p,target)
source_hashes={rel:hashlib.sha256((root/rel).read_bytes()).hexdigest() for rel in selected if (root/rel).is_file()}
(out/'isolated-copy-manifest.json').write_text(json.dumps(source_hashes,indent=2),encoding='utf-8')
dotnet='C:/Users/semih/.dotnet/dotnet.exe'
c=json.loads(subprocess.check_output(['docker','inspect','alkaros-test-pg'],text=True))[0]
settings=dict(s.split('=',1) for s in c['Config']['Env'] if '=' in s)
env=os.environ.copy(); env['PATH']=str(Path(dotnet).parent)+os.pathsep+env['PATH']; env['DOTNET_ROOT']=str(Path(dotnet).parent)
env.update(ALKAROS_TEST_PG_HOST='localhost',ALKAROS_TEST_PG_PORT=c['NetworkSettings']['Ports']['5432/tcp'][0]['HostPort'],ALKAROS_TEST_PG_USER=settings.get('POSTGRES_USER','postgres'))
if settings.get('POSTGRES_PASSWORD'): env['ALKAROS_TEST_PG_PASSWORD']=settings['POSTGRES_PASSWORD']
projects={'domain':'tests/Modules/Billing/SplitDesign/ALKAROS.Billing.SplitDesign.Tests.csproj','host':'tests/Host/Experience/Billing/ALKAROS.Host.Experience.Billing.Tests.csproj'}
filters={'domain':'FullyQualifiedName~AmountAndCustomSplitsUseAdjustedTotalsOrOriginalTotalsWhenNull','host':'FullyQualifiedName~ADiscountedBillPersistsAdjustedTotalsForEqualAndCustomSplits|FullyQualifiedName~AZeroNetAdjustmentAllowsItemSplitWhenPayableAndTaxAreUnchanged'}
results=[]
def run(name,kind):
    log=out/(name+'.log'); resultsdir=out/(name+'-results')
    args=[dotnet,'test',projects[kind],'-c','Debug','--artifacts-path',str(sandbox/'artifacts'),'--results-directory',str(resultsdir),'--logger','trx;LogFileName=run.trx','--filter',filters[kind]]
    with log.open('w',encoding='utf-8') as f: process=subprocess.run(args,cwd=sandbox,env=env,stdout=f,stderr=subprocess.STDOUT)
    trx=resultsdir/'run.trx'; counters={}; failures=[]; names=[]
    if trx.exists():
        tree=ET.parse(trx); ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
        counters=tree.find('.//t:Counters',ns).attrib
        for e in tree.findall('.//t:UnitTestResult',ns):
            names.append(e.attrib['testName'])
            if e.attrib.get('outcome')=='Failed': failures.append({'name':e.attrib['testName'],'message':e.findtext('.//t:Message',namespaces=ns)})
    row={'name':name,'command':args,'cwd':str(sandbox),'exit_code':process.returncode,'log_sha256':hashlib.sha256(log.read_bytes()).hexdigest(),'counters':counters,'failures':failures,'test_names':names}
    results.append(row)
    (out/'guard-mutation-results.json').write_text(json.dumps({'results':results,'production_mutated':False},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'name':name,'exit_code':process.returncode,'counters':counters,'failures':failures[:2]},ensure_ascii=False),flush=True)
    return row
for kind in projects:
    row=run('isolated-baseline-'+kind,kind)
    if row['exit_code'] or int(row['counters'].get('passed',0)) != (8 if kind=='domain' else 3): raise RuntimeError('Isolated baseline did not exercise all expected cases')
engine='src/Modules/Billing/SplitDesign/SplitEngine.cs'; store='src/Host/Experience/Billing/BillingSplitStore.cs'
mutants=[
('amount-payable','domain',engine,'CreateAmountSplit','var totalPayable = adjustment?.AdjustedPayableAmount ?? bill.PayableAmount;','var totalPayable = bill.PayableAmount;'),
('amount-tax','domain',engine,'CreateAmountSplit','var totalTax = adjustment?.AdjustedTaxTotal ?? bill.TaxTotal;','var totalTax = bill.TaxTotal;'),
('custom-payable','domain',engine,'CreateCustomSplit','var totalPayable = adjustment?.AdjustedPayableAmount ?? bill.PayableAmount;','var totalPayable = bill.PayableAmount;'),
('custom-tax','domain',engine,'CreateCustomSplit','var totalTax = adjustment?.AdjustedTaxTotal ?? bill.TaxTotal;','var totalTax = bill.TaxTotal;'),
('amount-null','domain',engine,'CreateAmountSplit','adjustment?.AdjustedPayableAmount ?? bill.PayableAmount','adjustment?.AdjustedPayableAmount ?? 0m'),
('custom-null','domain',engine,'CreateCustomSplit','adjustment?.AdjustedPayableAmount ?? bill.PayableAmount','adjustment?.AdjustedPayableAmount ?? 0m'),
('equal-handoff','host',store,'SaveEqualAsync','owners.Count, owners, actorId, adjustment);','owners.Count, owners, actorId, null);'),
('custom-handoff','host',store,'SaveCustomAsync','            adjustment);','            null);'),
('item-unchanged-totals','host',store,'SaveItemsAsync','adjustment.AdjustedPayableAmount != bill.PayableAmount || adjustment.AdjustedTaxTotal != bill.TaxTotal','true')]
for name,kind,rel,method,old,new in mutants:
    p=sandbox/rel; original=p.read_bytes(); text=original.decode('utf-8'); start=text.index(' '+method+'('); end=text.index('\n    }\n',start)
    body=text[start:end]
    if body.count(old)!=1: raise RuntimeError('Mutation anchor mismatch: '+name)
    try:
        p.write_bytes((text[:start]+body.replace(old,new,1)+text[end:]).encode('utf-8'))
        row=run('mutant-'+name,kind)
        row['mutant_source']=rel; row['before_sha256']=hashlib.sha256(original).hexdigest();row['mutated_sha256']=hashlib.sha256(p.read_bytes()).hexdigest();row['replacement']={'old':old,'new':new}
        row['killed']=row['exit_code']==1 and int(row['counters'].get('failed',0))>0 and bool(row['failures'])
        if not row['killed']: raise RuntimeError('Mutant not killed by executed tests: '+name)
    finally: p.write_bytes(original)
for kind in projects:
    if run('isolated-restored-'+kind,kind)['exit_code']:raise RuntimeError('Restored baseline failed')
changed=[rel for rel,h in source_hashes.items() if hashlib.sha256((root/rel).read_bytes()).hexdigest()!=h]
(out/'guard-mutation-results.json').write_text(json.dumps({'results':results,'production_mutated':False,'root_source_changes':changed,'killed_mutants':sum(r.get('killed',False) for r in results),'completed_utc':datetime.datetime.now(datetime.timezone.utc).isoformat()},ensure_ascii=False,indent=2),encoding='utf-8')
print('Mutation proof finished',sum(r.get('killed',False) for r in results),'root source changes',changed,flush=True)
