using UnityEngine;

namespace Alterverse.Visuals
{
    public sealed partial class StylizedHeroVisual
    {
        // Unity tears down the whole Play Mode world after OnApplicationQuit.
        // Do not explicitly destroy generated/shared render resources during that editor shutdown;
        // Unity owns the teardown at that point and can report them as persistent assets.
        private void OnApplicationQuit()
        {
            armorMesh = null;
            limbMesh = null;
            helmetMesh = null;
            detailMeshes.Clear();
            ownedMaterials.Clear();
        }
    }
}
