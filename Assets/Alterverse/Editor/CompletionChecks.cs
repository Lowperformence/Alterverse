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
            foreach(string name in new[]{"Menu","Match","Pressure","Climax","Victory","Defeat"})
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
                GameObject actor=new GameObject("DF_SkinRegression");actor.transform.position=new Vector3(14000,14000,14000);
                try
                {
                    actor.AddComponent<HeroIdentity>().Configure((HeroId)i);
                    actor.AddComponent<SkinPreviewSelection>().Voidfall=true;
                    actor.AddComponent<StylizedHeroVisual>().Build();
                    var renderers=actor.GetComponentsInChildren<Renderer>();bool found=false;
                    foreach(var renderer in renderers)if(renderer.sharedMaterial!=null&&renderer.sharedMaterial.name=="DF_VoidfallFinish")found=true;
                    if(!found)throw new System.InvalidOperationException("VOIDFALL finish missing on hero "+i);
                    if(actor.GetComponent<Alterverse.Combat.Health>()!=null||actor.GetComponentsInChildren<Collider>().Length>0)
                        throw new System.InvalidOperationException("Cosmetic preview unexpectedly contains gameplay components");
                }
                finally{actor.SetActive(false);Object.Destroy(actor);}
            }
            Debug.Log("DIMENSIONS FRONT: all six skin preview construction checks passed. Purchase/restart and visual approval still required.");
        }
    }
}
#endif
