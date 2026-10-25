using System.Collections;
using Alterverse.Combat;
using Alterverse.Audio;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Characters
{
    public sealed partial class HeroAbilityExecution
    {
        private bool TryCastBolt7(int slot,AimPose aim)
        {
            if(!Bolt7Balance.HasAbility(slot))return false;
            HeroCombatAudio.Windup(gameObject,slot+1);
            if(slot==1)
            {
                // Cone capture may carry a target point off-centre. Preserve the shown
                // wedge direction instead of rotating the whole damage area toward it.
                Vector3 direction=AimGeometry.Direction(aim.Direction);
                locomotion.FaceAction(direction,Bolt7Balance.RamWindup+.25f);
                StartCoroutine(ResolveBolt7Ram(direction,life));return true;
            }
            AbilitySpec spec=kit.Spec(slot);
            Vector3 point=aim.HasAimPoint?aim.Point:transform.position+aim.Direction*spec.Range;
            point=AimGeometry.ClampPoint(transform.position,point,spec.Range);
            // Ground VFX do not inherit the height of the actor's capsule centre.
            point.y=0;
            HeroSkillVfx.Area(gameObject,point,spec.Radius,spec.Windup,true,false,slot);
            if(slot==0||slot==2)HeroSkillVfx.Spell(gameObject,point,slot,spec.Windup);
            StartCoroutine(ResolveBolt7Area(slot,point,life));return true;
        }
        private IEnumerator ResolveBolt7Area(int slot,Vector3 point,int token)
        {
            yield return new WaitForSeconds(kit.Spec(slot).Windup);
            if(token!=life||self.IsDead||!HeroCombatStatus.CanAct(gameObject))yield break;
            HeroCombatAudio.Cast(gameObject,slot);
            int hits=slot==3?Bolt7Combat.Nullfield(gameObject,point):slot==2?Bolt7Combat.Impulse(gameObject,point):Bolt7Combat.Grenade(gameObject,point);
            HeroSkillVfx.Area(gameObject,point,kit.Spec(slot).Radius,.3f,false,false,slot);
            if(hits>0)HeroCombatAudio.Impact(gameObject,slot,point);
        }
        private IEnumerator ResolveBolt7Ram(Vector3 direction,int token)
        {
            yield return new WaitForSeconds(Bolt7Balance.RamWindup);
            if(token!=life||self.IsDead||!HeroCombatStatus.CanAct(gameObject))yield break;
            HeroCombatAudio.Cast(gameObject,1);
            Vector3 from=transform.position;
            if(locomotion.BeginDash(from+direction*Bolt7Balance.RamDash,.15f))
                HeroSkillVfx.Dash(gameObject,from,locomotion.DashDestination,1,.15f);
            yield return new WaitForSeconds(.15f);
            if(token!=life||self.IsDead||!HeroCombatStatus.CanAct(gameObject))yield break;
            int hits=Bolt7Combat.Ram(gameObject,from,direction);
            HeroSkillVfx.Area(gameObject,from,Bolt7Balance.RamRange,.32f,false,false,1);
            if(hits>0)HeroCombatAudio.Impact(gameObject,1,transform.position);
        }
    }
}
