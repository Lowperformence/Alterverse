using System.Reflection;
using Alterverse.Characters;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Exposes the two additional EPIC entries beside the existing weapon-store variant control.
    /// Purchase/equip logic remains owned by MainMenuController; this selector only changes the
    /// previewed catalog variant, so the existing wallet and persistence path is reused verbatim.
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
            if(count<4)return;
            int current=Mathf.Clamp((int)variantField.GetValue(menu),0,count-1);

            Rect safe=Screen.safeArea;
            if(safe.width<=0||safe.height<=0)return;
            float scale=Mathf.Min(safe.height/720f,safe.width/1200f);
            float menuWidth=safe.width/scale;
            float y=Screen.height-safe.yMax+(safe.height-720f*scale)*.5f;
            Matrix4x4 previousMatrix=GUI.matrix;
            Color previousColor=GUI.color;
            bool previousEnabled=GUI.enabled;
            try
            {
                GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x,y,0),Quaternion.identity,Vector3.one*scale);
                GUI.color=Color.white;
                GUI.enabled=true;
                theme??=new FrontEndTheme();

                float contentWidth=Mathf.Min(1360f,menuWidth-64f);
                float left=(menuWidth-contentWidth)*.5f;
                float previewWidth=contentWidth*.54f;

                // Keep the original 01/02 arrows untouched. The expansion lives on the preview
                // card itself, where there are no competing IMGUI controls to steal the click.
                Rect panel=new Rect(left+38f,538f,previewWidth-76f,64f);
                FrontEndTheme.Fill(panel,new Color(FrontEndTheme.Background.r,FrontEndTheme.Background.g,FrontEndTheme.Background.b,.91f));
                theme.Center(new Rect(panel.x+8f,panel.y+2f,panel.width-16f,18f),"NEW EPIC WEAPONS  •  500 SHARDS EACH");

                const float gap=8f;
                float buttonWidth=(panel.width-gap-16f)*.5f;
                for(int i=2;i<=3;i++)
                {
                    WeaponSkinItem item=WeaponSkinCatalog.For(hero,i);
                    Rect button=new Rect(panel.x+8f+(i-2)*(buttonWidth+gap),panel.y+23f,buttonWidth,35f);
                    string label=i==2?"EPIC 03":"EPIC 04";
                    if(theme.Action(button,label,selected:i==current))
                    {
                        variantField.SetValue(menu,i);
                        if(messageField!=null)messageField.SetValue(menu,string.Empty);
                        current=i;
                    }
                    FrontEndTheme.Fill(new Rect(button.x+8f,button.yMax-4f,button.width-16f,2f),item.Glow);
                }
            }
            finally
            {
                GUI.matrix=previousMatrix;
                GUI.color=previousColor;
                GUI.enabled=previousEnabled;
            }
        }
    }
}
