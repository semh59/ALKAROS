from pathlib import Path
import subprocess,json,hashlib,sys,datetime
root=Path('D:/project/ALKAROS');out=root/'evidence/V1-RMD-755';base='4eca31daab3c5e065d7e31d9a237665c75ce71d5';rows=[]
def run(name,args):
    p=subprocess.run(args,cwd=root,capture_output=True)
    log=out/(name+'.log');log.write_bytes(p.stdout+p.stderr)
    rows.append({'name':name,'command':args,'exit_code':p.returncode,'log_sha256':hashlib.sha256(log.read_bytes()).hexdigest()})
    return p
ancestor=run('commit-ancestor',['git','merge-base','--is-ancestor',base,'HEAD'])
count=run('commit-count',['git','rev-list','--count',base+'..HEAD'])
delta=run('committed-paths',['git','diff','--name-status','--find-renames',base+'...HEAD'])
scope=run('committed-scope',['python','-B','tools/task-scope/task_scope_tool.py','--task-id','V1-RMD-755','--diff-base',base,'--format','json'])
pending=run('postcommit-delta',['python','-B','C:/Users/semih/AppData/Local/hermes/profiles/alkaros/cache/scratch/755-close-delta.py'])
valid=all(r['exit_code']==0 for r in rows) and int(count.stdout.strip())>0 and bool(delta.stdout.strip())
report={'base':base,'head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'nonempty_range':int(count.stdout.strip())>0 and bool(delta.stdout.strip()),'commands':rows,'valid':valid,'utc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
(out/'postcommit-proof.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(report));sys.exit(0 if valid else 1)
