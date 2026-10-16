using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>Reconciles the acknowledged prediction, preserving movement made in flight.</summary>
    internal sealed class OwnerPoseHistory
    {
        internal const int Capacity=128;
        private struct Pose { public uint Sequence; public Vector3 Position; }
        private readonly List<Pose> pending=new(Capacity);
        private uint nextSequence,lastAcknowledged;
        public int Count=>pending.Count;

        public static bool Newer(uint sequence,uint previous)=>unchecked((int)(sequence-previous))>0;

        public uint Record(Vector3 position)
        {
            unchecked { nextSequence++; if(nextSequence==0)nextSequence++; }
            if(pending.Count==Capacity)pending.RemoveAt(0);
            pending.Add(new Pose{Sequence=nextSequence,Position=position});
            return nextSequence;
        }

        public bool TryReconcile(uint acknowledged,Vector3 authoritative,Vector3 current,out Vector3 corrected)
        {
            corrected=current;
            if(acknowledged==0||!Newer(acknowledged,lastAcknowledged)||!Finite(authoritative)||!Finite(current))return false;
            int index=pending.FindIndex(p=>p.Sequence==acknowledged);
            if(index<0)return false;
            Vector3 correction=authoritative-pending[index].Position;
            correction.y=0f;
            lastAcknowledged=acknowledged;
            pending.RemoveRange(0,index+1);
            // Later predictions share the same origin. Rebase them too, otherwise the next
            // acknowledgement would apply an already accepted correction a second time.
            for(int i=0;i<pending.Count;i++)
            {
                Pose pose=pending[i];pose.Position+=correction;pending[i]=pose;
            }
            corrected=current+correction;
            return true;
        }

        public void Clear(bool resetSequence=false)
        {
            pending.Clear();
            if(resetSequence)nextSequence=lastAcknowledged=0;
        }

        private static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&
            !float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);
    }
}
