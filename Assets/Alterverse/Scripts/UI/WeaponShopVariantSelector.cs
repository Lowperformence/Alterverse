using System.Reflection;
using Alterverse.Characters;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Scalable weapon browser layered over the weapon shop preview. The catalog remains the
    /// source of truth: adding more variants automatically extends the browser without another
    /// UI rewrite. Purchase/equip logic stays owned by MainMenuController.
    /// </summary>
    [DefaultExecutionOrder(1900)]
    internal sealed class WeaponShopVariantSelector : MonoBehaviour
    {
        private static WeaponShopVariantSelector instance;
        private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        private static FieldInfo pageField,weaponShopField,variantField,heroField,messageField;
        private FrontEndTheme theme;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(instance!=null)return;
            GameObject go=new GameObject("DF_WeaponShopVariantSelector");
            DontDestroyOnLoad(go);
            instance=go.AddComponent<WeaponShopVariantSelector>();
        }

        private static void CacheFields()
        {
            if(pageField!=null)return;
            System.Type t=typeof(MainMenuController);
            pageField=t.GetField("page",Flags);
            weaponShopField=t.GetField("weaponShop",Flags);
            variantField=t.GetField("weaponSkinVariant",Flags);
            heroField=t.GetField("selectedHero",Flags);
            messageField=t.GetField("shopMessage",Flags);
        }

        private static int Wrap(int value,int count)
        {
            if(count<=0)return 0;
            value%=count;
            return value<0?value+count:value;
        }

        private static void SelectVariant(MainMenuController menu,int variant,int count)
        {
            if(menu==null||variantField==null||count<=0)return;
            variant=Wrap(variant,count);
            variantField.SetValue(menu,variant);
            if(messageField!=null)messageField.SetValue(menu,string.Empty);
        }

        private void OnGUI()
        {
            MainMenuController menu=FindAnyObjectByType<MainMenuController>();
            if(menu==null||!MainMenuController.IsVisible)return;
            CacheFields();
            if(pageField==null||weaponShopField==null||variantField==null||heroField==null)return;
            object page=pageField.GetValue(menu);
            if(page==null||page.ToString()!="Shop"||!(bool)weaponShopField.GetValue(menu))return;

            int selectedHero=Mathf.Clamp((int)heroField.GetValue(menu),0,HeroIdentity.HeroCount-1);
            HeroId hero=(HeroId)selectedHero;
            int count=WeaponSkinCatalog.VariantCount(hero);
            if(count<=0)return;

            int raw=(int)variantField.GetValue(menu);
            int current=Mathf.Clamp(raw,0,count-1);
            if(raw!=current)variantField.SetValue(menu,current);

            Rect safe=Screen.safeArea;
            if(safe.width<=0||safe.height<=0)return;
            float scale=Mathf.Min(safe.height/720f,safe.width/1200f);
            float menuWidth=safe.width/scale;
            float y=Screen.height-safe.yMax+(safe.height-720f*scale)*.5f;
            Matrix4x4 previousMatrix=GUI.matrix;
            Color previousColor=GUI.color;
            bool previousEnabled=GUI.enabled;
            int previousDepth=GUI.depth;
            try
            {
                GUI.depth=-1000;
                GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x,y,0),Quaternion.identity,Vector3.one*scale);
                GUI.color=Color.white;
                GUI.enabled=true;
                theme??=new FrontEndTheme();

                float contentWidth=Mathf.Min(1360f,menuWidth-64f);
                float left=(menuWidth-contentWidth)*.5f;
                float previewWidth=contentWidth*.54f;
                float rightX=left+previewWidth+32f;
                float rightWidth=contentWidth-previewWidth-32f;

                // Cover the legacy hard-coded count text so the displayed amount always matches
                // the actual catalog size, including future weapon additions.
                Rect countLabel=new Rect(rightX+58f,478f,Mathf.Max(92f,rightWidth-116f),46f);
                FrontEndTheme.Fill(countLabel,new Color(FrontEndTheme.Background.r,FrontEndTheme.Background.g,FrontEndTheme.Background.b,.985f));
                theme.Center(countLabel,$"WEAPON {current+1:00} / {count:00}");

                // Main browser: PREV/NEXT advances one weapon at a time and wraps at both ends.
                // The numbered strip shows one page of direct-jump slots. As the catalog grows,
                // more pages are created automatically.
                Rect panel=new Rect(left+30f,526f,previewWidth-60f,96f);
                FrontEndTheme.Fill(panel,new Color(FrontEndTheme.Background.r,FrontEndTheme.Background.g,FrontEndTheme.Background.b,.94f));

                int slotsPerPage=Mathf.Clamp(Mathf.FloorToInt((panel.width-146f)/72f),3,6);
                int pageCount=Mathf.Max(1,Mathf.CeilToInt(count/(float)slotsPerPage));
                int pageIndex=Mathf.Clamp(current/slotsPerPage,0,pageCount-1);
                int pageStart=pageIndex*slotsPerPage;
                int pageEnd=Mathf.Min(count,pageStart+slotsPerPage);

                WeaponSkinItem selectedItem=WeaponSkinCatalog.For(hero,current);
                theme.Center(new Rect(panel.x+64f,panel.y+4f,panel.width-128f,18f),$"WEAPON BROWSER  •  PAGE {pageIndex+1}/{pageCount}");
                theme.Center(new Rect(panel.x+64f,panel.y+22f,panel.width-128f,20f),selectedItem.WeaponName.ToUpperInvariant());

                if(theme.Action(new Rect(panel.x+8f,panel.y+46f,52f,40f),"<"))
                {
                    SelectVariant(menu,current-1,count);
                    current=Wrap(current-1,count);
                }
                if(theme.Action(new Rect(panel.xMax-60f,panel.y+46f,52f,40f),">"))
                {
                    SelectVariant(menu,current+1,count);
                    current=Wrap(current+1,count);
                }

                float stripX=panel.x+68f;
                float stripWidth=panel.width-136f;
                int visibleCount=Mathf.Max(1,pageEnd-pageStart);
                const float gap=6f;
                float slotWidth=(stripWidth-gap*(visibleCount-1))/visibleCount;
                for(int variant=pageStart;variant<pageEnd;variant++)
                {
                    int slot=variant-pageStart;
                    WeaponSkinItem item=WeaponSkinCatalog.For(hero,variant);
                    Rect button=new Rect(stripX+slot*(slotWidth+gap),panel.y+46f,slotWidth,40f);
                    if(theme.Action(button,$"{variant+1:00}",selected:variant==current))
                    {
                        SelectVariant(menu,variant,count);
                        current=variant;
                    }
                    Color glow=item.Glow;
                    FrontEndTheme.Fill(new Rect(button.x+7f,button.yMax-4f,Mathf.Max(2f,button.width-14f),2f),new Color(glow.r,glow.g,glow.b,.92f));
                }

                // Mouse wheel over the browser also flips through weapons, useful on desktop.
                Event e=Event.current;
                if(e!=null&&e.type==EventType.ScrollWheel&&panel.Contains(e.mousePosition)&&Mathf.Abs(e.delta.y)>.01f)
                {
                    int direction=e.delta.y>0f?1:-1;
                    SelectVariant(menu,current+direction,count);
                    e.Use();
                }
            }
            finally
            {
                GUI.depth=previousDepth;
                GUI.matrix=previousMatrix;
                GUI.color=previousColor;
                GUI.enabled=previousEnabled;
            }
        }
    }
}
