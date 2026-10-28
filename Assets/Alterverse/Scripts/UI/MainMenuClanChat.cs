using System;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.UI
{
    public sealed partial class MainMenuController
    {
        string chatDraft="",chatAgeBand,reportMessageId,reportPlayerId,pendingPlayerAction,chatPendingOperation,chatSubmittedText;
        bool chatTermsAccepted,leaderTermsAccepted,showLeaderTerms,showBlockedPlayers;
        int reportReasonIndex,blockedOffset;
        Vector2 chatScroll,adminScroll,chatRulesScroll,leaderRulesScroll;
        GUIStyle clanRulesText,clanConsentText;
        SocialService.Player selectedChatPlayer;
        SocialService.Report selectedChatReport;
        string lastChatMessageId;
        float lastChatHeight;

        void ResetClanChatUI()
        {
            chatDraft="";chatAgeBand=null;chatTermsAccepted=leaderTermsAccepted=false;
            chatPendingOperation=chatSubmittedText=null;
            reportMessageId=reportPlayerId=pendingPlayerAction=null;
            selectedChatPlayer=null;selectedChatReport=null;
            showLeaderTerms=showBlockedPlayers=false;chatScroll=adminScroll=Vector2.zero;
            chatRulesScroll=leaderRulesScroll=Vector2.zero;
            lastChatMessageId=null;lastChatHeight=0;
        }
        string ChatTime(long milliseconds)
        {
            try{return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToLocalTime().ToString("HH:mm");}
            catch(ArgumentOutOfRangeException){return "--:--";}
        }
        void ChatRequest(string route,SocialService.Request request)
        {SocialService.Instance?.Load(route,request);}
        bool ChatBlocked(SocialService.Snapshot state,string playerId)
        {
            foreach(var p in state.blocked??NoPlayers)if(p.playerId==playerId)return true;
            return false;
        }
        void DrawClanChat(SocialService service,SocialService.Snapshot state)
        {
            if(chatPendingOperation!=null&&service.SentOperationId==chatPendingOperation)
            {if(chatDraft==chatSubmittedText)chatDraft="";chatPendingOperation=chatSubmittedText=null;chatScroll.y=float.MaxValue;}
            if(!state.chatAccepted){DrawChatConsent(service,state,false);return;}
            if(showLeaderTerms||state.role=="leader"&&!state.leaderAccepted)
            {DrawChatConsent(service,state,true);return;}
            if(reportMessageId!=null||reportPlayerId!=null){DrawChatReport(service,state);return;}
            if(selectedChatPlayer!=null){DrawChatMemberActions(service,state);return;}
            if(showBlockedPlayers){DrawChatBlocks(service,state);return;}
            service.KeepChatOpen();
            if(theme.Action(new Rect(Left,386,160,30),"BLOCKED PLAYERS"))showBlockedPlayers=true;
            if(!state.leaderAccepted&&theme.Action(new Rect(Left+172,386,220,30),"LEADERSHIP TERMS"))showLeaderTerms=true;
            theme.FitLabel(new Rect(Left+410,386,ContentWidth-410,30),state.warnings>0?$"MODERATION WARNINGS: {state.warnings}":"PRIVATE CLAN ROOM",theme.Small,true);
            var messages=service.Chat?.messages??Array.Empty<SocialService.Message>();
            float width=ContentWidth-28,height=0;
            foreach(var message in messages)height+=Mathf.Max(36,theme.Body.CalcHeight(new GUIContent(message.text),width-24))+64;
            if(messages.Length>0&&lastChatMessageId!=messages[messages.Length-1].messageId)
            {
                if(lastChatMessageId==null||chatScroll.y>=lastChatHeight-180)chatScroll.y=float.MaxValue;
                lastChatMessageId=messages[messages.Length-1].messageId;
            }
            lastChatHeight=height;
            float scrollHeight=Mathf.Max(156,height);
            chatScroll=GUI.BeginScrollView(new Rect(Left,424,ContentWidth,162),chatScroll,new Rect(0,0,width,scrollHeight));
            float y=0;
            foreach(var message in messages)
            {
                float rowHeight=Mathf.Max(36,theme.Body.CalcHeight(new GUIContent(message.text),width-24))+64;
                theme.Panel(new Rect(0,y,width,rowHeight-4));
                theme.FitLabel(new Rect(12,y+4,width-24,22),message.name+"  /  "+ChatTime(message.createdAt),theme.Small,true);
                theme.Label(new Rect(12,y+28,width-24,rowHeight-64),message.text,theme.Body);
                if(message.playerId!=state.me?.playerId)
                {
                    if(theme.Action(new Rect(12,y+rowHeight-34,112,28),"REPORT",enabled:!service.Busy))
                    {reportMessageId=message.messageId;reportPlayerId=null;reportReasonIndex=0;}
                    if(theme.Action(new Rect(136,y+rowHeight-34,150,28),"PLAYER ACTIONS",enabled:!service.Busy))
                    {
                        selectedChatPlayer=Array.Find(state.clan.members??NoPlayers,p=>p.playerId==message.playerId)??
                            new SocialService.Player{playerId=message.playerId,name=message.name};
                    }
                }
                y+=rowHeight;
            }
            if(messages.Length==0)theme.Label(new Rect(12,10,width-24,80),service.Chat==null?service.ChatStatus:"No messages yet. Say hello to your clan.",theme.Body);
            if(service.Chat==null&&theme.Action(new Rect(12,100,260,48),service.Busy?"CONNECTING…":"RETRY CHAT",true,enabled:!service.Busy))
                ChatRequest("/social/chat/read",new SocialService.Request{clanId=state.clan.clanId});
            GUI.EndScrollView();
            bool muted=state.mutedUntil>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            bool wasEnabled=GUI.enabled;GUI.enabled=wasEnabled&&!muted&&!service.Busy&&service.Chat!=null;
            chatDraft=GUI.TextField(new Rect(Left,594,ContentWidth-166,42),chatDraft,Mathf.Max(1,state.maxMessageLength),theme.Field);
            GUI.enabled=wasEnabled;
            if(theme.Action(new Rect(Right-154,594,154,42),"SEND",true,enabled:!muted&&!service.Busy&&service.Chat!=null&&!string.IsNullOrWhiteSpace(chatDraft)))
            {
                if(chatPendingOperation==null||chatDraft!=chatSubmittedText)
                {chatPendingOperation=Guid.NewGuid().ToString("N").Substring(0,20);chatSubmittedText=chatDraft;}
                ChatRequest("/social/chat/send",new SocialService.Request{clanId=state.clan.clanId,text=chatDraft,operationId=chatPendingOperation});
            }
            theme.FitLabel(new Rect(Left,640,ContentWidth,22),muted?"You are muted until "+ChatTime(state.mutedUntil):$"{chatDraft.Length}/{state.maxMessageLength}  /  Protect your privacy. Report suspicious players.",theme.Small,true);
        }
        void DrawChatConsent(SocialService service,SocialService.Snapshot state,bool leader)
        {
            // Own the full content area: readable rules must not be squeezed below
            // the clan header. Long server-provided terms scroll without shrinking.
            theme.Panel(new Rect(Left-8,258,ContentWidth+16,408));
            theme.Label(new Rect(Left+16,270,ContentWidth-224,36),leader?"CLAN MODERATION AGREEMENT":"CHAT RULES",theme.Heading);
            if(theme.Action(new Rect(Right-184,270,168,42),"BACK TO CLAN"))
            {clanDetailTab=0;showLeaderTerms=false;return;}
            theme.Label(new Rect(Left+16,314,ContentWidth-32,40),leader?
                "As clan leader, you moderate your clan chat. Please read the agreement.":
                "Choose your age group once. We store the group, not your exact age or date of birth.",theme.Body);
            if(leader)
            {
                DrawClanRulesText(new Rect(Left+16,360,ContentWidth-32,176),state.leaderRules,ref leaderRulesScroll);
                leaderTermsAccepted=DrawClanConsent(new Rect(Left+16,548,ContentWidth-32,56),leaderTermsAccepted,
                    "I have read and accept the clan moderation agreement.",!service.Busy);
                if(theme.Action(new Rect(Left+16,616,300,44),"ACCEPT TERMS",true,enabled:leaderTermsAccepted&&!service.Busy))
                {ChatRequest("/social/chat/leader-consent",new SocialService.Request{accepted=true,consentVersion=state.leaderRulesVersion});showLeaderTerms=false;leaderTermsAccepted=false;}
            }
            else
            {
                DrawClanRulesText(new Rect(Left+16,360,ContentWidth-32,102),state.chatRules,ref chatRulesScroll);
                theme.Label(new Rect(Left+16,466,ContentWidth-32,30),"YOUR AGE GROUP",theme.Body);
                string[] ages={"UNDER 13","13–17","18 OR OLDER"};string[] values={"under13","13-17","18+"};
                float gap=12,width=(ContentWidth-32-gap*2)/3;
                for(int i=0;i<ages.Length;i++)
                    if(theme.Action(new Rect(Left+16+i*(width+gap),502,width,44),ages[i],selected:chatAgeBand==values[i],enabled:!service.Busy))chatAgeBand=values[i];
                chatTermsAccepted=DrawClanConsent(new Rect(Left+16,554,ContentWidth-32,56),chatTermsAccepted,
                    "I have read and accept the chat rules.",!service.Busy);
                if(theme.Action(new Rect(Left+16,620,300,40),"ENTER CLAN CHAT",true,enabled:chatTermsAccepted&&(chatAgeBand=="13-17"||chatAgeBand=="18+")&&!service.Busy))
                    ChatRequest("/social/chat/consent",new SocialService.Request{accepted=true,consentVersion=state.chatRulesVersion,ageBand=chatAgeBand});
                if(chatAgeBand=="under13")theme.Label(new Rect(Left+332,620,ContentWidth-348,40),"Chat is unavailable for this age group.",theme.Body);
            }
        }
        void DrawClanRulesText(Rect rect,string text,ref Vector2 scroll)
        {
            clanRulesText??=new GUIStyle(theme.Body){fontSize=24,wordWrap=true};
            float width=rect.width-28,height=Mathf.Max(rect.height,clanRulesText.CalcHeight(new GUIContent(text??""),width)+12);
            scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,width,height));
            GUI.Label(new Rect(0,0,width,height),text??"",clanRulesText);
            GUI.EndScrollView();
        }
        bool DrawClanConsent(Rect rect,bool accepted,string label,bool enabled)
        {
            // One large hit target, including the label; no tiny default GUI.Toggle.
            if(theme.Action(rect,"",selected:accepted,enabled:enabled))accepted=!accepted;
            var box=new Rect(rect.x+10,rect.y+10,36,36);
            theme.Frame(box,true);
            if(accepted)FrontEndTheme.Fill(new Rect(box.x+7,box.y+7,22,22),FrontEndTheme.Positive);
            clanConsentText??=new GUIStyle(theme.Body){fontSize=22,wordWrap=true};
            theme.Label(new Rect(rect.x+60,rect.y+4,rect.width-72,rect.height-8),label,clanConsentText);
            return accepted;
        }
        void DrawChatReport(SocialService service,SocialService.Snapshot state)
        {
            theme.Label(new Rect(Left,390,ContentWidth,30),"REPORT TO GAME ADMINISTRATION",theme.Heading);
            var reasons=state.reportReasons??Array.Empty<string>();
            for(int i=0;i<reasons.Length;i++)
                if(theme.Action(new Rect(Left+(i%2)*(ContentWidth/2),430+(i/2)*40,ContentWidth/2-12,34),reasons[i],selected:reportReasonIndex==i))reportReasonIndex=i;
            if(theme.Action(new Rect(Left,608,230,42),"SUBMIT REPORT",true,enabled:!service.Busy&&reasons.Length>0))
            {
                ChatRequest("/social/chat/report",new SocialService.Request{clanId=state.clan.clanId,messageId=reportMessageId,playerId=reportPlayerId,reason=reasons[reportReasonIndex]});
                reportMessageId=reportPlayerId=null;selectedChatPlayer=null;
            }
            if(theme.Action(new Rect(Left+244,608,150,42),"CANCEL"))reportMessageId=reportPlayerId=null;
        }
        void DrawChatMemberActions(SocialService service,SocialService.Snapshot state)
        {
            if(reportPlayerId!=null){DrawChatReport(service,state);return;}
            var player=selectedChatPlayer;
            theme.Label(new Rect(Left,390,ContentWidth,34),player.name,theme.Heading);
            if(pendingPlayerAction!=null)
            {
                theme.Label(new Rect(Left,436,ContentWidth,62),"Confirm "+pendingPlayerAction.ToUpperInvariant()+" for this player?",theme.Body);
                if(theme.Action(new Rect(Left,520,200,42),"CONFIRM",true,enabled:!service.Busy))
                {
                    string route=pendingPlayerAction=="remove"?"/social/clan/kick":pendingPlayerAction=="transfer"?"/social/clan/transfer":"/social/chat/moderate";
                    ChatRequest(route,new SocialService.Request{playerId=player.playerId,clanId=state.clan.clanId,action=pendingPlayerAction,minutes=60});
                    selectedChatPlayer=null;pendingPlayerAction=null;
                }
                if(theme.Action(new Rect(Left+216,520,170,42),"CANCEL"))pendingPlayerAction=null;
                return;
            }
            bool blocked=ChatBlocked(state,player.playerId);
            SocialButton(new Rect(Left,438,200,36),blocked?"UNBLOCK":"BLOCK",blocked?"/social/unblock":"/social/block",player.playerId);
            if(theme.Action(new Rect(Left+214,438,200,36),"REPORT PLAYER",enabled:!service.Busy))
            {reportPlayerId=player.playerId;reportMessageId=null;reportReasonIndex=0;}
            if(state.role=="leader"&&player.role!=null)
            {
                string[] actions={"warn","mute","unmute","ban","remove","transfer"};
                string[] labels={"WARN","MUTE  /  1 HOUR","UNMUTE","BAN FROM CLAN","REMOVE","TRANSFER LEADERSHIP"};
                for(int i=0;i<actions.Length;i++)
                    if(theme.Action(new Rect(Left+(i%2)*(ContentWidth/2),486+(i/2)*42,ContentWidth/2-12,36),labels[i],enabled:!service.Busy&&state.leaderAccepted))pendingPlayerAction=actions[i];
                SocialButton(new Rect(Left,614,230,36),player.role=="officer"?"DEMOTE OFFICER":"MAKE OFFICER","/social/clan/role",player.playerId,role:player.role=="officer"?"member":"officer");
            }
            if(theme.Action(new Rect(Right-150,614,150,36),"BACK"))selectedChatPlayer=null;
        }
        void DrawChatBlocks(SocialService service,SocialService.Snapshot state)
        {
            theme.Label(new Rect(Left,390,ContentWidth,30),"BLOCKED PLAYERS",theme.Heading);
            var players=state.blocked??NoPlayers;
            blockedOffset=Mathf.Clamp(blockedOffset,0,Mathf.Max(0,players.Length-1)/4*4);
            int count=Mathf.Min(4,players.Length-blockedOffset);
            for(int i=0;i<count;i++)
            {
                var player=players[blockedOffset+i];float y=432+i*42;
                theme.Label(new Rect(Left,y,ContentWidth-180,36),player.name,theme.Body);
                SocialButton(new Rect(Right-168,y,168,34),"UNBLOCK","/social/unblock",player.playerId);
            }
            if(theme.Action(new Rect(Right-150,608,150,32),"BACK"))showBlockedPlayers=false;
            DrawSocialPager(blockedOffset,count,offset=>blockedOffset=offset,players.Length,y:642,pageSize:4);
        }
        void DrawChatAdministration(SocialService service)
        {
            if(selectedChatReport!=null)
            {
                var report=selectedChatReport;
                theme.FitLabel(new Rect(Left,278,ContentWidth,30),report.reason+"  /  "+report.status,theme.Heading);
                theme.Label(new Rect(Left,316,ContentWidth,56),"Clan: "+report.clanId+"  /  Message: "+report.messageId+"\nReport: "+report.reportId+"  /  "+ChatTime(report.createdAt),theme.Small,true);
                adminScroll=GUI.BeginScrollView(new Rect(Left,382,ContentWidth,132),adminScroll,new Rect(0,0,ContentWidth-28,Mathf.Max(132,theme.Body.CalcHeight(new GUIContent(report.evidence?.text??"Player report"),ContentWidth-40)+36)));
                theme.Label(new Rect(10,0,ContentWidth-48,30),report.evidence?.name??"Player",theme.Small,true);
                theme.Label(new Rect(10,34,ContentWidth-48,500),report.evidence?.text??"Player report",theme.Body);GUI.EndScrollView();
                string[] actions={"remove","suspend","unsuspend","close","resolve"};
                string[] labels={"REMOVE MESSAGE","SUSPEND ACCOUNT","RESTORE ACCOUNT","CLOSE CLAN","RESOLVE REPORT"};
                if(pendingPlayerAction!=null)
                {
                    theme.Label(new Rect(Left,528,ContentWidth,34),"Confirm "+pendingPlayerAction.ToUpperInvariant()+"?",theme.Body);
                    if(theme.Action(new Rect(Left,572,200,40),"CONFIRM",true,enabled:!service.Busy))
                    {ChatRequest("/social/chat/admin/action",new SocialService.Request{reportId=report.reportId,action=pendingPlayerAction});pendingPlayerAction=null;selectedChatReport=null;}
                    if(theme.Action(new Rect(Left+214,572,180,40),"CANCEL"))pendingPlayerAction=null;
                }
                else for(int i=0;i<actions.Length;i++)
                    if(theme.Action(new Rect(Left+(i%2)*(ContentWidth/2),526+(i/2)*40,ContentWidth/2-12,34),labels[i],enabled:!service.Busy&&(actions[i]!="remove"||!string.IsNullOrEmpty(report.messageId))))pendingPlayerAction=actions[i];
                if(theme.Action(new Rect(Right-130,642,130,28),"BACK")){selectedChatReport=null;pendingPlayerAction=null;}
                return;
            }
            if(theme.Action(new Rect(Left,272,220,36),"LOAD REPORTS",true,enabled:!service.Busy))service.Load("/social/chat/admin/reports");
            if(theme.Action(new Rect(Left+236,272,220,36),"RETRY EMAILS",enabled:!service.Busy))service.Load("/social/chat/admin/delivery");
            var reports=service.Reports?.reports??Array.Empty<SocialService.Report>();
            for(int i=0;i<reports.Length;i++)
            {
                var report=reports[i];float y=324+i*35;
                theme.Panel(new Rect(Left,y,ContentWidth,32));
                theme.FitLabel(new Rect(Left+12,y,ContentWidth-164,32),report.reason+"  /  "+report.status+"  /  "+ChatTime(report.createdAt),theme.Small);
                if(theme.Action(new Rect(Right-138,y,138,30),"REVIEW")){selectedChatReport=report;pendingPlayerAction=null;adminScroll=Vector2.zero;}
            }
            DrawSocialPager(service.Reports?.offset??0,reports.Length,offset=>service.Load("/social/chat/admin/reports",new SocialService.Request{offset=offset}));
        }
    }
}

