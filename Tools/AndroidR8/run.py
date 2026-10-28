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
    optional_rules = tmp / 'standard-google-ads-rules.pro'
    subprocess.run([str(a.dotnet_root / 'dotnet'), str(output), str(optional_rules)], cwd=root, check=True)
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
        # Reproduce all 47 absent optional SDK types from the native failure.
        # The Unity plugin's upstream consumer rule keeps both bridge namespaces.
        optional_types = [line.strip() for line in (here / 'fixtures/nextgen-missing-classes.txt').read_text().splitlines()
                          if line.strip() and not line.startswith('#')]
        assert len(optional_types) == 47
        ads_sources = []
        def java_source(name, body):
            package, _, short = name.rpartition('.')
            path = tmp / 'ads-src' / (name.replace('.', '/') + '.java')
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('package ' + package + ';\n' + body)
            ads_sources.append(str(path))
        for name in optional_types:
            java_source(name, 'public class ' + name.rsplit('.', 1)[1] + ' {}')
        java_source('com.google.unity.ads.nextgen.FixtureBridge',
                    'public class FixtureBridge {\n' + '\n'.join(
                        'public ' + name + ' optional' + str(i) + ';'
                        for i, name in enumerate(optional_types)) + '\n}')
        java_source('com.google.android.gms.ads.MobileAds',
                    'public class MobileAds { public static int version() { return 35; } }')
        java_source('com.google.android.gms.ads.rewarded.RewardedAd',
                    'public class RewardedAd { public static int load() { return 7; } }')
        java_source('com.google.unity.ads.UnityRewardedAdCallback',
                    'public interface UnityRewardedAdCallback { void onUserEarnedReward(); }')
        java_source('com.google.unity.ads.UnityRewardedAd', '''public class UnityRewardedAd {
            public int load() { return com.google.android.gms.ads.MobileAds.version()
                + com.google.android.gms.ads.rewarded.RewardedAd.load(); }
            public void show(UnityRewardedAdCallback callback) { callback.onUserEarnedReward(); }
        }''')
        java_source('regression.Smoke', '''public class Smoke {
            public static void main(String[] args) throws Exception {
                Class<?> bridgeClass = Class.forName("com.google.unity.ads.UnityRewardedAd");
                Object bridge = bridgeClass.getConstructor().newInstance();
                if (((Integer) bridgeClass.getMethod("load").invoke(bridge)) != 42)
                    throw new AssertionError("Standard SDK entry point");
                Class<?> callbackClass = Class.forName("com.google.unity.ads.UnityRewardedAdCallback");
                final int[] rewards = {0};
                Object callback = java.lang.reflect.Proxy.newProxyInstance(
                    callbackClass.getClassLoader(), new Class<?>[]{callbackClass},
                    (proxy, method, arguments) -> {
                        if (method.getName().equals("onUserEarnedReward")) rewards[0]++;
                        return null;
                    });
                bridgeClass.getMethod("show", callbackClass).invoke(bridge, callback);
                if (rewards[0] != 1) throw new AssertionError("JNI callback name");
                System.out.println("PASS Standard SDK JNI names and reward callback; inactive Next-Gen bridge not loaded.");
            }
        }''')
        ads_classes = tmp / 'ads-classes'
        ads_classes.mkdir()
        subprocess.run([*compiler, '-d', str(ads_classes), *ads_sources], check=True)
        ads_input, missing_active = tmp / 'ads-input.jar', tmp / 'missing-active.jar'
        for jar, omit_active in ((ads_input, False), (missing_active, True)):
            with zipfile.ZipFile(jar, 'w') as z:
                for path in ads_classes.rglob('*.class'):
                    name = path.relative_to(ads_classes).as_posix()
                    if name.startswith('com/google/android/libraries/ads/mobile/sdk/'):
                        continue
                    if omit_active and name == 'com/google/android/gms/ads/MobileAds.class':
                        continue
                    z.write(path, name)
        consumer = tmp / 'google-consumer.pro'
        consumer.write_text('-keep class com.google.unity.ads.** { *; }\n'
                            '-keep public class com.google.android.gms.ads.** { public *; }\n'
                            '-keep class regression.Smoke { public static void main(java.lang.String[]); }\n')
        def ads_r8(jar, config, out):
            return subprocess.run(['java', '-cp', str(a.r8_jar.resolve()), 'com.android.tools.r8.R8',
                '--release', '--classfile', '--lib', str(java_home), '--pg-conf', str(config),
                '--pg-conf', str(consumer), '--output', str(out), str(jar)], capture_output=True, text=True)
        before = ads_r8(ads_input, root / 'Assets/Plugins/Android/proguard-user.txt', tmp / 'before.jar')
        assert before.returncode != 0, 'control must reproduce R8 missing optional definitions'
        for name in optional_types:
            assert name in before.stdout + before.stderr, 'missing native diagnostic: ' + name
        optimized_ads = tmp / 'ads-optimized.jar'
        fixed = ads_r8(ads_input, optional_rules, optimized_ads)
        assert fixed.returncode == 0, fixed.stdout + fixed.stderr
        subprocess.run(['java', '-cp', str(optimized_ads), 'regression.Smoke'], check=True)
        active_error = ads_r8(missing_active, optional_rules, tmp / 'missing-active-output.jar')
        assert active_error.returncode != 0, 'missing active Standard SDK must still fail'
        assert 'com.google.android.gms.ads.MobileAds' in active_error.stdout + active_error.stderr
        print('Real R8: old rules fail all 47 optional Next-Gen definitions; generated Standard-only rules pass; missing active SDK still fails.')
