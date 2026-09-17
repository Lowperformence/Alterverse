using System.Collections.Generic;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Match
{
    public sealed class MatchScoreboard : MonoBehaviour
    {
        private sealed class Entry
        {
            public TeamMember Member;
            public string Name;
            public int Kills;
            public int Deaths;
            public int Assists;
            public int Score => Kills * 100 + Assists * 50;
        }

        public static MatchScoreboard Instance { get; private set; }
        [SerializeField] private float refreshInterval=.5f;
        [SerializeField] private float assistWindow=8f;
        private readonly Dictionary<Health,Entry> entries=new();
        private readonly Dictionary<Health,Dictionary<TeamMember,float>> damageCredit=new();
        private float nextRefresh;
        private bool expanded;
        private GUIStyle header;
        private GUIStyle row;

        private void Awake(){Instance=this;}
        private void Start(){RefreshRoster();}
        private void Update(){if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+refreshInterval;RefreshRoster();}if(Input.GetKeyDown(KeyCode.Tab))expanded=!expanded;}

        public void RecordDamage(Health victim,GameObject source)
        {
            if(victim==null||source==null)return;
            TeamMember attacker=source.GetComponentInParent<TeamMember>();TeamMember victimTeam=victim.GetComponent<TeamMember>();
            if(attacker==null||victimTeam==null||attacker.Team==victimTeam.Team)return;
            if(!damageCredit.TryGetValue(victim,out Dictionary<TeamMember,float> credits)){credits=new();damageCredit[victim]=credits;}
            credits[attacker]=Time.time;
        }

        private void RefreshRoster()
        {
            foreach(TeamMember member in Object.FindObjectsByType<TeamMember>(FindObjectsSortMode.None))
            {
                if(member==null)continue;Health health=member.GetComponent<Health>();if(health==null||entries.ContainsKey(health))continue;
                entries.Add(health,new Entry{Member=member,Name=DisplayName(member.gameObject.name)});health.DiedWithSource+=OnHeroDied;
            }
        }

        private void OnHeroDied(Health victim,GameObject source)
        {
            if(victim==null||!entries.TryGetValue(victim,out Entry victimEntry))return;victimEntry.Deaths++;
            TeamMember killer=source!=null?source.GetComponentInParent<TeamMember>():null;
            if(killer!=null&&killer.Team!=victimEntry.Member.Team)
            {
                Entry killerEntry=FindEntry(killer);if(killerEntry!=null)killerEntry.Kills++;
                if(damageCredit.TryGetValue(victim,out Dictionary<TeamMember,float> credits))foreach(var credit in credits)
                    if(credit.Key!=null&&credit.Key!=killer&&Time.time-credit.Value<=assistWindow&&credit.Key.Team==killer.Team){Entry assist=FindEntry(credit.Key);if(assist!=null)assist.Assists++;}
            }
            damageCredit.Remove(victim);
        }

        private Entry FindEntry(TeamMember member){foreach(Entry e in entries.Values)if(e.Member==member)return e;return null;}
        private static string DisplayName(string raw){return raw=="AV_Player"?"VEY R":raw.Replace("AV_","").Replace("Bot_"," ");}
        private int TeamKills(int team){int total=0;foreach(Entry e in entries.Values)if(e.Member.Team==team)total+=e.Kills;return total;}

        private void OnGUI()
        {
            header??=new GUIStyle(GUI.skin.label){fontSize=17,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};row??=new GUIStyle(GUI.skin.label){fontSize=15,alignment=TextAnchor.MiddleCenter};
            float compactWidth=Mathf.Min(280f,Screen.width*.38f);float compactX=(Screen.width-compactWidth)*.5f;GUI.Box(new Rect(compactX,42,compactWidth,30),GUIContent.none);GUI.Label(new Rect(compactX,44,compactWidth,25),$"ZERO  {TeamKills(0)}   •   {TeamKills(1)}  LUMEN",header);
            if(!expanded)return;
            float width=Mathf.Min(650f,Screen.width*.72f);float x=(Screen.width-width)*.5f;float y=78f;List<Entry> zero=new(),lumen=new();foreach(Entry e in entries.Values)(e.Member.Team==0?zero:lumen).Add(e);zero.Sort((a,b)=>b.Score.CompareTo(a.Score));lumen.Sort((a,b)=>b.Score.CompareTo(a.Score));float height=62f+Mathf.Max(zero.Count,lumen.Count)*27f;GUI.Box(new Rect(x,y,width,height),GUIContent.none);GUI.Label(new Rect(x,y+4,width,25),"ALTERVERSE  •  K / D / A / SCORE",header);DrawTeam(zero,x+10,y+34,width*.5f-15,"ZERO");DrawTeam(lumen,x+width*.5f+5,y+34,width*.5f-15,"LUMEN");
        }

        private void DrawTeam(List<Entry> team,float x,float y,float width,string title){GUI.Label(new Rect(x,y,width,22),title,header);for(int i=0;i<team.Count;i++){Entry e=team[i];GUI.Label(new Rect(x,y+24+i*27,width,24),$"{e.Name,-13}   {e.Kills} / {e.Deaths} / {e.Assists} / {e.Score}",row);}}
        private void OnDestroy(){foreach(var pair in entries)if(pair.Key!=null)pair.Key.DiedWithSource-=OnHeroDied;entries.Clear();if(Instance==this)Instance=null;}
    }
}
