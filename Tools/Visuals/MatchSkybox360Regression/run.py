#!/usr/bin/env python3
"""Compile the shipped client/server sky plus its material builder; API fixtures, not GPU QA."""
import argparse,hashlib,json,subprocess,tempfile
from pathlib import Path

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--dotnet-root',type=Path,required=True);args=parser.parse_args()
    here=Path(__file__).resolve().parent;root=here.parents[2];sdk=args.dotnet_root.resolve()
    runtime=root/'Assets/Alterverse/Scripts/Visuals/MatchSkybox360.cs';editor=root/'Assets/Alterverse/Editor/MatchSkybox360Assets.cs'
    owner=(root/'Assets/Alterverse/Scripts/Visuals/CosmicArenaBackdrop.cs').read_text()
    assert 'if(matchSkybox.Load(transform))return;' in owner and 'matchSkybox?.RestoreCamera();' in owner and 'matchSkybox?.Dispose();' in owner
    source=runtime.read_text()
    for word in ('UnityEditor','AssetDatabase','PlayerPrefs','RenderSettings.','Shader.Find','ZTest Always'):assert word not in source,word
    assert 'SaveAssets(' not in editor.read_text()
    resource=root/'Assets/Alterverse/Resources/MatchSkybox360';manifest=json.loads((resource/'manifest.json').read_text())
    assert manifest['auto_start'] and manifest['player_build_enabled']
    for image in manifest['artwork']:assert hashlib.sha256((resource/image['file']).read_bytes()).hexdigest()==image['sha256']
    assert not (root/'Assets/Alterverse/Editor/Skybox360Preview').exists()
    refs=sorted((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1];csc=sorted((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1];version=sorted((sdk/'shared/Microsoft.NETCore.App').iterdir())[-1].name
    with tempfile.TemporaryDirectory(prefix='df-match-sky-') as tmp:
        for server in (False,True):
            output=Path(tmp)/('Server.dll' if server else 'Client.dll')
            command=[str(sdk/'dotnet'),str(csc),'-nologo','-langversion:9','-define:UNITY_EDITOR'+(',UNITY_SERVER' if server else ''),'-out:'+str(output),*['-r:'+str(p) for p in refs.glob('*.dll')],str(runtime),str(editor),str(here/'EngineFixtures.cs'),str(here/'Regression.cs')]
            subprocess.run(command,check=True)
            output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':version}}}))
            subprocess.run([str(sdk/'dotnet'),str(output)],check=True)
    print('Automatic regular-match activation and Android resource graph verified. Native Unity/Android visual QA remains unrun.')
if __name__=='__main__':main()
