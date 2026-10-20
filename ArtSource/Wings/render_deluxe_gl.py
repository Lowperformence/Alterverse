"""Recapture Deluxe shop art from exported production geometry (no Unity/Blender needed).

Dependencies: numpy, Pillow, moderngl, scipy; EGL software rendering is supported.
Run LegacyIconExport.cs through run_visuals.py before recapturing changed geometry.
This studio renderer is an icon generator, not a Unity/Android acceptance test.
"""
from pathlib import Path
import json
import numpy as np
import moderngl
from PIL import Image
from scipy.ndimage import gaussian_filter

ROOT=Path(__file__).resolve().parents[2]
DATA=ROOT/'ArtSource/Wings/LegacyShopGeometry.json'

def normalize(v):
    return v/max(np.linalg.norm(v),1e-8)

def main():
    vertices=[]
    for node in json.loads(DATA.read_text())['Deluxe']:
        points=np.asarray(node['vertices'],dtype='f4')*node['scale']+node['position']
        for slot,indices in enumerate(node['triangles']):
            m=node['materials'][slot]
            for tri in np.asarray(indices).reshape(-1,3):
                p=points[tri]
                n=normalize(np.cross(p[1]-p[0],p[2]-p[0]))
                for v in p:vertices.append([*v,*n,*m['color'],*m['emission'],m['metallic'],m['smoothness']])
    data=np.asarray(vertices,dtype='f4')
    low=data[:,:3].min(0);high=data[:,:3].max(0);center=(low+high)*.5
    size=max(high[0]-low[0],(high[1]-low[1])*1.6)*1.2
    eye=center+np.array([size*.12,size*.35,7.])
    forward=normalize(center-eye);right=normalize(np.cross(forward,[0,1,0]));up=np.cross(right,forward)
    view=np.eye(4);view[:3,:3]=[right,up,-forward];view[:3,3]=-view[:3,:3]@eye
    projection=np.diag([2/size,2/(size/1.6),-2/20,1.]);projection[2,3]=-1.
    ctx=moderngl.create_standalone_context(backend='egl')
    program=ctx.program(vertex_shader="""#version 330
in vec3 position,normal,albedo,emission;in float metallic,smoothness;
uniform mat4 camera;
out vec3 p,n,c,e;out float metal,polish;
void main(){p=position;n=normal;c=albedo;e=emission;metal=metallic;polish=smoothness;gl_Position=camera*vec4(p,1);}
""",fragment_shader="""#version 330
in vec3 p,n,c,e;in float metal,polish;out vec4 color;
uniform vec3 eye,center;
vec3 light(vec3 normal,vec3 view,vec3 direction,vec3 power){
 float ndl=max(dot(normal,direction),0.);
 vec3 halfway=normalize(direction+view);
 float spec=pow(max(dot(normal,halfway),0.),mix(12.,96.,polish));
 vec3 reflectance=mix(vec3(.04),mix(c,vec3(1),.5),metal);
 return (c*ndl*(1.-metal*.35)+reflectance*spec*(.65+polish*.7))*power;
}
void main(){
 vec3 normal=normalize(n),view=normalize(eye-p);
 if(dot(normal,view)<0.)normal=-normal;
 vec3 rgb=c*vec3(.22,.25,.30);
 rgb+=light(normal,view,normalize(center+vec3(-2,4,4)-p),vec3(.95,.95,.95));
 rgb+=light(normal,view,normalize(center+vec3(3,2,-2)-p),vec3(.65,.72,.85));
 rgb+=light(normal,view,normalize(center+vec3(1,-1,5)-p),vec3(.30));
 float rim=pow(1.-max(dot(normal,view),0.),3.);
 rgb+=c*rim*.15;
 if(max(e.r,max(e.g,e.b))>0.)rgb=c*.25+e;
 color=vec4(rgb,1);
}
""")
    program['camera'].write(np.asarray((projection@view).T,dtype='f4').tobytes())
    program['eye'].value=tuple(eye);program['center'].value=tuple(center)
    buffer=ctx.buffer(data.tobytes())
    vao=ctx.vertex_array(program,[(buffer,'3f 3f 3f 3f 1f 1f','position','normal','albedo','emission','metallic','smoothness')])
    target=ctx.texture((1536,960),4,dtype='f4');depth=ctx.depth_renderbuffer((1536,960))
    fbo=ctx.framebuffer([target],depth);fbo.use();fbo.clear(.014,.023,.033,1)
    ctx.enable(moderngl.DEPTH_TEST);ctx.disable(moderngl.CULL_FACE);vao.render()
    rgb=np.frombuffer(fbo.read(components=4,dtype='f4'),dtype='f4').reshape(960,1536,4)[::-1,:,:3].copy()
    assert np.isfinite(rgb).all() and rgb.max()>1
    bright=np.maximum(rgb-.85,0)
    rgb+=gaussian_filter(bright,sigma=(12,12,0))*.28
    rgb=np.maximum(rgb,0)/(1+np.maximum(rgb,0)*.35)
    image=Image.fromarray((np.clip(rgb,0,1)**(1/2.2)*255).astype('uint8'))
    out=ROOT/'Assets/Alterverse/Resources/Wings/DeluxeIcon.png'
    image.resize((384,240),Image.Resampling.LANCZOS).save(out)
    print('Rendered '+str(out)+' from '+str(len(data)//3)+' production triangles.')
    ctx.release()

if __name__=='__main__':main()

