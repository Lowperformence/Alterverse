using UnityEngine;
namespace Alterverse.Characters
{
public enum HeroId{Veyr,Kaelon,Nyra,Elyra,Mira,Rook,Traveler}
public enum HeroRole{Assassin,Tank,Marksman,Mage,Support,Bruiser,Skirmisher}
public sealed class HeroIdentity:MonoBehaviour
{
public const int HeroCount=7;
[SerializeField]private HeroId hero;[SerializeField]private HeroRole role;public HeroId Hero=>hero;public HeroRole Role=>role;public string DisplayName=>NameFor(hero);
public void Configure(HeroId id){gameObject.layer=Alterverse.Combat.GameplayCollision.Units;hero=id;role=RoleFor(id);}
public static string NameFor(HeroId id)=>id switch{HeroId.Veyr=>"Zhaev Null",HeroId.Kaelon=>"Auren Kest",HeroId.Nyra=>"Sivra Quell",HeroId.Elyra=>"Ione Vaska",HeroId.Mira=>"Tahla Venn",HeroId.Rook=>"Brakk Orun",HeroId.Traveler=>"THE TRAVELER",_=>"Unknown Hero"};
public static HeroRole RoleFor(HeroId id)=>id switch{HeroId.Veyr=>HeroRole.Assassin,HeroId.Kaelon=>HeroRole.Tank,HeroId.Nyra=>HeroRole.Marksman,HeroId.Elyra=>HeroRole.Mage,HeroId.Mira=>HeroRole.Support,HeroId.Rook=>HeroRole.Bruiser,HeroId.Traveler=>HeroRole.Skirmisher,_=>HeroRole.Skirmisher};
public const float GlobalHealthMultiplier=1.00f;
public static float BaseHealth(HeroId id)=>(id switch{HeroId.Veyr=>200f,HeroId.Kaelon=>380f,HeroId.Nyra=>160f,HeroId.Elyra=>190f,HeroId.Mira=>230f,HeroId.Rook=>300f,HeroId.Traveler=>205f,_=>200f})*GlobalHealthMultiplier;
public static float AttackDamage(HeroId id)=>id switch{HeroId.Veyr=>24f,HeroId.Kaelon=>14f,HeroId.Nyra=>22f,HeroId.Elyra=>15f,HeroId.Mira=>16f,HeroId.Rook=>24f,HeroId.Traveler=>17f,_=>17f};
}
}
