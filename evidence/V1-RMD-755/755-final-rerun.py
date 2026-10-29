from pathlib import Path
import subprocess,shutil,json,sys
root=Path('D:/project/ALKAROS');out=root/'evidence/V1-RMD-755';scratch=Path(__file__).parent
archive=out/'before-assertion-cleanup';archive.mkdir(exist_ok=True)
for p in list(out.iterdir()):
    if p.is_file():shutil.copy2(p,archive/p.name)
    elif p.name.endswith('-results'):shutil.copytree(p,archive/p.name,dirs_exist_ok=True)
for n in ['755-final-code-review.json','755-final-provenance-review.json']:
    shutil.copy2(scratch/n,out/('review-before-cleanup-'+n))
commands=['755-runtime.py','755-mutations.py']
procs=[subprocess.Popen([sys.executable,'-B',str(scratch/n)],cwd=root,stdout=(out/(n+'.runner.log')).open('w',encoding='utf-8'),stderr=subprocess.STDOUT) for n in commands]
for n,p in zip(commands,procs):print(json.dumps({'script':n,'exit_code':p.wait()}),flush=True)
for name in ['755-build-final.py','755-verify-evidence.py','755-gates.py']:
    p=subprocess.run([sys.executable,'-B',str(scratch/name)],cwd=root)
    print(json.dumps({'script':name,'exit_code':p.returncode}),flush=True)
