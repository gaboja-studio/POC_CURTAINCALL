if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play first");
if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new Exception("Unsaved scene");
UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/InGame/Start.unity");
return new { version = UnityEditor.PlayerSettings.bundleVersion, scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path };
