using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Backrooms.Editor
{
    public static class BackroomsMenu
    {
        const string ScenePath = "Assets/Backrooms/Backrooms.unity";

        [MenuItem("Backrooms/Create Backrooms Scene")]
        static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Backrooms").AddComponent<BackroomsBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            Debug.Log("Backrooms: created " + ScenePath + ". Press Play and click the Game view to enter.");
        }
    }
}
