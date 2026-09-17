using UnityEngine;
namespace Alterverse.Characters
{
public enum HeroId{Veyr,Kaelon,Nyra,Elyra,Mira,Rook}
public enum HeroRole{Assassin,Tank,Marksman,Mage,Support,Bruiser}
public sealed class HeroIdentity:MonoBehaviour
{
[SerializeField]private HeroId hero;[SerializeField]private HeroRole role;
public HeroId Hero=>hero;public HeroRole Role=>role;public string DisplayName=>hero.ToString();
public void Configure(HeroId id){hero=id;role=RoleFor(id);gameObject.name=$"AV_{id}";}
public static HeroRole RoleFor(HeroId id)=>id switch{HeroId.Veyr=>HeroRole.Assassin,HeroId.Kaelon=>HeroRole.Tank,HeroId.Nyra=>HeroRole.Marksman,HeroId.Elyra=>HeroRole.Mage,HeroId.Mira=>HeroRole.Support,_=>HeroRole.Bruiser};
public static float BaseHealth(HeroId id)=>id switch{HeroId.Veyr=>140f,HeroId.Kaelon=>205f,HeroId.Nyra=>132f,HeroId.Elyra=>138f,HeroId.Mira=>150f,_=>185f};
public static float AttackDamage(HeroId id)=>id switch{HeroId.Veyr=>22f,HeroId.Kaelon=>15f,HeroId.Nyra=>20f,HeroId.Elyra=>17f,HeroId.Mira=>13f,_=>19f};
}
}
