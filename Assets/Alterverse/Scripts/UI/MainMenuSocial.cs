using System;
using Alterverse.Profile;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed partial class MainMenuController
    {
        int rankingTab,friendTab,friendOffset,clanTab,memberOffset;
        string socialIdentity,lastClanIdentity;
        bool clanApplications,clanOpen=true;
        int clanDetailTab,clanIconIndex,clanChatOffset=int.MaxValue;
        string clanChatText="",clanPendingMessageId,clanPendingMessageText;
        GUIStyle clanMessageStyle;
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
                    socialIdentity=identity;lastClanIdentity=null;clanDetailTab=0;clanChatOffset=int.MaxValue;clanChatText="";clanPendingMessageId=clanPendingMessageText=null;confirmRoute=confirmPlayer=confirmLabel=null;
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
                if(theme.Action(new Rect(Left+i*210,202,198,36),tabs[i],selected:rankingTab==i))
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
                    AvatarBadgeRenderer.Draw(new Rect(Left+10,y+2,30,30),Mathf.Max(0,c.avatar),Texture2D.whiteTexture,Time.unscaledTime);
                    theme.FitLabel(new Rect(Left+52,y,ContentWidth-418,34),$"#{c.rank}  [{c.tag}] {c.name}",theme.Body);
                    theme.FitLabel(new Rect(Right-350,y,330,34),$"TOTAL {c.totalLevel:N0}  /  {c.memberCount}/50 MEMBERS",theme.Small);
                }
            }
            DrawSocialPager(result.offset,count,offset=>service.Load(rankingTab==1?"/social/levels":"/social/clan-levels",new SocialService.Request{offset=offset}),result.total);
            SocialFooter();return true;
        }

        void DrawFriends()
        {
            Heading("Friends","Your crew, requests and new connections — all in one place.");
            string[] tabs={"FRIENDS","REQUESTS","SENT","BLOCKED","FIND PLAYERS"};
            float tabWidth=(ContentWidth-32f)/tabs.Length;
            for(int i=0;i<tabs.Length;i++)
                if(theme.Action(new Rect(Left+i*(tabWidth+8),212,tabWidth,40),tabs[i],selected:friendTab==i))
                {friendTab=i;friendOffset=0;confirmRoute=null;}
            if(!SocialReady()){SocialFooter();return;}
            var service=SocialService.Instance;
            var state=service.State;
            if(DrawSocialConfirmation())return;
            if(state==null){SocialFooter();return;}
            theme.Panel(new Rect(Left,262,ContentWidth,36));
            theme.FitLabel(new Rect(Left+14,262,ContentWidth-168,36),"PLAYER ID  "+state.me?.playerId,theme.Small,true);
            if(theme.Action(new Rect(Right-138,264,132,32),"COPY ID",enabled:!string.IsNullOrEmpty(state.me?.playerId)))
                GUIUtility.systemCopyBuffer=state.me.playerId;
            SocialService.Player[] rows;
            const int friendPageSize=6;
            friendOffset=Mathf.Min(friendOffset,Mathf.Max(0,((friendTab==0?state.friends:friendTab==1?state.incoming:friendTab==2?state.outgoing:state.blocked)?.Length??0)-1)/friendPageSize*friendPageSize);
            int start=friendOffset;
            if(friendTab==4)
            {
                playerQuery=GUI.TextField(new Rect(Left,306,ContentWidth-190,40),playerQuery,40,theme.Field);
                if(theme.Action(new Rect(Right-178,306,178,40),"SEARCH",enabled:!service.Busy&&playerQuery.Trim().Length>=2))
                {service.Load("/social/players",new SocialService.Request{query=playerQuery});}
                rows=service.Search?.players??NoPlayers;start=0;
            }
            else rows=(friendTab==0?state.friends:friendTab==1?state.incoming:friendTab==2?state.outgoing:state.blocked)??NoPlayers;
            int count=Mathf.Min(friendPageSize,Mathf.Max(0,rows.Length-start));
            for(int i=0;i<count;i++)
            {
                var p=rows[start+i];float y=(friendTab==4?354:308)+i*44;
                theme.Panel(new Rect(Left,y,ContentWidth,42));
                theme.IdentityTile(new Rect(Left+8,y+5,32,32),p.name,p.playerId==state.me?.playerId);
                theme.FitLabel(new Rect(Left+52,y+3,ContentWidth-330,21),p.name,theme.Body);
                theme.FitLabel(new Rect(Left+52,y+23,ContentWidth-330,17),$"LEVEL {p.level}  •  {p.playerId}",theme.Small,true);
                if(friendTab==0)
                {
                    SocialButton(new Rect(Right-252,y+4,120,34),"REMOVE","/social/friend/remove",p.playerId);
                    SocialButton(new Rect(Right-124,y+4,120,34),"BLOCK","/social/block",p.playerId);
                }
                else if(friendTab==1)
                {
                    SocialButton(new Rect(Right-252,y+4,120,34),"ACCEPT","/social/friend/accept",p.playerId);
                    SocialButton(new Rect(Right-124,y+4,120,34),"DECLINE","/social/friend/remove",p.playerId);
                }
                else if(friendTab==2)SocialButton(new Rect(Right-124,y+4,120,34),"CANCEL","/social/friend/remove",p.playerId);
                else if(friendTab==3)SocialButton(new Rect(Right-124,y+4,120,34),"UNBLOCK","/social/unblock",p.playerId);
                else
                {
                    bool friends=Array.Exists(state.friends??NoPlayers,x=>x.playerId==p.playerId);
                    bool sent=Array.Exists(state.outgoing??NoPlayers,x=>x.playerId==p.playerId);
                    bool incoming=Array.Exists(state.incoming??NoPlayers,x=>x.playerId==p.playerId);
                    SocialButton(new Rect(Right-252,y+4,120,34),friends?"FRIENDS":sent?"SENT":incoming?"ACCEPT":"ADD",
                        incoming?"/social/friend/accept":"/social/friend/request",p.playerId,enabled:!friends&&!sent);
                    SocialButton(new Rect(Right-124,y+4,120,34),"BLOCK","/social/block",p.playerId);
                }
            }
            if(friendTab==4)
                DrawSocialPager(service.Search?.offset??0,count,offset=>{service.Load("/social/players",new SocialService.Request{query=playerQuery,offset=offset});},pageSize:friendPageSize);
            else DrawSocialPager(friendOffset,count,offset=>friendOffset=offset,rows.Length,pageSize:friendPageSize);
            if(count==0)theme.EmptyState(new Rect(Left,364,ContentWidth,132),friendTab==4?"Find your next teammate":"No players here yet",
                friendTab==4?"Search by name or paste a player ID to connect.":friendTab==0?"Open FIND PLAYERS to send your first friend request.":"New requests and players will appear in this list.");
            SocialFooter();
        }

        void DrawClans()
        {
            Heading("Clans","Build your crew. Grow together. Up to 50 members per clan.");
            string[] tabs={"FIND CLANS","MY CLAN","CREATE CLAN"};
            for(int i=0;i<tabs.Length;i++)
                if(theme.Action(new Rect(Left+i*210,212,198,40),tabs[i],selected:clanTab==i))
                {clanTab=i;memberOffset=0;confirmRoute=null;if(i==0)SocialService.Instance?.Load("/social/clans");}
            if(!SocialReady()){SocialFooter();return;}
            if(DrawSocialConfirmation())return;
            var service=SocialService.Instance;var state=service.State;
            if(state==null){SocialFooter();return;}
            string currentClan=state.clan?.clanId;
            if(currentClan!=lastClanIdentity)
            {
                lastClanIdentity=currentClan;memberOffset=0;clanDetailTab=0;clanChatOffset=int.MaxValue;clanChatText="";clanPendingMessageId=clanPendingMessageText=null;
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
            int count=Mathf.Min(6,rows.Length);
            for(int i=0;i<count;i++)
            {
                var c=rows[i];float y=326+i*48;
                theme.Panel(new Rect(Left,y,ContentWidth,44));
                AvatarBadgeRenderer.Draw(new Rect(Left+10,y+6,32,32),Mathf.Max(0,c.avatar),Texture2D.whiteTexture,Time.unscaledTime);
                theme.FitLabel(new Rect(Left+56,y+2,ContentWidth-442,24),$"[{c.tag}] {c.name}",theme.Body);
                theme.FitLabel(new Rect(Left+56,y+25,ContentWidth-442,17),$"{c.memberCount} / 50 MEMBERS  •  TOTAL LEVEL {c.totalLevel:N0}",theme.Small,true);
                theme.StatusBadge(new Rect(Right-276,y+9,104,26),c.open?"OPEN":"APPLY",c.open?FrontEndTheme.Positive:FrontEndTheme.Accent);
                bool pending=Array.IndexOf(state.pendingClans??Array.Empty<string>(),c.clanId)>=0;
                SocialButton(new Rect(Right-160,y+4,156,36),pending?"CANCEL REQUEST":c.open?"JOIN":"APPLY",
                    pending?"/social/clan/cancel":"/social/clan/join",clanId:c.clanId,enabled:state.clan==null&&(pending||c.memberCount<50));
            }
            if(count==0)theme.EmptyState(new Rect(Left,336,ContentWidth,134),"Find your crew","Search for a clan or open CREATE CLAN to build your own.");
            DrawSocialPager(service.Clans?.offset??0,count,offset=>{service.Load("/social/clans",new SocialService.Request{query=clanQuery,offset=offset});},pageSize:6);
        }
        void DrawCreateClan(SocialService service,SocialService.Snapshot state)
        {
            if(state.clan!=null){theme.Label(new Rect(Left,284,ContentWidth,48),"You already belong to a clan. Open MY CLAN.",theme.Heading);return;}
            var pending=service.PendingClanCreation;
            if(pending!=null){clanName=pending.name??"";clanTag=pending.tag??"";clanDescription=pending.description??"";clanOpen=pending.open;}
            bool previousEnabled=GUI.enabled;
            theme.Panel(new Rect(Left-8,262,ContentWidth+16,390));
            GUI.enabled=previousEnabled&&pending==null&&!service.Busy;
            theme.Label(new Rect(Left,276,220,36),"NAME (3–24)",theme.Small);
            clanName=GUI.TextField(new Rect(Left+230,270,ContentWidth-230,42),clanName,24,theme.Field);
            theme.Label(new Rect(Left,334,220,36),"TAG (2–5)",theme.Small);
            clanTag=GUI.TextField(new Rect(Left+230,328,260,42),clanTag,5,theme.Field);
            theme.Label(new Rect(Left,392,220,36),"DESCRIPTION",theme.Small);
            clanDescription=GUI.TextField(new Rect(Left+230,386,ContentWidth-230,56),clanDescription,160,theme.Field);
            if(theme.Action(new Rect(Left,462,340,48),clanOpen?"JOIN POLICY: OPEN":"JOIN POLICY: APPLICATION"))clanOpen=!clanOpen;
            GUI.enabled=previousEnabled;
            theme.Label(new Rect(Left,526,ContentWidth,46),pending!=null?"Previous request pending. Retry to confirm its result; you will not be charged twice.":"Creating a clan costs 500 Shards. Names and tags must be unique. Maximum 50 members.",theme.Body,true);
            if(theme.Action(new Rect(Left,590,300,54),service.HasPendingClanCreation?"RETRY CLAN CREATION":"CREATE  /  500 SHARDS",true,enabled:!service.Busy&&state.clanCreationCost==500&&(service.HasPendingClanCreation||PlayerWallet.Balance>=500&&clanName.Trim().Length>=3&&clanTag.Length>=2)))
                service.CreateClan(new SocialService.Request{name=clanName,tag=clanTag,description=clanDescription,open=clanOpen});
            if(state.clanCreationCost!=500)theme.Label(new Rect(Left+320,590,ContentWidth-320,54),"Update the account server to enable clan creation.",theme.Small,true);
            else if(PlayerWallet.Balance<500&&!service.HasPendingClanCreation)theme.Label(new Rect(Left+320,590,ContentWidth-320,54),$"You need {500-PlayerWallet.Balance} more Shards.",theme.Small,true);
        }
        void DrawMyClan(SocialService service,SocialService.Snapshot state)
        {
            var clan=state.clan;
            if(clan==null){theme.EmptyState(new Rect(Left,272,ContentWidth,154),"Your clan starts here","Join a crew from FIND CLANS, or create a clan for 500 Shards.");return;}
            theme.Panel(new Rect(Left-8,262,ContentWidth+16,78));
            AvatarBadgeRenderer.Draw(new Rect(Left,268,66,66),Mathf.Max(0,clan.avatar),Texture2D.whiteTexture,Time.unscaledTime);
            theme.FitLabel(new Rect(Left+80,268,ContentWidth-270,30),$"[{clan.tag}] {clan.name}",theme.Heading);
            if(theme.Action(new Rect(Right-178,270,178,38),"LEAVE CLAN",enabled:!service.Busy))
                ConfirmAction("/social/clan/leave",null,clan.memberCount==1?"Leave and delete your empty clan?":"Leave this clan? Leaders must transfer leadership first.");
            theme.FitLabel(new Rect(Left+80,299,ContentWidth-270,18),$"{clan.memberCount} / 50 MEMBERS  •  TOTAL LEVEL {clan.totalLevel:N0}  •  {state.role?.ToUpperInvariant()}",theme.Small,true);
            theme.FitLabel(new Rect(Left+80,317,ContentWidth-80,20),clan.description,theme.Small,true);
            bool manages=state.role=="leader"||state.role=="officer";
            if(!manages)clanApplications=false;
            if(state.role!="leader"&&clanDetailTab==3)clanDetailTab=0;
            if(theme.Action(new Rect(Left,346,170,34),"MEMBERS",selected:clanDetailTab==0)){clanDetailTab=0;clanApplications=false;memberOffset=0;}
            if(manages&&theme.Action(new Rect(Left+180,346,220,34),"APPLICATIONS",selected:clanDetailTab==1)){clanDetailTab=1;clanApplications=true;memberOffset=0;}
            if(theme.Action(new Rect(Left+410,346,160,34),"CLAN CHAT",selected:clanDetailTab==2))
            {clanDetailTab=2;clanChatOffset=int.MaxValue;}
            if(state.role=="leader"&&theme.Action(new Rect(Left+580,346,190,34),"CLAN IMAGE",selected:clanDetailTab==3))
            {clanDetailTab=3;clanIconIndex=0;}
            if(clanDetailTab==2){DrawClanChat(service,state);return;}
            if(clanDetailTab==3){DrawClanImage(service,state);return;}
            var rows=(clanApplications?state.applications:clan.members)??NoPlayers;
            memberOffset=Mathf.Min(memberOffset,Mathf.Max(0,rows.Length-1)/5*5);
            int count=Mathf.Min(5,Mathf.Max(0,rows.Length-memberOffset));
            for(int i=0;i<count;i++)
            {
                var p=rows[memberOffset+i];float y=390+i*44;
                theme.Panel(new Rect(Left,y,ContentWidth,42));
                theme.FitLabel(new Rect(Left+14,y+2,ContentWidth-514,21),p.name,theme.Body);
                theme.FitLabel(new Rect(Left+14,y+23,ContentWidth-514,17),$"LEVEL {p.level}  •  {p.role?.ToUpperInvariant()}",theme.Small,true);
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
            DrawSocialPager(memberOffset,count,offset=>memberOffset=offset,rows.Length,pageSize:5);
            if(count==0)theme.EmptyState(new Rect(Left,390,ContentWidth,120),"No applications yet","Players who apply to your clan will appear here.");
        }
        void DrawClanImage(SocialService service,SocialService.Snapshot state)
        {
            var icons=new System.Collections.Generic.List<int>();
            foreach(var item in AvatarShopCatalog.Items)
                if(AvatarInventory.IsOwned(item.AvatarId))icons.Add(item.AvatarId);
            if(icons.Count==0)
            {
                theme.Label(new Rect(Left,402,ContentWidth,58),"Purchase an icon in the Store to use it as your clan image.",theme.Heading);
                if(theme.Action(new Rect(Left,480,260,48),"OPEN STORE"))SetPage(Page.Shop);
                return;
            }
            clanIconIndex=Mathf.Clamp(clanIconIndex,0,icons.Count-1);
            int id=icons[clanIconIndex];
            AvatarBadgeRenderer.Draw(new Rect(Left+24,400,160,160),id,Texture2D.whiteTexture,Time.unscaledTime);
            theme.Label(new Rect(Left+220,406,ContentWidth-220,42),AvatarPresentation.Name(id),theme.Heading);
            theme.Label(new Rect(Left+220,460,ContentWidth-220,42),"Choose one of your owned shop icons. Only the leader can change the clan image.",theme.Body,true);
            if(theme.Action(new Rect(Left+220,528,72,44),"<"))clanIconIndex=(clanIconIndex-1+icons.Count)%icons.Count;
            theme.Center(new Rect(Left+304,528,150,44),$"{clanIconIndex+1} / {icons.Count}");
            if(theme.Action(new Rect(Left+466,528,72,44),">"))clanIconIndex=(clanIconIndex+1)%icons.Count;
            if(theme.Action(new Rect(Left,590,300,48),state.clan.avatar==id?"SELECTED":"SET CLAN IMAGE",true,enabled:!service.Busy&&state.clan.avatar!=id))
                service.Load("/social/clan/icon",new SocialService.Request{avatar=id});
        }
        void DrawClanChat(SocialService service,SocialService.Snapshot state)
        {
            if(!string.IsNullOrEmpty(clanPendingMessageId)&&service.LastSentMessageId==clanPendingMessageId)
            {
                clanChatText="";clanPendingMessageId=clanPendingMessageText=null;
                clanChatOffset=int.MaxValue;
            }
            var messages=state.messages??Array.Empty<SocialService.Message>();
            clanChatOffset=Mathf.Clamp(clanChatOffset,0,Mathf.Max(0,messages.Length-1)/3*3);
            int count=Mathf.Min(3,Mathf.Max(0,messages.Length-clanChatOffset));
            clanMessageStyle??=new GUIStyle(theme.Small){richText=false,wordWrap=true};
            for(int i=0;i<count;i++)
            {
                var message=messages[clanChatOffset+i];float y=384+i*64;
                theme.Panel(new Rect(Left,y,ContentWidth,60));
                if(message.playerId==state.me?.playerId)theme.Frame(new Rect(Left,y,ContentWidth,60),true);
                string time=DateTimeOffset.FromUnixTimeMilliseconds(message.createdAt).ToLocalTime().ToString("HH:mm");
                theme.FitLabel(new Rect(Left+12,y,ContentWidth-126,20),message.name+"  /  "+time,theme.Small,true);
                GUI.Label(new Rect(Left+12,y+20,ContentWidth-138,38),message.text,clanMessageStyle);
                if((state.role=="leader"||message.playerId==state.me?.playerId)&&theme.Action(new Rect(Right-110,y+12,100,36),"DELETE",enabled:!service.Busy))
                    service.Load("/social/clan/message/remove",new SocialService.Request{messageId=message.messageId});
            }
            if(count==0)theme.EmptyState(new Rect(Left,390,ContentWidth,140),"Start the conversation","Write a message below to coordinate with your clan.");
            clanChatText=GUI.TextField(new Rect(Left,584,ContentWidth-166,42),clanChatText,240,theme.Field);
            if(theme.Action(new Rect(Right-154,584,154,42),"SEND",true,enabled:!service.Busy&&!string.IsNullOrWhiteSpace(clanChatText)))
            {
                string text=clanChatText.Trim();
                if(clanPendingMessageText!=text||string.IsNullOrEmpty(clanPendingMessageId))
                {clanPendingMessageId=Guid.NewGuid().ToString("N");clanPendingMessageText=text;}
                service.Load("/social/clan/message",new SocialService.Request{messageId=clanPendingMessageId,text=text});
            }
            DrawSocialPager(clanChatOffset,count,offset=>clanChatOffset=offset,messages.Length,pageSize:3);
        }
    }
}


