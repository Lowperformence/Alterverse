using System;
using Alterverse.Profile;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed partial class MainMenuController
    {
        private const int PlayerRankingLimit=100;
        int rankingTab=1,clanTab,memberOffset;
        int requestedRankingTab=-1;
        string rankingAccount;
        long rankingProfileRevision=-1;
        float rankingRefreshAt;
        string socialIdentity,lastClanIdentity;
        bool clanApplications,clanOpen=true,founderTermsAccepted;
        int clanDetailTab,clanIconIndex,clanDescriptionIndex;
        string clanQuery="";
        string confirmRoute,confirmPlayer,confirmLabel;
        string clanWarStatusMessage="";
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
                    socialIdentity=identity;lastClanIdentity=null;clanDetailTab=0;clanDescriptionIndex=0;confirmRoute=confirmPlayer=confirmLabel=null;
                    memberOffset=0;clanQuery="";ResetClanChatUI();founderTermsAccepted=false;
                }
                return true;
            }
            theme.Panel(new Rect(Left,272,ContentWidth,170));
            theme.Label(new Rect(Left+24,292,ContentWidth-48,48),"Connect your account to load rankings and clans.",theme.Heading);
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
                    LoadRankingPage(service,RankingOffset());
                else if(service.State==null)service.Load("/social/state");
                else if(page==Page.Clans&&clanTab==0)
                    service.Load("/social/clans",new SocialService.Request{query=clanQuery,offset=service.Clans?.offset??0});
                else service.Load("/social/state");
            }
        }
        int RankingOffset()=>rankingTab==1?SocialService.Instance?.Levels?.offset??0:SocialService.Instance?.ClanLevels?.offset??0;
        void LoadRankingPage(SocialService service,int offset)
        {
            if(service.Busy)return;
            if(rankingTab==1)offset=Mathf.Clamp(offset,0,PlayerRankingLimit-4);
            requestedRankingTab=rankingTab;
            rankingAccount=PaidShardCommerce.Instance?.AccountHash;
            rankingProfileRevision=AccountProfilePrefs.Revision;
            rankingRefreshAt=Time.unscaledTime+30f;
            service.Load(rankingTab==1?"/social/levels":"/social/clan-levels",
                new SocialService.Request{offset=offset});
        }
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
            if(rankingTab<1||rankingTab>2)rankingTab=1;
            Heading(rankingTab==1?"Player Levels":"Clan Levels",rankingTab==1?"TOP 100 PLAYERS BY LEVEL AND EXPERIENCE.":"Clans ranked by the sum of every member's player level.");
            string[] tabs={"PLAYER LEVELS","CLAN LEVELS"};
            for(int i=0;i<tabs.Length;i++)
            {
                int tabValue=i+1;
                if(theme.Action(new Rect(Left+i*220,202,208,38),tabs[i],selected:rankingTab==tabValue))
                {
                    rankingTab=tabValue;
                    requestedRankingTab=-1;
                }
            }
            if(!SocialReady()){SocialFooter();return true;}
            var service=SocialService.Instance;
            // A tab click during another request used to be silently dropped.
            // Retry once idle, refresh saved progress and keep visible lists current.
            bool changed=requestedRankingTab!=rankingTab||rankingAccount!=PaidShardCommerce.Instance?.AccountHash;
            if(!service.Busy&&(changed||rankingProfileRevision!=AccountProfilePrefs.Revision||
                Time.unscaledTime>=rankingRefreshAt))
                LoadRankingPage(service,changed?0:RankingOffset());
            var result=rankingTab==1?service.Levels:service.ClanLevels;
            if(result==null)
            {
                theme.Label(new Rect(Left,268,ContentWidth,36),service.Busy?
                    "Loading the global ranking…":"Tap REFRESH to load the global ranking.",theme.Body,true);
                SocialFooter();return true;
            }
            theme.FitLabel(new Rect(Left,256,ContentWidth,32),rankingTab==1?
                $"YOUR PLACE  #{result.selfRank}  /  LEVEL {result.me?.level??PlayerExperience.Level}":
                result.selfRank>0?$"YOUR CLAN  #{result.selfRank}":"Join a clan to appear in the clan ranking.",theme.Heading);
            theme.Label(new Rect(Left,292,ContentWidth,22),"PLAYER LEVELS ARE SAVED GAME PROGRESS  /  ALL-TIME",theme.Small,true);
            int count;
            if(rankingTab==1)
            {
                var rows=result.players??NoPlayers;
                count=Mathf.Min(rows.Length,Mathf.Max(0,PlayerRankingLimit-result.offset));
                for(int i=0;i<count;i++)
                {
                    var p=rows[i];float y=322+i*37;
                    theme.RankingPanel(new Rect(Left,y,ContentWidth,34),p.rank);
                    theme.RankingBadge(new Rect(Left+10,y+4,96,26),p.rank);
                    theme.FitLabel(new Rect(Left+118,y,ContentWidth-444,34),p.name,theme.Body);
                    theme.FitLabel(new Rect(Right-310,y,290,34),$"LEVEL {p.level}  /  {p.xp:N0} XP",theme.Small);
                }
            }
            else
            {
                var rows=result.clans??NoClans;count=rows.Length;
                string myClanId=service.State?.clan?.clanId;
                for(int i=0;i<rows.Length;i++)
                {
                    var c=rows[i];float y=322+i*37;
                    bool atWar=ClanWarAndXpService.IsAtWarWithClan(c.clanId);
                    int bonusLvl=ClanWarAndXpService.GetClanBonusLevel(c.clanId);
                    theme.RankingPanel(new Rect(Left,y,ContentWidth,34),c.rank);
                    theme.RankingBadge(new Rect(Left+10,y+4,96,26),c.rank);
                    AvatarBadgeRenderer.Draw(new Rect(Left+118,y+2,30,30),Mathf.Max(0,c.avatar),Texture2D.whiteTexture,Time.unscaledTime);
                    theme.FitLabel(new Rect(Left+160,y,ContentWidth-640,34),(atWar?"☠ ":"")+$"[{c.tag}] {c.name}",theme.Body);
                    theme.FitLabel(new Rect(Right-470,y,310,34),$"TOTAL {(c.totalLevel+bonusLvl):N0}  /  {c.memberCount}/50 MEMBERS",theme.Small);
                    if(!string.IsNullOrEmpty(myClanId)&&c.clanId!=myClanId)
                    {
                        bool canDeclare=atWar||ClanWarAndXpService.ActiveWarCount<ClanWarAndXpService.MaxEnemyClans;
                        if(theme.Action(new Rect(Right-152,y+2,144,30),atWar?"END WAR":"☠ WAR",selected:atWar,enabled:canDeclare))
                        {
                            if(atWar)ClanWarAndXpService.EndWar(c.clanId,out clanWarStatusMessage);
                            else ClanWarAndXpService.DeclareWar(c.clanId,c.name,c.tag,out clanWarStatusMessage);
                        }
                    }
                }
            }
            int visibleTotal=rankingTab==1?Mathf.Min(result.total,PlayerRankingLimit):result.total;
            DrawSocialPager(result.offset,count,offset=>LoadRankingPage(service,offset),visibleTotal);
            SocialFooter();return true;
        }


        void DrawClans()
        {
            Heading("Clans","Build your crew. Grow together. Up to 50 members per clan.");
            string[] tabs=SocialService.Instance?.State?.isChatAdmin==true?new[]{"FIND CLANS","MY CLAN","CREATE CLAN","ADMIN"}:new[]{"FIND CLANS","MY CLAN","CREATE CLAN"};
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
                lastClanIdentity=currentClan;ResetClanChatUI();memberOffset=0;clanDetailTab=0;clanDescriptionIndex=0;
                if(clanTab!=3)clanTab=currentClan!=null?1:0;
            }
            if(clanTab==0)DrawClanBrowser(service,state);
            else if(clanTab==1)DrawMyClan(service,state);
            else if(clanTab==2)DrawCreateClan(service,state);
            else DrawChatAdministration(service);
            SocialFooter();
        }
        void DrawClanBrowser(SocialService service,SocialService.Snapshot state)
        {
            string browserInfo=!string.IsNullOrEmpty(clanWarStatusMessage)?clanWarStatusMessage:
                state.clan!=null?$"ACTIVE CLAN WARS: {ClanWarAndXpService.ActiveWarCount} / {ClanWarAndXpService.MaxEnemyClans}  •  ☠ ENEMY CLAN KILLS IN ONLINE MATCHES GRANT +1 SHARD":
                "Browse clans below. Names are assigned automatically.";
            theme.Label(new Rect(Left,270,ContentWidth,40),browserInfo,theme.Small,true);
            var rows=service.Clans?.clans??NoClans;
            int count=Mathf.Min(6,rows.Length);
            for(int i=0;i<count;i++)
            {
                var c=rows[i];float y=326+i*48;
                bool isOwnClan=state.clan!=null&&state.clan.clanId==c.clanId;
                bool atWar=ClanWarAndXpService.IsAtWarWithClan(c.clanId);
                int bonusLvl=ClanWarAndXpService.GetClanBonusLevel(c.clanId);
                theme.Panel(new Rect(Left,y,ContentWidth,44));
                AvatarBadgeRenderer.Draw(new Rect(Left+10,y+6,32,32),Mathf.Max(0,c.avatar),Texture2D.whiteTexture,Time.unscaledTime);
                theme.FitLabel(new Rect(Left+56,y+2,ContentWidth-442,24),(atWar?"☠ ":"")+$"[{c.tag}] {c.name}",theme.Body);
                theme.FitLabel(new Rect(Left+56,y+25,ContentWidth-442,17),$"{c.memberCount} / 50 MEMBERS  •  TOTAL LEVEL {(c.totalLevel+bonusLvl):N0}"+(atWar?"  •  ☠ AT WAR":""),theme.Small,true);
                theme.StatusBadge(new Rect(Right-276,y+9,104,26),atWar?"☠ WAR":c.open?"OPEN":"APPLY",atWar?FrontEndTheme.Danger:c.open?FrontEndTheme.Positive:FrontEndTheme.Accent);
                if(state.clan!=null&&!isOwnClan)
                {
                    bool canDeclare=atWar||ClanWarAndXpService.ActiveWarCount<ClanWarAndXpService.MaxEnemyClans;
                    string warBtnLabel=atWar?"END WAR":canDeclare?"☠ DECLARE WAR":"MAX 5 WARS";
                    if(theme.Action(new Rect(Right-160,y+4,156,36),warBtnLabel,selected:atWar,enabled:canDeclare))
                    {
                        if(atWar)ClanWarAndXpService.EndWar(c.clanId,out clanWarStatusMessage);
                        else ClanWarAndXpService.DeclareWar(c.clanId,c.name,c.tag,out clanWarStatusMessage);
                    }
                }
                else
                {
                    bool pending=Array.IndexOf(state.pendingClans??Array.Empty<string>(),c.clanId)>=0;
                    SocialButton(new Rect(Right-160,y+4,156,36),isOwnClan?"YOUR CLAN":pending?"CANCEL REQUEST":c.open?"JOIN":"APPLY",
                        pending?"/social/clan/cancel":"/social/clan/join",clanId:c.clanId,enabled:state.clan==null&&(pending||c.memberCount<50));
                }
            }
            if(count==0)theme.EmptyState(new Rect(Left,336,ContentWidth,134),"Find your crew","Browse the clan directory or create your own.");
            DrawSocialPager(service.Clans?.offset??0,count,offset=>{service.Load("/social/clans",new SocialService.Request{query=clanQuery,offset=offset});},pageSize:6);
        }
        void DrawGeneratedClanFields(bool enabled)
        {
            // The server assigns names and tags at creation, never the client.
            theme.Label(new Rect(Left,276,170,36),"CLAN NAME",theme.Small);
            theme.FitLabel(new Rect(Left+180,270,ContentWidth-210,42),
                "AUTOMATICALLY GENERATED",theme.Body);
            theme.Label(new Rect(Left,334,170,36),"CLAN TAG",theme.Small);
            theme.Label(new Rect(Left+180,328,ContentWidth-180,42),
                "AUTOMATICALLY GENERATED",theme.Body);
            theme.Label(new Rect(Left,392,170,36),"DESCRIPTION",theme.Small);
            theme.Label(new Rect(Left+180,384,ContentWidth-180,60),
                SafeIdentityCatalog.Descriptions[0],theme.Body,true);
            theme.Label(new Rect(Left+180,450,ContentWidth-180,36),
                "Generated identities keep the clan directory safe.",theme.Small,true);
        }
        void DrawCreateClan(SocialService service,SocialService.Snapshot state)
        {
            if(state.clan!=null){theme.Label(new Rect(Left,284,ContentWidth,48),"You already belong to a clan. Open MY CLAN.",theme.Heading);return;}
            var pending=service.PendingClanCreation;
            if(pending!=null)
            {
                // Retain the original request for receipt confirmation, but never
                // render free text from a legacy request pending on this device.
                clanDescriptionIndex=0;
                clanOpen=pending.open;
            }
            theme.Panel(new Rect(Left-8,262,ContentWidth+16,402));
            theme.Label(new Rect(Left,270,ContentWidth,26),"Generated clan name and tag  /  50 members  /  500 Shards",theme.Small,true);
            theme.Label(new Rect(Left,302,ContentWidth,30),SafeIdentityCatalog.Descriptions[0],theme.Body,true);
            if(theme.Action(new Rect(Left,342,340,38),clanOpen?"JOIN POLICY: OPEN":"JOIN POLICY: APPLICATION",enabled:pending==null&&!service.Busy))clanOpen=!clanOpen;
            if(state.chatEnabled)
            {
                DrawClanRulesText(new Rect(Left,386,ContentWidth,126),state.leaderRules,ref leaderRulesScroll);
                founderTermsAccepted=DrawClanConsent(new Rect(Left,522,ContentWidth,56),founderTermsAccepted,
                    "I have read and accept the clan moderation agreement.",!service.Busy);
            }
            theme.Label(new Rect(Left,582,ContentWidth,24),pending!=null?"Pending request: retry to confirm. You will not be charged twice.":"The server assigns a unique clan identity.",theme.Small,true);
            bool supported=state.clanCreationCost==500&&state.clanProtocolVersion>=(state.chatEnabled?4:3);
            if(ShardPurchaseAction(new Rect(Left,614,300,46),service.HasPendingClanCreation?"RETRY CLAN CREATION":"CREATE  /  500 SHARDS",service.HasPendingClanCreation?0:500,true,enabled:!service.Busy&&supported&&(!state.chatEnabled||founderTermsAccepted)))
                service.CreateClan(new SocialService.Request{descriptionId=0,open=clanOpen,accepted=founderTermsAccepted,consentVersion=state.leaderRulesVersion});
            if(!supported)theme.Label(new Rect(Left+320,614,ContentWidth-320,46),"Update the account server to create clans.",theme.Small,true);
            else if(PlayerWallet.Balance<500&&!service.HasPendingClanCreation)theme.Label(new Rect(Left+320,614,ContentWidth-320,46),$"You need {500-PlayerWallet.Balance} more Shards.",theme.Small,true);
        }

        void DrawMyClan(SocialService service,SocialService.Snapshot state)
        {
            var clan=state.clan;
            if(clan==null){theme.EmptyState(new Rect(Left,272,ContentWidth,154),"Your clan starts here","Join a crew from FIND CLANS, or create a clan for 500 Shards.");return;}
            if(clanDetailTab==2&&state.chatEnabled&&(!state.chatAccepted||showLeaderTerms||state.role=="leader"&&!state.leaderAccepted))
            {DrawChatConsent(service,state,state.chatAccepted&&(showLeaderTerms||state.role=="leader"&&!state.leaderAccepted));return;}
            ClanWarAndXpService.GetClanXpProgress(clan.clanId,out int totalClanXp,out int clanBonusLvl,out int xpIntoLvl,out int xpNeedLvl);
            int remainingDonations=ClanWarAndXpService.RemainingDonationsToday();
            theme.Panel(new Rect(Left-8,262,ContentWidth+16,78));
            AvatarBadgeRenderer.Draw(new Rect(Left,268,66,66),Mathf.Max(0,clan.avatar),Texture2D.whiteTexture,Time.unscaledTime);
            theme.FitLabel(new Rect(Left+80,268,ContentWidth-518,30),$"[{clan.tag}] {clan.name}",theme.Heading);
            if(ShardPurchaseAction(new Rect(Right-436,270,250,38),
                remainingDonations>0?$"BUY CLAN XP / {ClanWarAndXpService.DonationShardCost} SHARDS":"CLAN XP LIMIT REACHED",
                ClanWarAndXpService.DonationShardCost,true,
                enabled:!service.Busy&&remainingDonations>0&&clanBonusLvl<ClanWarAndXpService.MaxClanBonusLevel))
            {
                ClanWarAndXpService.TryDonateShardsForClanXp(clan.clanId,out clanWarStatusMessage);
            }
            if(theme.Action(new Rect(Right-178,270,178,38),"LEAVE CLAN",enabled:!service.Busy))
                ConfirmAction("/social/clan/leave",null,clan.memberCount==1?"Leave and delete your empty clan?":"Leave this clan? Leaders must transfer leadership first.");
            theme.FitLabel(new Rect(Left+80,299,ContentWidth-270,18),
                $"{clan.memberCount} / 50 MEMBERS  •  TOTAL LEVEL {(clan.totalLevel+clanBonusLvl):N0} (BONUS +{clanBonusLvl})  •  CLAN XP {xpIntoLvl}/{xpNeedLvl}  •  {state.role?.ToUpperInvariant()}",theme.Small,true);
            theme.FitLabel(new Rect(Left+80,317,ContentWidth-80,20),
                !string.IsNullOrEmpty(clanWarStatusMessage)?clanWarStatusMessage:
                $"{clan.description}  •  +{ClanWarAndXpService.DonationClanXpGain} CLAN XP / {ClanWarAndXpService.DonationShardCost} SHARDS ({ClanWarAndXpService.DonationsUsedToday()}/{ClanWarAndXpService.MaxDailyDonations} TODAY)",theme.Small,true);
            bool manages=state.role=="leader"||state.role=="officer";
            if(!manages)clanApplications=false;
            if(state.role!="leader"&&clanDetailTab==3)clanDetailTab=0;
            if(theme.Action(new Rect(Left,346,150,34),"MEMBERS",selected:clanDetailTab==0)){clanDetailTab=0;clanApplications=false;memberOffset=0;}
            if(manages&&theme.Action(new Rect(Left+158,346,190,34),"APPLICATIONS",selected:clanDetailTab==1)){clanDetailTab=1;clanApplications=true;memberOffset=0;}
            if(state.role=="leader"&&theme.Action(new Rect(Left+356,346,166,34),"CLAN IMAGE",selected:clanDetailTab==3))
            {clanDetailTab=3;clanIconIndex=0;}
            if(state.chatEnabled&&theme.Action(new Rect(Left+530,346,118,34),"CHAT",selected:clanDetailTab==2))clanDetailTab=2;
            if(theme.Action(new Rect(Left+656,346,184,34),$"☠ WARS ({ClanWarAndXpService.ActiveWarCount}/{ClanWarAndXpService.MaxEnemyClans})",selected:clanDetailTab==4))clanDetailTab=4;
            if(clanDetailTab==2){DrawClanChat(service,state);return;}
            if(clanDetailTab==3){DrawClanImage(service,state);return;}
            if(clanDetailTab==4){DrawClanWars(service);return;}
            if(selectedChatPlayer!=null){DrawChatMemberActions(service,state);return;}
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
                    if(state.chatEnabled&&theme.Action(new Rect(Right-488,y,310,36),"PLAYER ACTIONS",enabled:!service.Busy))
                    {selectedChatPlayer=p;return;}
                    if(state.role=="leader"&&!state.chatEnabled)
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
            var icons=AvatarCollection.OwnedIds();
            if(icons.Count==0)
            {
                theme.Label(new Rect(Left,402,ContentWidth,58),"No owned avatars are available yet.",theme.Heading);
                if(theme.Action(new Rect(Left,480,260,48),"BROWSE AVATARS"))OpenAvatarStore();
                return;
            }
            clanIconIndex=Mathf.Clamp(clanIconIndex,0,icons.Count-1);
            int id=icons[clanIconIndex];
            AvatarBadgeRenderer.Draw(new Rect(Left+24,400,160,160),id,Texture2D.whiteTexture,Time.unscaledTime);
            theme.Label(new Rect(Left+220,406,ContentWidth-220,42),AvatarPresentation.Name(id),theme.Heading);
            theme.Label(new Rect(Left+220,460,ContentWidth-220,42),"Choose any owned avatar, including starter and reward images. Only the leader can change it.",theme.Body,true);
            if(theme.Action(new Rect(Left+220,528,72,44),"<"))clanIconIndex=(clanIconIndex-1+icons.Count)%icons.Count;
            theme.Center(new Rect(Left+304,528,150,44),$"{clanIconIndex+1} / {icons.Count}");
            if(theme.Action(new Rect(Left+466,528,72,44),">"))clanIconIndex=(clanIconIndex+1)%icons.Count;
            if(theme.Action(new Rect(Left,590,300,48),state.clan.avatar==id?"SELECTED":"SET CLAN IMAGE",true,enabled:!service.Busy&&state.clan.avatar!=id))
                service.Load("/social/clan/icon",new SocialService.Request{avatar=id});
            if(theme.Action(new Rect(Left+316,590,260,48),"BROWSE AVATARS"))OpenAvatarStore();
        }

        void DrawClanWars(SocialService service)
        {
            var wars=ClanWarAndXpService.GetEnemyClans();
            theme.Label(new Rect(Left,390,ContentWidth,28),
                $"ACTIVE CLAN WARS ({wars.Count} / {ClanWarAndXpService.MaxEnemyClans})  •  ☠ ENEMY CLAN PLAYERS SHOW A SKULL & GRANT +1 SHARD PER KILL IN ONLINE MATCHES",
                theme.Small,true);
            for(int i=0;i<wars.Count;i++)
            {
                var enemy=wars[i];
                float y=424+i*42;
                theme.Panel(new Rect(Left,y,ContentWidth,38));
                theme.FitLabel(new Rect(Left+14,y+6,ContentWidth-210,26),$"☠ [{enemy.tag}] {enemy.name}",theme.Body);
                if(theme.Action(new Rect(Right-176,y+3,168,32),"END WAR",true))
                {
                    ClanWarAndXpService.EndWar(enemy.clanId,out clanWarStatusMessage);
                    break;
                }
            }
            if(wars.Count==0)
            {
                theme.EmptyState(new Rect(Left,428,ContentWidth,130),
                    "No active clan wars",
                    "Declare war on up to 5 clans in FIND CLANS or CLAN LEVELS. Enemy clan players are marked with a skull in online matches and grant +1 Shard per kill.");
            }
            if(theme.Action(new Rect(Left,640,260,36),"FIND CLANS TO DECLARE WAR",true))
            {
                clanTab=0;
                service?.Load("/social/clans");
            }
        }
    }
}
