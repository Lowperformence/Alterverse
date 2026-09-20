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
        private const float Radius=64;
        private CombatHudLayout layout;
        private Rect previousSafe;
        private Color accent;
        public void Initialize(Health player,VeyrAbilities legacyAbilities,Health enemy)
        {
            health=player;if(player==null)return;
            attack=player.GetComponent<BasicAttack>();motor=player.GetComponent<HeroMotor>();
            abilities=player.GetComponent<PlayerHeroAbilities>();identity=player.GetComponent<HeroIdentity>();view=Camera.main;
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
                details[i]=$"RANGE {abilities.PreviewRange(i):0.#}m  ·  AUTO TARGET";glyphs[i]=GlyphFor(i);
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
            return slot==0?CombatGlyph.Swing:slot==1?CombatGlyph.Armor:slot==2?CombatGlyph.Slam:CombatGlyph.Overdrive;
        }
        private void OnEnable()=>EnhancedTouchSupport.Enable();
        private void OnDestroy()=>buttonView?.Dispose();
        private void OnDisable(){buttonView?.SetVisible(false);ResetInput();if(EnhancedTouchSupport.enabled)EnhancedTouchSupport.Disable();}
        private void OnApplicationFocus(bool value){focused=value;if(!value){buttonView?.SetVisible(false);ResetInput();}}
        private void OnApplicationPause(bool value){suspended=value;if(value){buttonView?.SetVisible(false);ResetInput();}}
        private void ResetInput(){joystickFinger=combatFinger=pressedSlot=inspectedSlot=-1;mouseJoystick=false;combatInside=false;motor?.SetMoveInput(Vector2.zero);}
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
                    {joystickFinger=touch.touchId;joystickOrigin=point;joystickCenter=new Vector2(Mathf.Clamp(point.x,Radius+12,layout.Width-Radius-12),Mathf.Clamp(point.y,Radius+12,layout.Height-Radius-12));}
                }
                if(touch.touchId==joystickFinger)
                {
                    foundMove=true;
                    if(ended||canceled){joystickFinger=-1;motor?.SetMoveInput(Vector2.zero);}
                    else
                    {
                        Vector2 delta=Vector2.ClampMagnitude(point-joystickOrigin,Radius);joystickKnob=joystickCenter+delta;
                        float magnitude=delta.magnitude/Radius;
                        Vector2 input=magnitude<=.08f?Vector2.zero:delta.normalized*Mathf.Clamp01((magnitude-.08f)/.92f);
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
                    if(slot>=0){combatFinger=touch.touchId;BeginPress(slot);}
                }
                if(touch.touchId==combatFinger)
                {
                    foundCombat=true;combatInside=layout.ContainsWithMargin(pressedSlot,point);
                    if(ended||canceled){EndPress(!canceled&&combatInside);combatFinger=-1;}
                    else if(pressedSlot==4&&combatInside)attack?.TryAttack();
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
                if(slot>=0)BeginPress(slot);
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
                    float magnitude=delta.magnitude/Radius;
                    Vector2 input=magnitude<=.08f?Vector2.zero:delta.normalized*Mathf.Clamp01((magnitude-.08f)/.92f);
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
            combatInside=layout.ContainsWithMargin(pressedSlot,point);
            if(mouse.leftButton.wasReleasedThisFrame)EndPress(combatInside);
            else if(pressedSlot==4&&combatInside&&mouse.leftButton.isPressed)attack?.TryAttack();
        }
        private void BeginPress(int slot)
        {
            pressedSlot=slot;combatInside=true;inspectedSlot=slot<4?slot:-1;pressStarted=Time.unscaledTime;
            if(slot==4)attack?.TryAttack();
        }
        private void EndPress(bool commit)
        {
            int slot=pressedSlot;pressedSlot=-1;inspectedSlot=-1;
            if(!commit||slot<0||slot>3||abilities==null)return;
            bool success=slot==0?abilities.CastQ():slot==1?abilities.CastW():slot==2?abilities.CastE():abilities.CastUltimate();
            if(success){feedback=names[slot];Alterverse.Audio.UiSoundRouter.Confirm();}
            else if(!HeroCombatStatus.CanAct(health.gameObject))feedback="STUNNED — CANNOT CAST";
            else if(abilities.RemainingFor(slot)>0)feedback="ABILITY RECHARGING";
            else if(glyphs[slot]==CombatGlyph.Heal||glyphs[slot]==CombatGlyph.Shelter||glyphs[slot]==CombatGlyph.Shield||glyphs[slot]==CombatGlyph.Wildsurge)feedback="NO ELIGIBLE ALLY / ALREADY PROTECTED";
            else feedback="NO TARGET / PATH BLOCKED";
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
                DrawPreview();DrawHealth();DrawJoystick();DrawHint();
            }
            finally{GUI.matrix=matrix;GUI.color=color;}
        }
        private void DrawHealth()
        {
            Rect r=new Rect(16,16,218,76);theme.Panel(r);
            theme.Label(new Rect(r.x+12,r.y+5,194,24),heroName,centered);
            Rect bar=new Rect(r.x+12,r.y+34,194,9);
            FrontEndTheme.Fill(new Rect(bar.x-1,bar.y-1,bar.width+2,bar.height+2),new Color(.18f,.22f,.28f,.92f));
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
            Vector2 center=active?joystickCenter:new Vector2(104,layout.Height-104);
            Vector2 knob=active?joystickKnob:center;

            // Intentionally minimal mobile joystick: one outer circle and one thumb circle.
            // No fill on the movement field, no accent color, no direction line, glow or extra rings.
            Color outer=new Color(1f,1f,1f,active?.36f:.22f);
            Color thumbFill=new Color(.82f,.84f,.87f,active?.32f:.20f);
            Color thumbOutline=new Color(1f,1f,1f,active?.40f:.25f);

            CombatGlyphs.Ring(center,58f,1.5f,outer,64);
            CombatGlyphs.Disc(knob,20f,thumbFill,28);
            CombatGlyphs.Ring(knob,20f,1.25f,thumbOutline,40);
        }
        private void DrawHint()
        {
            if(inspectedSlot<0)
            {
                if(Time.unscaledTime<feedbackUntil)theme.Label(new Rect(layout.Width-410,layout.Height-290,390,28),feedback,centered);
                return;
            }
            Rect r=new Rect(layout.Width-418,layout.Height-386,394,130);theme.Panel(r);
            theme.Label(new Rect(r.x+12,r.y+6,r.width-24,24),names[inspectedSlot],centered);
            theme.Label(new Rect(r.x+14,r.y+34,r.width-28,42),descriptions[inspectedSlot],small);
            string detail=glyphs[inspectedSlot]==CombatGlyph.Dash?"DIRECTION: MOVE / AUTO EVADE · COLLISION MAY SHORTEN":details[inspectedSlot];
            theme.Label(new Rect(r.x+12,r.y+80,r.width-24,18),detail,small,true);
            theme.Label(new Rect(r.x+12,r.y+103,r.width-24,20),combatInside?"RELEASE TO CAST · DRAG OUT TO CANCEL":"RELEASE TO CANCEL",small);
        }
        private void ProjectLine(Vector3 from,Vector3 to,Color color)
        {
            Vector3 a=view.WorldToScreenPoint(from),b=view.WorldToScreenPoint(to);
            if(a.z>0&&b.z>0)CombatGlyphs.Line(layout.Point(new Vector2(a.x,a.y)),layout.Point(new Vector2(b.x,b.y)),3,color);
        }
        private void DrawPreview()
        {
            if(inspectedSlot<0||!combatInside||abilities==null)return;
            if(view==null)view=Camera.main;if(view==null)return;
            float range=abilities.PreviewRange(inspectedSlot);if(range<=0)return;
            Vector3 center=health.transform.position;center.y=.18f;
            Color tint=(glyphs[inspectedSlot]==CombatGlyph.Heal||glyphs[inspectedSlot]==CombatGlyph.Shelter||glyphs[inspectedSlot]==CombatGlyph.Wildsurge)?new Color(.45f,.87f,.66f):FrontEndTheme.Accent;
            if(glyphs[inspectedSlot]==CombatGlyph.Dash)
            {
                Vector3 direction=abilities.PreviewDirection(inspectedSlot);direction.y=0;direction.Normalize();
                Vector3 end=center+direction*range,side=Vector3.Cross(Vector3.up,direction)*.35f;
                ProjectLine(center,end,tint);ProjectLine(end,end-direction*.6f+side,tint);ProjectLine(end,end-direction*.6f-side,tint);
                return;
            }
            for(int i=0;i<48;i++)
            {
                float a=i*Mathf.PI*2/48,b=(i+1)*Mathf.PI*2/48;
                Vector3 p=view.WorldToScreenPoint(center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*range);
                Vector3 q=view.WorldToScreenPoint(center+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*range);
                if(p.z>0&&q.z>0)CombatGlyphs.Line(layout.Point(new Vector2(p.x,p.y)),layout.Point(new Vector2(q.x,q.y)),2,tint);
            }
        }
    }
}

