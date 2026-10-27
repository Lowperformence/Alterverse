using UnityEngine;
namespace Alterverse.Visuals
{
    /// <summary>Serialized vendor references keep the original meshes, textures and URP shaders in player builds.</summary>
    public sealed class PlanetWindCatalog : ScriptableObject
    {
        public GameObject flows,dust,leaves;
    }
}
