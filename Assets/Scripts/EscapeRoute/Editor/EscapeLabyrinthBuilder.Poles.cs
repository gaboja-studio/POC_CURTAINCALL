using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        static readonly List<HumanCannonPole> labyrinthPoles = new();

        static void ResetLabyrinthPoles() => labyrinthPoles.Clear();

        static HumanCannonPole LPole(string name, Vector3 localGrip, Vector3 localForward)
        {
            if (!labyrinthFrame || !labyrinthRoot || string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("A copied labyrinth and a unique pole name are required.");
            var existing = labyrinthFrame.GetComponentsInChildren<HumanCannonPole>(true);
            if (existing.Any(p => p.name == name))
                throw new InvalidOperationException("Duplicate labyrinth pole name: " + name);
            var source = existing.Single(p => p.name == "A04A_01_CavernExit");
            Vector3 forward = Vector3.ProjectOnPlane(labyrinthFrame.TransformDirection(localForward), Vector3.up);
            if (forward.sqrMagnitude < .000001f)
                throw new InvalidOperationException("Pole forward must have a horizontal direction: " + name);
            forward.Normalize();
            Vector3 scale = source.transform.lossyScale, parentScale = labyrinthRoot.lossyScale;
            if (Mathf.Min(parentScale.x, parentScale.y, parentScale.z) <= 0)
                throw new InvalidOperationException("Pole placement requires a positive labyrinth world scale.");
            var copy = Object.Instantiate(source.gameObject, labyrinthRoot);
            copy.name = name;
            copy.transform.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
            var pole = copy.GetComponent<HumanCannonPole>();
            var data = new SerializedObject(pole);
            foreach (string field in new[] { "grip", "visualBar", "leftCable", "rightCable", "leftAnchor", "rightAnchor" })
            {
                var referenced = data.FindProperty(field).objectReferenceValue as Transform;
                if (!referenced || !referenced.IsChildOf(copy.transform))
                    throw new InvalidOperationException("Cloned pole has an external or missing " + field + ": " + name);
            }
            if (source.LinkedGate)
                throw new InvalidOperationException("The regular pole source must not have a linked gate.");
            data.FindProperty("linkedGate").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            // Rotate and translate the entire module around its actual Grip. The source
            // mesh, cables and internal references retain their current world scale.
            var orientation = Quaternion.LookRotation(forward, Vector3.up);
            copy.transform.rotation = orientation * Quaternion.Inverse(pole.Grip.rotation) * copy.transform.rotation;
            copy.transform.position += LW(localGrip) - pole.Grip.position;
            copy.SetActive(true);
            pole.enabled = true;
            LSupportPole(pole);
            pole.PresentSwing(0);
            labyrinthPoles.Add(pole);
            return pole;
        }

        static void RegisterLabyrinthPoles()
        {
            var systems = labyrinthFrame.gameObject.scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<HumanCannonPoleSystem>(true)).ToArray();
            if (systems.Length != 1)
                throw new InvalidOperationException("Expected one shared pole system; found " + systems.Length + ".");
            var data = new SerializedObject(systems[0]);
            var poles = data.FindProperty("poles");
            var present = new HashSet<HumanCannonPole>();
            for (int i = 0; i < poles.arraySize; i++)
                if (poles.GetArrayElementAtIndex(i).objectReferenceValue is HumanCannonPole pole) present.Add(pole);
            // Existing indices are network identities. Never sort or compact the source array.
            foreach (var pole in labyrinthPoles)
            {
                if (!pole || !pole.transform.IsChildOf(labyrinthRoot))
                    throw new InvalidOperationException("A newly authored pole no longer belongs to the copied labyrinth.");
                if (!present.Add(pole)) continue;
                int index = poles.arraySize;
                poles.arraySize = index + 1;
                poles.GetArrayElementAtIndex(index).objectReferenceValue = pole;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void LSupportPole(HumanCannonPole pole)
        {
            var template = GameSettings.Poles.Visual(false);
            if (!template) throw new InvalidOperationException("The shared regular pole visual is missing.");
            var visual = template.transform;
            var grip = visual.Find("Grip");
            var bar = visual.Find("VisualBar");
            var left = visual.Find("LeftAnchor");
            var right = visual.Find("RightAnchor");
            if (!grip || !bar || !left || !right)
                throw new InvalidOperationException("Shared regular pole visual lacks Grip, bar or anchors.");
            // Match the common presentation's anchor resizing; no scene-specific visual
            // or swing rule is introduced. Serialized references remain the cloned ones.
            float width = LPoleAuthoredBarWidth(bar);
            float ratio = width > .0001f ? GameSettings.Poles.BarWidth(false) / width : 1;
            Vector3 axis = Vector3.ProjectOnPlane(bar.localRotation * Vector3.right, Vector3.up).normalized;
            Vector3 Anchor(Transform anchor)
            {
                Vector3 p = anchor.localPosition + axis * (Vector3.Dot(anchor.localPosition - bar.localPosition, axis) * (ratio - 1));
                return pole.Grip.position + pole.Grip.rotation * visual.localRotation
                    * Vector3.Scale(p - grip.localPosition, visual.localScale);
            }
            var a = Anchor(left); var b = Anchor(right);
            var data = new SerializedObject(pole);
            ((Transform)data.FindProperty("leftAnchor").objectReferenceValue).position = a;
            ((Transform)data.FindProperty("rightAnchor").objectReferenceValue).position = b;
            Vector3 side = (b - a).normalized;
            var center = (a + b) * .5f;
            float beamWidth = Vector3.Distance(a, b) + 1.2f;
            float maximumBodyTop = pole.HangingPosition(GameSettings.Poles.MaxSwingAngle, 0).y
                + GameSettings.Base.Character.CapsuleHeight;
            if (Mathf.Min(a.y, b.y) < maximumBodyTop + .2f)
                throw new InvalidOperationException("Shared pole anchors leave insufficient overhead body clearance: " + pole.name);
            // Supports are siblings, so the body audit cannot hide them by excluding
            // the pole's own mesh hierarchy. No player-accessible shelf is added below.
            var beam = LBox(pole.name + " high load beam", labyrinthFrame.InverseTransformPoint(center + Vector3.up * .12f),
                new Vector3(beamWidth, .24f, .3f), wornSteel);
            beam.transform.rotation = Quaternion.LookRotation(Vector3.Cross(side, Vector3.up), Vector3.up);
            LBox(pole.name + " beam task-light lens", labyrinthFrame.InverseTransformPoint(center - Vector3.up * .015f),
                new Vector3(.18f, .03f, .12f), bBulb, false);
            var lamp = LAnchor(pole.name + " beam task light", labyrinthFrame.InverseTransformPoint(center - Vector3.up * .05f))
                .gameObject.AddComponent<Light>();
            lamp.transform.SetParent(beam.transform, true);
            // A broad, soft pool describes the bar and its surrounding surfaces without
            // the old hard circular hotspot. Keep the entire cone below the fixture.
            lamp.transform.rotation = Quaternion.LookRotation(Vector3.down + pole.Forward * .35f, pole.Forward);
            lamp.type = LightType.Spot; lamp.range = 9.5f; lamp.intensity = 95;
            lamp.spotAngle = 120; lamp.innerSpotAngle = 38;
            lamp.color = new Color(.84f, .87f, .79f); lamp.shadows = LightShadows.None;
            // The second emitter shares the same physical lens. Its short forward wash
            // reaches the next grip/apron, rather than flooding the room or hidden lanes.
            var fill = new GameObject(pole.name + " beam forward fill").AddComponent<Light>();
            fill.transform.SetParent(lamp.transform, false);
            fill.transform.rotation = Quaternion.LookRotation(pole.Forward + Vector3.down * .75f, Vector3.up);
            fill.type = LightType.Spot; fill.range = 10; fill.intensity = 35;
            fill.spotAngle = 70; fill.innerSpotAngle = 24;
            fill.color = lamp.color; fill.shadows = LightShadows.None;
            Physics.SyncTransforms();
            foreach (float sign in new[] { -1f, 1f })
            {
                var bottom = center + side * (sign * (beamWidth * .5f - .12f)) + Vector3.up * .24f;
                var hit = Physics.RaycastAll(bottom + Vector3.up * .02f, Vector3.up, 200, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.collider.gameObject.scene == labyrinthFrame.gameObject.scene
                        && !h.collider.transform.IsChildOf(labyrinthRoot)
                        && h.normal.y < -.5f).OrderBy(h => h.distance).FirstOrDefault();
                if (!hit.collider)
                    throw new InvalidOperationException("No existing overhead structure for pole suspension: " + pole.name
                        + " at " + labyrinthFrame.InverseTransformPoint(bottom));
                var top = hit.point + Vector3.up * .08f;
                LBox(pole.name + " fixed ceiling hanger " + sign, labyrinthFrame.InverseTransformPoint((bottom + top) * .5f),
                    new Vector3(.14f, top.y - bottom.y, .14f), wornSteel);
                LBox(pole.name + " ceiling anchor plate " + sign, labyrinthFrame.InverseTransformPoint(hit.point),
                    new Vector3(.45f, .12f, .45f), bSteel);
            }
        }

        static float LPoleAuthoredBarWidth(Transform bar)
        {
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (var filter in bar.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                var bounds = filter.sharedMesh.bounds;
                var matrix = bar.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    float x = matrix.MultiplyPoint3x4(corner).x;
                    min = Mathf.Min(min, x); max = Mathf.Max(max, x);
                }
            }
            return float.IsFinite(min) && float.IsFinite(max) ? (max - min) * bar.TransformVector(Vector3.right).magnitude : 0;
        }
    }
}
