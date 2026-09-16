using UnityEngine;

namespace Alterverse.Visuals
{
    public static class PrototypeVfx
    {
        public static void Burst(Vector3 position, Color color, float size = 1f, int pieces = 7)
        {
            for (int i = 0; i < pieces; i++)
            {
                float angle = 360f / pieces * i;
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                GameObject shard = Create(PrimitiveType.Cube, "AV_VFX_BurstShard", position + Vector3.up * .8f + direction * .2f, color);
                shard.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                shard.AddComponent<TransientScaleVfx>().Configure(new Vector3(.10f,.10f,.35f)*size,new Vector3(.02f,.02f,1.65f)*size,.24f,220f);
            }
        }

        public static void Ring(Vector3 position, Color color, float radius = 1.5f, float duration = .35f)
        {
            GameObject ring=Create(PrimitiveType.Cylinder,"AV_VFX_Ring",position+Vector3.up*.06f,color);
            ring.AddComponent<TransientScaleVfx>().Configure(new Vector3(.25f,.025f,.25f),new Vector3(radius,.015f,radius),duration,90f);
        }

        public static void Slash(Transform source, Color color, float length=1.7f)
        {
            GameObject slash=Create(PrimitiveType.Cube,"AV_VFX_Slash",source.position+Vector3.up*.75f+source.forward,color);
            slash.transform.rotation=source.rotation*Quaternion.Euler(0f,25f,-25f);
            slash.AddComponent<TransientScaleVfx>().Configure(new Vector3(.12f,.7f,length),new Vector3(.025f,.08f,length*1.2f),.18f,320f);
        }

        public static void ShadowTrail(Vector3 from, Vector3 to, Color color)
        {
            Vector3 delta=to-from; float distance=delta.magnitude;
            if(distance<.1f)return;
            GameObject trail=Create(PrimitiveType.Cube,"AV_VFX_ShadowTrail",(from+to)*.5f+Vector3.up*.45f,color);
            trail.transform.rotation=Quaternion.LookRotation(delta.normalized,Vector3.up);
            trail.AddComponent<TransientScaleVfx>().Configure(new Vector3(.65f,.08f,distance),new Vector3(.05f,.02f,distance*1.05f),.28f,0f);
        }

        public static void SoulLink(Vector3 from, Vector3 to, Color color)
        {
            Vector3 a=from+Vector3.up*.8f, b=to+Vector3.up*.8f, delta=b-a;
            GameObject link=Create(PrimitiveType.Cube,"AV_VFX_SoulLink",(a+b)*.5f,color);
            link.transform.rotation=Quaternion.LookRotation(delta.normalized,Vector3.up);
            link.AddComponent<TransientScaleVfx>().Configure(new Vector3(.08f,.08f,delta.magnitude),new Vector3(.015f,.015f,delta.magnitude),.32f,0f);
        }

        public static void FutureMark(Transform target, Color color, float duration)
        {
            GameObject mark=Create(PrimitiveType.Cylinder,"AV_VFX_FutureMark",target.position+Vector3.up*2.25f,color);
            mark.transform.SetParent(target,true); mark.transform.rotation=Quaternion.Euler(90f,0f,0f);
            mark.AddComponent<TransientScaleVfx>().Configure(new Vector3(.18f,.02f,.18f),new Vector3(1.15f,.02f,1.15f),duration,-240f);
            Ring(target.position,color,1.4f,.55f);
        }

        private static GameObject Create(PrimitiveType type,string name,Vector3 position,Color color)
        {
            GameObject target=GameObject.CreatePrimitive(type); target.name=name; target.transform.position=position;
            Collider collider=target.GetComponent<Collider>(); if(collider!=null)collider.enabled=false;
            Renderer renderer=target.GetComponent<Renderer>(); if(renderer!=null)renderer.material.color=color;
            return target;
        }
    }
}
