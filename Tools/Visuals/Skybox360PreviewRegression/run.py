#!/usr/bin/env python3
"""Compile the whole editor preview and exercise state ownership; not a Unity rendering test."""
import argparse,json,subprocess,tempfile
from pathlib import Path

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--dotnet-root',type=Path,required=True);args=parser.parse_args()
    here=Path(__file__).resolve().parent;root=here.parents[2];sdk=args.dotnet_root.resolve()
    source=root/'Assets/Alterverse/Editor/Skybox360Preview/Skybox360MatchPreview.cs'
    code=source.read_text()
    assert code.startswith('#if UNITY_EDITOR\n') and code.rstrip().endswith('#endif')
    for prohibited in ('RuntimeInitializeOnLoad','InitializeOnLoad','RenderSettings.','SaveAssets(','SaveScene(','EditorBuildSettings','PlayerPrefs.'):
        assert prohibited not in code.replace('never touches RenderSettings.',''),prohibited
    assert 'ZTest Always' not in code and not list(source.parent.rglob('*.shader'))
    refs=sorted((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
    csc=sorted((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
    runtime=sorted((sdk/'shared/Microsoft.NETCore.App').iterdir())[-1].name
    with tempfile.TemporaryDirectory(prefix='df-skybox-match-') as tmp:
        output=Path(tmp)/'Regression.dll'
        subprocess.run([str(sdk/'dotnet'),str(csc),'-nologo','-langversion:9','-define:UNITY_EDITOR','-out:'+str(output),*['-r:'+str(p) for p in refs.glob('*.dll')],str(source),str(here/'EngineFixtures.cs'),str(here/'Regression.cs')],check=True)
        output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime}}}))
        subprocess.run([str(sdk/'dotnet'),str(output)],check=True)
        # The editor source compiles to an empty library in a player/server build.
        subprocess.run([str(sdk/'dotnet'),str(csc),'-nologo','-target:library','-out:'+str(Path(tmp)/'Player.dll'),*['-r:'+str(p) for p in refs.glob('*.dll')],str(source)],check=True)
    print('Player/server compile: preview contributes no runtime code. No native Unity or Android visual test was run.')

if __name__=='__main__':main()
