using System;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.UI;
using UnityEngine;

public static class PrismRegression
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Near(float actual,float expected,string message)=>Check(Math.Abs(actual-expected)<.001f,message+" actual="+actual+" expected="+expected);
    static Health Hero(HeroId id,int team)
    {
        var actor=new GameObject(id.ToString());
        actor.AddComponent<HeroIdentity>().Hero=id;
        actor.AddComponent<TeamMember>().Team=team;
        var health=actor.AddComponent<Health>();health.Configure(200);
        actor.AddComponent<HeroAbilityKit>().Configure(id);
        actor.AddComponent<HeroAbilityExecution>();
        return health;
    }
    static void Shot(Health actor,Health target,int slot=0,bool volley=false)=>actor.GetComponent<HeroAbilityExecution>().Contact(slot,target,volley);
    static void Reset(){Time.time=0;CoopAuthority.CanMutateAuthoritativeState=true;CoopGameplayCommands.Damage=0;}
    public static void Main()
    {
        Reset();var sivra=Hero(HeroId.Nyra,0);var enemy=Hero(HeroId.Veyr,1);
        sivra.TakeDamage(50);Shot(sivra,enemy);
        Near(sivra.HeroBaseShield,25,"Restore exactly 15 base shield");
        Near(enemy.Shield,53,"Enemy loses 15 shield plus the original 32 damage");
        Near(sivra.Current,200,"Shield absorption never heals health");
        Near(enemy.Current,200,"Shielded target retains health");
        Near(CoopGameplayCommands.Damage,47,"Authoritative damage acknowledgement includes drain exactly once");
        Time.time=3;GameObject.Invoke(sivra,"Update");Near(sivra.Shield,25,"Refilled base shield does not expire");

        Reset();sivra=Hero(HeroId.Nyra,0);enemy=Hero(HeroId.Veyr,1);
        enemy.TakeDamage(95);sivra.TakeDamage(40);Shot(sivra,enemy);
        Near(sivra.Shield,25,"Only the target's remaining five shield can transfer");
        Near(enemy.Shield,0,"Partial shield cannot become negative");
        Near(enemy.Current,168,"Transfer occurs before the normal 32 damage");
        Near(CoopGameplayCommands.Damage,37,"Partial drain is not inflated to 15");

        Reset();sivra=Hero(HeroId.Nyra,0);enemy=Hero(HeroId.Veyr,1);
        enemy.TakeDamage(100);Shot(sivra,enemy);
        Near(sivra.Shield,60,"Unshielded enemy creates no free shield");
        Near(enemy.Current,168,"Unshielded enemy still takes the original damage");

        Reset();sivra=Hero(HeroId.Nyra,0);enemy=Hero(HeroId.Veyr,1);Shot(sivra,enemy);
        Near(sivra.HeroBaseShield,60,"Base capacity is unchanged");
        Near(sivra.TemporaryShield,15,"Full base shield receives 15 overflow");
        Time.time=2.999f;Near(sivra.Shield,75,"Overflow remains before three seconds");
        Time.time=3;Near(sivra.Shield,60,"Overflow expires at three seconds");

        Reset();sivra=Hero(HeroId.Nyra,0);enemy=Hero(HeroId.Veyr,1);sivra.TakeDamage(5);Shot(sivra,enemy);
        Near(sivra.HeroBaseShield,60,"Base shield is filled first");
        Near(sivra.TemporaryShield,10,"Only excess enters temporary shield");
        Near(sivra.Shield,70,"Mixed refill and overflow conserve 15 points");
        Time.time=3;Near(sivra.Shield,60,"Expiry retains the refilled base points");

        Reset();sivra=Hero(HeroId.Nyra,0);enemy=Hero(HeroId.Veyr,1);sivra.AddShield(40,10,false);Shot(sivra,enemy);
        Near(sivra.TemporaryShield,55,"Absorption adds to an existing shield instead of taking only its maximum");
        Time.time=3;Near(sivra.TemporaryShield,55,"Existing longer shield duration is not shortened");
        Time.time=10;Near(sivra.TemporaryShield,0,"Temporary pool still expires normally");

        Reset();sivra=Hero(HeroId.Nyra,0);enemy=Hero(HeroId.Veyr,1);sivra.AddShield(78,10,false);Shot(sivra,enemy);
        Near(sivra.TemporaryShield,80,"Existing temporary shield cap is respected");
        Near(enemy.Shield,66,"Only the two points actually received are siphoned");
        Shot(sivra,enemy);Near(sivra.Shield,140,"Repeated contact cannot overfill a capped recipient");
        Near(enemy.Shield,34,"A capped recipient does not destroy additional enemy shield");

        Reset();sivra=Hero(HeroId.Nyra,0);enemy=Hero(HeroId.Veyr,1);enemy.AddShield(10,10,false);Shot(sivra,enemy);
        Near(enemy.TemporaryShield,0,"Enemy temporary shield is consumed first");
        Near(enemy.HeroBaseShield,63,"Only remaining siphon and damage consume the base pool");
        Near(sivra.Shield,75,"Mixed enemy pools transfer exactly 15");

        Reset();sivra=Hero(HeroId.Nyra,0);enemy=Hero(HeroId.Veyr,1);enemy.DamageGate=()=>false;Shot(sivra,enemy);
        Near(enemy.Shield,100,"Invulnerable targets lose no shield");
        Near(sivra.Shield,60,"Rejected contact grants no shield");
        Near(CoopGameplayCommands.Damage,0,"Rejected hit records no authoritative damage");
        var ally=Hero(HeroId.Veyr,0);Shot(sivra,ally);Near(ally.Shield,100,"No friendly fire or allied siphon");
        Shot(sivra,null);Near(sivra.Shield,60,"Missed contact grants no shield");

        Reset();sivra=Hero(HeroId.Nyra,0);enemy=Hero(HeroId.Veyr,1);CoopAuthority.CanMutateAuthoritativeState=false;Shot(sivra,enemy);
        Near(enemy.Shield,100,"Replica cannot remove enemy shield");
        Near(sivra.Shield,60,"Replica cannot award shield");
        Near(CoopGameplayCommands.Damage,0,"Replica produces no siphon acknowledgement");
        CoopAuthority.CanMutateAuthoritativeState=true;
        sivra.TakeDamage(1000);Near(enemy.TransferShieldTo(sivra,15,3),0,"Dead recipient cannot absorb");
        enemy.TakeDamage(1000);var survivor=Hero(HeroId.Nyra,0);
        Near(enemy.TransferShieldTo(survivor,15,3),0,"Dead target cannot donate shield");

        Reset();sivra=Hero(HeroId.Nyra,0);enemy=Hero(HeroId.Veyr,1);Shot(sivra,enemy,2);
        Near(enemy.Shield,75,"Light Snare now deals 25");
        Near(sivra.Shield,60,"Light Snare does not acquire Prism Shot's absorb");
        Near(HeroCombatStatus.Slow,.7f,"Light Snare retains its 30 percent slow");
        Near(HeroCombatStatus.Seconds,1.8f,"Light Snare retains its duration");
        Reset();sivra=Hero(HeroId.Nyra,0);enemy=Hero(HeroId.Veyr,1);Shot(sivra,enemy,3,true);
        Near(enemy.Shield,100-80f/3,"Sightline Break divides 80 total damage across three shots");
        Near(sivra.Shield,60,"Ultimate volley does not siphon shield");
        Shot(sivra,enemy,3,true);Shot(sivra,enemy,3,true);Near(enemy.Shield,20,"Three Sightline Break shots deal exactly 80 total");
        var other=Hero(HeroId.Veyr,0);enemy=Hero(HeroId.Veyr,1);Shot(other,enemy);
        Near(enemy.Shield,62,"Other hero's skill damage is unchanged");

        Reset();sivra=Hero(HeroId.Nyra,0);enemy=Hero(HeroId.Veyr,1);
        Near(enemy.TransferShieldTo(sivra,15,3),15,"Direct transfer reports exact amount");
        Near(enemy.HeroBaseShieldRechargeRemaining,11.25f,"Siphon restarts enemy recharge delay");
        enemy.AddShield(20,4,false);Near(enemy.TemporaryShield,20,"New enemy shields work immediately after absorption");
        Time.time=11.249f;GameObject.Invoke(enemy,"Update");Near(enemy.HeroBaseShield,85,"No early shield recharge");
        Time.time=11.25f;GameObject.Invoke(enemy,"Update");Near(enemy.HeroBaseShield,100,"Normal enemy recharge remains intact");

        Reset();sivra=Hero(HeroId.Nyra,0);enemy=Hero(HeroId.Veyr,1);Shot(sivra,enemy);
        var replica=Hero(HeroId.Nyra,0);CoopAuthority.CanMutateAuthoritativeState=false;
        replica.ApplyNetworkSnapshot(sivra.Current,sivra.Shield,false);Near(replica.Shield,75,"Existing network shield snapshot preserves absorbed points");
        CoopAuthority.CanMutateAuthoritativeState=true;
        Reset();var ione=Hero(HeroId.Elyra,0);enemy=Hero(HeroId.Veyr,1);Shot(ione,enemy,1);
        Near(ione.TemporaryShield,20,"Invert Field absorbs exactly 20 shield on a full recipient");
        Near(enemy.Shield,54,"Invert transfer precedes unchanged 26 damage");
        Near(CoopGameplayCommands.Damage,46,"Invert drain and damage acknowledged once");
        Near(HeroCombatStatus.Slow,.65f,"Invert retains its slow");
        Time.time=3;Near(ione.TemporaryShield,0,"Invert overflow expires at three seconds");
        Reset();ione=Hero(HeroId.Elyra,0);enemy=Hero(HeroId.Veyr,1);enemy.TakeDamage(93);Shot(ione,enemy,1);
        Near(ione.TemporaryShield,7,"Only seven remaining shield points transfer");Near(enemy.Current,174,"Normal damage follows partial transfer");
        Reset();ione=Hero(HeroId.Elyra,0);enemy=Hero(HeroId.Veyr,1);enemy.TakeDamage(100);Shot(ione,enemy,1);
        Near(ione.Shield,80,"An unshielded target grants no Invert shield");
        Reset();ione=Hero(HeroId.Elyra,0);enemy=Hero(HeroId.Veyr,1);ione.TakeDamage(30);Shot(ione,enemy,1);
        Near(ione.HeroBaseShield,70,"Invert refills missing base shield first");Near(ione.TemporaryShield,0,"No overflow before base is full");
        Reset();ione=Hero(HeroId.Elyra,0);enemy=Hero(HeroId.Veyr,1);enemy.DamageGate=()=>false;Shot(ione,enemy,1);
        Near(ione.Shield,80,"Invulnerable contact grants no Invert shield");Near(enemy.Shield,100,"Invulnerable donor is unchanged");
        enemy.DamageGate=null;CoopAuthority.CanMutateAuthoritativeState=false;Shot(ione,enemy,1);
        Near(ione.Shield,80,"Replica cannot absorb Invert shield");CoopAuthority.CanMutateAuthoritativeState=true;
        Reset();ione=Hero(HeroId.Elyra,0);enemy=Hero(HeroId.Veyr,0);Shot(ione,enemy,1);Near(enemy.Shield,100,"Invert cannot drain allies");
        enemy=Hero(HeroId.Veyr,1);Shot(ione,enemy,0);Near(ione.Shield,80,"Ione Q does not acquire the W absorb");
        Check(HeroAttackCatalog.For(HeroId.Elyra)[2].Stats.Contains("20 SHIELD ABSORB"),"Invert card includes shield absorption");
        Check(HeroAttackCatalog.For(HeroId.Nyra)[4].Effect.Contains("80 total damage"),"Ultimate description follows the production damage value");
        var cards=HeroAttackCatalog.For(HeroId.Nyra);
        Check(cards[1].Effect.Contains("15")&&cards[1].Effect.Contains("3s"),"Ability card describes absorption and overflow duration");
        Check(cards[1].Stats.Contains("15 SHIELD ABSORB"),"Primary skill stats make the additional shield absorption visible");
        Check(cards[3].Stats.Contains("25 DMG"),"Light Snare card reads updated production damage");
        Near(HeroAbilityKit.LoadoutFor(HeroId.Nyra).Q.Power,32,"Prism Shot retains its normal 32 damage");
        Near(HeroAbilityKit.LoadoutFor(HeroId.Nyra).E.Cooldown,12,"Light Snare cooldown is unchanged");
        Console.WriteLine("PASS "+checks+" Prism Shot shield-transfer, damage-order, caps, rejection, authority, snapshot, recharge and Light Snare assertions. Unity/network transport not tested.");
    }
}

