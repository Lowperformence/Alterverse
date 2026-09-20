using Alterverse.Match;
using Alterverse.Combat;
using Alterverse.Profile;
using Alterverse.UI;
using UnityEngine;
namespace Alterverse.Audio
{
    // Production clips only. Missing assets are reported once, never substituted with test tones.
    public sealed class ProductionMusicDirector:MonoBehaviour
    {
        static readonly string[] Names={"Menu","Match","Pressure","Climax","Victory","Defeat"};
        readonly AudioClip[] clips=new AudioClip[6];readonly AudioSource[] decks=new AudioSource[2];
        DimensionsFront front;int current=-1,deck;float fade=1,eventUntil,nextResolve;int result=-1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create(){if(FindAnyObjectByType<ProductionMusicDirector>()==null)new GameObject("DF_ProductionMusic").AddComponent<ProductionMusicDirector>();}
        void Awake()
        {
            EnsureDecks();
            int missing=0;for(int i=0;i<2;i++){clips[i]=Resources.Load<AudioClip>("Audio/Music/"+Names[i]);if(clips[i]==null)missing++;}
            clips[2]=clips[3]=clips[1]; // One coherent licensed match bed; intensity changes mixing, not playback position.
            if(missing>0)Debug.LogWarning("DIMENSIONS FRONT: FINAL MUSIC ASSET REQUIRED — "+missing+" missing production clips. See Docs/Production-Audio.md.");
            DimensionPlanetDirector.PlanetArrived+=Planet;
        }
        void EnsureDecks()
        {
            if(decks[0]!=null&&decks[1]!=null)return;
            AudioSource[] existing=GetComponents<AudioSource>();
            for(int i=0;i<2;i++)
            {
                if(decks[i]==null)decks[i]=i<existing.Length?existing[i]:gameObject.AddComponent<AudioSource>();
                decks[i].playOnAwake=false;decks[i].spatialBlend=0;decks[i].priority=64;
            }
        }
        void Event()=>eventUntil=Time.unscaledTime+12;
        void Planet(Vector3 position)=>Event();
        void End(int winner)
        {
            var player=FindAnyObjectByType<Alterverse.Characters.HeroRespawn>();var team=player!=null?player.GetComponent<TeamMember>():null;
            result=team!=null&&winner==team.Team?4:5;
        }
        void Update()
        {
            if(decks[0]==null||decks[1]==null)EnsureDecks();
            if(front==null&&Time.unscaledTime>=nextResolve)
            {
                nextResolve=Time.unscaledTime+.5f;front=FindAnyObjectByType<DimensionsFront>();
                if(front!=null){front.MatchEnded+=End;result=-1;eventUntil=0;}
            }
            bool menu=MainMenuController.IsVisible;
            int target=menu?0:result>=0?result:front==null?0:front.IsOvertime||front.TimeRemaining<90||Mathf.Max(front.ZeroControl,front.LumenControl)>85?3:AudioMixState.CombatActive||Time.unscaledTime<eventUntil?2:1;
            // A missing intensity stem keeps the authored match bed. Missing result fades to silence.
            if(target>1&&target<4&&clips[target]==null)target=1;
            if(target!=current&&fade>=1&&clips[target]==decks[deck].clip)current=target;
            if(target!=current&&fade>=1)
            {
                current=target;deck=1-deck;AudioSource next=decks[deck];next.Stop();next.clip=clips[target];next.loop=target<4;next.volume=0;
                if(next.clip!=null)next.Play();fade=0;
            }
            bool paused=MatchClock.InBackground||(!menu&&MatchClock.Paused&&result<0);
            for(int i=0;i<2;i++){if(paused)decks[i].Pause();else decks[i].UnPause();}
            if(!paused)fade=Mathf.MoveTowards(fade,1,Time.unscaledDeltaTime/.9f);
            float gain=PlayerProfile.MasterVolume*PlayerProfile.MusicVolume*(AudioMixState.CombatActive?.25f:current==3?.45f:current==2?.40f:.34f);
            decks[deck].volume=gain*Mathf.Sin(fade*Mathf.PI*.5f);decks[1-deck].volume=gain*Mathf.Cos(fade*Mathf.PI*.5f);
            if(fade>=1)decks[1-deck].Stop();
        }
        void OnDestroy(){if(front!=null)front.MatchEnded-=End;DimensionPlanetDirector.PlanetArrived-=Planet;}
    }
}
