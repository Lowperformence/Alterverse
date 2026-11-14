using System;
using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.Visuals;
using UnityEngine;

namespace UnityEngine
{
    public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 zero=>new();}
    public class Transform {public Vector3 position;}
    public class GameObject {public readonly Transform transform=new();}
}
namespace Alterverse.Combat
{
    public class Health
    {
        public bool IsDead;public float Current=100,Shield,Received;public readonly Transform transform=new();public GameObject Source;
        public void TakeDamage(float damage,GameObject source){Received+=damage;Source=source;}
    }
}
namespace Alterverse.Visuals
{
    public static class Bolt7Vfx {public static void Basic(GameObject actor,Vector3 target,float duration){}public static void BasicImpact(GameObject actor,Vector3 target){}}
}
namespace Alterverse.Multiplayer
{
    public static class CoopAuthority {public static bool Active=true,LocalHasServerAuthority;}
    public class CoopWorldEntityKey {public string Key;}
}
namespace Alterverse.UI
{
    public class KillstreakBoltTurret
    {
        public const float AttackDamage=1f;
        private static readonly List<KillstreakBoltTurret> active=new();
        public static event Action<KillstreakBoltTurret,Vector3> NetworkShot;
        public readonly GameObject gameObject=new();
        private GameObject muzzleActor,ownerHero;
        private float recoilOffset;
        private bool mobile;
        private CoopWorldEntityKey key;
        public static int Spawns;
        public static Vector3 LastPosition;
        public static bool LastMobile;
        public T GetComponent<T>() where T:class=>key as T;
        public static KillstreakBoltTurret Spawn(int team,Vector3 position,GameObject owner,bool mobile=false,string networkKey=null)
        {
            Spawns++;LastPosition=position;LastMobile=mobile;
            var turret=new KillstreakBoltTurret{mobile=mobile,ownerHero=owner,key=new CoopWorldEntityKey{Key=networkKey}};
            active.Add(turret);return turret;
        }
        public void TestFire(Health victim)=>FireBoltBasicAttack(victim);
        /*PRODUCTION_METHODS*/
    }
}
static class ReplicaRegression
{
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Main()
    {
        var pos=new Vector3(3,0,9);
        Check(Alterverse.UI.KillstreakBoltTurret.EnsureNetworkReplica("not-a-turret",pos)==null,"unknown entity key rejected");
        Check(Alterverse.UI.KillstreakBoltTurret.EnsureNetworkReplica("KST:2:1:4",pos)==null,"invalid team rejected");
        Check(Alterverse.UI.KillstreakBoltTurret.EnsureNetworkReplica("KST:0:3:4",pos)==null,"invalid variant rejected");
        Check(Alterverse.UI.KillstreakBoltTurret.EnsureNetworkReplica("KST:0:1:0",pos)==null,"invalid sequence rejected");
        var mobile=Alterverse.UI.KillstreakBoltTurret.EnsureNetworkReplica("KST:0:1:4",pos);
        Check(mobile!=null&&Alterverse.UI.KillstreakBoltTurret.LastMobile&&Alterverse.UI.KillstreakBoltTurret.LastPosition.z==9,"mobile replica at snapshot location");
        Check(ReferenceEquals(mobile,Alterverse.UI.KillstreakBoltTurret.EnsureNetworkReplica("KST:0:1:4",pos))&&Alterverse.UI.KillstreakBoltTurret.Spawns==1,"duplicate discovery is idempotent");
        var normal=Alterverse.UI.KillstreakBoltTurret.EnsureNetworkReplica("KST:1:0:5",pos);
        Check(normal!=null&&!Alterverse.UI.KillstreakBoltTurret.LastMobile,"standard replica has standard variant");
        CoopAuthority.LocalHasServerAuthority=true;
        Check(Alterverse.UI.KillstreakBoltTurret.EnsureNetworkReplica("KST:0:1:8",pos)==null,"server cannot create a replica");
        var victim=new Health();normal.TestFire(victim);Check(victim.Received==1,"standard turret deals one damage");
        mobile.TestFire(victim);Check(victim.Received==3,"mobile turret deals twice the standard damage");
        var owner=new GameObject();var owned=Alterverse.UI.KillstreakBoltTurret.Spawn(0,pos,owner,true);
        var lethal=new Health{Current=2};owned.TestFire(lethal);
        Check(ReferenceEquals(lethal.Source,owner),"lethal hit credits original owner");
        Console.WriteLine("PASS network replica identity, variants and damage checks");
    }
}
