using UnityEngine;
namespace Alterverse.Characters
{
public readonly struct AbilitySpec{public readonly string Name;public readonly float Cooldown,Power,Range;public AbilitySpec(string name,float cooldown,float power,float range){Name=name;Cooldown=cooldown;Power=power;Range=range;}}
public sealed class HeroAbilityKit:MonoBehaviour
{
public HeroId Hero{get;private set;}public AbilitySpec Passive{get;private set;}public AbilitySpec Q{get;private set;}public AbilitySpec W{get;private set;}public AbilitySpec E{get;private set;}public AbilitySpec Ultimate{get;private set;}
public void Configure(HeroId id){Hero=id;switch(id){case HeroId.Veyr:Passive=new("Seelenhunger",0,.18f,0);Q=new("Schattenstich",7,34,5.5f);W=new("Seelenriss",8,30,3.2f);E=new("Nullschritt",10,0,5.5f);Ultimate=new("Ende der Zukunft",42,70,5.5f);break;case HeroId.Kaelon:Passive=new("Resonanzpanzerung",0,.22f,0);Q=new("Dimensionsramme",9,24,5);W=new("Lumenwall",12,0,4);E=new("Gravitationsanker",11,18,5.5f);Ultimate=new("Realität halten",48,28,6);break;case HeroId.Nyra:Passive=new("Resonanztreffer",0,.16f,0);Q=new("Lichtlanze",6,32,9);W=new("Prismensprung",10,0,4.5f);E=new("Sonnenfessel",11,18,7);Ultimate=new("Tausend Horizonte",44,54,9);break;case HeroId.Elyra:Passive=new("Raumbruch",0,.15f,0);Q=new("Sternenfalte",7,30,7.5f);W=new("Gravitationsspalt",10,22,6);E=new("Phasenversatz",11,0,5);Ultimate=new("Ereignishorizont",50,62,7);break;case HeroId.Mira:Passive=new("Lebensband",0,.2f,0);Q=new("Verdant Impuls",6,20,6.5f);W=new("Schutzblüte",10,0,6);E=new("Wurzelbindung",11,15,6);Ultimate=new("Weltenhain",50,34,7);break;default:Passive=new("Schrottwut",0,.2f,0);Q=new("Brecherhieb",6,31,2.8f);W=new("Panzergriff",10,0,0);E=new("Trümmersturm",9,24,3.5f);Ultimate=new("Letzter Überlebender",46,48,4);break;}}
}
}
