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
        public string ExitLabel="BACK TO MENU";
        public bool Won, Ready, Retrying;
        public string Difficulty, Hero, BonusName;
        public int Kills, Deaths, Assists, Objectives, Xp, Shards, BaseXp, XpBonus, BaseShards, ShardBonus;
        public float BonusPercent;
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
            BonusBadge=!Ready?"FINALIZING…":BonusPercent>0?"+"+Alterverse.Match.WingMatchRewardBonus.PercentLabel(BonusPercent)+"% WING BONUS":(XpBonus>0||ShardBonus>0)?"BONUS REWARD":"MATCH REWARD";
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
        private static GUIStyle surface;
        private static Texture2D backdrop;
        private static readonly Color Cyan=new(.34f,.86f,1f), Gold=new(1f,.79f,.40f), Violet=new(.73f,.60f,1f);
        private static readonly Color Coral=new(1f,.48f,.53f), Mint=new(.40f,.94f,.77f);
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
            Background(width,height);
            Color original=GUI.color;
            float reveal=Mathf.Clamp01((Time.unscaledTime-shownAt)/.22f);
            GUI.color=original*new Color(1,1,1,Mathf.Lerp(.82f,1f,reveal));
            Color accent=data.Won?Gold:Coral;
            Header(layout,accent);
            switch(page)
            {
                case Page.Progress: Progress(layout.Page,ref progressScroll);break;
                case Page.Details: Details(layout.Page);break;
                default:
                    Kda(layout.Kda);
                    Reward(layout.Xp,"ACCOUNT XP",data.XpText,data.XpBreakdown,layout.ShowBreakdown,Cyan);
                    Reward(layout.Shards,"SHARDS",data.ShardsText,data.ShardBreakdown,layout.ShowBreakdown,Violet);
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
            if(page!=Page.Summary)
            {
                float tabX=layout.Footer.x+(page==Page.Details?smallWidth+8:0);
                FrontEndTheme.Fill(new Rect(tabX+12,layout.Footer.yMax-4,smallWidth-24,2),Cyan);
            }
            // Animation and alternate pages cannot bypass the receipt-completion gate.
            return Action(new Rect(layout.Footer.x+smallWidth*2+16,layout.Footer.y,layout.Footer.width-smallWidth*2-16,52),data.Ready?data.ExitLabel:"FINALIZING…",true,data.Ready);
        }
        private void EnsureStyles()
        {
            if(theme!=null)return;theme=new FrontEndTheme();
            title=Style(40,true);title.font=FrontEndTypography.Brand;number=Style(36,true);label=Style(18,true);small=Style(17,false);
            button=Style(18,true);button.alignment=TextAnchor.MiddleCenter;
            EnsureArtwork();
        }
        private static GUIStyle Style(int size,bool bold)=>new(GUI.skin.label)
        {
            font=bold?FrontEndTypography.Display:FrontEndTypography.Body,
            fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,wordWrap=false,clipping=TextClipping.Clip,
            alignment=TextAnchor.MiddleLeft,padding=new RectOffset(0,0,0,0),richText=false,
            normal={textColor=FrontEndTheme.Text}
        };
        // Artwork belongs only to this screen. Shared menu/HUD styling is untouched.
        // Cache the small textures once; no particles, cameras or per-frame texture uploads.
        private static void EnsureArtwork()
        {
            if(surface!=null&&backdrop!=null)return;
            const int size=48;
            var mask=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {name="DF_ResultCardMask",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=Mathf.Max(Mathf.Abs(x+.5f-size*.5f)-14,0);
                float dy=Mathf.Max(Mathf.Abs(y+.5f-size*.5f)-14,0);
                pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(9.5f-Mathf.Sqrt(dx*dx+dy*dy)));
            }
            mask.SetPixels(pixels);mask.Apply(false,true);
            surface=new GUIStyle{border=new RectOffset(14,14,14,14)};
            surface.normal.background=mask;
            const int sky=128;
            backdrop=new Texture2D(sky,sky,TextureFormat.RGBA32,false)
            {name="DF_ResultAtmosphere",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            pixels=new Color[sky*sky];
            for(int y=0;y<sky;y++)for(int x=0;x<sky;x++)
            {
                float u=x/(float)(sky-1),v=y/(float)(sky-1);
                float blue=Mathf.Exp(-((u-.14f)*(u-.14f)*5+(v-.83f)*(v-.83f)*5));
                float purple=Mathf.Exp(-((u-.93f)*(u-.93f)*7+(v-.18f)*(v-.18f)*6));
                pixels[y*sky+x]=new Color(.018f+blue*.018f+purple*.055f,.027f+blue*.075f+purple*.015f,.055f+blue*.115f+purple*.09f,1);
            }
            backdrop.SetPixels(pixels);backdrop.Apply(false,true);
        }
        private static Color Alpha(Color color,float alpha)=>new(color.r,color.g,color.b,alpha);
        private static void Surface(Rect r,Color tint,float strength=.12f)
        {
            if(Event.current.type!=EventType.Repaint)return;
            Color previous=GUI.color;
            GUI.color=previous*new Color(0,0,0,.24f);
            surface.Draw(new Rect(r.x,r.y+3,r.width,r.height),GUIContent.none,false,false,false,false);
            GUI.color=previous*Color.Lerp(new Color(.035f,.065f,.105f,.98f),tint,strength);
            surface.Draw(r,GUIContent.none,false,false,false,false);
            GUI.color=previous;
            FrontEndTheme.Fill(new Rect(r.x+12,r.y+1,Mathf.Max(0,r.width-24),1),Alpha(tint,.32f));
        }
        private static void Background(float width,float height)
        {
            GUI.DrawTexture(new Rect(0,0,width,height),backdrop);
            if(Event.current.type!=EventType.Repaint)return;
            Color line=new(.30f,.66f,.88f,.06f);
            // Sparse orbital facets frame the result without competing with the numbers.
            CombatGlyphs.Line(new Vector2(0,height*.7f),new Vector2(width*.40f,0),1,line);
            CombatGlyphs.Line(new Vector2(width*.68f,height),new Vector2(width,height*.28f),1,line);
            CombatGlyphs.Line(new Vector2(width*.72f,0),new Vector2(width,height*.38f),1,line);
        }
        private void Ink(Rect r,string text,GUIStyle style,Color tint,bool fit=false)
        {
            Color previous=GUI.color;GUI.color*=tint;
            int size=style.fontSize;
            if(fit)style.fontSize=Mathf.Min(size,Mathf.Max(12,Mathf.FloorToInt(r.height*.86f)));
            if(fit)theme.FitLabel(r,text,style);else theme.Label(r,text,style);
            style.fontSize=size;
            GUI.color=previous;
        }
        private static void Emblem(Rect r,Color tint,bool victory)
        {
            if(Event.current.type!=EventType.Repaint)return;
            Vector2 c=r.center;float s=Mathf.Min(r.width,r.height)*.5f;
            CombatGlyphs.Ring(c,s,1,Alpha(tint,.25f),6);
            if(victory)
            {
                // Trophy silhouette, independent of font glyph coverage on Android.
                CombatGlyphs.Line(c+new Vector2(-s*.43f,-s*.42f),c+new Vector2(s*.43f,-s*.42f),2,tint);
                CombatGlyphs.Line(c+new Vector2(-s*.43f,-s*.42f),c+new Vector2(-s*.28f,s*.12f),2,tint);
                CombatGlyphs.Line(c+new Vector2(s*.43f,-s*.42f),c+new Vector2(s*.28f,s*.12f),2,tint);
                CombatGlyphs.Line(c+new Vector2(-s*.28f,s*.12f),c+new Vector2(s*.28f,s*.12f),2,tint);
                CombatGlyphs.Line(c+new Vector2(0,s*.12f),c+new Vector2(0,s*.48f),2,tint);
                CombatGlyphs.Line(c+new Vector2(-s*.3f,s*.48f),c+new Vector2(s*.3f,s*.48f),2,tint);
                CombatGlyphs.Line(c+new Vector2(-s*.47f,-s*.25f),c+new Vector2(-s*.67f,-s*.25f),2,tint);
                CombatGlyphs.Line(c+new Vector2(-s*.67f,-s*.25f),c+new Vector2(-s*.35f,s*.02f),2,tint);
                CombatGlyphs.Line(c+new Vector2(s*.47f,-s*.25f),c+new Vector2(s*.67f,-s*.25f),2,tint);
                CombatGlyphs.Line(c+new Vector2(s*.67f,-s*.25f),c+new Vector2(s*.35f,s*.02f),2,tint);
            }
            else
            {
                CombatGlyphs.Line(c+new Vector2(-s*.36f,-s*.38f),c+new Vector2(s*.36f,s*.38f),2,tint);
                CombatGlyphs.Line(c+new Vector2(s*.36f,-s*.38f),c+new Vector2(-s*.36f,s*.38f),2,tint);
            }
        }
        private static void RewardIcon(Rect r,bool xp,Color tint)
        {
            if(Event.current.type!=EventType.Repaint)return;
            Vector2 c=r.center;float s=r.width*.36f;
            if(xp)
            {
                CombatGlyphs.Ring(c,s,1.5f,Alpha(tint,.5f),12);
                CombatGlyphs.Line(c+new Vector2(-s*.45f,s*.15f),c+new Vector2(0,-s*.38f),2,tint);
                CombatGlyphs.Line(c+new Vector2(0,-s*.38f),c+new Vector2(s*.45f,s*.15f),2,tint);
            }
            else
            {
                Vector2 a=c+new Vector2(0,-s),b=c+new Vector2(s*.65f,0),d=c+new Vector2(0,s),e=c+new Vector2(-s*.65f,0);
                CombatGlyphs.Line(a,b,2,tint);CombatGlyphs.Line(b,d,2,tint);
                CombatGlyphs.Line(d,e,2,tint);CombatGlyphs.Line(e,a,2,tint);
                CombatGlyphs.Line(a,d,1,Alpha(tint,.4f));CombatGlyphs.Line(e,b,1,Alpha(tint,.4f));
            }
        }
        private void Header(MatchResultLayout layout,Color accent)
        {
            Rect r=layout.Header;
            string ranked = Alterverse.Multiplayer.CoopAuthority.ActivePlan?.Session?.Mode == Alterverse.Multiplayer.SessionMode.OnlineRanked
                ? Alterverse.Multiplayer.RankedService.Instance?.ResultText : null;
            Surface(r,accent,.10f);
            float emblem=layout.Stacked?40:34;
            Emblem(new Rect(r.x+12,r.y+6,emblem,emblem),accent,data.Won);
            float titleX=r.x+emblem+24;
            Ink(new Rect(titleX,r.y,layout.Stacked?r.xMax-titleX-12:r.width*.55f-emblem-24,Mathf.Min(48,r.height)),data.Result,title,accent,true);
            if(layout.Stacked)
            {
                theme.FitLabel(new Rect(r.x+16,r.y+48,r.width-32,24),string.IsNullOrEmpty(ranked)?data.ObjectivesText:ranked,small,true);
            }
            else
            {
                float x=r.x+r.width*.57f,w=r.width*.43f-16;
                theme.FitLabel(new Rect(x,r.y+3,w,23),data.Hero??"MATCH COMPLETE",label);
                theme.FitLabel(new Rect(x,r.y+25,w,Mathf.Min(21,r.height-25)),string.IsNullOrEmpty(ranked)?data.ObjectivesText:ranked,small,true);
            }
            FrontEndTheme.Fill(new Rect(r.x+16,r.yMax-2,Mathf.Min(100,r.width*.25f),2),accent);
        }
        private void Kda(Rect r)
        {
            float w=(r.width-16)/3;
            Stat(new Rect(r.x,r.y,w,r.height),"KILLS",data.KillsText,Cyan);
            Stat(new Rect(r.x+w+8,r.y,w,r.height),"DEATHS",data.DeathsText,Coral);
            Stat(new Rect(r.x+(w+8)*2,r.y,w,r.height),"ASSISTS",data.AssistsText,Mint);
        }
        private void Stat(Rect r,string name,string value,Color accent)
        {
            Surface(r,accent,.08f);
            Ink(new Rect(r.x+14,r.y+4,r.width-28,21),name,small,accent);
            Ink(new Rect(r.x+14,r.y+23,r.width-28,r.height-24),value,number,Color.white,true);
            FrontEndTheme.Fill(new Rect(r.xMax-25,r.yMax-13,11,2),Alpha(accent,.65f));
        }
        private void Reward(Rect r,string name,string value,string breakdown,bool showBreakdown,Color accent)
        {
            Surface(r,accent,.15f);
            if(r.height<76)
            {
                Ink(new Rect(r.x+12,r.y+14,68,24),name=="ACCOUNT XP"?"XP":name,label,accent);
                Ink(new Rect(r.x+82,r.y+7,r.width-94,40),value,number,accent,true);
                return;
            }
            RewardIcon(new Rect(r.xMax-51,r.y+9,36,36),name=="ACCOUNT XP",accent);
            Ink(new Rect(r.x+14,r.y+4,r.width-70,21),name,small,accent,true);
            Ink(new Rect(r.x+14,r.y+24,r.width-70,38),value,number,accent,true);
            theme.FitLabel(new Rect(r.x+14,r.y+59,r.width-28,23),data.BonusBadge,small);
            if(showBreakdown&&data.Ready)theme.FitLabel(new Rect(r.x+14,r.y+84,r.width-28,23),breakdown,small,true);
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
                    Rect drop=new(x,y,cardWidth,cardHeight);Surface(drop,Gold,.12f);
                    BattleItemIconRenderer.Draw(new Rect(x+10,y+(compact?5:9),compact?42:56,compact?42:56),reward.Item,true);
                    float textX=x+(compact?66:80),textWidth=cardWidth-(compact?78:92);
                    Ink(new Rect(textX,y+(compact?0:5),textWidth,22),reward.Label,small,Gold,true);
                    theme.FitLabel(new Rect(textX,y+(compact?24:27),textWidth,26),reward.Name,label);
                    if(!compact)Ink(new Rect(textX,y+52,textWidth,20),"RECEIVED",small,Mint);
                }
            }
            if(data.Ready)
            {
                ProgressCard(new Rect(0,dropHeight+8,w,96),data.PassTitle,data.PassText,data.PassGain,data.PassProgress,Violet);
                ProgressCard(new Rect(0,dropHeight+112,w,96),data.AccountTitle,data.AccountText,"",data.AccountProgress,Cyan);
            }
            GUI.EndScrollView();
        }
        private void ProgressCard(Rect r,string heading,string detail,string gain,float fraction,Color tint)
        {
            Surface(r,tint,.07f);
            theme.FitLabel(new Rect(r.x+14,r.y+6,r.width-28,26),heading,label);
            theme.FitLabel(new Rect(r.x+14,r.y+33,r.width-28,23),detail,small,true);
            Rect rail=new(r.x+14,r.y+60,r.width-28,6);
            FrontEndTheme.Fill(rail,new Color(.025f,.04f,.075f));
            float filled=rail.width*Mathf.Clamp01(fraction);
            if(filled>0)
            {
                FrontEndTheme.Fill(new Rect(rail.x,rail.y,filled,rail.height),Alpha(tint,.75f));
                FrontEndTheme.Fill(new Rect(rail.x,rail.y,filled,1),tint);
            }
            Ink(new Rect(r.x+14,r.y+71,r.width-28,23),gain,small,tint,true);
        }
        private void Details(Rect viewport)
        {
            // Two lines per value also accommodate long MVP names and exact large reward totals.
            BeginScroll(viewport,ref detailScroll,data.Details.Length*68);
            for(int i=0;i<data.Details.Length;i++)
            {
                float y=i*68;if(y+64<detailScroll.y||y>detailScroll.y+viewport.height)continue;
                Rect r=new(0,y,viewport.width-20,62);Surface(r,i%2==0?Cyan:Violet,.05f);
                Ink(new Rect(14,y+4,r.width-28,22),data.Details[i].Label,small,Cyan,true);
                theme.FitLabel(new Rect(14,y+27,r.width-28,30),data.Details[i].Value,label);
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




