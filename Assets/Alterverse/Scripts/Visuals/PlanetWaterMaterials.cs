using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Water finish for the existing Earth pool cylinders, owned by the arena.</summary>
    public static class PlanetWaterMaterials
    {
        public static void Apply(Material material)
        {
            if(material==null)return;
            Shader shader=Resources.Load<Shader>("DF_MapWater");
            if(shader==null||!shader.isSupported)return;
            Color color=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):material.color;
            material.shader=shader;
            material.SetColor("_BaseColor",color);
            material.SetColor("_ShallowColor",new Color(.07f,.34f,.38f,1f));
            material.SetFloat("_Smoothness",.90f);
            material.SetFloat("_WaveStrength",.14f);
            material.SetFloat("_ShoreStrength",.065f);
            material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.enableInstancing=true;
        }
    }
}
