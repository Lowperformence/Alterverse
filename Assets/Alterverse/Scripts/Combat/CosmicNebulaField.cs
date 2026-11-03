using Alterverse.Characters;
using Alterverse.Multiplayer;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
namespace Alterverse.Combat
{
    public sealed class CosmicNebulaField:MonoBehaviour
    {
        private const string RevealMessage="DF_NEBULA_REVEAL_V1";
        private static CosmicNebulaField instance;
        private CosmicNebulaLayout.Zone[] zones;
        private CustomMessagingManager messaging;
        private Material fogMaterial;
        private void Awake()
        {
            instance=this;
            string seed=CoopAuthority.ActivePlan?.MatchId??OfflineLobbyRoster.MatchId??System.Guid.NewGuid().ToString("N");
            zones=CosmicNebulaLayout.Create(seed);
            if(!Application.isBatchMode)CreateClouds();
            Attach();
        }
        private void Update()=>Attach();
        private void Attach()
        {
            var manager=NetworkManager.Singleton;
            var next=CoopAuthority.Active&&manager!=null&&manager.IsListening?manager.CustomMessagingManager:null;
            if(next==messaging)return;
            if(messaging!=null)messaging.UnregisterNamedMessageHandler(RevealMessage);
            messaging=next;
            if(messaging!=null)messaging.RegisterNamedMessageHandler(RevealMessage,OnReveal);
        }
        private int At(Vector3 point)
        {
            for(int i=0;i<zones.Length;i++)if(zones[i].Contains(point.x,point.z))return i;
            return -1;
        }
        // Shown to the owner and allies only while enemies outside the cloud cannot see this hero.
        public static bool IsConcealed(Health target)
            =>instance!=null&&target!=null&&!target.IsDead&&target.GetComponent<HeroIdentity>()!=null&&
              instance.At(target.transform.position)>=0&&(target.GetComponent<CosmicNebulaState>()?.RevealRemaining??0)<=0;
        public static bool HiddenFrom(GameObject observer,Health target)
        {
            if(instance==null||observer==null||target==null||target.IsDead||observer==target.gameObject||
                target.GetComponent<HeroIdentity>()==null)return false;
            var viewer=observer.GetComponent<TeamMember>();var owner=target.GetComponent<TeamMember>();
            int viewerTeam=viewer!=null?viewer.Team:observer.GetComponent<LaneMinion>()?.Team??observer.GetComponent<LaneTower>()?.Team??-1;
            if(viewerTeam<0||owner==null||viewerTeam==owner.Team)return false;
            int cloud=instance.At(target.transform.position);
            return CosmicNebulaLayout.Concealed(true,cloud>=0,cloud>=0&&instance.At(observer.transform.position)==cloud,
                target.GetComponent<CosmicNebulaState>()?.RevealRemaining??0);
        }
        public static void BroadcastReveal(GameObject actor)
        {
            var manager=NetworkManager.Singleton;
            if(instance==null||instance.messaging==null||manager==null||!manager.IsServer||!CoopAuthority.Active)return;
            var slot=actor.GetComponent<CoopHeroSlot>();
            if(slot==null||slot.Slot<0||slot.Slot>=MatchSession.MaxPlayers)return;
            using var writer=new FastBufferWriter(32,Allocator.Temp);
            writer.WriteValueSafe(slot.Slot);writer.WriteValueSafe(manager.ServerTime.Time);writer.WriteValueSafe(2f);
            foreach(ulong client in manager.ConnectedClientsIds)if(client!=NetworkManager.ServerClientId)
                instance.messaging.SendNamedMessage(RevealMessage,client,writer,NetworkDelivery.ReliableSequenced);
        }
        private void OnReveal(ulong sender,FastBufferReader reader)
        {
            var manager=NetworkManager.Singleton;
            if(manager==null||manager.IsServer||sender!=NetworkManager.ServerClientId||reader.Length<16)return;
            reader.ReadValueSafe(out int index);reader.ReadValueSafe(out double sample);reader.ReadValueSafe(out float remaining);
            if(index<0||index>=MatchSession.MaxPlayers)return;
            foreach(var slot in FindObjectsByType<CoopHeroSlot>(FindObjectsInactive.Exclude))
                if(slot.Slot==index)
                {(slot.GetComponent<CosmicNebulaState>()??slot.gameObject.AddComponent<CosmicNebulaState>()).Apply(sample,manager.ServerTime.Time,remaining);break;}
        }
        private void CreateClouds()
        {
            Shader shader=Resources.Load<Shader>("CombatVfx/CosmicNebula");
            if(shader==null)return;
            fogMaterial=new Material(shader){name="Cosmic nebula"};
            foreach(var zone in zones)
            {
                var go=new GameObject("DF_CosmicNebula");go.transform.SetParent(transform,false);
                go.transform.position=new Vector3(zone.X,.8f,zone.Z);
                var particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=particles.main;main.playOnAwake=false;main.loop=true;main.duration=5;main.startLifetime=5;
                main.startSpeed=.08f;main.startSize=zone.Radius*1.2f;main.maxParticles=24;
                main.startColor=new ParticleSystem.MinMaxGradient(new Color(.18f,.38f,.8f,.22f),new Color(.53f,.22f,.7f,.28f));
                var emission=particles.emission;emission.rateOverTime=4;
                var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=zone.Radius*.65f;
                shape.rotation=new Vector3(90,0,0);
                var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=fogMaterial;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                particles.Simulate(5,true,true);particles.Play();
            }
        }
        private void OnDestroy()
        {
            if(messaging!=null)messaging.UnregisterNamedMessageHandler(RevealMessage);
            if(fogMaterial!=null)Destroy(fogMaterial);
            if(instance==this)instance=null;
        }
    }
}
