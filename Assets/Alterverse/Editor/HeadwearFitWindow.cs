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
        enum Hat {None,BetaTruckerCap,UfoTestHat,WizardHat,PropellerCap,MushroomHat}
        Hat hat=Hat.BetaTruckerCap;
        bool wings,closeup=true;
        float yaw=15,pitch,headTilt;
        Quaternion headRest;
        string status="";
        [MenuItem("DIMENSIONS FRONT/Headwear/BETA Cappy fitting studio")]
        public static void Open()=>GetWindow<HeadwearFitWindow>("Headwear studio");
        [MenuItem("DIMENSIONS FRONT/Headwear/Fitting studio")]
        public static void OpenAll()=>Open();
        void OnDisable(){preview?.Cleanup();preview=null;actor=null;}
        void Build()
        {
            preview?.Cleanup();preview=new PreviewRenderUtility();
            var selected=hat switch
            {
                Hat.BetaTruckerCap=>UniversalHeroCosmetic.BetaCap,
                Hat.UfoTestHat=>UniversalHeroCosmetic.UfoTestHat,
                Hat.WizardHat=>UniversalHeroCosmetic.WizardHat,
                Hat.PropellerCap=>UniversalHeroCosmetic.PropellerCap,
                Hat.MushroomHat=>UniversalHeroCosmetic.MushroomHat,
                _=>UniversalHeroCosmetic.Original
            };
            actor=CreatePreviewActor(hero,selected,wings);
            preview.AddSingleGO(actor);
            var view=actor.GetComponent<HeadwearPresentation>();
            if(view.Head!=null)headRest=view.Head.localRotation;
            status=selected!=UniversalHeroCosmetic.Original?(view.Current==selected?
                "Head found: "+view.Head.name+" · bounds "+view.HeadBounds.size: view.LastFitError):"Headwear removed; wing selection retained.";
            preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=50;
            preview.camera.fieldOfView=30;
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
        internal static GameObject CreatePrototypeActor(HeroId id,string resource,bool wings)
        {
            var go=CreatePreviewActor(id,UniversalHeroCosmetic.Original,wings);
            var view=go.GetComponent<HeadwearPresentation>();
            if(view.ApplyPrototype(resource))return go;
            string error=view.LastFitError;DestroyImmediate(go);
            throw new System.InvalidOperationException(resource+": "+error);
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
            if(GUILayout.Button("Check all 5 hats on all 10 heroes"))HeadwearRegression.Run();
            if(preview==null||rebuild)
            {
                try{Build();}catch(System.Exception error){status=error.Message;}
            }
            EditorGUILayout.HelpBox(status??"Head attachment unavailable.",MessageType.None);
            if((int)hat>=(int)Hat.WizardHat)
                EditorGUILayout.HelpBox("Free test hat. Available in Inventory > Cosmetics > Skins. The BETA cap remains a premium Battle Pass reward.",MessageType.Info);
            string art=hat==Hat.BetaTruckerCap?"BetaCap":hat.ToString();
            var icon=hat==Hat.None?null:Resources.Load<Texture2D>("Headwear/"+art+"Icon");
            if(icon!=null)GUILayout.Label(icon,GUILayout.Height(100));
            Rect rect=GUILayoutUtility.GetRect(100,10000,200,10000);
            if(actor==null||preview==null||rect.width<1||rect.height<1)return;
            Event e=Event.current;
            if(e.type==EventType.MouseDrag&&rect.Contains(e.mousePosition))
            {yaw+=e.delta.x*.6f;pitch=Mathf.Clamp(pitch+e.delta.y*.3f,-30,30);e.Use();Repaint();}
            actor.transform.rotation=Quaternion.Euler(0,yaw,0);
            var view=actor.GetComponent<HeadwearPresentation>();
            if(view.Head!=null)view.Head.localRotation=headRest*Quaternion.Euler(headTilt,0,headTilt*.25f);
            Vector3 target=actor.transform.position+Vector3.up*.6f;
            float distance=3.7f;
            if(closeup&&view.Head!=null)
            {
                Transform root=actor.transform.Find("DF_VisualRoot");
                Bounds frame=new Bounds(view.Head.position,Vector3.zero);
                for(int i=0;i<8;i++)
                {
                    Vector3 corner=view.HeadBounds.center+Vector3.Scale(view.HeadBounds.extents,
                        new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    frame.Encapsulate(root.TransformPoint(corner));
                }
                foreach(Transform part in view.Head.GetComponentsInChildren<Transform>(true))
                    if(part.name.StartsWith("DF_Headwear_"))
                    {
                        var renderer=part.GetComponent<Renderer>();
                        if(renderer!=null)frame.Encapsulate(renderer.bounds);
                    }
                target=frame.center;
                float aspect=Mathf.Max(.1f,rect.width/rect.height);
                distance=Mathf.Max(frame.size.y,frame.size.x/aspect)*.5f/Mathf.Tan(15*Mathf.Deg2Rad)*1.2f+frame.extents.z;
            }
            Vector3 offset=Quaternion.Euler(pitch,0,0)*new Vector3(0,0,distance);
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


