using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
    /// <summary>임시 씬을 RenderTexture에만 그린다. 화면 조작이나 실제 입력 검증이 아니다.</summary>
    public sealed class HumanCannonOutlinePlayTests
    {
        const string OutlinePath = "Assets/Scenes/Tests/MainStage/HumanCannon/Materials/human-cannon-outline.mat";
        Scene scene;
        Camera camera;
        MeshRenderer source, outline;
        Material original, faded;
        RenderTexture target;
        Texture2D pixels;

        [SetUp]
        public void SetUp()
        {
            // PreviewScene은 런타임 OnEnable을 실행하지 않으므로 실제 임시 씬에서
            // 컴포넌트 구독까지 검증한다. 기존 코스와 떨어진 위치를 화면 밖에만 렌더한다.
            scene = SceneManager.CreateScene("Outline regression " + Guid.NewGuid());
            var origin = new Vector3(10000, 10000, 10000);
            var cameraObject = new GameObject("Outline regression camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.transform.position = origin + new Vector3(0, 0, -5);
            camera.orthographic = true;
            camera.orthographicSize = 2;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 10;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.allowHDR = camera.allowMSAA = false;
            target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            pixels = new Texture2D(128, 128, TextureFormat.RGB24, false);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(cube, scene);
            cube.transform.position = origin;
            cube.transform.localScale = Vector3.one * 2;
            source = cube.GetComponent<MeshRenderer>();
            original = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            original.SetColor("_BaseColor", Color.white);
            source.sharedMaterial = original;
            var outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
            Assert.IsNotNull(outlineMaterial);
            GameType("CurtainCall.HumanCannon.HumanCannonOutline")
                .GetMethod("AttachToHierarchy", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { cube, outlineMaterial });
            outline = cube.GetComponentsInChildren<MeshRenderer>().Single(renderer => renderer != source);
            Assert.IsNull(outline.GetComponent<Collider>(), "가림 회피용 외곽선에 별도 충돌체를 만들지 않는다.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (camera != null) camera.targetTexture = null;
            if (scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
            if (faded != null) Object.Destroy(faded);
            if (original != null) Object.Destroy(original);
            if (pixels != null) Object.Destroy(pixels);
            if (target != null) { target.Release(); Object.Destroy(target); }
            yield return null;
        }

        [Test]
        public void OcclusionFade_HidesOpaqueShellBeforeRender_AndRestoresOutlineImmediately()
        {
            Render();
            Assert.That(DarkPixels(), Is.GreaterThan(20), "불투명 물체의 실제 외곽선이 그려져야 한다.");
            AssertWhiteCenter();

            // 공용 가림 코드가 생성하는 것과 동일한 투명 재질로 바꾼다.
            // 프레임을 넘기지 않으므로 LateUpdate만 확인하는 구현은 이 검사를 통과하지 못한다.
            var makeTransparent = GameType("CurtainCall.Cameras.CameraOcclusionFade")
                .GetMethod("MakeTransparent", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(makeTransparent);
            faded = (Material)makeTransparent.Invoke(null, new object[] { original });
            Assert.IsNotNull(faded);
            faded.SetColor("_BaseColor", new Color(1, 1, 1, 0.2f));
            source.sharedMaterial = faded;
            Render();
            AssertWhiteCenter();
            Assert.That(DarkPixels(), Is.Zero, "투명 물체 뒤에 검은 면이나 외곽선이 남으면 안 된다.");
            Assert.IsFalse(outline.enabled);

            // 가림 복구와 같은 프레임에 외곽선도 돌아오며 공유 원본 재질은 변경하지 않는다.
            source.sharedMaterial = original;
            Render();
            AssertWhiteCenter();
            Assert.IsTrue(outline.enabled);
            Assert.That(DarkPixels(), Is.GreaterThan(20));
            Assert.That(original.GetFloat("_Surface"), Is.Zero);
            Assert.That(original.GetColor("_BaseColor"), Is.EqualTo(Color.white));
        }

        [Test]
        public void SourceVisibility_IsRespectedAndCanRecoverOnTheNextRender()
        {
            source.enabled = false;
            Render();
            Assert.That(DarkPixels(), Is.Zero);
            Assert.IsFalse(outline.enabled);
            source.enabled = true;
            source.forceRenderingOff = true;
            Render();
            Assert.That(DarkPixels(), Is.Zero);
            Assert.IsTrue(outline.forceRenderingOff);
            source.forceRenderingOff = false;
            Render();
            Assert.IsFalse(outline.forceRenderingOff);
            Assert.That(DarkPixels(), Is.GreaterThan(20));
        }

        void Render()
        {
            camera.Render();
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                pixels.Apply();
            }
            finally { RenderTexture.active = previous; }
        }

        void AssertWhiteCenter()
        {
            Color color = pixels.GetPixel(64, 64);
            Assert.That(Mathf.Min(color.r, color.g, color.b), Is.GreaterThan(0.98f),
                "흰 물체와 흰 배경 사이에 불투명 외곽 메시의 내부가 비치면 안 된다.");
        }

        int DarkPixels() => pixels.GetPixels().Count(color => Mathf.Min(color.r, color.g, color.b) < 0.8f);

        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false)).First(type => type != null);
    }
}
