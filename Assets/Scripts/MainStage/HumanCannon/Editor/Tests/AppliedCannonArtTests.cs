using System;
using System.Linq;
using CurtainCall.Tightrope.Saws;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    public sealed class AppliedCannonArtTests
    {
        Scene preview;
        T[] All<T>() where T : Component => preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        static Transform Ref(UnityEngine.Object owner, string field) => (Transform)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        static float Number(UnityEngine.Object owner, string field) => new SerializedObject(owner).FindProperty(field).floatValue;
        static Vector3[] Vertices(Transform root) => root.GetComponentsInChildren<MeshFilter>(true)
            .Where(f => f.sharedMesh != null && f.GetComponent<Renderer>() != null && f.GetComponent<Renderer>().enabled)
            .SelectMany(f => f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v))).ToArray();
        static Bounds LocalBounds(Transform root)
        {
            var points = Vertices(root).Select(root.InverseTransformPoint).ToArray();
            Assert.That(points, Is.Not.Empty, root.name);
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points.Skip(1)) bounds.Encapsulate(point);
            return bounds;
        }
        static void Near(Vector3 expected, Vector3 actual, string message) => Assert.That(Vector3.Distance(expected, actual), Is.LessThan(.001f), message);

        [SetUp] public void OpenCopy() => preview = EditorSceneManager.OpenPreviewScene(HumanCannonTestSceneBuilder.ScenePath);
        [TearDown] public void CloseCopy() { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }

        [Test]
        public void RoundFabricMatchesRadialBounceAndKeepsVisibleInteractiveHandle()
        {
            var trampoline = All<HumanCannonTrampoline>().Single();
            Assert.That(trampoline.CircularSurface, Is.True);
            var fabric = trampoline.GetComponentsInChildren<MeshFilter>().Single(f => f.sharedMesh != null && f.sharedMesh.name == "trampoline_3_002");
            var bounds = fabric.GetComponent<Renderer>().bounds;
            Assert.That(bounds.size.x, Is.EqualTo(trampoline.HalfSize.x * 2).Within(.001f));
            Assert.That(bounds.size.z, Is.EqualTo(trampoline.HalfSize.y * 2).Within(.001f));
            Assert.That(bounds.max.y, Is.EqualTo(trampoline.Surface.y).Within(.001f));
            Assert.That(trampoline.Handle.GetComponentsInChildren<Renderer>().Any(r => r.enabled), Is.True);
            Near(new Vector3(-2.8f, 0, 0), trampoline.transform.InverseTransformPoint(trampoline.Handle.position), "Original handle offset");
            Assert.That(TrampolineContact.Contains(new Vector2(1.9f, 1.9f), trampoline.HalfSize, .25f, true), Is.False);
            Assert.That(TrampolineContact.Contains(new Vector2(1.59f, 1.59f), trampoline.HalfSize, .25f, true), Is.True);
            float outer = Vertices(trampoline.transform.Find("AppliedRoundTrampoline/FrameAndFabric"))
                .Select(trampoline.transform.InverseTransformPoint).Max(p => new Vector2(p.x, p.z).magnitude);
            Assert.That(outer, Is.EqualTo(trampoline.LoadHalfSize.x).Within(.001f));
            Assert.That(trampoline.UpwardSpeed, Is.EqualTo(12));
            Assert.That(trampoline.CarryMoveSpeed, Is.EqualTo(4.5f));
        }

        [Test]
        public void EveryCannonKeepsItsBoardingSeatAndLaunchAnchors()
        {
            var stations = All<HumanCannonStation>(); Assert.That(stations.Length, Is.EqualTo(10));
            foreach (var station in stations)
            {
                Assert.That(station.transform.Find("AppliedCannon"), Is.Not.Null, station.name);
                Near(new Vector3(0, 0, -1.5f), station.transform.InverseTransformPoint(station.BoardingPoint.position), station.name);
                Near(new Vector3(0, 1.2f, 0), station.transform.InverseTransformPoint(station.Seat.position), station.name);
                Near(new Vector3(0, 2, 1.5f), station.transform.InverseTransformPoint(station.Muzzle.position), station.name);
            }
        }

        [TestCase(-80f)] [TestCase(0f)] [TestCase(80f)]
        public void EveryPoleKeepsBarAndCableAlignedThroughoutSwing(float angle)
        {
            var poles = All<HumanCannonPole>(); Assert.That(poles.Length, Is.EqualTo(10));
            foreach (var pole in poles)
            {
                pole.PresentSwing(angle); var bar = Ref(pole, "visualBar");
                Near(pole.BarPosition(angle), bar.TransformPoint(LocalBounds(bar).center), pole.name + " bar center");
                foreach (string side in new[] { "left", "right" })
                {
                    var cable = Ref(pole, side + "Cable"); var anchor = Ref(pole, side + "Anchor");
                    var bounds = LocalBounds(cable);
                    Near(Vector3.zero, bounds.center, pole.name + " cable mesh pivot");
                    Assert.That(bounds.size.y, Is.EqualTo(1).Within(.001f));
                    Near(anchor.position, cable.TransformPoint(Vector3.up * .5f), pole.name + " ceiling attachment");
                    Near(pole.BarPosition(angle) + anchor.position - (Ref(pole, "leftAnchor").position + Ref(pole, "rightAnchor").position) * .5f,
                        cable.TransformPoint(Vector3.down * .5f), pole.name + " bar attachment");
                }
            }
        }

        [TestCase(0)] [TestCase(.371f)] [TestCase(2.3f)]
        public void AllMovingHazardsFitTheirDamageVolumesAtTheActualPose(float elapsed)
        {
            foreach (var hazard in All<HumanCannonHazard>())
            {
                hazard.Present(elapsed, 1); var points = Vertices(Ref(hazard, "visual")); Assert.That(points, Is.Not.Empty, hazard.name);
                var center = hazard.Position(elapsed, 1);
                if (hazard.Kind == CannonHazardKind.SpikeMace)
                    Assert.That(points.Max(p => Vector3.Distance(p, center)), Is.EqualTo(hazard.Radius).Within(.001f), hazard.name);
                else if (hazard.Kind == CannonHazardKind.GroundSaw)
                {
                    var local = points.Select(p => Quaternion.Inverse(hazard.BladeRotation) * (p - center)).ToArray();
                    Assert.That(local.Max(p => new Vector2(p.x, p.y).magnitude), Is.EqualTo(hazard.Radius).Within(.001f), hazard.name);
                    Assert.That(local.Max(p => Mathf.Abs(p.z)), Is.EqualTo(Number(hazard, "bladeHalfThickness")).Within(.001f), hazard.name);
                }
                else
                {
                    // A vertex must lie inside at least one of the two rotating damage capsules.
                    var angle = elapsed * Number(hazard, "rotationSpeed") % 360;
                    var rotation = hazard.BladeRotation * Quaternion.Euler(0, 0, angle);
                    float half = Number(hazard, "bladeHalfLength"), width = Number(hazard, "bladeHalfThickness");
                    foreach (var point in points.Select(p => Quaternion.Inverse(rotation) * (p - center)))
                    {
                        float a = Vector3.Distance(point, new Vector3(Mathf.Clamp(point.x, -half, half), 0, 0));
                        float b = Vector3.Distance(point, new Vector3(0, Mathf.Clamp(point.y, -half, half), 0));
                        Assert.That(Mathf.Min(a, b), Is.LessThanOrEqualTo(width + .001f), hazard.name);
                    }
                }
            }
        }

        [Test]
        public void AppliedFloorAndCushionBoundsMatchTheRetainedSolidColliders()
        {
            int found = 0;
            foreach (var box in All<BoxCollider>())
            {
                var art = box.transform.Cast<Transform>().FirstOrDefault(t => t.name is "AppliedConcreteFloor" or "AppliedMetalFloor" or "AppliedCushion");
                if (art == null) continue;
                var bounds = LocalBounds(box.transform);
                Near(box.center, bounds.center, box.name + " collider center");
                Near(box.size, bounds.size, box.name + " collider size"); found++;
            }
            Assert.That(found, Is.EqualTo(81), "74 concrete surfaces + 6 transport panels + cushion");
            Assert.That(All<Collider>().Length, Is.EqualTo(91), "Visual replacement must not add physical blockers");
        }

        [Test]
        public void MetalTransportPanelsKeepSafeLandingAndVisibleOccupiedIndicators()
        {
            foreach (var transport in All<HumanCannonTransport>())
            {
                Assert.That(transport.HasTrapDoors, Is.True); var serialized = new SerializedObject(transport);
                var indicators = serialized.FindProperty("boardingIndicators"); Assert.That(indicators.arraySize, Is.GreaterThanOrEqualTo(2));
                for (int i = 0; i < indicators.arraySize; i++)
                {
                    var renderer = (Renderer)indicators.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(renderer != null && renderer.enabled && renderer.gameObject.name != "FloorPanel", Is.True);
                }
                transport.PresentOccupied(true);
                var block = new MaterialPropertyBlock(); ((Renderer)indicators.GetArrayElementAtIndex(0).objectReferenceValue).GetPropertyBlock(block);
                Assert.That(Vector4.Distance(block.GetColor("_BaseColor"), HumanCannonTransport.OccupiedColor), Is.LessThan(.0001f));
                var floors = serialized.FindProperty("trapColliders");
                transport.PresentTrap(true, 0, 1);
                for (int i = 0; i < floors.arraySize; i++) Assert.That(((Collider)floors.GetArrayElementAtIndex(i).objectReferenceValue).enabled, Is.False);
                transport.PresentTrap(false, 0, 1);
                for (int i = 0; i < floors.arraySize; i++) Assert.That(((Collider)floors.GetArrayElementAtIndex(i).objectReferenceValue).enabled, Is.True);
            }
        }
    }
}
