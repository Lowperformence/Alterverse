using Alterverse.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace Alterverse.UI
{
    // Presentation only: this canvas deliberately has no GraphicRaycaster or Selectables.
    // PrototypeHUD remains the sole owner of input and ability execution.
    internal sealed class AbilityButtonView
    {
        private static readonly int CellId=Shader.PropertyToID("_Cell"), AccentId=Shader.PropertyToID("_Accent"),
            CooldownId=Shader.PropertyToID("_Cooldown"), UnavailableId=Shader.PropertyToID("_Unavailable"),
            HeldId=Shader.PropertyToID("_Held"), PressId=Shader.PropertyToID("_Press"),
            UltimateId=Shader.PropertyToID("_Ultimate"), ClockId=Shader.PropertyToID("_Clock");
        private readonly GameObject root;
        private readonly Canvas canvas;
        private readonly RectTransform[] faces=new RectTransform[5];
        private readonly Material[] materials=new Material[5];
        private readonly Text[] numbers=new Text[5];
        private readonly string[] seconds=new string[601];
        private readonly Text ultimateLabel;
        private Rect lastSafe;
        private bool laidOut;

        public AbilityButtonView(Transform owner,HeroId hero)
        {
            Texture2D atlas=Resources.Load<Texture2D>("CombatUI/AbilityAtlas");
            Material template=Resources.Load<Material>("CombatUI/AbilityButton");
            if(atlas==null||template==null)
            {
                Debug.LogError("DIMENSIONS FRONT: CombatUI atlas/material missing. Reimport the CombatUI assets.");
                return;
            }
            root=new GameObject("Ability art HUD",typeof(RectTransform),typeof(Canvas));
            root.transform.SetParent(owner,false);
            canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30;
            Font font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for(int n=0;n<seconds.Length;n++)seconds[n]=n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            for(int i=0;i<5;i++)
            {
                var go=new GameObject(i==4?"Basic attack":"Ability "+i,typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
                go.transform.SetParent(root.transform,false);
                faces[i]=go.GetComponent<RectTransform>();
                faces[i].anchorMin=faces[i].anchorMax=new Vector2(0,1);
                faces[i].pivot=new Vector2(.5f,.5f);
                var image=go.GetComponent<RawImage>();
                Texture2D heroIcon=hero==HeroId.Nerok?Resources.Load<Texture2D>("CyberShark/Icons/"+i):
                    hero==HeroId.Timmy?Resources.Load<Texture2D>("AndromedaTimmy/Icons/"+i):hero==HeroId.VoidKnight?Resources.Load<Texture2D>("VoidKnight/Icons/"+i):null;
                Texture2D art=heroIcon!=null?heroIcon:atlas;
                image.texture=art;
                image.raycastTarget=false;
                materials[i]=new Material(template){name="Ability HUD material "+i};
                // Keep RawImage and shader material explicitly bound to the same atlas texture.
                materials[i].mainTexture=art;
                materials[i].SetVector(CellId,heroIcon!=null?new Vector4(0,0,1,1):AtlasCell(hero,i));
                materials[i].SetColor(AccentId,Accent(hero));
                materials[i].SetFloat(UltimateId,i==3?1:0);
                image.material=materials[i];
                numbers[i]=MakeLabel(go.transform,"Cooldown",font,24);
                numbers[i].fontStyle=FontStyle.Bold;
                // One cached outline per number, no per-frame effects/components.
                var outline=numbers[i].gameObject.AddComponent<Outline>();
                outline.effectColor=new Color(0,0,0,.95f);outline.effectDistance=new Vector2(1,-1);
                numbers[i].enabled=false;
            }
            ultimateLabel=MakeLabel(root.transform,"Ultimate label",font,12);
            ultimateLabel.text="ULTIMATE";ultimateLabel.color=Color.Lerp(Accent(hero),Color.white,.55f);
            ultimateLabel.rectTransform.anchorMin=ultimateLabel.rectTransform.anchorMax=new Vector2(0,1);
            ultimateLabel.rectTransform.pivot=new Vector2(.5f,.5f);
            canvas.enabled=false;
        }

        private static Text MakeLabel(Transform parent,string name,Font font,int size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));
            go.transform.SetParent(parent,false);
            Text text=go.GetComponent<Text>();text.font=font;text.fontSize=size;text.alignment=TextAnchor.MiddleCenter;
            text.color=Color.white;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Overflow;
            text.verticalOverflow=VerticalWrapMode.Overflow;
            text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;
            text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;
            return text;
        }

        // Pixel-calibrated crops: generated source has nonuniform spacing. Do not assume a uniform grid.
        // Original six rows are preserved; Traveler occupies the appended seventh row.
        // Atlas dimensions: 1145 x 1616. Cropping/masking uses the same shader for every hero.
        internal static Vector4 AtlasCell(HeroId hero,int slot)
        {
            int row=hero switch {HeroId.Veyr=>0,HeroId.Kaelon=>1,HeroId.Nyra=>2,HeroId.Elyra=>3,HeroId.Mira=>4,HeroId.Traveler=>6,_=>5};
            int column=slot==4?0:slot+1;
            float centerY=row switch {0=>113,1=>324,2=>535,3=>746,4=>958,6=>1500,_=>1181};
            float centerX=column switch {0=>111,1=>332,2=>552,3=>770,_=>1008};
            float side=212;
            if(column==4&&row!=6)centerY=row==5?1185:centerY+1;
            // Ultimates sample only their interior artwork; the shader supplies a complete, uncropped frame.
            float x=centerX-side*.5f,y=centerY-side*.5f;
            return new Vector4(x/1145f,1-(y+side)/1616f,side/1145f,side/1616f);
        }

        internal static Color Accent(HeroId hero)=>hero switch
        {
            HeroId.VoidKnight=>new Color(.62f,.18f,1f),HeroId.Veyr=>new Color(.72f,.24f,1),HeroId.Kaelon=>new Color(1,.64f,.16f),
            HeroId.Nyra=>new Color(.12f,.78f,1),HeroId.Elyra=>new Color(.53f,.34f,1),
            HeroId.Mira=>new Color(.14f,1,.57f),HeroId.Rook=>new Color(1,.3f,.08f),
            HeroId.Traveler=>new Color(.16f,.82f,1f),HeroId.Nerok=>new Color(.04f,.72f,1f),HeroId.Timmy=>new Color(.30f,.85f,1f),_=>Color.white
        };

        public void SetVisible(bool visible){if(canvas!=null&&canvas.enabled!=visible)canvas.enabled=visible;}

        public void Layout(CombatHudLayout layout)
        {
            if(canvas==null||(laidOut&&lastSafe==layout.Safe))return;
            laidOut=true;lastSafe=layout.Safe;
            for(int i=0;i<5;i++)
            {
                Rect hit=layout.Action(i);
                // Ultimate is no longer shrunk to 64 px; its whole 84 px footprint stays inside its hit area.
                float size=(i==3?84:Mathf.Min(hit.width,hit.height)-4)*layout.Scale;
                faces[i].sizeDelta=new Vector2(size,size);
                faces[i].anchoredPosition=new Vector2(layout.Safe.x+hit.center.x*layout.Scale,
                    -(Screen.height-layout.Safe.yMax+hit.center.y*layout.Scale));
                numbers[i].fontSize=Mathf.Max(12,Mathf.RoundToInt((i==3?24:23)*layout.Scale));
            }
            Rect ult=layout.Action(3);
            ultimateLabel.rectTransform.sizeDelta=new Vector2(ult.width*layout.Scale,18*layout.Scale);
            ultimateLabel.rectTransform.anchoredPosition=new Vector2(layout.Safe.x+ult.center.x*layout.Scale,
                -(Screen.height-layout.Safe.yMax+(ult.y-12)*layout.Scale));
            ultimateLabel.fontSize=Mathf.Max(9,Mathf.RoundToInt(11*layout.Scale));
        }

        public void SetState(int slot,float remaining,float duration,bool unavailable,bool held,float pressAge)
        {
            if(canvas==null)return;
            float impulse=Mathf.Clamp01(1-pressAge/.16f);
            float scale=held?.965f:1;
            scale-=.035f*impulse;
            faces[slot].localScale=new Vector3(scale,scale,1);
            Material m=materials[slot];
            m.SetFloat(CooldownId,remaining>0?Mathf.Clamp01(remaining/Mathf.Max(.01f,duration)):0);
            m.SetFloat(UnavailableId,unavailable?1:0);
            m.SetFloat(HeldId,held?1:0);m.SetFloat(PressId,impulse);m.SetFloat(ClockId,Time.unscaledTime);
            Text label=numbers[slot];bool cooling=remaining>0;
            if(label.enabled!=cooling)label.enabled=cooling;
            if(cooling)
            {
                string value=seconds[Mathf.Clamp(Mathf.CeilToInt(remaining),1,600)];
                if(label.text!=value)label.text=value;
            }
        }

        public void Dispose()
        {
            if(canvas!=null)canvas.enabled=false;
            if(root!=null)Object.Destroy(root);
            for(int i=0;i<materials.Length;i++)if(materials[i]!=null)Object.Destroy(materials[i]);
        }
    }
}


