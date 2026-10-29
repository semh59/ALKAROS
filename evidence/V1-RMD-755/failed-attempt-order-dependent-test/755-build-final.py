from pathlib import Path
import os,subprocess,json,hashlib
root=Path('D:/project/ALKAROS'); out=root/'evidence/V1-RMD-755';dotnet=Path('C:/Users/semih/.dotnet/dotnet.exe');env=os.environ.copy();env['DOTNET_ROOT']=str(dotnet.parent);env['PATH']=str(dotnet.parent)+os.pathsep+env['PATH'];results=[]
for name,proj in [('splitdesign','tests/Modules/Billing/SplitDesign/ALKAROS.Billing.SplitDesign.Tests.csproj'),('host-billing','tests/Host/Experience/Billing/ALKAROS.Host.Experience.Billing.Tests.csproj')]:
    args=[str(dotnet),'build',proj,'-c','Debug','--no-restore','--artifacts-path',str(out/'artifacts')];p=out/(name+'-build.log')
    with p.open('w',encoding='utf-8') as f:r=subprocess.run(args,cwd=root,env=env,stdout=f,stderr=subprocess.STDOUT)
    text=p.read_text(encoding='utf-8',errors='replace');row={'name':name,'command':args,'exit_code':r.returncode,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'summary':text.splitlines()[-7:]};results.append(row);print(json.dumps(row,ensure_ascii=False))
(out/'build-results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
