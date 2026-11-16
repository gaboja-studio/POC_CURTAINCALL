if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play first");
if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new Exception("Unsaved scene");
UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
    UnityEditor.SceneManagement.NewSceneMode.Single);
return "Empty scene opened for standalone HUD bootstrap test";
