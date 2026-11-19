using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static void ReplaceOptionalCannonsWithSwings()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.isDirty || scene.path != ScenePath)
                throw new InvalidOperationException("Open the playable escape route in Edit Mode.");
            labyrinthFrame = scene.GetRootGameObjects().Single().transform;
            var layout = labyrinthFrame.Find("EscapeBackground/70_LabyrinthRemake");
            if (!layout || layout.Find("83_ServiceCrossings"))
                throw new InvalidOperationException("Expected the original bypasses before rebuilding.");

            labyrinthRoot = new GameObject("83_ServiceCrossings").transform;
            labyrinthRoot.SetParent(layout, false);
            bConcrete = CrossingMaterial("concrete");
            bSteel = wornSteel = CrossingMaterial("steel");
            labyrinthRopes.Clear();
            ResetLabyrinthPoles();

            var cannons = labyrinthFrame.GetComponentsInChildren<HumanCannonStation>(true);
            var bypassNames = new[] { "Labyrinth_02_InspectionRelay", "Labyrinth_03_ProcessingLaunch", "Labyrinth_04_ExtractionLaunch" };
            foreach (var cannon in bypassNames.Select(name => cannons.Single(c => c.name == name)).ToArray())
                Object.DestroyImmediate(cannon.gameObject);
            var boundary = labyrinthFrame.Find("Inspection relay timed wire air boundary");
            if (boundary) Object.DestroyImmediate(boundary.gameObject);
            var obsolete = labyrinthFrame.GetComponentsInChildren<HumanCannonPole>(true)
                .SingleOrDefault(p => p.name == "Inspection cannon midair transfer swing");
            if (obsolete)
            {
                var parent = obsolete.transform.parent;
                Object.DestroyImmediate(obsolete.gameObject);
                foreach (var anchor in parent.Cast<Transform>()
                             .Where(t => t.name.StartsWith("Inspection flight recovery swing ceiling anchor plate", StringComparison.Ordinal)).ToArray())
                    Object.DestroyImmediate(anchor.gameObject);
            }

            BuildInspectionServiceCrossing();
            BuildExtractionServiceCrossing();
            RegisterLabyrinthPoles();
            AppendCrossingRopes();
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static Material CrossingMaterial(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Levels/EscapeFactoryArt/factory-" + name + ".mat");

        static void BuildInspectionServiceCrossing()
        {
            LRope("Inspection service entry wire", new(99,-6,76.05f), new(96,-6,70.2f));
            LFloor("Inspection service swing departure", new(96,-6,69), 3.4f, 2.4f);
            LPole("Inspection service swing", new(94.6f,-2.85f,67.3f), new(-5.6f,0,-4.3f));
            LFloor("Inspection service swing receiver", new(89,-9,63), 4.8f, 4.8f);

            // Open only the lower service slot in the concrete cores. The upper walkways,
            // timed wires and the pursuer's higher service ledge remain in place.
            LCutNamedBox("Concrete remainder", new Bounds(new(87,-11,48), new(4f,7,17)));
            LCutNamedBox("Conveyor arrival rear rail", new Bounds(new(77,-21,17), new(2.4f,3,1)));
            foreach (var post in labyrinthFrame.GetComponentsInChildren<Transform>(true)
                         .Where(t => t.name.StartsWith("Conveyor arrival rear rail post ", StringComparison.Ordinal)
                             && Mathf.Abs(labyrinthFrame.InverseTransformPoint(t.position).x - 77) < 1.2f).ToArray())
                Object.DestroyImmediate(post.gameObject);

            var path = new Vector3[] { new(89,-9,60.6f), new(87,-9,55), new(87,-10,48),
                new(86,-12,41), new(83,-15,34), new(81,-18,26), new(80,-20,21), new(77,-22,16.95f) };
            AddCrossingWires("Inspection service descent", path);
        }

        static void BuildExtractionServiceCrossing()
        {
            LRope("Extraction service entry wire", new(130.95f,0,32), new(133.4f,0,34.45f));
            LFloor("Extraction service swing departure", new(134.6f,0,34.45f), 2.4f, 2.4f);
            LPole("Extraction service swing", new(136.2f,3.1f,36.1f), new(3.8f,0,6.2f));
            LFloor("Extraction service swing receiver", new(140,0,41.7f), 4.8f, 3.4f);
            LRope("Extraction service exit wire", new(140,0,43.38f), new(140,0,44.04f));
        }

        static void AddCrossingWires(string prefix, Vector3[] path)
        {
            for (int i = 1; i < path.Length; i++)
                LRope(prefix + " " + i, path[i - 1], path[i]);
        }

        static void AppendCrossingRopes()
        {
            var course = labyrinthFrame.GetComponentsInChildren<TightropeCourse>(true).Single();
            var data = new SerializedObject(course);
            var ropes = data.FindProperty("sceneRopes");
            int first = ropes.arraySize;
            ropes.arraySize += labyrinthRopes.Count;
            var shape = data.FindProperty("sceneShape");
            shape.FindPropertyRelative("laneCount").intValue = ropes.arraySize;
            var distances = shape.FindPropertyRelative("laneEndDistances");
            distances.arraySize = ropes.arraySize;
            for (int i = 0; i < labyrinthRopes.Count; i++)
            {
                ropes.GetArrayElementAtIndex(first + i).objectReferenceValue = labyrinthRopes[i];
                distances.GetArrayElementAtIndex(first + i).floatValue = labyrinthRopes[i].size.z;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            course.Rebuild();
        }
    }
}
