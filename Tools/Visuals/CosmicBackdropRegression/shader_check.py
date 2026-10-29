#!/usr/bin/env python3
"""Compile production HLSL with Unity helper shims and render its fragment math on Mesa.

Needs glslangValidator, spirv-cross, moderngl, numpy and Pillow. This is not a
Unity/URP import, an Android driver test, or a mobile performance benchmark.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess
import tempfile
import numpy as np
from PIL import Image, ImageDraw
import moderngl


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--glslang', required=True)
    parser.add_argument('--spirv-cross', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[3]
    source = (root/'Assets/Alterverse/Resources/MapPanoramas/CosmicSky.shader').read_text()
    shared = source.split('HLSLINCLUDE')[1].split('ENDHLSL')[0]
    shared = re.sub(r'#include[^\n]+', '', shared)
    shim = '''
#define TEXTURE2D(name) Texture2D name
#define SAMPLER(name) SamplerState name
#define SAMPLE_TEXTURE2D(tex,sam,uv) tex.Sample(sam,uv)
#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };
cbuffer UnityPerFrame {float4 _TestViewport;};
float4 TransformObjectToHClip(float3 p){return float4(p,1);}
float4 GetScaledScreenParams(){return _TestViewport;}
'''
    vertex = '''#version 450
in vec2 p;layout(location=0) out vec2 input_uv;
void main(){gl_Position=vec4(p,0,1);input_uv=p*.5+.5;}
'''
    args.output.mkdir(parents=True, exist_ok=True)
    context = moderngl.create_standalone_context(backend='egl')
    programs = []
    compiled = 0
    with tempfile.TemporaryDirectory(prefix='df-cosmic-shader-') as temporary:
        temporary = Path(temporary)
        for top in (0, 1):
            for index, body in enumerate(re.findall(r'HLSLPROGRAM(.*?)ENDHLSL', source, re.S)):
                fragment = re.search(r'#pragma fragment (\w+)', body)[1]
                body = re.sub(r'#pragma[^\n]+', '', body)
                hlsl = temporary/f'pass{index}-top{top}.hlsl'
                hlsl.write_text(f'#define UNITY_UV_STARTS_AT_TOP {top}\n'+shim+shared+body)
                for stage, entry in [('vert', 'Vert'), ('frag', fragment)]:
                    spirv = temporary/f'pass{index}-top{top}.{stage}.spv'
                    subprocess.run([args.glslang, '-D', '-V', '--auto-map-bindings', '--auto-map-locations',
                                    '-S', stage, '-e', entry, str(hlsl), '-o', str(spirv)], check=True, capture_output=True)
                    compiled += 1
                if top == 0:
                    glsl = temporary/f'pass{index}.frag'
                    subprocess.run([args.spirv_cross, str(spirv), '--version', '450', '--no-420pack-extension',
                                    '--glsl-emit-ubo-as-plain-uniforms', '--output', str(glsl)], check=True)
                    programs.append(context.program(vertex_shader=vertex, fragment_shader=glsl.read_text()))
    vertices = context.buffer(np.array([-1,-1, 3,-1, -1,3], dtype='f4').tobytes())
    arrays = [context.simple_vertex_array(program, vertices, 'p') for program in programs]
    def set_value(program, name, value):
        for key in program:
            if key.endswith('.'+name):
                program[key].value = value
                return
    def texture(path, srgb):
        with Image.open(path) as image:
            image = image.convert('RGB').transpose(Image.Transpose.FLIP_TOP_BOTTOM)
            result = context.texture(image.size, 3, image.tobytes(), internal_format=0x8C41 if srgb else None)
        result.filter = (moderngl.LINEAR, moderngl.LINEAR)
        result.repeat_x = result.repeat_y = not srgb
        return result
    noise = texture(root/'Assets/Alterverse/Resources/MapPanoramas/CosmicNoise.png', False)
    def render(program_index, art, aspect, exposure, seed, time, meteor=(0,0,0,.16)):
        width = 960
        height = round(width/aspect)
        target = context.texture((width,height), 4, dtype='f4')
        framebuffer = context.framebuffer([target]);framebuffer.use()
        program = programs[program_index]
        art.use(0);noise.use(1)
        program['SPIRV_Cross_Combined_Panoramasampler_Panorama'].value = 0
        program['SPIRV_Cross_Combined_CosmicNoisesampler_CosmicNoise'].value = 1
        for name, value in {'_ImageAspect':2., '_FrameAspect':width/height,
                            '_CosmicParams':(exposure,seed,time,.018), '_DustColor':(.4,.6,1,1),
                            '_MeteorA':meteor,'_MeteorB':(0,0,0,.16), '_TestViewport':(width,height,0,0)}.items():
            set_value(program,name,value)
        arrays[program_index].render(moderngl.TRIANGLES)
        data = np.frombuffer(framebuffer.read(components=4,dtype='f4'),dtype='f4').reshape(height,width,4)[::-1].copy()
        framebuffer.release();target.release()
        assert np.isfinite(data).all() and (data[...,3] == 1).all()
        assert data[...,:3].min() >= 0 and data[...,:3].max() <= 1
        return data
    def display(data):
        linear = data[...,:3]
        srgb = np.where(linear <= .0031308, linear*12.92, 1.055*linear**(1/2.4)-.055)
        return Image.fromarray(np.round(np.clip(srgb,0,1)*255).astype('u1'))
    manifest = json.loads((root/'ArtSource/CosmicSkies/manifest.json').read_text())
    catalog = (root/'Assets/Alterverse/Scripts/Visuals/CosmicSkyMaterial.cs').read_text()
    profiles = {name:(float(exposure.rstrip('f')),float(seed)) for name,seed,exposure in re.findall(
        r'ArenaVariant\.(\w+)=>Arena\("\w+",(\d+),new\([^)]*\),([.\d]+f),',catalog)}
    profiles.update(Menu=(.92,173), Dark00=(.72,97))
    sheet = Image.new('RGB',(1280,16+4*188),(6,9,18));draw = ImageDraw.Draw(sheet)
    draw.text((12,3),'Cosmic shader preview - isolated Mesa renderer, not a Unity screenshot',fill=(205,216,236))
    checks = 0
    for index, entry in enumerate(manifest['artwork']):
        name = entry['name'];art = texture(root/entry['path'], True);exposure,seed = profiles[name]
        frame = render(1,art,2,exposure,seed,0)
        later = render(1,art,2,exposure,seed,70)
        assert np.abs(frame[...,:3]-later[...,:3]).mean() < .006
        streak = render(1,art,2,exposure,seed,0,(.45,.85,.8,.16))
        altered = np.abs(frame[...,:3]-streak[...,:3]).max(axis=-1) > .03
        assert .00001 < altered.mean() < .02
        sky = render(0,art,16/9,exposure,seed,0)
        menu = render(1,art,16/9,exposure,seed,0)
        assert np.abs(sky-menu).max() < .003
        render(0,art,2.22,exposure,seed,20)
        column,row = index%4,index//4
        sheet.paste(display(streak).resize((320,160),Image.Resampling.LANCZOS),(column*320,16+row*188))
        draw.text((column*320+10,16+row*188+164),name,fill=(205,216,236))
        checks += 6;art.release()
    sheet.save(args.output/'shader-preview.jpg',quality=92)
    report = {'hlsl_variants_compiled':compiled,'fragment_checks':checks,
              'renderer':context.info['GL_RENDERER'],'artwork_count':len(manifest['artwork']),
              'limitations':'Unity math helpers shimmed; no native URP, ASTC, Android driver or mobile performance test.'}
    (args.output/'shader-validation.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
