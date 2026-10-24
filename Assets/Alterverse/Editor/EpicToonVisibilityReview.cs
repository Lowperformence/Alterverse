#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Alterverse.Visuals;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Alterverse.Editor
{
    // Native Editor readback, isolated from the user's scene. This is a visibility smoke
    // test and a review sheet, not proof of animation alignment or Android frame rate.
    internal static class EpicToonVisibilityReview
    {
        private const string LibraryPath="Assets/Alterverse/Resources/CombatVfx/EpicToon/Library.asset";
        private const int Size=160;
        private static bool queued;
        [Serializable] private sealed class Result
        {
            public string key,image,hero,action,phase;public int peakParticles,visiblePixels;public bool shaderSupported,passed;
        }
        [Serializable] private sealed class Report
        {
            public string unityVersion,graphicsDevice,libraryVersion,note;
            public Result[] results;
        }
        internal static void Schedule()
        {
            if(queued||Application.isBatchMode)return;
            queued=true;EditorApplication.delayCall+=RunQueued;
        }
        private static void RunQueued()
        {
            queued=false;
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){Schedule();return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode||SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            string fingerprint=AssetDatabase.GetAssetDependencyHash(LibraryPath)+"_"+
                AssetDatabase.GetAssetDependencyHash("Assets/Alterverse/Scripts/Visuals/HeroVfxPool.cs")+"_"+
                AssetDatabase.GetAssetDependencyHash("Assets/Alterverse/Scripts/Visuals/HeroVfxPool.EpicToon.cs")+"_"+
                AssetDatabase.GetAssetDependencyHash("Assets/Alterverse/Editor/EpicToonVisibilityReview.cs");
            string key="DF_EpicVisibility_r1_"+fingerprint;
            if(SessionState.GetBool(key,false))return;
            SessionState.SetBool(key,true);
            try{RenderAll();}
            catch(Exception error){Debug.LogError("DF: automatic VFX visibility review failed: "+error.Message);}
        }
        [MenuItem("DIMENSIONS FRONT/VFX/Render Epic Toon visibility review")]
        public static void RenderAll()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before the isolated visibility review.");
            EpicToonReplacementBuilder.Validate();
            var library=AssetDatabase.LoadAssetAtPath<EpicVfxLibrary>(LibraryPath);
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Library/DF_EpicVfxReview"));
            Directory.CreateDirectory(directory);
            var results=new List<Result>();var summary=new System.Text.StringBuilder();
            var sheet=new Texture2D(Size*6,Size*Mathf.Max(1,Mathf.CeilToInt(library.entries.Length/6f)),TextureFormat.RGBA32,false,false);
            var background=new Color32[sheet.width*sheet.height];
            for(int i=0;i<background.Length;i++)background[i]=new Color32(0,0,0,255);
            sheet.SetPixels32(background);int failed=0,index=0;
            try
            {
                foreach(var entry in library.entries)
                {
                    var result=RenderOne(entry,directory,sheet,index++);results.Add(result);
                    if(!result.passed)failed++;
                    summary.AppendLine((result.passed?"PASS ":"FAIL ")+result.key+": particles="+result.peakParticles+", pixels="+result.visiblePixels+", shader="+result.shaderSupported);
                }
                sheet.Apply(false,false);File.WriteAllBytes(Path.Combine(directory,"contact-sheet.png"),sheet.EncodeToPNG());
                RenderCrowded(library,directory);
                var report=new Report{unityVersion=Application.unityVersion,graphicsDevice=SystemInfo.graphicsDeviceType.ToString(),
                    libraryVersion=library.sourceVersion,note="Native isolated prefab visibility only. Representative colours; actual ability/camera alignment, warning radius and Android performance still need acceptance. Nerok and Timmy retain their original visuals.",results=results.ToArray()};
                File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true));
                var html=new System.Text.StringBuilder("<!doctype html><meta charset='utf-8'><title>Epic VFX review</title><style>body{background:#111;color:#eee;font:16px sans-serif}main{display:flex;flex-wrap:wrap;gap:12px}figure{margin:0;padding:10px;background:#222}img{width:160px;height:160px}small{display:block}</style><h1>Epic VFX native review</h1><p>Isolated source and attack compositions. Representative colours; combat/camera and Android still need review. Nerok and Timmy are excluded.</p><main>");
                foreach(var result in results)html.Append("<figure><img src='").Append(result.image).Append("'><figcaption>").Append(result.key).Append(result.passed?" — PASS":" — FAIL").Append("</figcaption><small>Particles: ").Append(result.peakParticles).Append(" / pixels: ").Append(result.visiblePixels).Append("</small></figure>");
                html.Append("</main><h2>Gemeinsame Ansicht — 640 × 360</h2><p>Oben/unten je vier Helden: Veyr, Kaelon, Nyra, Elyra, Mira, Rook, Traveler, Void Knight. Native VFX-Kompositionen in echter Weltgroesse; keine Abnahme von Gameplay-Kamera, Animation, Sounds oder Android-Leistung. Nyras Q ist ein Runtime-Strahl und fehlt in dieser Prefab-Ansicht.</p>");
                for(int action=-1;action<4;action++)foreach(int frame in new[]{0,1,2})
                    html.Append("<figure><img style='width:640px;height:360px;max-width:100%;object-fit:contain' src='crowded_").Append(action+1).Append('_').Append(frame).Append(".png'><figcaption>").Append(action<0?"Standardangriffe":action==3?"Ultimates":"Fähigkeit "+(action+1)).Append(" / Phase ").Append(frame+1).Append("</figcaption></figure>");
                File.WriteAllText(Path.Combine(directory,"index.html"),html.ToString());
                Debug.Log("DF: native VFX visibility review "+(results.Count-failed)+"/"+results.Count+" passed. Report and PNG previews: "+directory+"\n"+summary);
                if(failed>0)throw new InvalidOperationException(failed+" Epic families did not render visible supported pixels. See Library/DF_EpicVfxReview/report.json.");
            }
            finally{Object.DestroyImmediate(sheet);}
        }
        private static Result RenderOne(EpicVfxLibrary.Entry entry,string directory,Texture2D sheet,int index)
        {
            var preview=new PreviewRenderUtility();Texture2D best=null;
            var result=new Result{key=entry.key,image=entry.key+".png",shaderSupported=true};
            var look=EpicVfxLibrary.FindLookByKey(entry.key);
            if(look!=null){result.hero=look.Hero;result.action=look.Name;result.phase=entry.key.EndsWith("_Cast")?"preparation":entry.key.EndsWith("_Hit")?"hit":"main";}
            try
            {
                var root=Object.Instantiate(entry.prefab);preview.AddSingleGO(root);
                root.transform.position=Vector3.zero;root.transform.rotation=Quaternion.identity;
                root.transform.localScale=Vector3.one*(PreviewRadius(entry)/Mathf.Max(.01f,entry.boundsRadius));root.SetActive(true);
                var camera=preview.camera;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(0,0,0,1);camera.orthographic=true;camera.orthographicSize=8;
                camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.allowHDR=false;
                camera.transform.position=new Vector3(0,5,-8);camera.transform.LookAt(Vector3.zero);
                var tint=PreviewBlock(entry,Vector3.zero,Vector3.forward);
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.SetPropertyBlock(tint);
                    foreach(var material in renderer.sharedMaterials)
                    {
                        if(material==null)continue;
                        if(material.shader==null||!material.shader.isSupported){result.shaderSupported=false;continue;}
                        for(int pass=0;pass<material.passCount;pass++)ShaderUtil.CompilePass(material,pass,true);
                        if(ShaderUtil.ShaderHasError(material.shader))result.shaderSupported=false;
                    }
                }
                // Compare with an EMPTY render from this same preview camera. A failed camera
                // render or a non-black editor background must not count as a visible effect.
                root.SetActive(false);
                var empty=Capture(preview);var baseline=empty.GetPixels32();Object.DestroyImmediate(empty);
                root.SetActive(true);
                var systems=root.GetComponentsInChildren<ParticleSystem>(true);
                foreach(float sample in new[]{.06f,.18f,.40f})
                {
                    int count=0;
                    foreach(var system in systems)
                    {system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);system.Play(false);system.Simulate(sample,false,true,false);system.Pause(false);count+=system.particleCount;}
                    result.peakParticles=Mathf.Max(result.peakParticles,count);
                    var frame=Capture(preview);int pixels=0;var colours=frame.GetPixels32();
                    for(int i=0;i<colours.Length;i++)
                        if(Mathf.Abs(colours[i].r-baseline[i].r)>8||Mathf.Abs(colours[i].g-baseline[i].g)>8||Mathf.Abs(colours[i].b-baseline[i].b)>8)pixels++;
                    if(best==null||pixels>result.visiblePixels)
                    {if(best!=null)Object.DestroyImmediate(best);best=frame;result.visiblePixels=pixels;}
                    else Object.DestroyImmediate(frame);
                }
                // A few sparkle pixels must not hide an edge-on, unreadable portal disc.
                string source=look?.Source??entry.key;
                int minimumPixels=source=="Portal"||source=="Vortex"?24:3;
                result.passed=result.shaderSupported&&result.peakParticles>0&&result.visiblePixels>=minimumPixels;
                if(best!=null)
                {
                    File.WriteAllBytes(Path.Combine(directory,result.image),best.EncodeToPNG());
                    int x=(index%6)*Size,y=sheet.height-((index/6)+1)*Size;
                    sheet.SetPixels32(x,y,Size,Size,best.GetPixels32());
                }
                return result;
            }
            finally{if(best!=null)Object.DestroyImmediate(best);preview.Cleanup();}
        }
        private static Texture2D Capture(PreviewRenderUtility preview)
        {
            preview.BeginStaticPreview(new Rect(0,0,Size,Size));Texture2D frame=null;
            try{preview.Render(true,false);}
            finally{frame=preview.EndStaticPreview();}
            return frame;
        }
        private static float PreviewRadius(EpicVfxLibrary.Entry entry)
        {
            var look=EpicVfxLibrary.FindLookByKey(entry.key);
            if(look==null)return entry.key.StartsWith("Weather")?5f:entry.key=="BomberNuke"?3.24f:1f;
            if(entry.key.EndsWith("_Hit"))return look.Slot==3?.24f:.18f;
            if(entry.key.EndsWith("_Cast"))return .48f;
            var hero=(HeroId)Enum.Parse(typeof(HeroId),look.Hero);var spec=AimingSettings.Current.For(hero,look.Slot);
            if(spec.aimType==AimType.Melee||spec.aimType==AimType.AreaCone)return spec.castRange*.82f;
            if(spec.aimType==AimType.Area||spec.aimType==AimType.SelfArea)return spec.aoeRadius*.96f;
            if(spec.aimType==AimType.Projectile||spec.aimType==AimType.Beam)return spec.projectileWidth*.85f;
            if(spec.aimType==AimType.Dash&&(look.Source=="Portal"||look.Source=="SpinPortal"))return .85f*EpicVfxLibrary.PortalScaleMultiplier;
            return .6f;
        }
        private static MaterialPropertyBlock PreviewBlock(EpicVfxLibrary.Entry entry,Vector3 center,Vector3 direction)
        {
            var block=new MaterialPropertyBlock();block.SetColor(Shader.PropertyToID("_Tint"),PreviewTint(entry.key));
            block.SetFloat(Shader.PropertyToID("_NativePalette"),EpicVfxLibrary.FindLookByKey(entry.key)==null?1f:0f);
            float radius=PreviewRadius(entry);block.SetVector(Shader.PropertyToID("_AttackClip"),new Vector4(center.x,center.z,radius,1));
            return block;
        }
        private static void RenderCrowded(EpicVfxLibrary library,string directory)
        {
            string[] heroes={"Veyr","Kaelon","Nyra","Elyra","Mira","Rook","Traveler","VoidKnight"};
            for(int action=-1;action<4;action++)
            {
                var preview=new PreviewRenderUtility();
                try
                {
                    var camera=preview.camera;camera.orthographic=true;camera.orthographicSize=6.5f;
                    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.055f,.07f);
                    camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.allowHDR=false;
                    camera.transform.position=new Vector3(0,12,-12);camera.transform.LookAt(Vector3.zero);
                    var roots=new List<GameObject>();
                    for(int i=0;i<heroes.Length;i++)
                    {
                        var look=EpicVfxLibrary.FindLook(heroes[i],action);var entry=library.Find(look.Key);if(entry?.prefab==null)continue;
                        var root=Object.Instantiate(entry.prefab);preview.AddSingleGO(root);roots.Add(root);
                        Vector3 point=new Vector3((i%4-1.5f)*2.25f,.25f,i<4?-1.2f:1.2f);
                        Vector3 forward=i<4?Vector3.forward:Vector3.back;
                        root.transform.SetPositionAndRotation(point,Quaternion.LookRotation(forward));
                        root.transform.localScale=Vector3.one*(PreviewRadius(entry)/Mathf.Max(.01f,entry.boundsRadius));root.SetActive(true);
                        var block=PreviewBlock(entry,point,forward);
                        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))renderer.SetPropertyBlock(block);
                    }
                    float[] samples={.06f,.18f,.40f};
                    for(int frame=0;frame<samples.Length;frame++)
                    {
                        foreach(var root in roots)foreach(var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                        {ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);ps.Play(false);ps.Simulate(samples[frame],false,true,false);ps.Pause(false);}
                        preview.BeginStaticPreview(new Rect(0,0,640,360));Texture2D image=null;
                        try{preview.Render(true,false);}finally{image=preview.EndStaticPreview();}
                        try{File.WriteAllBytes(Path.Combine(directory,"crowded_"+(action+1)+"_"+frame+".png"),image.EncodeToPNG());}
                        finally{if(image!=null)Object.DestroyImmediate(image);}
                    }
                }
                finally{preview.Cleanup();}
            }
        }
        private static Color PreviewTint(string key)
        {
            var look=EpicVfxLibrary.FindLookByKey(key);
            if(look!=null)return look.Hero switch
            {
                "Kaelon"=>look.Slot==0?new Color(.68f,.20f,1f):look.Slot==2?new Color(.08f,.46f,1f):new Color(1f,.84f,.06f),
                "Nyra"=>look.Slot==0?new Color(.06f,.38f,1f):look.Slot==2?Color.white:look.Slot==3?new Color(1f,.045f,.025f):new Color(.30f,.85f,1f),
                "Mira"=>look.Slot==1||look.Slot==2?new Color(.22f,1f,.52f):new Color(1f,.72f,.28f),
                "Rook"=>new Color(1f,.38f,.09f),
                "Traveler"=>look.Slot==3?Color.white:look.Slot==0||look.Slot==1?new Color(1f,.82f,.08f):new Color(.64f,.24f,1f),
                _=>new Color(.76f,.24f,1f)
            };
            return key switch
            {
            "Heal"=>new Color(.22f,1f,.52f),
            "NatureMissile" or "NatureImpact" or "NatureNova" or "Nova" or "Orb" or "HeavyImpact"=>new Color(1f,.72f,.28f),
            "Fire" or "FireImpact" or "FireNova" or "Punch"=>new Color(1f,.38f,.09f),
            "Claw" or "Jet" or "Smoke" or "Water" or "Vortex" or "Shield"=>new Color(.30f,.85f,1f),
            "Missile" or "Muzzle"=>new Color(.06f,.38f,1f),
                _=>new Color(.76f,.24f,1f)
            };
        }
    }
}
#endif
