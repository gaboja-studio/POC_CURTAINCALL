using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.Saws.EditorTools.Tests
{
    public sealed class TightropeSawVisualTests
    {
        readonly List<Object> owned = new();
        readonly List<TightropeSawVisualResources> resources = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var owner in resources) if (owner != null) Release(owner);
            resources.Clear();
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [TestCase(1.2f, 0.1f)]
        [TestCase(0.6f, 0.06f)]
        public void DefaultVisualFitsItsConfiguredEnvelopeWithoutAddingColliders(float diameter, float thickness)
        {
            var (saws, root) = Fixture();
            var saw = Create(saws, root, null, diameter, thickness);
            Assert.That(saw.GetComponentsInChildren<Collider>(true), Is.Empty);
            foreach (var filter in saw.GetComponentsInChildren<MeshFilter>(true))
            {
                if (TightropeOutline.IsOutlineRenderer(filter.GetComponent<Renderer>())) continue;
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = saw.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    Assert.That(new Vector2(point.x, point.z).magnitude, Is.LessThanOrEqualTo(diameter * 0.5f + 0.00001f));
                    Assert.That(Mathf.Abs(point.y), Is.LessThanOrEqualTo(thickness * 0.5f + 0.00001f));
                }
            }
            Assert.That(saw.Find("RotationMarkTop"), Is.Not.Null);
            Assert.That(saw.Find("RotationMarkBottom"), Is.Not.Null);
        }

        [Test]
        public void GeneratedSawsShareMaterialsAndReleaseOnlyTheirOwnedResources()
        {
            var (saws, root) = Fixture();
            Create(saws, root, null, 1.2f, 0.1f);
            Create(saws, root, null, 0.6f, 0.06f);
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            var materials = renderers.Select(r => r.sharedMaterial).Distinct().ToArray();
            var sourceMesh = root.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(materials.Length, Is.EqualTo(4), "원판·날·중심·회전 표식의 재질만 계층 전체에서 공유한다.");
            Assert.That(renderers.Length, Is.GreaterThan(materials.Length));
            // 일반 MonoBehaviour는 EditMode에서 Unity가 수명 콜백을 자동 실행하지 않는다.
            Release(root.GetComponent<TightropeSawVisualResources>());
            Object.DestroyImmediate(root.gameObject);
            Assert.That(materials.All(material => material == null), Is.True);
            Assert.That(sourceMesh != null, Is.True, "Unity 기본 도형 메시를 파괴하면 안 된다.");
        }

        [Test]
        public void AuthoredPrefabKeepsItsGeometryMaterialsAndCollider()
        {
            var (saws, root) = Fixture();
            var prefab = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            prefab.transform.localScale = new Vector3(0.7f, 0.2f, 0.4f);
            var originalMesh = prefab.GetComponent<MeshFilter>().sharedMesh;
            var originalMaterial = prefab.GetComponent<MeshRenderer>().sharedMaterial;
            var saw = Create(saws, root, prefab, 1.2f, 0.1f);
            Assert.That(saw.localScale, Is.EqualTo(prefab.transform.localScale));
            Assert.That(saw.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(originalMesh));
            Assert.That(saw.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(originalMaterial));
            Assert.That(saw.GetComponent<BoxCollider>().size, Is.EqualTo(prefab.GetComponent<BoxCollider>().size));
            Assert.That(saw.Find("RotationMarkTop"), Is.Null);
            Object.DestroyImmediate(root.gameObject);
            Assert.That(originalMesh != null && originalMaterial != null, Is.True);
        }

        [Test]
        public void HorizontalAndIncomingSawsUseDifferentConfiguredColors()
        {
            var (saws, root) = Fixture();
            var horizontal = Create(saws, root, null, 1.2f, 0.1f, SawKind.Horizontal);
            var incoming = Create(saws, root, null, 1.2f, 0.1f, SawKind.Vertical);
            var horizontalColor = horizontal.Find("Blade").GetComponent<Renderer>().sharedMaterial.color;
            var incomingColor = incoming.Find("Blade").GetComponent<Renderer>().sharedMaterial.color;
            Assert.That(Vector4.Distance(horizontalColor, saws.Settings.horizontalColor), Is.LessThan(0.0001f));
            Assert.That(Vector4.Distance(incomingColor, saws.Settings.verticalColor), Is.LessThan(0.0001f));
            Assert.That(Vector4.Distance(horizontalColor, incomingColor), Is.GreaterThan(0.1f),
                "두 종류의 톱은 재질 변환 오차보다 충분히 큰 색 차이를 유지한다.");
            Assert.That(horizontal.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(incoming.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [TestCase(100f)]
        [TestCase(150f)]
        public void LearningPlacementScalesToCourseAndLeavesRoomForEarlyJumps(float length)
        {
            var entries = SawSettings.CreateLearningHorizontalSaws(length);
            Assert.That(entries.Length, Is.EqualTo(20));
            Assert.That(entries[0].distance, Is.EqualTo(length * 0.08f).Within(0.0001f));
            Assert.That(entries[2].distance, Is.EqualTo(length * 0.2f).Within(0.0001f));
            Assert.That(entries[^1].distance, Is.EqualTo(length * 0.98f).Within(0.0001f));
            for (int i = 1; i < entries.Length; i++)
            {
                Assert.That(entries[i].distance, Is.GreaterThan(entries[i - 1].distance));
                Assert.That(entries[i].startSide, Is.Not.EqualTo(entries[i - 1].startSide));
            }
            float firstGap = entries[1].distance - entries[0].distance;
            float finalGap = entries[^1].distance - entries[^2].distance;
            Assert.That(firstGap, Is.GreaterThan(finalGap * 2f));
        }

        (TightropeSaws saws, Transform root) Fixture()
        {
            var owner = Track(new GameObject("Saw visual test"));
            var saws = owner.AddComponent<TightropeSaws>();
            var root = new GameObject("Generated");
            root.transform.SetParent(owner.transform, false);
            resources.Add(root.AddComponent<TightropeSawVisualResources>());
            return (saws, root.transform);
        }

        static void Release(TightropeSawVisualResources owner) => typeof(TightropeSawVisualResources)
            .GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);

        static Transform Create(TightropeSaws saws, Transform parent, GameObject prefab, float diameter, float thickness, SawKind kind = SawKind.Horizontal) =>
            (Transform)typeof(TightropeSaws).GetMethod("CreateSaw", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(saws, new object[] { parent, "Test saw", prefab, diameter, thickness, kind });

        T Track<T>(T value) where T : Object
        {
            owned.Add(value);
            return value;
        }
    }
}
