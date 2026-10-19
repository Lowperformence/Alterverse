"""Author the approved anthracite mechanical RGB design; retain Angel geometry exactly."""
import colorsys
import json
import math
from pathlib import Path

HERE=Path(__file__).resolve().parent

def build():
    target=HERE/'PremiumWingParts.json'
    data=json.loads(target.read_text())
    parts=[]
    materials=data['materials']
    materials.update(Anthracite=[[.045,.052,.064],.88,.31,0],
        Gunmetal=[[.17,.19,.22],.9,.25,0],Recess=[[.012,.015,.021],.65,.38,0],
        RGB_Channel=[[.05,.6,1],.15,.25,2.2])
    def add(name,verts,faces,mat,side=1,group='05',colors=None):
        p=dict(name=name,verts=verts,faces=faces,material=mat,side=side,group=group,bone='Wing.R' if side else 'Root')
        if colors:p['vertexColors']=colors
        parts.append(p)
        if side:
            q=dict(p,name=name.replace('.R','.L'),verts=[[-x,y,z] for x,y,z in verts],
                faces=[f[::-1] for f in faces],side=-1,bone='Wing.L')
            parts.append(q)
    def plate(name,outline,front,depth,mat,side=1):
        n=len(outline);v=[[x,front,z] for x,z in outline]+[[x,front+depth,z] for x,z in outline]
        faces=[list(range(n-1,-1,-1)),list(range(n,2*n))]+[[i,(i+1)%n,(i+1)%n+n,i+n] for i in range(n)]
        add(name,v,faces,mat,side)
    def cylinder(name,x,z,r,front,depth,mat,side=1,n=16):
        plate(name,[(x+r*math.cos(i*math.tau/n),z+r*math.sin(i*math.tau/n)) for i in range(n)],front,depth,mat,side)
    def bar(name,a,b,w,front,depth,mat):
        dx,dz=b[0]-a[0],b[1]-a[1];length=math.hypot(dx,dz);sx,sz=-dz/length*w,dx/length*w
        plate(name,[(a[0]+sx,a[1]+sz),(b[0]+sx,b[1]+sz),(b[0]-sx,b[1]-sz),(a[0]-sx,a[1]-sz)],front,depth,mat)
    plate('BackMount', [(-.21,-.28),(.21,-.28),(.25,.19),(.13,.33),(-.13,.33),(-.25,.19)],.015,.12,'Anthracite',0)
    plate('ControllerArmor',[(-.15,-.22),(.15,-.22),(.18,.16),(0,.29),(-.18,.16)],-.035,.05,'Gunmetal',0)
    plate('ControllerInset',[(-.10,-.18),(.10,-.18),(.11,.13),(0,.22),(-.11,.13)],-.05,.015,'Anthracite',0)
    for side in [-1,1]:
        x=side*.07
        plate('ControllerStrip'+str(side),[(x-.012,-.12),(x+.012,-.12),(x+.012,.14),(x-.012,.14)],-.067,.01,'RGB_Channel',0)
    bar('MainSpar.R',(.25,.19),(.89,.82),.08,.02,.12,'Gunmetal')
    bar('SparArmor.R',(.29,.23),(.85,.77),.055,-.035,.07,'Anthracite')
    bar('LowerLink.R',(.28,.10),(.66,-.10),.055,.02,.08,'Gunmetal')
    tips=[(1.74,1.40),(1.85,1.03),(1.87,.66),(1.78,.28),(1.60,-.10),(1.35,-.43),(1.07,-.73),(.78,-.93)]
    for i,tip in enumerate(tips):
        a=(.30+.036*i,.38-.037*i);dx,dz=tip[0]-a[0],tip[1]-a[1];length=math.hypot(dx,dz)
        ux,uz=dx/length,dz/length;nx,nz=-uz,ux;w=.115 if i<5 else .10
        def point(t,offset=0):return (a[0]+dx*t+nx*offset,a[1]+dz*t+nz*offset)
        outline=[point(0,-w*.45),point(.26,-w),point(.78,-w*.74),point(1),point(.80,w*.55),point(.24,w),point(0,w*.45)]
        y=-.055-i*.009
        plate(f'Blade_{i:02}.R',outline,y,.075,'Anthracite')
        plate(f'Bevel_{i:02}.R',[point(.22,-w*.92),point(.78,-w*.70),point(1),point(.75,-w*.36)],y-.005,.011,'Gunmetal')
        plate(f'LayeredArmor_{i:02}.R',[point(.05,w*.36),point(.32,w*.72),point(.64,w*.40),point(.52,w*.1),point(.17,w*.02)],y-.021,.02,'Gunmetal')
        # Dark channel border and a continuous narrow luminous ribbon.
        bar(f'ChannelRecess_{i:02}.R',point(.19,-w*.14),point(.90,-w*.14),.025,y-.024,.012,'Recess')
        verts=[];colors=[]
        for station in range(9):
            t=.20+.70*station/8;center=point(t,-w*.14)
            hue=(.50+.28*t+.025*i)%1
            for sign in [-1,1]:
                verts.append([center[0]+nx*.0105*sign,y-.039,center[1]+nz*.0105*sign])
                colors.append(list(colorsys.hsv_to_rgb(hue,.94,1)))
        add(f'RGBStrip_{i:02}.R',verts,[[2*s,2*s+1,2*s+3,2*s+2] for s in range(8)],'RGB_Channel',colors=colors)
        cylinder(f'BladeHinge_{i:02}.R',*point(.12),.040,y-.045,.023,'Gunmetal',n=12)
        cylinder(f'BladeBolt_{i:02}.R',*point(.12),.014,y-.071,.023,'Recess',n=8)
    cylinder('ShoulderJoint.R',.26,.19,.122,-.17,.16,'Gunmetal')
    cylinder('ShoulderCap.R',.26,.19,.092,-.184,.022,'Anthracite')
    cylinder('ShoulderPin.R',.26,.19,.032,-.207,.025,'Gunmetal',n=12)
    for i in range(6):
        a=i*math.tau/6
        cylinder(f'JointBolt_{i}.R',.26+.073*math.cos(a),.19+.073*math.sin(a),.009,-.211,.02,'Gunmetal',n=6)
    data['models']={'Angel':data['models']['Angel'],'RGB':parts}
    data['design']='Mechanical anthracite RGB strip wings, approved 2026-10-19; Angel unchanged.'
    target.write_text(json.dumps(data,separators=(',',':'))+'\n')
    print('RGB mechanical parts:',len(parts),'Angel parts:',len(data['models']['Angel']))

if __name__=='__main__':build()
