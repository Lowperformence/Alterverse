using Alterverse.Characters;using Alterverse.Combat;using Alterverse.Visuals;using UnityEngine;using UnityEngine.InputSystem;using UnityEngine.InputSystem.EnhancedTouch;using ETouch=UnityEngine.InputSystem.EnhancedTouch.Touch;
namespace Alterverse.UI
{
public sealed class PrototypeHUD:MonoBehaviour
{
private Health playerHealth;private BasicAttack basicAttack;private HeroMotor motor;private PlayerHeroAbilities abilities;private HeroIdentity identity;private Health enemyHealth;private GUIStyle titleStyle,textStyle,centerStyle,buttonStyle,abilityStyle,ultStyle;private Texture2D pixel;private int inspectedAbility=-1;private float hintUntil;private string castMessage="";private int armedAbility=-1;private GUIStyle descriptionStyle;private int joystickFinger=-1;private Vector2 joystickCenter,joystickKnob;private const float JoystickRadius=72f;
public void Initialize(Health player,VeyrAbilities legacyAbilities,Health enemy){playerHealth=player;enemyHealth=enemy;if(player!=null){basicAttack=player.GetComponent<BasicAttack>();motor=player.GetComponent<HeroMotor>();abilities=player.GetComponent<PlayerHeroAbilities>();identity=player.GetComponent<HeroIdentity>();}pixel=new Texture2D(1,1);pixel.SetPixel(0,0,Color.white);pixel.Apply();}
private void OnEnable(){EnhancedTouchSupport.Enable();}private void OnDisable(){armedAbility=-1;if(EnhancedTouchSupport.enabled)EnhancedTouchSupport.Disable();if(motor!=null)motor.SetMoveInput(Vector2.zero);joystickFinger=-1;}
private void EnsureStyles(){titleStyle??=new GUIStyle(GUI.skin.label){fontSize=18,fontStyle=FontStyle.Bold};textStyle??=new GUIStyle(GUI.skin.label){fontSize=13,fontStyle=FontStyle.Bold};centerStyle??=new GUIStyle(GUI.skin.label){fontSize=15,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};buttonStyle??=new GUIStyle(GUI.skin.button){fontSize=14,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};abilityStyle??=new GUIStyle(buttonStyle){fontSize=11,wordWrap=true};ultStyle??=new GUIStyle(buttonStyle){fontSize=11,wordWrap=true};}
private void Update(){if(Time.timeScale<=0f||playerHealth==null||playerHealth.IsDead){armedAbility=-1;if(motor!=null)motor.SetMoveInput(Vector2.zero);joystickFinger=-1;return;}if(playerHealth!=null){abilities??=playerHealth.GetComponent<PlayerHeroAbilities>();identity??=playerHealth.GetComponent<HeroIdentity>();}HandleTouchJoystick();}
private float HudScale=>Mathf.Clamp(Screen.height/900f,.72f,1.25f);private void HandleTouchJoystick(){var touches=ETouch.activeTouches;if(motor==null||touches.Count==0){if(joystickFinger>=0&&motor!=null)motor.SetMoveInput(Vector2.zero);joystickFinger=-1;return;}float scale=HudScale;for(int i=0;i<touches.Count;i++){var touch=touches[i];Vector2 screenPos=touch.screenPosition,guiPos=new Vector2(screenPos.x/scale,(Screen.height-screenPos.y)/scale);if(touch.phase==UnityEngine.InputSystem.TouchPhase.Began&&joystickFinger<0&&screenPos.x<Screen.width*.45f){joystickFinger=touch.touchId;joystickCenter=guiPos;joystickKnob=guiPos;}if(touch.touchId!=joystickFinger)continue;if(touch.phase==UnityEngine.InputSystem.TouchPhase.Ended||touch.phase==UnityEngine.InputSystem.TouchPhase.Canceled){joystickFinger=-1;motor.SetMoveInput(Vector2.zero);return;}Vector2 delta=Vector2.ClampMagnitude(guiPos-joystickCenter,JoystickRadius);joystickKnob=joystickCenter+delta;motor.SetMoveInput(new Vector2(delta.x,-delta.y)/JoystickRadius);}}
private void OnGUI(){if(Time.timeScale<=0f||playerHealth==null)return;EnsureStyles();Matrix4x4 previous=GUI.matrix;try{float scale=HudScale;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));float sw=Screen.width/scale,sh=Screen.height/scale;DrawRangePreview();DrawPlayerPanel();DrawCombatButtons(sw,sh);DrawJoystick(sh);DrawAbilityHint(sw,sh);}finally{GUI.matrix=previous;}}
private void DrawPlayerPanel(){if(playerHealth==null)return;Rect panel=new Rect(18,18,285,76);Color accent=identity!=null?StylizedArtDirection.HeroAccent(identity.Hero):StylizedArtDirection.ZeroEnergy;DimensionUiMotif.DrawSplitPanel(panel,pixel,accent,StylizedArtDirection.LumenEnergy,Time.unscaledTime);DimensionUiMotif.DrawCornerCuts(panel,pixel,accent);GUI.Label(new Rect(32,23,250,25),(identity!=null?identity.DisplayName.ToUpperInvariant():"HERO"),titleStyle);GUI.Label(new Rect(32,48,105,20),$"HP {playerHealth.Current:0} / {playerHealth.Max:0}"+(playerHealth.Shield>0?$" +{playerHealth.Shield:0}":""),textStyle);DrawBar(new Rect(136,51,148,12),playerHealth.Normalized,accent);}
private void DrawCombatButtons(float sw,float sh){float attackSize=96,attackX=sw-126,attackY=sh-130;Color accent=identity!=null?StylizedArtDirection.HeroAccent(identity.Hero):StylizedArtDirection.ZeroEnergy;DrawActionFrame(new Rect(attackX-6,attackY-6,attackSize+12,attackSize+12),accent,.48f);if(GUI.Button(new Rect(attackX,attackY,attackSize,attackSize),"ATTACK",buttonStyle)&&basicAttack!=null)basicAttack.TryAttack();DrawAbilityButton(new Rect(sw-224,sh-111,76,76),AbilityEffect(0)+"\n[Q]",abilities?.QRemaining??0,()=>ArmAbility(0),false);DrawAbilityButton(new Rect(sw-199,sh-202,72,72),AbilityEffect(1)+"\n[E]",abilities?.WRemaining??0,()=>ArmAbility(1),false);DrawAbilityButton(new Rect(sw-113,sh-224,72,72),AbilityEffect(2)+"\n[R]",abilities?.ERemaining??0,()=>ArmAbility(2),false);DrawAbilityButton(new Rect(sw-300,sh-194,78,78),AbilityEffect(3)+"\n[F]",abilities?.UltRemaining??0,()=>ArmAbility(3),true);}
private static string Short(string value){return string.IsNullOrEmpty(value)?"?":value.ToUpperInvariant();}private void DrawAbilityButton(Rect rect,string label,float cooldown,System.Action action,bool ultimate){Color accent=identity!=null?StylizedArtDirection.HeroAccent(identity.Hero):StylizedArtDirection.ZeroEnergy;bool ready=cooldown<=.05f;float pulse=ultimate&&ready?.55f+.15f*Mathf.Sin(Time.unscaledTime*4f):.32f;DrawActionFrame(new Rect(rect.x-4,rect.y-4,rect.width+8,rect.height+8),ultimate?StylizedArtDirection.WarmAccent:accent,pulse);bool old=GUI.enabled;GUI.enabled=ready;if(GUI.Button(rect,ready?(ultimate?"ULT\n"+label:label):$"{label}\n{cooldown:0.0}",ultimate?ultStyle:abilityStyle))action?.Invoke();GUI.enabled=old;if(!ready){float ratio=Mathf.Clamp01(cooldown/12f);Fill(new Rect(rect.x+4,rect.y+4,rect.width-8,(rect.height-8)*ratio),new Color(.015f,.015f,.025f,.42f));Fill(new Rect(rect.x+5,rect.y+rect.height-7,(rect.width-10)*ratio,3),new Color(1,1,1,.3f));}else Fill(new Rect(rect.x+rect.width*.28f,rect.y+5,rect.width*.44f,2),new Color(accent.r,accent.g,accent.b,.7f));}
private string AbilityEffect(int slot)
{
HeroId hero=identity!=null?identity.Hero:HeroId.Veyr;
if(hero==HeroId.Mira&&slot==1)return "HEAL";
if(hero==HeroId.Mira&&slot==2)return "SHIELD ALLY";
if(hero==HeroId.Mira&&slot==3)return "TEAM HEAL";
if(hero==HeroId.Kaelon&&slot==1)return "SHIELD";
if(hero==HeroId.Kaelon&&slot==0)return "STUN";
if(hero==HeroId.Kaelon&&slot==3)return "CITADEL";
if(hero==HeroId.Rook&&slot==1)return "RECOVER";
if(hero==HeroId.Veyr&&slot==2)return "DASH";
if((hero==HeroId.Nyra&&slot==1)||(hero==HeroId.Elyra&&slot==2))return "EVADE";
if(hero==HeroId.Nyra&&slot==3)return "VOLLEY";
if(abilities!=null&&abilities.SpecFor(slot).Radius>0)return "AREA HIT";
return slot==3?"EXECUTE":"STRIKE";
}
private void ArmAbility(int slot)
{
if(abilities==null||playerHealth==null||playerHealth.IsDead)return;
armedAbility=slot;inspectedAbility=slot;hintUntil=float.PositiveInfinity;castMessage="PREVIEW";
}
private void CastAbility(int slot)
{
if(abilities==null)return;inspectedAbility=slot;hintUntil=Time.unscaledTime+3f;
bool success=slot==0?abilities.CastQ():slot==1?abilities.CastW():slot==2?abilities.CastE():abilities.CastUltimate();
castMessage=success?"CAST":abilities.RemainingFor(slot)>0?"ON COOLDOWN":"NO TARGET IN RANGE";armedAbility=-1;
}
private void DrawAbilityHint(float sw,float sh)
{
if(inspectedAbility<0||Time.unscaledTime>hintUntil||abilities==null||playerHealth.IsDead)return;
AbilitySpec spec=abilities.SpecFor(inspectedAbility);
float width=Mathf.Min(430,sw-36);Rect area=new Rect(sw-width-18,Mathf.Max(100,sh-432),width,164);
Fill(area,new Color(.025f,.035f,.055f,.97f));
descriptionStyle??=new GUIStyle(textStyle){wordWrap=true,fontSize=13};
GUI.Label(new Rect(area.x+12,area.y+8,area.width-24,22),spec.Name.ToUpperInvariant(),textStyle);
GUI.Label(new Rect(area.x+12,area.y+33,area.width-24,46),abilities.Describe(inspectedAbility),descriptionStyle);
GUI.Label(new Rect(area.x+12,area.y+83,area.width-24,22),$"RANGE {abilities.PreviewRange(inspectedAbility):0.#}m  |  COOLDOWN {abilities.CooldownFor(inspectedAbility):0.#}s",textStyle);
if(armedAbility>=0)
{
bool wasEnabled=GUI.enabled;GUI.enabled=!playerHealth.IsDead&&abilities.RemainingFor(armedAbility)<=0;
if(GUI.Button(new Rect(area.x+12,area.y+116,area.width*.53f,36),"CAST",buttonStyle))CastAbility(armedAbility);
GUI.enabled=wasEnabled;
if(GUI.Button(new Rect(area.x+area.width*.59f,area.y+116,area.width*.37f,36),"CANCEL",buttonStyle)){armedAbility=-1;hintUntil=0;}
}
else GUI.Label(new Rect(area.x+12,area.y+119,area.width-24,28),castMessage,textStyle);
}
private void DrawRangePreview()
{
if(armedAbility<0||abilities==null||playerHealth==null||playerHealth.IsDead||Event.current.type!=EventType.Repaint)return;
Camera cam=Camera.main;if(cam==null)return;float range=abilities.PreviewRange(armedAbility);if(range<=0)return;
Vector3 center=playerHealth.transform.position;center.y=.18f;
Color color=AbilityEffect(armedAbility).Contains("HEAL")?new Color(.3f,1f,.5f):new Color(.3f,.85f,1f);
const int segments=48;
for(int i=0;i<segments;i++)
{
float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
Vector3 p=cam.WorldToScreenPoint(center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*range);
Vector3 q=cam.WorldToScreenPoint(center+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*range);
if(p.z<=0||q.z<=0)continue;
Vector2 from=new Vector2(p.x,Screen.height-p.y)/HudScale,to=new Vector2(q.x,Screen.height-q.y)/HudScale;
Vector2 delta=to-from;Matrix4x4 before=GUI.matrix;
try{GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,from);Fill(new Rect(from.x,from.y-1,delta.magnitude,2),color);}
finally{GUI.matrix=before;}
}
}
private void DrawActionFrame(Rect r,Color c,float alpha){Fill(r,new Color(StylizedArtDirection.Ink.r,StylizedArtDirection.Ink.g,StylizedArtDirection.Ink.b,.72f));Fill(new Rect(r.x,r.y,r.width,2),new Color(c.r,c.g,c.b,alpha));Fill(new Rect(r.x,r.y,2,r.height*.32f),new Color(c.r,c.g,c.b,alpha));Fill(new Rect(r.xMax-2,r.yMax-r.height*.32f,2,r.height*.32f),new Color(c.r,c.g,c.b,alpha));Fill(new Rect(r.x+r.width*.68f,r.yMax-2,r.width*.32f,2),new Color(c.r,c.g,c.b,alpha));}
private void DrawJoystick(float sh){Vector2 center=joystickFinger>=0?joystickCenter:new Vector2(112,sh-112),knob=joystickFinger>=0?joystickKnob:center;Color accent=identity!=null?StylizedArtDirection.HeroAccent(identity.Hero):StylizedArtDirection.ZeroEnergy;Rect baseRect=new Rect(center.x-70,center.y-70,140,140);DrawActionFrame(baseRect,accent,.3f);Fill(new Rect(center.x-2,center.y-2,4,4),new Color(1,1,1,.45f));GUI.Box(new Rect(knob.x-29,knob.y-29,58,58),joystickFinger>=0?"":"MOVE",centerStyle);}private void DrawBar(Rect rect,float normalized,Color color){Fill(rect,new Color(.02f,.02f,.03f,.9f));Fill(new Rect(rect.x+2,rect.y+2,(rect.width-4)*Mathf.Clamp01(normalized),rect.height-4),color);if(normalized<.3f)Fill(new Rect(rect.x+2,rect.y+2,(rect.width-4)*Mathf.Clamp01(normalized),2),new Color(1,.35f,.2f,.85f));}private void Fill(Rect r,Color c){if(pixel==null)return;Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,pixel);GUI.color=old;}private void OnDestroy(){if(pixel!=null)Destroy(pixel);}
}
}

