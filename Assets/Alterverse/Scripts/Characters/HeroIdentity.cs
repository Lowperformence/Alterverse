using UnityEngine;
namespace Alterverse.Characters
{
public enum HeroId{Veyr,Kaelon,Nyra,Elyra,Mira,Rook,Traveler,Nerok,Timmy,VoidKnight,Bolt7}
public enum HeroRole{Assassin,Tank,Marksman,Mage,Support,Bruiser,Skirmisher}
public sealed class HeroIdentity:MonoBehaviour
{
public const int CatalogHeroCount=11;
public const int HeroCount=CatalogHeroCount;
public const float HeroCollisionRadius=.40f;
public const float HeroCollisionHeight=2f;
private Vector3 controllerBaseCenter,capsuleBaseCenter;private bool capturedControllerCenter,capturedCapsuleCenter;
[SerializeField]private HeroId hero;[SerializeField]private HeroRole role;public HeroId Hero=>hero;public HeroRole Role=>role;public string DisplayName=>NameFor(hero);
public void Configure(HeroId id)
{
gameObject.layer=Alterverse.Combat.GameplayCollision.Units;hero=id;role=RoleFor(id);
// Keep hero combat hitboxes slightly tighter than the visuals so dodges and close passes feel clean.
// Unit-vs-unit hard collision is already disabled; this also makes targeting volumes less oversized.
CharacterController controller=GetComponent<CharacterController>();
if(controller!=null){controller.height=id==HeroId.Timmy?TimmyBalance.CollisionHeight:HeroCollisionHeight;controller.radius=id==HeroId.Timmy?TimmyBalance.CollisionRadius:HeroCollisionRadius;if(!capturedControllerCenter){controllerBaseCenter=controller.center;capturedControllerCenter=true;}controller.center=controllerBaseCenter+Vector3.up*(id==HeroId.Timmy?(TimmyBalance.CollisionHeight-HeroCollisionHeight)*.5f:0f);}
CapsuleCollider capsule=GetComponent<CapsuleCollider>();
if(capsule!=null){capsule.height=id==HeroId.Timmy?TimmyBalance.CollisionHeight:HeroCollisionHeight;capsule.radius=id==HeroId.Timmy?TimmyBalance.CollisionRadius:HeroCollisionRadius;if(!capturedCapsuleCenter){capsuleBaseCenter=capsule.center;capturedCapsuleCenter=true;}capsule.center=capsuleBaseCenter+Vector3.up*(id==HeroId.Timmy?(TimmyBalance.CollisionHeight-HeroCollisionHeight)*.5f:0f);}
// Tahla/Mira is procedurally assembled by StylizedHeroVisual. Install her dedicated visual
// interceptor before that component builds so side mantle panels can be omitted before batching
// and the feminine face polish is applied consistently in matches and menu previews.
if(id==HeroId.Mira&&GetComponent<Alterverse.Visuals.TahlaHealerVisualPolish>()==null)
gameObject.AddComponent<Alterverse.Visuals.TahlaHealerVisualPolish>();
// HeroVisualMotion owns the base gait/guard. The dedicated melee overlay may replace only the
// short basic-attack pose after that base has been evaluated.
}
private void Start()
{
if(hero==HeroId.Bolt7)
{
// Keep the shared player range-preview UI while the mech owns its dedicated animation rig.
if(GetComponent<PlayerHeroAbilities>()!=null&&GetComponent<Alterverse.UI.AbilityRangePreviewOverlay>()==null)
gameObject.AddComponent<Alterverse.UI.AbilityRangePreviewOverlay>();
return;
}
if(hero==HeroId.VoidKnight&&GetComponent<VoidKnightState>()==null)gameObject.AddComponent<VoidKnightState>();
// Configure is intentionally called before several combat/visual components are added by the runtime roster.
// Install the universal attack-presentation layer in Start so it can resolve Health, TeamMember,
// BasicAttack, HeroAbilityExecution and HeroVisualMotion immediately and track the real cast targets cleanly.
if(GetComponent<Alterverse.Visuals.HeroAttackReadabilityPresentation>()==null)
gameObject.AddComponent<Alterverse.Visuals.HeroAttackReadabilityPresentation>();
// Melee basics use a dedicated weapon/fist delivery layer: visible draw-back, contact and follow-through.
// Classification comes from the combat profile so all current short-range heroes receive it automatically.
if(!Alterverse.Combat.HeroAttackDelivery.IsRanged(hero)&&GetComponent<Alterverse.Visuals.MeleeBasicAttackAnimation>()==null)
gameObject.AddComponent<Alterverse.Visuals.MeleeBasicAttackAnimation>();
// Only actors that actually own the ability execution runtime need the extra mobile-readable cast cue.
// This keeps non-combat replicas/lightweight presentation actors free of an unnecessary Update loop.
if(GetComponent<HeroAbilityExecution>()!=null&&GetComponent<Alterverse.Visuals.HeroAbilityReadabilityBoost>()==null)
gameObject.AddComponent<Alterverse.Visuals.HeroAbilityReadabilityBoost>();
// Only player-controlled heroes need the hold-to-preview overlay. It observes touch/mouse input
// without consuming it; PrototypeHUD remains the sole owner of actually casting abilities.
if(GetComponent<PlayerHeroAbilities>()!=null&&GetComponent<Alterverse.UI.AbilityRangePreviewOverlay>()==null)
gameObject.AddComponent<Alterverse.UI.AbilityRangePreviewOverlay>();
}
public static string NameFor(HeroId id)=>id switch{HeroId.Bolt7=>"BOLT-7",HeroId.Veyr=>"Zhaev Null",HeroId.Kaelon=>"Auren Kest",HeroId.Nyra=>"Sivra Quell",HeroId.Elyra=>"Ione Vaska",HeroId.Mira=>"Tahla Venn",HeroId.Rook=>"Brakk Orun",HeroId.Traveler=>"THE TRAVELER",HeroId.Nerok=>"Nerok Abyss",HeroId.Timmy=>"Andromeda Timmy",HeroId.VoidKnight=>"Void Knight",_=>"Unknown Hero"};
public static HeroRole RoleFor(HeroId id)=>id switch{HeroId.Bolt7=>HeroRole.Bruiser,HeroId.VoidKnight=>HeroRole.Bruiser,HeroId.Veyr=>HeroRole.Assassin,HeroId.Timmy=>HeroRole.Assassin,HeroId.Kaelon=>HeroRole.Tank,HeroId.Nyra=>HeroRole.Marksman,HeroId.Elyra=>HeroRole.Mage,HeroId.Mira=>HeroRole.Support,HeroId.Rook=>HeroRole.Bruiser,HeroId.Traveler=>HeroRole.Skirmisher,HeroId.Nerok=>HeroRole.Bruiser,_=>HeroRole.Skirmisher};
// Preserve the existing 15% durability adjustment and add 5% to current base HP.
// The independent universal hero shield is configured by Health.
public const float GlobalHealthMultiplier=1.15f*1.05f;
// THE TRAVELER receives an additional 5% hero-specific health increase on top of the global multiplier.
// Sivra's 185 HP and Ione's 200 HP are final in-game maxima.
public static float BaseHealth(HeroId id)=>id==HeroId.Bolt7?Bolt7Balance.Health:id==HeroId.VoidKnight?VoidKnightBalance.RawHealth*GlobalHealthMultiplier+VoidKnightBalance.BonusHealth:id==HeroId.Nyra?185f:id==HeroId.Elyra?200f:
    (id switch{HeroId.Veyr=>200f,HeroId.Timmy=>TimmyBalance.RawHealth,HeroId.Kaelon=>380f*1.05f,HeroId.Mira=>230f,HeroId.Rook=>300f,HeroId.Traveler=>205f*1.05f,HeroId.Nerok=>285f,_=>200f})*GlobalHealthMultiplier;
public static float AttackDamage(HeroId id)=>id switch{HeroId.Bolt7=>Bolt7Balance.BasicDamage,HeroId.VoidKnight=>VoidKnightBalance.BasicDamage,HeroId.Veyr=>22f,HeroId.Timmy=>TimmyBalance.BasicDamage,HeroId.Kaelon=>14f,HeroId.Nyra=>13f,HeroId.Elyra=>15f,HeroId.Mira=>15f,HeroId.Rook=>24f,HeroId.Traveler=>15f,HeroId.Nerok=>24f,_=>17f};
}
}

