#!/usr/bin/env python3
"""Rebake only the client star panorama; keep Unity's runtime shader and 3D planet assets intact.

Dependencies: numpy, Pillow, moderngl, glcontext; standalone EGL/OpenGL 3.3 context.
"""
from pathlib import Path
import hashlib,io,json
import moderngl
import numpy as np
from PIL import Image

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
ASSETS=ROOT/'Assets/Alterverse/Resources/MatchSkybox360'

def main():
    context=moderngl.create_standalone_context(backend='egl')
    vertex='#version 330\nin vec2 pos;out vec2 uv;void main(){uv=pos*.5+.5;gl_Position=vec4(pos,0,1);}'
    fragment='#version 330\nin vec2 uv;out vec4 frag;\n'+(HERE/'background.glsl').read_text()+'''
void main(){
    float lon=(uv.x-.5)*2.*PI,lat=(uv.y-.5)*PI;
    vec3 p=vec3(sin(lon)*cos(lat),sin(lat),cos(lon)*cos(lat));
    // Same Unity panoramic orientation as the original asset.
    frag=vec4(background(vec3(p.z,p.y,-p.x)),1);
}'''
    program=context.program(vertex_shader=vertex,fragment_shader=fragment)
    buffer=context.buffer(np.array([-1,-1,3,-1,-1,3],dtype='f4').tobytes())
    vao=context.simple_vertex_array(program,buffer,'pos')
    texture=context.texture((4096,2048),4,dtype='f4');frame=context.framebuffer([texture]);frame.use()
    vao.render(moderngl.TRIANGLES)
    pixels=np.frombuffer(frame.read(components=4,dtype='f4'),dtype='f4').reshape(2048,4096,4)[::-1].copy()
    assert np.isfinite(pixels).all() and (pixels[:,:,3]==1).all()
    linear=pixels[:,:,:3]
    srgb=np.where(linear<=.0031308,linear*12.92,1.055*np.maximum(linear,0)**(1/2.4)-.055)
    image=Image.fromarray(np.round(np.clip(srgb,0,1)*255).astype('u1'))
    encoded=io.BytesIO();image.save(encoded,format='PNG',optimize=True)
    path=ASSETS/'Textures/Sky360_StarsNebula.png';temporary=path.with_suffix('.partial')
    temporary.write_bytes(encoded.getvalue());temporary.replace(path)
    manifest_path=ASSETS/'manifest.json';manifest=json.loads(manifest_path.read_text())
    for artwork in manifest['artwork']:
        if artwork['file']=='Textures/Sky360_StarsNebula.png':
            artwork['sha256']=hashlib.sha256(encoded.getvalue()).hexdigest()
    manifest['star_visibility_fix']={'date':'2026-10-30','angular_core_radius':[.0017,.0043],
        'source':'Tools/Visuals/MatchSkybox360Stars','android_sky_compression':'ASTC_4x4',
        'unchanged':'nebula colors, panorama orientation, planets, runtime camera/sky shader, server'}
    manifest_path.write_text(json.dumps(manifest,indent=2)+'\n')
    print(json.dumps({'output':str(path),'bytes':len(encoded.getvalue()),'sha256':hashlib.sha256(encoded.getvalue()).hexdigest(),
                      'renderer':context.info['GL_RENDERER']}))
    vao.release();buffer.release();program.release();frame.release();texture.release();context.release()

if __name__=='__main__':main()
