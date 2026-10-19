using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CurtainCall.Tightrope.Saws;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    /// <summary>외곽선의 원본 보존·공용 투명화 호환·생성 메시/재질 수명을 검사한다.</summary>
    public sealed class TightropeOutlineTests
    {
        readonly List<Object> owned = new();
        readonly List<TightropeOutline> outlines = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var outline in outlines) if (outline != null) Invoke(outline, "OnDisable");
            outlines.Clear();
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [Test]
        public void AttachIsIdempotentAndPreservesOriginalGeometryMaterialAndCollider()
        {
            var cube = Cube();
            var originalMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            var originalMaterial = cube.GetComponent<MeshRenderer>().sharedMaterial;
            var collider = cube.GetComponent<BoxCollider>();
            var outline = Attach(cube);
            Assert.That(TightropeOutline.AttachToHierarchy(cube), Is.SameAs(outline));
            Invoke(outline, "Build");
            var shell = Shell(cube);
            Assert.That(cube.GetComponents<TightropeOutline>().Length, Is.EqualTo(1));
            Assert.That(cube.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(originalMesh));
            Assert.That(cube.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(originalMaterial));
            Assert.That(cube.GetComponent<BoxCollider>(), Is.SameAs(collider));
            Assert.That(cube.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));
            Assert.That(shell.GetComponent<Collider>(), Is.Null);
            Assert.That(shell.sharedMaterial.shader.name, Is.EqualTo("CurtainCall/Tightrope/Outline"));
            Assert.That(shell.sharedMaterial.GetFloat("_OutlinePixels"), Is.EqualTo(2f));
            Assert.That(shell.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
        }

        [Test]
        public void SawWidthDoesNotChangeOtherOutlinesOrDuplicateTheirSharedMaterial()
        {
            var saw = Cube();
            var platform = Cube();
            var sawOutline = Attach(saw);
            var platformOutline = Attach(platform);
            Invoke(sawOutline, "Build");
            Invoke(platformOutline, "Build");
            sawOutline.SetWidth(3f);
            var properties = new MaterialPropertyBlock();
            Shell(saw).GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_OutlinePixels"), Is.EqualTo(3f));
            Shell(platform).GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_OutlinePixels"), Is.EqualTo(2f));
            Assert.That(Shell(saw).sharedMaterial, Is.SameAs(Shell(platform).sharedMaterial));
            Invoke(sawOutline, "Clear");
            Invoke(sawOutline, "Build");
            Shell(saw).GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_OutlinePixels"), Is.EqualTo(3f), "재생성 뒤에도 톱날 전용 굵기를 유지한다.");
        }

        [Test]
        public void SharedMeshesAndMaterialAreReleasedOnlyAfterTheirLastUser()
        {
            var first = Cube();
            var second = Cube();
            var firstOutline = Attach(first);
            var secondOutline = Attach(second);
            Invoke(firstOutline, "Build");
            Invoke(secondOutline, "Build");
            var mesh = Shell(first).GetComponent<MeshFilter>().sharedMesh;
            var material = Shell(first).sharedMaterial;
            Assert.That(Shell(second).GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
            Assert.That(Shell(second).sharedMaterial, Is.SameAs(material));
            Invoke(firstOutline, "OnDisable");
            Assert.That(mesh != null && material != null, Is.True);
            Invoke(secondOutline, "OnDisable");
            Assert.That(mesh == null && material == null, Is.True, "마지막 사용자가 떠나면 생성 자원을 해제한다.");
            Assert.That(first.GetComponent<MeshFilter>().sharedMesh != null, Is.True, "공유 원본 메시를 해제하면 안 된다.");
        }

        [Test]
        public void RebuildDoesNotAccumulateShellsOrGeneratedResources()
        {
            var cube = Cube();
            var outline = Attach(cube);
            for (int i = 0; i < 4; i++)
            {
                Invoke(outline, "Build");
                Invoke(outline, "Build");
                Assert.That(cube.GetComponentsInChildren<MeshRenderer>().Count(TightropeOutline.IsOutlineRenderer), Is.EqualTo(1));
                var shell = Shell(cube);
                var mesh = shell.GetComponent<MeshFilter>().sharedMesh;
                var material = shell.sharedMaterial;
                Invoke(outline, "Clear");
                Assert.That(shell == null && mesh == null && material == null, Is.True);
                Assert.That(cube.transform.childCount, Is.Zero);
            }
        }

        [Test]
        public void NestedPrefabOutlinesHaveOneOwnerForEachSourceRenderer()
        {
            var root = Track(new GameObject("Outlined prefab"));
            var cube = Cube();
            cube.transform.SetParent(root.transform, false);
            var child = Attach(cube);
            Invoke(child, "Build");
            var parent = Attach(root);
            Invoke(parent, "Build");
            Assert.That(root.GetComponentsInChildren<MeshRenderer>().Count(TightropeOutline.IsOutlineRenderer), Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<MeshRenderer>().Count(r => !TightropeOutline.IsOutlineRenderer(r)), Is.EqualTo(1));
        }

        [Test]
        public void TransparentSourceHidesShellBeforeRenderingAndOpaqueSourceRestoresIt()
        {
            var cube = Cube();
            var source = cube.GetComponent<MeshRenderer>();
            var material = Track(new Material(Shader.Find("Universal Render Pipeline/Unlit")));
            source.sharedMaterial = material;
            var outline = Attach(cube);
            Invoke(outline, "Build");
            var shell = Shell(cube);
            Assert.That(shell.enabled, Is.True);
            material.renderQueue = (int)RenderQueue.Transparent;
            Invoke(outline, "BeforeCameraRendering", default(ScriptableRenderContext), null);
            Assert.That(shell.enabled, Is.False);
            material.renderQueue = (int)RenderQueue.Geometry;
            material.SetFloat("_Surface", 1f);
            Invoke(outline, "BeforeCameraRendering", default(ScriptableRenderContext), null);
            Assert.That(shell.enabled, Is.False);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_ZWrite", 1f);
            Invoke(outline, "BeforeCameraRendering", default(ScriptableRenderContext), null);
            Assert.That(shell.enabled, Is.True);
            Assert.That(source.sharedMaterial, Is.SameAs(material));
        }

        [Test]
        public void HiddenSourceCannotLeaveAnOutlineVisible()
        {
            var cube = Cube();
            var source = cube.GetComponent<MeshRenderer>();
            var outline = Attach(cube);
            Invoke(outline, "Build");
            var shell = Shell(cube);
            source.enabled = false;
            Invoke(outline, "BeforeCameraRendering", default(ScriptableRenderContext), null);
            Assert.That(shell.enabled, Is.False);
            source.enabled = true;
            source.forceRenderingOff = true;
            Invoke(outline, "BeforeCameraRendering", default(ScriptableRenderContext), null);
            Assert.That(shell.forceRenderingOff, Is.True);
            source.forceRenderingOff = false;
            Invoke(outline, "BeforeCameraRendering", default(ScriptableRenderContext), null);
            Assert.That(shell.enabled && !shell.forceRenderingOff, Is.True);
        }

        [Test]
        public void ReadableMeshWithoutNormalsGetsItsOwnGeneratedNormals()
        {
            var go = Track(new GameObject("Mesh without normals"));
            var mesh = Track(new Mesh());
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
            var outline = Attach(go);
            Invoke(outline, "Build");
            Assert.That(mesh.normals, Is.Empty);
            Assert.That(Shell(go).GetComponent<MeshFilter>().sharedMesh.normals.Length, Is.EqualTo(3));
        }

        [Test]
        public void CameraCanQuerySawsBeforeInitializationWithoutException()
        {
            var go = Track(new GameObject("Uninitialized saws"));
            var saws = go.AddComponent<TightropeSaws>();
            Assert.That(saws.TryGetHorizontalPosition(0, out var position), Is.False);
            Assert.That(position, Is.EqualTo(Vector3.zero));
            Assert.That(saws.TryGetHorizontalPosition(-1, out _), Is.False);
            Assert.That(saws.TryGetHorizontalPosition(1000, out _), Is.False);
        }

        GameObject Cube() => Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
        TightropeOutline Attach(GameObject target)
        {
            var outline = TightropeOutline.AttachToHierarchy(target);
            outlines.Add(outline);
            return outline;
        }
        T Track<T>(T value) where T : Object { value.hideFlags = HideFlags.DontSave; owned.Add(value); return value; }
        static MeshRenderer Shell(GameObject source) => source.GetComponentsInChildren<MeshRenderer>().Single(TightropeOutline.IsOutlineRenderer);
        static object Invoke(object target, string method, params object[] arguments) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    }
}
