var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var settings = CurtainCall.Settings.GameSettings.Base;
return new {
    scene.path, scene.isDirty,
    waitingRoom = settings.SpaceTransition.WaitingRoomScene,
    exploration = settings.SpaceTransition.ExplorationScene,
    roots = scene.GetRootGameObjects().Select(r => r.name).ToArray(),
    sourceGuid = UnityEditor.AssetDatabase.AssetPathToGUID("Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity"),
    copyGuid = UnityEditor.AssetDatabase.AssetPathToGUID("Assets/Scenes/Tests/WaitingRoom/Pursuit/WaitingRoomPursuit.unity"),
    buildEntries = UnityEditor.EditorBuildSettings.scenes.Where(s => s.path.Contains("WaitingRoom")).Select(s => new { s.path, s.enabled }).ToArray()
};
