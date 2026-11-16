using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.HumanCannon;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        // Layout only. The caller supplies the supported decks, openings and player guides,
        // then performs the scene collision audit and save. No clock, input or wait rules change.
        static HumanCannonStation[] BuildConveyorParallelStations()
        {
            if (Application.isPlaying || !labyrinthFrame ||
                (labyrinthFrame.gameObject.scene.path != LabyrinthScenePath && labyrinthFrame.gameObject.scene.path != LabyrinthCopyPath))
                throw new InvalidOperationException("Parallel launch requires the copied labyrinth in edit mode.");
            var source = labyrinthFrame.GetComponentsInChildren<HumanCannonStation>(true)
                .Single(s => s.name == "A04B_ConveyorCannon");
            var sourceLoops = Enumerable.Range(0, 6).Select(i => labyrinthFrame
                .GetComponentsInChildren<HumanCannonPoleLoop>(true).Single(l => l.name == "LoopTrack_" + i)).ToArray();
            var cannonSystem = labyrinthFrame.GetComponentsInChildren<HumanCannonSystem>(true).Single();
            var poleSystem = labyrinthFrame.GetComponentsInChildren<HumanCannonPoleSystem>(true).Single();
            if (labyrinthFrame.GetComponentsInChildren<HumanCannonHazardCourse>(true).Length != 1)
                throw new InvalidOperationException("The conveyor must use the existing shared hazard clock.");

            var original = sourceLoops[0];
            var originalData = new SerializedObject(original);
            if (!original.IsConfigured || !originalData.FindProperty("wallFed").boolValue
                || Mathf.Abs(originalData.FindProperty("metersPerSecond").floatValue - 5f) > .001f
                || Mathf.Abs(originalData.FindProperty("phase01").floatValue) > .0001f
                || Vector3.Distance(labyrinthFrame.InverseTransformVector(original.transform.TransformVector(Vector3.right)), Vector3.right) > .0001f
                || Vector3.Distance(labyrinthFrame.InverseTransformVector(original.transform.TransformVector(Vector3.forward)), Vector3.forward) > .0001f)
                throw new InvalidOperationException("Expected the reviewed, unscaled 5m/s wall-fed conveyor.");
            foreach (var loop in sourceLoops)
            {
                var data = new SerializedObject(loop);
                foreach (string field in new[] { "straightLength", "radius", "metersPerSecond", "exitStraightLength", "unloadingDistance", "yawOffsetDegrees" })
                    if (Mathf.Abs(data.FindProperty(field).floatValue - originalData.FindProperty(field).floatValue) > .0001f)
                        throw new InvalidOperationException("Existing conveyor paths differ: " + loop.name);
                int index = Array.IndexOf(sourceLoops, loop);
                if (!data.FindProperty("wallFed").boolValue || data.FindProperty("reverseDirection").boolValue
                    || Vector3.Distance(data.FindProperty("localCenter").vector3Value, originalData.FindProperty("localCenter").vector3Value) > .0001f
                    || Vector3.Distance(loop.transform.position, original.transform.position) > .0001f
                    || Quaternion.Angle(loop.transform.rotation, original.transform.rotation) > .001f
                    || Vector3.Distance(loop.transform.lossyScale, original.transform.lossyScale) > .0001f
                    || Mathf.Abs(data.FindProperty("phase01").floatValue - index / 6f) > .0001f)
                    throw new InvalidOperationException("Existing conveyor phases or transforms changed: " + loop.name);
            }
            RequireRegistered(cannonSystem, "stations", new Object[] { source });
            RequireRegistered(poleSystem, "poles", sourceLoops.Select(l => (Object)l.GetComponentsInChildren<HumanCannonPole>(true).Single()).ToArray());

            float spacing = original.Length / 12f;
            var stations = new HumanCannonStation[4];
            stations[0] = source; // Preserve its hierarchy, position, ballistic settings and network index.
            for (int i = 1; i < stations.Length; i++)
            {
                string name = "Labyrinth_03_ParallelConveyorCannon_" + (i + 1);
                var station = labyrinthFrame.GetComponentsInChildren<HumanCannonStation>(true).SingleOrDefault(s => s.name == name);
                if (!station)
                {
                    var copy = Object.Instantiate(source.gameObject, source.transform.parent);
                    copy.name = name;
                    station = copy.GetComponent<HumanCannonStation>();
                }
                if (station.transform.parent != source.transform.parent)
                    throw new InvalidOperationException("Parallel cannon has an unexpected scaled parent: " + name);
                station.transform.localScale = source.transform.localScale;
                station.transform.SetPositionAndRotation(source.transform.position + labyrinthFrame.TransformVector(Vector3.right * (spacing * i)), source.transform.rotation);
                station.gameObject.SetActive(true);
                station.enabled = true;
                RequireInternalTransforms(station, station.transform, "boardingPoint", "seat", "muzzle");
                foreach (var barrel in station.GetComponentsInChildren<CannonBarrelPresentation>(true))
                {
                    RequireInternalTransforms(barrel, station.transform, "barrelPivot");
                    var animation = new SerializedObject(barrel).FindProperty("animationRoot").objectReferenceValue as GameObject;
                    if (!animation || !animation.transform.IsChildOf(station.transform))
                        throw new InvalidOperationException("Cloned cannon animation points outside its own hierarchy: " + name);
                }
                if ((station.LaunchVelocity - source.LaunchVelocity).sqrMagnitude > .000001f)
                    throw new InvalidOperationException("Parallel launch must retain the source ballistic velocity.");
                stations[i] = station;
            }

            var additions = new HumanCannonPole[6];
            for (int i = 0; i < additions.Length; i++)
            {
                string name = "Labyrinth_03_ParallelConveyorLoop_" + i;
                var loop = labyrinthFrame.GetComponentsInChildren<HumanCannonPoleLoop>(true).SingleOrDefault(l => l.name == name);
                if (!loop)
                {
                    var copy = Object.Instantiate(original.gameObject, original.transform.parent);
                    copy.name = name;
                    loop = copy.GetComponent<HumanCannonPoleLoop>();
                }
                if (loop.transform.parent != original.transform.parent)
                    throw new InvalidOperationException("Parallel loop has an unexpected scaled parent: " + name);
                loop.transform.localScale = original.transform.localScale;
                loop.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
                loop.gameObject.SetActive(true);
                loop.enabled = true;
                var data = new SerializedObject(loop);
                RequireInternalTransforms(loop, loop.transform, "carriage");
                // Existing six phases remain 0,2,4,6,8,10 over twelve. Only fill their gaps.
                data.FindProperty("phase01").floatValue = (i * 2 + 1) / 12f;
                data.ApplyModifiedPropertiesWithoutUndo();
                var pole = loop.GetComponentsInChildren<HumanCannonPole>(true).Single();
                pole.name = "Labyrinth_03_ParallelConveyorGrip_" + i;
                pole.enabled = true;
                if (pole.LinkedGate || !loop.Carriage || !pole.transform.IsChildOf(loop.Carriage))
                    throw new InvalidOperationException("A conveyor grip must belong to its own carriage without a switch gate.");
                RequireInternalTransforms(pole, pole.transform, "grip", "visualBar", "leftCable", "rightCable", "leftAnchor", "rightAnchor");
                if (!loop.IsConfigured) throw new InvalidOperationException("Incomplete cloned conveyor: " + name);
                loop.RefreshPose();
                pole.PresentSwing(0f);
                additions[i] = pole;
            }
            AppendParallelDevices(cannonSystem, "stations", stations.Skip(1).Cast<Object>());
            AppendParallelDevices(poleSystem, "poles", additions.Cast<Object>());
            return stations;
        }

        static void RequireInternalTransforms(Object target, Transform root, params string[] fields)
        {
            var data = new SerializedObject(target);
            foreach (string field in fields)
            {
                var part = data.FindProperty(field).objectReferenceValue as Transform;
                if (!part || !part.IsChildOf(root))
                    throw new InvalidOperationException(target.name + " has an external or missing " + field);
            }
        }

        static void RequireRegistered(Object system, string field, IEnumerable<Object> expected)
        {
            var array = new SerializedObject(system).FindProperty(field);
            var present = new HashSet<Object>();
            for (int i = 0; i < array.arraySize; i++) present.Add(array.GetArrayElementAtIndex(i).objectReferenceValue);
            foreach (var device in expected)
                if (!present.Contains(device)) throw new InvalidOperationException("Original device is missing from " + field + ": " + device.name);
        }

        static void AppendParallelDevices(Object system, string field, IEnumerable<Object> additions)
        {
            var data = new SerializedObject(system);
            var array = data.FindProperty(field);
            var before = new Object[array.arraySize];
            var present = new HashSet<Object>();
            for (int i = 0; i < before.Length; i++)
            { before[i] = array.GetArrayElementAtIndex(i).objectReferenceValue; present.Add(before[i]); }
            foreach (var device in additions)
            {
                if (!present.Add(device)) continue;
                int index = array.arraySize;
                array.arraySize = index + 1;
                array.GetArrayElementAtIndex(index).objectReferenceValue = device;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            data.Update();
            array = data.FindProperty(field);
            for (int i = 0; i < before.Length; i++)
                if (array.GetArrayElementAtIndex(i).objectReferenceValue != before[i])
                    throw new InvalidOperationException("Existing device indices changed in " + field);
        }
    }
}
