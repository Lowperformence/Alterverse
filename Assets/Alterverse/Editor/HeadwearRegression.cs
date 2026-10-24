#if UNITY_EDITOR
using System;
using Alterverse.Characters;
using Alterverse.Shop;
using Alterverse.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alterverse.EditorChecks
{
    public static class HeadwearRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/BETA Cappy all-hero attachment")]
        public static void Run()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            int checkedHeroes=0;
            try
            {
                foreach(var item in new[]{UniversalHeroCosmetic.BetaCap,UniversalHeroCosmetic.UfoTestHat,
                    UniversalHeroCosmetic.WizardHat,UniversalHeroCosmetic.PropellerCap,UniversalHeroCosmetic.MushroomHat})
                for(int i=0;i<HeroIdentity.HeroCount;i++)
                {
                    GameObject actor=null;
                    try
                    {
                        string resource=HeadwearSelection.ResourceFor(item);
                        actor=HeadwearFitWindow.CreatePreviewActor((HeroId)i,item,true);
                        SceneManager.MoveGameObjectToScene(actor,scene);
                        var view=actor.GetComponent<HeadwearPresentation>();
                        Require(view!=null&&view.Current==item,
                            (HeroId)i+": "+(view?.LastFitError??"headwear missing"));
                        Require(view.Head!=null,"Head bone missing.");
                        var cap=view.Head.Find("DF_Headwear_"+resource);
                        Require(cap!=null,"Cap is not parented to the real head.");
                        var mesh=cap.GetComponent<MeshFilter>()?.sharedMesh;
                        Require(mesh!=null&&mesh.vertexCount>0&&mesh.vertexCount<6000,"Invalid headwear mesh budget.");
                        Require(mesh.subMeshCount==(resource=="BetaCap"?3:6),"Unexpected headwear material count.");
                        var root=actor.transform.Find("DF_VisualRoot");
                        Vector3 contact=root.InverseTransformPoint(view.Head.TransformPoint(view.SeatInHeadSpace));
                        Require(Mathf.Abs(contact.y-view.CrownSection.center.y)<.0001f&&
                            contact.y<view.HeadBounds.max.y&&contact.y>=view.HeadBounds.center.y,
                            "Head does not enter the hat opening around the upper crown.");
                        Require(cap.GetComponentsInChildren<Collider>(true).Length==0,"Cosmetic introduced a collider.");
                        Vector3 position=cap.localPosition,scale=cap.localScale;
                        Quaternion rotation=cap.localRotation;
                        view.Head.localRotation*=Quaternion.Euler(15,20,10);
                        Require(cap.parent==view.Head&&cap.localPosition==position&&cap.localScale==scale&&cap.localRotation==rotation,
                            "Cap did not retain its head attachment under a head pose.");
                        int count=actor.GetComponentsInChildren<MeshFilter>(true).Length;
                        Require(view.Apply(item),"Repeated equip failed.");
                        Require(actor.GetComponentsInChildren<MeshFilter>(true).Length==count,"Repeated equip duplicated cap geometry.");
                        Require(view.Apply(UniversalHeroCosmetic.Original),"Unequip failed.");
                        Require(view.Current==UniversalHeroCosmetic.Original,"Unequip did not clear the headwear slot.");
                        checkedHeroes++;
                    }
                    finally{if(actor!=null)UnityEngine.Object.DestroyImmediate(actor);}
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
            Debug.Log("DIMENSIONS FRONT: headwear attachment passed for "+checkedHeroes+" hat/hero combinations. Visual clipping and Android/multiplayer checks still required.");
        }
        static void Require(bool value,string message)
        {if(!value)throw new InvalidOperationException("BETA Cappy: "+message);}
    }
}
#endif


