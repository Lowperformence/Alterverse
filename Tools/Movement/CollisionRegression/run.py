"""Exercise the production Sweep method with deterministic query results; does not simulate Unity physics."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mono-root', type=Path)
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    production = here.parents[2] / 'Assets/Alterverse/Scripts/Characters/HeroLocomotion.cs'
    source = production.read_text()
    start = source.index('        private void Sweep(')
    opening = source.index('{', start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    # Compile the actual method, unchanged, so a regression exercises production filtering.
    method = source[start:end]
    wrapper = '''using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
public sealed class SweepUnderTest {
public Transform transform=new Transform();
private CharacterController controller=new CharacterController();
private readonly RaycastHit[] hits=new RaycastHit[32];
public void Run(out float allowed,out Vector3 normal,out Collider blocker) {
Sweep(Vector3.zero,new Vector3(1,0,0),2f,out allowed,out normal,out blocker);
}
''' + method + '\n}\n'
    env = os.environ.copy()
    if args.mono_root:
        root = args.mono_root.resolve()
        runtime = str(root / 'usr/bin/mono-sgen')
        compiler = [runtime, str(root / 'usr/lib/mono/4.5/mcs.exe')]
        env['MONO_PATH'] = str(root / 'usr/lib/mono/4.5')
        env['MONO_CFG_DIR'] = str(root / 'etc')
    else:
        runtime, mcs = shutil.which('mono'), shutil.which('mcs')
        if not runtime or not mcs:
            parser.error('Mono runtime/compiler missing. Install Mono or pass --mono-root.')
        compiler = [mcs]
    with tempfile.TemporaryDirectory(prefix='alterverse-collision-') as temporary:
        temp = Path(temporary)
        (temp / 'Sweep.cs').write_text(wrapper)
        binary = temp / 'Regression.exe'
        subprocess.run(compiler + ['-langversion:latest', '-out:' + str(binary),
                       str(temp / 'Sweep.cs'), str(here / 'Regression.cs')], check=True, env=env)
        subprocess.run([runtime, str(binary)], check=True, env=env)


if __name__ == '__main__':
    main()
