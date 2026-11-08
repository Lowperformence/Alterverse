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

namespace Alterverse.Characters {public class TrenchSoldier:UnityEngine.MonoBehaviour {}}

namespace Alterverse.Match
{
    public class MatchTelemetry
    {public static MatchTelemetry Instance;public void RecordHealing(Health healer,float amount){}}
    public class MatchScoreboard
    {public static MatchScoreboard Instance;public void RecordSupport(Component target,GameObject source){}}
}

namespace Alterverse.UI
{
    public static class MainMenuController {public static bool IsVisible;}
    public static class FrontEndTypography {public static object Display;}
    public static class CombatGlyphs
    {
        public static UnityEngine.Vector2 LastCenter;public static int Rings;
        public static void Ring(UnityEngine.Vector2 center,float radius,float width,UnityEngine.Color tint,int segments){LastCenter=center;Rings++;}
        public static void Line(UnityEngine.Vector2 a,UnityEngine.Vector2 b,float width,UnityEngine.Color tint){}
    }
}
