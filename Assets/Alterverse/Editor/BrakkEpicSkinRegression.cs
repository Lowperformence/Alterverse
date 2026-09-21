#if UNITY_EDITOR
using System;
using Alterverse.Characters;
using Alterverse.Shop;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class BrakkEpicSkinRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Brakk epic red claw skin regression")]
        public static void Run()
        {
            WeaponSkinItem item=WeaponSkinCatalog.For(HeroId.Rook);
            Require(item.Name=="REACTOR BREAKER // EPIC","Unexpected Brakk epic weapon skin.");
            Require(item.Glow.r>.95f&&item.Glow.g<.10f,"Brakk epic shop accent is not red.");

            GameObject hero=null;
            try
            {
                hero=new GameObject("DF_BrakkEpicSkinCheck");
                hero.AddComponent<HeroIdentity>().Configure(HeroId.Rook);
                var preview=hero.AddComponent<SkinPreviewSelection>();
                preview.WeaponOverdrive=true;
                hero.AddComponent<StylizedHeroVisual>().Build();

                Transform root=hero.transform.Find("DF_VisualRoot");
                Require(root!=null,"Visual root missing.");
                Transform clawsL=Find(root,"DF_ReactorBreakerRedClawsL");
                Transform clawsR=Find(root,"DF_ReactorBreakerRedClawsR");
                Require(clawsL!=null,"Left red claw rack missing from Brakk epic shop skin.");
                Require(clawsR!=null,"Right red claw rack missing from Brakk epic shop skin.");

                MeshFilter filterL=clawsL.GetComponent<MeshFilter>();
                MeshFilter filterR=clawsR.GetComponent<MeshFilter>();
                Require(filterL!=null&&filterL.sharedMesh!=null&&filterL.sharedMesh.vertexCount>0,
                    "Left red claw rack mesh is empty.");
                Require(filterR!=null&&filterR.sharedMesh!=null&&filterR.sharedMesh.vertexCount>0,
                    "Right red claw rack mesh is empty.");

                Require(clawsL.IsChildOf(Find(root,"DF_GauntletL")),
                    "Left red claws are not attached to the left gauntlet.");
                Require(clawsR.IsChildOf(Find(root,"DF_GauntletR")),
                    "Right red claws are not attached to the right gauntlet.");

                Debug.Log("DIMENSIONS FRONT: Brakk epic red claw skin regression PASSED.");
            }
            finally
            {
                if(hero!=null)UnityEngine.Object.DestroyImmediate(hero);
            }
        }

        private static Transform Find(Transform root,string name)
        {
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                if(t.name==name)return t;
            return null;
        }

        private static void Require(bool ok,string message)
        {
            if(!ok)throw new InvalidOperationException("Brakk epic skin regression: "+message);
        }
    }
}
#endif
