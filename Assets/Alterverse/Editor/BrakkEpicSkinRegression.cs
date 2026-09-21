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
                Transform claws=Find(root,"DF_ReactorBreakerRedClaws");
                Require(claws!=null,"Red claw rack missing from Brakk epic shop skin.");
                MeshFilter filter=claws.GetComponent<MeshFilter>();
                Require(filter!=null&&filter.sharedMesh!=null&&filter.sharedMesh.vertexCount>0,"Red claw rack mesh is empty.");
                Require(claws.IsChildOf(Find(root,"DF_GauntletR")),"Red claws are not attached to the right gauntlet.");

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
