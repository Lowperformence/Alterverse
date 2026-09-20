using Alterverse.Characters;
using UnityEngine;
namespace Alterverse.Audio
{
    // 6 heroes x (Basic,Q,W,E,R) x (Cast,Impact). No runtime synthesis or generic fallback.
    public static class HeroCombatAudio
    {
        public const int CueCount=60;
        static readonly string[] Heroes={"Veyr","Kaelon","Nyra","Elyra","Mira","Rook"};
        static readonly string[] Actions={"Basic","Q","W","E","R"};
        public static bool HasImpact(int hero,int action)=>action==0||(hero switch
        {
            0=>action!=3,1=>action!=2,2=>action!=2,3=>action!=3,4=>action==1||action==4,5=>action!=2,_=>false
        });
        public static AudioClip[] Load()
        {
            var clips=new AudioClip[CueCount];
            for(int h=0;h<6;h++)for(int a=0;a<5;a++)for(int phase=0;phase<2;phase++)
            {
                if(phase==1&&!HasImpact(h,a))continue;
                string path="Audio/Heroes/"+Heroes[h]+"/"+Actions[a]+(phase==0?"_Cast":"_Impact");
                clips[h*10+a*2+phase]=Resources.Load<AudioClip>(path);
                if(clips[h*10+a*2+phase]==null)Debug.LogWarning("DIMENSIONS FRONT: FINAL PRODUCTION SFX REQUIRED: "+path);
            }
            return clips;
        }
        public static void Play(GameObject actor,int action,bool impact,Vector3 position)
        {
            if(actor==null||action<0||action>4)return;
            var identity=actor.GetComponent<HeroIdentity>();if(identity==null)return;
            Camera camera=Camera.main;if(camera==null)return;Vector3 p=camera.WorldToViewportPoint(position);
            if(p.z<=0||p.x<-.15f||p.x>1.15f||p.y<-.15f||p.y>1.15f)return;
            int hero=(int)identity.Hero;if(hero<0||hero>=6||impact&&!HasImpact(hero,action))return;
            bool ultimate=action==4;
            float gain=action==0?(impact?.24f:.34f):ultimate?(impact?.62f:.74f):(impact?.32f:.44f);
            // Mix around the ground point at screen center, not camera height above the map.
            Ray ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            Plane ground=new Plane(Vector3.up,Vector3.zero);
            float attenuation=1;
            if(ground.Raycast(ray,out float distance))
            {
                Vector3 delta=position-ray.GetPoint(distance);delta.y=0;
                attenuation=Mathf.Lerp(1,ultimate?.48f:.20f,Mathf.InverseLerp(3,ultimate?30:22,delta.magnitude));
            }
            float pan=Mathf.Clamp((p.x-.5f)*1.1f,-.65f,.65f);
            // Four real volley releases .18s apart must not be swallowed by the generic .3s limiter.
            float cooldown=ultimate&&identity.Hero==HeroId.Nyra?.08f:ultimate?(impact?.22f:.3f):(impact?.09f:.12f);
            DimensionAudio.PlayHero(hero*10+action*2+(impact?1:0),gain*attenuation,ultimate?2:action==0?0:1,cooldown,pan);
        }
        public static void Cast(GameObject actor,int slot)=>Play(actor,slot+1,false,actor.transform.position);
        public static void Impact(GameObject actor,int slot,Vector3 point)=>Play(actor,slot+1,true,point);
    }
}

