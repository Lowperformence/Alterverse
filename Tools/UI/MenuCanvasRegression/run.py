#!/usr/bin/env python3
"""Compile and exercise production FrontEndCanvas with deterministic UI/engine/tween fixtures.
No Unity renderer, shader execution, native fonts, keyboard or Android build is involved.
A separate compile links the real installed DOTween DLL to verify its API signatures.
"""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--mono-root', required=True, type=Path)
p.add_argument('--roslyn', required=True, type=Path)
a = p.parse_args()
here = Path(__file__).resolve().parent
root = here.parents[2]
env = dict(os.environ, MONO_PATH=str(a.mono_root/'usr/lib/mono/4.5')+':'+str(a.mono_root/'usr/lib/mono/4.5/Facades'), MONO_CFG_DIR=str(a.mono_root/'etc'))
mono = a.mono_root/'usr/bin/mono-sgen'
compiler = [str(mono), str(a.roslyn), '-nologo', '-noconfig', '-langversion:9', '-r:'+str(a.mono_root/'usr/lib/mono/4.5/System.dll'), '-r:'+str(a.mono_root/'usr/lib/mono/4.5/System.Core.dll')]
source = root/'Assets/Alterverse/Scripts/UI/FrontEndCanvas.cs'
with tempfile.TemporaryDirectory(prefix='df-menu-canvas-') as temp:
    temp = Path(temp)
    binary = temp/'Regression.exe'
    subprocess.run(compiler+['-define:HEADLESS_TWEEN', '-out:'+str(binary), str(here/'EngineStubs.cs'), str(source), str(here/'Regression.cs')], env=env, check=True)
    subprocess.run([str(mono), str(binary)], env=env, check=True)
    engine = temp/'UnityEngine.dll'
    subprocess.run(compiler+['-target:library', '-out:'+str(engine), str(here/'EngineStubs.cs')], env=env, check=True)
    tween = root/'Assets/Plugins/Demigiant/DOTween/DOTween.dll'
    subprocess.run(compiler+['-target:library', '-out:'+str(temp/'Menu.dll'), '-r:'+str(engine), '-r:'+str(tween), str(source)], env=env, check=True)
    print('PASS production menu renderer compiles against the real DOTween 1.3.030 API.')
