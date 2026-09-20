#if UNITY_EDITOR
using Alterverse.Characters;
using Alterverse.Shop;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;
namespace Alterverse.EditorChecks
{
    public static class CompletionChecks
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Production audio assets")]
        public static void AudioAssets()
        {
            int missing=0;
            foreach(string name in new[]{"Menu","Match"})
                if(Resources.Load<AudioClip>("Audio/Music/"+name)==null){Debug.LogWarning("FINAL MUSIC ASSET REQUIRED: "+name);missing++;}
            foreach(Alterverse.Audio.DimensionSound sound in System.Enum.GetValues(typeof(Alterverse.Audio.DimensionSound)))
                if(Resources.Load<AudioClip>("Audio/SFX/"+sound)==null){Debug.LogWarning("FINAL SFX ASSET REQUIRED: "+sound);missing++;}
            Debug.Log("DIMENSIONS FRONT audio asset audit: "+missing+" missing clips. Listening/device verification is separate.");
        }
        [MenuItem("DIMENSIONS FRONT/Checks/Six hero skin previews (Play mode)")]
        public static void SkinPreviews()
        {
            if(!Application.isPlaying){Debug.LogWarning("Enter Play mode to check skin meshes.");return;}
            for(int i=0;i<6;i++)
            {
                CheckSkinPreview((HeroId)i,UniversalHeroCosmetic.Voidfall);
                CheckSkinPreview((HeroId)i,UniversalHeroCosmetic.BetaSupporterWings);
            }
            Debug.Log("DIMENSIONS FRONT: VOIDFALL and BETA SUPPORTER WINGS preview construction passed for all six heroes. Purchase/restart and visual approval still require device testing.");
        }

        private static void CheckSkinPreview(HeroId hero,UniversalHeroCosmetic cosmetic)
        {
            GameObject actor=new GameObject("DF_SkinRegression_"+cosmetic);actor.transform.position=new Vector3(14000,14000,14000);
            try
            {
                actor.AddComponent<HeroIdentity>().Configure(hero);
                actor.AddComponent<SkinPreviewSelection>().UniversalCosmetic=cosmetic;
                actor.AddComponent<StylizedHeroVisual>().Build();
                bool found=false;
                if(cosmetic==UniversalHeroCosmetic.Voidfall)
                {
                    foreach(var renderer in actor.GetComponentsInChildren<Renderer>())
                        if(renderer.sharedMaterial!=null&&renderer.sharedMaterial.name=="DF_VoidfallFinish")found=true;
                }
                else
                {
                    Transform visual=actor.transform.Find("DF_VisualRoot");
                    found=visual!=null&&visual.Find("DF_BetaSupporterWingLeft")!=null&&visual.Find("DF_BetaSupporterWingRight")!=null;
                    foreach(var renderer in actor.GetComponentsInChildren<Renderer>())
                        if(renderer.sharedMaterial!=null&&renderer.sharedMaterial.name=="DF_BetaSupporterWingCore")found=true;
                }
                if(!found)throw new System.InvalidOperationException(cosmetic+" preview missing on hero "+hero);
                if(actor.GetComponent<Alterverse.Combat.Health>()!=null||actor.GetComponentsInChildren<Collider>().Length>0)
                    throw new System.InvalidOperationException(cosmetic+" preview unexpectedly contains gameplay components");
            }
            finally{actor.SetActive(false);Object.Destroy(actor);}
        }
    }
}
#endif
