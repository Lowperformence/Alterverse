"""Compile actual startup updater and menu gate for Android, Editor and server."""
import argparse,json,subprocess,tempfile
from pathlib import Path
import xml.etree.ElementTree as ET
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet-root',type=Path,required=True);a=p.parse_args()
here=Path(__file__).resolve().parent;root=here.parents[2]
sdk=sorted((a.dotnet_root/'sdk').iterdir())[-1];runtime=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
specs=[x.attrib['spec'] for x in ET.parse(root/'Assets/Alterverse/Editor/StartupAppUpdateDependencies.xml').findall('.//androidPackage')]
assert specs==['com.google.android.play:app-update:2.1.0']
rules=(root/'Assets/Plugins/Android/proguard-user.txt').read_text()
assert all(x in rules for x in ['com.google.android.play.core.appupdate.**','com.google.android.gms.tasks.**'])
for target,defines in [('android','UNITY_ANDROID,STARTUP_UPDATE_FIXTURE'),('editor','UNITY_EDITOR,STARTUP_UPDATE_FIXTURE'),('server','UNITY_SERVER,STARTUP_UPDATE_FIXTURE')]:
 with tempfile.TemporaryDirectory(prefix='df-startup-update-') as directory:
  output=Path(directory)/'StartupUpdate.dll';response=Path(directory)/'compile.rsp'
  response.write_text('\n'.join(['-nologo','-nowarn:0649,0414','-langversion:9','-define:'+defines,'-out:'+str(output),*['-r:'+str(x) for x in runtime.glob('*.dll')],str(here/'EngineStubs.cs'),str(here/'Regression.cs'),str(root/'Tools/Profile/GoogleNameRegression/MenuStartupFixtures.cs'),str(root/'Assets/Alterverse/Scripts/Profile/StartupAppUpdateManager.cs'),str(root/'Assets/Alterverse/Scripts/UI/MainMenuAccountStartup.cs')]))
  subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(response)],check=True)
  output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
  subprocess.run([str(a.dotnet_root/'dotnet'),str(output)],check=True)
