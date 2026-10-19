using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest]
        public IEnumerator HumanCannonThirdPersonPresetMatchesRopeOrbitAtDifferentAngles()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            yield return StandOnRopeForSawTest(1, 4f);
            var director = (Component)Static("CurtainCall.Cameras.CameraViewDirector", "Current");
            yield return Until(() => (bool)Get(director, "IsFreeThirdPerson"), 3, "줄 위 공전 시점 적용");
            var source = Resources.Load("Settings/Camera/FreeThirdPerson", GameType("CurtainCall.Cameras.CameraViewPreset"));
            Assert.IsNotNull(source);
            object view = Get(director, "CurrentView");
            Assert.AreEqual("FreeThirdPerson", Get(view, "Kind").ToString());
            Assert.IsTrue((bool)Get(director, "UsesMouseLook"));
            Assert.AreNotSame(source, view, "동료 구도 확대가 인간대포의 공용 프리셋을 변경하면 안 된다.");
            foreach (var angle in new[] { Vector2.zero, new Vector2(90f, 25f), new Vector2(235f, -20f) })
            {
                // 실제 마우스/화면을 조작하지 않고 공용 입력이 적분하는 시선 상태를 합성한다.
                Set(director, "yaw", angle.x);
                Set(director, "pitch", angle.y);
                yield return new WaitForSeconds(.4f);
                object[] args = { source, player.transform.position, angle.x, angle.y, Quaternion.identity };
                var expectedPosition = (Vector3)GameType("CurtainCall.Cameras.CameraViewDirector")
                    .GetMethod("FreeThirdPersonPose").Invoke(null, args);
                Assert.That(Camera.main.fieldOfView, Is.EqualTo((float)Get(source, "FieldOfView")).Within(.001f));
                Assert.That(Vector3.Distance(Camera.main.transform.position, expectedPosition), Is.LessThan(.003f));
                Assert.That(Quaternion.Angle(Camera.main.transform.rotation, (Quaternion)args[4]), Is.LessThan(.01f));
                Assert.That(Camera.main.transform.right.y, Is.EqualTo(0f).Within(.0001f));
                Assert.That(player.transform.forward, Is.EqualTo((Vector3)Get(course, "Forward")),
                    "시점 공전이 줄 위 캐릭터의 코스 방향을 돌리면 안 된다.");
            }
        }

        [UnityTest]
        public IEnumerator OrbitCameraTracksActualSoloMotionAcrossFrameRates()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            var director = (Component)Static("CurtainCall.Cameras.CameraViewDirector", "Current");
            Assert.IsNotNull(Camera.main);
            var probeObject = new GameObject("Tightrope actual camera pose probe");
            var probe = probeObject.AddComponent<TightropeCameraLatePoseProbe>();
            probe.Subject = player.transform;
            probe.Output = Camera.main;
            float previousCapture = Time.captureDeltaTime;
            try
            {
                foreach (int fps in new[] { 30, 60, 120, 0 })
                {
                    Time.captureDeltaTime = 1f / 60f;
                    probe.Samples.Clear();
                    probe.Recording = false;
                    yield return StandOnRopeForSawTest(1, 4f);
                    for (int frame = 0; frame < 60; frame++) yield return null;
                    Assert.AreEqual("FreeThirdPerson", Get(Get(director, "CurrentView"), "Kind").ToString());
                    Assert.IsTrue((bool)Get(director, "UsesMouseLook"));
                    Set(director, "yaw", 125f);
                    Set(director, "pitch", 15f);
                    yield return null;

                    // Capture after both movement and Cinemachine output, rather than reading
                    // a current player transform against the previous frame's Camera.main.
                    probe.Recording = true;
                    int frames = fps > 0 ? fps * 3 : 180;
                    for (int frame = 0; frame < frames; frame++)
                    {
                        Time.captureDeltaTime = fps > 0 ? 1f / fps
                            : frame % 3 == 0 ? 1f / 30f : 1f / 120f;
                        Call(mover, "SetMoveInput", 1f);
                        yield return null;
                    }
                    probe.Recording = false;
                    Assert.That(probe.Samples.Count, Is.GreaterThan(10));
                    float elapsed = 0f, maxStepError = 0f, maxRotationStep = 0f, maxScreenStep = 0f;
                    float smallestRatio = float.PositiveInfinity, largestRatio = float.NegativeInfinity;
                    int compared = 0;
                    Vector3 forward = (Vector3)Get(course, "Forward");
                    for (int i = 1; i < probe.Samples.Count; i++)
                    {
                        var before = probe.Samples[i - 1];
                        var after = probe.Samples[i];
                        elapsed += after.DeltaTime;
                        if (elapsed < 1f) continue; // Exclude the initial view transition and target warp.
                        Vector3 playerStep = after.Feet - before.Feet;
                        Vector3 cameraStep = after.Position - before.Position;
                        float along = Vector3.Dot(playerStep, forward);
                        if (along < .00001f) continue;
                        float ratio = Vector3.Dot(cameraStep, forward) / along;
                        smallestRatio = Mathf.Min(smallestRatio, ratio);
                        largestRatio = Mathf.Max(largestRatio, ratio);
                        maxStepError = Mathf.Max(maxStepError, (cameraStep - playerStep).magnitude);
                        maxRotationStep = Mathf.Max(maxRotationStep, Quaternion.Angle(before.Rotation, after.Rotation));
                        maxScreenStep = Mathf.Max(maxScreenStep, Vector2.Distance(before.Viewport, after.Viewport));
                        compared++;
                    }
                    Debug.Log($"[TightropeActualCameraProbe] fps={(fps == 0 ? "variable" : fps.ToString())}, samples={compared}, maxStepError={maxStepError:F6}, minFollowRatio={smallestRatio:F4}, maxFollowRatio={largestRatio:F4}, maxRotationStep={maxRotationStep:F5}, maxViewportStep={maxScreenStep:F6}");
                    Assert.That(compared, Is.GreaterThan(15));
                    Assert.That(maxStepError, Is.LessThan(.001f), "일정한 단독 전진에서 실제 카메라 이동이 플레이어 이동을 멈추거나 뒤늦게 따라가면 안 된다.");
                    Assert.That(smallestRatio, Is.GreaterThan(.98f), "프레임 간격이 바뀌어도 카메라 추종 속도가 플레이어보다 느려지면 안 된다.");
                    Assert.That(largestRatio, Is.LessThan(1.02f), "밀린 카메라 이동을 다음 프레임에 몰아서 따라잡으면 안 된다.");
                    Assert.That(maxRotationStep, Is.LessThan(.05f), "직진 중 실제 시선 방향이 프레임마다 흔들리면 안 된다.");
                    Assert.That(maxScreenStep, Is.LessThan(.0001f), "균일한 전진에서 화면 속 발 위치가 흔들리면 안 된다.");
                }
            }
            finally
            {
                Time.captureDeltaTime = previousCapture;
                Object.Destroy(probeObject);
            }
        }
    }

    // Brain outputs in LateUpdate. This observer only records the completed pose;
    // it never calls ManualUpdate or writes a camera/player transform.
    [DefaultExecutionOrder(32000)]
    public sealed class TightropeCameraLatePoseProbe : MonoBehaviour
    {
        public struct Sample
        {
            public Vector3 Feet, Position;
            public Quaternion Rotation;
            public Vector2 Viewport;
            public float DeltaTime;
        }

        public Transform Subject;
        public Camera Output;
        public bool Recording;
        public readonly List<Sample> Samples = new();

        void LateUpdate()
        {
            if (!Recording || Subject == null || Output == null) return;
            Samples.Add(new Sample
            {
                Feet = Subject.position,
                Position = Output.transform.position,
                Rotation = Output.transform.rotation,
                Viewport = Output.WorldToViewportPoint(Subject.position),
                DeltaTime = Time.deltaTime
            });
        }
    }
}
