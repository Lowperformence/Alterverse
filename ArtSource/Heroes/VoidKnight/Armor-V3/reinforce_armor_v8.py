"""Reinforced V3 secondary armor, evaluated inside build_armor_v3.py.

Closed, rest-bound machined panels and physical channels; no new materials,
textures, shader dependencies, skeleton changes or animation adjustments.
"""

def rim(name,outline,y,thickness,mat,bone,module,assembly,frame=None,inset=.88):
    """A closed hollow frame with a real recessed center, rather than a drawn border."""
    cx=sum(x for x,z in outline)/len(outline);cz=sum(z for x,z in outline)/len(outline)
    inner=[(cx+(x-cx)*inset,cz+(z-cz)*inset) for x,z in outline]
    n=len(outline);verts=[(x,yy,z) for yy in [y,y+thickness] for ring in [outline,inner] for x,z in ring]
    faces=[]
    for i in range(n):
        j=(i+1)%n
        faces += [(i,j,n+j,n+i),(2*n+i,3*n+i,3*n+j,2*n+j),
            (i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)]
    return mesh(name,verts,faces,mat,bone,module,assembly,0,frame)

def cassette(name,x,y,z,w,h,bone,module,assembly,frame=None,reverse=False):
    """Inset ventilation pocket with four individually modeled slats and side flanges."""
    sign=-1 if reverse else 1
    outline=[(x-w*.5,z-h*.36),(x-w*.36,z-h*.5),(x+w*.36,z-h*.5),
        (x+w*.5,z-h*.36),(x+w*.5,z+h*.36),(x+w*.36,z+h*.5),
        (x-w*.36,z+h*.5),(x-w*.5,z+h*.36)]
    plate(name+'_Pocket',outline,y,sign*.005,cloth,bone,module,assembly,0,frame)
    rim(name+'_Flange',outline,y-sign*.002,sign*.004,edge,bone,module,assembly,frame,.85)
    # Pool the slats as closed solids to preserve editability without runtime draws.
    for j in range(4):
        zz=z+h*(j-1.5)*.18
        # Keep the slats in their pocket plane. Projecting onto the parent shell
        # would bury them behind the opaque pocket backing.
        a=x-w*.34;b=x+w*.34;half=h*.0325;dy=sign*.002
        top=[(a,y-sign*.003,zz-half),(b,y-sign*.003,zz+h*.08-half),
            (b,y-sign*.003,zz+h*.08+half),(a,y-sign*.003,zz+half)]
        vv=top+[(xx,yy+dy,zzz) for xx,yy,zzz in top]
        ff=[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)]
        batch([frame(p) if frame else p for p in vv],ff,black,bone,module,assembly)

for sg,label in [(-1,'L'),(1,'R')]:
    sx=lambda points,s=sg:[(s*x,z) for x,z in points]
    # Clavicle bridges become armoured suspension yokes with inset service slots.
    plate('A03_ClavicleSpur_'+label,sx([(.065,1.512),(.108,1.525),(.178,1.504),(.190,1.487),(.147,1.489),(.100,1.506)]),
        [-.188,-.162,-.117,-.133,-.170,-.189],.008,black,'Chest','A03','Cuirass',.0009)
    # Raised two-piece breastplate scutes give the chest a deliberate swept silhouette.
    for j in range(2):
        z=1.410-j*.041
        outline=sx([(.066,z+.027),(.105,z+.038),(.185,z+.009),(.205,z-.015),
            (.180,z-.030),(.126,z-.012),(.087,z-.024),(.058,z-.009)])
        plate('A03_SweptChestScute_'+label+'_'+str(j),outline,
            [-.239,-.224,-.178,-.157,-.186,-.222,-.244,-.250],.010,black,'Chest','A03','Cuirass',.0010)
        # A discontinuous silver edge catches light without outlining every panel.
        strip('A03_ScuteEdge',[(sg*.075,-.252,z+.018),(sg*.105,-.237,z+.027),(sg*.171,-.194,z+.004)],
            .0023,edge,'Chest','A03','Cuirass',pool=True)
    # A narrow recessed extractor pod beside the chest core.
    chest_frame=lambda p,s=sg:Vector((s*(.172+p[0]),p[1]-.178,p[2]+1.429))
    cassette('A03_ChestExtractor_'+label,0,-.025,0,.038,.044,'Chest','A03','Cuirass',chest_frame)
    plate('A17_CoreLock_'+label,sx([(.039,1.444),(.055,1.426),(.060,1.403),(.048,1.389),(.037,1.408)]),
        -.270,.010,black,'Chest','A17','Cuirass',.0008)
    bolt((sg*.049,-.275,1.417),(0,-1,0),'Chest','A17','Cuirass',.0024)
    # Three ribs on the flank read from the game camera as actual articulated depth.
    flank=lambda p,s=sg:Vector((s*(.195-p[1]),p[0],p[2]))
    for j in range(3):
        z=1.355-j*.035
        plate('A03_FlankRib_'+label+'_'+str(j),[(-.094,z+.017),(-.020,z+.022),(.086,z+.008),(.095,z-.010),(-.022,z-.004),(-.089,z-.012)],
            -.005,.010,black,'Chest','A03','Cuirass',.0008,flank)
    # Scapular cooling cassettes and an overlapping vertebral spine.
    cassette('A04_ScapulaVent_'+label,sg*.116,.183,1.427,.052,.074,'Chest','A04','Cuirass',reverse=True)
    strip('A04_BackSpineRail',[(sg*.049,.204,1.484),(sg*.057,.210,1.389),(sg*.043,.207,1.286)],
        .0030,edge,'Chest','A04','Cuirass',depth=-.001,pool=True)

    # Pelvic articulation gets a second hanging plate below the existing hip armor.
    thigh='Thigh'+label;arm='UpperArm'+label;fore='Forearm'+label;shin='Shin'+label
    plate('A12_LowerHipLame_'+label,sx([(.072,.925),(.141,.904),(.215,.882),(.221,.847),(.185,.784),(.102,.824)]),
        [-.189,-.195,-.159,-.154,-.196,-.207],.011,black,thigh,'A12','Tassets',.0011)
    plate('A12_HipCrownLatch_'+label,sx([(.094,1.003),(.126,1.004),(.143,.971),(.126,.947),(.106,.959)]),
        -.185,.011,black,thigh,'A12','Tassets',.0008)
    strip('A12_TassetEdge',[(sg*.106,-.213,.837),(sg*.179,-.203,.801),(sg*.202,-.181,.849)],
        .0027,edge,thigh,'A12','Tassets',pool=True)

    # Three layers along the broad cuisse, with recessed energy feed and side heatsink.
    f,L=frame_for(thigh)
    for j in range(2):
        z=.225+j*.055
        plate('A13_QuadricepsLame_'+label+'_'+str(j),[(-.086,z-.019),(-.045,z-.007),(-.032,z+.028),
            (-.043,z+.050),(-.086,z+.028)],-.143+j*.003,.009,black,thigh,'A13','Cuisse_'+label,.0009,f)
    feed=[(.044,.185),(.065,.171),(.077,.190),(.065,L-.113),(.038,L-.074),(.034,L-.108)]
    plate('A13_ThighFeedSeat_'+label,feed,-.150,.005,cloth,thigh,'A13','Cuisse_'+label,.0005,f)
    rim('A13_ThighFeedRim_'+label,feed,-.153,.004,edge,thigh,'A13','Cuisse_'+label,f,.78)
    lens=[(.050,.197),(.058,.188),(.064,.198),(.053,L-.121),(.043,L-.108)]
    plate('A18_ThighFeed_'+label,lens,-.154,.002,energy,thigh,'A18','Cuisse_'+label,.0004,f)
    # The outer cassette's plane points outwards; its bezel has actual sidewalls.
    side=lambda p,s=sg:f((s*(.126-p[1]),p[0],p[2]))
    cassette('A13_ThighCooling_'+label,0,-.004,.232,.067,.092,thigh,'A13','Cuisse_'+label,side)
    plate('A13_LateralReinforcement_'+label,[(-.053,.294),(.048,.286),(.044,L-.082),(0,L-.052),(-.044,L-.084)],
        -.006,.009,black,thigh,'A13','Cuisse_'+label,.0009,side)
    for zz in [.143,.303]:
        bolt(side((-.037,-.014,zz)),side((0,-1,0))-side((0,0,0)),thigh,'A13','Cuisse_'+label,.0030)
    glyph(-.017,-.147,.243,thigh,'A13','Cuisse_'+label,frame=f,size=.8)
    cassette('A13_RearRelease_'+label,0,.126,.211,.062,.048,thigh,'A13','Cuisse_'+label,f,reverse=True)

    # Shoulder surface relief follows the existing collision-safe silhouette.
    # No added lateral spikes or height intrude into the corrected blade corridor.
    plate('A05_ShoulderKeel_'+label,sx([(.268,1.575),(.303,1.585),(.345,1.565),(.333,1.550),(.306,1.542),(.280,1.555)]),
        [-.157,-.158,-.157,-.173,-.184,-.177],.009,black,arm,'A05','Pauldron_'+label,.0010)
    for j in range(2):
        z=1.478-j*.052
        plate('A05_ScaleRivetTab_'+label+'_'+str(j),sx([(.358,z+.020),(.388,z+.009),(.397,z-.009),(.369,z-.023),(.347,z-.008)]),
            -.147,.008,black,arm,'A05','Pauldron_'+label,.0008)
        bolt((sg*.374,-.154,z-.001),(0,-1,0),arm,'A05','Pauldron_'+label,.0026)
    f,L=frame_for(arm)
    cassette('A06_BicepVent_'+label,0,-.105,.151,.058,.050,arm,'A06','Pauldron_'+label,f)
    for side_sign in [-1,1]:
        strip('A06_ElbowTendon',[(side_sign*.047,-.097,L-.071),(side_sign*.043,-.101,L-.016)],
            .0050,edge,arm,'A07','Pauldron_'+label,f,pool=True)
    # Forearm side armor, knuckle-side collar and an inset terminal aperture.
    f,L=frame_for(fore)
    for ss in [-1,1]:
        plate('A08_ForearmFlange_'+label+'_'+str(ss),[(ss*.034,.071),(ss*.062,.060),(ss*.071,.103),(ss*.047,L-.028),(ss*.030,L-.051)],
            -.108,.009,black,fore,'A08','Vambrace_'+label,.0009,f)
    plate('A08_WristCollar_'+label,[(-.049,L-.047),(-.035,L-.063),(.035,L-.063),(.049,L-.047),(.045,L-.020),(-.045,L-.020)],
        -.110,.010,black,fore,'A08','Vambrace_'+label,.0010,f)
    cassette('A08_TerminalVent_'+label,-.031,-.119,.109,.026,.039,fore,'A08','Vambrace_'+label,f)
    # Knee has a central raised shield, recessed V motif and shin heel locks.
    f,L=frame_for(shin)
    plate('A14_PatellaCap_'+label,[(-.040,-.015),(0,-.033),(.040,-.015),(.045,.029),(.020,.062),(0,.069),(-.020,.062),(-.045,.029)],
        -.133,.012,black,shin,'A14','Greave_'+label,.0011,f,ridge=.002)
    strip('A14_KneeChevron',[(-.024,-.148,.007),(0,-.153,.022),(.024,-.148,.007)],.0034,edge,shin,'A14','Greave_'+label,f,pool=True)
    plate('A15_LowerShinGuard_'+label,[(-.041,L-.105),(-.030,L-.123),(.030,L-.123),(.041,L-.105),(.035,L-.058),(0,L-.035),(-.035,L-.058)],
        -.112,.008,black,shin,'A15','Greave_'+label,.0009,f)
    # Compact ankle catches visually link the shin to the boot without a rig change.
    for ss in [-1,1]:
        strip('A15_AnkleLock',[(ss*.035,-.121,L-.113),(ss*.031,-.123,L-.063)],
            .0042,edge,shin,'A15','Greave_'+label,f,pool=True)

# Helmet details are deliberately concentrated around temple service and jaw vents;
# the original narrow V visor and vertical slit stay clean and recognizable.
for sg,label in [(-1,'L'),(1,'R')]:
    side=lambda p,s=sg:Vector((s*(.147-p[1]),p[0]+.015,p[2]+1.704))
    cassette('H07_JawExtractor_'+label,0,-.002,0,.040,.039,'Head','H07','Helmet',side)
    strip('H03_CrownEdge',[(sg*.035,-.103,1.844),(sg*.035,-.031,1.893),(sg*.033,.045,1.883)],
        .0024,edge,'Head','H03','Helmet',pool=True)

# Capped sternum struts frame the core; tiny pieces stay consolidated by material.
for sg in [-1,1]:
    strip('A17_CoreStrut',[(sg*.018,-.244,1.486),(sg*.060,-.263,1.429)],.0048,edge,'Chest','A17','Cuirass',pool=True)
