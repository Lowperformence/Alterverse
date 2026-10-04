using System.Reflection;
using Alterverse.Characters;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Keeps Veyr's two swords in the requested crossed X guard without interfering with
    /// the active strike path. HeroVisualMotion still owns attacks; this only replaces its
    /// previous split idle/free-hand guard after that pose has been written for the frame.
    /// </summary>
    [DefaultExecutionOrder(950)]
    public sealed class VeyrCrossedBladeGuard : MonoBehaviour
    {
        private static readonly BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        private static readonly Quaternion OldGuardL=Quaternion.Euler(16f,-24f,-14f);
        private static readonly Quaternion OldGuardR=Quaternion.Euler(-18f,22f,14f);
        private static readonly Quaternion CrossedGuardL=Quaternion.Euler(4f,0f,-50f);
        private static readonly Quaternion CrossedGuardR=Quaternion.Euler(4f,0f,50f);

        private HeroVisualMotion motion;
        private HeroIdentity identity;
        private Transform weaponL,weaponR;
        private Quaternion restL=Quaternion.identity,restR=Quaternion.identity;
        private bool ready;

        private void Start()
        {
            identity=GetComponent<HeroIdentity>();
            motion=GetComponent<HeroVisualMotion>();
            if(identity==null||identity.Hero!=HeroId.Veyr||motion==null){enabled=false;return;}

            weaponL=FindVisual("WeaponL")??FindVisual("BladeL");
            weaponR=FindVisual("WeaponR")??FindVisual("BladeR");
            FieldInfo leftField=typeof(HeroVisualMotion).GetField("weaponRestL",PrivateInstance);
            FieldInfo rightField=typeof(HeroVisualMotion).GetField("weaponRestR",PrivateInstance);
            if(leftField==null||rightField==null){enabled=false;return;}
            restL=(Quaternion)leftField.GetValue(motion);
            restR=(Quaternion)rightField.GetValue(motion);
            ready=weaponL!=null||weaponR!=null;
        }

        private Transform FindVisual(string suffix)
        {
            Transform root=transform.Find("DF_VisualRoot");
            if(root==null)return null;
            string wanted="DF_"+suffix;
            foreach(Transform child in root.GetComponentsInChildren<Transform>(true))
                if(child.name==wanted)return child;
            return null;
        }

        private void LateUpdate()
        {
            if(!ready || GetComponent<MeleeBasicAttackAnimation>()?.OwnsPose == true)return;
            // HeroVisualMotion writes the old guard every idle frame and also to the free hand
            // during a one-handed slash. Replace only that exact guard; never overwrite a strike.
            if(weaponL!=null)
            {
                Quaternion old=restL*OldGuardL;
                if(Quaternion.Angle(weaponL.localRotation,old)<3f)
                    weaponL.localRotation=restL*CrossedGuardL;
            }
            if(weaponR!=null)
            {
                Quaternion old=restR*OldGuardR;
                if(Quaternion.Angle(weaponR.localRotation,old)<3f)
                    weaponR.localRotation=restR*CrossedGuardR;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallBootstrap()
        {
            var go=new GameObject("DF_VeyrCrossedGuardBootstrap");
            DontDestroyOnLoad(go);
            go.hideFlags=HideFlags.HideAndDontSave;
            go.AddComponent<Bootstrap>();
        }

        private sealed class Bootstrap : MonoBehaviour
        {
            private float nextScan;
            private void OnEnable()=>SceneManager.sceneLoaded+=OnSceneLoaded;
            private void OnDisable()=>SceneManager.sceneLoaded-=OnSceneLoaded;
            private void OnSceneLoaded(Scene scene,LoadSceneMode mode){nextScan=0f;Scan();}
            private void Update()
            {
                if(Time.unscaledTime<nextScan)return;
                nextScan=Time.unscaledTime+1f;
                Scan();
            }
            private static void Scan()
            {
                HeroIdentity[] heroes=Object.FindObjectsByType<HeroIdentity>(FindObjectsSortMode.None);
                foreach(HeroIdentity hero in heroes)
                {
                    if(hero==null||hero.Hero!=HeroId.Veyr)continue;
                    if(hero.GetComponent<VeyrCrossedBladeGuard>()==null)
                        hero.gameObject.AddComponent<VeyrCrossedBladeGuard>();
                }
            }
        }
    }
}

