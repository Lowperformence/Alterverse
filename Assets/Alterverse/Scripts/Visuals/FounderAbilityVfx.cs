using Alterverse.Characters;using Alterverse.Combat;using Alterverse.Shop;using UnityEngine;
namespace Alterverse.Visuals
{
public static class FounderAbilityVfx
{
private static readonly Color Zero=new Color(.48f,.12f,.95f),Lumen=new Color(.05f,.9f,1f),Gold=new Color(1f,.72f,.18f);
public static void CastSignature(HeroIdentity hero,int slot,Vector3 origin,Vector3 impact){if(!FounderEntitlement.Owned||hero==null||hero.GetComponent<PlayerHeroAbilities>()==null)return;float size=slot==3?1.15f:.65f;PrototypeVfx.Ring(origin,slot%2==0?Zero:Lumen,slot==3?1.8f:1f,.22f);PrototypeVfx.Burst(impact,Gold,size,slot==3?9:4);if(slot==3){PrototypeVfx.Ring(impact,Zero,2.7f,.42f);PrototypeVfx.Ring(impact+Vector3.up*.04f,Lumen,2.15f,.36f);}}
}
}
