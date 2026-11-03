const string source = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";
const string folder = "Assets/Scenes/Tests/WaitingRoom/Pursuit";
const string target = folder + "/WaitingRoomPursuit.unity";
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
    if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
        throw new System.InvalidOperationException("Open scene has unsaved changes; preserve it before changing scenes.");
var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<CurtainCall.Settings.BaseGameSettings>("Assets/Resources/Settings/GameSettings/BaseGameSettings.asset");
if (settings == null) throw new System.InvalidOperationException("Base settings missing.");
if (UnityEditor.EditorUtility.IsDirty(settings)) throw new System.InvalidOperationException("Base settings have unsaved changes.");
var sourceGuid = UnityEditor.AssetDatabase.AssetPathToGUID(source);
var before = System.IO.File.ReadAllBytes(source);
if (!UnityEditor.AssetDatabase.IsValidFolder(folder)) UnityEditor.AssetDatabase.CreateFolder("Assets/Scenes/Tests/WaitingRoom", "Pursuit");
if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(target) == null && !UnityEditor.AssetDatabase.CopyAsset(source, target))
    throw new System.InvalidOperationException("Scene copy failed.");
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(target, UnityEditor.SceneManagement.OpenSceneMode.Single);
var so = new UnityEditor.SerializedObject(settings);
so.FindProperty("spaceTransition.waitingRoomScene").stringValue = "WaitingRoomPursuit";
so.FindProperty("spaceTransition.explorationScene").stringValue = "WaitingRoomPursuit";
var voice = so.FindProperty("voiceChat.scenes");
bool hasVoice = false;
for (int i = 0; i < voice.arraySize; i++) if (voice.GetArrayElementAtIndex(i).stringValue == "WaitingRoomPursuit") hasVoice = true;
if (!hasVoice) { int n = voice.arraySize; voice.InsertArrayElementAtIndex(n); voice.GetArrayElementAtIndex(n).stringValue = "WaitingRoomPursuit"; }
so.ApplyModifiedProperties();
UnityEditor.AssetDatabase.SaveAssetIfDirty(settings);
var build = UnityEditor.EditorBuildSettings.scenes.ToList();
if (!build.Any(s => s.path == target)) build.Add(new UnityEditor.EditorBuildSettingsScene(target, true));
UnityEditor.EditorBuildSettings.scenes = build.ToArray();
UnityEditor.AssetDatabase.SaveAssets();
if (!before.SequenceEqual(System.IO.File.ReadAllBytes(source)) || sourceGuid != UnityEditor.AssetDatabase.AssetPathToGUID(source))
    throw new System.InvalidOperationException("Source scene changed unexpectedly.");
var roots = scene.GetRootGameObjects();
var behaviours = roots.SelectMany(r => r.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true)).Where(c => c != null).ToArray();
return new {
    scene = scene.path, scene.isDirty, sourcePreserved = true, sourceGuid,
    copyGuid = UnityEditor.AssetDatabase.AssetPathToGUID(target),
    waitingRoom = settings.SpaceTransition.WaitingRoomScene, exploration = settings.SpaceTransition.ExplorationScene,
    roots = roots.Select(r => new { r.name, r.activeSelf }).ToArray(),
    integration = behaviours.Where(c => c.GetType().Name.Contains("Gather") || c.GetType().Name.Contains("Timer") || c.GetType().Name.Contains("Escape"))
        .Select(c => new { type = c.GetType().FullName, c.name, active = c.isActiveAndEnabled }).ToArray(),
    buildEntry = UnityEditor.EditorBuildSettings.scenes.Where(s => s.path == target).Select(s => new { s.path, s.enabled }).ToArray()
};
