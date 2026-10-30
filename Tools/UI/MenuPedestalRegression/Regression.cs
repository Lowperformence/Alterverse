using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alterverse.Characters;
using Alterverse.UI;
using UnityEngine;

static class TestRig
{
    public static Transform Part(string name,Transform parent,Vector3 position)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
    public static void Build(GameObject actor)
    {
        if(actor.transform.Find("DF_VisualRoot")!=null)return;
        HeroId hero=actor.GetComponent<HeroIdentity>()?.Hero??HeroId.Veyr;
        var root=Part("DF_VisualRoot",actor.transform,new Vector3(0,.32f,0));
        float scale=hero==HeroId.Timmy?.64f:hero==HeroId.Bolt7?1.22f:1f;
        root.localScale=new Vector3(1.22f,1.32f,1.22f)*scale;
        var model=Part("DF_AuthoredModel",root,new Vector3(0,-.96f,0));model.localScale=new Vector3(1.10f,1,1.10f);
        var hips=Part("Hips",model,new Vector3(0,1.65f,0));Part("Head",hips,new Vector3(0,.85f,0));
        var bones=new List<Transform>();var vertices=new List<Vector3>();var weights=new List<BoneWeight>();var faces=new List<int>();
        foreach(bool right in new[]{false,true})
        {
            string suffix=right?"R":"L";
            var hip=Part("Thigh"+suffix,hips,new Vector3(right?.23f:-.23f,0,0));
            var knee=Part("Shin"+suffix,hip,new Vector3(0,-.56f,.12f));
            string footName=hero==HeroId.Bolt7?(right?"Foot_R":"Foot_L"):hero==HeroId.Timmy?(right?"mixamorig:RightFoot":"mixamorig:LeftFoot"):"Foot"+suffix;
            var foot=Part(footName,knee,new Vector3(0,right?-.52f:-.56f,-.12f));
            int bone=bones.Count;bones.Add(foot);
            int start=vertices.Count;
            for(int i=0;i<8;i++)
            {
                Vector3 p=new((i&1)==0?-.12f:.12f,(i&2)==0?-.09f:.16f,(i&4)==0?-.14f:.28f);
                vertices.Add(model.InverseTransformPoint(foot.TransformPoint(p)));
                weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});
            }
            foreach(int i in new[]{0,1,5,0,5,4,2,6,7,2,7,3,0,2,3,0,3,1})faces.Add(start+i);
        }
        // Unreferenced foot-weighted vertex and a low weapon must not become soles.
        vertices.Add(new Vector3(0,-25,0));weights.Add(new BoneWeight{boneIndex0=0,weight0=1});
        var body=Part("Body",model,Vector3.zero).gameObject;
        var skin=body.AddComponent<SkinnedMeshRenderer>();skin.bones=bones.ToArray();
        skin.sharedMesh=new Mesh{vertices=vertices.ToArray(),boneWeights=weights.ToArray(),
            bindposes=bones.Select(b=>b.worldToLocalMatrix*model.localToWorldMatrix).ToArray(),triangles=faces.ToArray()};
        var weapon=Part("WeaponBelowFeet",model,new Vector3(1,-4,0)).gameObject;
        weapon.AddComponent<MeshFilter>().sharedMesh=new Mesh{vertices=new[]{new Vector3(0,-3,0)},triangles=new[]{0,0,0}};
        weapon.AddComponent<MeshRenderer>();
        actor.AddComponent<Alterverse.Visuals.HeroGroundedLocomotion>();
    }
    public static float ActualSole(SkinnedMeshRenderer skin,int bone)
    {
        Mesh mesh=skin.sharedMesh;float minimum=float.PositiveInfinity;
        Vector3[] points=mesh.vertices;BoneWeight[] weights=mesh.boneWeights;var bind=mesh.bindposes;
        foreach(int i in mesh.triangles)if(weights[i].boneIndex0==bone)
            minimum=Mathf.Min(minimum,skin.bones[bone].localToWorldMatrix.MultiplyPoint3x4(bind[bone].MultiplyPoint3x4(points[i])).y);
        return minimum;
    }
}

static class Regression
{
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    static void Call(object target,string method,params object[] values)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,values);
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    static void Main()
    {
        foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))
        foreach(float origin in new[]{0f,12000f})
        {
            var stage=new GameObject("stage").transform;stage.position=new Vector3(origin,origin,origin);
            var actor=TestRig.Part("preview",stage,Vector3.zero).gameObject;
            actor.AddComponent<HeroIdentity>().Configure(hero);TestRig.Build(actor);
            actor.transform.localRotation=Quaternion.Euler(0,-16,0);
            var skin=actor.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=skin.sharedMesh;
            var grounding=new MenuHeroGrounding(actor);grounding.Prepare();
            Check(!actor.GetComponent<Alterverse.Visuals.HeroGroundedLocomotion>().enabled,"only preview terrain grounding is disabled");
            Check(grounding.PlaceFeet(stage,MenuHeroPedestal.SurfaceY)&&grounding.Ready,"both named foot families resolve: "+hero);
            float floor=stage.position.y+MenuHeroPedestal.SurfaceY;
            float left=TestRig.ActualSole(skin,0),right=TestRig.ActualSole(skin,1);
            Check(Math.Abs(left-floor)<=.003f&&Math.Abs(right-floor)<=.003f,
                $"both actual soles touch the deck: {hero} at {origin}; gaps {left-floor:F6}/{right-floor:F6}");
            Check(grounding.Radius>=.88f&&grounding.Radius<=1.5f,"platform radius remains bounded and covers feet");
            int reads=mesh.Reads;
            for(int frame=0;frame<40;frame++)
            {
                actor.transform.localRotation=Quaternion.Euler(0,-16+Mathf.Sin(frame*.1f)*4,0);
                actor.transform.localPosition=Vector3.zero;
                grounding.PlaceFeet(stage,MenuHeroPedestal.SurfaceY);
            }
            Check(mesh.Reads==reads,"idle updates reuse cached support points without mesh reads");
            left=TestRig.ActualSole(skin,0);right=TestRig.ActualSole(skin,1);
            Check(Math.Abs(left-floor)<=.003f&&Math.Abs(right-floor)<=.003f,"no cumulative drift while turning on deck");
        }

        var host=new GameObject("menu stage");var pedestal=new MenuHeroPedestal(host.transform,31);
        Check(host.GetComponentsInChildren<Renderer>().Length==2,"one three-material deck plus one contact pass");
        Check(host.GetComponentsInChildren<Transform>().All(t=>t==host.transform||t.gameObject.layer==31),"all pedestal geometry is isolated to the portrait layer");
        var deck=host.GetComponentsInChildren<MeshFilter>().First().sharedMesh;
        Vector3[] verts=deck.vertices;int triangles=0;
        for(int sub=0;sub<3;sub++)
        {
            int[] faces=deck.GetTriangles(sub);triangles+=faces.Length/3;
            foreach(int i in faces)Check(i>=0&&i<verts.Length,"pedestal mesh index valid");
            for(int i=0;i<faces.Length;i+=3)
            {
                var a=verts[faces[i]];var b=verts[faces[i+1]];var c=verts[faces[i+2]];
                var normal=Vector3.Cross(b-a,c-a);
                Check(normal.sqrMagnitude>1e-12f,"deck contains no zero-area triangles");
                if(sub==1)Check(normal.y>0&&a.y==0&&b.y==0&&c.y==0,"flat top is upward-facing at the exact stand plane");
                if(sub==0&&a.y<-.03f&&b.y<-.03f&&c.y<-.03f)
                    Check(Vector3.Dot(normal,new Vector3((a.x+b.x+c.x)/3,0,(a.z+b.z+c.z)/3))>0,"shell normals face outward");
            }
        }
        Check(triangles<1000&&verts.Length<2500,"deck mesh stays compact for mobile");
        pedestal.SetRadius(1.2f);
        Check(Math.Abs(pedestal.Bounds.size.x-2.4f)<.0001f&&Math.Abs(pedestal.Bounds.max.y-MenuHeroPedestal.SurfaceY)<.0001f,"framing bounds cover full deck at exact surface height");
        int materials=Material.Created.Count,meshes=Mesh.Created.Count;
        pedestal.Dispose();pedestal.Dispose();
        Check(host.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh.Destroyed),"owned deck and contact meshes released idempotently");

        CheckInitialStandPose();
        var menu=new GameObject("showcase").AddComponent<MenuHeroShowcase>();
        menu.Present(HeroId.Veyr,true);Call(menu,"Update");Call(menu,"LateUpdate");
        Check(menu.Image==null&&!Field<MenuHeroGrounding>(menu,"grounding").Ready,
            "A newly constructed actor is not exposed or foot-solved before its Start/pose frame");
        Time.frameCount++;Call(menu,"Update");Call(menu,"LateUpdate");
        var first=Field<GameObject>(menu,"actor");var originalGrounding=Field<MenuHeroGrounding>(menu,"grounding");
        var actualDeck=Field<MenuHeroPedestal>(menu,"pedestal");var camera=Field<Camera>(menu,"previewCamera");
        Check(originalGrounding.Ready&&menu.Image!=null&&camera.targetTexture!=null,"full production showcase exposes only a grounded, fitted portrait");
        Bounds deckBounds=actualDeck.Bounds;
        for(int corner=0;corner<8;corner++)
        {
            Vector3 world=deckBounds.center+Vector3.Scale(deckBounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
            Vector3 view=camera.transform.InverseTransformPoint(world);
            Check(Math.Abs(view.y)<camera.orthographicSize&&Math.Abs(view.x)<camera.orthographicSize*1536/1728f,"entire stand stays inside portrait camera");
        }
        menu.Present(HeroId.Timmy,true);Time.frameCount++;Call(menu,"Update");Call(menu,"LateUpdate");
        menu.Present(HeroId.Veyr,true);Time.frameCount++;Call(menu,"Update");Call(menu,"LateUpdate");
        Check(Field<GameObject>(menu,"actor")==first&&Field<MenuHeroGrounding>(menu,"grounding")==originalGrounding,"cached hero retains measured soles across switching");
        Check(Field<MenuHeroPedestal>(menu,"pedestal")==actualDeck,"all heroes share one stage pedestal");
        menu.Present(HeroId.Bolt7,true);Time.frameCount++;Call(menu,"Update");Call(menu,"LateUpdate");
        Check(Field<MenuHeroGrounding>(menu,"grounding").Ready,"dedicated BOLT rig reaches complete preview grounding");
        CheckMobileBudget(menu,camera);
        menu.Present(HeroId.Bolt7,false);Check(!Field<GameObject>(menu,"stage").activeSelf,"hidden menu also hides the pedestal");
        var target=(RenderTexture)menu.Image;var owned=Field<GameObject>(menu,"stage").GetComponentsInChildren<MeshFilter>().Where(f=>f.gameObject.name.StartsWith("DF_Menu")).Select(f=>f.sharedMesh).ToArray();
        Call(menu,"OnDestroy");Check(!target.Live&&target.Destroyed&&owned.All(m=>m.Destroyed),"showcase teardown releases stage resources");
        Console.WriteLine($"PASS menu pedestal: {checks} geometry, sole, cache, framing and lifetime checks; synthetic meshes/affine engine fixtures, no native Unity screenshot.");
    }
    static void CheckInitialStandPose()
    {
        foreach(float origin in new[]{0f,12000f})
        foreach(int fps in new[]{30,60})
        {
            Time.deltaTime=1f/fps;
            var stage=new GameObject("entry pose").transform;stage.position=new Vector3(origin,origin,origin);
            var actor=TestRig.Part("portrait",stage,Vector3.zero).gameObject;
            actor.AddComponent<HeroIdentity>().Configure(HeroId.Veyr);
            actor.AddComponent<Alterverse.Shop.SkinPreviewSelection>();TestRig.Build(actor);
            var feet=actor.GetComponentInChildren<SkinnedMeshRenderer>().bones;
            var pose=actor.AddComponent<Alterverse.Visuals.HeroProceduralRig>();
            pose.TestBind(feet[0].parent.parent,feet[0].parent,feet[0],feet[1].parent.parent,feet[1].parent,feet[1]);
            var grounding=new MenuHeroGrounding(actor);grounding.Prepare();
            Quaternion hip=Quaternion.Euler(-8,0,0),knee=Quaternion.Euler(24,0,0),ankle=Quaternion.Euler(-8,0,0);
            void Pose()
            {
                Time.frameCount++;actor.transform.localPosition=Vector3.zero;
                pose.SetProceduralPose(1,hip,knee,ankle,hip,knee,ankle,Quaternion.identity,Quaternion.identity,Quaternion.identity,Quaternion.identity);
                Call(pose,"LateUpdate");
            }
            Pose();
            Check(Quaternion.Angle(feet[1].parent.localRotation,knee)<.05f,
                "A menu actor reaches its standing knee pose on the first posed frame");
            grounding.PlaceFeet(stage,MenuHeroPedestal.SurfaceY);
            Transform head=actor.GetComponentsInChildren<Transform>().First(t=>t.name=="Head");
            float height=head.position.y;
            for(int frame=0;frame<fps;frame++)
            {
                Pose();grounding.PlaceFeet(stage,MenuHeroPedestal.SurfaceY);
                Check(Math.Abs(head.position.y-height)<.003f,
                    $"No entry-height settling at {fps} FPS and stage origin {origin}");
            }
            var combat=new GameObject("combat pose");
            var combatPose=combat.AddComponent<Alterverse.Visuals.HeroProceduralRig>();
            Transform Joint(string name)=>TestRig.Part(name,combat.transform,Vector3.zero);
            Transform combatKnee=Joint("knee");
            combatPose.TestBind(Joint("hip"),combatKnee,Joint("ankle"),Joint("hipR"),Joint("kneeR"),Joint("ankleR"));
            Time.frameCount++;
            combatPose.SetProceduralPose(1,hip,knee,ankle,hip,knee,ankle,Quaternion.identity,Quaternion.identity,Quaternion.identity,Quaternion.identity);
            Call(combatPose,"LateUpdate");
            Check(Quaternion.Angle(combatKnee.localRotation,knee)>1f,
                "Actors without the preview marker retain their normal smooth pose transition");
        }
        Time.deltaTime=1f/60f;
    }

    static void CheckMobileBudget(MenuHeroShowcase menu,Camera camera)
    {
        var original=camera.targetTexture;var actor=Field<GameObject>(menu,"actor");float size=camera.orthographicSize;
        Check(original.width==1536&&original.height==1728&&original.antiAliasing==4,"Retain desktop portrait target");
        Application.isMobilePlatform=true;
        Alterverse.Performance.MobilePerformanceProfile.Current=Alterverse.Performance.MobileQuality.Balanced;
        Call(menu,"LateUpdate");var balanced=camera.targetTexture;
        Check(balanced.width==1024&&balanced.height==1152&&balanced.antiAliasing==2,"Bound balanced Android portrait cost");
        Check(original.Destroyed&&!original.Live,"Release replaced target");
        Call(menu,"LateUpdate");Check(camera.targetTexture==balanced,"Reuse target while quality stays unchanged");
        Alterverse.Performance.MobilePerformanceProfile.Current=Alterverse.Performance.MobileQuality.Battery;
        Call(menu,"LateUpdate");Check(camera.targetTexture.width==768&&camera.targetTexture.height==864&&camera.targetTexture.antiAliasing==1,"Battery portrait budget");
        Check(Application.targetFrameRate==30,"Battery frame pacing remains 30 FPS");
        var battery=camera.targetTexture;
        Alterverse.Performance.MobilePerformanceProfile.Current=Alterverse.Performance.MobileQuality.Performance;
        RenderTexture.FailCreate=true;Call(menu,"LateUpdate");
        Check(camera.targetTexture==battery&&!battery.Destroyed,"A failed target allocation retains the previous image");
        RenderTexture.FailCreate=false;Call(menu,"LateUpdate");
        Check(camera.targetTexture.width==1536&&camera.targetTexture.antiAliasing==4&&Application.targetFrameRate==60,"High quality and 60 FPS remain available");
        Check(Field<GameObject>(menu,"actor")==actor&&camera.orthographicSize==size,"Quality changes preserve hero size, camera framing and actor identity");
        Time.frameCount++;int reads=Alterverse.Profile.AccountProfilePrefs.Reads;
        for(int i=0;i<2000;i++){var q=Alterverse.Performance.MobilePerformanceProfile.Current;}
        Check(Alterverse.Profile.AccountProfilePrefs.Reads==reads+1,"Read mobile quality once per frame, even for many effects");
        Alterverse.Profile.AccountProfilePrefs.Account="second-account";Alterverse.Profile.AccountProfilePrefs.Value=0;
        Check(Alterverse.Performance.MobilePerformanceProfile.Current==Alterverse.Performance.MobileQuality.Battery,"Account switch invalidates cache immediately");
        Alterverse.Performance.MobilePerformanceProfile.Current=Alterverse.Performance.MobileQuality.Balanced;
        Check(Alterverse.Performance.MobilePerformanceProfile.Current==Alterverse.Performance.MobileQuality.Balanced,"Settings write invalidates cache in the same frame");
        Alterverse.Profile.AccountProfilePrefs.Value=2;Time.frameCount++;
        Check(Alterverse.Performance.MobilePerformanceProfile.Current==Alterverse.Performance.MobileQuality.Performance,"Cloud preference changes appear on the next frame");
    }
}
