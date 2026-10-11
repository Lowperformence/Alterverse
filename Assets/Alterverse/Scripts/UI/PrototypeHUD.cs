using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch=UnityEngine.InputSystem.EnhancedTouch.Touch;
namespace Alterverse.UI
{
    // Keep the component name for existing bootstrap/scene compatibility.
    [DefaultExecutionOrder(-200)]
    public sealed class PrototypeHUD:MonoBehaviour
    {
        private Health health;
        private BasicAttack attack;
        private ManualAiming aiming;
        private HeroMotor motor;
        private PlayerHeroAbilities abilities;
        private HeroIdentity identity;
        private Camera view;
        private FrontEndTheme theme;
        private GUIStyle centered,small;
        private readonly string[] names=new string[4],descriptions=new string[4],details=new string[4];
        private readonly CombatGlyph[] glyphs=new CombatGlyph[4];
        private string heroName,statusText="",feedback="",healthText="";
        private AbilityButtonView buttonView;
        private float pressStarted=-10f;
        private float nextReadout;
        private float feedbackUntil,damageTrail=1,damageHold,previousHp=1;
        private bool focused=true,suspended,combatInside;
        private int joystickFinger=-1,combatFinger=-1,pressedSlot=-1,inspectedSlot=-1;
        private bool mouseJoystick;
        private Vector2 joystickCenter,joystickOrigin,joystickKnob;
        private const float Radius=CombatHudLayout.JoystickRadius;
        private CombatHudLayout layout;
        private Rect previousSafe;
        private Color accent;
        private Texture2D joystickOuterTexture,joystickThumbTexture;
        public void Initialize(Health player,VeyrAbilities legacyAbilities,Health enemy)
        {
            health=player;if(player==null)return;
            if (player.GetComponent<AbilityRangePreviewOverlay>() == null)
                player.gameObject.AddComponent<AbilityRangePreviewOverlay>();
            attack=player.GetComponent<BasicAttack>();motor=player.GetComponent<HeroMotor>();
            abilities=player.GetComponent<PlayerHeroAbilities>();aiming=ManualAiming.For(player.gameObject);identity=player.GetComponent<HeroIdentity>();view=Camera.main;
            heroName=identity!=null?identity.DisplayName.ToUpperInvariant():"HERO";
            accent=identity!=null?StylizedArtDirection.HeroAccent(identity.Hero):FrontEndTheme.Accent;
            damageTrail=health.Normalized;CacheAbilities();
            buttonView?.Dispose();buttonView=new AbilityButtonView(transform,identity!=null?identity.Hero:HeroId.Veyr);
        }
        private void CacheAbilities()
        {
            if(abilities==null)return;
            for(int i=0;i<4;i++)
            {
                AbilitySpec spec=abilities.SpecFor(i);names[i]=spec.Name.ToUpperInvariant();descriptions[i]=abilities.Describe(i);
                AimSpec aim=abilities.AimFor(i);
                details[i]=aim.aimType==AimType.Self ? "SELF  ·  TAP TO CAST" :
                    aim.aimType==AimType.SelfArea ? $"RADIUS {aim.aoeRadius:0.#}m  ·  TAP TO CAST" :
                    $"RANGE {abilities.PreviewRange(i):0.#}m  ·  DRAG TO AIM";
                glyphs[i]=GlyphFor(i);
            }
        }
        private CombatGlyph GlyphFor(int slot)
        {
            HeroId h=identity!=null?identity.Hero:HeroId.Veyr;
            if(h==HeroId.Veyr)return slot==0?CombatGlyph.NullCut:slot==1?CombatGlyph.Rift:slot==2?CombatGlyph.Dash:CombatGlyph.Execute;
            if(h==HeroId.Kaelon)return slot==0?CombatGlyph.Impact:slot==1?CombatGlyph.Shield:slot==2?CombatGlyph.Gravity:CombatGlyph.Citadel;
            if(h==HeroId.Nyra)return slot==0?CombatGlyph.PrismShot:slot==1?CombatGlyph.Dash:slot==2?CombatGlyph.Snare:CombatGlyph.Volley;
            if(h==HeroId.Elyra)return slot==0?CombatGlyph.OrbitFracture:slot==1?CombatGlyph.Invert:slot==2?CombatGlyph.Phase:CombatGlyph.Singularity;
            if(h==HeroId.Mira)return slot==0?CombatGlyph.Seed:slot==1?CombatGlyph.Heal:slot==2?CombatGlyph.Shelter:CombatGlyph.Wildsurge;
            if(h==HeroId.Rook)return slot==0?CombatGlyph.Swing:slot==1?CombatGlyph.Armor:slot==2?CombatGlyph.Slam:CombatGlyph.Overdrive;
            if(h==HeroId.Traveler)return slot==0?CombatGlyph.ChronoBeam:slot==1?CombatGlyph.TimeStep:slot==2?CombatGlyph.ChronoField:CombatGlyph.Paradox;
            return CombatGlyph.ChronoBeam;
        }
        private void OnEnable()=>EnhancedTouchSupport.Enable();
        private void OnDestroy()
        {
            buttonView?.Dispose();
            if(joystickOuterTexture!=null)Destroy(joystickOuterTexture);
            if(joystickThumbTexture!=null)Destroy(joystickThumbTexture);
        }
        private void OnDisable(){buttonView?.SetVisible(false);ResetInput();if(EnhancedTouchSupport.enabled)EnhancedTouchSupport.Disable();}
        private void OnApplicationFocus(bool value){focused=value;if(!value){buttonView?.SetVisible(false);ResetInput();}}
        private void OnApplicationPause(bool value){suspended=value;if(value){buttonView?.SetVisible(false);ResetInput();}}
        private void ResetInput(){joystickFinger=combatFinger=pressedSlot=inspectedSlot=-1;mouseJoystick=false;combatInside=false;aiming?.Cancel();motor?.SetMoveInput(Vector2.zero);}
        private void Update()
        {
            layout=new CombatHudLayout(Screen.safeArea);
            if(previousSafe!=layout.Safe){previousSafe=layout.Safe;ResetInput();}
            if(layout.Safe.width<=0||layout.Safe.height<=0||Time.timeScale<=0||health==null||health.IsDead||!focused||suspended){buttonView?.SetVisible(false);ResetInput();return;}
            float hp=health.Normalized;
            if(hp<previousHp)damageHold=Time.unscaledTime+.25f;previousHp=hp;
            if(hp>damageTrail)damageTrail=hp;
            if(hp<damageTrail&&Time.unscaledTime>=damageHold)damageTrail=Mathf.MoveTowards(damageTrail,hp,Time.unscaledDeltaTime*.7f);
            HeroCombatStatus status=health.GetComponent<HeroCombatStatus>();
            statusText=status!=null&&status.Stunned?"STUNNED":status!=null&&status.MoveMultiplier<.99f?"SLOWED":"";
            if (!HeroCombatStatus.CanAct(health.gameObject))
            { aiming?.Cancel(); pressedSlot = inspectedSlot = -1; combatInside = false; }
            HandleTouches();
            // A touch gesture exclusively owns touch controls; the mouse path is for editor/desktop testing.
            if(ETouch.activeTouches.Count==0&&combatFinger<0)HandleMouse();
            if(Time.unscaledTime>=nextReadout)
            {
                nextReadout=Time.unscaledTime+.1f;
                healthText=$"{health.Current:0} / {health.Max:0}"+(health.Shield>0?$"   +{health.Shield:0} SHIELD":"");

            }
        }
        private void LateUpdate()
        {
            bool visible=layout.Safe.width>0&&layout.Safe.height>0&&Time.timeScale>0&&health!=null&&!health.IsDead&&focused&&!suspended;
            buttonView?.SetVisible(visible);
            if(!visible||buttonView==null)return;
            buttonView.Layout(layout);
            bool blocked=!HeroCombatStatus.CanAct(health.gameObject);
            for(int i=0;i<5;i++)
            {
                float remaining=i<4?(abilities!=null?abilities.RemainingFor(i):0):(attack!=null?attack.RemainingCooldown:0);
                float duration=i<4?(abilities!=null?abilities.CooldownFor(i):1):(attack!=null?attack.AttackCooldown:1);
                bool unavailable=blocked||(i<4?abilities==null:attack==null);
                buttonView.SetState(i,remaining,duration,unavailable,pressedSlot==i&&combatInside,pressedSlot==i?Time.unscaledTime-pressStarted:10);
            }
        }
        private void HandleTouches()
        {
            bool foundMove=false,foundCombat=false;
            var touches=ETouch.activeTouches;
            // Resolve all movement before any combat release, regardless of finger order.
            for(int i=0;i<touches.Count;i++)
            {
                var touch=touches[i];Vector2 point=layout.Point(touch.screenPosition);
                bool ended=touch.phase==UnityEngine.InputSystem.TouchPhase.Ended,canceled=touch.phase==UnityEngine.InputSystem.TouchPhase.Canceled;
                if(touch.phase==UnityEngine.InputSystem.TouchPhase.Began)
                {
                    if(layout.Hit(point)<0&&joystickFinger<0&&layout.MoveCapture.Contains(point))
                    {joystickFinger=touch.touchId;joystickOrigin=point;joystickCenter=new Vector2(Mathf.Clamp(point.x,Radius+12,layout.Width-Radius-12),Mathf.Clamp(point.y,Radius+12,layout.Height-Radius-12));joystickKnob=joystickCenter;}
                }
                if(touch.touchId==joystickFinger)
                {
                    foundMove=true;
                    if(ended||canceled){joystickFinger=-1;motor?.SetMoveInput(Vector2.zero);}
                    else
                    {
                        Vector2 delta=Vector2.ClampMagnitude(point-joystickOrigin,Radius);joystickKnob=joystickCenter+delta;
                        // HeroMotor applies the dead zone once; keep the HUD mapping linear.
                        Vector2 input=delta/Radius;
                        motor?.SetMoveInput(new Vector2(input.x,-input.y));
                    }
                }
            }
            if(!foundMove&&joystickFinger>=0){joystickFinger=-1;motor?.SetMoveInput(Vector2.zero);}
            for(int i=0;i<touches.Count;i++)
            {
                var touch=touches[i];Vector2 point=layout.Point(touch.screenPosition);
                bool ended=touch.phase==UnityEngine.InputSystem.TouchPhase.Ended,canceled=touch.phase==UnityEngine.InputSystem.TouchPhase.Canceled;
                if(touch.phase==UnityEngine.InputSystem.TouchPhase.Began&&combatFinger<0&&pressedSlot<0)
                {
                    int slot=layout.Hit(point);
                    if(slot>=0 && BeginPress(slot,touch.screenPosition)){combatFinger=touch.touchId;}
                }
                if(touch.touchId==combatFinger)
                {
                    foundCombat=true;
                    if(pressedSlot==4)
                    {
                        combatInside=layout.ContainsWithMargin(4,point);
                        if(!ended&&!canceled&&combatInside)TryBasicPress();
                    }
                    else
                    {
                        aiming?.Drag(touch.screenPosition,layout.Scale,layout.CancelZone.Contains(point));
                        combatInside=aiming!=null&&aiming.Active&&!aiming.Cancelled;
                    }
                    if(ended||canceled){EndPress(!canceled&&combatInside);combatFinger=-1;}
                }
            }
            if(!foundCombat&&combatFinger>=0){EndPress(false);combatFinger=-1;}
        }
        private void HandleMouse()
        {
            Mouse mouse=Mouse.current;if(mouse==null)return;
            Vector2 point=layout.Point(mouse.position.ReadValue());

            if(mouse.leftButton.wasPressedThisFrame)
            {
                int slot=layout.Hit(point);
                if(slot>=0)BeginPress(slot,mouse.position.ReadValue());
                else if(layout.MoveCapture.Contains(point))
                {
                    mouseJoystick=true;
                    joystickOrigin=point;
                    joystickCenter=new Vector2(Mathf.Clamp(point.x,Radius+12,layout.Width-Radius-12),Mathf.Clamp(point.y,Radius+12,layout.Height-Radius-12));
                    joystickKnob=joystickCenter;
                }
            }

            if(mouseJoystick)
            {
                if(mouse.leftButton.isPressed)
                {
                    Vector2 delta=Vector2.ClampMagnitude(point-joystickOrigin,Radius);
                    joystickKnob=joystickCenter+delta;
                    // Use the same single-dead-zone mapping as touch input.
                    Vector2 input=delta/Radius;
                    motor?.SetMoveInput(new Vector2(input.x,-input.y));
                }
                if(mouse.leftButton.wasReleasedThisFrame)
                {
                    mouseJoystick=false;
                    motor?.SetMoveInput(Vector2.zero);
                }
                return;
            }

            if(pressedSlot<0)return;
            if(pressedSlot==4)
            {
                combatInside=layout.ContainsWithMargin(4,point);
                if(mouse.leftButton.isPressed&&!mouse.leftButton.wasReleasedThisFrame&&combatInside)TryBasicPress();
            }
            else
            {
                aiming?.Drag(mouse.position.ReadValue(),layout.Scale,layout.CancelZone.Contains(point));
                combatInside=aiming!=null&&aiming.Active&&!aiming.Cancelled;
            }
            if(mouse.leftButton.wasReleasedThisFrame)EndPress(combatInside);
        }
        private bool BeginPress(int slot,Vector2 screen)
        {
            if(slot==4)
            {
                if(attack==null||!attack.isActiveAndEnabled||health==null||health.IsDead||Time.timeScale<=0f||!HeroCombatStatus.CanAct(health.gameObject))return false;
                aiming?.Cancel();
                pressedSlot=4;combatInside=true;inspectedSlot=-1;pressStarted=Time.unscaledTime;
                // Capture even during cooldown so a held press resumes at the next ready attack.
                if(TryBasicPress())Alterverse.Audio.UiSoundRouter.Confirm();
                return true;
            }
            if(aiming==null||!aiming.Begin(slot,screen))return false;
            pressedSlot=slot;combatInside=true;inspectedSlot=slot<4?slot:-1;
            pressStarted=Time.unscaledTime;return true;
        }
        private bool TryBasicPress()
        {
            if(attack==null||!attack.TryAttack())return false;
            feedback="ATTACK";feedbackUntil=Time.unscaledTime+1.5f;nextReadout=0;
            return true;
        }
        private void EndPress(bool commit)
        {
            int slot=pressedSlot;pressedSlot=inspectedSlot=-1;combatInside=false;
            // Basic attacks fire on press/hold; release must never queue another shot.
            if(slot==4){aiming?.Cancel();return;}
            if(!commit||slot<0||slot>3||aiming==null){aiming?.Cancel();return;}
            bool success=aiming.Release();
            if(success){feedback=names[slot];Alterverse.Audio.UiSoundRouter.Confirm();}
            else if(!HeroCombatStatus.CanAct(health.gameObject))feedback="STUNNED — CANNOT CAST";
            else if(abilities.RemainingFor(slot)>0)feedback="RECHARGING";
            else if(slot<4&&(glyphs[slot]==CombatGlyph.Heal||glyphs[slot]==CombatGlyph.Shelter||glyphs[slot]==CombatGlyph.Shield||glyphs[slot]==CombatGlyph.Wildsurge))feedback="NO ELIGIBLE ALLY / ALREADY PROTECTED";
            else feedback="PATH BLOCKED";
            feedbackUntil=Time.unscaledTime+1.5f;nextReadout=0;
        }
        private void OnGUI()
        {
            if(layout.Safe.width<=0||layout.Safe.height<=0||Time.timeScale<=0||health==null||health.IsDead||!focused||suspended)return;
            layout=new CombatHudLayout(Screen.safeArea);if(layout.Safe.width<=0||layout.Safe.height<=0)return;
            theme??=new FrontEndTheme();centered??=new GUIStyle(theme.Body){alignment=TextAnchor.MiddleCenter,fontSize=14};
            small??=new GUIStyle(centered){fontSize=12};
            Matrix4x4 matrix=GUI.matrix;Color color=GUI.color;
            try
            {
                GUI.matrix=layout.Matrix;
                if(Event.current.type!=EventType.Repaint)return;
                DrawHealth();DrawJoystick();DrawHint();DrawCancelZone();
            }
            finally{GUI.matrix=matrix;GUI.color=color;}
        }
        private void DrawHealth()
        {
            Rect r=new Rect(16,16,218,76);theme.HudPanel(r);
            FrontEndTheme.Fill(new Rect(r.x+12,r.y+3,r.width-24,1),FrontEndTheme.Accent);
            theme.Label(new Rect(r.x+12,r.y+5,194,24),heroName,centered);
            Rect bar=new Rect(r.x+12,r.y+34,194,9);
            FrontEndTheme.Fill(new Rect(bar.x-1,bar.y-1,bar.width+2,bar.height+2),new Color(FrontEndTheme.Accent.r,FrontEndTheme.Accent.g,FrontEndTheme.Accent.b,.55f));
            FrontEndTheme.Fill(bar,FrontEndTheme.Background);
            FrontEndTheme.Fill(new Rect(bar.x,bar.y,bar.width*damageTrail,bar.height),new Color(.72f,.76f,.82f,.58f));
            FrontEndTheme.Fill(new Rect(bar.x,bar.y,bar.width*health.Normalized,bar.height),health.Normalized<.3f?new Color(.95f,.43f,.32f):accent);
            if(health.Shield>0)
            {
                Rect shield=new Rect(bar.x,bar.y+11,bar.width*Mathf.Clamp01(health.Shield/health.Max),3);
                FrontEndTheme.Fill(shield,FrontEndTheme.Accent);
            }
            theme.Label(new Rect(r.x+12,r.y+48,194,22),healthText,small);
            if(statusText.Length>0)
            {
                Rect status=new Rect(r.x+28,r.yMax+4,r.width-56,23);
                FrontEndTheme.Fill(status,new Color(FrontEndTheme.Surface.r,FrontEndTheme.Surface.g,FrontEndTheme.Surface.b,.92f));
                FrontEndTheme.Fill(new Rect(status.x,status.y,status.width,1),statusText=="STUNNED"?new Color(.96f,.48f,.32f):new Color(.86f,.72f,.34f));
                theme.Label(status,statusText,centered);
            }
        }
        private void DrawJoystick()
        {
            bool active=joystickFinger>=0||mouseJoystick;
            Vector2 center=active?joystickCenter:layout.JoystickIdle;
            Vector2 knob=active?joystickKnob:center;
            EnsureJoystickTextures();

            // Draw each joystick part as one anti-aliased circle texture.
            // This avoids the segmented/ribbed look caused by CombatGlyphs.Ring line segments.
            Color before=GUI.color;
            GUI.color=new Color(1f,1f,1f,active?.36f:.22f);
            GUI.DrawTexture(new Rect(center.x-Radius,center.y-Radius,Radius*2,Radius*2),joystickOuterTexture,ScaleMode.StretchToFill,true);

            GUI.color=new Color(.90f,.90f,.90f,active?.36f:.22f);
            GUI.DrawTexture(new Rect(knob.x-CombatHudLayout.ThumbRadius,knob.y-CombatHudLayout.ThumbRadius,CombatHudLayout.ThumbRadius*2,CombatHudLayout.ThumbRadius*2),joystickThumbTexture,ScaleMode.StretchToFill,true);
            GUI.color=before;
        }

        private void EnsureJoystickTextures()
        {
            if(joystickOuterTexture==null)joystickOuterTexture=BuildJoystickCircleTexture(256,true);
            if(joystickThumbTexture==null)joystickThumbTexture=BuildJoystickCircleTexture(128,false);
        }

        private static Texture2D BuildJoystickCircleTexture(int size,bool outlineOnly)
        {
            Texture2D texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true)
            {
                name=outlineOnly?"DF_JoystickOuterCircle":"DF_JoystickThumbCircle",
                filterMode=FilterMode.Bilinear,
                wrapMode=TextureWrapMode.Clamp,
                hideFlags=HideFlags.HideAndDontSave
            };
            Color32[] pixels=new Color32[size*size];
            float center=(size-1)*.5f;
            // Cached analytic circle coverage; no segmented line drawing.
            float radius=size*.5f-2f;
            float stroke=size*(outlineOnly?1.25f/(Radius*2):.9f/(CombatHudLayout.ThumbRadius*2));

            for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                float dx=x-center,dy=y-center;
                float distance=Mathf.Sqrt(dx*dx+dy*dy);
                // Flat fill and a single thin outline. Only the boundary pixels are
                // antialiased; no radial gradient, glow or additional disc is drawn.
                float outer=Mathf.Clamp01(radius+.5f-distance);
                float inner=Mathf.Clamp01(radius-stroke+.5f-distance);
                float alpha=outer-(outlineOnly?1f:.25f)*inner;

                pixels[y*size+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(Mathf.Clamp01(alpha)*255f));
            }

            texture.SetPixels32(pixels);
            texture.Apply(false,true);
            return texture;
        }

        private void DrawHint()
        {
            if(inspectedSlot<0)
            {
                if(Time.unscaledTime<feedbackUntil)
                {
                    Rect message=new Rect(layout.Width-410,layout.Height-290,390,28);
                    theme.Panel(message);theme.Label(message,feedback,centered);
                }
                return;
            }
            if(!Alterverse.AI.BotDifficultySettings.ShowHoldAttackHelp)return;
            Rect r=new Rect(layout.Width-418,layout.Height-386,394,130);theme.Panel(r);
            theme.Label(new Rect(r.x+12,r.y+6,r.width-24,24),names[inspectedSlot],theme.Button);
            theme.Label(new Rect(r.x+14,r.y+34,r.width-28,42),descriptions[inspectedSlot],small);
            string detail=glyphs[inspectedSlot]==CombatGlyph.Dash?"DRAG TO CHOOSE DIRECTION · COLLISION MAY SHORTEN":details[inspectedSlot];
            theme.Label(new Rect(r.x+12,r.y+80,r.width-24,18),detail,small,true);
            theme.Label(new Rect(r.x+12,r.y+103,r.width-24,20),combatInside?"RELEASE TO CAST · DRAG TO CANCEL ZONE":"RELEASE TO CANCEL",small);
        }
        private void DrawCancelZone()
        {
            if(aiming==null||!aiming.Active)return;
            Rect zone=layout.CancelZone;
            FrontEndTheme.Fill(zone,aiming.Cancelled?new Color(.9f,.2f,.18f,.8f):new Color(.12f,.15f,.2f,.8f));
            theme.Label(zone,"CANCEL",centered);
        }
    }
}


