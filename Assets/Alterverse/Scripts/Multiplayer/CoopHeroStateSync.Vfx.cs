using System;
using Alterverse.Combat;
using Alterverse.Visuals;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    public sealed partial class CoopHeroStateSync
    {
        private const string HeroVfxMessage="DF_COOP_HERO_VFX_V1";

        private void OnHostHeroVfx(HeroVfxEvent cue)
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(audioRelay!=this||!CoopAuthority.Active||!CoopAuthority.LocalHasServerAuthority||manager==null||!manager.IsServer||messaging==null||manager.ConnectedClientsIds==null)return;
            CoopHeroSlot actor=cue.Actor!=null?cue.Actor.GetComponent<CoopHeroSlot>():null;
            if(actor==null||actor.Slot<0||actor.Slot>=MatchSession.MaxPlayers)return;
            Health target=cue.Target!=null?cue.Target.GetComponentInParent<Health>():null;
            CoopHeroSlot targetSlot=target!=null?target.GetComponent<CoopHeroSlot>():null;
            string worldKey="";
            if(target!=null&&targetSlot==null)
            {
                CoopWorldEntityKey key=target.GetComponent<CoopWorldEntityKey>()??target.gameObject.AddComponent<CoopWorldEntityKey>();
                key.Configure();worldKey=key.Key??"";
            }
            using FastBufferWriter writer=new(256,Allocator.Temp);
            writer.WriteValueSafe(CoopPlanHandshake.Fingerprint(CoopAuthority.ActivePlan));
            writer.WriteValueSafe(actor.Slot);writer.WriteValueSafe((int)cue.Cue);
            writer.WriteValueSafe(cue.Slot);writer.WriteValueSafe(targetSlot!=null?targetSlot.Slot:-1);
            writer.WriteValueSafe(new FixedString128Bytes(worldKey));
            writer.WriteValueSafe(cue.Origin.x);writer.WriteValueSafe(cue.Origin.y);writer.WriteValueSafe(cue.Origin.z);
            writer.WriteValueSafe(cue.Point.x);writer.WriteValueSafe(cue.Point.y);writer.WriteValueSafe(cue.Point.z);
            writer.WriteValueSafe(cue.Duration);writer.WriteValueSafe(cue.Radius);
            writer.WriteValueSafe(cue.Flag);writer.WriteValueSafe(cue.Secondary);
            writer.WriteValueSafe(manager.ServerTime.Time);
            foreach(ulong id in manager.ConnectedClientsIds)
                if(id!=NetworkManager.ServerClientId)messaging.SendNamedMessage(HeroVfxMessage,id,writer,NetworkDelivery.ReliableSequenced);
        }

        private void OnHeroVfx(ulong sender,FastBufferReader reader)
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority||manager==null||!manager.IsListening||sender!=NetworkManager.ServerClientId)return;
            if(!MatchNetworkProtocol.ReadRound(ref reader))return;
            reader.ReadValueSafe(out int actorSlot);reader.ReadValueSafe(out int rawCue);
            reader.ReadValueSafe(out int ability);reader.ReadValueSafe(out int targetSlot);
            reader.ReadValueSafe(out FixedString128Bytes worldKey);
            reader.ReadValueSafe(out float ox);reader.ReadValueSafe(out float oy);reader.ReadValueSafe(out float oz);
            reader.ReadValueSafe(out float x);reader.ReadValueSafe(out float y);reader.ReadValueSafe(out float z);
            reader.ReadValueSafe(out float duration);reader.ReadValueSafe(out float radius);
            reader.ReadValueSafe(out bool flag);reader.ReadValueSafe(out bool secondary);
            reader.ReadValueSafe(out double sent);
            if(actorSlot<0||actorSlot>=slots.Length||ability< -1||ability>3||targetSlot< -1||targetSlot>=slots.Length||
                !Enum.IsDefined(typeof(HeroVfxCue),rawCue)||!MatchNetworkProtocol.Finite(ox)||!MatchNetworkProtocol.Finite(oy)||
                !MatchNetworkProtocol.Finite(oz)||!MatchNetworkProtocol.Finite(x)||!MatchNetworkProtocol.Finite(y)||!MatchNetworkProtocol.Finite(z)||
                !MatchNetworkProtocol.Finite(duration)||duration<0||duration>30||!MatchNetworkProtocol.Finite(radius)||radius<0||radius>80||
                double.IsNaN(sent)||double.IsInfinity(sent))return;
            GameObject actor=slots[actorSlot]!=null?slots[actorSlot].gameObject:null;
            if(actor==null)return;
            HeroVfxCue cue=(HeroVfxCue)rawCue;
            float elapsed=Mathf.Max(0,(float)(manager.ServerTime.Time-sent));
            bool cancellation=cue==HeroVfxCue.CancelWarnings||cue==HeroVfxCue.CancelProjectiles||cue==HeroVfxCue.TimmyHold&&duration<=0;
            if(!cancellation&&elapsed>Mathf.Max(.5f,duration+.5f))return;
            Health target=null;
            string targetWorldKey=worldKey.ToString();
            if(targetSlot>=0)target=slots[targetSlot]!=null?slots[targetSlot].GetComponent<Health>():null;
            else if(targetWorldKey.Length>0)
                for(int i=0;i<Health.Active.Count;i++)
                {
                    Health hp=Health.Active[i];if(hp==null)continue;
                    if(hp.GetComponent<CoopWorldEntityKey>()?.Key==targetWorldKey){target=hp;break;}
                }
            Vector3 point=new(x,y,z),origin=new(ox,oy,oz);
            // Contact flashes align with receipt of authoritative health. Flights and warnings
            // retain their original clock; late packets cannot restart their full lifetime.
            bool flash=cue==HeroVfxCue.Impact||cue==HeroVfxCue.PlayerHit||cue==HeroVfxCue.ShieldContact;
            using var replay=HeroVfxEvents.Replay(flash?0:elapsed,origin);
            switch(cue)
            {
                case HeroVfxCue.Cast:
                    HeroSkillVfx.Cast(actor,ability,duration);
                    // The owner already predicted this action. Remote actors need the same
                    // body windup as offline; a muzzle flash alone does not animate their rig.
                    if(!slots[actorSlot].IsLocalHuman)
                    {
                        var visual=actor.GetComponent<Alterverse.Visuals.HeroVisualMotion>();
                        visual?.PulseAbility(ability,duration);visual?.SeekAction(elapsed);
                    }
                    break;
                case HeroVfxCue.BasicWindup:
                    // Remote rigs retain snapshot presentation; only the owner waits for acceptance.
                    if(slots[actorSlot].IsLocalHuman)
                        actor.GetComponent<BasicAttack>()?.ApplyNetworkWindup(point,target,duration,elapsed);
                    break;
                case HeroVfxCue.Basic:HeroSkillVfx.Basic(actor,point,duration,target!=null?target.transform:null,flag);break;
                case HeroVfxCue.Spell:HeroSkillVfx.Spell(actor,point,ability,duration,target!=null?target.transform:null);break;
                case HeroVfxCue.Impact:if(target!=null)HeroSkillVfx.Impact(actor,target,ability);break;
                case HeroVfxCue.Area:HeroSkillVfx.Area(actor,point,radius,duration,flag,secondary,ability);break;
                case HeroVfxCue.Dash:HeroSkillVfx.Dash(actor,origin,point,ability,duration);break;
                case HeroVfxCue.TargetWarning:if(target!=null)HeroSkillVfx.TargetWarning(actor,target,duration);break;
                case HeroVfxCue.Support:if(target!=null)HeroSkillVfx.Support(actor,target,flag,duration);break;
                case HeroVfxCue.SupportLink:if(target!=null)HeroSkillVfx.SupportLink(actor,target);break;
                case HeroVfxCue.TimmyHold:if(target!=null)HeroSkillVfx.TimmyHold(actor,target,duration);break;
                case HeroVfxCue.Debuff:if(target!=null)HeroSkillVfx.Debuff(actor,target,ability);break;
                case HeroVfxCue.Ultimate:HeroSkillVfx.Ultimate(actor,point);break;
                case HeroVfxCue.CancelWarnings:HeroSkillVfx.CancelWarnings(actor);break;
                case HeroVfxCue.CancelProjectiles:HeroSkillVfx.CancelProjectiles(actor);break;
                case HeroVfxCue.ShieldContact:HeroSkillVfx.ShieldContact(actor,target!=null?target.gameObject:null);break;
                case HeroVfxCue.PlayerHit:if(target!=null)HeroSkillVfx.PlayerHit(actor,target.gameObject);break;
            }
        }
    }
}


