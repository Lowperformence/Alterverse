#if UNITY_EDITOR
using System;
using Alterverse.Characters;
using Alterverse.Shop;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class MageVisualRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Mage visual and epic staff regression")]
        public static void Run()
        {
            WeaponSkinItem item=WeaponSkinCatalog.For(HeroId.Elyra);
            Require(item.Name=="ASTRAL ORBIT // EPIC","Old mage weapon skin is still active.");
            Require(item.WeaponName=="Astral Crystal Staff","Mage shop weapon name is wrong.");
            Require(item.Glow.maxColorComponent>.9f,"Mage epic staff glow is too dim.");

            GameObject normal=null,premium=null;
            try
            {
                normal=Build(HeroId.Elyra,false);
                CheckRig(normal,false);

                premium=Build(HeroId.Elyra,true);
                CheckRig(premium,true);

                Debug.Log("DIMENSIONS FRONT: mage visual and ASTRAL ORBIT epic staff regression PASSED.");
            }
            finally
            {
                if(normal!=null)UnityEngine.Object.DestroyImmediate(normal);
                if(premium!=null)UnityEngine.Object.DestroyImmediate(premium);
            }
        }

        private static GameObject Build(HeroId hero,bool premium)
        {
            var go=new GameObject(premium?"DF_MageEpicCheck":"DF_MageBaseCheck");
            go.AddComponent<HeroIdentity>().Configure(hero);
            if(premium)
            {
                var preview=go.AddComponent<SkinPreviewSelection>();
                preview.WeaponOverdrive=true;
            }
            go.AddComponent<StylizedHeroVisual>().Build();
            return go;
        }

        private static void CheckRig(GameObject hero,bool premium)
        {
            Transform root=hero.transform.Find("DF_VisualRoot");
            Require(root!=null,"Mage visual root missing.");
            Require(root.GetComponentsInChildren<Collider>(true).Length==0,"Mage visual contains a gameplay collider.");

            Transform weapon=root.Find("DF_ArmR/DF_WeaponR");
            Require(weapon!=null,"Mage staff is not attached to the right arm.");
            Require(Find(root,"DF_Orb")!=null,"Mage crystal / release point is missing.");
            Require(Find(root,"DF_MageFaceMask")!=null,"Void Mage face mask is missing.");
            Require(Find(root,"DF_MageEyeSlit")!=null,"Void Mage eye glow is missing.");
            Require(Find(root,"DF_MageCrownCenter")!=null,"Void Mage crown silhouette is missing.");
            Require(Find(root,"DF_ArcaneHandCrystal")!=null,"Void Mage offhand crystal is missing.");

            if(premium)
            {
                Require(Find(root,"DF_AstralHaloA")!=null,"Epic staff outer halo is missing.");
                Require(Find(root,"DF_AstralHaloB")!=null,"Epic staff inner halo is missing.");
                Require(Find(root,"DF_AstralShardL")!=null&&Find(root,"DF_AstralShardR")!=null,
                    "Epic staff suspended crystal shards are missing.");
            }

            int renderers=root.GetComponentsInChildren<MeshRenderer>(true).Length;
            Require(renderers>0&&renderers<=24,"Mage renderer budget exceeded: "+renderers);
        }

        private static Transform Find(Transform root,string name)
        {
            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                if(t.name==name)return t;
            return null;
        }

        private static void Require(bool ok,string message)
        {
            if(!ok)throw new InvalidOperationException("Mage visual regression: "+message);
        }
    }
}
#endif
