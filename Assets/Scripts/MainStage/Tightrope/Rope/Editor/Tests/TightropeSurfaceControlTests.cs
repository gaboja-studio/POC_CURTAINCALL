using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.EscapeRoute;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.EditorTools.Tests
{
    public sealed partial class TightropeSurfaceControlTests
    {
        readonly List<Object> objects = new();
        readonly List<EscapeDevelopmentSession> suppressedDevelopmentSessions = new();
        readonly Vector3 origin = new(9000f, 9000f, 9000f);
        TightropeCourse previousCourse, course;
        CourseControlSwitcher switcher;
        NetworkPlayer player;
        PlayerMover mover;
        PlayerBalance balance;
        PlayerController controller;
        PlayerControlScheme previousDefault;
        Scene testScene;

        [SetUp]
        public void SetUp()
        {
            // TestRunner의 저장되지 않은 씬을 유지하고 기존 개발 옵션만 잠시 보관한다.
            testScene = SceneManager.GetActiveScene();
            foreach (var session in Object.FindObjectsByType<EscapeDevelopmentSession>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (session.SuppressFallConsequences)
                {
                    suppressedDevelopmentSessions.Add(session);
                    session.SuppressFallConsequences = false;
                }
            previousCourse = (TightropeCourse)typeof(TightropeCourse)
                .GetField("current", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            previousDefault = ControlSettings.Current.DefaultScheme;
            ControlSettings.Current.DefaultScheme = Track(ScriptableObject.CreateInstance<PlatformControlScheme>());
            var courseObject = MakeObject("Surface test course");
            courseObject.transform.position = origin;
            switcher = courseObject.AddComponent<CourseControlSwitcher>();
            course = courseObject.GetComponent<TightropeCourse>();
            Invoke(switcher, "Awake");
            var playerObject = MakeObject("Surface test player");
            playerObject.transform.position = origin + new Vector3(0f, 0f, -1f);
            mover = playerObject.AddComponent<PlayerMover>();
            balance = playerObject.AddComponent<PlayerBalance>();
            controller = playerObject.AddComponent<PlayerController>();
            player = playerObject.AddComponent<NetworkPlayer>();
            Invoke(mover, "Awake");
            Invoke(balance, "Awake");
            Invoke(controller, "Awake");
            // EditMode에서는 런타임 생명주기 콜백이 자동 호출되지 않는다.
            Invoke(player, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            if (switcher != null) { Invoke(switcher, "OnDisable"); Invoke(switcher, "OnDestroy"); }
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] is GameObject gameObject && gameObject != null)
                {
                    if (gameObject.TryGetComponent(out EscapeDevelopmentSession session)) Invoke(session, "OnDisable");
                    Object.DestroyImmediate(gameObject);
                }
            ControlSettings.Current.DefaultScheme = previousDefault;
            typeof(TightropeCourse).GetField("current", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, previousCourse);
            foreach (var value in objects) if (value != null) Object.DestroyImmediate(value);
            objects.Clear();
            foreach (var session in suppressedDevelopmentSessions)
                if (session != null) session.SuppressFallConsequences = true;
            suppressedDevelopmentSessions.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void LocalSpawn_DisablesSharedPrefabDefaultBalanceBeforeFirstGroundedFrame()
        {
            Assert.That(balance.IsActive, Is.True, "공용 프리팹의 activeOnStart 기본값 재현");
            Invoke(switcher, "Bind", player);
            Assert.That(balance.IsActive, Is.False);
            Assert.That(controller.HasAssignedScheme, Is.False);
        }

        [Test]
        public void EnterActualRope_SnapsToCenterAndEnablesRopeControlsAndBalance()
        {
            int lane = Mathf.Min(1, course.LaneCount - 1);
            float side = course.Layout.GetLaneSide(lane);
            AddRope(lane, side);
            MovePlayer(new Vector3(side + 0.12f, 0f, 4f));
            Invoke(switcher, "Bind", player);
            Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(switcher.IsLocalOnRope, Is.True);
            Assert.That(controller.Scheme, Is.InstanceOf<TightropeControlScheme>());
            Assert.That(balance.IsActive, Is.True);
            Assert.That(mover.FreeMovement, Is.False);
            Assert.That(course.GetSide(player.transform.position), Is.EqualTo(side).Within(0.01f));
        }

        [Test]
        public void LeaveRopeForPlatform_DisablesBalanceAndRestoresSharedMovement()
        {
            AddBox("Platform", new Vector3(0f, -0.25f, -3f), new Vector3(20f, 0.5f, 6f));
            Invoke(switcher, "Bind", player);
            Invoke(switcher, "Apply", true);
            Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(switcher.IsLocalOnRope, Is.False);
            Assert.That(balance.IsActive, Is.False);
            Assert.That(controller.HasAssignedScheme, Is.False);
            Assert.That(mover.FreeMovement, Is.True);
            balance.SetBalanceActive(true); // 외부 재초기화가 기본 활성값을 다시 넣어도 안전해야 한다.
            Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(balance.IsActive, Is.False);
        }

        [Test]
        public void PiggybackDismount_RequiresGroundContactOnAnAssignedPlatform()
        {
            var platform = AddBox("Dismount platform", new Vector3(0f, -.25f, -3f), new Vector3(4f, .5f, 4f));
            var ordinary = AddBox("Unassigned platform", new Vector3(5f, -.25f, -3f), new Vector3(4f, .5f, 4f));
            var data = new UnityEditor.SerializedObject(course);
            data.FindProperty("useSceneGeometry").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            typeof(TightropeCourse).GetField("piggybackDismountPlatforms", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(course, new[] { platform });

            MovePlayer(new Vector3(0f, 0f, -3f));
            mover.Simulated = false; // 호스트가 보는 원격 플레이어는 소유자 이동을 실행하지 않는다.
            Assert.That(Invoke(course, "PiggybackDismountSupport", player), Is.SameAs(platform));
            mover.ResetExternalMotion(true);
            Assert.That(Invoke(course, "PiggybackDismountSupport", player), Is.Null);
            mover.ResetExternalMotion(false);
            MovePlayer(new Vector3(5f, 0f, -3f));
            Assert.That(Invoke(course, "PiggybackDismountSupport", player), Is.Null);
            Assert.That(Invoke(course, "ManualPiggybackDismountSupport", player), Is.SameAs(ordinary));
            MovePlayer(new Vector3(2.5f, 0f, -3f));
            Assert.That(Invoke(course, "PiggybackDismountSupport", player), Is.Null);
            Assert.That(Invoke(course, "ManualPiggybackDismountSupport", player), Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EscapePiggybackStopsBalanceAsSoonAsCarrierDropsOffRope(bool passenger)
        {
            AuthoredRopes();
            var deck = AddBox("Lower deck", new Vector3(0, -2.25f, 4), new Vector3(6, .5f, 6));
            var ordinary = AddBox("Unassigned landing", new Vector3(8, -2.25f, 4), new Vector3(6, .5f, 6));
            const BindingFlags member = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(TightropeCourse).GetField("piggybackDismountPlatforms", member).SetValue(course, new[] { deck });
            var current = typeof(PiggybackSystem).GetProperty("Current");
            var previous = current.GetValue(null);
            var stack = MakeObject("Piggyback fixture").AddComponent<PiggybackSystem>();
            var remoteObject = MakeObject("Second player");
            var remoteMover = remoteObject.AddComponent<PlayerMover>();
            var remote = remoteObject.AddComponent<NetworkPlayer>();
            typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(remote, 1UL);
            var all = (List<NetworkPlayer>)typeof(NetworkPlayer).GetField("all", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var carrier = passenger ? remote : player;
            var carrierMover = passenger ? remoteMover : mover;
            Invoke(remoteMover, "Awake");
            Invoke(remote, "Awake");
            ulong bottom = carrier.OwnerClientId, top = passenger ? player.OwnerClientId : remote.OwnerClientId;
            ((Dictionary<ulong, ulong>)typeof(PiggybackSystem).GetField("belowOf", member).GetValue(stack)).Add(top, bottom);
            ((Dictionary<ulong, ulong>)typeof(PiggybackSystem).GetField("aboveOf", member).GetValue(stack)).Add(bottom, top);
            all.Add(remote);
            bool registeredPlayer = !all.Contains(player);
            if (registeredPlayer) all.Add(player);
            try
            {
                current.SetValue(null, stack);
                Invoke(switcher, "Bind", player);
                carrier.transform.position = origin + new Vector3(8, -2, 4);
                Physics.SyncTransforms();
                Invoke(stack, "ServerResolvePlatformDismounts");
                Assert.That(stack.GetAbove(carrier), Is.Not.Null, "A stack formed on ordinary ground can keep moving.");
                carrier.transform.position = origin + new Vector3(0, 0, 4);
                Physics.SyncTransforms();
                Invoke(stack, "ServerResolvePlatformDismounts");
                Invoke(switcher, "UpdateSurfaceMode");
                Assert.That(balance.IsActive, Is.True);
                typeof(PlayerMover).GetProperty("CurrentJump").SetValue(carrierMover, JumpKind.InPlace);
                Invoke(switcher, "UpdateSurfaceMode");
                Assert.That(balance.IsActive, Is.True, "A rope jump still uses rope balance.");
                carrierMover.ResetExternalMotion(true);
                Invoke(switcher, "UpdateSurfaceMode");
                Assert.That(balance.IsActive, Is.False, "An actual drop disables balance before floor contact.");
                Assert.That(switcher.IsLocalOnRope, Is.False);
                Invoke(switcher, "UpdateSurfaceMode");
                Assert.That(balance.IsActive, Is.False, "A pending piggyback link cannot re-enable balance.");
                Invoke(stack, "ServerResolvePlatformDismounts");
                Assert.That(stack.GetAbove(carrier), Is.Not.Null, "Airborne stacks must stay linked.");
                carrierMover.ResetExternalMotion(false);
                carrier.transform.position = origin + new Vector3(0, -2, 4);
                Physics.SyncTransforms();
                Invoke(switcher, "UpdateSurfaceMode");
                Assert.That(balance.IsActive, Is.False);
                Assert.That(Invoke(course, "PiggybackDismountSupport", carrier), Is.SameAs(deck));
                carrier.transform.position = origin + new Vector3(8, -2, 4);
                Physics.SyncTransforms();
                Assert.That(Invoke(course, "PiggybackLandingGround", carrier), Is.SameAs(ordinary));
                Invoke(stack, "ServerResolvePlatformDismounts");
                Assert.That(stack.GetAbove(carrier), Is.Null, "A rope stack dismounts onto nearby unassigned ground.");
            }
            finally
            {
                all.Remove(remote);
                if (registeredPlayer) all.Remove(player);
                current.SetValue(null, previous);
            }
        }

        [Test]
        public void PiggybackDismount_ReservesThreeClearPositionsAwayFromPlatformEdge()
        {
            var platform = AddBox("Dismount platform", new Vector3(0f, -.25f, -3f), new Vector3(6f, .5f, 4f));
            AddBox("Obstruction on left", new Vector3(-1.75f, 1f, -3f), new Vector3(2.5f, 2f, 4f));
            var carrierFeet = origin + new Vector3(2.9f, 0f, -4.9f);
            var positions = new Dictionary<ulong, Vector3>();
            var find = typeof(PiggybackSystem).GetMethod("TryFindPlatformSeat", BindingFlags.Static | BindingFlags.NonPublic);
            for (ulong rider = 1; rider <= 3; rider++)
            {
                object[] args = { platform, carrierFeet, .3f, 1.5f, positions, Vector3.zero };
                Assert.That(find.Invoke(null, args), Is.True);
                var feet = (Vector3)args[5];
                Assert.That(feet.x - origin.x, Is.InRange(-.19f, 2.49f));
                Assert.That(feet.z - origin.z, Is.InRange(-4.49f, -1.51f));
                foreach (var used in positions.Values) Assert.That(Vector3.Distance(feet, used), Is.GreaterThan(.7f));
                positions.Add(rider, feet);
            }
            AddBox("Remaining deck blocked", new Vector3(1.5f, 1f, -3f), new Vector3(3f, 2f, 4f));
            Assert.That(find.Invoke(null, new object[] { platform, carrierFeet, .3f, 1.5f, positions, Vector3.zero }), Is.False);
        }

        [Test]
        public void OrdinaryFloorInsideRopeCoordinates_IsNotARopeSurface()
        {
            int lane = Mathf.Min(1, course.LaneCount - 1);
            float side = course.Layout.GetLaneSide(lane);
            AddRope(lane, side);
            AddBox("Raised floor", new Vector3(side, 0f, 4f), new Vector3(2f, 0.2f, 2f));
            MovePlayer(new Vector3(side, 0.1f, 4f));
            Assert.That(course.IsOnRope(player.transform.position, mover.BodyRadius, out _), Is.True,
                "이전 높이·좌표 검사로는 다른 바닥을 줄로 오인한다");
            Assert.That(course.TryGetSupportingRope(mover, out _), Is.False);
        }

        [Test]
        public void FloorAtExactlyRopeHeight_TakesPrecedenceOverOverlappingRope()
        {
            int lane = Mathf.Min(1, course.LaneCount - 1);
            float side = course.Layout.GetLaneSide(lane);
            AddRope(lane, side);
            AddBox("Coincident floor", new Vector3(side, -0.1f, 4f), new Vector3(2f, 0.2f, 2f));
            MovePlayer(new Vector3(side, 0f, 4f));
            Assert.That(course.TryGetSupportingRope(mover, out _), Is.False);
        }

        [Test]
        public void LoosePieceOverlappingCastOriginDoesNotHideTheSupportingRope()
        {
            int lane = Mathf.Min(1, course.LaneCount - 1);
            float side = course.Layout.GetLaneSide(lane);
            AddRope(lane, side);
            // 떨어져 나간 조각이 발 위 탐색구와 겹쳐도 발밑 줄은 그대로 있다.
            AddBox("Loose limb beside feet", new Vector3(side + .25f, .4f, 4f), new Vector3(.2f, .2f, .2f));
            MovePlayer(new Vector3(side, 0f, 4f));
            Assert.That(course.TryGetSupportingRope(mover, out int supported), Is.True);
            Assert.That(supported, Is.EqualTo(lane));
        }

        [Test]
        public void AirborneOrCarriedPlayer_PreservesExistingRopeBalance()
        {
            Invoke(switcher, "Bind", player);
            Invoke(switcher, "Apply", true);
            mover.Simulated = false;
            MovePlayer(new Vector3(0f, 1.5f, 4f));
            Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(switcher.IsLocalOnRope, Is.True);
            Assert.That(balance.IsActive, Is.True);
        }

        [Test]
        public void AnotherContentAssignedControls_AreNotReplacedOrBalancedOff()
        {
            var external = Track(ScriptableObject.CreateInstance<PlatformControlScheme>());
            controller.SetScheme(external);
            Invoke(switcher, "Bind", player);
            Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(controller.Scheme, Is.SameAs(external));
            Assert.That(balance.IsActive, Is.True);
        }

        [TestCase(CampaignPhase.None, false, false, true)]
        [TestCase(CampaignPhase.Intro, false, false, true)]
        [TestCase(CampaignPhase.Performance, true, true, true)]
        [TestCase(CampaignPhase.Performance, true, false, false)]
        [TestCase(CampaignPhase.Performance, false, true, false)]
        [TestCase(CampaignPhase.RestTime, true, true, false)]
        [TestCase(CampaignPhase.Night, true, true, false)]
        [TestCase(CampaignPhase.Escape, true, true, false)]
        public void TraversalScope_SeparatesPracticeFromOtherCampaignPhases(
            CampaignPhase phase, bool tightrope, bool open, bool expected) =>
            Assert.That(TightropeCourse.AllowsTraversal(phase, tightrope, open), Is.EqualTo(expected));

        void AddRope(int lane, float side)
        {
            var rope = AddBox("Rope", new Vector3(side, -0.05f, 4f), new Vector3(0.4f, 0.1f, 8f));
            var surfaces = (Dictionary<Collider, int>)typeof(TightropeCourse)
                .GetField("ropeSurfaces", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(course);
            surfaces.Add(rope, lane);
        }

        BoxCollider[] AuthoredRopes()
        {
            var first = AddBox("Landing rope", new Vector3(0f, -.1f, 4f), new Vector3(.4f, .2f, 8f));
            var turn = AddBox("Turn rope", new Vector3(4f, -.1f, 8f), new Vector3(.4f, .2f, 8f));
            turn.transform.rotation = Quaternion.Euler(0, 90, 0);
            var data = new UnityEditor.SerializedObject(course);
            data.FindProperty("useSceneGeometry").boolValue = true;
            data.FindProperty("independentSceneRopes").boolValue = true;
            var ropes = data.FindProperty("sceneRopes"); ropes.arraySize = 2;
            ropes.GetArrayElementAtIndex(0).objectReferenceValue = first;
            ropes.GetArrayElementAtIndex(1).objectReferenceValue = turn;
            data.FindProperty("sceneShape").FindPropertyRelative("laneCount").intValue = 2;
            data.ApplyModifiedPropertiesWithoutUndo();
            course.Rebuild();
            typeof(TightropeCourse).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, course);
            Physics.SyncTransforms();
            return new[] { first, turn };
        }

        bool SafeLanding(Vector3 feet) => (bool)typeof(HumanCannonSystem)
            .GetMethod("IsSafeLandingSurface", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { mover, feet });

        [Test]
        public void CannonLandingOnRopeEdge_UsesBodySupportInsteadOfFootCenter()
        {
            var rope = AuthoredRopes()[0];
            var zone = rope.gameObject.AddComponent<HumanCannonLandingZone>();
            MovePlayer(new Vector3(.24f, 0f, 4f));
            Assert.That(zone.SupportsLanding(player.transform.position), Is.False, "기존 발 중심 검사의 오판 재현");
            Assert.That(course.TryGetSupportingRope(mover, out _), Is.True);
            Assert.That(SafeLanding(player.transform.position), Is.True, "실제 지지줄 착지에는 추락 부상이 없어야 한다");
            // 원격 몸 위치가 아직 이전 좌표여도 요청된 착지 좌표에서 같은 표면을 검사한다.
            Vector3 reportedFeet = player.transform.position;
            MovePlayer(new Vector3(2f, 1f, 4f));
            Assert.That(SafeLanding(reportedFeet), Is.True);
        }

        [Test]
        public void CannonLandingOnOuterCapsuleEdge_IsNotRejectedByASmallerSupportProbe()
        {
            AuthoredRopes();
            float radius = mover.BodyRadius;
            float sideBeyondRope = radius * .95f;
            float feetHeight = Mathf.Sqrt(radius * radius - sideBeyondRope * sideBeyondRope) - radius;
            MovePlayer(new Vector3(.2f + sideBeyondRope, feetHeight, 4));
            Assert.That(course.TryGetSupportingRope(mover, out int lane), Is.True,
                "실제 캡슐 하단이 줄 위 모서리에 닿지만 90% 탐색구는 줄에 닿지 않는다.");
            Assert.That(lane, Is.EqualTo(0));
            Assert.That(SafeLanding(player.transform.position), Is.True);
            MovePlayer(new Vector3(.2f + radius * 1.1f, feetHeight, 4));
            Assert.That(SafeLanding(player.transform.position), Is.False, "실제 캡슐 밖 허공은 안전 착지가 아니다.");
        }

        [Test]
        public void NearbyInactiveLineAtAnotherHeight_DoesNotHideActualSupportingRope()
        {
            var ropes = AuthoredRopes();
            float radius = mover.BodyRadius;
            float sideBeyondRope = radius * .95f;
            float feetHeight = Mathf.Sqrt(radius * radius - sideBeyondRope * sideBeyondRope) - radius;
            float side = .2f + sideBeyondRope;
            ropes[1].transform.SetPositionAndRotation(origin + new Vector3(side, feetHeight + .25f, 4), Quaternion.identity);
            ropes[1].enabled = false;
            MovePlayer(new Vector3(side, feetHeight, 4));
            Assert.That(Mathf.Abs(course.RopeHeightAt(player.transform.position) - player.transform.position.y),
                Is.GreaterThan(course.Shape.RopeHeightTolerance), "최근접 중심선은 발밑 지지면의 높이가 아니다.");
            Assert.That(course.TryGetSupportingRope(mover, out int lane), Is.True);
            Assert.That(lane, Is.EqualTo(0));
            Assert.That(SafeLanding(player.transform.position), Is.True);
        }

        [Test]
        public void ExternalCannonMotion_IsNotKilledByRopeDepthDuringControlHandoff()
        {
            AuthoredRopes();
            MovePlayer(new Vector3(0, -course.Shape.FallDepth - 1, 4));
            Assert.That(Invoke(course, "IsBelowFallDepth", mover), Is.True, "실제 외줄 이탈 추락은 유지한다.");
            mover.Simulated = false;
            Assert.That(Invoke(course, "IsBelowFallDepth", mover), Is.False,
                "대포·그네가 이동을 맡는 프레임에는 외줄의 1m 바닥선을 적용하지 않는다.");
        }

        [TestCase(true, 0f)]
        [TestCase(true, .5f)]
        [TestCase(false, 0f)]
        public void RunningCourseNeverKillsFromRopeHeightAlone(bool authored, float aboveFloor)
        {
            AuthoredRopes();
            var data = new UnityEditor.SerializedObject(course);
            data.FindProperty("useSceneGeometry").boolValue = authored;
            data.ApplyModifiedPropertiesWithoutUndo();
            // A의 두 번째 대포대처럼 가까운 외줄보다 6m 낮은 정상 바닥과 짧은 낙하.
            AddBox("Lower cannon preparation deck", new Vector3(0, -6.4f, 4), new Vector3(8, .8f, 9));
            MovePlayer(new Vector3(0, -6 + aboveFloor, 4));
            balance.SetBalanceActive(false);
            var run = course.gameObject.AddComponent<TightropeRun>();
            typeof(TightropeRun).GetProperty("State").SetValue(run, TightropeRunState.Running);
            var local = typeof(NetworkPlayer).GetProperty("Local");
            var previous = local.GetValue(null);
            try
            {
                local.SetValue(null, player);
                Assert.That(course.UsesRopeRules(player), Is.False);
                Assert.That(Invoke(course, "IsBelowFallDepth", mover), Is.EqualTo(aboveFloor > 0f),
                    "현재 공통 규칙은 실제 발판에 지지된 사람을 자연 추락 대상으로 보지 않는다.");
                Invoke(course, "CheckLocalFall");
                Assert.That(balance.HasFallen, Is.False, "외줄 이탈은 일반 낙하이며 실제 착지 높이가 피해를 결정한다.");
            }
            finally { local.SetValue(null, previous); }
        }

        [Test]
        public void FallSuppression_IsOptInAndLimitedToItsEnabledScene()
        {
            var session = AddDevelopmentSession();
            Assert.That(EscapeDevelopmentSession.FallConsequencesSuppressed(balance), Is.False);
            session.SuppressFallConsequences = true;
            Assert.That(EscapeDevelopmentSession.FallConsequencesSuppressed(balance), Is.True);
            session.enabled = false;
            Assert.That(EscapeDevelopmentSession.FallConsequencesSuppressed(balance), Is.False);
            session.enabled = true;
            Invoke(session, "OnEnable");
            var otherScene = EditorSceneManager.NewPreviewScene();
            try
            {
                SceneManager.MoveGameObjectToScene(session.gameObject, otherScene);
                Assert.That(EscapeDevelopmentSession.FallConsequencesSuppressed(balance), Is.False,
                    "함께 열린 다른 원본 인간대포·외줄 씬까지 옵션이 전파되면 안 된다.");
            }
            finally
            {
                Invoke(session, "OnDisable");
                EditorSceneManager.ClosePreviewScene(otherScene);
            }
        }

        [Test]
        public void FallSuppression_BlocksBalanceFallBeforeHangAndClearsTheFailedGauge()
        {
            AddDevelopmentSession().SuppressFallConsequences = true;
            bool hangRequested = false, fell = false;
            var previousCatcher = PlayerBalance.GaugeFallCatcher;
            balance.Fell += () => fell = true;
            PlayerBalance.GaugeFallCatcher = _ => { hangRequested = true; return false; };
            try
            {
                balance.ForceFall();
                balance.SetDisplayedState(true, PlayerBalance.MaxValue);
                Invoke(balance, "UpdateRedTime", balance.FallTime);
                Assert.That(balance.HasFallen, Is.False);
                Assert.That(fell, Is.False);
                Assert.That(hangRequested, Is.False, "죽지 않는 개발 모드에서 구조 대기로 입력을 묶지 않는다.");
                Assert.That(balance.RedTime, Is.Zero);
                Assert.That(balance.Value, Is.Zero);
            }
            finally { PlayerBalance.GaugeFallCatcher = previousCatcher; }
        }

        [Test]
        public void FallSuppression_DoesNotSwallowConfirmedHazardDeath()
        {
            AddDevelopmentSession().SuppressFallConsequences = true;
            // 톱·화염 등이 호스트에서 이미 확정한 상태를 재현한다. NGO를 시작하지 않고 초기값만 주입한다.
            typeof(NetworkPlayer).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(player, new Unity.Netcode.NetworkVariable<PlayerState>(PlayerState.Fallen));
            balance.ForceFall();
            Assert.That(balance.HasFallen, Is.True, "확정된 위험물 사망의 래그돌 전환은 유지한다.");
        }

        [TestCase(1, true)]
        [TestCase(-1, true)]
        [TestCase(1, false)]
        [TestCase(-1, false)]
        public void EscapeRopeBalance_ResolvesFullGaugeInEitherDirection(int direction, bool hasGrip)
        {
            AuthoredRopes();
            var session = AddDevelopmentSession();
            session.SuppressFallConsequences = session.AllowRopeBalanceFalls = true;
            MovePlayer(new Vector3(0, 0, 4));
            player.transform.forward = Vector3.forward * direction;
            Invoke(switcher, "Bind", player);
            Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(switcher.IsLocalOnRope, Is.True);
            Assert.That(Vector3.Dot(mover.CourseForward, player.transform.forward), Is.GreaterThan(.99f));

            var previousCatcher = PlayerBalance.GaugeFallCatcher;
            int catches = 0, falls = 0;
            balance.Fell += () => falls++;
            PlayerBalance.GaugeFallCatcher = _ => { catches++; return hasGrip; };
            try
            {
                balance.ForceFall();
                Assert.That(falls, Is.Zero, "The scene still protects ordinary falls and cannon landings.");
                balance.SetDisplayedState(true, PlayerBalance.MaxValue * direction);
                Invoke(balance, "UpdateRedTime", balance.FallTime);
                Assert.That(catches, Is.EqualTo(1));
                Assert.That(balance.HasFallen, Is.EqualTo(!hasGrip));
                balance.DropFromRopeGrip();
                Assert.That(falls, Is.EqualTo(1), "A missing or expired grip resolves one fall.");
            }
            finally { PlayerBalance.GaugeFallCatcher = previousCatcher; }
        }

        [Test]
        public void FallSuppression_DisablesRopeDepthAndRestoresItWhenTurnedOff()
        {
            AuthoredRopes();
            MovePlayer(new Vector3(0, -course.Shape.FallDepth - 1, 4));
            var session = AddDevelopmentSession();
            session.SuppressFallConsequences = true;
            Assert.That(Invoke(course, "IsBelowFallDepth", mover), Is.False);
            session.SuppressFallConsequences = false;
            Assert.That(Invoke(course, "IsBelowFallDepth", mover), Is.True);
        }

        EscapeDevelopmentSession AddDevelopmentSession()
        {
            var session = MakeObject("Development fall rules").AddComponent<EscapeDevelopmentSession>();
            Invoke(session, "OnEnable");
            return session;
        }

        [TestCase(.8f, 0f)]
        [TestCase(0f, 1f)]
        [TestCase(0f, -1f)]
        public void CannonLandingNearRopeWithoutSupport_IsNotSafe(float side, float height)
        {
            AuthoredRopes();
            MovePlayer(new Vector3(side, height, 4f));
            Assert.That(SafeLanding(player.transform.position), Is.False);
        }

        [Test]
        public void DisabledAndTriggerRopesRejectLandingButOrdinaryWalkableFloorAcceptsIt()
        {
            var rope = AuthoredRopes()[0];
            MovePlayer(new Vector3(0, 0, 4));
            Assert.That(SafeLanding(player.transform.position), Is.True);
            rope.enabled = false; Physics.SyncTransforms();
            Assert.That(SafeLanding(player.transform.position), Is.False);
            rope.enabled = true; rope.isTrigger = true; Physics.SyncTransforms();
            Assert.That(SafeLanding(player.transform.position), Is.False);
            rope.isTrigger = false;
            AddBox("Actual ordinary floor", new Vector3(0, 0, 4), new Vector3(2, .2f, 2));
            MovePlayer(new Vector3(0, .1f, 4));
            Assert.That(SafeLanding(player.transform.position), Is.True);
        }

        [Test]
        public void ConnectedRopeTurn_UpdatesAxisWithoutResettingControlsAndRestoresPreviousAxis()
        {
            AuthoredRopes();
            mover.CourseForwardOverride = Vector3.left;
            Invoke(switcher, "Bind", player);
            MovePlayer(new Vector3(0, 0, 4));
            Invoke(switcher, "UpdateSurfaceMode");
            var ropeScheme = controller.Scheme;
            Assert.That(Vector3.Dot(mover.CourseForward, Vector3.forward), Is.GreaterThan(.999f));
            MovePlayer(new Vector3(4, 0, 8));
            Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(controller.Scheme, Is.SameAs(ropeScheme));
            Assert.That(balance.IsActive, Is.True);
            Assert.That(Vector3.Dot(mover.CourseForward, Vector3.right), Is.GreaterThan(.999f));
            Assert.That(Vector3.Distance(player.transform.position, origin + new Vector3(4, 0, 8)), Is.LessThan(.02f));
            Invoke(switcher, "ReleaseControls");
            Assert.That(mover.CourseForward, Is.EqualTo(Vector3.left));
        }

        [TestCase(1, false)] [TestCase(-1, false)]
        [TestCase(1, true)] [TestCase(-1, true)]
        public void AuthoredRopeEntryPreservesFacingAndDoesNotFlipOnTheFollowingFrame(int direction, bool legacyOption)
        {
            AuthoredRopes();
            var data = new UnityEditor.SerializedObject(course);
            Assert.IsNull(data.FindProperty("bidirectionalSceneTraversal"));
            data.ApplyModifiedPropertiesWithoutUndo();
            Invoke(switcher, "Bind", player);
            MovePlayer(new Vector3(0, 0, 4));
            player.transform.rotation = Quaternion.LookRotation(Vector3.forward * direction);
            Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(Vector3.Dot(mover.CourseForward, Vector3.forward * direction), Is.GreaterThan(.999f));
            Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(Vector3.Dot(mover.CourseForward, Vector3.forward * direction), Is.GreaterThan(.999f));
            Assert.That(course.AuthoredLaneDirection(player.transform.position, mover.CourseForward, 1), Is.EqualTo(direction));
            Assert.That(course.AuthoredLaneDirection(player.transform.position, mover.CourseForward, -1), Is.EqualTo(-direction));
        }

        [Test]
        public void ReverseTraversalSideJumpUsesThePlayersRightRatherThanTheAuthoredRight()
        {
            AuthoredRopes();
            var left = AddBox("Reverse right landing", new Vector3(-2, -.1f, 4), new Vector3(.4f, .2f, 8));
            var data = new UnityEditor.SerializedObject(course);
            Assert.IsNull(data.FindProperty("bidirectionalSceneTraversal"));
            data.FindProperty("sceneShape.laneCount").intValue = 3;
            data.FindProperty("sceneShape.laneSpacing").floatValue = 2;
            var ropes = data.FindProperty("sceneRopes"); ropes.arraySize = 3;
            ropes.GetArrayElementAtIndex(2).objectReferenceValue = left;
            data.ApplyModifiedPropertiesWithoutUndo(); course.Rebuild();
            MovePlayer(new Vector3(0, 0, 4));
            mover.CourseForwardOverride = Vector3.back;
            mover.LaneSpacing = 2;
            var rules = player.gameObject.AddComponent<RopeLaneJumpRules>();
            typeof(RopeLaneJumpRules).GetField("mover", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rules, mover);
            Assert.That(rules.CanLaneJump(1), Is.True, "Player right is world left while facing the opposite rope end.");
            Assert.That(rules.CanLaneJump(-1), Is.False, "No parallel rope exists on the player's left.");
        }

        [TestCase(1)] [TestCase(-1)]
        public void GeneratedStuntRopePreservesEntryDirectionAndRestoresPlatformAxis(int direction)
        {
            int lane = Mathf.Min(1, course.LaneCount - 1);
            float side = course.Layout.GetLaneSide(lane);
            AddRope(lane, side);
            mover.CourseForwardOverride = Vector3.right;
            MovePlayer(new Vector3(side, 0, 4));
            player.transform.rotation = Quaternion.LookRotation(Vector3.forward * direction);
            Invoke(switcher, "Bind", player); Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(mover.CourseForward, Is.EqualTo(Vector3.forward * direction));
            Invoke(switcher, "UpdateSurfaceMode");
            Assert.That(mover.CourseForward, Is.EqualTo(Vector3.forward * direction));
            Assert.That(course.AuthoredLaneDirection(player.transform.position, mover.CourseForward, 1), Is.EqualTo(direction));
            Invoke(switcher, "ReleaseControls");
            Assert.That(mover.CourseForward, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void GeneratedReverseLaneJumpLandsBehindBlockerInThePlayersTraversalAxis()
        {
            typeof(TightropeCourse).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, course);
            int lane = 1; float side = course.Layout.GetLaneSide(lane);
            AddRope(lane, side); AddRope(0, course.Layout.GetLaneSide(0));
            MovePlayer(new Vector3(side, 0, 4)); mover.CourseForwardOverride = Vector3.back;
            var other = MakeObject("Reverse landing blocker").AddComponent<PlayerMover>();
            other.transform.position = origin + new Vector3(course.Layout.GetLaneSide(0), 0, 4);
            Invoke(other, "Awake"); Invoke(other, "OnEnable");
            try
            {
                var rules = player.gameObject.AddComponent<RopeLaneJumpRules>();
                typeof(RopeLaneJumpRules).GetField("mover", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rules, mover);
                Assert.That(rules.CanLaneJump(1), Is.False, "3m 경계에서 동료를 피한 착지점이 범위를 초과하면 뛰지 않는다.");
                other.transform.position += Vector3.forward * 2;
                Assert.That(rules.CanLaneJump(1), Is.True);
            }
            finally { Invoke(other, "OnDisable"); }
        }

        [Test]
        public void EscapeACannonLandingGroupsAllowSideJumpsAlongTheirFullLengthInBothDirections()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Levels/escape_route.unity");
            try
            {
                var authored = scene.GetRootGameObjects()[0].GetComponentInChildren<TightropeCourse>(true);
                if (authored == null)
                    foreach (var root in scene.GetRootGameObjects())
                        if ((authored = root.GetComponentInChildren<TightropeCourse>(true)) != null) break;
                Assert.That(authored, Is.Not.Null);
                typeof(TightropeCourse).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, authored);
                var rules = player.gameObject.AddComponent<RopeLaneJumpRules>();
                typeof(RopeLaneJumpRules).GetField("mover", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(rules, mover);
                mover.LaneSpacing = authored.LaneSpacing;
                mover.LaneLandingTarget = direction => (Vector3?)Invoke(rules, "GetLandingTarget", direction);
                foreach (var lanes in new[] { new[] { 11, 7, 13 }, new[] { 14, 8, 12 } })
                for (int index = 0; index < lanes.Length; index++)
                foreach (float fraction in new[] { .15f, .5f, .85f })
                foreach (int facing in new[] { -1, 1 })
                foreach (float speed in new[] { -.5f, 0f, .5f })
                {
                    int lane = lanes[index];
                    Vector3 start = authored.GetLanePosition(lane, 0);
                    Vector3 forward = (authored.GetLanePosition(lane, 1) - start).normalized;
                    mover.transform.position = authored.GetLanePosition(lane, authored.Layout.GetLaneEnd(lane) * fraction);
                    mover.CourseForwardOverride = forward * facing;
                    typeof(PlayerMover).GetField("groundVelocity", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(mover, mover.CourseForward * speed);
                    foreach (int direction in new[] { -1, 1 })
                    {
                        int neighbor = index + direction * facing;
                        bool available = neighbor >= 0 && neighbor < lanes.Length;
                        Assert.That(rules.CanLaneJump(direction), Is.EqualTo(available),
                            $"lane={lane}, fraction={fraction}, facing={facing}, speed={speed}, side={direction}");
                        if (!available) continue;
                        Assert.That(rules.TryGetLandingDistance(direction, out float distance), Is.True);
                        Vector3 landing = mover.GetLaneLandingPosition(direction);
                        Assert.That(Vector3.Distance(landing, authored.GetLanePosition(lanes[neighbor], distance)), Is.LessThan(.02f),
                            "허용된 점프의 실제 도착점도 옆줄 중앙에 있어야 한다.");
                    }
                }
            }
            finally
            {
                typeof(TightropeCourse).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, course);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        BoxCollider AddBox(string name, Vector3 center, Vector3 size)
        {
            var go = MakeObject(name);
            go.transform.position = origin + center;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            Physics.SyncTransforms();
            return box;
        }

        void MovePlayer(Vector3 position)
        {
            mover.Body.enabled = false;
            player.transform.position = origin + position;
            mover.Body.enabled = true;
            Physics.SyncTransforms();
        }

        GameObject MakeObject(string name)
        {
            var value = Track(new GameObject(name));
            if (value.scene != testScene) SceneManager.MoveGameObjectToScene(value, testScene);
            return value;
        }

        T Track<T>(T value) where T : Object { value.hideFlags = HideFlags.DontSave; objects.Add(value); return value; }
        static object Invoke(object target, string method, params object[] args) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
