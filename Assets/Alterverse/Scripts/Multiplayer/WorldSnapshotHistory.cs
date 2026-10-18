using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>Skip unchanged stationary state; periodic refreshes recover lost packets.</summary>
    public sealed class WorldSnapshotHistory
    {
        private struct State
        {
            public Vector3 Position;
            public float Yaw,Health,Shield,SentAt;
            public bool Dead;
        }
        private readonly Dictionary<string,State> sent=new();
        public bool ShouldSend(string key,Vector3 position,float yaw,float health,float shield,bool dead,bool moving,float now)
        {
            if(!moving&&sent.TryGetValue(key,out State old)&&now>=old.SentAt&&now-old.SentAt<1f&&
                position.x==old.Position.x&&position.y==old.Position.y&&position.z==old.Position.z&&
                yaw==old.Yaw&&health==old.Health&&shield==old.Shield&&dead==old.Dead)return false;
            sent[key]=new State{Position=position,Yaw=yaw,Health=health,Shield=shield,Dead=dead,SentAt=now};
            return true;
        }
        public void Remove(string key)=>sent.Remove(key);
        public void Clear()=>sent.Clear();
    }
}
