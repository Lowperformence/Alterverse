using Alterverse.Audio;
using Alterverse.Characters;
using Alterverse.Visuals;
using UnityEngine;
namespace Alterverse.Combat
{
    [RequireComponent(typeof(Health))]
    public sealed class CombatFeedback:MonoBehaviour
    {
        private Health health;
        private Renderer[] renderers;
        private MaterialPropertyBlock[] saved;
        private bool[] flashed;
        private readonly MaterialPropertyBlock bright=new();
        private float flashUntil;
        private bool flashing;
        private Camera view;
        private static readonly int BaseColor=Shader.PropertyToID("_BaseColor"),ColorId=Shader.PropertyToID("_Color");
        private void Awake(){health=GetComponent<Health>();health.Damaged+=OnDamaged;}
        private void Start(){RefreshVisuals();view=Camera.main;CombatImpactPool.Warm(transform.root);}
        public void RefreshVisuals()
        {
            Restore();renderers=GetComponentsInChildren<Renderer>(true);
            saved=new MaterialPropertyBlock[renderers.Length];flashed=new bool[renderers.Length];
            for(int i=0;i<saved.Length;i++)saved[i]=new MaterialPropertyBlock();
        }
        private void OnDamaged(float damage,float absorbed,GameObject source)
        {
            if(!isActiveAndEnabled||damage+absorbed<=.01f)return;
            if(view==null)view=Camera.main;if(view==null)return;
            Vector3 point=transform.position+Vector3.up*1.1f,p=view.WorldToViewportPoint(point);
            if(p.z<=0||p.x<-.05f||p.x>1.05f||p.y<-.05f||p.y>1.05f)return;
            bool shieldOnly=damage<=.01f;
            Color tint=shieldOnly?new Color(.45f,.87f,1f):Color.Lerp(SourceColor(source),Color.white,.65f);
            if(renderers==null)RefreshVisuals();
            // Keep the original property blocks across repeated hits, not an already flashed snapshot.
            for(int i=0;i<renderers.Length;i++)
            {
                Renderer r=renderers[i];if(r==null||!r.enabled)continue;
                if(!flashed[i]){r.GetPropertyBlock(saved[i]);flashed[i]=true;}
                r.GetPropertyBlock(bright);bright.SetColor(BaseColor,tint);bright.SetColor(ColorId,tint);r.SetPropertyBlock(bright);
            }
            flashing=true;flashUntil=Time.time+.065f;
            CombatImpactPool.Emit(point,tint,(damage+absorbed)/Mathf.Max(1,health.Max*.2f));
            bool heavy=damage>=Mathf.Max(45f,health.Max*.16f);AudioMixState.MarkCombat();
            if(AudioCuePolicy.Allow(heavy?"heavy-hit":"hit",heavy?AudioCuePriority.Critical:AudioCuePriority.Combat))
                DimensionAudio.Play(heavy?DimensionSound.HitHeavy:DimensionSound.HitLight,shieldOnly?.28f:heavy?.72f:.48f,Random.Range(.96f,1.045f));
        }
        private static Color SourceColor(GameObject source)
        {HeroIdentity hero=source!=null?source.GetComponentInParent<HeroIdentity>():null;return hero!=null?StylizedArtDirection.HeroAccent(hero.Hero):new Color(.95f,.74f,.45f);}
        private void LateUpdate(){if(flashing&&Time.time>=flashUntil)Restore();}
        private void Restore()
        {
            if(renderers!=null)for(int i=0;i<renderers.Length;i++)if(flashed[i]){if(renderers[i]!=null)renderers[i].SetPropertyBlock(saved[i]);flashed[i]=false;}
            flashing=false;
        }
        private void OnDisable()=>Restore();
        private void OnDestroy(){Restore();if(health!=null)health.Damaged-=OnDamaged;}
    }
    // Retained for existing transient effects outside hero impact feedback.
    public sealed class TransientVfx:MonoBehaviour
    {
        private float lifetime,growSpeed,age;
        public void Initialize(float duration,float growth){lifetime=duration;growSpeed=growth;}
        private void Update(){age+=Time.deltaTime;transform.localScale+=Vector3.one*growSpeed*Time.deltaTime;if(age>=lifetime)Destroy(gameObject);}
    }
}
