"""Compile the real weather runtime and check profiles, readability masks, budgets and ownership."""
import argparse,json,re,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root',type=Path,required=True)
a=p.parse_args();here=Path(__file__).resolve().parent;root=here.parents[2]
sdk=sorted((a.dotnet_root/'sdk').iterdir())[-1]
runtime=sorted((a.dotnet_root/'shared/Microsoft.NETCore.App').iterdir())[-1]
arena_source=(root/'Assets/Alterverse/Scripts/Match/ArenaVariantRuntime.cs').read_text()
actual=re.search(r'enum ArenaVariant\s*\{([^}]+)',arena_source).group(1)
arena_names={part.strip().split('=')[0].strip() for part in actual.split(',')}
weather=(root/'Assets/Alterverse/Scripts/Visuals/PlanetWeatherRuntime.cs').read_text()
assert arena_names==set(re.findall(r'case ArenaVariant\.(\w+):',weather)), 'Every current arena needs its own explicit weather profile'
fixtures=(here/'Fixtures.cs').read_text()
fixture_names={part.strip() for part in re.search(r'enum ArenaVariant\s*\{([^}]+)',fixtures).group(1).split(',')}
assert arena_names==fixture_names, 'Fixture arena catalog must match production'
shader=(root/'Assets/Resources/DF_PlanetWeather.shader').read_text()
assert 'ZWrite Off' in shader and 'ZTest LEqual' in shader
assert 'smoothstep(39.0*_ArenaStretch,46.0*_ArenaStretch' in shader
assert 'smoothstep(25.0,32.0' in shader
assert 'input.color.a*falloff*falloff*mask' in shader
with tempfile.TemporaryDirectory(prefix='df-weather-') as temp:
 temp=Path(temp);output=temp/'Regression.dll';rsp=temp/'compile.rsp'
 files=[here/'Fixtures.cs',here/'Regression.cs',root/'Assets/Alterverse/Scripts/Visuals/PlanetWeatherRuntime.cs']
 rsp.write_text('\n'.join(['-nologo','-target:exe','-langversion:9','-out:'+str(output),*['-r:'+str(f) for f in runtime.glob('*.dll')],*map(str,files)]))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(rsp)],check=True)
 output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime.name}}}))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(output)],check=True)
 # UNITY_SERVER must also compile with the installer excluded.
 server_rsp=temp/'server.rsp'
 server_rsp.write_text(rsp.read_text().replace('-target:exe','-target:library\n-define:UNITY_SERVER').replace(str(output),str(temp/'Server.dll')))
 subprocess.run([str(a.dotnet_root/'dotnet'),str(sdk/'Roslyn/bincore/csc.dll'),'@'+str(server_rsp)],check=True)
