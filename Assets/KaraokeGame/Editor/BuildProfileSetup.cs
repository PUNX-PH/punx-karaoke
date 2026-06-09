using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KaraokeGame.Editor
{
    public static class BuildProfileSetup
    {
        private const string MenuPath = "KaraokeGame/Add Scenes to Build Settings";

        [MenuItem(MenuPath)]
        public static void AddScenesToBuild()
        {
            // Find both scenes anywhere in the project.
            string[] guids = AssetDatabase.FindAssets("t:Scene SongSelect") ;
            string songSelectPath = FindScenePath(guids, "SongSelect");

            guids = AssetDatabase.FindAssets("t:Scene GameScreen");
            string gameScreenPath = FindScenePath(guids, "GameScreen");

            if (songSelectPath == null)
            {
                Debug.LogError("[KaraokeGame] Could not find a scene named 'SongSelect'. Create it first.");
                return;
            }
            if (gameScreenPath == null)
            {
                Debug.LogError("[KaraokeGame] Could not find a scene named 'GameScreen'. Create it first.");
                return;
            }

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            EnsureScene(scenes, songSelectPath);  // index 0
            EnsureScene(scenes, gameScreenPath);  // index 1

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[KaraokeGame] Build Settings updated:\n  0 — " + songSelectPath + "\n  1 — " + gameScreenPath);
            EditorUtility.DisplayDialog("KaraokeGame", "Scenes added to Build Settings.\n\nSongSelect = 0\nGameScreen  = 1", "OK");
        }

        private static string FindScenePath(string[] guids, string sceneName)
        {
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == sceneName)
                    return path;
            }
            return null;
        }

        private static void EnsureScene(List<EditorBuildSettingsScene> list, string path)
        {
            foreach (var s in list)
            {
                if (s.path == path)
                {
                    s.enabled = true;
                    return;
                }
            }
            list.Add(new EditorBuildSettingsScene(path, true));
        }
    }
}