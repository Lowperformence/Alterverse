using UnityEngine;

namespace Alterverse.Visuals
{
    public enum StructureFinish { Concrete, Deck, Metal }

    // Imported, world-space surface data also covers generated discs without UVs.
    // Only mutate the builder's owned opaque materials; never add geometry or lights.
    public static class StructureSurfaceMaterials
    {
        private static Shader shader;
        private static readonly Texture2D[] details=new Texture2D[3];
        private static Texture2D concreteFine,metalFine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {shader=null;for(int i=0;i<details.Length;i++)details[i]=null;concreteFine=metalFine=null;}

        public static bool Apply(Material material,StructureFinish finish)
        {
            if(material==null)return false;
            int index=(int)finish;if(index<0||index>=details.Length)return false;
            Color emission=material.HasProperty("_EmissionColor")?material.GetColor("_EmissionColor"):Color.black;
            if(Mathf.Max(emission.r,emission.g,emission.b)>.001f)return false;
            if(shader==null)shader=Resources.Load<Shader>("DF_ScenerySurface");
            if(shader==null||!shader.isSupported)return false;
            if(details[index]==null)details[index]=Resources.Load<Texture2D>("StructureFinishes/"+finish);
            if(details[index]==null)return false;

            Color tint=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):material.color;
            float metallic=material.HasProperty("_Metallic")?material.GetFloat("_Metallic"):0f;
            Texture map=material.HasProperty("_BaseMap")?material.GetTexture("_BaseMap"):null;
            Vector2 tiling=material.HasProperty("_BaseMap")?material.GetTextureScale("_BaseMap"):Vector2.one;
            Vector2 offset=material.HasProperty("_BaseMap")?material.GetTextureOffset("_BaseMap"):Vector2.zero;
            bool concrete=finish==StructureFinish.Concrete,deck=finish==StructureFinish.Deck;
            if(concreteFine==null)concreteFine=Resources.Load<Texture2D>("SurfaceMicro/Concrete");
            if(metalFine==null)metalFine=Resources.Load<Texture2D>("SceneryFinishes/Metal");
            Texture2D fine=concrete?concreteFine:metalFine;
            material.shader=shader;
            material.SetColor("_BaseColor",tint);material.SetColor("_EmissionColor",emission);
            material.SetTexture("_BaseMap",map!=null?map:Texture2D.whiteTexture);
            material.SetTextureScale("_BaseMap",tiling);material.SetTextureOffset("_BaseMap",offset);
            material.SetTexture("_DetailData",details[index]);
            material.SetFloat("_DetailScale",concrete ? .32f : deck ? .55f : .85f);
            material.SetFloat("_Relief",concrete ? .26f : deck ? .20f : .15f);
            material.SetFloat("_ValueContrast",concrete ? .72f : deck ? .78f : .48f);
            material.SetFloat("_RoughnessWeight",concrete ? .92f : .80f);
            material.SetFloat("_Metallic",concrete?Mathf.Min(metallic,.08f):Mathf.Min(metallic,.70f));
            material.SetFloat("_Smoothness",concrete ? .12f : .24f);
            material.SetFloat("_RoughnessBias",0f);
            material.SetFloat("_Sheen",0f);material.SetFloat("_Transmission",0f);
            material.SetTexture("_FineData",fine!=null?fine:Texture2D.grayTexture);
            material.SetFloat("_FineScale",concrete?1.8f:.65f);
            material.SetFloat("_FineRelief",fine!=null?(concrete ? .06f : .035f):0f);
            material.SetFloat("_FineContrast",fine!=null?(concrete ? .22f : .12f):0f);
            material.DisableKeyword("_NORMALMAP");material.enableInstancing=true;
            return true;
        }
    }
}
