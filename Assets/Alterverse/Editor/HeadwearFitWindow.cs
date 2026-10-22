#if UNITY_EDITOR
using Alterverse.Characters;
using Alterverse.Shop;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public sealed class HeadwearFitWindow : EditorWindow
    {
        PreviewRenderUtility preview;
        GameObject actor;
        HeroId hero;
        enum Hat {None,BetaCappy,UfoTestHat}
        Hat hat=Hat.BetaCappy;
        bool wings,closeup=true;
        float yaw=15,pitch,headTilt;
        Quaternion headRest;
        string status="";
        [MenuItem("DIMENSIONS FRONT/Headwear/BETA Cappy fitting studio")]
        public static void Open()=>GetWindow<HeadwearFitWindow>("BETA Cappy");
        void OnDisable(){preview?.Cleanup();preview=null;actor=null;}
        void Build()
        {
            preview?.Cleanup();preview=new PreviewRenderUtility();
            var selected=hat==Hat.BetaCappy?UniversalHeroCosmetic.BetaCap:hat==Hat.UfoTestHat?UniversalHeroCosmetic.UfoTestHat:UniversalHeroCosmetic.Original;
            actor=CreatePreviewActor(hero,selected,wings);
            preview.AddSingleGO(actor);
            var view=actor.GetComponent<HeadwearPresentation>();
            if(view.Head!=null)headRest=view.Head.localRotation;
            status=selected!=UniversalHeroCosmetic.Original?(view.Current==selected?
                "Head found: "+view.Head.name+" · bounds "+view.HeadBounds.size: view.LastFitError):"Headwear removed; wing selection retained.";
            preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=50;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.025f,.035f,.055f);
            preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(35,25,0);
            preview.lights[1].intensity=.8f;preview.lights[1].transform.rotation=Quaternion.Euler(10,210,0);
        }
        internal static GameObject CreatePreviewActor(HeroId id,bool cap,bool wings)
            =>CreatePreviewActor(id,cap?UniversalHeroCosmetic.BetaCap:UniversalHeroCosmetic.Original,wings);
        internal static GameObject CreatePreviewActor(HeroId id,UniversalHeroCosmetic headwear,bool wings)
        {
            var go=new GameObject("DF_HeadwearPreview_"+id){hideFlags=HideFlags.HideAndDontSave};
            go.AddComponent<HeroIdentity>().Configure(id);
            var selection=go.AddComponent<SkinPreviewSelection>();
            selection.UniversalCosmetic=wings?UniversalHeroCosmetic.Voidfall:UniversalHeroCosmetic.Original;
            selection.Headwear=headwear;
            go.AddComponent<StylizedHeroVisual>().Build();
            return go;
        }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Isolated model preview. No ownership, purchases, player saves, levels or scenes are changed.",MessageType.Info);
            EditorGUI.BeginChangeCheck();
            hero=(HeroId)EditorGUILayout.EnumPopup("Hero",hero);
            hat=(Hat)EditorGUILayout.EnumPopup("Headwear",hat);
            wings=EditorGUILayout.Toggle("Preview with Voidfall wings",wings);
            bool rebuild=EditorGUI.EndChangeCheck();
            closeup=EditorGUILayout.Toggle("Head close-up",closeup);
            headTilt=EditorGUILayout.Slider("Head tilt",headTilt,-25,25);
            if(GUILayout.Button("Check head attachment on all 10 heroes"))HeadwearRegression.Run();
            if(preview==null||rebuild)
            {
                try{Build();}catch(System.Exception error){status=error.Message;}
            }
            EditorGUILayout.HelpBox(status??"Head attachment unavailable.",MessageType.None);
            Rect rect=GUILayoutUtility.GetRect(100,10000,200,10000);
            if(actor==null||preview==null||rect.width<1||rect.height<1)return;
            Event e=Event.current;
            if(e.type==EventType.MouseDrag&&rect.Contains(e.mousePosition))
            {yaw+=e.delta.x*.6f;pitch=Mathf.Clamp(pitch+e.delta.y*.3f,-30,30);e.Use();Repaint();}
            actor.transform.rotation=Quaternion.Euler(0,yaw,0);
            var view=actor.GetComponent<HeadwearPresentation>();
            if(view.Head!=null)view.Head.localRotation=headRest*Quaternion.Euler(headTilt,0,headTilt*.25f);
            Vector3 target=closeup&&view.Head!=null?view.Head.position+actor.transform.up*.07f:actor.transform.position+Vector3.up*.6f;
            Vector3 offset=Quaternion.Euler(pitch,0,0)*new Vector3(0,.04f,closeup?1.1f:3.7f);
            preview.camera.transform.position=target+offset;
            preview.camera.transform.rotation=Quaternion.LookRotation(target-preview.camera.transform.position,Vector3.up);
            if(e.type==EventType.Repaint)
            {
                preview.BeginPreview(rect,GUIStyle.none);
                try{preview.Render(allowScriptableRenderPipeline:true);}
                finally{GUI.DrawTexture(rect,preview.EndPreview(),ScaleMode.StretchToFill,false);}
            }
        }
    }
}
#endif
