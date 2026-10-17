using System;
using UnityEngine;

namespace Alterverse.Visuals
{
    internal enum HeroVfxCue
    {
        Cast, Basic, Spell, Impact, Area, Dash, TargetWarning, Support, SupportLink,
        Debuff, Ultimate, CancelWarnings, CancelProjectiles, ShieldContact, PlayerHit
    }

    // Semantic presentation events, independent of meshes/materials and gameplay state.
    internal readonly struct HeroVfxEvent
    {
        public readonly HeroVfxCue Cue;
        public readonly GameObject Actor,Target;
        public readonly Vector3 Origin,Point;
        public readonly int Slot;
        public readonly float Duration,Radius;
        public readonly bool Flag,Secondary;
        public HeroVfxEvent(HeroVfxCue cue,GameObject actor,Vector3 point,int slot,float duration,
            float radius,GameObject target,bool flag,bool secondary,Vector3? origin)
        {
            Cue=cue;Actor=actor;Target=target;Point=point;Slot=slot;Duration=duration;
            Radius=radius;Flag=flag;Secondary=secondary;Origin=origin??(actor!=null?actor.transform.position:Vector3.zero);
        }
    }

    internal static class HeroVfxEvents
    {
        internal static event Action<HeroVfxEvent> Emitted;
        private static int depth,replaying;
        private static float replayElapsed;
        private static Vector3? replayOrigin;
        internal static bool InsideSemanticEffect=>depth>0;
        internal static float ReplayElapsed=>replaying>0?replayElapsed:0f;
        internal static Vector3? ReplayOrigin=>replaying>0?replayOrigin:null;

        internal readonly struct Scope:IDisposable
        {
            public void Dispose(){depth=Math.Max(0,depth-1);}
        }
        internal static Scope Begin(HeroVfxCue cue,GameObject actor,Vector3 point=default,int slot=-1,
            float duration=0,float radius=0,GameObject target=null,bool flag=false,bool secondary=false,Vector3? origin=null)
        {
            depth++;
            if(depth==1&&replaying==0&&actor!=null)
            {
                try {Emitted?.Invoke(new HeroVfxEvent(cue,actor,point,slot,duration,radius,target,flag,secondary,origin));}
                catch(Exception ex){Debug.LogWarning("DIMENSIONS FRONT VFX: event publication failed: "+ex.Message);}
            }
            return new Scope();
        }
        internal readonly struct ReplayScope:IDisposable
        {
            private readonly float elapsed;
            private readonly Vector3? origin;
            internal ReplayScope(float previousElapsed,Vector3? previousOrigin){elapsed=previousElapsed;origin=previousOrigin;}
            public void Dispose(){replaying=Math.Max(0,replaying-1);replayElapsed=elapsed;replayOrigin=origin;}
        }
        internal static ReplayScope Replay(float elapsed,Vector3 origin)
        {
            var scope=new ReplayScope(replayElapsed,replayOrigin);
            replaying++;replayElapsed=Mathf.Max(0,elapsed);replayOrigin=origin;
            return scope;
        }
    }
}
