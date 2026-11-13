using System;
using System.Linq;
using System.Reflection;
using CurtainCall.Dev;
using CurtainCall.Dev.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

public static class DevCheckpointSceneMigration
{
    static readonly string[] paths = {
        "Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity",
        "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity",
        "Assets/Scenes/Levels/escape_route.unity"
    };

    public static object Migrate()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            return paths.Select(path => {
                var scene = EditorSceneManager.OpenScene(path);
                var route = DevCheckpointRoute.In(scene);
                var before = route.Points.ToDictionary(p => p.label, p => route.PoseFor(p).position);
                int count = DevCheckpointWindow.MigrateLegacyPoints(scene);
                if (route.Points.Any(p => before.TryGetValue(p.label, out var old) && (route.PoseFor(p).position - old).sqrMagnitude > .0001f))
                    throw new Exception("Existing point unexpectedly moved: " + path);
                if (route.Points[0].label != "씬 출발점" || route.Points.Any(x => x.radius < .1f))
                    throw new Exception("Missing spawn or radius: " + path);
                if (count != DevCheckpointWindow.MigrateLegacyPoints(scene))
                    throw new Exception("Migration duplicated points: " + path);
                if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Save failed: " + path);
                return new { path, count, added = route.Points.Where(p => !before.ContainsKey(p.label)).Select(p=>p.label).ToArray(),
                    ordered = route.Points.Select(p => new { p.label, p.radius, p.teleportOnly, pos = route.PoseFor(p).position.ToString() }).ToArray() };
            }).ToArray();
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }

    public static object CheckEditor()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        DevCheckpointWindow window = null;
        try
        {
            var scene = EditorSceneManager.OpenScene(paths[0]);
            var route = DevCheckpointRoute.In(scene);
            window = EditorWindow.GetWindow<DevCheckpointWindow>();
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            window.GetType().GetMethod("Bind", privateInstance).Invoke(window, new object[] { route });
            var list = (ReorderableList)window.GetType().GetField("list", privateInstance).GetValue(window);
            var bounds = new Rect(20, 40, 150, 24);
            var click = new Event { type = EventType.MouseDown, mousePosition = bounds.center, clickCount = 2, button = 0 };
            var isClick = window.GetType().GetMethod("IsFrameClick", BindingFlags.Static | BindingFlags.NonPublic);
            if (!(bool)isClick.Invoke(null, new object[] { click, bounds })) throw new Exception("Double click was ignored");
            click.clickCount = 1;
            if ((bool)isClick.Invoke(null, new object[] { click, bounds })) throw new Exception("Single click triggered focus");
            window.GetType().GetMethod("FramePoint", privateInstance).Invoke(window, new object[] { 2 });
            var target = route.PoseFor(route.Points[2]).position;
            if ((SceneView.lastActiveSceneView.pivot - target).sqrMagnitude > .01f) throw new Exception("SceneView did not focus the selected checkpoint");
            var position = target + new Vector3(.2f, .4f, 2.3f);
            window.GetType().GetMethod("AddAt", privateInstance).Invoke(window, new object[] { position, SceneView.lastActiveSceneView });
            if ((route.PoseFor(route.Points[^1]).position - position).sqrMagnitude > .001f) throw new Exception("Scene placement missed");
            var serialized = list.serializedProperty;
            string added = route.Points[^1].label;
            serialized.MoveArrayElement(serialized.arraySize - 1, 1);
            serialized.GetArrayElementAtIndex(1).FindPropertyRelative("radius").floatValue = .75f;
            serialized.serializedObject.ApplyModifiedProperties();
            if (route.Points[1].label != added || Mathf.Abs(route.Points[1].radius - .75f) > .001f)
                throw new Exception("Order / radius did not update");
            return new { doubleClick = true, frame = true, add = true, reorder = true, radius = route.Points[1].radius };
        }
        finally
        {
            if (window != null) window.Close();
            // Do not save the temporary checkpoint or reorder.
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path == paths[0]) EditorSceneManager.OpenScene(paths[1]);
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }
}
