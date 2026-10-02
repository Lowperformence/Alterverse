using System;
using Alterverse.Profile;
using UnityEngine;

namespace Alterverse.UI
{
    // A prepared presentation snapshot. No reward, wallet, receipt or save operations live here.
    internal sealed class MatchResultScreenData
    {
        internal readonly struct Metric
        {
            public readonly string Label, Value;
            public Metric(string label,string value){Label=label;Value=value;}
        }
        internal readonly struct DropReward
        {
            public readonly BattleItemType Item;
            public readonly string Name, Label;
            public DropReward(BattleItemType item,int amount)
            {Item=item;Name=BattleItemInventory.Name(item);Label="MATCH DROP  /  ×"+Mathf.Max(1,amount);}
        }
        public DropReward[] Drops;
        public string DropButton;
        public bool Won, Ready, Retrying;
        public string Difficulty, Hero, BonusName;
        public int Kills, Deaths, Assists, Objectives, Xp, Shards, BaseXp, XpBonus, BaseShards, ShardBonus, BonusPercent;
        public int AccountLevel, PassLevel, PassXpGained;
        public bool AccountMax, PassMax, PassLevelUp;
        public long AccountInto, AccountRequired;
        public int PassInto, PassRequired;
        public float AccountProgress, PassProgress;
        public BattleItemType Drop;
        public Metric[] Details=Array.Empty<Metric>();
        public string Result, KillsText, DeathsText, AssistsText, ObjectivesText, XpText, ShardsText,
            XpBreakdown, ShardBreakdown, BonusBadge, DropName, AccountTitle, AccountText, PassTitle, PassText, PassGain, Status;
        public void Prepare()
        {
            Result=Won?"VICTORY":"DEFEAT";
            KillsText=Number(Kills);DeathsText=Number(Deaths);AssistsText=Number(Assists);
            ObjectivesText="OBJECTIVES  "+Number(Objectives);
            XpText=Ready?"+"+Number(Xp):"—";ShardsText=Ready?"+"+Number(Shards):"—";
            XpBreakdown=Number(BaseXp)+" BASE  +"+Number(XpBonus)+" BONUS";
            ShardBreakdown=Number(BaseShards)+" BASE  +"+Number(ShardBonus)+" BONUS";
            BonusBadge=!Ready?"FINALIZING…":BonusPercent>0?"+"+BonusPercent+"% WING BONUS":(XpBonus>0||ShardBonus>0)?"BONUS REWARD":"MATCH REWARD";
            if(Drops==null)Drops=Drop==BattleItemType.None?Array.Empty<DropReward>():new[]{new DropReward(Drop,1)};
            DropButton=Drops.Length==1?"DROP ×1":Drops.Length>1?"DROPS ×"+Drops.Length:"PROGRESS";
            DropName=Drop==BattleItemType.None?"NO BATTLE ITEM DROP":BattleItemInventory.Name(Drop);
            AccountTitle="ACCOUNT  /  LEVEL "+AccountLevel;
            AccountText=AccountMax?"MAX LEVEL":AccountInto.ToString("N0")+" / "+AccountRequired.ToString("N0")+" XP";
            PassTitle="BATTLE PASS  /  LEVEL "+PassLevel;
            PassText=PassMax?"MAX LEVEL":PassInto.ToString("N0")+" / "+PassRequired.ToString("N0")+" XP";
            PassGain="+"+PassXpGained.ToString("N0")+" XP"+(PassLevelUp?"  •  LEVEL UP":"");
            Status=Ready?"REWARDS RECEIVED":Retrying?"SAVE PENDING  •  RETRYING…":"FINALIZING REWARDS…";
        }
        // Preserve exact values in Details; abbreviate only extreme display values, never rewards.
        public static string Number(int value)
        {
            if(value>=1000000000)return (value/1000000000d).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"B";
            if(value>=1000000)return (value/1000000d).ToString(value>=100000000?"0":"0.#",System.Globalization.CultureInfo.InvariantCulture)+"M";
            if(value>=1000)return (value/1000d).ToString(value>=10000?"0":"0.#",System.Globalization.CultureInfo.InvariantCulture)+"K";
            return value.ToString("N0");
        }
    }
    internal readonly struct MatchResultLayout
    {
        public readonly Rect Header, Kda, Xp, Shards, Lower, Footer, Page;
        public readonly bool Stacked, ShowBreakdown;
        public static float Scale(Rect safe)=>Mathf.Max(1f,Mathf.Min(safe.width/760f,safe.height/480f));
        public MatchResultLayout(float width,float height)
        {
            float margin=height<320?8:height<420?10:18,gap=height<320?4:8;
            float content=Mathf.Min(1080,width-margin*2),left=(width-content)*.5f;
            Stacked=width<600&&height>=500;ShowBreakdown=height>=460;
            float header=Stacked?76:height<320?44:54;
            float stats=height<420?62:76;
            float reward=height<270?56:ShowBreakdown?112:height<320?84:88;
            Header=new Rect(left,margin,content,header);
            Kda=new Rect(left,Header.yMax+gap,content,stats);
            float rewardY=Kda.yMax+gap;
            float cardWidth=Stacked?content:(content-gap)*.5f;
            Xp=new Rect(left,rewardY,cardWidth,reward);
            Shards=Stacked?new Rect(left,Xp.yMax+gap,cardWidth,reward):new Rect(Xp.xMax+gap,rewardY,cardWidth,reward);
            Footer=new Rect(left,height-margin-52,content,52);
            Lower=new Rect(left,Shards.yMax+gap,content,Mathf.Max(0,Footer.y-gap-Shards.yMax-gap));
            Page=new Rect(left,Header.yMax+gap,content,Mathf.Max(0,Footer.y-gap-Header.yMax-gap));
        }
    }
    internal sealed class MatchResultScreen
    {
        private enum Page { Summary, Progress, Details }
        private Page page;
        private MatchResultScreenData data;
        private FrontEndTheme theme;
        private GUIStyle title, number, label, small, button;
        private float shownAt;
        private Vector2 summaryScroll,progressScroll,detailScroll,pointerStart,scrollStart;
        private bool pointerDown,dragging;
        public void SetData(MatchResultScreenData value)
        {
            if(data==null)shownAt=Time.unscaledTime;
            data=value;data.Prepare();
        }
        public bool Draw(float width,float height)
        {
            if(data==null)return false;
            EnsureStyles();
            var layout=new MatchResultLayout(width,height);
            FrontEndTheme.Fill(new Rect(0,0,width,height),FrontEndTheme.Background);
            Color original=GUI.color;
            float reveal=Mathf.Clamp01((Time.unscaledTime-shownAt)/.22f);
            GUI.color=original*new Color(1,1,1,Mathf.Lerp(.82f,1f,reveal));
            Color accent=data.Won?FrontEndTheme.Accent:new Color(.65f,.55f,.77f);
            Header(layout,accent);
            switch(page)
            {
                case Page.Progress: Progress(layout.Page,ref progressScroll);break;
                case Page.Details: Details(layout.Page);break;
                default:
                    Kda(layout.Kda,accent);
                    Reward(layout.Xp,"ACCOUNT XP",data.XpText,data.XpBreakdown,layout.ShowBreakdown,accent);
                    Reward(layout.Shards,"SHARDS",data.ShardsText,data.ShardBreakdown,layout.ShowBreakdown,accent);
                    if(layout.Lower.height>=40)Progress(layout.Lower,ref summaryScroll);
                    break;
            }
            GUI.color=original;
            float smallWidth=(layout.Footer.width-24)*.25f;
            string progressLabel=page==Page.Progress?"SUMMARY":data.Ready?data.DropButton:"PROGRESS";
            if(Action(new Rect(layout.Footer.x,layout.Footer.y,smallWidth,52),progressLabel,data.Ready&&data.Drops.Length>0&&page!=Page.Progress,true))
            {page=page==Page.Progress?Page.Summary:Page.Progress;pointerDown=dragging=false;}
            if(Action(new Rect(layout.Footer.x+smallWidth+8,layout.Footer.y,smallWidth,52),page==Page.Details?"SUMMARY":"DETAILS",false,true))
            {page=page==Page.Details?Page.Summary:Page.Details;pointerDown=dragging=false;}
            // Animation and alternate pages cannot bypass the receipt-completion gate.
            return Action(new Rect(layout.Footer.x+smallWidth*2+16,layout.Footer.y,layout.Footer.width-smallWidth*2-16,52),data.Ready?"BACK TO MENU":"FINALIZING…",true,data.Ready);
        }
        private void EnsureStyles()
        {
            if(theme!=null)return;theme=new FrontEndTheme();
            title=Style(40,true);title.font=FrontEndTypography.Brand;number=Style(32,true);label=Style(18,true);small=Style(17,false);
            button=Style(18,true);button.alignment=TextAnchor.MiddleCenter;
        }
        private static GUIStyle Style(int size,bool bold)=>new(GUI.skin.label)
        {
            font=bold?FrontEndTypography.Display:FrontEndTypography.Body,
            fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,wordWrap=false,clipping=TextClipping.Clip,
            alignment=TextAnchor.MiddleLeft,padding=new RectOffset(0,0,0,0),richText=false,
            normal={textColor=FrontEndTheme.Text}
        };
        private void Header(MatchResultLayout layout,Color accent)
        {
            Rect r=layout.Header;
            theme.Label(new Rect(r.x,r.y,layout.Stacked?r.width:r.width*.57f,48),data.Result,title);
            if(layout.Stacked)
            {
                theme.Label(new Rect(r.x,r.y+48,r.width,24),data.ObjectivesText,small,true);
            }
            else
            {
                float x=r.x+r.width*.59f,w=r.width*.41f;
                theme.Label(new Rect(x,r.y+12,w,23),data.ObjectivesText,small,true);
            }
            FrontEndTheme.Fill(new Rect(r.x,r.yMax-2,r.width,2),accent);
        }
        private void Kda(Rect r,Color accent)
        {
            float w=(r.width-16)/3;
            Stat(new Rect(r.x,r.y,w,r.height),"KILLS",data.KillsText,accent);
            Stat(new Rect(r.x+w+8,r.y,w,r.height),"DEATHS",data.DeathsText,accent);
            Stat(new Rect(r.x+(w+8)*2,r.y,w,r.height),"ASSISTS",data.AssistsText,accent);
        }
        private void Stat(Rect r,string name,string value,Color accent)
        {
            theme.Panel(r);FrontEndTheme.Fill(new Rect(r.x,r.y,2,r.height),accent);
            theme.Label(new Rect(r.x+12,r.y+4,r.width-24,21),name,label,true);
            theme.Label(new Rect(r.x+12,r.y+23,r.width-24,r.height-24),value,number);
        }
        private void Reward(Rect r,string name,string value,string breakdown,bool showBreakdown,Color accent)
        {
            theme.Panel(r);FrontEndTheme.Fill(new Rect(r.x,r.y,r.width,2),accent);
            if(r.height<76)
            {
                theme.Label(new Rect(r.x+10,r.y+14,68,24),name=="ACCOUNT XP"?"XP":name,label,true);
                theme.Label(new Rect(r.x+82,r.y+7,r.width-92,40),value,number);
                return;
            }
            theme.Label(new Rect(r.x+12,r.y+4,r.width-24,21),name,label,true);
            theme.Label(new Rect(r.x+12,r.y+23,r.width-24,38),value,number);
            theme.Label(new Rect(r.x+12,r.y+59,r.width-24,23),data.BonusBadge,small,true);
            if(showBreakdown&&data.Ready)theme.Label(new Rect(r.x+12,r.y+84,r.width-24,23),breakdown,small,true);
        }
        private void Progress(Rect viewport,ref Vector2 scroll)
        {
            bool compact=page==Page.Summary&&viewport.height<76&&data.Drops.Length==1;
            int columns=viewport.width>=700?2:1;
            if(data.Drops.Length==1)columns=1;
            float cardHeight=compact?52:76;
            int rows=Mathf.CeilToInt(data.Drops.Length/(float)columns);
            float dropHeight=data.Ready?(data.Drops.Length==0?30:rows*(cardHeight+8)-8):48;
            float content=data.Ready?dropHeight+216:48;
            BeginScroll(viewport,ref scroll,content);
            float w=viewport.width-20;
            if(!data.Ready)theme.Label(new Rect(10,4,w-20,38),data.Status,label,true);
            else if(data.Drops.Length==0)theme.Label(new Rect(10,0,w-20,28),data.DropName,small,true);
            else
            {
                float cardWidth=(w-(columns-1)*8)/columns;
                for(int i=0;i<data.Drops.Length;i++)
                {
                    var reward=data.Drops[i];float x=(i%columns)*(cardWidth+8),y=(i/columns)*(cardHeight+8);
                    Rect drop=new(x,y,cardWidth,cardHeight);theme.Panel(drop);
                    BattleItemIconRenderer.Draw(new Rect(x+10,y+(compact?5:9),compact?42:56,compact?42:56),reward.Item,true);
                    float textX=x+(compact?66:80),textWidth=cardWidth-(compact?78:92);
                    theme.Label(new Rect(textX,y+(compact?0:5),textWidth,22),reward.Label,small,true);
                    theme.Label(new Rect(textX,y+(compact?24:27),textWidth,26),reward.Name,label);
                    if(!compact)theme.Label(new Rect(textX,y+52,textWidth,20),"RECEIVED",small,true);
                }
            }
            if(data.Ready)
            {
                ProgressCard(new Rect(0,dropHeight+8,w,96),data.PassTitle,data.PassText,data.PassGain,data.PassProgress);
                ProgressCard(new Rect(0,dropHeight+112,w,96),data.AccountTitle,data.AccountText,"",data.AccountProgress);
            }
            GUI.EndScrollView();
        }
        private void ProgressCard(Rect r,string heading,string detail,string gain,float fraction)
        {
            theme.Panel(r);
            theme.Label(new Rect(r.x+12,r.y+6,r.width-24,26),heading,label);
            theme.Label(new Rect(r.x+12,r.y+33,r.width-24,23),detail,small,true);
            theme.Progress(new Rect(r.x+12,r.y+60,r.width-24,8),fraction);
            theme.Label(new Rect(r.x+12,r.y+71,r.width-24,23),gain,small,true);
        }
        private void Details(Rect viewport)
        {
            // Two lines per value also accommodate long MVP names and exact large reward totals.
            BeginScroll(viewport,ref detailScroll,data.Details.Length*68);
            for(int i=0;i<data.Details.Length;i++)
            {
                float y=i*68;if(y+64<detailScroll.y||y>detailScroll.y+viewport.height)continue;
                Rect r=new(0,y,viewport.width-20,62);theme.Panel(r);
                theme.Label(new Rect(12,y+4,r.width-24,22),data.Details[i].Label,small,true);
                theme.Label(new Rect(12,y+27,r.width-24,30),data.Details[i].Value,label);
            }
            GUI.EndScrollView();
        }
        private void BeginScroll(Rect viewport,ref Vector2 scroll,float content)
        {
            Event e=Event.current;int id=GUIUtility.GetControlID(FocusType.Passive);
            if(e.type==EventType.MouseDown&&e.button==0&&viewport.Contains(e.mousePosition))
            {pointerDown=true;dragging=false;pointerStart=e.mousePosition;scrollStart=scroll;}
            if(e.rawType==EventType.MouseDrag&&pointerDown&&(dragging||Mathf.Abs(e.mousePosition.y-pointerStart.y)>7))
            {dragging=true;GUIUtility.hotControl=id;scroll.y=scrollStart.y+pointerStart.y-e.mousePosition.y;e.Use();}
            if(e.rawType==EventType.MouseUp&&pointerDown)
            {pointerDown=false;if(dragging){GUIUtility.hotControl=0;e.Use();dragging=false;}}
            scroll.y=Mathf.Clamp(scroll.y,0,Mathf.Max(0,content-viewport.height));
            scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,viewport.width-20,Mathf.Max(viewport.height,content)),false,false);
        }
        private bool Action(Rect r,string text,bool primary,bool enabled)
        {
            return theme.Action(r,text,primary:primary,enabled:enabled);
        }
    }
}

