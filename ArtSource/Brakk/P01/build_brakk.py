"""BRAKK P01: original procedural blockout. Python 3, no third-party assets."""
import json, math
from pathlib import Path
ROOT = Path(__file__).resolve().parent
parts = []
def box(name, center, size, material, group):
    x,y,z=center; a,b,c=[v/2 for v in size]
    vertices=[(x+i*a,y+j*b,z+k*c) for i,j,k in
              [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),
               (-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    faces=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
    parts.append(dict(name=name,vertices=vertices,faces=faces,material=material,group=group))
def ellipsoid(name,center,size,material,group):
    # Closed surface with unique poles, eight rings and twelve segments.
    n=12; rings=8
    x,y,z=center; a,b,c=[v/2 for v in size]
    v=[(x,y,z-c)]
    for r in range(1,rings+1):
        t=-math.pi/2+r*math.pi/(rings+1)
        for s in range(n):
            u=s*2*math.pi/n
            v.append((x+a*math.cos(t)*math.cos(u),y+b*math.cos(t)*math.sin(u),z+c*math.sin(t)))
    v.append((x,y,z+c)); top=len(v)-1
    f=[(0,1+(s+1)%n,1+s) for s in range(n)]
    for r in range(rings-1):
        q=1+r*n
        for s in range(n): f.append((q+s,q+(s+1)%n,q+n+(s+1)%n,q+n+s))
    f.extend((top,1+(rings-1)*n+s,1+(rings-1)*n+(s+1)%n) for s in range(n))
    parts.append(dict(name=name,vertices=v,faces=f,material=material,group=group))
ellipsoid('01_Head',(0,-.015,1.715),(.26,.25,.31),'skin','head')
box('01_Jaw',(0,-.08,1.625),(.19,.18,.08),'skin','head')
box('01_Nose',(0,-.15,1.705),(.055,.06,.065),'skin','head')
box('01_Hair',(0,.005,1.865),(.225,.20,.03),'hair','head')
box('02_BrowGuard',(0,-.15,1.79),(.27,.045,.035),'graphite','head')
for side,sign in [('L',1),('R',-1)]:
    box('01_Eye_'+side,(sign*.06,-.144,1.74),(.04,.01,.018),'petrol','head')
    box('02_Cheek_'+side,(sign*.133,-.09,1.67),(.025,.09,.085),'graphite','head')
ellipsoid('03_Undershirt',(0,0,1.30),(.56,.34,.53),'petrol','torso')
box('04_JacketBack',(0,.175,1.34),(.52,.07,.44),'ochre','torso')
for s,k in [('L',1),('R',-1)]:
    box('04_JacketFront_'+s,(k*.155,-.19,1.32),(.235,.075,.43),'ochre','torso')
    box('05_ChestPlate_'+s,(k*.14,-.24,1.42),(.24,.055,.19),'graphite','torso')
box('05_Clasp',(.05,-.285,1.31),(.07,.03,.055),'amber','torso')
box('06_BackPlate',(0,.23,1.43),(.37,.05,.22),'graphite','torso')
box('07_Belt',(0,0,1.065),(.49,.37,.09),'graphite','pelvis')
box('12_TrousersHip',(0,0,.94),(.45,.32,.19),'petrol','pelvis')
for s,k in [('L',1),('R',-1)]:
    ellipsoid('08_UpperArm_'+s,(k*.40,0,1.34),(.24,.24,.36),'skin','arms')
    box('08_Shoulder_'+s,(k*.36,0,1.515),(.23,.31,.11),'graphite','arms')
    ellipsoid('09_Forearm_'+s,(k*.47,-.015,1.10),(.19,.22,.25),'petrol','arms')
    box('09_Elbow_'+s,(k*.46,.105,1.20),(.12,.07,.10),'graphite','arms')
    x=k*.49
    box('10_Glove_'+s,(x,-.02,.91),(.19,.19,.20),'petrol','gauntlets')
    box('10_Wrist_'+s,(x,0,1.035),(.25,.26,.065),'graphite','gauntlets')
    box('10_Dorsal_'+s,(x,-.145,.93),(.29,.065,.23),'graphite','gauntlets')
    box('10_Palm_'+s,(x,.12,.93),(.24,.045,.18),'graphite','gauntlets')
    for j in [-1,1]:
        box('11_Damper_'+s+str(j),(x+j*.158,0,.945),(.035,.12,.17),'amber','gauntlets')
    for j in range(3):
        box('11_Knuckle_'+s+str(j),(x+(j-1)*.094,-.19,.845),(.085,.06,.095),'graphite','gauntlets')
    ellipsoid('12_Thigh_'+s,(k*.145,0,.735),(.26,.29,.36),'petrol','legs')
    box('13_ThighArmor_'+s,(k*.245,-.03,.75),(.055,.25,.23),'graphite','legs')
    ellipsoid('12_Calf_'+s,(k*.15,.005,.37),(.21,.23,.34),'petrol','legs')
    box('14_Knee_'+s,(k*.15,-.155,.535),(.19,.07,.16),'graphite','legs')
    box('15_Shin_'+s,(k*.15,-.135,.33),(.16,.045,.22),'graphite','legs')
    box('16_Boot_'+s,(k*.15,-.045,.115),(.25,.37,.21),'graphite','legs')
    box('16_Sole_'+s,(k*.15,-.045,.02),(.26,.38,.04),'hair','legs')
materials={'skin':(.40,.22,.13),'hair':(.045,.035,.025),'graphite':(.13,.16,.18),
           'petrol':(.055,.18,.20),'ochre':(.63,.36,.12),'amber':(.95,.58,.12)}
def write_obj(filename, exploded=False):
    lines=['# BRAKK P01; meters; Z up; front -Y; procedural blockout','mtllib brakk.mtl']; offset=1
    shifts={'head':(0,0,.55),'torso':(0,0,.20),'arms':(.32,0,.20),
            'gauntlets':(.60,0,0),'pelvis':(0,0,0),'legs':(.18,0,-.10)}
    for p in parts:
        dx,dy,dz=shifts[p['group']] if exploded else (0,0,0)
        if p['vertices'][0][0]<0: dx=-dx
        lines.extend(['o '+p['name'],'usemtl '+p['material']])
        for x,y,z in p['vertices']: lines.append(f'v {x+dx:.6f} {y+dy:.6f} {z+dz:.6f}')
        for face in p['faces']: lines.append('f '+' '.join(str(i+offset) for i in face))
        offset+=len(p['vertices'])
    (ROOT/filename).write_text('\n'.join(lines)+'\n')
if __name__=='__main__':
    write_obj('brakk_prototype.obj'); write_obj('brakk_exploded.obj', True)
    (ROOT/'brakk.mtl').write_text('\n'.join(f'newmtl {n}\nKd {r} {g} {b}\nKs 0.1 0.1 0.1\nNs 24\n' for n,(r,g,b) in materials.items()))
    (ROOT/'parts.json').write_text(json.dumps(parts,indent=2))
    print(f'{len(parts)} separate meshes; '+str(sum(len(p['vertices']) for p in parts))+' vertices')
