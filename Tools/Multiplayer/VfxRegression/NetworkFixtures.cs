using System;
using UnityEngine;
using Unity.Netcode;
using Alterverse.Combat;
using Alterverse.Visuals;

namespace Alterverse.Visuals
{
    public class VoidKnightPresentation:MonoBehaviour
    {public void BeginCounter(){}public void BeginBasic(float seconds){}}
}
namespace Alterverse.Multiplayer
{
    public sealed class CoopWorldEntityKey:MonoBehaviour
    {public string Key;public void Configure(){Key??=gameObject.name;}}
    public sealed partial class CoopHeroStateSync:MonoBehaviour
    {
        private static CoopHeroStateSync audioRelay;
        private CustomMessagingManager messaging;
        private readonly CoopHeroSlot[] slots=new CoopHeroSlot[MatchSession.MaxPlayers];
        public void Bind(CustomMessagingManager messages)
        {audioRelay=this;messaging=messages;HeroVfxEvents.Emitted+=OnHostHeroVfx;messages.RegisterNamedMessageHandler(HeroVfxMessage,OnHeroVfx);}
        public void Unbind(){if(audioRelay==this)audioRelay=null;HeroVfxEvents.Emitted-=OnHostHeroVfx;messaging.UnregisterNamedMessageHandler(HeroVfxMessage);}
        public void Slot(CoopHeroSlot slot){slots[slot.Slot]=slot;}
        public void Receive(ulong sender,byte[] bytes){OnHeroVfx(sender,new FastBufferReader(bytes));}
        public static int TimmyRelays,LegacyRelays;
        public static GameObject LastCombatSoundActor;
        public static void BroadcastTimmyEffect(GameObject a,int kind,Vector3 p,Vector3 q,float r,float d)
        {if(!HeroVfxEvents.InsideSemanticEffect)TimmyRelays++;}
        public static void BroadcastSharkEffect(GameObject a,int kind,Vector3 p,float r,float d,Health t=null)
        {if(!HeroVfxEvents.InsideSemanticEffect)LegacyRelays++;}
        public static void BroadcastOrunOverdrive(GameObject a,Vector3 p)
        {if(!HeroVfxEvents.InsideSemanticEffect)LegacyRelays++;}
        public static void BroadcastVoidKnightEffect(GameObject a,int kind,Vector3 p,Vector3 q,float r,float d)
        {if(!HeroVfxEvents.InsideSemanticEffect)LegacyRelays++;}
        public static void BroadcastCombatSound(GameObject a,int action,int phase,Vector3 p){LastCombatSoundActor=a;}
        public static void BroadcastKillSound(GameObject a){}
    }
}
