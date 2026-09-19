using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;
namespace Alterverse.Visuals
{
    // Thin opaque line geometry; one shared material, no transparent screens or burst particle clouds.
    public sealed class HeroSkillVfx : MonoBehaviour
    {
        private static Material ink;
        const int MaxStrokes=128,ReservedWarnings=32;
        static readonly System.Collections.Generic.List<HeroSkillVfx> pool=new();
        private LineRenderer stroke;
        private float until,began,baseWidth;
        private Health ownerHealth,shieldHealth;
        private Transform follow;
        private Vector3 followPosition;
        private bool follows,shieldBound;
        private readonly Vector3[] trackedPoints=new Vector3[48];
        public static void CancelWarnings(GameObject actor)
        {foreach(var fx in pool)if(fx!=null&&fx.gameObject.activeSelf&&fx.cancelWithOwner&&fx.owner==actor)fx.gameObject.SetActive(false);}

        private GameObject owner;
        private bool cancelWithOwner;
        private void Update()
        {
            if(Time.time>=until||(cancelWithOwner&&(owner==null||!owner.activeInHierarchy||ownerHealth!=null&&ownerHealth.IsDead))||
                follows&&follow==null||shieldBound&&(shieldHealth==null||shieldHealth.IsDead||shieldHealth.Shield<=0))
            {gameObject.SetActive(false);return;}
            if(follows)
            {
                Vector3 delta=follow.position-followPosition;followPosition=follow.position;
                for(int i=0;i<stroke.positionCount;i++){trackedPoints[i]+=delta;stroke.SetPosition(i,trackedPoints[i]);}
            }
            float age=Mathf.InverseLerp(began,until,Time.time);
            stroke.widthMultiplier=baseWidth*(cancelWithOwner?1+age*.7f:1-age*.45f);
        }
        private static HeroSkillVfx Stroke(Vector3[] points,Color color,float width,float duration,bool loop,GameObject owner=null,bool cancel=false)
        {
            if(ink==null)
            {
                Shader shader=Shader.Find("Universal Render Pipeline/Particles/Unlit")??Shader.Find("Sprites/Default");
                if(shader==null)return null;
                ink=new Material(shader){name="DF_SharedSkillInk"};
            }
            HeroSkillVfx fx=null;
            for(int i=pool.Count-1;i>=0;i--)if(pool[i]==null)pool.RemoveAt(i);
            int limit=cancel?MaxStrokes:MaxStrokes-ReservedWarnings;
            // Separate reserved slots prevent impacts/basic attacks from starving warning outlines.
            for(int i=0;i<pool.Count&&i<limit;i++)if(!pool[i].gameObject.activeSelf){fx=pool[i];break;}
            if(fx==null&&pool.Count<limit)
            {
                GameObject obj=new("DF_PooledSkillStroke");
                fx=obj.AddComponent<HeroSkillVfx>();fx.stroke=obj.AddComponent<LineRenderer>();pool.Add(fx);
                fx.stroke.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;fx.stroke.receiveShadows=false;
            }
            if(fx==null)return null;
            if(owner!=null)fx.transform.SetParent(owner.transform.parent,true);
            fx.stroke.sharedMaterial=ink;fx.stroke.useWorldSpace=true;fx.stroke.loop=loop;
            fx.stroke.startColor=fx.stroke.endColor=color;fx.stroke.widthMultiplier=width;
            fx.stroke.positionCount=points.Length;fx.stroke.SetPositions(points);
            System.Array.Copy(points,fx.trackedPoints,points.Length);
            fx.began=Time.time;fx.until=Time.time+Mathf.Max(.05f,duration);fx.baseWidth=width;
            fx.owner=owner;fx.ownerHealth=owner!=null?owner.GetComponent<Health>():null;fx.cancelWithOwner=cancel;
            fx.follow=null;fx.follows=false;fx.shieldBound=false;fx.shieldHealth=null;
            fx.gameObject.SetActive(true);return fx;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMaterial() { foreach(var fx in pool)if(fx!=null)Destroy(fx.gameObject);pool.Clear();if (ink != null) Destroy(ink); ink = null; }
        public static void Area(GameObject actor, Vector3 center, float radius, float duration, bool warning, bool beneficial=false)
        {
            HeroIdentity hero = actor.GetComponent<HeroIdentity>();
            Color color = warning && !beneficial ? new Color(1f,.48f,.16f) : hero != null ? StylizedArtDirection.HeroAccent(hero.Hero) : Color.white;
            center.y = .22f;
            Vector3[] ring = new Vector3[40];
            for (int i = 0; i < ring.Length; i++)
            { float a = i * Mathf.PI * 2 / ring.Length; ring[i] = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius; }
            Stroke(ring, color, warning ? .14f : .2f, duration, true, actor, warning);
            int corners = hero == null ? 4 : hero.Hero == HeroId.Kaelon ? 6 : hero.Hero == HeroId.Mira ? 12 : hero.Hero == HeroId.Rook ? 8 : 4;
            Vector3[] glyph = new Vector3[corners];
            for (int i = 0; i < corners; i++)
            { float a = i * Mathf.PI * 2 / corners; float r = radius * ((hero != null && hero.Hero == HeroId.Mira && i % 2 == 1) ? .38f : .7f); glyph[i] = center + new Vector3(Mathf.Cos(a), .01f, Mathf.Sin(a)) * r; }
            Stroke(glyph, hero != null ? StylizedArtDirection.HeroAccent(hero.Hero) : color, .055f, duration, true, actor, warning);
        }
        static Color Accent(GameObject actor)=>StylizedArtDirection.HeroAccent(actor.GetComponent<HeroIdentity>().Hero);
        static void Follow(HeroSkillVfx fx,Transform target)
        {if(fx==null||target==null)return;fx.follow=target;fx.followPosition=target.position;fx.follows=true;}
        static void BindShield(HeroSkillVfx fx,Health target)
        {
            if(fx==null||target==null)return;
            Follow(fx,target.transform);fx.shieldHealth=target;fx.shieldBound=true;
        }
        static void ShieldShell(GameObject actor,Health target,float duration)
        {
            if(actor==null||target==null)return;
            Color accent=Accent(actor);
            Color shieldColor=Color.Lerp(accent,new Color(.25f,.9f,1f),.55f);
            Vector3 c=target.transform.position;

            // Three horizontal hex rings plus vertical ribs read as a real force shield
            // from the top-down camera while staying cheap on mobile.
            var lower=Ring(actor,c+Vector3.up*.22f,1.12f,duration,shieldColor,6);
            var middle=Ring(actor,c+Vector3.up*.95f,.98f,duration,shieldColor,6);
            var crown=Ring(actor,c+Vector3.up*1.62f,.46f,duration,Color.Lerp(shieldColor,Color.white,.45f),6);
            BindShield(lower,target);BindShield(middle,target);BindShield(crown,target);

            for(int i=0;i<3;i++)
            {
                float a=i*Mathf.PI/3f;
                Vector3 d=new(Mathf.Cos(a),0,Mathf.Sin(a));
                Vector3 side=Vector3.Cross(Vector3.up,d);
                Vector3[] rib=
                {
                    c+d*1.08f+Vector3.up*.24f,
                    c+d*.72f+side*.07f+Vector3.up*.94f,
                    c+Vector3.up*1.78f,
                    c-d*.72f-side*.07f+Vector3.up*.94f,
                    c-d*1.08f+Vector3.up*.24f
                };
                BindShield(Stroke(rib,shieldColor,.115f,duration,false,actor),target);
            }
        }
        static HeroSkillVfx Ring(GameObject actor,Vector3 center,float radius,float seconds,Color color,int sides=20, bool warning=false)
        {
            Vector3[] points=new Vector3[sides];
            for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;points[i]=center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;}
            return Stroke(points,color,.13f,seconds,true,actor,warning);
        }
        public static void Cast(GameObject actor,int slot)
        {
            HeroId hero=actor.GetComponent<HeroIdentity>().Hero;
            Ring(actor,actor.transform.position+Vector3.up*.45f,slot==3?.85f:.55f,.22f,Accent(actor),hero==HeroId.Kaelon?6:hero==HeroId.Elyra?3:hero==HeroId.Mira?12:4);
        }
        public static void TargetWarning(GameObject actor,Health target,float seconds)
        {
            var fx=Ring(actor,target.transform.position+Vector3.up*.28f,.8f,seconds,new Color(1,.48f,.16f),4,true);
            Follow(fx,target.transform);
        }
        public static void Spell(GameObject actor,Vector3 impact,int slot)
        {
            HeroId hero=actor.GetComponent<HeroIdentity>().Hero;Color color=Accent(actor);
            Vector3 start=actor.transform.position+Vector3.up*.65f,end=impact+Vector3.up*.65f;
            Vector3 forward=end-start;forward.y=0;forward=forward.sqrMagnitude<.01f?actor.transform.forward:forward.normalized;
            Vector3 side=Vector3.Cross(Vector3.up,forward);
            if(hero==HeroId.Veyr||hero==HeroId.Kaelon||hero==HeroId.Rook)
            {
                Vector3[] arc=new Vector3[9];float radius=hero==HeroId.Rook?1.05f:.8f;
                for(int i=0;i<arc.Length;i++){float a=Mathf.Lerp(-1.1f,1.1f,i/8f);arc[i]=end+side*Mathf.Sin(a)*radius+forward*(Mathf.Cos(a)-.6f)*radius;}
                Stroke(arc,color,slot==3?.28f:.2f,.24f,false,actor);
                if(hero==HeroId.Veyr)Stroke(new[]{start,end-side*.35f,end+side*.35f},color,.14f,.16f,false,actor);
            }
            else
            {
                // Existing mechanics resolve instantly: a beam shows the real damage event, without invented flight delay.
                Stroke(new[]{start,end},color,slot==3?.2f:.14f,.18f,false,actor);
                Stroke(new[]{end-forward*.65f-side*.26f,end,end-forward*.65f+side*.26f},color,.13f,.22f,false,actor);
            }
        }
        public static void Ultimate(GameObject actor, Vector3 target)
        {
            HeroIdentity identity=actor.GetComponent<HeroIdentity>();if(identity==null)return;
            Color c=Accent(actor);Vector3 origin=actor.transform.position+Vector3.up*.7f;
            Vector3 direction=target-origin;direction.y=0;if(direction.sqrMagnitude<.01f)direction=actor.transform.forward;direction.Normalize();
            Vector3 side=Vector3.Cross(Vector3.up,direction);Vector3 end=origin+direction*Mathf.Max(3.2f,Vector3.Distance(actor.transform.position,target));
            switch(identity.Hero)
            {
                case HeroId.Veyr: // fractured boomerang / void crescent
                    Stroke(new[]{origin-side*.25f,origin+direction*1.4f+side*.9f,end,end-direction*.65f-side*.85f},c,.28f,.42f,false,actor);
                    Stroke(new[]{end-side*.65f,end+direction*.35f,end+side*.65f},Color.white,.09f,.3f,false,actor);break;
                case HeroId.Nyra: // long precision bolt
                    Stroke(new[]{origin,end},c,.22f,.34f,false,actor);Stroke(new[]{origin+side*.28f,end-side*.28f},Color.white,.07f,.26f,false,actor);break;
                case HeroId.Elyra: // unstable lightning fork
                    Stroke(new[]{origin,origin+direction*1.2f+side*.45f,origin+direction*2.3f-side*.35f,end},c,.24f,.4f,false,actor);
                    Stroke(new[]{origin+direction*2.1f,end-direction*.7f+side*.75f},c,.1f,.28f,false,actor);break;
                case HeroId.Kaelon: // expanding citadel shock front
                    Ring(actor,actor.transform.position+Vector3.up*.32f,1.15f,.38f,c,6);Ring(actor,actor.transform.position+Vector3.up*.34f,2.1f,.48f,c,6);break;
                case HeroId.Mira: // large restorative dimensional bloom
                    Ring(actor,actor.transform.position+Vector3.up*.38f,1.35f,.4f,c,12);Ring(actor,actor.transform.position+Vector3.up*.4f,2.5f,.55f,new Color(.45f,1f,.72f),12);break;
                default: // heavy overdrive cleave
                    Vector3 p=actor.transform.position+direction*1.7f+Vector3.up*.55f;
                    Stroke(new[]{p-side*1.5f,p+direction*.75f,p+side*1.5f},c,.34f,.4f,false,actor);Ring(actor,p,.95f,.32f,c,8);break;
            }
        }
        public static void Impact(GameObject actor,Health target,int slot)
        {
            Vector3 p=target.transform.position+Vector3.up*.5f;Color c=Accent(actor);
            Stroke(new[]{p+new Vector3(-.5f,0,-.4f),p,p+new Vector3(.5f,0,.4f)},c,.19f,.22f,false,actor);
            Stroke(new[]{p+new Vector3(-.4f,0,.5f),p,p+new Vector3(.4f,0,-.5f)},c,.16f,.22f,false,actor);
            if(slot==3&&target.GetComponent<HeroIdentity>()!=null)Ring(actor,p,.65f,.28f,c,8);
        }
        public static void Support(GameObject actor,Health target,bool shield,float duration)
        {
            Color c=shield?new Color(.3f,.82f,1):new Color(.35f,1,.65f);
            Vector3 p=target.transform.position+Vector3.up*.42f;
            if(shield)
            {
                ShieldShell(actor,target,duration);
                return;
            }

            var fx=Ring(actor,p,.65f,.45f,c,16);Follow(fx,target.transform);
            Follow(Stroke(new[]{p+Vector3.left*.4f,p+Vector3.right*.4f},c,.18f,.45f,false,actor),target.transform);
            Follow(Stroke(new[]{p+Vector3.back*.4f,p+Vector3.forward*.4f},c,.18f,.45f,false,actor),target.transform);
        }
        public static void SupportLink(GameObject actor,Health target)
        {Stroke(new[]{actor.transform.position+Vector3.up*.6f,target.transform.position+Vector3.up*.6f},new Color(.4f,.92f,.75f),.13f,.25f,false,actor);}
        public static void Debuff(GameObject actor,Health target)
        {if(target.GetComponent<HeroIdentity>()==null)return;var fx=Ring(actor,target.transform.position+Vector3.up*.25f,.65f,.5f,new Color(.9f,.55f,.24f),4);Follow(fx,target.transform);}
        public static void PlayerHit(GameObject victim,GameObject source)
        {
            if(source==null)return;Vector3 d=source.transform.position-victim.transform.position;d.y=0;
            if(d.sqrMagnitude<.01f)return;d.Normalize();Vector3 side=Vector3.Cross(Vector3.up,d)*.3f;
            Vector3 p=victim.transform.position+Vector3.up*.35f+d*.95f;
            Stroke(new[]{p-side-d*.2f,p,p+side-d*.2f},new Color(1,.62f,.4f),.16f,.18f,false,victim);
        }
        public static void Basic(GameObject actor, Vector3 impact)
        {
            HeroIdentity hero = actor.GetComponent<HeroIdentity>();
            Color color = hero != null ? StylizedArtDirection.HeroAccent(hero.Hero) : Color.white;
            actor.GetComponent<HeroVisualMotion>()?.PulseAttack(false);
            Vector3 start = actor.transform.position + Vector3.up * .5f, end = impact + Vector3.up * .5f;
            bool ranged = actor.GetComponent<HeroCombatProfile>()?.AttackRange > 4f;
            if (ranged) Stroke(new[] { start, end }, color, hero != null && hero.Hero == HeroId.Nyra ? .09f : .055f, .14f, false, actor);
            else
            {
                Vector3 side = actor.transform.right * .7f;
                Stroke(new[] { end - side, end + actor.transform.forward * .35f, end + side }, color, .12f, .16f, false, actor);
            }
        }
        public static void Link(GameObject actor, Vector3 impact, bool ultimate = false)
        {
            Basic(actor, impact); actor.GetComponent<HeroVisualMotion>()?.PulseAttack(ultimate);
            if (ultimate) Area(actor, impact, .65f, .28f, false);
        }
        public static void Dash(GameObject actor, Vector3 from, Vector3 to)
        {
            Color color = StylizedArtDirection.HeroAccent(actor.GetComponent<HeroIdentity>().Hero);
            Stroke(new[] { from + Vector3.up*.25f, to + Vector3.up*.25f }, color, .18f, .25f, false, actor);
            Follow(Ring(actor,from+Vector3.up*.4f,.55f,.22f,color,4),actor.transform);
        }
    }
}
