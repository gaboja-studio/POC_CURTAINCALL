using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using CurtainCall.EscapeRoute;

public static class Revision7Audit
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("EditMode only");
        var report = new List<object>();
        foreach (string path in new[] { "Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRoutePursuitRemodel.unity",
            "Assets/Scenes/Levels/escape_route.unity", "Assets/Scenes/Tests/EscapeRoute/Pursuer/UnderstagePursuerPlaytest.unity" })
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var chases = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EscapeUnderstageChase>(true)).ToArray();
                if (chases.Length != 1) throw new Exception(path + " chases=" + chases.Length);
                foreach (var chase in chases)
                {
                    var dolls = chase.GetComponentsInChildren<EscapePursuerDoll>(true);
                    if (dolls.Length != 1) throw new Exception("Duplicate/missing doll");
                    var doll = dolls[0]; var actor = new SerializedObject(chase);
                    if (doll.revision != 7 || actor.FindProperty("doll").objectReferenceValue != doll || doll.chase != chase)
                        throw new Exception("Incorrect revision/controller link");
                    var skins = doll.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var bones = skins.SelectMany(s => s.bones).Distinct().ToArray();
                    int missing = doll.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                    if (skins.Length != 9 || bones.Length != 207 || bones.Any(b => !b) || missing != 0
                        || doll.clips.Any(c => !c) || doll.clips.Distinct().Count() != 25
                        || doll.folds.Length != 145 || doll.sleeves.Length != 6 || doll.stanceBones.Length != 3
                        || skins.Any(s => !s.sharedMesh || s.sharedMaterials.Any(m => !m))
                        || doll.legacyMeshes.Any(r => r && r.enabled)) throw new Exception("Rig/skin/clip integrity failed");
                    var boss = new SerializedObject(doll.boss); var parts = boss.FindProperty("parts");
                    for (int i = 0; i < 8; i++)
                    {
                        var part = parts.GetArrayElementAtIndex(i);
                        if (part.FindPropertyRelative("hitbox").objectReferenceValue != doll.hitboxes[i]
                            || !doll.hitboxes[i] || !doll.starts[i] || !doll.middles[i] || !doll.ends[i]
                            || !(part.FindPropertyRelative("meshes").GetArrayElementAtIndex(0).objectReferenceValue is SkinnedMeshRenderer))
                            throw new Exception("Severable part mismatch " + i);
                    }
                    report.Add(new { scene = path, revision = doll.revision, skins = skins.Length, bones = bones.Length,
                        clips = doll.clips.Distinct().Count(), folds = doll.folds.Length, sleeves = doll.sleeves.Length,
                        hitboxes = doll.hitboxes.Length, missingScripts = missing, dirty = scene.isDirty });
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText("Tasks/Active/Task-20261114-130-escape-pursuer-followup/Preview/revision7-scenes.json", json);
        return json;
    }
}
