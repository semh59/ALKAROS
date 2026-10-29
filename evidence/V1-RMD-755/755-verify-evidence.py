from pathlib import Path
import json, hashlib, subprocess, xml.etree.ElementTree as ET, datetime
root=Path('D:/project/ALKAROS');out=root/'evidence/V1-RMD-755'
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
checks=[]
def check(name, condition, details=None):
    checks.append({'name':name,'passed':bool(condition),'details':details})
results=json.loads((out/'guard-mutation-results.json').read_text(encoding='utf-8'))
check('nine_killed_mutants',results.get('killed_mutants')==9)
check('no_root_source_mutation',results.get('root_source_changes')==[] and results.get('production_mutated') is False)
for row in results['results']:
    check(row['name']+'-log-hash', hashlib.sha256((out/(row['name']+'.log')).read_bytes()).hexdigest()==row['log_sha256'])
    expected=1 if row['name'].startswith('mutant-') else 0
    check(row['name']+'-exit', row['exit_code']==expected)
    check(row['name']+'-tests-executed',int(row['counters'].get('executed','0')) in [3,8])
    if expected:
        messages=[x['message'] or '' for x in row['failures']]
        check(row['name']+'-behavior-not-setup',bool(messages) and not any(x in '\n'.join(messages) for x in ['Failed to connect','FileNotFoundException','FindRepositoryRoot','error CS']))
    trx=out/(row['name']+'-results/run.trx');tree=ET.parse(trx)
    check(row['name']+'-trx',tree.find('.//t:Counters',ns).attrib==row['counters'])
    for source_key,sha_key in [('before','before_sha256'),('mutated','mutated_sha256')]:
        if sha_key in row: check(row['name']+'-'+source_key+'-hash-shape',len(row[sha_key])==64)
full=json.loads((out/'execution-results.json').read_text(encoding='utf-8'))
for row in full:
    check(row['name']+'-full-log-hash',hashlib.sha256((out/(row['name']+'-full.log')).read_bytes()).hexdigest()==row['log_sha256'])
    check(row['name']+'-full-exit',row['exit_code']==0)
    if row['name']=='splitdesign':
        names=[r for r in row['tests'] if 'AmountAndCustomSplitsUseAdjustedTotalsOrOriginalTotalsWhenNull' in r['name']]
        check('eight-new-domain-cases-executed',len(names)==8 and all(r['outcome']=='Passed' for r in names),names)
    if row['name']=='host-billing':
        equalcustom=[r for r in row['tests'] if 'ADiscountedBillPersistsAdjustedTotalsForEqualAndCustomSplits' in r['name']]
        zero=[r for r in row['tests'] if 'AZeroNetAdjustmentAllowsItemSplitWhenPayableAndTaxAreUnchanged' in r['name']]
        regression=[r for r in row['tests'] if 'ATipAndAnEqualDiscountThatKeepThePayableButLowerTheTaxRefuseAnItemSplitWith409' in r['name']]
        check('new-host-cases-executed',len(equalcustom)==2 and len(zero)==1 and len(regression)==1 and all(r['outcome']=='Passed' for r in equalcustom+zero+regression),equalcustom+zero+regression)
check('full-host-not-pending',len(full)==2)
report={'checks':checks,'all_passed':all(r['passed'] for r in checks),'utc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
(out/'evidence-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'checks':len(checks),'all_passed':report['all_passed'],'failed':[c['name'] for c in checks if not c['passed']]}))
raise SystemExit(0 if report['all_passed'] else 1)
