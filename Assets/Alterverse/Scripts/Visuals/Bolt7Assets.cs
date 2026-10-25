using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    // PNG bytes are deliberately imported as TextAssets, independent of texture importer presets.
    internal static class Bolt7Assets
    {
        private static readonly Dictionary<string,Texture2D> textures=new();
        private static readonly HashSet<string> missing=new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){foreach(var t in textures.Values)if(t!=null)Object.Destroy(t);textures.Clear();missing.Clear();}
        internal static Texture2D Texture(string name,bool linear=false)
        {
            if(textures.TryGetValue(name,out var ready)&&ready!=null)return ready;
            var source=Resources.Load<TextAsset>("Bolt7/Raw/"+name);
            if(source!=null)
            {
                var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,linear){name="BOLT7_"+name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                if(ImageConversion.LoadImage(texture,source.bytes,true)){textures[name]=texture;return texture;}
                Object.Destroy(texture);
            }
            // Retain compatibility with an existing locally imported source texture.
            var imported=Resources.Load<Texture2D>("Bolt7/"+name);
            if(imported!=null){textures[name]=imported;return imported;}
            if(missing.Add(name))Debug.LogError("DF BOLT-7: missing/invalid PNG data at Resources/Bolt7/Raw/"+name+".bytes");
            return null;
        }
        internal static Texture2D Icon(int slot)=>Texture("Icons/"+slot);
    }
}
