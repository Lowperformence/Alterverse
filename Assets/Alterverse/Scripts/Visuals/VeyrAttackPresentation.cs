using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Presentation-only polish for Zhaev Null's twin-blade attacks.
    /// HeroVisualMotion still owns attack timing; this runs afterwards and reshapes only the visible pose.
    /// Damage, attack range, hit timing and gameplay collision are untouched.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class VeyrAttackPresentation : MonoBehaviour
    {
        private HeroIdentity identity;
        private Transform root,armL,armR,weaponL,weaponR;
        private Quaternion armRestL,armRestR,weaponRestL,weaponRestR;
        private bool initialized;

        private void Awake()=>identity=GetComponent<HeroIdentity>();

        private void LateUpdate()
        {
            if(identity==null)identity=GetComponent<HeroIdentity>();
            if(identity==null||identity.Hero!=HeroId.Veyr)return;
            if(!initialized&&!TryInitialize())return;

            PolishSide(armR,armRestR,weaponR,weaponRestR,1f);
            PolishSide(armL,armRestL,weaponL,weaponRestL,-1f);
        }

        private bool TryInitialize()
        {
            root=transform.Find("DF_VisualRoot");
            if(root==null)return false;

            armL=FindPart("ArmL");armR=FindPart("ArmR");
            weaponL=FindPart("WeaponL");weaponR=FindPart("WeaponR");
            if(armL==null||armR==null||weaponL==null||weaponR==null)return false;

            armRestL=armL.localRotation;armRestR=armR.localRotation;
            weaponRestL=weaponL.localRotation;weaponRestR=weaponR.localRotation;
            initialized=true;
            return true;
        }

        private Transform FindPart(string shortName)
        {
            if(root==null)return null;
            string target="DF_"+shortName;
            Transform[] all=root.GetComponentsInChildren<Transform>(true);
            for(int i=0;i<all.Length;i++)if(all[i]!=null&&all[i].name==target)return all[i];
            return null;
        }

        private static void PolishSide(Transform arm,Quaternion armRest,Transform weapon,Quaternion weaponRest,float side)
        {
            if(arm==null||weapon==null)return;

            // HeroVisualMotion leaves the weapon exactly at rest outside an attack, which gives us
            // a reliable presentation-only attack signal without touching combat state.
            float weaponAngle=Quaternion.Angle(weaponRest,weapon.localRotation);
            float attack=Mathf.InverseLerp(4f,24f,weaponAngle);
            if(attack<=.001f)return;

            // Open the shoulder arc substantially so the blades travel around the torso instead of
            // making a small chop close to the legs. Keep pitch authored by HeroVisualMotion.
            Quaternion armDelta=Quaternion.Inverse(armRest)*arm.localRotation;
            Vector3 armEuler=SignedEuler(armDelta.eulerAngles);
            armEuler.y*=1.38f;
            armEuler.z*=1.24f;
            arm.localRotation=armRest*Quaternion.Euler(armEuler);

            // The old blade pitch pointed the long sword geometry down toward the thighs. During
            // the visible strike, bias the blade upward into a chest-height cutting plane and widen
            // its lateral sweep. The hilt stays on the animated hand because position is untouched.
            Quaternion weaponDelta=Quaternion.Inverse(weaponRest)*weapon.localRotation;
            Vector3 weaponEuler=SignedEuler(weaponDelta.eulerAngles);
            weaponEuler.x=Mathf.Lerp(weaponEuler.x,-38f,attack);
            weaponEuler.y*=Mathf.Lerp(1f,1.55f,attack);
            weaponEuler.z*=Mathf.Lerp(1f,1.30f,attack);

            // A small outward cant makes left/right cuts read as two broad opposing arcs rather than
            // overlapping in the middle of the character.
            weaponEuler.z+=side*8f*attack;
            weapon.localRotation=weaponRest*Quaternion.Euler(weaponEuler);
        }

        private static Vector3 SignedEuler(Vector3 euler)
        {
            return new Vector3(Signed(euler.x),Signed(euler.y),Signed(euler.z));
        }

        private static float Signed(float angle)=>angle>180f?angle-360f:angle;
    }
}
