using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.Cameras.Tests
{
    /// <summary>공용 카메라 프리팹의 실제 편집 모드 pipeline. 게임 코드의 Awake나 캐시는 강제로 초기화하지 않는다.</summary>
    [Category("CommonProgressionRegression")]
    public sealed class CameraEditorPipelineTests
    {
        const string RigPath = "Assets/Resources/Prefabs/Controllers/Camera/PlayerFollowCamera.prefab";

        [TestCase(false)]
        [TestCase(true)]
        public void CameraMeshSurface_PreservesEmptySpaceAndSweepsFacesEdgesAndCorners(bool transformed)
        {
            var go = new GameObject("CameraMeshFixture");
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right * 4, Vector3.up * 4 }, triangles = new[] { 0, 1, 2 } };
            try {
                if (transformed) {
                    go.transform.SetPositionAndRotation(new Vector3(10, 7, -2), Quaternion.Euler(20, 37, -15));
                    go.transform.localScale = new Vector3(-2, 3, .2f);
                }
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var surface = go.AddComponent(GameType("CurtainCall.Cameras.CameraCollisionSurface"));
                surface.GetType().GetField("UseMeshGeometry").SetValue(surface, true);
                var method = surface.GetType().GetMethod("Intersect");
                Vector3 direction = go.transform.forward;
                Vector3 center = go.transform.TransformPoint(new Vector3(1, 1, 0));
                object[] args = { center - direction * 5, direction, .25f, 0f };
                Assert.IsTrue((bool)method.Invoke(surface, args));
                Assert.That((float)args[3], Is.EqualTo(4.75f).Within(.001f), "front face");
                args[0] = center + direction * 5; args[1] = -direction;
                Assert.IsTrue((bool)method.Invoke(surface, args));
                Assert.That((float)args[3], Is.EqualTo(4.75f).Within(.001f), "back face");
                args[0] = go.transform.TransformPoint(new Vector3(3, 3, 0)) - direction * 5; args[1] = direction;
                Assert.IsFalse((bool)method.Invoke(surface, args), "empty area inside mesh bounds stays open");
                args[0] = center + direction * .1f;
                Assert.IsTrue((bool)method.Invoke(surface, args)); Assert.AreEqual(0f, args[3]);
                // Moving the surface invalidates the cached world geometry.
                go.transform.position += direction * 10;
                args[0] = center - direction * 5;
                Assert.IsTrue((bool)method.Invoke(surface, args));
                Assert.That((float)args[3], Is.EqualTo(14.75f).Within(.001f));
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); go.transform.localScale = Vector3.one;
                args[0] = new Vector3(-.15f, 1, -5); args[1] = Vector3.forward;
                Assert.IsTrue((bool)method.Invoke(surface, args));
                Assert.That((float)args[3], Is.EqualTo(4.8f).Within(.001f), "edge grazed only by sphere radius");
                args[0] = new Vector3(-.15f, -.15f, -5);
                Assert.IsTrue((bool)method.Invoke(surface, args));
                Assert.That((float)args[3], Is.EqualTo(5 - Mathf.Sqrt(.25f * .25f - 2 * .15f * .15f)).Within(.001f), "corner");
                Assert.IsNull(go.GetComponent<Collider>());
            } finally { Object.DestroyImmediate(go); Object.DestroyImmediate(mesh); }
        }

        [TestCase(0f)]
        [TestCase(37f)]
        public void CameraOnlySurface_HandlesScaleRotationRadiusAndStartingInside(float angle)
        {
            var go = new GameObject("CameraOnlySurfaceFixture");
            try {
                go.transform.SetPositionAndRotation(new Vector3(10, 7, -2), Quaternion.Euler(0, angle, 0));
                go.transform.localScale = new Vector3(2, 3, .2f);
                var surface = go.AddComponent(GameType("CurtainCall.Cameras.CameraCollisionSurface"));
                var method = surface.GetType().GetMethod("Intersect");
                Vector3 origin = go.transform.position - go.transform.forward * 5;
                object[] args = { origin, go.transform.forward, .25f, 0f };
                Assert.IsTrue((bool)method.Invoke(surface, args));
                Assert.That((float)args[3], Is.EqualTo(4.65f).Within(.001f));
                args[0] = go.transform.position;
                Assert.IsTrue((bool)method.Invoke(surface, args)); Assert.AreEqual(0f, args[3]);
                args[0] = origin + go.transform.right * 5;
                Assert.IsFalse((bool)method.Invoke(surface, args));
                Assert.IsNull(go.GetComponent<Collider>());
            } finally { Object.DestroyImmediate(go); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EditModePipeline_DoesNotApplyRuntimeViewsBeforeAwake(bool hasRequest)
        {
            Assert.That(Application.isPlaying, Is.False);
            var preview = EditorSceneManager.NewPreviewScene();
            ScriptableObject request = null;
            Component director = null;
            Action preparing = null;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
                Assert.That(prefab, Is.Not.Null);
                var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                var camera = rig.GetComponent<CinemachineCamera>();
                var follow = rig.GetComponent<CinemachineFollow>();
                var composer = rig.GetComponent<CinemachineRotationComposer>();
                director = rig.GetComponent(GameType("CurtainCall.Cameras.CameraViewDirector"));
                var extension = (CinemachineExtension)rig.GetComponent(GameType("CurtainCall.Cameras.CameraViewPoseExtension"));
                Assert.That(director, Is.Not.Null);
                Assert.That(Application.IsPlaying(rig), Is.False);
                // 편집기 스크립트 재로드가 사용하는 연결 경로만 실행한다. Director.Awake는 호출하지 않는다.
                typeof(CinemachineExtension).GetMethod("ConnectToVcam", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(extension, new object[] { true });

                int calls = 0;
                preparing = () => calls++;
                director.GetType().GetEvent("PreparingView").AddEventHandler(director, preparing);
                if (hasRequest)
                {
                    request = ScriptableObject.CreateInstance(GameType("CurtainCall.Cameras.CameraViewPreset"));
                    request.hideFlags = HideFlags.HideAndDontSave;
                    request.GetType().GetField("Height").SetValue(request, 30f);
                    request.GetType().GetField("Distance").SetValue(request, 40f);
                    request.GetType().GetField("FieldOfView").SetValue(request, 95f);
                    director.GetType().GetMethod("RequestView").Invoke(director, new object[] { rig, request, 100, Vector3.right });
                }

                Vector3 followBefore = follow.FollowOffset;
                Vector3 lookBefore = composer.TargetOffset;
                float fovBefore = camera.Lens.FieldOfView;
                CursorLockMode lockBefore = Cursor.lockState;
                bool cursorBefore = Cursor.visible;

                Assert.DoesNotThrow(() => camera.InternalUpdateCameraState(Vector3.up, -1f),
                    "편집 모드에서 Awake 이전 Director를 경유해도 예외가 없어야 한다");
                Assert.That(calls, Is.Zero, "편집 미리보기에서 콘텐츠의 플레이용 요청을 평가하지 않는다");
                Assert.That(director.GetType().GetProperty("CurrentView").GetValue(director), Is.Null);
                Assert.That(follow.FollowOffset, Is.EqualTo(followBefore));
                Assert.That(composer.TargetOffset, Is.EqualTo(lookBefore));
                Assert.That(camera.Lens.FieldOfView, Is.EqualTo(fovBefore));
                Assert.That(Cursor.lockState, Is.EqualTo(lockBefore));
                Assert.That(Cursor.visible, Is.EqualTo(cursorBefore));
            }
            finally
            {
                if (director != null && preparing != null)
                    director.GetType().GetEvent("PreparingView").RemoveEventHandler(director, preparing);
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                if (request != null) Object.DestroyImmediate(request);
            }
        }

        [TestCase(0f)] [TestCase(70f)] [TestCase(180f)]
        public void OrdinaryFlightCameraClearsAnEmbeddedPivotWithoutMovingThePlayer(float yaw)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = new GameObject("Embedded flight camera test");
            root.transform.SetPositionAndRotation(new Vector3(12000, 12000, 12000), Quaternion.Euler(0, yaw, 0));
            try
            {
                var actor = new GameObject("Inactive owner"); actor.SetActive(false); actor.transform.SetParent(root.transform, false);
                var player = actor.AddComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                var rig = new GameObject("Inactive camera"); rig.SetActive(false); rig.transform.SetParent(root.transform, false);
                var director = rig.AddComponent(GameType("CurtainCall.Cameras.CameraViewDirector"));
                var preset = Resources.Load("Settings/Camera/FreeThirdPerson");
                director.GetType().GetField("local", fields).SetValue(director, player);
                director.GetType().GetField("selected", fields).SetValue(director, preset);
                director.GetType().GetField("freeThirdPerson", fields).SetValue(director, true);
                float height = (float)preset.GetType().GetField("LookHeight").GetValue(preset);
                var obstacle = new GameObject("Muzzle or low beam"); obstacle.transform.SetParent(root.transform, false);
                obstacle.transform.localPosition = Vector3.up * height;
                var box = obstacle.AddComponent<BoxCollider>(); box.size = new Vector3(.4f, height * 2 + 1f, 4f);
                Physics.SyncTransforms();
                var state = CameraState.Default;
                state.RawPosition = root.transform.TransformPoint(new Vector3(3f, height + 1f, -4f));
                state.RawOrientation = Quaternion.Euler(27f, yaw + 42f, 0f);
                var playerPosition = actor.transform.position;
                object[] args = { state };
                director.GetType().GetMethod("ApplyWallCollision", fields).Invoke(director, args);
                var final = (CameraState)args[0];
                var finalPosition = final.GetFinalPosition();
                Assert.That(Vector3.Distance(finalPosition, box.ClosestPoint(finalPosition)), Is.GreaterThanOrEqualTo(.249f));
                Assert.That(Quaternion.Angle(final.GetFinalOrientation(), state.RawOrientation), Is.LessThan(.02f));
                Assert.That(actor.transform.position, Is.EqualTo(playerPosition));
            }
            finally { Object.DestroyImmediate(root); Physics.SyncTransforms(); }
        }

        [Test]
        public void CorridorWallKeepsTheCameraOnTheRequestedSideDuringRepeatedTurns()
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = new GameObject("Corridor camera fixture");
            root.transform.position = new Vector3(14500, 14500, 14500);
            try
            {
                var actor = new GameObject("Inactive player");
                actor.SetActive(false);
                actor.transform.SetParent(root.transform, false);
                var player = actor.AddComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                var rig = new GameObject("Inactive rig");
                rig.SetActive(false);
                rig.transform.SetParent(root.transform, false);
                var director = rig.AddComponent(GameType("CurtainCall.Cameras.CameraViewDirector"));
                var type = director.GetType();
                var preset = Resources.Load("Settings/Camera/FreeThirdPerson");
                type.GetField("local", fields).SetValue(director, player);
                type.GetField("selected", fields).SetValue(director, preset);
                type.GetField("yaw", fields).SetValue(director, 90f);
                float height = (float)preset.GetType().GetField("LookHeight").GetValue(preset);
                Vector3 pivot = root.transform.position + Vector3.up * height;
                var wall = new GameObject("Wall");
                wall.transform.SetParent(root.transform, false);
                wall.transform.localPosition = new Vector3(-1.35f, 2f, 0f);
                var box = wall.AddComponent<BoxCollider>();
                box.size = new Vector3(.2f, 4f, 16f);
                Physics.SyncTransforms();

                var apply = type.GetMethod("ApplyWallCollision", fields);
                foreach (float turn in new[] { 35f, 70f, 90f, 72f, 90f })
                {
                    var arm = Quaternion.Euler(0f, turn, 0f) * Vector3.back;
                    var state = CameraState.Default;
                    state.RawPosition = pivot + arm * 4f;
                    state.RawOrientation = Quaternion.LookRotation(-arm);
                    object[] args = { state };
                    apply.Invoke(director, args);
                    var output = (CameraState)args[0];
                    Vector3 position = output.GetFinalPosition();
                    Vector3 offset = position - pivot;
                    Assert.That(offset.magnitude, Is.InRange(.8f, 2f));
                    Assert.That(Vector3.Dot(offset.normalized, arm), Is.GreaterThan(.999f));
                    Assert.That(Quaternion.Angle(output.GetFinalOrientation(), state.RawOrientation), Is.LessThan(.02f));
                    Assert.That(Vector3.Distance(position, box.ClosestPoint(position)), Is.GreaterThanOrEqualTo(.249f));
                }
                float clippedDistance = (float)type.GetField("collisionDistance", fields).GetValue(director);
                type.GetField("collisionReturnAfter", fields).SetValue(director, Time.unscaledTime + 1f);
                type.GetField("collisionFrame", fields).SetValue(director, Time.frameCount + 1);
                var clearView = CameraState.Default;
                clearView.RawPosition = pivot + Vector3.right * 4f;
                clearView.RawOrientation = Quaternion.LookRotation(Vector3.left);
                object[] clearArgs = { clearView };
                apply.Invoke(director, clearArgs);
                var clearOffset = ((CameraState)clearArgs[0]).GetFinalPosition() - pivot;
                Assert.That(clearOffset.x, Is.EqualTo(clippedDistance).Within(.002f));
                Assert.That(clearOffset.z, Is.EqualTo(0f).Within(.002f));
                Assert.That(type.GetField("yaw", fields).GetValue(director), Is.EqualTo(90f));
            }
            finally { Object.DestroyImmediate(root); Physics.SyncTransforms(); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OcclusionFadeFindsASiblingRendererAndRestoresItsMaterial(bool startsInside)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = new GameObject("Foreground prop fixture");
            root.transform.position = new Vector3(14000, 14000, 14000);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Component fade = null;
            try
            {
                var target = new GameObject("Camera target").transform; target.SetParent(root.transform, false);
                var rig = new GameObject("Fade rig"); rig.SetActive(false); rig.transform.SetParent(root.transform, false);
                fade = rig.AddComponent(GameType("CurtainCall.Cameras.CameraOcclusionFade"));
                var prop = new GameObject("Dynamic prop"); prop.transform.SetParent(root.transform, false);
                prop.transform.localPosition = new Vector3(0f, 1f, startsInside ? -4f : -2f);
                prop.AddComponent<Rigidbody>().useGravity = false;
                var collider = new GameObject("Collider branch"); collider.transform.SetParent(prop.transform, false);
                collider.AddComponent<BoxCollider>();
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube); visual.transform.SetParent(prop.transform, false);
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                var renderer = visual.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                var rules = Activator.CreateInstance(GameType("CurtainCall.Cameras.CameraOcclusionRules"));
                Physics.SyncTransforms();
                fade.GetType().GetMethod("MarkOccluders", fields).Invoke(fade,
                    new object[] { root.transform.position + new Vector3(0f, 1f, -4f), target, 1f, rules });
                Assert.That(renderer.sharedMaterial, Is.Not.SameAs(material));
                fade.GetType().GetMethod("RestoreAll", fields).Invoke(fade, null);
                Assert.That(renderer.sharedMaterial, Is.SameAs(material));
            }
            finally
            {
                if (fade != null) fade.GetType().GetMethod("RestoreAll", fields).Invoke(fade, null);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(material);
                Physics.SyncTransforms();
            }
        }

        [TestCase(30f)] [TestCase(-40f)] [TestCase(179f)]
        public void RotatingMountPreservesThePlayersMouseLookOffset(float turn)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var root = new GameObject("Moving pole camera");
            root.SetActive(false);
            try
            {
                var type = GameType("CurtainCall.Cameras.CameraViewDirector");
                var director = root.AddComponent(type);
                var yaw = type.GetField("yaw", fields);
                type.GetField("followedHeading", fields).SetValue(director, Vector3.forward);
                yaw.SetValue(director, 40f);
                var heading = Quaternion.Euler(0f, turn, 0f) * Vector3.forward;
                var follow = type.GetMethod("UpdateFollowedHeading", fields);
                follow.Invoke(director, new object[] { heading, true });
                follow.Invoke(director, new object[] { heading, true });
                Assert.That(Mathf.DeltaAngle(turn, (float)yaw.GetValue(director)), Is.EqualTo(40f).Within(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        // Assembly-CSharp는 asmdef에서 직접 참조할 수 없으므로 기존 카메라 PlayMode 테스트와 같은 공개 API 호출 방식을 쓴다.
        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false)).First(type => type != null);
    }
}
