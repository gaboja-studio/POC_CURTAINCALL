AssetDatabase.Refresh();
CurtainCall.HumanCannon.EditorTools.CannonBarrelArtBuilder.BuildPrefab();
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(CurtainCall.HumanCannon.EditorTools.HumanCannonTestSceneBuilder.ScenePath);
int stations = CurtainCall.HumanCannon.EditorTools.CannonBarrelArtBuilder.ConfigureStations(scene);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
var gallery = CurtainCall.HumanCannon.EditorTools.CannonBarrelArtBuilder.BuildGallery();
return "Configured " + stations + " stations; gallery " + gallery;
