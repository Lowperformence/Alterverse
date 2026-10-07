using System;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed partial class MainMenuController
    {
        int rankingTab,friendTab,friendOffset,clanTab,memberOffset;
        string socialIdentity,lastClanIdentity;
        bool clanApplications,clanOpen=true;
        string playerQuery="",clanQuery="",clanName="",clanTag="",clanDescription="";
        string confirmRoute,confirmPlayer,confirmLabel;
        static readonly SocialService.Player[] NoPlayers=Array.Empty<SocialService.Player>();
        static readonly SocialService.Clan[] NoClans=Array.Empty<SocialService.Clan>();

        bool SocialReady()
        {
            var service=SocialService.Instance;
            if(service!=null&&service.Connected)
            {
                string identity=service.State?.me?.playerId;
                if(identity!=null&&identity!=socialIdentity)
                {
                    socialIdentity=identity;lastClanIdentity=null;confirmRoute=confirmPlayer=confirmLabel=null;
                    friendOffset=memberOffset=0;playerQuery=clanQuery="";
                    clanName=clanTag=clanDescription="";
                }
                return true;
            }
            theme.Panel(new Rect(Left,272,ContentWidth,170));
            theme.Label(new Rect(Left+24,292,ContentWidth-48,48),"Connect your account to load players, friends and clans.",theme.Heading);
            theme.Label(new Rect(Left+24,348,ContentWidth-48,36),AccountServerBootstrap.Instance?.Status??"Account service unavailable.",theme.Body,true);
            if(theme.Action(new Rect(Left+24,394,200,40),"OPEN ACCOUNT"))SetPage(Page.Profile);
            return false;
        }
        void SocialFooter()
        {
            var service=SocialService.Instance;
            theme.FitLabel(new Rect(Left,672,ContentWidth-170,32),service?.Busy==true?"SYNCHRONIZING…":service?.Status??"Account server not connected.",theme.Small,true);
            if(theme.Action(new Rect(Right-150,670,150,36),"REFRESH",enabled:service!=null&&!service.Busy))
            {
                if(page==Page.Leaderboard&&rankingTab>0)
                    service.Load(rankingTab==1?"/social/levels":"/social/clan-levels",new SocialService.Request{offset=RankingOffset()});
                else if(service.State==null)service.Load("/social/state");
                else if(page==Page.Friends&&friendTab==4&&playerQuery.Trim().Length>=2)
                    service.Load("/social/players",new SocialService.Request{query=playerQuery,offset=service.Search?.offset??0});
                else if(page==Page.Clans&&clanTab==0)
                    service.Load("/social/clans",new SocialService.Request{query=clanQuery,offset=service.Clans?.offset??0});
                else service.Load("/social/state");
            }
        }
        int RankingOffset()=>rankingTab==1?SocialService.Instance?.Levels?.offset??0:SocialService.Instance?.ClanLevels?.offset??0;
        void SocialAction(string route,string playerId=null,string clanId=null,string role=null)
        {
            SocialService.Instance?.Load(route,new SocialService.Request{playerId=playerId,clanId=clanId,role=role});
        }
        bool SocialButton(Rect rect,string label,string route,string playerId=null,string clanId=null,string role=null,bool enabled=true)
        {
            var service=SocialService.Instance;
            if(!theme.Action(rect,label,enabled:enabled&&service!=null&&!service.Busy))return false;
            SocialAction(route,playerId,clanId,role);return true;
        }
        void ConfirmAction(string route,string playerId,string label)
        {confirmRoute=route;confirmPlayer=playerId;confirmLabel=label;}
        bool DrawSocialConfirmation()
        {
            if(string.IsNullOrEmpty(confirmRoute))return false;
            theme.Panel(new Rect(Left,272,ContentWidth,210));
            theme.Label(new Rect(Left+24,294,ContentWidth-48,60),confirmLabel,theme.Heading);
            if(theme.Action(new Rect(Left+24,396,220,54),"CONFIRM",true,enabled:SocialService.Instance?.Busy==false))
            {SocialAction(confirmRoute,confirmPlayer);confirmRoute=confirmPlayer=confirmLabel=null;}
            if(theme.Action(new Rect(Left+260,396,180,54),"CANCEL"))confirmRoute=confirmPlayer=confirmLabel=null;
            SocialFooter();return true;
        }
        void DrawSocialPager(int offset,int count,Action<int> change,int total=-1,float y=626,int pageSize=8)
        {
            bool busy=SocialService.Instance?.Busy==true;
            if(theme.Action(new Rect(Left,y,100,36),"PREVIOUS",enabled:offset>0&&!busy))change(Mathf.Max(0,offset-pageSize));
            theme.Center(new Rect(Left+110,y,ContentWidth-220,36),count==0?"NO RESULTS":$"{offset+1}–{offset+count}"+(total>=0?$" / {total}":""));
            if(theme.Action(new Rect(Right-100,y,100,36),"NEXT",enabled:!busy&&(total>=0?offset+count<total:count==pageSize)))change(offset+pageSize);
        }

        bool DrawRankingTabs()
        {
            if(rankingTab>0)Heading(rankingTab==1?"Player Levels":"Clan Levels",rankingTab==1?"All players ranked by level, then total experience.":"Clans ranked by the sum of every member's player level.");
            string[] tabs={"MOST WINS","PLAYER LEVELS","CLAN LEVELS"};
            for(int i=0;i<tabs.Length;i++)
            {
                if(theme.Action(new Rect(Left+i*210,202,198,36),tabs[i],rankingTab==i))
                {
                    rankingTab=i;
                    if(i>0)SocialService.Instance?.Load(i==1?"/social/levels":"/social/clan-levels");
                }
            }
            if(rankingTab==0)return false;
            if(!SocialReady()){SocialFooter();return true;}
            var service=SocialService.Instance;
            var result=rankingTab==1?service.Levels:service.ClanLevels;
            if(result==null)
            {
                theme.Label(new Rect(Left,268,ContentWidth,36),"Tap REFRESH to load the global ranking.",theme.Body,true);
                SocialFooter();return true;
            }
            theme.FitLabel(new Rect(Left,256,ContentWidth,32),rankingTab==1?
                $"YOUR PLACE  #{result.selfRank}  /  LEVEL {result.me?.level??PlayerExperience.Level}":
                result.selfRank>0?$"YOUR CLAN  #{result.selfRank}":"Join a clan to appear in the clan ranking.",theme.Heading);
            theme.Label(new Rect(Left,292,ContentWidth,22),"PLAYER LEVELS ARE SAVED GAME PROGRESS  /  ALL-TIME",theme.Small,true);
            int count;
            if(rankingTab==1)
            {
                var rows=result.players??NoPlayers;count=rows.Length;
                for(int i=0;i<rows.Length;i++)
                {
                    var p=rows[i];float y=322+i*37;
                    theme.Panel(new Rect(Left,y,ContentWidth,34));
                    theme.FitLabel(new Rect(Left+14,y,ContentWidth-340,34),$"#{p.rank}  {p.name}",theme.Body);
                    theme.FitLabel(new Rect(Right-310,y,290,34),$"LEVEL {p.level}  /  {p.xp:N0} XP",theme.Small);
                }
            }
            else
            {
                var rows=result.clans??NoClans;count=rows.Length;
                for(int i=0;i<rows.Length;i++)
                {
                    var c=rows[i];float y=322+i*37;
                    theme.Panel(new Rect(Left,y,ContentWidth,34));
                    theme.FitLabel(new Rect(Left+14,y,ContentWidth-380,34),$"#{c.rank}  [{c.tag}] {c.name}",theme.Body);
                    theme.FitLabel(new Rect(Right-350,y,330,34),$"TOTAL {c.totalLevel:N0}  /  {c.memberCount}/50 MEMBERS",theme.Small);
                }
            }
            DrawSocialPager(result.offset,count,offset=>service.Load(rankingTab==1?"/social/levels":"/social/clan-levels",new SocialService.Request{offset=offset}),result.total);
            SocialFooter();return true;
        }

        void DrawFriends()
        {
            Heading("Friends","Find players by name or player ID. Friendships require an accepted request.");
            string[] tabs={"FRIENDS","REQUESTS","SENT","BLOCKED","FIND PLAYERS"};
            for(int i=0;i<tabs.Length;i++)
                if(theme.Action(new Rect(Left+i*155,212,145,40),tabs[i],friendTab==i))
                {friendTab=i;friendOffset=0;confirmRoute=null;}
            if(!SocialReady()){SocialFooter();return;}
            var service=SocialService.Instance;
            var state=service.State;
            if(DrawSocialConfirmation())return;
            if(state==null){SocialFooter();return;}
            theme.FitLabel(new Rect(Left,262,ContentWidth,30),"YOUR PLAYER ID  "+state.me?.playerId,theme.Body);
            SocialService.Player[] rows;
            friendOffset=Mathf.Min(friendOffset,Mathf.Max(0,((friendTab==0?state.friends:friendTab==1?state.incoming:friendTab==2?state.outgoing:state.blocked)?.Length??0)-1)/8*8);
            int start=friendOffset;
            if(friendTab==4)
            {
                playerQuery=GUI.TextField(new Rect(Left,298,ContentWidth-190,40),playerQuery,40,theme.Field);
                if(theme.Action(new Rect(Right-178,298,178,40),"SEARCH",enabled:!service.Busy&&playerQuery.Trim().Length>=2))
                {service.Load("/social/players",new SocialService.Request{query=playerQuery});}
                rows=service.Search?.players??NoPlayers;start=0;
            }
            else rows=(friendTab==0?state.friends:friendTab==1?state.incoming:friendTab==2?state.outgoing:state.blocked)??NoPlayers;
            int count=Mathf.Min(8,Mathf.Max(0,rows.Length-start));
            for(int i=0;i<count;i++)
            {
                var p=rows[start+i];float y=(friendTab==4?346:308)+i*34;
                theme.Panel(new Rect(Left,y,ContentWidth,32));
                theme.FitLabel(new Rect(Left+12,y,ContentWidth-390,32),$"{p.name}  /  LV {p.level}  /  {p.playerId}",theme.Small);
                if(friendTab==0)
                {
                    SocialButton(new Rect(Right-252,y,120,30),"REMOVE","/social/friend/remove",p.playerId);
                    SocialButton(new Rect(Right-124,y,120,30),"BLOCK","/social/block",p.playerId);
                }
                else if(friendTab==1)
                {
                    SocialButton(new Rect(Right-252,y,120,30),"ACCEPT","/social/friend/accept",p.playerId);
                    SocialButton(new Rect(Right-124,y,120,30),"DECLINE","/social/friend/remove",p.playerId);
                }
                else if(friendTab==2)SocialButton(new Rect(Right-124,y,120,30),"CANCEL","/social/friend/remove",p.playerId);
                else if(friendTab==3)SocialButton(new Rect(Right-124,y,120,30),"UNBLOCK","/social/unblock",p.playerId);
                else
                {
                    bool friends=Array.Exists(state.friends??NoPlayers,x=>x.playerId==p.playerId);
                    bool sent=Array.Exists(state.outgoing??NoPlayers,x=>x.playerId==p.playerId);
                    bool incoming=Array.Exists(state.incoming??NoPlayers,x=>x.playerId==p.playerId);
                    SocialButton(new Rect(Right-252,y,120,30),friends?"FRIENDS":sent?"SENT":incoming?"ACCEPT":"ADD",
                        incoming?"/social/friend/accept":"/social/friend/request",p.playerId,enabled:!friends&&!sent);
                    SocialButton(new Rect(Right-124,y,120,30),"BLOCK","/social/block",p.playerId);
                }
            }
            if(friendTab==4)
                DrawSocialPager(service.Search?.offset??0,count,offset=>{service.Load("/social/players",new SocialService.Request{query=playerQuery,offset=offset});});
            else DrawSocialPager(friendOffset,count,offset=>friendOffset=offset,rows.Length);
            if(count==0)theme.Label(new Rect(Left,362,ContentWidth,40),friendTab==4?"Search by name or paste a player's ID.":"No players in this list.",theme.Body,true);
            SocialFooter();
        }

        void DrawClans()
        {
            Heading("Clans","Create a clan or join one. Up to 50 members contribute to your clan's total level.");
            string[] tabs={"FIND CLANS","MY CLAN","CREATE CLAN"};
            for(int i=0;i<tabs.Length;i++)
                if(theme.Action(new Rect(Left+i*210,212,198,40),tabs[i],clanTab==i))
                {clanTab=i;memberOffset=0;confirmRoute=null;if(i==0)SocialService.Instance?.Load("/social/clans");}
            if(!SocialReady()){SocialFooter();return;}
            if(DrawSocialConfirmation())return;
            var service=SocialService.Instance;var state=service.State;
            if(state==null){SocialFooter();return;}
            string currentClan=state.clan?.clanId;
            if(currentClan!=lastClanIdentity)
            {
                lastClanIdentity=currentClan;memberOffset=0;
                clanTab=currentClan!=null?1:0;
            }
            if(clanTab==0)DrawClanBrowser(service,state);
            else if(clanTab==1)DrawMyClan(service,state);
            else DrawCreateClan(service,state);
            SocialFooter();
        }
        void DrawClanBrowser(SocialService service,SocialService.Snapshot state)
        {
            clanQuery=GUI.TextField(new Rect(Left,270,ContentWidth-190,40),clanQuery,40,theme.Field);
            if(theme.Action(new Rect(Right-178,270,178,40),"SEARCH",enabled:!service.Busy))
            {service.Load("/social/clans",new SocialService.Request{query=clanQuery});}
            var rows=service.Clans?.clans??NoClans;
            for(int i=0;i<rows.Length;i++)
            {
                var c=rows[i];float y=326+i*36;
                theme.Panel(new Rect(Left,y,ContentWidth,34));
                theme.FitLabel(new Rect(Left+12,y,ContentWidth-440,34),$"[{c.tag}] {c.name}",theme.Body);
                theme.FitLabel(new Rect(Right-416,y,240,34),$"{c.memberCount}/50  /  LV {c.totalLevel:N0}  /  "+(c.open?"OPEN":"APPLY"),theme.Small);
                bool pending=Array.IndexOf(state.pendingClans??Array.Empty<string>(),c.clanId)>=0;
                SocialButton(new Rect(Right-160,y,156,32),pending?"CANCEL REQUEST":c.open?"JOIN":"APPLY",
                    pending?"/social/clan/cancel":"/social/clan/join",clanId:c.clanId,enabled:state.clan==null&&(pending||c.memberCount<50));
            }
            if(rows.Length==0)theme.Label(new Rect(Left,346,ContentWidth,40),"Search to browse clans, or create your own.",theme.Body,true);
            DrawSocialPager(service.Clans?.offset??0,rows.Length,offset=>{service.Load("/social/clans",new SocialService.Request{query=clanQuery,offset=offset});});
        }
        void DrawCreateClan(SocialService service,SocialService.Snapshot state)
        {
            if(state.clan!=null){theme.Label(new Rect(Left,284,ContentWidth,48),"You already belong to a clan. Open MY CLAN.",theme.Heading);return;}
            theme.Label(new Rect(Left,276,220,36),"NAME (3–24)",theme.Small);
            clanName=GUI.TextField(new Rect(Left+230,270,ContentWidth-230,42),clanName,24,theme.Field);
            theme.Label(new Rect(Left,334,220,36),"TAG (2–5)",theme.Small);
            clanTag=GUI.TextField(new Rect(Left+230,328,260,42),clanTag,5,theme.Field);
            theme.Label(new Rect(Left,392,220,36),"DESCRIPTION",theme.Small);
            clanDescription=GUI.TextField(new Rect(Left+230,386,ContentWidth-230,56),clanDescription,160,theme.Field);
            if(theme.Action(new Rect(Left,462,340,48),clanOpen?"JOIN POLICY: OPEN":"JOIN POLICY: APPLICATION"))clanOpen=!clanOpen;
            theme.Label(new Rect(Left,526,ContentWidth,46),"Creating a clan is free. Names and tags must be unique. Maximum 50 members.",theme.Body,true);
            if(theme.Action(new Rect(Left,590,300,54),"CREATE CLAN",true,enabled:!service.Busy&&clanName.Trim().Length>=3&&clanTag.Length>=2))
                service.Load("/social/clan/create",new SocialService.Request{name=clanName,tag=clanTag,description=clanDescription,open=clanOpen});
        }
        void DrawMyClan(SocialService service,SocialService.Snapshot state)
        {
            var clan=state.clan;
            if(clan==null){theme.Label(new Rect(Left,284,ContentWidth,48),"Join or create a clan to see its members.",theme.Heading);return;}
            theme.FitLabel(new Rect(Left,268,ContentWidth-190,40),$"[{clan.tag}] {clan.name}  /  TOTAL LEVEL {clan.totalLevel:N0}",theme.Heading);
            if(theme.Action(new Rect(Right-178,270,178,38),"LEAVE CLAN",enabled:!service.Busy))
                ConfirmAction("/social/clan/leave",null,clan.memberCount==1?"Leave and delete your empty clan?":"Leave this clan? Leaders must transfer leadership first.");
            theme.FitLabel(new Rect(Left,310,ContentWidth,28),clan.description,theme.Small,true);
            bool manages=state.role=="leader"||state.role=="officer";
            if(!manages)clanApplications=false;
            if(theme.Action(new Rect(Left,346,170,34),"MEMBERS",!clanApplications)){clanApplications=false;memberOffset=0;}
            if(manages&&theme.Action(new Rect(Left+180,346,220,34),"APPLICATIONS",clanApplications)){clanApplications=true;memberOffset=0;}
            var rows=(clanApplications?state.applications:clan.members)??NoPlayers;
            memberOffset=Mathf.Min(memberOffset,Mathf.Max(0,rows.Length-1)/6*6);
            int count=Mathf.Min(6,Mathf.Max(0,rows.Length-memberOffset));
            for(int i=0;i<count;i++)
            {
                var p=rows[memberOffset+i];float y=390+i*38;
                theme.Panel(new Rect(Left,y,ContentWidth,36));
                theme.FitLabel(new Rect(Left+12,y,ContentWidth-510,36),$"{p.name}  /  LV {p.level}  /  {p.role}",theme.Small);
                if(clanApplications)
                {
                    SocialButton(new Rect(Right-252,y,120,36),"ACCEPT","/social/clan/accept",p.playerId);
                    SocialButton(new Rect(Right-124,y,120,36),"DECLINE","/social/clan/reject",p.playerId);
                }
                else if(p.playerId!=state.me?.playerId)
                {
                    if(state.role=="leader")
                    {
                        SocialButton(new Rect(Right-488,y,140,36),p.role=="officer"?"DEMOTE":"OFFICER","/social/clan/role",p.playerId,role:p.role=="officer"?"member":"officer");
                        if(theme.Action(new Rect(Right-340,y,160,36),"MAKE LEADER",enabled:!service.Busy))
                            ConfirmAction("/social/clan/transfer",p.playerId,"Transfer clan leadership to "+p.name+"?");
                    }
                    if(manages&&p.role!="leader"&&(state.role=="leader"||p.role=="member"))
                        if(theme.Action(new Rect(Right-172,y,168,36),"REMOVE",enabled:!service.Busy))
                            ConfirmAction("/social/clan/kick",p.playerId,"Remove "+p.name+" from your clan?");
                }
            }
            DrawSocialPager(memberOffset,count,offset=>memberOffset=offset,rows.Length,pageSize:6);
        }
    }
}
