using System;
using UnityEngine;

namespace Alterverse.Visuals
{
    // Generated from the user's licensed Epic Toon FX import, never from demo controllers.
    public sealed class EpicVfxLibrary : ScriptableObject
    {
        public const string CurrentVersion = "Epic Toon FX user import; DF replacement 2";
        public const string ResourcePath = "CombatVfx/EpicToon/Library";
        [Serializable] public sealed class Entry
        {
            public string key;
            public GameObject prefab;
            public float scale = 1f;
            public float nativeSeconds = 1f;
        }
        public Entry[] entries;
        public Material beamMaterial;
        public string sourceVersion;
        public Entry Find(string key)
        {
            if(entries != null) foreach(var entry in entries)
                if(entry != null && entry.key == key) return entry;
            return null;
        }
    }
}
