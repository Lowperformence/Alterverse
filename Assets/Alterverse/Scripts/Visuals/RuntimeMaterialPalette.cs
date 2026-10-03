using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Owns only runtime material copies and releases them with their generating actor.</summary>
    public sealed class RuntimeMaterialPalette:MonoBehaviour
    {
        private readonly List<Material> repairs=new();
        public void OwnRepair(Material material){if(material!=null)repairs.Add(material);}
        private readonly Dictionary<(Material,Color),Material> copies=new();
        private readonly Dictionary<(Shader,Color),Material> fallbacks=new();
        private readonly Dictionary<Material,Material> tintSources=new();
        public static RuntimeMaterialPalette For(GameObject owner)
            =>owner.GetComponent<RuntimeMaterialPalette>()??owner.AddComponent<RuntimeMaterialPalette>();
        public static void Tint(GameObject owner,Renderer renderer,Color color)
        {
            if(owner==null||renderer==null||renderer.sharedMaterial==null)return;
            var palette=For(owner);
            Material source=renderer.sharedMaterial;
            if(palette.tintSources.TryGetValue(source,out Material original))source=original;
            var key=(source,color);
            if(!palette.copies.TryGetValue(key,out Material copy))
            {
                copy=new Material(source){name="DF_OwnedTint",color=color};
                palette.copies.Add(key,copy);
                palette.tintSources.Add(copy,source);
            }
            renderer.sharedMaterial=copy;
        }
        public Material Fallback(Shader shader,Color color)
        {
            if(shader==null)return null;
            var key=(shader,color);
            if(!fallbacks.TryGetValue(key,out Material material))
            {
                material=new Material(shader){name="DF_OwnedFallback",color=color};
                fallbacks.Add(key,material);
            }
            return material;
        }
        private void OnDestroy()
        {
            foreach(Material material in copies.Values)if(material!=null)Destroy(material);
            foreach(Material material in fallbacks.Values)if(material!=null)Destroy(material);
            foreach(Material material in repairs)if(material!=null)Destroy(material);
            copies.Clear();fallbacks.Clear();repairs.Clear();tintSources.Clear();
        }
    }
}
