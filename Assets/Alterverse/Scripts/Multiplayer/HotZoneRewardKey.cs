using System.Globalization;

namespace Alterverse.Multiplayer
{
    // Snapshot identity includes enough spawn metadata to recover without a resolve event.
    public static class HotZoneRewardKey
    {
        public static string Create(int occurrence,int team)
            => "HOTZONE:"+occurrence.ToString(CultureInfo.InvariantCulture)+":T"+team.ToString(CultureInfo.InvariantCulture);

        public static bool TryParse(string key,out int occurrence,out int team)
        {
            occurrence=-1;team=-1;
            if(string.IsNullOrEmpty(key)||!key.StartsWith("HOTZONE:",System.StringComparison.Ordinal))return false;
            int split=key.IndexOf(":T",8,System.StringComparison.Ordinal);
            if(split<9||split!=key.Length-3)return false;
            if(!int.TryParse(key.Substring(8,split-8),NumberStyles.None,CultureInfo.InvariantCulture,out int parsed)||parsed<0)return false;
            int assigned=key[split+2]-'0';
            if(assigned<0||assigned>1)return false;
            occurrence=parsed;team=assigned;return true;
        }
    }
}
