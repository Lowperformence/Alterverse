using UnityEngine;

namespace Alterverse.Visuals
{
    public static class NerokAnchorMaterial
    {
        // UV channel 2 stores the skinned anchor's rest coordinates, so rust follows the metal.
        public static Material Create(bool skinned)
        {
            Shader shader = Resources.Load<Shader>("CyberShark/AnchorMetal");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError("DF: Nerok AnchorMetal shader missing/unsupported; using silver fallback.");
                return RuntimeShaderLibrary.Create(new Color(.68f,.71f,.73f), false, .85f, .5f);
            }
            var material = new Material(shader) { name = "DF_Nerok_SilverAnchorWithRust" };
            material.SetFloat("_UseRestCoordinates", skinned ? 1f : 0f);
            return material;
        }
    }
}
