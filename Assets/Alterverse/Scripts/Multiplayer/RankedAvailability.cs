using System;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>Release switch shared by the Ranked menu and queue entry point.</summary>
    public static class RankedAvailability
    {
        public const string LockedMessage = "RANKED LOCKED • AVAILABLE AFTER RELEASE";
        [Serializable] sealed class Configuration { public bool enabled; }

        // Missing or invalid configuration keeps Ranked locked.
        public static bool Enabled
        {
            get
            {
                try
                {
                    var asset = Resources.Load<TextAsset>("RankedAvailability");
                    return asset != null && JsonUtility.FromJson<Configuration>(asset.text)?.enabled == true;
                }
                catch (Exception) { return false; }
            }
        }
    }
}
