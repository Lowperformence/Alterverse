using System;
using System.Collections.Generic;
using System.Linq;
using Alterverse.Shop;
using UnityEngine;

namespace UnityEngine {public static class Time {public static float unscaledTime;}}
namespace Unity.Collections
{
    public enum Allocator {Temp}
    public struct FixedString128Bytes
    {readonly string value;public FixedString128Bytes(string v){value=v;}public override string ToString()=>value;}
}
namespace Unity.Netcode
{
    public enum NetworkDelivery {ReliableSequenced}
    public sealed class FastBufferWriter:IDisposable
    {
        public readonly List<object> Values=new();public FastBufferWriter(int capacity,Unity.Collections.Allocator allocator){}
        public void WriteValueSafe<T>(T value)=>Values.Add(value);public void Dispose(){}
    }
    public struct FastBufferReader
    {
        readonly object[] values;int index;
        public FastBufferReader(object[] values){this.values=values;index=0;}
        public void ReadValueSafe<T>(out T value)
        {if(index>=values.Length)throw new OverflowException();value=(T)values[index++];}
    }
    public sealed class CustomMessagingManager
    {
        public readonly Dictionary<string,Action<ulong,FastBufferReader>> Handlers=new();
        public readonly List<(string Name,ulong Peer,object[] Values)> Sent=new();
        public void RegisterNamedMessageHandler(string name,Action<ulong,FastBufferReader> handler)=>Handlers.Add(name,handler);
        public void UnregisterNamedMessageHandler(string name)=>Handlers.Remove(name);
        public void SendNamedMessage(string name,ulong peer,FastBufferWriter writer,NetworkDelivery delivery)=>Sent.Add((name,peer,writer.Values.ToArray()));
    }
    public sealed class NetworkManager
    {
        public static NetworkManager Singleton;public const ulong ServerClientId=0;
        public bool IsServer;public readonly List<ulong> ConnectedClientsIds=new(){0,7,8};
    }
}
namespace Alterverse.Multiplayer
{
    public static class MatchSession {public const int MaxPlayers=8;}
    public static class CoopAuthority {public static bool Active=true,LocalHasServerAuthority;public static object ActivePlan=new();}
    public static class CoopPlanHandshake {public static uint Round=123;public static uint Fingerprint(object plan)=>Round;}
    public static class MatchNetworkProtocol
    {
        public static bool ReadRound(ref Unity.Netcode.FastBufferReader reader)
        {reader.ReadValueSafe(out uint round);return round==CoopPlanHandshake.Round;}
    }
    public sealed class CoopHeroSlot:Component {}
    // Slot/owner authorization are the existing transport boundary, tested by its own suite.
    // This fixture compiles the complete new production cosmetic message implementation.
    public sealed partial class CoopHeroStateSync
    {
        readonly Unity.Netcode.CustomMessagingManager messaging=new();
        readonly CoopHeroSlot[] slots=new CoopHeroSlot[MatchSession.MaxPlayers];
        int localSlot=-1;string localPlayerId="player";
        bool ValidateOwner(ulong sender,int slot,string player)=>sender==7&&slot==1&&player=="player";
        void RefreshSlots(bool immediate){}
        public void Initialize()=>RegisterHeadwear();
        public void Stop()=>UnregisterHeadwear();
        public Unity.Netcode.CustomMessagingManager Messages=>messaging;
        public void Put(int slot,GameObject actor,bool local=false)
        {slots[slot]=actor.GetComponent<CoopHeroSlot>()??actor.AddComponent<CoopHeroSlot>();if(local)localSlot=slot;}
        public UniversalHeroCosmetic Value(int slot)=>roundHeadwear[slot];
        public void Tick(Unity.Netcode.NetworkManager manager)=>TickHeadwear(manager);
        public void Request(ulong sender,params object[] values)=>OnHeadwearRequest(sender,new Unity.Netcode.FastBufferReader(values));
        public void State(ulong sender,params object[] values)=>OnHeadwearState(sender,new Unity.Netcode.FastBufferReader(values));
    }
}
