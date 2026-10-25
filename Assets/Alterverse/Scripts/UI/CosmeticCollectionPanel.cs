using System;
using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Profile;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    // Event-refreshed data, clipped visible rows and one shared 3D preview, even with 100+ entries.
    internal sealed class CosmeticCollectionPanel : IDisposable
    {
        private sealed class Card
        {
            public CosmeticDefinition Definition;
            public bool Owned, Equipped;
            public string Status;
        }
        private sealed class BagSlot
        {
            public BattleItemType Item;
            public string Name, Description, Count;
            public bool Equipped;
        }
        private sealed class ViewState
        {
            public Vector2 Scroll;
            public UniversalHeroCosmetic Selected=UniversalHeroCosmetic.Original;
            public int Filter;
            public bool DetailOpen, PointerDown, Dragging;
            public Vector2 PointerStart, ScrollStart;
            public string Message="";
        }
        private readonly ViewState shop=new(), inventory=new(), bag=new();
        private readonly List<Card> shopCards=new(), inventoryCards=new();
        private readonly List<BagSlot> bagSlots=new();
        private bool dirty=true, battleItems, previewOnly;
        private HeroId hero;
        private UniversalHeroCosmetic equipped;
        private BattleItemType selectedItem;
        private int balance;
        private string balanceLabel="",heroLabel="";
        public bool InventoryPreview=>!battleItems;
        public UniversalHeroCosmetic Preview(bool ownedOnly)=>ownedOnly?inventory.Selected:shop.Selected;
        public CosmeticCollectionPanel(){ProfileTransaction.Committed+=Invalidate;PaidShardCommerce.EntitlementsChanged+=Invalidate;BattleItemInventory.Changed+=Invalidate;}
        public void Dispose(){ProfileTransaction.Committed-=Invalidate;PaidShardCommerce.EntitlementsChanged-=Invalidate;BattleItemInventory.Changed-=Invalidate;}
        public void Invalidate(){dirty=true;}
        public string BalanceText(HeroId selectedHero){Refresh(selectedHero);return balanceLabel;}
        private void Refresh(HeroId selectedHero)
        {
            if(previewOnly||!dirty&&hero==selectedHero)return;
            hero=selectedHero;
            UniversalCosmeticSelection.EnsureMigrated(hero);
            equipped=UniversalCosmeticSelection.EquippedFor(hero);
            balance=PlayerWallet.Balance;balanceLabel="SHARDS  "+balance.ToString("N0");heroLabel=HeroIdentity.NameFor(hero);
            shopCards.Clear();inventoryCards.Clear();
            for(int i=0;i<CosmeticCatalog.All.Count;i++)
            {
                var definition=CosmeticCatalog.All[i];bool owned=CosmeticCatalog.IsOwned(definition.Id);
                bool active=definition.Headwear?HeadwearSelection.EquippedFor(hero)==definition.Id:equipped==definition.Id;
                var card=new Card{Definition=definition,Owned=owned,Equipped=active,Status=active?"EQUIPPED":owned?"OWNED":"LOCKED"};
                if(definition.Purchasable)shopCards.Add(card);if(owned)inventoryCards.Add(card);
            }
            NormalizeSelection(shop,shopCards);
            NormalizeSelection(inventory,inventoryCards);
            bagSlots.Clear();
            var activeItem=BattleItemInventory.Equipped;
            foreach(var item in BattleItemInventory.All)
            {
                int count=BattleItemInventory.Count(item);if(count<=0)continue;
                bagSlots.Add(new BagSlot{Item=item,Name=BattleItemInventory.Name(item),Description=BattleItemInventory.ShortDescription(item),Count="×"+count,Equipped=activeItem==item});
            }
            bool found=false;foreach(var slot in bagSlots)found|=slot.Item==selectedItem;
            if(!found)selectedItem=bagSlots.Count>0?bagSlots[0].Item:BattleItemType.None;
            dirty=false;
        }
        private static IEnumerable<CosmeticDefinition> Definitions(List<Card> cards)
        {foreach(var card in cards)yield return card.Definition;}
        private void NormalizeSelection(ViewState state,List<Card> cards)
        {
            var preferred=previewOnly?UniversalHeroCosmetic.Original:state.Filter==0?equipped:HeadwearSelection.EquippedFor(hero);
            foreach(var card in cards)
                if(card.Definition.Id==state.Selected&&Matches(card,state.Filter))
                {preferred=state.Selected;break;}
            var selected=CosmeticCollectionCategories.Choose(Definitions(cards),state.Filter,preferred);
            if(state.Selected!=selected)state.Message="";
            state.Selected=selected;
        }
        private static string EmptyCategory(ViewState state,bool ownedOnly)=>state.Filter==0?
            (ownedOnly?"NO WINGS OWNED":"NO WINGS AVAILABLE"):
            (ownedOnly?"NO HATS OWNED":"NO HATS AVAILABLE");
        public void DrawInventoryTabs(FrontEndTheme theme,Rect rect)
        {
            float width=Mathf.Min(210,(rect.width-12)*.5f);
            if(theme.Action(new Rect(rect.x,rect.y,width,52),"COSMETICS",selected:!battleItems))battleItems=false;
            if(theme.Action(new Rect(rect.x+width+12,rect.y,width,52),"BATTLE ITEMS",selected:battleItems))battleItems=true;
        }
        public void Draw(FrontEndTheme theme,Rect area,HeroId selectedHero,bool ownedOnly,MenuHeroShowcase showcase,Action<int> changeHero,Action openShards=null)
        {
            Refresh(selectedHero);
            bool compact=Screen.safeArea.width<1000||area.width<900;
            if(ownedOnly&&battleItems){DrawBag(theme,area,compact);return;}
            ViewState state=ownedOnly?inventory:shop;
            List<Card> cards=ownedOnly?inventoryCards:shopCards;
            float gridWidth=compact?area.width:Mathf.Floor(area.width*.54f);
            Rect grid=new(area.x,area.y,gridWidth,area.height);
            Rect detail=new(grid.xMax+16,area.y,area.width-gridWidth-16,area.height);
            if(compact&&state.DetailOpen)
            {
                if(theme.Action(new Rect(area.x,area.y,150,48),"< COLLECTION"))state.DetailOpen=false;
                DrawDetail(theme,new Rect(area.x,area.y+58,area.width,area.height-58),state,ownedOnly,showcase,openShards);
                return;
            }
            theme.Panel(grid);
            HeroSelector(theme,new Rect(grid.x+12,grid.y+10,grid.width-24,48),changeHero);
            float top=grid.y+70;
            for(int i=0;i<CosmeticCollectionCategories.Count;i++)
            {
                if(theme.Action(new Rect(grid.x+12+i*108,top,100,44),CosmeticCollectionCategories.Label(i),selected:state.Filter==i))
                {state.Filter=i;state.Scroll=Vector2.zero;state.Message="";NormalizeSelection(state,cards);}
            }
            top+=54;
            Rect viewport=new(grid.x+12,top,grid.width-24,grid.yMax-top-34);
            int count=0;foreach(var card in cards)if(Matches(card,state.Filter))count++;
            int columns=CollectionGridLayout.Columns(viewport.width,170,compact?3:4);
            float cellWidth=CollectionGridLayout.CellWidth(viewport.width,columns);
            const float height=170;
            float contentHeight=CollectionGridLayout.ContentHeight(count,columns,height);
            BeginGrid(viewport,state,contentHeight);
            int index=0;
            foreach(var card in cards)
            {
                if(!Matches(card,state.Filter))continue;
                int row=index/columns,col=index%columns;index++;
                Rect rect=new(col*(cellWidth+12),row*(height+12),cellWidth,height);
                if(rect.yMax<state.Scroll.y||rect.y>state.Scroll.y+viewport.height)continue;
                bool selected=state.Selected==card.Definition.Id;
                FrontEndTheme.Fill(rect,selected?FrontEndTheme.Raised:FrontEndTheme.Surface);
                Outline(rect,selected?card.Definition.Accent:card.Equipped?FrontEndTheme.Accent:new Color(.16f,.19f,.24f),selected||card.Equipped?2:1);
                CosmeticIcon(new Rect(rect.x+12,rect.y+9,rect.width-24,65),card.Definition,card.Owned);
                theme.Label(new Rect(rect.x+12,rect.y+78,rect.width-24,40),card.Definition.Name,theme.Small,!card.Owned);
                theme.FitLabel(new Rect(rect.x+12,rect.y+123,rect.width-24,20),card.Definition.PriceLabel,theme.Small,!card.Owned);
                theme.Label(new Rect(rect.x+22,rect.y+147,rect.width-34,18),card.Status,theme.Small,!card.Owned);
                FrontEndTheme.Fill(new Rect(rect.x+10,rect.y+152,6,6),card.Equipped?FrontEndTheme.Accent:card.Owned?card.Definition.Accent:FrontEndTheme.Muted);
                if(GUI.Button(rect,GUIContent.none,GUIStyle.none)&&!state.Dragging)
                {state.Selected=card.Definition.Id;state.DetailOpen=true;state.Message="";Alterverse.Audio.UiSoundRouter.Confirm();}
            }
            GUI.EndScrollView();
            if(count==0)theme.FitLabel(new Rect(viewport.x,viewport.y+12,viewport.width,30),EmptyCategory(state,ownedOnly),theme.Small,true);
            theme.Label(new Rect(grid.x+12,grid.yMax-28,grid.width-24,22),ownedOnly?"ACCOUNT COLLECTION  •  EQUIP PER HERO":balanceLabel,theme.Small,true);
            if(!compact)DrawDetail(theme,detail,state,ownedOnly,showcase,openShards);
        }
#if UNITY_EDITOR
        internal void PopulateStressFixture(bool items)
        {
            previewOnly=true;battleItems=items;hero=HeroId.Veyr;heroLabel="LAYOUT PREVIEW";balance=10000;balanceLabel="PREVIEW  /  NO SAVE WRITES";
            shopCards.Clear();inventoryCards.Clear();bagSlots.Clear();
            for(int i=0;i<100;i++)
            {
                var source=CosmeticCatalog.All[i%CosmeticCatalog.All.Count];
                var definition=new CosmeticDefinition((UniversalHeroCosmetic)(1000+i),i==3?"A VERY LONG FUTURE COSMETIC NAME FOR CLIPPING":source.Name+" "+(i+1),source.Price,"",source.Category,source.Description,source.Accent,headwear:source.Headwear);
                var card=new Card{Definition=definition,Owned=i%3!=0,Equipped=i==1,Status=i==1?"EQUIPPED":i%3!=0?"OWNED":"LOCKED"};
                shopCards.Add(card);if(card.Owned)inventoryCards.Add(card);
            }
            for(int i=0;i<30;i++)bagSlots.Add(new BagSlot{Item=BattleItemInventory.All[i%4],Name="TEST STACK "+(i+1),Description="Layout fixture only. No inventory is changed.",Count="×"+(i+1),Equipped=i==0});
            shop.Selected=shopCards[0].Definition.Id;inventory.Selected=inventoryCards[0].Definition.Id;selectedItem=BattleItemType.Heal;
            NormalizeSelection(shop,shopCards);NormalizeSelection(inventory,inventoryCards);
        }
#endif
        private static bool Matches(Card card,int filter)=>CosmeticCollectionCategories.Matches(card.Definition,filter);
        private void HeroSelector(FrontEndTheme theme,Rect rect,Action<int> changeHero)
        {
            if(theme.Action(new Rect(rect.x,rect.y,52,rect.height),"<")){changeHero(((int)hero+HeroIdentity.HeroCount-1)%HeroIdentity.HeroCount);dirty=true;}
            theme.FitLabel(new Rect(rect.x+64,rect.y,rect.width-128,rect.height),heroLabel,theme.Body);
            if(theme.Action(new Rect(rect.xMax-52,rect.y,52,rect.height),">")){changeHero(((int)hero+1)%HeroIdentity.HeroCount);dirty=true;}
        }
        private void DrawDetail(FrontEndTheme theme,Rect rect,ViewState state,bool ownedOnly,MenuHeroShowcase showcase,Action openShards)
        {
            var sourceCards=ownedOnly?inventoryCards:shopCards;
            CosmeticDefinition definition=null;foreach(var card in sourceCards)if(card.Definition.Id==state.Selected&&Matches(card,state.Filter)){definition=card.Definition;break;}
            if(definition==null)
            {
                theme.Panel(rect);
                theme.FitLabel(new Rect(rect.x+16,rect.y+16,rect.width-32,30),EmptyCategory(state,ownedOnly),theme.Small,true);
                return;
            }
            bool owned=false;foreach(var card in sourceCards)if(card.Definition.Id==definition.Id){owned=card.Owned;break;}
            bool active=definition.Headwear?HeadwearSelection.EquippedFor(hero)==definition.Id:equipped==definition.Id;
            theme.Panel(rect);FrontEndTheme.Fill(new Rect(rect.x,rect.y,rect.width,2),definition.Accent);
            theme.FitLabel(new Rect(rect.x+16,rect.y+12,rect.width-32,36),definition.Name,theme.Heading);
            float previewWidth=rect.width*.45f;
            Rect preview=new(rect.x+8,rect.y+56,previewWidth,rect.height-137);
            showcase?.DrawPreview(preview);
            float x=preview.xMax+8,w=rect.xMax-x-16,y=rect.y+58;
            theme.Label(new Rect(x,y,w,24),active?"EQUIPPED":owned?"OWNED":"ACCOUNT UNLOCK",theme.Small,true);
            theme.FitLabel(new Rect(x,y+30,w,30),definition.PriceLabel,theme.Body);
            theme.Label(new Rect(x,y+69,w,36),definition.Description,theme.Small,true);
            theme.Label(new Rect(x,y+108,w,22),"PROGRESSION BONUS",theme.Small,true);
            theme.Label(new Rect(x,y+136,w,40),definition.BonusLabel,theme.Small);
            theme.Label(new Rect(x,y+182,w,22),"NO COMBAT POWER",theme.Small,true);
            string action=ownedOnly?(active?"EQUIPPED":"EQUIP FOR HERO"):owned?"OWNED":balance>=definition.Price?"BUY":"NOT ENOUGH SHARDS";
            bool shortOnShards=balance<definition.Price;
            bool enabled=!previewOnly&&(ownedOnly?!active:!owned&&(PlayerWallet.CanAfford(definition.Price)||shortOnShards&&openShards!=null&&!PaidShardCommerce.Spending));
            float actionWidth=ownedOnly&&active&&definition.Id!=UniversalHeroCosmetic.Original?(rect.width-44)*.55f:rect.width-32;
            if(theme.Action(new Rect(rect.x+16,rect.yMax-75,actionWidth,48),action,true,enabled:enabled))
            {
                if(ownedOnly)
                {if(UniversalCosmeticSelection.EquipFor(hero,definition.Id)){state.Message="EQUIPPED  /  "+heroLabel;dirty=true;}}
                else
                {
                    if(PlayerWallet.Balance<definition.Price){openShards?.Invoke();return;}
                    var result=CosmeticCatalog.Purchase(definition.Id);
                    state.Message=result==AvatarPurchaseResult.Pending?"Saving purchase…":result==AvatarPurchaseResult.Purchased?"PURCHASED  •  AVAILABLE IN INVENTORY":result==AvatarPurchaseResult.AlreadyOwned?"ALREADY OWNED":result==AvatarPurchaseResult.InsufficientShards?"NOT ENOUGH SHARDS":"Purchase could not be completed. Please try again.";
                    dirty=true;
                }
            }
            if(!previewOnly&&ownedOnly&&active&&definition.Id!=UniversalHeroCosmetic.Original&&theme.Action(new Rect(rect.x+28+actionWidth,rect.yMax-75,rect.width-actionWidth-44,48),"UNEQUIP"))
            {
                if(definition.Headwear)HeadwearSelection.EquipFor(hero,UniversalHeroCosmetic.Original);
                else UniversalCosmeticSelection.EquipFor(hero,UniversalHeroCosmetic.Original);
                state.Message=definition.Headwear?"HEADWEAR UNEQUIPPED":"ORIGINAL EQUIPPED";dirty=true;
            }
            theme.FitLabel(new Rect(rect.x+16,rect.yMax-24,rect.width-32,20),(state.Message=="Saving purchase…"?PaidShardCommerce.Instance?.Status??state.Message:state.Message),theme.Small,true);
        }
        private void DrawBag(FrontEndTheme theme,Rect area,bool compact)
        {
            float width=compact?area.width:area.width*.57f;
            Rect grid=new(area.x,area.y,width,area.height);
            if(compact&&bag.DetailOpen)
            {
                if(theme.Action(new Rect(area.x,area.y,150,48),"< BAG"))bag.DetailOpen=false;
                BagDetail(theme,new Rect(area.x,area.y+58,area.width,area.height-58));return;
            }
            theme.Panel(grid);
            theme.Label(new Rect(grid.x+16,grid.y+12,grid.width-32,26),"BATTLE BAG",theme.Heading);
            Rect viewport=new(grid.x+16,grid.y+54,grid.width-32,grid.height-70);
            int columns=CollectionGridLayout.Columns(viewport.width,100,6);
            float size=CollectionGridLayout.CellWidth(viewport.width,columns);
            BeginGrid(viewport,bag,CollectionGridLayout.ContentHeight(bagSlots.Count,columns,size));
            for(int i=0;i<bagSlots.Count;i++)
            {
                var slot=bagSlots[i];Rect rect=new(i%columns*(size+12),i/columns*(size+12),size,size);
                if(rect.yMax<bag.Scroll.y||rect.y>bag.Scroll.y+viewport.height)continue;
                BattleItemIconRenderer.Draw(rect,slot.Item,slot.Equipped);
                if(slot.Item==selectedItem)Outline(rect,FrontEndTheme.Accent,2);
                theme.Label(new Rect(rect.x+8,rect.yMax-27,rect.width-16,22),slot.Count,theme.Small);
                if(slot.Equipped)FrontEndTheme.Fill(new Rect(rect.xMax-14,rect.y+8,6,6),FrontEndTheme.Accent);
                if(GUI.Button(rect,GUIContent.none,GUIStyle.none)&&!bag.Dragging){selectedItem=slot.Item;bag.DetailOpen=true;bag.Message="";}
            }
            if(bagSlots.Count==0)theme.Label(new Rect(12,12,viewport.width-24,80),"Your bag is empty.\nEarn battle items in matches or visit Battle Supply.",theme.Body,true);
            GUI.EndScrollView();
            if(!compact)BagDetail(theme,new Rect(grid.xMax+16,area.y,area.width-width-16,area.height));
        }
        private void BagDetail(FrontEndTheme theme,Rect rect)
        {
            theme.Panel(rect);BagSlot slot=null;foreach(var candidate in bagSlots)if(candidate.Item==selectedItem){slot=candidate;break;}
            if(slot==null){theme.Center(rect,"SELECT AN ITEM");return;}
            theme.FitLabel(new Rect(rect.x+20,rect.y+16,rect.width-40,36),slot.Name,theme.Heading);
            BattleItemIconRenderer.Draw(new Rect(rect.x+20,rect.y+70,90,90),slot.Item,slot.Equipped);
            theme.Label(new Rect(rect.x+130,rect.y+80,rect.width-150,32),slot.Count,theme.Heading);
            theme.Label(new Rect(rect.x+130,rect.y+120,rect.width-150,24),slot.Equipped?"EQUIPPED":"OWNED",theme.Small,true);
            theme.Label(new Rect(rect.x+20,rect.y+180,rect.width-40,60),slot.Description,theme.Body);
            theme.Label(new Rect(rect.x+20,rect.y+238,rect.width-40,38),"Team supply drop after 2 minutes. Stays until an ally collects it.",theme.Small,true);
            if(theme.Action(new Rect(rect.x+20,rect.yMax-70,rect.width-40,48),slot.Equipped?"UNEQUIP":"EQUIP",true,enabled:!previewOnly))
            {
                bool equipped=BattleItemInventory.Equip(slot.Equipped?BattleItemType.None:slot.Item);
                dirty=true;
                bag.Message=equipped?(slot.Equipped?"UNEQUIPPED":"EQUIPPED"):"Item is no longer available. Choose another item.";
            }
            theme.Label(new Rect(rect.x+20,rect.yMax-21,rect.width-40,18),bag.Message,theme.Small,true);
        }
        private static void BeginGrid(Rect viewport,ViewState state,float contentHeight)
        {
            // Mouse events are also supplied for touch by IMGUI. A drag steals the button's
            // hot control before mouse-up, preventing a swipe from selecting or buying.
            Event e=Event.current;int id=GUIUtility.GetControlID(FocusType.Passive);
            if(e.type==EventType.MouseDown&&e.button==0&&viewport.Contains(e.mousePosition))
            {state.PointerDown=true;state.Dragging=false;state.PointerStart=e.mousePosition;state.ScrollStart=state.Scroll;}
            if(e.rawType==EventType.MouseDrag&&state.PointerDown&&(state.Dragging||Mathf.Abs(e.mousePosition.y-state.PointerStart.y)>8))
            {
                state.Dragging=true;GUIUtility.hotControl=id;
                state.Scroll.y=state.ScrollStart.y+state.PointerStart.y-e.mousePosition.y;e.Use();
            }
            if(e.rawType==EventType.MouseUp&&state.PointerDown)
            {
                state.PointerDown=false;
                if(state.Dragging){GUIUtility.hotControl=0;e.Use();state.Dragging=false;}
            }
            state.Scroll.y=Mathf.Clamp(state.Scroll.y,0,Mathf.Max(0,contentHeight-viewport.height));
            state.Scroll=GUI.BeginScrollView(viewport,state.Scroll,new Rect(0,0,viewport.width-20,Mathf.Max(viewport.height,contentHeight)),false,false);
        }
        private static void Outline(Rect r,Color color,float thickness)
        {
            FrontEndTheme.Fill(new Rect(r.x,r.y,r.width,thickness),color);FrontEndTheme.Fill(new Rect(r.x,r.yMax-thickness,r.width,thickness),color);
            FrontEndTheme.Fill(new Rect(r.x,r.y,thickness,r.height),color);FrontEndTheme.Fill(new Rect(r.xMax-thickness,r.y,thickness,r.height),color);
        }
        private static void CosmeticIcon(Rect rect,CosmeticDefinition definition,bool owned)
        {
            if(definition.Headwear){HeadwearIconRenderer.Draw(rect,definition.Id);return;}
            Texture2D icon=WingPreviewIcons.Load(definition.Id);
            if(icon!=null)
            {
                Color previous=GUI.color;GUI.color=owned?Color.white:new Color(.6f,.6f,.6f,1f);
                GUI.DrawTexture(rect,icon,ScaleMode.ScaleToFit,true);GUI.color=previous;return;
            }
            Color color=definition.Accent;if(!owned)color=Color.Lerp(color,FrontEndTheme.Muted,.5f);
            Vector2 c=rect.center;float s=Mathf.Min(rect.height,rect.width*.5f);
            if(definition.Id==UniversalHeroCosmetic.Original)
            {
                FrontEndTheme.Fill(new Rect(c.x-s*.13f,c.y-s*.35f,s*.26f,s*.22f),color);
                FrontEndTheme.Fill(new Rect(c.x-s*.25f,c.y-s*.07f,s*.5f,s*.46f),color);return;
            }
            for(int side=-1;side<=1;side+=2)
                for(int blade=0;blade<4;blade++)
                {
                    float x=c.x+side*(s*.15f+blade*s*.13f);
                    Rect feather=new(x-s*.055f,c.y-s*.35f+blade*s*.09f,s*.11f,s*(.72f-blade*.12f));
                    FrontEndTheme.Fill(feather,color);
                }
            FrontEndTheme.Fill(new Rect(c.x-3,c.y-8,6,26),Color.white);
        }
    }
    // The same geometry is used for real collections and large-count regression fixtures.
    internal static class CollectionGridLayout
    {
        public static int Columns(float width,float minimum,int maximum)=>Mathf.Clamp(Mathf.FloorToInt((width-20+12)/(minimum+12)),2,maximum);
        public static float CellWidth(float width,int columns)=>(width-20-12*(columns-1))/columns;
        public static float ContentHeight(int count,int columns,float height)=>Mathf.Max(0,Mathf.CeilToInt(count/(float)columns)*(height+12)-12);
    }
}



