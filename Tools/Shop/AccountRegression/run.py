"""Compile real account cache, wallet, journal and commerce coroutines against a delayed HTTP fixture.
No Google checkout, production database or live account is accessed.
"""
import argparse,json,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet-root',type=Path,required=True);p.add_argument('--negative-control',action='store_true');a=p.parse_args()
here=Path(__file__).resolve().parent;src=here.parents[2]/'Assets/Alterverse/Scripts'
with tempfile.TemporaryDirectory(prefix='df-account-regression-') as folder:
    folder=Path(folder);dll=folder/'Regression.dll';sdk=sorted((a.dotnet_root/'sdk').iterdir())[-1];refs=sorted((a.dotnet_root/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1];version=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1].name
    names=['Shop/PaidShardCommerce.cs','Profile/AccountProfilePrefs.cs','Profile/PlayerWallet.cs','Profile/ProfileTransaction.cs']
    files=[src/n for n in names]
    if a.negative_control:
        broken=folder/'ConcurrentCommerce.cs';broken.write_text(files[0].read_text().replace(' || Instance.fulfilling','').replace(' || fulfilling','').replace('||fulfilling',''));files[0]=broken
    rsp=folder/'compile.rsp';rsp.write_text('\n'.join(['-nologo','-langversion:9','-nowarn:0649,0067','-out:'+str(dll),*['-r:'+str(x) for x in refs.glob('*.dll')],str(here/'EngineStubs.cs'),str(here/'Regression.cs'),*map(str,files)]))
    subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(rsp)],check=True)
    dll.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':version}}}))
    subprocess.run([str(a.dotnet_root/'dotnet'),str(dll)],check=True)
