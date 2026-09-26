using UnityEngine;
namespace Alterverse.Characters
{
public enum HeroId{Veyr,Kaelon,Nyra,Elyra,Mira,Rook,Traveler}
public enum HeroRole{Assassin,Tank,Marksman,Mage,Support,Bruiser,Skirmisher}
public sealed class HeroIdentity:MonoBehaviour
{
public const int HeroCount=7;
public const float HeroCollisionRadius=.40f;
public const float HeroCollisionHeight=2f;
[SerializeField]private HeroId hero;[SerializeField]private HeroRole role;public HeroId Hero=>hero;public HeroRole Role=>role;public string DisplayName=>NameFor(hero);
public void Configure(HeroId id)
{
gameObject.layer=Alterverse.Combat.GameplayCollision.Units;hero=id;role=RoleFor(id);
// Keep hero combat hitboxes slightly tighter than the visuals so dodges and close passes feel clean.
// Unit-vs-unit hard collision is already disabled; this also makes targeting volumes less oversized.
CharacterController controller=GetComponent<CharacterController>();
if(controller!=null){controller.height=HeroCollisionHeight;controller.radius=HeroCollisionRadius;}
CapsuleCollider capsule=GetComponent<CapsuleCollider>();
if(capsule!=null){capsule.height=HeroCollisionHeight;capsule.radius=HeroCollisionRadius;}
// Tahla/Mira is procedurally assembled by StylizedHeroVisual. Install her dedicated visual
// interceptor before that component builds so side mantle panels can be omitted before batching
// and the feminine face polish is applied consistently in matches and menu previews.
if(id==HeroId.Mira&&GetComponent<Alterverse.Visuals.TahlaHealerVisualPolish>()==null)
gameObject.AddComponent<Alterverse.Visuals.TahlaHealerVisualPolish>();
// Zhaev's presentation-only sword polish reshapes the visible strike after HeroVisualMotion.
// It never changes damage, range, cooldowns or hit timing.
if(id==HeroId.Veyr&&GetComponent<Alterverse.Visuals.VeyrAttackPresentation>()==null)
gameObject.AddComponent<Alterverse.Visuals.VeyrAttackPresentation>();
}
private void Start()
{
// Configure is intentionally called before several combat/visual components are added by the runtime roster.
// Install the universal attack-presentation layer in Start so it can resolve Health, TeamMember,
// BasicAttack, HeroAbilityExecution and HeroVisualMotion immediately and track the real cast targets cleanly.
if(GetComponent<Alterverse.Visuals.HeroAttackReadabilityPresentation>()==null)
gameObject.AddComponent<Alterverse.Visuals.HeroAttackReadabilityPresentation>();
// Only actors that actually own the ability execution runtime need the extra mobile-readable cast cue.
// This keeps non-combat replicas/lightweight presentation actors free of an unnecessary Update loop.
if(GetComponent<HeroAbilityExecution>()!=null&&GetComponent<Alterverse.Visuals.HeroAbilityReadabilityBoost>()==null)
gameObject.AddComponent<Alterverse.Visuals.HeroAbilityReadabilityBoost>();
// Only player-controlled heroes need the hold-to-preview overlay. It observes touch/mouse input
// without consuming it; PrototypeHUD remains the sole owner of actually casting abilities.
if(GetComponent<PlayerHeroAbilities>()!=null&&GetComponent<Alterverse.UI.AbilityRangePreviewOverlay>()==null)
gameObject.AddComponent<Alterverse.UI.AbilityRangePreviewOverlay>();
}
public static string NameFor(HeroId id)=>id switch{HeroId.Veyr=>"Zhaev Null",HeroId.Kaelon=>"Auren Kest",HeroId.Nyra=>"Sivra Quell",HeroId.Elyra=>"Ione Vaska",HeroId.Mira=>"Tahla Venn",HeroId.Rook=>"Brakk Orun",HeroId.Traveler=>"THE TRAVELER",_=>"Unknown Hero"};
public static HeroRole RoleFor(HeroId id)=>id switch{HeroId.Veyr=>HeroRole.Assassin,HeroId.Kaelon=>HeroRole.Tank,HeroId.Nyra=>HeroRole.Marksman,HeroId.Elyra=>HeroRole.Mage,HeroId.Mira=>HeroRole.Support,HeroId.Rook=>HeroRole.Bruiser,HeroId.Traveler=>HeroRole.Skirmisher,_=>HeroRole.Skirmisher};
// 15% more base health creates noticeably longer hero fights while preserving displayed damage
// values and keeping movement/rotations responsive. The existing universal 30 HP shield remains
// unchanged, so this does not stack another new sustain system on top.
public const float GlobalHealthMultiplier=1.15f;
public static float BaseHealth(HeroId id)=>(id switch{HeroId.Veyr=>200f,HeroId.Kaelon=>380f,HeroId.Nyra=>160f,HeroId.Elyra=>190f,HeroId.Mira=>230f,HeroId.Rook=>300f,HeroId.Traveler=>205f,_=>200f})*GlobalHealthMultiplier;
public static float AttackDamage(HeroId id)=>id switch{HeroId.Veyr=>24f,HeroId.Kaelon=>14f,HeroId.Nyra=>22f,HeroId.Elyra=>15f,HeroId.Mira=>16f,HeroId.Rook=>24f,HeroId.Traveler=>17f,_=>17f};
}
}
