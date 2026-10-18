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
    public sealed class CameraEditorPipelineTests
    {
        const string RigPath = "Assets/Resources/Prefabs/Controllers/Camera/PlayerFollowCamera.prefab";

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

        // Assembly-CSharp는 asmdef에서 직접 참조할 수 없으므로 기존 카메라 PlayMode 테스트와 같은 공개 API 호출 방식을 쓴다.
        static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false)).First(type => type != null);
    }
}
