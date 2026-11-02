var scenes = Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
    .Select(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i))
    .Select(s => new { s.path, s.name, s.isDirty, s.isLoaded }).ToArray();
var paths = Enumerable.Range(1, 4).Select(i => "Assets/_ThirdParty/Synty/PolygonApocalypse/Models/SM_Prop_Mannequin_Male_" + i.ToString("00") + ".fbx")
    .Concat(Enumerable.Range(1, 6).Select(i => "Assets/_ThirdParty/Synty/PolygonApocalypse/Models/SM_Prop_Mannequin_Female_" + i.ToString("00") + ".fbx"));
var models = paths.Select(p => {
    var go = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(p);
    return new { path = p, exists = go != null,
        skinnedMeshes = go != null ? go.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true).Length : 0,
        transforms = go != null ? go.GetComponentsInChildren<UnityEngine.Transform>(true).Length : 0 };
}).ToArray();
return new { scenes, models };
