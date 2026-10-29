using System;
using System.Collections.Generic;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>Feet-only measurements and a flat stand pose for an isolated menu actor.</summary>
    internal sealed class MenuHeroGrounding
    {
        sealed class Foot
        {
            public Transform Bone, Knee, Hip;
            public Vector3[] Points;
            public Vector3 Center;
            public Vector2 Size;
            public float Reach;
        }
        readonly GameObject actor;
        Foot left, right;
        bool attempted;
        HeroGroundedLocomotion arenaGrounding;
        public float Radius {get;private set;}=.88f;
        public bool Ready=>left?.Points?.Length>0&&right?.Points?.Length>0;
        public Vector3 LeftCenter=>left.Bone.TransformPoint(left.Center);
        public Vector3 RightCenter=>right.Bone.TransformPoint(right.Center);
        public Vector2 LeftSize=>left.Size;
        public Vector2 RightSize=>right.Size;

        public MenuHeroGrounding(GameObject actor){this.actor=actor;}

        // Update runs before all LateUpdate pose writers. The preview owns a flat deck,
        // so arena raycasts/contact history must not compete with this menu-only solve.
        public void Prepare()
        {
            if(arenaGrounding==null)arenaGrounding=actor.GetComponent<HeroGroundedLocomotion>();
            if(arenaGrounding!=null&&arenaGrounding.enabled)arenaGrounding.enabled=false;
        }

        public bool PlaceFeet(Transform stage,float surfaceY)
        {
            if(!attempted)
            {
                attempted=true;
                Transform[] parts=actor.GetComponentsInChildren<Transform>(true);
                left=Capture(parts,false);right=Capture(parts,true);
                if(!Ready)
                {
                    Debug.LogWarning("DF: menu pedestal could not measure both visible feet on "+actor.name,actor);
                    return false;
                }
                Radius=Mathf.Clamp(Mathf.Max(.88f,Mathf.Max(FootRadius(left),FootRadius(right))+.18f),.88f,1.5f);
            }
            if(!Ready)return false;
            float floor=stage.TransformPoint(new Vector3(0,surfaceY,0)).y;
            // Lower the body to its higher sole, then bend the other leg upward.
            // This avoids stretching a straight leg to force a lower ankle to the floor.
            float highest=Mathf.Max(Lowest(left),Lowest(right));
            if(!Finite(highest)||Mathf.Abs(highest-actor.transform.position.y)>8f)return false;
            actor.transform.position+=Vector3.up*(floor-highest);
            Plant(left,floor);Plant(right,floor);
            return true;
        }

        void Plant(Foot foot,float floor)
        {
            for(int pass=0;pass<3;pass++)
            {
                float delta=floor-Lowest(foot);
                if(Mathf.Abs(delta)<.0011f)return; // one float ULP at the remote portrait stage
                Vector3 target=foot.Bone.position+Vector3.up*delta;
                if(foot.Hip!=null&&foot.Knee!=null&&foot.Reach>.05f)
                {
                    Vector3 hint=foot.Knee.position+actor.transform.forward*foot.Reach*.25f;
                    CombatMotion.Solve(foot.Hip,foot.Knee,foot.Bone,target,hint,.99999f);
                }
                else foot.Bone.position=target; // rigid procedural boot fallback
            }
        }

        static float Lowest(Foot foot)
        {
            float y=float.PositiveInfinity;
            Matrix4x4 matrix=foot.Bone.localToWorldMatrix;
            foreach(Vector3 point in foot.Points)y=Mathf.Min(y,matrix.MultiplyPoint3x4(point).y);
            return y;
        }

        float FootRadius(Foot foot)
        {
            float radius=0;
            foreach(Vector3 p in foot.Points)
            {
                Vector3 local=actor.transform.InverseTransformPoint(foot.Bone.TransformPoint(p));
                radius=Mathf.Max(radius,new Vector2(local.x,local.z).magnitude);
            }
            return radius;
        }

        static string Canonical(string name)=>name.ToLowerInvariant().Replace("_","").Replace(".","").Replace("mixamorig:","");
        static Transform FindFoot(Transform[] parts,bool right)
        {
            string side=right?"r":"l",word=right?"right":"left";
            foreach(Transform part in parts)
            {
                string n=Canonical(part.name);
                if(n=="foot"+side||n==word+"foot"||n=="foot"+word)return part;
            }
            foreach(Transform part in parts)if(Canonical(part.name)=="dfanklejoint"+side)return part;
            foreach(Transform part in parts)if(Canonical(part.name)=="dfboot"+side)return part;
            return null;
        }

        Foot Capture(Transform[] parts,bool isRight)
        {
            Transform bone=FindFoot(parts,isRight);
            if(bone==null)return null;
            var foot=new Foot{Bone=bone,Knee=bone.parent,Hip=bone.parent?.parent};
            if(foot.Hip!=null&&foot.Knee!=null)
                foot.Reach=Vector3.Distance(foot.Hip.position,foot.Knee.position)+Vector3.Distance(foot.Knee.position,bone.position);
            var points=new List<Vector3>();
            foreach(SkinnedMeshRenderer skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh=skin.sharedMesh;
                if(!skin.enabled||!skin.gameObject.activeInHierarchy||mesh==null||!mesh.isReadable)continue;
                Transform[] bones=skin.bones;
                int footIndex=Array.IndexOf(bones,bone);
                if(footIndex<0)continue;
                Vector3[] vertices=mesh.vertices;BoneWeight[] weights=mesh.boneWeights;Matrix4x4[] bind=mesh.bindposes;
                var visible=new bool[vertices.Length];
                foreach(int index in mesh.triangles)if(index>=0&&index<visible.Length)visible[index]=true;
                for(int i=0;i<vertices.Length&&i<weights.Length;i++)
                {
                    if(!visible[i]||!FootWeighted(weights[i],footIndex))continue;
                    AddPoint(foot,SkinVertex(bones,bind,weights[i],vertices[i]),points);
                }
            }
            foreach(MeshFilter filter in actor.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh=filter.sharedMesh;var renderer=filter.GetComponent<Renderer>();
                if(mesh==null||renderer==null||!renderer.enabled||!filter.gameObject.activeInHierarchy||!Under(filter.transform,bone))continue;
                if(mesh.isReadable)
                {
                    Vector3[] vertices=mesh.vertices;
                    foreach(int index in mesh.triangles)if(index>=0&&index<vertices.Length)
                        AddPoint(foot,filter.transform.TransformPoint(vertices[index]),points);
                }
            }
            if(points.Count==0)return foot;
            // Cache the lower envelope in a fixed 8x8 footprint grid, in the actor's
            // upright capture frame. Feet may have arbitrary imported bone axes.
            Vector3 min=new(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity),max=-min;
            foreach(Vector3 p in points)
            {
                Vector3 local=actor.transform.InverseTransformPoint(bone.TransformPoint(p));
                min=Vector3.Min(min,local);max=Vector3.Max(max,local);
            }
            var cells=new Vector3[64];var heights=new float[64];var used=new bool[64];
            foreach(Vector3 p in points)
            {
                Vector3 local=actor.transform.InverseTransformPoint(bone.TransformPoint(p));
                int x=Mathf.Clamp((int)((local.x-min.x)/Mathf.Max(.001f,max.x-min.x)*7.999f),0,7);
                int z=Mathf.Clamp((int)((local.z-min.z)/Mathf.Max(.001f,max.z-min.z)*7.999f),0,7),index=z*8+x;
                if(!used[index]||local.y<heights[index]){cells[index]=p;heights[index]=local.y;used[index]=true;}
            }
            var support=new List<Vector3>(64);
            for(int i=0;i<64;i++)if(used[i])support.Add(cells[i]);
            foot.Points=support.ToArray();
            foot.Center=bone.InverseTransformPoint(actor.transform.TransformPoint(new Vector3((min.x+max.x)*.5f,min.y,(min.z+max.z)*.5f)));
            foot.Size=new Vector2(max.x-min.x,max.z-min.z);
            return foot;
        }

        static bool Under(Transform part,Transform parent)
        {for(Transform t=part;t!=null;t=t.parent)if(t==parent)return true;return false;}
        static void AddPoint(Foot foot,Vector3 world,List<Vector3> points)
        {
            Vector3 offset=world-foot.Bone.position;
            float limit=Mathf.Max(.40f,foot.Reach*.55f);
            if(!Finite(offset.x)||!Finite(offset.y)||!Finite(offset.z)||offset.sqrMagnitude>limit*limit)return;
            points.Add(foot.Bone.InverseTransformPoint(world));
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool FootWeighted(BoneWeight w,int index)=>(w.boneIndex0==index&&w.weight0>.35f)||
            (w.boneIndex1==index&&w.weight1>.35f)||(w.boneIndex2==index&&w.weight2>.35f)||(w.boneIndex3==index&&w.weight3>.35f);
        static Vector3 SkinVertex(Transform[] bones,Matrix4x4[] bind,BoneWeight w,Vector3 point)
        {
            Vector3 result=Vector3.zero;float total=0;
            for(int i=0;i<4;i++)
            {
                int index=i==0?w.boneIndex0:i==1?w.boneIndex1:i==2?w.boneIndex2:w.boneIndex3;
                float weight=i==0?w.weight0:i==1?w.weight1:i==2?w.weight2:w.weight3;
                if(weight<=0||!Finite(weight)||index<0||index>=bones.Length||index>=bind.Length||bones[index]==null)continue;
                result+=bones[index].localToWorldMatrix.MultiplyPoint3x4(bind[index].MultiplyPoint3x4(point))*weight;total+=weight;
            }
            return total>.00001f?result/total:new Vector3(float.NaN,float.NaN,float.NaN);
        }
    }
}
