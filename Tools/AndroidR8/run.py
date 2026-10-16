"""Check production R8 helpers and, optionally, compile representative JNI bridges with real R8."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root', type=Path, required=True)
p.add_argument('--r8-jar', type=Path)
a = p.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[1]
editor = root / 'Assets/Alterverse/Editor'
runtime = sorted((a.dotnet_root / 'shared/Microsoft.NETCore.App').iterdir())[-1]
sdk = sorted((a.dotnet_root / 'sdk').iterdir())[-1]
with tempfile.TemporaryDirectory(prefix='df-r8-regression-') as tmp:
    tmp = Path(tmp)
    output = tmp / 'R8Regression.dll'
    response = tmp / 'compile.rsp'
    response.write_text('\n'.join(['-nologo', '-define:UNITY_EDITOR,UNITY_ANDROID', '-target:exe', '-out:' + str(output),
        *['-r:' + str(path) for path in runtime.glob('*.dll')], str(here / 'Regression.cs'),
        str(editor / 'AndroidReleaseBuild.cs'), str(editor / 'AndroidR8Optimization.cs')]))
    subprocess.run([str(a.dotnet_root / 'dotnet'), str(sdk / 'Roslyn/bincore/csc.dll'), '@' + str(response)], check=True)
    output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
    subprocess.run([str(a.dotnet_root / 'dotnet'), str(output)], cwd=root, check=True)
    if a.r8_jar:
        classes = tmp / 'classes'
        classes.mkdir()
        names = ['com.google.games.bridge.FixtureBridge', 'com.google.android.gms.games.FixtureClient',
                 'com.google.android.gms.tasks.FixtureTask', 'com.android.billingclient.api.FixtureBilling',
                 'com.android.billingclient.api.FixtureListener', 'game.internal.Unused']
        sources = []
        for name in names:
            package, _, short = name.rpartition('.')
            path = tmp / (name.replace('.', '/') + '.java')
            path.parent.mkdir(parents=True, exist_ok=True)
            body = ('public interface FixtureListener { void onResponse(); }' if short == 'FixtureListener' else
                    f'public class {short} {{ public {short}() {{}} public int entryPoint() {{ return 42; }} }}')
            path.write_text(f'package {package};\n' + body)
            sources.append(str(path))
        compiler = ['javac'] if shutil.which('javac') else ['java', '-m', 'jdk.compiler/com.sun.tools.javac.Main']
        subprocess.run([*compiler, '-d', str(classes), *sources], check=True)
        input_jar = tmp / 'input.jar'
        with zipfile.ZipFile(input_jar, 'w') as z:
            for path in classes.rglob('*.class'):
                z.write(path, path.relative_to(classes))
        output_jar = tmp / 'optimized.jar'
        java_home = Path(shutil.which('java')).resolve().parents[1]
        cmd = ['java', '-cp', str(a.r8_jar.resolve()), 'com.android.tools.r8.R8', '--release', '--classfile',
               '--lib', str(java_home), '--pg-conf', str(root / 'Assets/Plugins/Android/proguard-user.txt'),
               '--output', str(output_jar), str(input_jar)]
        result = subprocess.run(cmd, capture_output=True, text=True)
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
        with zipfile.ZipFile(output_jar) as z:
            actual = set(z.namelist())
        for name in names[:-1]:
            assert name.replace('.', '/') + '.class' in actual, name
            javap = ['javap'] if shutil.which('javap') else ['java', '-m', 'jdk.jdeps/com.sun.tools.javap.Main']
            public_api = subprocess.check_output([*javap, '-classpath', str(output_jar), name], text=True)
            assert ('onResponse' if name.endswith('FixtureListener') else 'entryPoint') in public_api, name
        assert 'game/internal/Unused.class' not in actual, 'unreferenced code must still be removed'
        print('Real R8 check passed: 5 JNI API/bridge classes and callback names retained; unrelated unused class removed.')
