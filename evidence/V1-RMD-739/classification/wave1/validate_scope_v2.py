from pathlib import Path
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[4]
HERE=Path(__file__).resolve().parent

def load(name):
    return json.loads((HERE/name).read_text(encoding='utf-8'))

def validate_scope(groups, candidates):
    expected={g['id'] for g in candidates}
    actual={g['id'] for g in groups}
    assert len(groups)==len(actual) and actual==expected, 'Scope group coverage differs'
    for group in groups:
        assert group['readiness'] in {'scope-reviewed','decision-blocked','scope-blocked','historical-regression-only','verification-only','non-defect-review'},group['id']
        writes=group['write_paths'];reads=group['read_only_paths']
        assert len(writes)==len(set(writes)),group['id']
        assert not set(writes)&set(reads),group['id']
        for path in writes+reads:
            assert (ROOT/path).is_file(),(group['id'],path)
            assert '..' not in Path(path).parts and not Path(path).is_absolute(),path
        assert set(group['dependency_groups'])<=expected,group['id']
        assert group['id'] not in group['dependency_groups'],group['id']
        if group['readiness']=='scope-reviewed':
            assert not group['scope_blockers'],group['id']
            assert writes or group['proposed_new_paths'],group['id']
        if group['readiness'] in {'scope-blocked','decision-blocked'}:
            assert group['scope_blockers'],group['id']
    by_id={g['id']:g for g in groups}
    def visit(identifier,stack):
        assert identifier not in stack,stack+[identifier]
        for dep in by_id[identifier]['dependency_groups']:
            visit(dep,stack+[identifier])
    for identifier in by_id:
        visit(identifier,[])

if __name__=='__main__':
    candidates=load('GOREV_ADAYLARI.json')
    baseline=load('KAPSAM_TEMEL_V2.json')
    assert len(candidates)==baseline['candidate_groups']==62
    assert baseline['source_total']==len(baseline['wave1_source_ids'])+len(baseline['remaining_source_ids'])==1376
    assert not set(baseline['wave1_source_ids'])&set(baseline['remaining_source_ids'])
    assert baseline['remaining_count']==len(baseline['remaining_source_ids'])==1322
    assert [d['id'] for d in baseline['new_items_without_original_parent']]==['PAY-012']
    assert not baseline['source_hash_mismatches']
    result={'result':'PASS','tier':'planning validation, not application tests','source_total':1376,'wave1_sources':54,'remaining':1322,'scope_review':'pending'}
    scope=HERE/'KESIN_YUZEY_V3.json'
    if scope.exists():
        data=json.loads(scope.read_text(encoding='utf-8'))
        validate_scope(data['groups'],candidates)
        summary=data['summary']
        assert summary['candidate_groups']==len(data['groups'])
        import collections
        assert summary['readiness_counts']==dict(collections.Counter(g['readiness'] for g in data['groups']))
        assert data['current_parent_closures']['F0031']['closure_status']=='kismi-kalan-kusur-var'
        for path,sha in data['source_hashes'].items():
            assert hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==sha,path
        for group in data['groups']:
            assert group['scope_is_write_authorization'] is False,group['id']
            if group['readiness']=='historical-regression-only':
                assert not any(p.startswith(('src/','database/')) for p in group['write_paths']),group['id']
        assert next(g for g in data['groups'] if g['id']=='RG-SELF-REVOKE')['readiness']=='decision-blocked'
        conflicts=load('YAZMA_CAKISMALARI_V3.json')
        expected={}
        for index,left in enumerate(data['groups']):
            for right in data['groups'][index+1:]:
                paths=sorted(set(left['write_paths'])&set(right['write_paths']))
                if paths:expected[tuple(sorted([left['id'],right['id']]))]=paths
        assert {tuple(sorted([c['left'],c['right']])):c['shared_write_paths'] for c in conflicts}==expected
        assert len(conflicts)==len(expected)==summary.get('shared_write_conflict_pairs',summary.get('shared_write_pairs'))
        result['readiness_counts']=summary['readiness_counts']
        result['scope_review']='structure-validated; semantic/ownership gaps tracked separately'
    print(json.dumps(result,ensure_ascii=False,indent=2))
