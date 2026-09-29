using System.Reflection;
using Alterverse.Characters;
using Alterverse.Shop;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>
    /// Extends the existing weapon-store selector to four catalog entries without changing the
    /// purchase/equip flow. The store still owns all transactions; this layer only exposes the
    /// additional catalog variants in the same preview row.
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
            if(count<=2)return;
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
                float x=left+previewWidth+32f;
                float rowWidth=contentWidth-previewWidth-32f;
                Rect cover=new Rect(x-2f,476f,rowWidth+4f,52f);
                FrontEndTheme.Fill(cover,new Color(FrontEndTheme.Background.r,FrontEndTheme.Background.g,FrontEndTheme.Background.b,.98f));

                const float gap=6f;
                float buttonWidth=(rowWidth-gap*(count-1))/count;
                for(int i=0;i<count;i++)
                {
                    WeaponSkinItem item=WeaponSkinCatalog.For(hero,i);
                    string label=i<2?$"{i+1:00}":$"EPIC {i+1:00}";
                    Rect button=new Rect(x+i*(buttonWidth+gap),480f,buttonWidth,44f);
                    if(theme.Action(button,label,selected:i==current))
                    {
                        variantField.SetValue(menu,i);
                        if(messageField!=null)messageField.SetValue(menu,string.Empty);
                        current=i;
                    }
                    if(i>=2)
                        FrontEndTheme.Fill(new Rect(button.x+9f,button.yMax-5f,button.width-18f,2f),item.Glow);
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
