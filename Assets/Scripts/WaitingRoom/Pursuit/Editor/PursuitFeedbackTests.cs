using System.Reflection;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public sealed class PursuitFeedbackTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        Scene scene;
        PursuitFeedback feedback, second;
        GameObject observer, source;
        GameSettings settings, previousSettings;
        WorldUiSettings world;
        WaitingRoomSettings waiting;
        Object previousOwner;
        NetworkPlayer previousSpectator;
        CameraViewDirector previousCamera;
        bool previouslySpectating;

        [SetUp]
        public void SetUp()
        {
            previousSettings = (GameSettings)typeof(GameSettings).GetField("current", Static).GetValue(null);
            settings = ScriptableObject.CreateInstance<GameSettings>();
            waiting = ScriptableObject.CreateInstance<WaitingRoomSettings>();
            world = ScriptableObject.CreateInstance<WorldUiSettings>();
            typeof(GameSettings).GetField("waitingRoom", Instance).SetValue(settings, waiting);
            typeof(GameSettings).GetField("worldUi", Instance).SetValue(settings, world);
            typeof(GameSettings).GetField("current", Static).SetValue(null, settings);
            previousOwner = (Object)typeof(PursuitFeedback).GetField("displayOwner", Static).GetValue(null);
            typeof(PursuitFeedback).GetField("displayOwner", Static).SetValue(null, null);
            previouslySpectating = CameraSpectator.IsSpectating;
            previousSpectator = CameraSpectator.Target;
            previousCamera = CameraViewDirector.Current;
            SetStaticProperty(typeof(CameraSpectator), "IsSpectating", false);
            SetStaticProperty(typeof(CameraSpectator), "Target", null);
            SetStaticProperty(typeof(CameraViewDirector), "Current", null);
            scene = EditorSceneManager.NewPreviewScene();
            observer = NewObject("Local player"); source = NewObject("Escape source");
            feedback = NewObject("Shared feedback").AddComponent<PursuitFeedback>();
            Call(feedback, "OnEnable");
            waiting.Pursuit.PulseAmount = 0; // Deterministic mesh alpha; production pulse remains unchanged.
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (feedback) Call(feedback, "OnDisable");
                if (second) Call(second, "OnDisable");
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
            finally
            {
                typeof(PursuitFeedback).GetField("displayOwner", Static).SetValue(null, previousOwner);
                SetStaticProperty(typeof(CameraSpectator), "IsSpectating", previouslySpectating);
                SetStaticProperty(typeof(CameraSpectator), "Target", previousSpectator);
                SetStaticProperty(typeof(CameraViewDirector), "Current", previousCamera);
                typeof(GameSettings).GetField("current", Static).SetValue(null, previousSettings);
                Object.DestroyImmediate(settings); Object.DestroyImmediate(world); Object.DestroyImmediate(waiting);
            }
        }

        GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        PursuitThreatSample Escape(bool targeted, float distance, bool ready = true, double round = 10,
            GameObject focus = null) => PursuitThreatSample.Escape(focus ? focus : observer, source, round,
                ready && world.escapeThreatEnabled, targeted, distance, world.escapeThreatDistance);

        void Present(PursuitThreatSample sample, float dt = 1) => Call(feedback, "Present", sample, dt);
        Canvas Canvas => feedback.GetComponentInChildren<Canvas>(true);
        PursuitEdgeGraphic Edge => feedback.GetComponentInChildren<PursuitEdgeGraphic>(true);

        void AssertHidden()
        {
            Assert.That(feedback.Intensity, Is.Zero);
            Assert.That(feedback.IsChased, Is.False);
            Assert.That(Canvas.enabled, Is.False);
            using var mesh = new VertexHelper();
            Call(Edge, "OnPopulateMesh", mesh);
            Assert.That(mesh.currentVertCount, Is.Zero);
        }

        [TestCase(true, 100f, true)]
        [TestCase(false, 17f, true)]
        [TestCase(false, 18f, true)]
        [TestCase(false, 18.01f, false)]
        public void EscapeTargetsOrConfiguredProximityDriveTheExistingEdge(bool targeted, float distance, bool danger)
        {
            world.escapeThreatDistance = 18;
            Present(Escape(targeted, distance));
            Assert.That(feedback.IsChased, Is.EqualTo(danger));
            Assert.That(Canvas.enabled, Is.EqualTo(danger));
            Assert.That(feedback.Intensity > 0, Is.EqualTo(danger));
            Assert.That(Edge.raycastTarget, Is.False);
        }

        [Test]
        public void DistanceSettingChangesProximityButNeverDropsAnExplicitFarTarget()
        {
            world.escapeThreatDistance = 8;
            Present(Escape(false, 12)); AssertHidden();
            world.escapeThreatDistance = 18;
            Present(Escape(false, 12)); Assert.That(feedback.Intensity, Is.GreaterThan(0));
            world.escapeThreatDistance = 0;
            Present(Escape(false, 0)); AssertHidden();
            Present(Escape(true, 100)); Assert.That(feedback.IsChased, Is.True);
        }

        [Test]
        public void EscapeToggleIsIndependentOfWaitingToggleAndClearsImmediately()
        {
            Assert.That(waiting.Pursuit.ScreenEffect, Is.True, "The existing waiting-room default stays enabled.");
            waiting.Pursuit.ScreenEffect = false;
            Present(Escape(true, 100));
            Assert.That(feedback.Intensity, Is.GreaterThan(0));
            world.escapeThreatEnabled = false;
            Present(Escape(true, 1), 0); AssertHidden();
            world.escapeThreatEnabled = true;
            Present(Escape(true, 1)); Assert.That(feedback.Intensity, Is.GreaterThan(0));
            Present(new PursuitThreatSample(observer, source, 10, true, true, 1), 0);
            AssertHidden(); // The waiting-room path still honors its own toggle.
        }

        [Test]
        public void MissingEncounterOrInvalidRoundClearsInsteadOfLeavingAFadingOldThreat()
        {
            Present(Escape(true, 2));
            Present(Escape(true, 2, ready: false), 0); AssertHidden();
            Present(Escape(true, 2));
            Present(Escape(true, 2, round: -1), 0); AssertHidden();
            Present(Escape(true, 2));
            Present(default, 0); AssertHidden();
        }

        [Test]
        public void SwitchingSpectatorOrRoundCannotCarryThePreviousPulse()
        {
            Present(Escape(true, 2));
            var teammate = NewObject("Spectated teammate");
            Present(Escape(false, 100, focus: teammate), 0); AssertHidden();
            Present(Escape(true, 2, focus: teammate));
            Present(Escape(false, 100, round: 20, focus: teammate), 0); AssertHidden();
        }

        [TestCase(PlayerState.Fallen)]
        [TestCase(PlayerState.Retired)]
        [TestCase(PlayerState.EscapeCaptured)]
        [TestCase(PlayerState.Arrived)]
        public void DeadCapturedOrFinishedSpectatorFocusIsRejected(PlayerState state)
        {
            var reader = observer.AddComponent<PlayerInputReader>();
            var teammate = NewObject("Spectated teammate").AddComponent<NetworkPlayer>();
            // Construct offline state without network writes or a transport/server.
            typeof(NetworkPlayer).GetField("state", Instance).SetValue(teammate, new NetworkVariable<PlayerState>(state));
            SetStaticProperty(typeof(CameraSpectator), "IsSpectating", true);
            SetStaticProperty(typeof(CameraSpectator), "Target", teammate);
            Present(Escape(true, 1));
            var focus = ScreenObserver(reader);
            Assert.That(focus, Is.Null);
            Present(PursuitThreatSample.Escape(focus, source, 10, true, true, 1, 18), 0);
            AssertHidden();
        }

        [Test]
        public void ScreenFocusUsesTheSpectatedPlayerAndNeverFallsBackWhenTargetIsMissing()
        {
            var reader = observer.AddComponent<PlayerInputReader>();
            var teammate = NewObject("Spectated teammate").AddComponent<NetworkPlayer>();
            SetStaticProperty(typeof(CameraSpectator), "IsSpectating", true);
            SetStaticProperty(typeof(CameraSpectator), "Target", teammate);
            Assert.That(ScreenObserver(reader), Is.EqualTo(teammate.gameObject));
            teammate.gameObject.SetActive(false);
            Assert.That(ScreenObserver(reader), Is.Null);
            SetStaticProperty(typeof(CameraSpectator), "Target", null);
            Assert.That(ScreenObserver(reader), Is.Null);
            SetStaticProperty(typeof(CameraSpectator), "IsSpectating", false);
            Assert.That(ScreenObserver(reader), Is.EqualTo(observer));
            var balance = observer.AddComponent<PlayerBalance>();
            typeof(PlayerBalance).GetProperty("HasFallen").GetSetMethod(true).Invoke(balance, new object[] { true });
            Assert.That(ScreenObserver(reader), Is.Null);
        }

        [Test]
        public void EdgeMeshKeepsTheCentreClearAndDisappearsOnCleanup()
        {
            Present(Escape(true, 0));
            var rect = Edge.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.sizeDelta = new Vector2(640, 360);
            using (var mesh = new VertexHelper())
            {
                Call(Edge, "OnPopulateMesh", mesh);
                Assert.That(mesh.currentVertCount, Is.GreaterThan(0));
                var vertex = new UIVertex();
                float inner = .5f - waiting.Pursuit.EdgeWidth;
                for (int i = 0; i < mesh.currentVertCount; i++)
                {
                    mesh.PopulateUIVertex(ref vertex, i);
                    if (vertex.color.a == 0) continue;
                    var point = (Vector2)vertex.position - rect.rect.center;
                    Assert.That(Mathf.Abs(point.x) >= rect.rect.width * inner - .01f
                        || Mathf.Abs(point.y) >= rect.rect.height * inner - .01f, Is.True, "Danger must not cover the centre.");
                }
            }
            var oldCanvas = Canvas.gameObject;
            Call(feedback, "OnDisable");
            Assert.That(oldCanvas == null, Is.True);
            Assert.That(feedback.Intensity, Is.Zero);
        }

        [Test]
        public void MultipleScenePresentersShareOneOverlayAndSurvivorTakesOver()
        {
            second = NewObject("Additive scene feedback").AddComponent<PursuitFeedback>();
            Call(second, "OnEnable");
            Assert.That(feedback.GetComponentsInChildren<PursuitEdgeGraphic>(true), Has.Length.EqualTo(1));
            Assert.That(second.GetComponentsInChildren<PursuitEdgeGraphic>(true), Is.Empty);
            Call(feedback, "OnDisable");
            Assert.That((bool)Call(second, "ClaimDisplay"), Is.True);
            Call(second, "OnEnable");
            Assert.That(second.GetComponentsInChildren<PursuitEdgeGraphic>(true), Has.Length.EqualTo(1));
            Call(feedback, "OnEnable");
            Assert.That(feedback.GetComponentsInChildren<PursuitEdgeGraphic>(true), Is.Empty);
        }

        static GameObject ScreenObserver(PlayerInputReader reader) => (GameObject)typeof(PursuitFeedback)
            .GetMethod("ScreenObserver", Static).Invoke(null, new object[] { reader });
        static object Call(object target, string method, params object[] args) => target.GetType()
            .GetMethod(method, Instance, null, System.Array.ConvertAll(args, arg => arg.GetType()), null).Invoke(target, args);
        static void SetStaticProperty(System.Type type, string name, object value) => type.GetProperty(name)
            .GetSetMethod(true).Invoke(null, new[] { value });
    }
}
