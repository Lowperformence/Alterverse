using UnityEngine;
namespace Alterverse.Characters
{
public readonly struct AbilitySpec{public readonly string Name;public readonly float Cooldown,Power,Range;public AbilitySpec(string name,float cooldown,float power,float range){Name=name;Cooldown=cooldown;Power=power;Range=range;}}
public sealed class HeroAbilityKit:MonoBehaviour
{
public HeroId Hero{get;private set;}public AbilitySpec Passive{get;private set;}public AbilitySpec Q{get;private set;}public AbilitySpec W{get;private set;}public AbilitySpec E{get;private set;}public AbilitySpec Ultimate{get;private set;}
public void Configure(HeroId id){Hero=id;switch(id){case HeroId.Veyr:Passive=new("Soul Hunger",0,.18f,0);Q=new("Shadow Pierce",7,34,5.5f);W=new("Soul Rend",8,30,3.2f);E=new("Null Step",10,0,5.5f);Ultimate=new("Future's End",42,70,5.5f);break;case HeroId.Kaelon:Passive=new("Resonant Armor",0,.22f,0);Q=new("Dimension Ram",9,24,5);W=new("Lumen Wall",12,0,4);E=new("Gravity Anchor",11,18,5.5f);Ultimate=new("Hold Reality",48,28,6);break;case HeroId.Nyra:Passive=new("Resonance Shot",0,.16f,0);Q=new("Light Lance",6,32,9);W=new("Prism Vault",10,0,4.5f);E=new("Solar Tether",11,18,7);Ultimate=new("Thousand Horizons",44,54,9);break;case HeroId.Elyra:Passive=new("Spacebreak",0,.15f,0);Q=new("Starfold",7,30,7.5f);W=new("Gravity Rift",10,22,6);E=new("Phase Shift",11,0,5);Ultimate=new("Event Horizon",50,62,7);break;case HeroId.Mira:Passive=new("Lifebond",0,.2f,0);Q=new("Verdant Pulse",6,20,6.5f);W=new("Guardian Bloom",10,0,6);E=new("Rootbind",11,15,6);Ultimate=new("Worldgrove",50,34,7);break;default:Passive=new("Scrap Fury",0,.2f,0);Q=new("Breaker Fist",6,31,2.8f);W=new("Armor Grip",10,0,0);E=new("Wreckstorm",9,24,3.5f);Ultimate=new("Last Survivor",46,48,4);break;}}
}
}
