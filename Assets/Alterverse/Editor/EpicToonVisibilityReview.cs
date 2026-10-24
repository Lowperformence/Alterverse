#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Alterverse.Visuals;
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
            public string key,image;public int peakParticles,visiblePixels;public bool shaderSupported,passed;
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
                var report=new Report{unityVersion=Application.unityVersion,graphicsDevice=SystemInfo.graphicsDeviceType.ToString(),
                    libraryVersion=library.sourceVersion,note="Native isolated prefab visibility only. Representative colours; actual ability/camera alignment, warning radius and Android performance still need acceptance. Nerok remains unchanged.",results=results.ToArray()};
                File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true));
                var html=new System.Text.StringBuilder("<!doctype html><meta charset='utf-8'><title>Epic VFX review</title><style>body{background:#111;color:#eee;font:16px sans-serif}main{display:flex;flex-wrap:wrap;gap:12px}figure{margin:0;padding:10px;background:#222}img{width:160px;height:160px}small{display:block}</style><h1>Epic VFX native review</h1><p>Isolated source prefabs. Representative colours; real attacks and Android still need review. Nerok is excluded.</p><main>");
                foreach(var result in results)html.Append("<figure><img src='").Append(result.image).Append("'><figcaption>").Append(result.key).Append(result.passed?" — PASS":" — FAIL").Append("</figcaption><small>Particles: ").Append(result.peakParticles).Append(" / pixels: ").Append(result.visiblePixels).Append("</small></figure>");
                File.WriteAllText(Path.Combine(directory,"index.html"),html.Append("</main>").ToString());
                Debug.Log("DF: native VFX visibility review "+(results.Count-failed)+"/"+results.Count+" passed. Report and PNG previews: "+directory+"\n"+summary);
                if(failed>0)throw new InvalidOperationException(failed+" Epic families did not render visible supported pixels. See Library/DF_EpicVfxReview/report.json.");
            }
            finally{Object.DestroyImmediate(sheet);}
        }
        private static Result RenderOne(EpicVfxLibrary.Entry entry,string directory,Texture2D sheet,int index)
        {
            var preview=new PreviewRenderUtility();Texture2D best=null;
            var result=new Result{key=entry.key,image=entry.key+".png",shaderSupported=true};
            try
            {
                var root=Object.Instantiate(entry.prefab);preview.AddSingleGO(root);
                root.transform.position=Vector3.zero;root.transform.rotation=Quaternion.identity;
                root.transform.localScale=Vector3.one*entry.scale;root.SetActive(true);
                var camera=preview.camera;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(0,0,0,1);camera.orthographic=true;camera.orthographicSize=4;
                camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.allowHDR=false;
                camera.transform.position=new Vector3(0,5,-8);camera.transform.LookAt(Vector3.zero);
                var tint=new MaterialPropertyBlock();tint.SetColor(Shader.PropertyToID("_Tint"),PreviewTint(entry.key));
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
                int minimumPixels=entry.key=="Portal"||entry.key=="Vortex"?24:3;
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
        private static Color PreviewTint(string key)=>key switch
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
#endif
