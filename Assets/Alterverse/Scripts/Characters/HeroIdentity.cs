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
CharacterController controller=GetComponent<CharacterController>();
if(controller!=null){controller.height=id==HeroId.Timmy?TimmyBalance.CollisionHeight:HeroCollisionHeight;controller.radius=id==HeroId.Timmy?TimmyBalance.CollisionRadius:HeroCollisionRadius;if(!capturedControllerCenter){controllerBaseCenter=controller.center;capturedControllerCenter=true;}controller.center=controllerBaseCenter+Vector3.up*(id==HeroId.Timmy?(TimmyBalance.CollisionHeight-HeroCollisionHeight)*.5f:0f);}
CapsuleCollider capsule=GetComponent<CapsuleCollider>();
if(capsule!=null){capsule.height=id==HeroId.Timmy?TimmyBalance.CollisionHeight:HeroCollisionHeight;capsule.radius=id==HeroId.Timmy?TimmyBalance.CollisionRadius:HeroCollisionRadius;if(!capturedCapsuleCenter){capsuleBaseCenter=capsule.center;capturedCapsuleCenter=true;}capsule.center=capsuleBaseCenter+Vector3.up*(id==HeroId.Timmy?(TimmyBalance.CollisionHeight-HeroCollisionHeight)*.5f:0f);}
if(id==HeroId.Mira&&GetComponent<Alterverse.Visuals.TahlaHealerVisualPolish>()==null)
gameObject.AddComponent<Alterverse.Visuals.TahlaHealerVisualPolish>();
}
private void Start()
{
if(GetComponent<Alterverse.Combat.Health>()!=null&&GetComponent<Alterverse.Visuals.CosmicNebulaConcealment>()==null)gameObject.AddComponent<Alterverse.Visuals.CosmicNebulaConcealment>();
if(hero==HeroId.Bolt7)
{
if(GetComponent<PlayerHeroAbilities>()!=null&&GetComponent<Alterverse.UI.AbilityRangePreviewOverlay>()==null)
gameObject.AddComponent<Alterverse.UI.AbilityRangePreviewOverlay>();
return;
}
if(hero==HeroId.VoidKnight&&GetComponent<VoidKnightState>()==null)gameObject.AddComponent<VoidKnightState>();
if(GetComponent<Alterverse.Visuals.HeroAttackReadabilityPresentation>()==null)
gameObject.AddComponent<Alterverse.Visuals.HeroAttackReadabilityPresentation>();
if(!Alterverse.Combat.HeroAttackDelivery.IsRanged(hero)&&GetComponent<Alterverse.Visuals.MeleeBasicAttackAnimation>()==null)
gameObject.AddComponent<Alterverse.Visuals.MeleeBasicAttackAnimation>();
if(GetComponent<HeroAbilityExecution>()!=null&&GetComponent<Alterverse.Visuals.HeroAbilityReadabilityBoost>()==null)
gameObject.AddComponent<Alterverse.Visuals.HeroAbilityReadabilityBoost>();
if(GetComponent<PlayerHeroAbilities>()!=null&&GetComponent<Alterverse.UI.AbilityRangePreviewOverlay>()==null)
gameObject.AddComponent<Alterverse.UI.AbilityRangePreviewOverlay>();
}
public static string NameFor(HeroId id)=>id switch{HeroId.Bolt7=>"BOLT-7",HeroId.Veyr=>"Zhaev Null",HeroId.Kaelon=>"Auren Kest",HeroId.Nyra=>"Sivra Quell",HeroId.Elyra=>"Ione Vaska",HeroId.Mira=>"Tahla Venn",HeroId.Rook=>"Brakk Orun",HeroId.Traveler=>"THE TRAVELER",HeroId.Nerok=>"Nerok Abyss",HeroId.Timmy=>"Andromeda Timmy",HeroId.VoidKnight=>"Void Knight",_=>"Unknown Hero"};
public static HeroRole RoleFor(HeroId id)=>id switch{HeroId.Bolt7=>HeroRole.Bruiser,HeroId.VoidKnight=>HeroRole.Bruiser,HeroId.Veyr=>HeroRole.Assassin,HeroId.Timmy=>HeroRole.Assassin,HeroId.Kaelon=>HeroRole.Tank,HeroId.Nyra=>HeroRole.Marksman,HeroId.Elyra=>HeroRole.Mage,HeroId.Mira=>HeroRole.Support,HeroId.Rook=>HeroRole.Bruiser,HeroId.Traveler=>HeroRole.Skirmisher,HeroId.Nerok=>HeroRole.Bruiser,_=>HeroRole.Skirmisher};
public const float GlobalHealthMultiplier=1.15f*1.05f;
public static float BaseHealth(HeroId id)
{
    float raw = id switch
    {
        HeroId.Bolt7 => Bolt7Balance.Health,
        HeroId.VoidKnight => VoidKnightBalance.RawHealth * GlobalHealthMultiplier + VoidKnightBalance.BonusHealth,
        HeroId.Nyra => 185f,
        HeroId.Elyra => 200f,
        HeroId.Veyr => 200f * GlobalHealthMultiplier,
        HeroId.Timmy => TimmyBalance.RawHealth * GlobalHealthMultiplier,
        HeroId.Kaelon => (380f * 1.05f) * GlobalHealthMultiplier,
        HeroId.Mira => 230f * GlobalHealthMultiplier,
        HeroId.Rook => 300f * GlobalHealthMultiplier,
        HeroId.Traveler => (205f * 1.05f) * GlobalHealthMultiplier,
        HeroId.Nerok => 285f * GlobalHealthMultiplier,
        _ => 200f * GlobalHealthMultiplier
    };
    return raw + 50f;
}
public static float AttackDamage(HeroId id)=>id switch{HeroId.Bolt7=>Bolt7Balance.BasicDamage,HeroId.VoidKnight=>VoidKnightBalance.BasicDamage,HeroId.Veyr=>22f,HeroId.Timmy=>TimmyBalance.BasicDamage,HeroId.Kaelon=>14f,HeroId.Nyra=>13f,HeroId.Elyra=>15f,HeroId.Mira=>15f,HeroId.Rook=>24f,HeroId.Traveler=>15f,HeroId.Nerok=>24f,_=>17f};
}
}