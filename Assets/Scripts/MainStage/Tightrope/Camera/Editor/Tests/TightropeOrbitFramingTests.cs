using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed class TightropeOrbitFramingTests
    {
        static readonly TightropeFramingMath.Frame Baseline = new(3.75f, 4.5f, 1.2f, 1.75f);

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(180f)]
        [TestCase(270f)]
        public void FixedBaseline_MatchesHumanCannonThirdPersonAtNeutralPitch(float courseYaw)
        {
            var source = Resources.Load<CameraViewPreset>("Settings/Camera/FreeThirdPerson");
            Assert.That(source, Is.Not.Null);
            var rules = CurtainCall.Settings.GameSettings.Tightrope.Camera;
            var baseline = new TightropeFramingMath.Frame(rules.Height, rules.Distance, rules.LookHeight, rules.LookAhead);
            var feet = new Vector3(10f, 7f, -20f);
            var rotation = Quaternion.Euler(0f, courseYaw, 0f);
            var pose = TightropeFramingMath.CameraPose(feet, rotation, rotation * Vector3.forward, baseline, out _);
            var cannonPosition = CameraViewDirector.FreeThirdPersonPose(source, feet, courseYaw, 0f, out var cannonRotation);
            Assert.That(source.FieldOfView, Is.EqualTo(60f));
            Assert.That(Vector3.Distance(pose.position, cannonPosition), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(pose.rotation, cannonRotation), Is.LessThan(.01f));
            Assert.That((pose.rotation * Vector3.right).y, Is.EqualTo(0f).Within(.0001f));
        }

        [TestCase(0f, 60f, 1.777778f)]
        [TestCase(4.5f, 60f, 1.777778f)]
        [TestCase(0f, 60f, .65f)]
        [TestCase(4.5f, 60f, .65f)]
        [TestCase(0f, 10f, 1.777778f)]
        public void FixedView_FitsStackCompanionAndFrontSawAcrossCourseDirections(
            float riderHeight, float fov, float aspect)
        {
            var origin = new Vector3(30f, 8f, -15f);
            var feet = origin + Vector3.up * riderHeight;
            var localSubjects = new Bounds(new Vector3(0f, 3.4f, 0f), new Vector3(.6f, 6.8f, .6f));
            localSubjects.Encapsulate(new Bounds(new Vector3(3f, .85f, 0f), new Vector3(.6f, 1.7f, .6f)));
            localSubjects.Encapsulate(new Bounds(new Vector3(0f, .5f, 8f), Vector3.one * 1.2f));
            var cameraObject = new GameObject("Fixed framing viewport");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = fov;
                camera.aspect = aspect;
                foreach (float courseYaw in new[] { 0f, 90f, 180f, 270f })
                {
                    var rotation = Quaternion.Euler(0, courseYaw, 0);
                    var forward = rotation * Vector3.forward;
                    var subjects = new Bounds(origin + rotation * localSubjects.center, Vector3.zero);
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var sign = new Vector3((corner & 1) == 0 ? -1 : 1,
                            (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                        subjects.Encapsulate(origin + rotation * (localSubjects.center + Vector3.Scale(localSubjects.extents, sign)));
                    }
                    var frame = TightropeFramingMath.Fit(subjects, feet, rotation, forward,
                        Baseline, fov, aspect, 1000f, .5f, 2f);
                    var pose = TightropeFramingMath.CameraPose(feet, rotation, forward, frame, out _);
                    cameraObject.transform.SetPositionAndRotation(pose.position, pose.rotation);
                    var heading = Vector3.ProjectOnPlane(pose.rotation * Vector3.forward, Vector3.up).normalized;
                    Assert.That(Vector3.Dot(heading, forward), Is.GreaterThan(.999f), "구도 확대 후에도 코스 전방을 바라본다.");
                    Assert.That(Vector3.Dot(pose.position - feet, forward), Is.LessThan(0f), "카메라는 코스 뒤쪽에 고정된다.");
                    subjects.Expand(1f);
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var sign = new Vector3((corner & 1) == 0 ? -1 : 1,
                            (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                        var viewport = camera.WorldToViewportPoint(subjects.center + Vector3.Scale(subjects.extents, sign));
                        Assert.That(viewport.z, Is.GreaterThan(0f));
                        Assert.That(viewport.x, Is.InRange(-.001f, 1.001f), $"courseYaw={courseYaw}, corner={corner}");
                        Assert.That(viewport.y, Is.InRange(-.001f, 1.001f), $"courseYaw={courseYaw}, corner={corner}");
                    }
                }
            }
            finally { Object.DestroyImmediate(cameraObject); }
        }

        [Test]
        public void IncomingCompanion_SmoothsBothZoomDirectionsWithoutSnapping()
        {
            var wide = new TightropeFramingMath.Frame(6f, 10f, 3f, 0f);
            var paused = TightropeFramingMath.Smooth(Baseline, wide, .45f, 0f);
            Assert.That(paused.Distance, Is.EqualTo(Baseline.Distance));
            var expanded = TightropeFramingMath.Smooth(Baseline, wide, .45f, 1f / 60f);
            Assert.That(expanded.Distance, Is.GreaterThan(Baseline.Distance).And.LessThan(Baseline.Distance + .25f));
            Assert.That(expanded.LookHeight, Is.GreaterThan(Baseline.LookHeight).And.LessThan(wide.LookHeight));
            var once = TightropeFramingMath.Smooth(expanded, Baseline, .45f, .1f);
            var twice = TightropeFramingMath.Smooth(
                TightropeFramingMath.Smooth(expanded, Baseline, .45f, .05f), Baseline, .45f, .05f);
            Assert.That(once.Distance, Is.GreaterThan(Baseline.Distance).And.LessThan(wide.Distance));
            Assert.That(twice.Distance, Is.EqualTo(once.Distance).Within(.0001f));
            Assert.That(twice.LookHeight, Is.EqualTo(once.LookHeight).Within(.0001f));
        }

        [Test]
        public void RuntimePresets_KeepSharedAssetsIntactAndEditModeDoesNotActivateView()
        {
            var settings = Resources.Load<CameraViewSettings>("Settings/Camera/CameraViewSettings");
            Assert.That(settings, Is.Not.Null);
            var third = Resources.Load<CameraViewPreset>("Settings/Camera/FreeThirdPerson");
            Assert.That(third, Is.Not.Null);
            Assert.That(settings.DefaultThirdPerson, Is.SameAs(third), "플랫폼 기본 시점은 공용 자유 3인칭이다(Task-089).");
            float thirdFov = third.FieldOfView, thirdDistance = third.Distance;
            var rig = new GameObject("Tightrope preset ownership");
            try
            {
                var framing = rig.AddComponent<TightropeCameraFraming>();
                var type = typeof(TightropeCameraFraming);
                var director = rig.GetComponent<CameraViewDirector>();
                typeof(CameraViewDirector).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(director, settings);
                type.GetField("director", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(framing, director);
                type.GetMethod("EnsurePresets", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(framing, null);
                var platformCopy = (CameraViewPreset)type.GetField("platformView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(framing);
                var thirdCopy = (CameraViewPreset)type.GetField("preset", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(framing);
                Assert.That(platformCopy, Is.Not.SameAs(third));
                Assert.That(thirdCopy, Is.Not.SameAs(third));
                Assert.That(platformCopy.Kind, Is.EqualTo(CameraViewKind.FreeThirdPerson), "플랫폼도 자유 3인칭");
                Assert.That(platformCopy.Distance, Is.EqualTo(third.Distance), "플랫폼은 공용 값 그대로");
                Assert.That(thirdCopy.Kind, Is.EqualTo(CameraViewKind.FreeThirdPerson));
                Assert.That(thirdCopy.FieldOfView, Is.EqualTo(third.FieldOfView));
                Assert.That(thirdCopy.MouseSensitivity, Is.EqualTo(third.MouseSensitivity));
                Assert.That(thirdCopy.PitchLimit, Is.EqualTo(third.PitchLimit));
                Assert.That(thirdCopy.TransitionDuration, Is.EqualTo(.18f));
                platformCopy.Distance = 9f;
                thirdCopy.FieldOfView = 81f;
                Assert.That(third.Distance, Is.EqualTo(thirdDistance));
                Assert.That(third.FieldOfView, Is.EqualTo(thirdFov));
                Assert.That(framing.CurrentMode, Is.EqualTo(CameraViewKind.FreeThirdPerson));
                Assert.That(framing.IsLocalViewActive, Is.False);
            }
            finally { Object.DestroyImmediate(rig); }
            Assert.That(third, Is.Not.Null);
        }

        [TestCase(CampaignPhase.None, true)]
        [TestCase(CampaignPhase.Intro, true)]
        [TestCase(CampaignPhase.Performance, false)]
        [TestCase(CampaignPhase.RestTime, false)]
        [TestCase(CampaignPhase.Night, false)]
        public void LocalViewScope_IncludesPlatformPracticeButExcludesOtherPerformanceAndRest(
            CampaignPhase phase, bool expected)
        {
            var current = typeof(TightropeCourse).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = current.GetValue(null);
            var root = new GameObject("Camera traversal boundary");
            var rig = new GameObject("Traversal camera");
            var person = new GameObject("Traversal player");
            try
            {
                var course = root.AddComponent<TightropeCourse>();
                var bridge = root.AddComponent<TightropeCampaignBridge>();
                var campaign = root.AddComponent<CampaignRun>();
                typeof(CampaignRun).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(campaign, new CampaignSnapshot { Phase = phase, TrickId = "HumanCannon" });
                typeof(TightropeCampaignBridge).GetField("<Campaign>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(bridge, campaign);
                current.SetValue(null, course);
                var framing = rig.AddComponent<TightropeCameraFraming>();
                var director = rig.GetComponent<CameraViewDirector>();
                typeof(TightropeCameraFraming).GetField("director", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(framing, director);
                director.SetLocalPlayer(person.AddComponent<NetworkPlayer>());
                Assert.That(course.IsGameplayActive, Is.False);
                Assert.That(typeof(TightropeCameraFraming).GetProperty("CanUseLocalView",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(framing), Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(rig);
                Object.DestroyImmediate(person);
                Object.DestroyImmediate(root);
                current.SetValue(null, previous);
            }
        }
    }
}
