#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorTools
{
    public sealed class EdgegapMatchmakerSecretSetup : EditorWindow
    {
        private const string SecretDirectory = "Assets/Resources/LocalSecrets";
        private const string SecretPath = SecretDirectory + "/edgegap_matchmaker_token.txt";
        private string token = "";

        [MenuItem("Tools/DIMENSIONS FRONT/Edgegap/Set Matchmaker Token")]
        private static void Open()
        {
            EdgegapMatchmakerSecretSetup window = GetWindow<EdgegapMatchmakerSecretSetup>(true, "Edgegap Matchmaker Token", true);
            window.minSize = new Vector2(520f, 165f);
            window.maxSize = new Vector2(760f, 220f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("DIMENSIONS FRONT • Edgegap Matchmaker", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Paste the Matchmaker Auth Token from Edgegap. It is stored only in Assets/Resources/LocalSecrets, which is excluded from Git. The token is included locally when you build the game.",
                MessageType.Info);

            EditorGUILayout.Space(4f);
            token = EditorGUILayout.PasswordField("Auth Token", token);
            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(token)))
            {
                if (GUILayout.Button("Save Token Locally", GUILayout.Height(30f)))
                    Save();
            }
        }

        private void Save()
        {
            string trimmed = (token ?? "").Trim();
            if (trimmed.Length < 8)
            {
                EditorUtility.DisplayDialog("Edgegap", "The token looks too short.", "OK");
                return;
            }

            Directory.CreateDirectory(SecretDirectory);
            File.WriteAllText(SecretPath, trimmed);
            token = "";
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent("Token saved locally"));
            Debug.Log("DIMENSIONS FRONT: Edgegap Matchmaker token saved locally. The LocalSecrets folder is ignored by Git.");
        }
    }
}
#endif
