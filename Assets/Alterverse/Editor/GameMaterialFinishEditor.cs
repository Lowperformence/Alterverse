#if UNITY_EDITOR
using UnityEditor;
using Alterverse.Visuals;

namespace Alterverse.EditorTools
{
    sealed class GameMaterialFinishEditor:AssetPostprocessor
    {
        [InitializeOnLoadMethod]
        static void Register()=>EditorApplication.delayCall+=Initialize;
        static void Initialize()
        {
            GameMaterialFinishes.Refresh();VisualEffectResources.Initialize();
        }
        static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
        {
            foreach(string path in imported)
                if(path.StartsWith("Assets/Alterverse/Resources/MaterialFinishes/",System.StringComparison.Ordinal)||
                    path=="Assets/Alterverse/Resources/CombatVfx/FlowNoise.png")
                {EditorApplication.delayCall+=Initialize;break;}
        }
    }
}
#endif
