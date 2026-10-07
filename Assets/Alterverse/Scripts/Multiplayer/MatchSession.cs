using System;using Alterverse.Characters;using Alterverse.Profile;
namespace Alterverse.Multiplayer
{
public enum SessionMode{LocalBots,OnlineCasual,OnlineRanked,Private}
[Serializable]public sealed class MatchPlayerSlot{public string PlayerId;public string DisplayName;public int Team;public HeroId Hero;public BattleItemType BattleItem;public bool IsLocal;public bool IsBot;public bool Ready;public bool Connected=true;public bool IsHost;}
public sealed class MatchSession
{
public const int TeamSize=5;public const int MaxPlayers=10;public string SessionId{get;private set;}public SessionMode Mode{get;private set;}public MatchPlayerSlot[] Slots{get;private set;}=new MatchPlayerSlot[MaxPlayers];public bool IsOnline=>Mode!=SessionMode.LocalBots;public bool IsFull{get{for(int i=0;i<Slots.Length;i++)if(Slots[i]==null)return false;return true;}}
public MatchSession(string id,SessionMode mode){SessionId=string.IsNullOrEmpty(id)?Guid.NewGuid().ToString("N"):id;Mode=mode;}
public bool TryAssign(int index,MatchPlayerSlot slot){if(index<0||index>=MaxPlayers||slot==null||slot.Team<0||slot.Team>1)return false;int teamCount=0;for(int i=0;i<Slots.Length;i++)if(i!=index&&Slots[i]!=null&&Slots[i].Team==slot.Team)teamCount++;if(teamCount>=TeamSize)return false;Slots[index]=slot;return true;}
}
}
