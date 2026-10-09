#!/usr/bin/env python3
"""Compile the unchanged HeroEnergy HLSL and render controlled fixtures outside Unity.

Dependencies: numpy, Pillow, moderngl, glslangValidator, spirv-cross, spirv-val.
The substitute Core.hlsl supplies matrices/view math only. This does not validate
URP integration, Unity's platform variants or Android driver/performance behaviour.
"""
import argparse
import json
import math
from pathlib import Path
import re
import struct
import subprocess
import tempfile

import moderngl
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
SHADER = ROOT / "Assets/Alterverse/Resources/CombatVfx/HeroEnergy.shader"
CORE = """#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };
cbuffer PreviewCamera {
 float4x4 _PreviewObjectToClip;
 float4x4 _PreviewObjectToWorld;
 float4x4 _PreviewWorldToObject;
 float4 _PreviewCameraPosition;
};
float3 SafeNormalize(float3 p) { return p * rsqrt(max(dot(p,p),1e-8)); }
float3 TransformObjectToWorld(float3 p) { return mul(_PreviewObjectToWorld,float4(p,1)).xyz; }
float4 TransformObjectToHClip(float3 p) { return mul(_PreviewObjectToClip,float4(p,1)); }
float3 TransformObjectToWorldNormal(float3 n) { return SafeNormalize(mul(n,(float3x3)_PreviewWorldToObject)); }
float3 GetWorldSpaceNormalizeViewDir(float3 p) { return SafeNormalize(_PreviewCameraPosition.xyz-p); }
"""

def compile_program(ctx, shader, native, temp):
    source = shader.read_text().split("HLSLPROGRAM", 1)[1].split("ENDHLSL", 1)[0]
    source = re.sub(r"^\s*#pragma.*$", "", source, flags=re.M)
    source = source.replace('"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"', '"PreviewCore.hlsl"')
    source = source.replace('"Assets/Alterverse/Resources/CombatVfx/HeroEnergySurface.hlsl"', '"HeroEnergySurface.hlsl"')
    path = temp / (shader.parent.name + "-" + shader.stem + ".hlsl")
    path.write_text(source)
    (temp / "PreviewCore.hlsl").write_text(CORE)
    (temp / "HeroEnergySurface.hlsl").write_text((ROOT / "Assets/Alterverse/Resources/CombatVfx/HeroEnergySurface.hlsl").read_text())
    stages = []
    for stage, entry in (("vert", "vert"), ("frag", "frag")):
        spv = temp / (path.stem + "." + stage + ".spv")
        command = [str(native / "glslangValidator"), "-D", "-V", "--hlsl-iomap", "--auto-map-locations", "--auto-map-bindings", "-S", stage, "-e", entry, "-o", str(spv), str(path)]
        result = subprocess.run(command, text=True, capture_output=True)
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
        subprocess.run([str(native / "spirv-val"), str(spv)], check=True, capture_output=True)
        glsl = subprocess.check_output([str(native / "spirv-cross"), str(spv), "--version", "330"], text=True)
        glsl = re.sub(r"layout\(binding = \d+, std140\)(\s*uniform PreviewCamera)", r"layout(binding = 1, std140)\1", glsl)
        glsl = re.sub(r"layout\(binding = \d+, std140\)(\s*uniform UnityPerMaterial)", r"layout(binding = 0, std140)\1", glsl)
        # HLSL semantic names differ between stages in SPIRV-Cross GLSL. Match by location.
        if stage == "vert":
            names = re.findall(r"(?:layout\(location = (\d+)\) )?out (\w+) (\w+);", glsl)
            output_names = [name for _, _, name in names]
        else:
            names = re.findall(r"(?:layout\(location = (\d+)\) )?in (\w+) (\w+);", glsl)
            for (_, _, name), output_name in zip(names, output_names):
                glsl = re.sub(r"\b" + re.escape(name) + r"\b", output_name, glsl)
        stages.append(glsl)
    program = ctx.program(vertex_shader=stages[0], fragment_shader=stages[1])
    program["UnityPerMaterial"].binding = 0
    program["PreviewCamera"].binding = 1
    return program

def quad(vertices, a, b, c, d, color_a=(1,1,1,1), color_b=None):
    if color_b is None:
        color_b = color_a
    pts = np.array([a,b,c,d], dtype=np.float32)
    normal = np.cross(pts[1]-pts[0], pts[2]-pts[0])
    normal /= max(np.linalg.norm(normal), 1e-8)
    colors = (color_a,color_b,color_b,color_a)
    for k in (0,1,2,0,2,3):
        vertices.append((*pts[k], *colors[k], *normal))

def fixture(shape):
    v = []
    clear = (1,1,1,0)
    hot = (1.4,1.4,1.4,1)
    if shape == "plasma":
        # Same 24x12 sphere tessellation and vertex brightness as the runtime Orb body.
        for j in range(12):
            lo = -math.pi/2+j*math.pi/12
            hi = lo+math.pi/12
            for k in range(24):
                a = k*math.pi/12
                b = (k+1)*math.pi/12
                point = lambda lon, lat: np.array([math.cos(lon)*math.cos(lat), math.sin(lat), math.sin(lon)*math.cos(lat)])*.5
                ca = (.45+1.05*(math.sin(lo)*.5+.5),)*3+(1,)
                cb = (.45+1.05*(math.sin(hi)*.5+.5),)*3+(1,)
                quad(v,point(a,lo),point(a,hi),point(b,hi),point(b,lo),ca,cb)
    elif shape == "shield":
        # Runtime Shield side panels; base/cap rims are omitted in this material fixture.
        for k in range(6):
            a = k*math.pi/3+.08
            b = (k+1)*math.pi/3-.08
            p = np.array([math.cos(a),0,math.sin(a)])
            q = np.array([math.cos(b),0,math.sin(b)])
            up = np.array([0,1.2,0])
            quad(v,p*.84,q*.84,q*.72+up,p*.72+up,(1,1,1,.16))
            quad(v,p*.84,p*.88,p*.76+up,p*.72+up,(1,1,1,.9))
    elif shape == "ring":
        for k in range(64):
            a = k*math.pi/32
            b = (k+1)*math.pi/32
            p = np.array([math.sin(a),0,math.cos(a)])
            q = np.array([math.sin(b),0,math.cos(b)])
            quad(v,p*.9,p*.965,q*.965,q*.9,clear,hot)
            quad(v,p*.965,p*1.025,q*1.025,q*.965,hot,clear)
    else:
        # Runtime crossed Seed planes.
        quad(v,(0,0,-.6),(-.22,0,-.08),(0,0,.62),(.22,0,-.08),(1,1,1,.55),hot)
        quad(v,(0,0,-.6),(0,-.13,-.08),(0,0,.62),(0,.13,-.08),(1,1,1,.55),hot)
    return np.asarray(v, dtype=np.float32)

def matrices(shape, scale):
    angle = -.3 if shape == "plasma" else .65
    c,s = math.cos(angle),math.sin(angle)
    yaw = np.array([[c,0,s,0],[0,1,0,0],[-s,0,c,0],[0,0,0,1]], dtype=np.float32)
    tilt = .85 if shape in ("ring", "seed") else .18
    c,s = math.cos(tilt),math.sin(tilt)
    pitch = np.array([[1,0,0,0],[0,c,-s,0],[0,s,c,0],[0,0,0,1]], dtype=np.float32)
    model = pitch@yaw@np.diag([scale,scale*.97,scale*1.03,1])
    model[1,3] = -.42*scale if shape == "shield" else 0
    clip = np.diag([.90,.90,.10,1])@model
    return b"".join(np.asarray(m.T, dtype=np.float32).tobytes() for m in (clip,model,np.linalg.inv(model)))+struct.pack("4f",0,1.1,8,0)

def render(ctx, program, shape, hue, style, life, quality, polish=1, scale=1, size=192):
    material = ctx.buffer(struct.pack("8f", *hue, polish,life,style,quality))
    camera = ctx.buffer(matrices(shape,scale))
    material.bind_to_uniform_block(0)
    camera.bind_to_uniform_block(1)
    data = fixture(shape)
    buffer = ctx.buffer(data.tobytes())
    names = list(program)
    pos = next(n for n in names if n.endswith("position"))
    color = next(n for n in names if n.endswith("color"))
    normal = next((n for n in names if n.endswith("normal")), None)
    vao = ctx.vertex_array(program, [(buffer,"3f 4f 3f" if normal else "3f 4f 12x",pos,color,*([normal] if normal else []))])
    target = ctx.texture((size,size),4,dtype="f4")
    depth = ctx.depth_renderbuffer((size,size))
    frame = ctx.framebuffer([target],depth)
    frame.use()
    frame.clear(.018,.026,.042,0,depth=1)
    ctx.enable(moderngl.BLEND|moderngl.DEPTH_TEST)
    ctx.disable(moderngl.CULL_FACE)
    ctx.depth_mask = False
    # Unity's Blend SrcAlpha OneMinusSrcAlpha applies to RGB and alpha.
    ctx.blend_func = moderngl.SRC_ALPHA,moderngl.ONE_MINUS_SRC_ALPHA
    vao.render()
    image = np.frombuffer(frame.read(components=4,dtype="f4"),np.float32).reshape(size,size,4)[::-1].copy()
    for resource in (vao,buffer,material,camera,frame,depth,target):
        resource.release()
    return image

def main():
    p=argparse.ArgumentParser()
    p.add_argument("--native-bin",type=Path,required=True)
    p.add_argument("--baseline",type=Path,required=True)
    p.add_argument("--output",type=Path,required=True)
    args=p.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    ctx=moderngl.create_standalone_context(backend="egl")
    rows=[]
    report={"renderer":ctx.info["GL_RENDERER"],"scope":"Original HLSL compiled to SPIR-V/GLSL; substitute Unity Core matrices; controlled mesh fixtures. Not Unity or Android.","renders":0,"warning_max_error":0,"alpha_max_error":0}
    cases=(("plasma",1,(.23,.38,1,1)),("shield",2,(.15,.7,1,.82)),("ring",3,(.85,.22,1,1)),("seed",4,(.25,1,.38,1)))
    with tempfile.TemporaryDirectory() as temp:
        old=compile_program(ctx,args.baseline,args.native_bin,Path(temp))
        new=compile_program(ctx,SHADER,args.native_bin,Path(temp))
        for shape,style,hue in cases:
            row=[]
            for prog in (old,new):
                image=render(ctx,prog,shape,hue,style,.33,1)
                # Gentle display conversion only, no post-process glow added to the fixture.
                rgb=np.clip(image[:,:,:3],0,1)**(1/2.2)
                row.append(Image.fromarray((rgb*255).astype(np.uint8)))
            rows.append((shape,row))
            for life in (0,.1,.33,.65,.95):
                for quality in (.4,.45,.85,1):
                    for scale in (.22,1):
                        image=render(ctx,new,shape,hue,style,life,quality,scale=scale)
                        assert np.isfinite(image).all(),(shape,life,quality,scale)
                        assert image[:,:,3].min()>=-1e-6 and image[:,:,3].max()<=1.00001
                        reference=render(ctx,old,shape,hue,style,life,quality,scale=scale)
                        alpha_error=float(np.abs(image[:,:,3]-reference[:,:,3]).max())
                        report["alpha_max_error"]=max(report["alpha_max_error"],alpha_error)
                        assert alpha_error<1e-6,"Surface detail changed mesh coverage"
                        report["renders"]+=2
            for scale in (.22,1):
                a=render(ctx,old,shape,hue,0,.65,.85,polish=0,scale=scale)
                b=render(ctx,new,shape,hue,0,.65,.85,polish=0,scale=scale)
                error=float(np.abs(a-b).max())
                report["warning_max_error"]=max(report["warning_max_error"],error)
                assert error<1e-6,"Warning colour/opacity changed"
                report["renders"]+=2
        old.release();new.release()
    canvas=Image.new("RGB",(416,900),(12,17,27))
    draw=ImageDraw.Draw(canvas)
    draw.text((14,12),"Controlled HLSL material fixtures | outside Unity",fill=(224,230,240))
    draw.text((14,32),"BEFORE",fill=(180,192,208));draw.text((222,32),"AFTER",fill=(180,192,208))
    for i,(shape,row) in enumerate(rows):
        y=58+i*210
        for j,tile in enumerate(row):
            canvas.paste(tile,(12+j*204,y+15))
        draw.text((14,y),shape,fill=(220,230,242))
    canvas.save(args.output/"energy-material-comparison.png")
    (args.output/"energy-render-validation.json").write_text(json.dumps(report,indent=2)+"\n")
    print(json.dumps(report,indent=2))

if __name__=="__main__":
    main()
