"""Geometry QA using the shipped kit, EGL/ModernGL, NumPy and Pillow.

These are model/layout previews on flat ground, not Unity screenshots or URP validation.
"""
import argparse
import json
from pathlib import Path

import moderngl
import numpy as np
from PIL import Image

from validate_earth_map import ROOT, accepted, blocked, transformed


def look_at(eye,target):
    eye=np.array(eye,dtype='f4');target=np.array(target,dtype='f4')
    forward=target-eye;forward/=np.linalg.norm(forward)
    right=np.cross(forward,np.array([0,1,0]));right/=np.linalg.norm(right);up=np.cross(right,forward)
    matrix=np.eye(4,dtype='f4');matrix[:3,:3]=np.array([right,up,-forward]);matrix[:3,3]=-matrix[:3,:3]@eye
    return matrix


def ortho(width,height,near=.1,far=250):
    m=np.eye(4,dtype='f4');m[0,0]=2/width;m[1,1]=2/height;m[2,2]=-2/(far-near);m[2,3]=-(far+near)/(far-near)
    return m


def geometry(data):
    kit={a['name']:a for a in data['assets']};verts=[];colors=[]
    for site in data['placements']:
        points=transformed(kit[site['asset']],site)
        if not accepted(np.concatenate(points)):continue
        for p,v in zip(kit[site['asset']]['parts'],points):
            t=np.array(p['triangles']).reshape(-1,3);verts.append(v[t]);colors.extend([data['materials'][p['material']]['color'][:3]]*len(t))
    for surface in data['surfaces']:
        for p in surface['parts']:
            v=np.array(p['vertices']).reshape(-1,3)
            if not surface['water']:v[:,1]+=.015
            for tri in np.array(p['triangles']).reshape(-1,3):
                if blocked(v[tri]):continue
                verts.append(v[tri][None]);colors.append(data['materials'][p['material']]['color'][:3])
    verts.append(np.array([[[-42,0,-36],[-42,0,36],[42,0,36]],[[-42,0,-36],[42,0,36],[42,0,-36]]]))
    colors.extend([[.20,.27,.11]]*2)
    triangles=np.concatenate(verts).astype('f4');normals=np.cross(triangles[:,1]-triangles[:,0],triangles[:,2]-triangles[:,0])
    normals/=np.linalg.norm(normals,axis=1)[:,None]
    return np.concatenate([triangles.reshape(-1,3),np.repeat(normals,3,axis=0),np.repeat(np.array(colors,dtype='f4'),3,axis=0)],axis=1).astype('f4')


def render(output):
    data=json.loads((ROOT/'Assets/Alterverse/Resources/EarthMap/Dressing.json').read_text())
    ctx=moderngl.create_standalone_context(backend='egl');ctx.enable(moderngl.DEPTH_TEST|moderngl.CULL_FACE)
    buffer=ctx.buffer(geometry(data).tobytes())
    vertex='''#version 330
    uniform mat4 mvp, lightVP;
    in vec3 position, normal, color;
    out vec3 n, c, p;out vec4 shadow;
    void main(){p=position;n=normal;c=color;shadow=lightVP*vec4(position,1);gl_Position=mvp*vec4(position,1);}
    '''
    fragment='''#version 330
    uniform sampler2D shadowMap;
    in vec3 n,c,p;in vec4 shadow;out vec4 pixel;
    void main(){vec3 coord=shadow.xyz/shadow.w*.5+.5;float shade=0.;
      for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++){
        float depth=texture(shadowMap,coord.xy+vec2(x,y)/2048.).r;
        shade+=coord.z-.0012>depth?.42:1.;}
      shade/=9.;float sun=max(0.,dot(normalize(n),normalize(vec3(.42,.82,.32))));
      float sky=.48+.14*max(0.,n.y);vec3 rgb=c*(sky+sun*.75*shade);
      if(n.y>.98&&p.y<.012){float grain=sin(p.x*13.7+p.z*9.2)*sin(p.z*8.1)*.018;rgb+=grain;}
      pixel=vec4(pow(max(rgb,vec3(0)),vec3(1./2.2)),1);}
    '''
    prog=ctx.program(vertex_shader=vertex,fragment_shader=fragment)
    vao=ctx.vertex_array(prog,[(buffer,'3f 3f 3f','position','normal','color')])
    depth_prog=ctx.program(vertex_shader='''#version 330
    uniform mat4 mvp;in vec3 position;void main(){gl_Position=mvp*vec4(position,1);}''',fragment_shader='''#version 330
    void main(){}''')
    depth_vao=ctx.vertex_array(depth_prog,[(buffer,'3f 24x','position')])
    light=ortho(125,120)@look_at((42,82,32),(0,0,0))
    shadow=ctx.depth_texture((2048,2048));shadow.compare_func='';shadow.repeat_x=False;shadow.repeat_y=False
    depth_fbo=ctx.framebuffer(depth_attachment=shadow);depth_fbo.use();ctx.viewport=(0,0,2048,2048);depth_fbo.clear(depth=1)
    depth_prog['mvp'].write(light.T.tobytes());depth_vao.render();shadow.use(location=0)
    prog['shadowMap']=0;prog['lightVP'].write(light.T.tobytes())
    size=(1440,1000);fbo=ctx.simple_framebuffer(size);fbo.use();ctx.viewport=(0,0,*size)
    output.mkdir(parents=True,exist_ok=True)
    for name,eye,target,width in [('EarthLayout',(55,82,100),(0,0,0),107),('EarthVillage',(18,30,3),(-2,0,25),40)]:
        fbo.clear(.12,.16,.18,1,depth=1);matrix=ortho(width,width*size[1]/size[0])@look_at(eye,target)
        prog['mvp'].write(matrix.T.tobytes());vao.render()
        img=Image.frombytes('RGB',size,fbo.read(components=3)).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
        img.save(output/(name+'.png'));print(output/(name+'.png'))
    ctx.release()


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--output',type=Path,default=ROOT/'ArtSource/EarthMap/GeometryQA')
    args=parser.parse_args();render(args.output)
