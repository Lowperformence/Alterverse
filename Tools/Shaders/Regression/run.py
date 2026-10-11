#!/usr/bin/env python3
"""Audit shader assets and run production editor preflight against deterministic engine fixtures.
This does not replace Unity shader import, native GPU compilation, or Android device testing.
"""
import argparse, os, re, subprocess, tempfile
from pathlib import Path


def audit(root):
    errors = []
    settings = (root / 'ProjectSettings/EditorSettings.asset').read_text()
    if not re.search(r'^  m_AsyncShaderCompilation: 0$', settings, re.M):
        errors.append('Asynchronous editor compilation exposes cyan placeholder shaders.')
    shaders = sorted((root / 'Assets').rglob('*.shader'))
    shader_names, shader_guids = {}, {}
    pass_count = 0
    for path in shaders:
        text = path.read_text()
        match = re.search(r'\bShader\s+"([^"]+)"', text)
        if not match:
            errors.append(f'Missing shader declaration: {path}')
            continue
        name = match[1]
        if name in shader_names:
            errors.append(f'Duplicate shader name: {name}')
        shader_names[name] = path
        meta = Path(str(path) + '.meta')
        guid = re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text(), re.M) if meta.exists() else None
        if not guid or guid[1] in shader_guids:
            errors.append(f'Missing/duplicate shader GUID: {path}')
        else:
            shader_guids[guid[1]] = path
        programs = re.findall(r'HLSLPROGRAM(.*?)ENDHLSL|CGPROGRAM(.*?)ENDCG', text, re.S)
        if not programs:
            errors.append(f'No render programs: {path}')
        pass_count += len(programs)
        for hlsl, cg in programs:
            for stage in ('vertex', 'fragment'):
                if not re.search(r'#pragma\s+' + stage + r'\s+\w+', hlsl or cg):
                    errors.append(f'Missing {stage} entry point: {path}')
    for path in list((root / 'Assets').rglob('*.shader')) + list((root / 'Assets').rglob('*.hlsl')):
        for include in re.findall(r'#include\s+"(Assets/[^\"]+)"', path.read_text()):
            if not (root / include).is_file():
                errors.append(f'Missing shader include: {path}: {include}')
    materials = sorted((root / 'Assets').rglob('*.mat'))
    for path in materials:
        shader = re.search(r'm_Shader: \{fileID: (\d+), guid: ([a-f0-9]{32})', path.read_text())
        if shader and shader[2] != '0000000000000000f000000000000000' and shader[2] not in shader_guids:
            # Imported materials can point to package shaders. Only report missing local references.
            matches = [p for p in (root / 'Assets').rglob('*.meta') if f'guid: {shader[2]}' in p.read_text()]
            if not matches:
                errors.append(f'Unresolved material shader reference: {path}')
    if errors:
        raise AssertionError('\n'.join(errors))
    print(f'PASS: {len(shaders)} project shaders, {pass_count} render passes, local includes and {len(materials)} material references')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mono-root', type=Path, required=True)
    parser.add_argument('--roslyn', type=Path, required=True)
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    root = here.parents[2]
    audit(root)
    mono_root = args.mono_root.resolve()
    env = dict(os.environ, MONO_PATH=str(mono_root / 'usr/lib/mono/4.5') + ':' + str(mono_root / 'usr/lib/mono/4.5/Facades'),
               MONO_CFG_DIR=str(mono_root / 'etc'), LD_LIBRARY_PATH=str(mono_root / 'usr/lib') + ':' + str(mono_root / 'usr/lib/x86_64-linux-gnu'))
    mono = mono_root / 'usr/bin/mono-sgen'
    with tempfile.TemporaryDirectory(prefix='df-shader-regression-') as directory:
        binary = Path(directory) / 'Regression.exe'
        command = [str(mono), str(args.roslyn.resolve()), '-nologo', '-noconfig', '-langversion:9', '-define:UNITY_EDITOR',
                   '-r:' + str(mono_root / 'usr/lib/mono/4.5/System.dll'), '-r:' + str(mono_root / 'usr/lib/mono/4.5/System.Core.dll'),
                   '-out:' + str(binary), str(here / 'EngineStubs.cs'), str(here / 'Regression.cs'),
                   str(root / 'Assets/Alterverse/Editor/ProjectShaderValidation.cs')]
        subprocess.run(command, env=env, check=True)
        subprocess.run([str(mono), str(binary)], env=env, check=True)


if __name__ == '__main__':
    main()
