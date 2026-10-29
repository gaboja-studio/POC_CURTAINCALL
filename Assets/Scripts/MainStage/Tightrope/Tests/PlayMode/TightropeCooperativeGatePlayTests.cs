using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest]
        public IEnumerator CooperativeGateEnlargedOriginalDiscStopsSoloJumpWithoutAnInvisibleWall()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var settings = Get(saws, "Settings");
            bool enabledBefore = (bool)Get(settings, "cooperativeGates");
            var distancesBefore = (float[])Get(settings, "cooperativeGateDistances");
            float approachBefore = (float)Get(settings, "cooperativeGateApproachDistance");
            try
            {
                ConfigureStationaryGateForTest(saws, settings, 6f);
                yield return StandOnRopeForSawTest(1, 4f);
                yield return null;
                Assert.IsTrue((bool)Get(saws, "IsCooperativeGateStopped"));
                var view = saws.transform.Find("Generated/CooperativeGate");
                var collider = view.GetComponentInChildren<MeshCollider>();
                Assert.IsTrue(collider.enabled);
                Assert.IsNull(view.Find("ContinuousSawFrame"));
                var prepared = Get(saws, "cooperativeGatePrefab") as GameObject;
                if (prepared == null) Assert.IsNotNull(view.Find("IncomingSaw/Tooth0"));
                else Assert.AreSame(prepared.GetComponentInChildren<MeshFilter>().sharedMesh,
                    view.Find("IncomingSaw").GetComponentInChildren<MeshFilter>().sharedMesh,
                    "준비한 원판 에셋을 재사용한다.");
                Assert.That(collider.bounds.size.y, Is.EqualTo(2.1f).Within(.01f));
                Assert.That(collider.bounds.size.z, Is.EqualTo(2.1f).Within(.01f));
                Assert.That(collider.bounds.size.x, Is.EqualTo(.2f).Within(.01f));
                var adjacent = (Vector3)Call(course, "GetLanePosition", 2, 6f) + Vector3.up;
                Assert.That(Vector3.Distance(collider.ClosestPoint(adjacent), adjacent), Is.GreaterThan(2f), "다른 줄까지 막는 보이지 않는 벽이 없다.");
                Assert.That(Mathf.Abs(Vector3.Dot(view.Find("IncomingSaw").up, (Vector3)Get(course, "Right"))), Is.GreaterThan(.999f), "기존 정면 톱과 같은 오른쪽 회전축");
                object[] deniedLaunch = { player, 0f, 0f };
                Assert.IsFalse((bool)Call(saws, "TryGetCooperativeLaunch", deniedLaunch), "같은 출발선의 혼자 선 플레이어는 보정을 받지 않는다.");
                Call(mover, "LaunchJump", 3f);
                yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "일반 높이로 관문을 향한 점프");
                float peak = 0f;
                float deadline = Time.realtimeSinceStartup + 3f;
                do
                {
                    peak = Mathf.Max(peak, player.transform.position.y - course.transform.position.y);
                    Assert.That((float)Call(course, "GetDistance", player.transform.position), Is.LessThan(6f), "피격을 감수해도 닫힌 관문을 몸으로 통과할 수 없다.");
                    yield return null;
                } while ((bool)Get(mover, "IsAirborne") && Time.realtimeSinceStartup < deadline);
                float commonJump = (float)Get(Get(Static("CurtainCall.Settings.GameSettings", "Base"), "Character"), "JumpHeight");
                Assert.That(peak, Is.GreaterThan(commonJump - .1f));
                for (int i = 0; i < 20; i++) { Call(mover, "SetMoveInput", 1f); yield return null; }
                Assert.That((float)Call(course, "GetDistance", player.transform.position), Is.LessThan(6f));
                Assert.IsTrue((bool)Get(saws, "IsCooperativeGateActive"));
                Assert.IsFalse((bool)Get(saws, "IsCooperativeGatePassable"));
            }
            finally
            {
                Call(saws, "ResetCooperativeGates");
                Set(settings, "cooperativeGates", enabledBefore);
                Set(settings, "cooperativeGateDistances", distancesBefore);
                Set(settings, "cooperativeGateApproachDistance", approachBefore);
            }
        }

        [UnityTest]
        public IEnumerator CooperativeGateTwoHighForwardJumpLandsOnPlateAndHopsTheDiscOverTeammate() => CooperativeGateJumpScenario(false);

        [UnityTest]
        public IEnumerator CooperativeGateInjuredTwoHighForwardJumpStillOpensRearPlate() => CooperativeGateJumpScenario(true);

        IEnumerator CooperativeGateJumpScenario(bool injured)
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var settings = Get(saws, "Settings");
            bool enabledBefore = (bool)Get(settings, "cooperativeGates");
            var distancesBefore = (float[])Get(settings, "cooperativeGateDistances");
            float approachBefore = (float)Get(settings, "cooperativeGateApproachDistance");
            Component helperObject = null;
            try
            {
                ConfigureStationaryGateForTest(saws, settings, 6f);
                float launch = (float)Get(saws, "CooperativeJumpLaunchDistance");
                var gateView = saws.transform.Find("Generated/CooperativeGate");
                yield return StandOnRopeForSawTest(1, launch - .7f);
                float closedGateHeight = gateView.position.y;
                var prefab = (GameObject)Get(Get(network, "NetworkConfig"), "PlayerPrefab");
                var helperInstance = Object.Instantiate(prefab);
                helperObject = helperInstance.GetComponent(GameType("Unity.Netcode.NetworkObject"));
                Call(helperObject, "SpawnWithOwnership", 777UL, true);
                var helper = helperInstance.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                var helperMover = helperInstance.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
                var basePosition = (Vector3)Call(course, "GetLanePosition", 1, launch);
                Quaternion rotation = Quaternion.LookRotation((Vector3)Get(course, "Forward"));
                Call(helperMover, "SetStartPose", basePosition, rotation, true);
                var replica = helperInstance.GetComponent(GameType("Unity.Netcode.Components.NetworkTransform"));
                Call(replica, "SetStateInternal", basePosition, rotation, helper.transform.localScale, true);
                Set(replica, "InboundState", Get(replica, "OutboundState"));
                Call(replica, "TransformStateUpdate");
                Physics.SyncTransforms();
                // 위치/부상을 검사하는 배치 완료 대역. 실제 복귀 통신은 Multiplayer 회귀 검사에서 다룬다.
                ((IDictionary)Get(run, "placedPlayers"))[helper] = (int)Get(helper, "RestartVersion");
                yield return null;
                Assert.That(Vector3.Distance(helper.transform.position, basePosition), Is.LessThan(.01f));
                if (injured)
                {
                    Call(helper, "ServerCut", 2);
                    Call(player, "ServerCut", 2);
                    yield return Until(() => (float)Get(helperMover, "BodyHeight") < 1f && (float)Get(mover, "JumpHeightMultiplier") < 1f,
                        2f, "두 플레이어의 실제 양다리 부상 적용");
                }
                int initialParts = Convert.ToInt32(Get(player, "LostParts"));
                var stack = (Component)Static("CurtainCall.Tightrope.PiggybackSystem", "Current");
                Call(player.GetComponent(GameType("CurtainCall.Player.PlayerInteraction")), "RequestInteract");
                yield return Until(() => Call(stack, "GetBelow", player) == helper, 2f, "실제 목마 연결");
                yield return Until(() => player.transform.position.y - helper.transform.position.y > (injured ? .4f : 1.4f), 2f, "2층 발 높이 배치");
                object[] assistedLaunch = { player, 0f, 0f };
                object[] support = { helperMover, 0 };
                bool supported = (bool)Call(course, "TryGetSupportingRope", support);
                string eligibility = $"top={player.transform.position}, bottom={helper.transform.position}, "
                    + $"topNetAir={Get(player, "IsAirborne")}, topMoveAir={Get(mover, "IsAirborne")}, free={Get(mover, "FreeMovement")}, "
                    + $"jumpMultiplier={Get(mover, "JumpHeightMultiplier")}, bottomNetAir={Get(helper, "IsAirborne")}, "
                    + $"bottomMoveAir={Get(helperMover, "IsAirborne")}, support={supported}/{support[1]}, "
                    + $"topState={Get(player, "State")}, bottomState={Get(helper, "State")}, stopped={Get(saws, "IsCooperativeGateStopped")}, launch={launch}";
                if (!supported)
                {
                    float radius = (float)Get(helperMover, "BodyRadius") * .9f;
                    foreach (var hit in Physics.SphereCastAll(helper.transform.position + Vector3.up * (radius + .1f), radius,
                        Vector3.down, .5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        eligibility += $"; supportHit={hit.collider.name}@{hit.distance:F4},owner={hit.collider.GetComponentInParent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"))}";
                }
                Assert.IsTrue((bool)Call(saws, "TryGetCooperativeLaunch", assistedLaunch), "출발선의 실제 목마 최상층만 허용: " + eligibility);
                float previousJumpOverride = (float)Get(mover, "CourseJumpHeightOverride");
                var follow = Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.PiggybackFollow"));
                var piggyback = Get(Static("CurtainCall.Settings.GameSettings", "Tightrope"), "Piggyback");
                // W+Space가 호출하는 실제 분리 점프 경로. 원격 동료는 합성 위치만 받고 AI는 사용하지 않는다.
                Call(follow, "Launch", player, (float)Get(piggyback, "ForwardJumpSpeed"));
                Assert.That((float)Get(mover, "CourseJumpHeightOverride"), Is.EqualTo(previousJumpOverride).Within(.001f), "관문 보정은 이번 시작 궤적에만 적용하고 기본값을 되돌린다.");
                yield return Until(() => (bool)Get(mover, "IsAirborne"), 2f, "목마 위층 앞으로 분리 점프");
                yield return Until(() => !(bool)Get(mover, "IsAirborne"), 3f, "관문 너머 실제 착지");
                Assert.That((float)Call(course, "GetDistance", player.transform.position), Is.InRange(7.4f, 9f));
                Assert.AreEqual(initialParts, Convert.ToInt32(Get(player, "LostParts")), "협동 점프는 기존 부상과 관계없이 톱 장벽에 닿지 않는다.");
                yield return Until(() => (bool)Get(saws, "IsCooperativeGatePassable"), 2f, "뒤 발판이 관문을 위로 튕겨 아래 통로를 엶");
                var collider = saws.transform.Find("Generated/CooperativeGate").GetComponentInChildren<MeshCollider>();
                Assert.IsFalse(collider.enabled);
                yield return Until(() => saws.transform.Find("Generated/ReleasedGate0") is { } released
                    && released.position.y > closedGateHeight + 1.5f, 1f, "톱이 실제로 동료 머리 위로 튀어 오른다.");
                Vector3 releasedPosition = saws.transform.Find("Generated/ReleasedGate0").position;
                Vector3 releasedLane = (Vector3)Call(course, "GetLanePosition", 1, 6f);
                Assert.That(Mathf.Abs(Vector3.Dot(releasedPosition - releasedLane, (Vector3)Get(course, "Right"))),
                    Is.LessThan(.001f), "발판으로 튀긴 뒤에도 같은 줄을 직진한다.");
                yield return Until(() => !(bool)Get(saws, "IsCooperativeGateActive"), 2f, "톱이 관문을 떠나 뒤쪽 줄에 착지");
                Assert.IsFalse(gateView.gameObject.activeSelf, "원래 자리에 열린 톱이나 보이지 않는 벽을 남기지 않는다.");
                Assert.IsTrue((bool)Get(saws, "IsTeamPassageOpen"));
                Assert.That((float)Call(course, "GetDistance", helper.transform.position), Is.LessThan(6f), "아래 동료가 아직 앞에 있어도 뒤 발판으로 길을 연다.");
                // 같은 통과자가 뒤로 걸어서 실제 충돌체가 열렸는지 확인한다(순간 이동 통과가 아니다).
                float deadline = Time.realtimeSinceStartup + 3f;
                while ((float)Call(course, "GetDistance", player.transform.position) > 5.5f && Time.realtimeSinceStartup < deadline)
                { Call(mover, "SetMoveInput", -1f); yield return null; }
                Assert.That((float)Call(course, "GetDistance", player.transform.position), Is.LessThan(6f));
                Assert.AreEqual(initialParts, Convert.ToInt32(Get(player, "LostParts")));
            }
            finally
            {
                if (helperObject != null)
                {
                    if ((bool)Get(helperObject, "IsSpawned")) Call(helperObject, "Despawn", true);
                    else Object.Destroy(helperObject.gameObject);
                }
                Call(saws, "ResetCooperativeGates");
                Set(settings, "cooperativeGates", enabledBefore);
                Set(settings, "cooperativeGateDistances", distancesBefore);
                Set(settings, "cooperativeGateApproachDistance", approachBefore);
            }
        }

        [UnityTest]
        public IEnumerator CooperativeGateApproachSnapshotAndWholeCourseResetRemainAuthoritative()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            yield return StandOnRopeForSawTest(1, 4f);
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var settings = Get(saws, "Settings");
            bool enabledBefore = (bool)Get(settings, "cooperativeGates");
            var distancesBefore = (float[])Get(settings, "cooperativeGateDistances");
            float approachBefore = (float)Get(settings, "cooperativeGateApproachDistance");
            try
            {
                Set(settings, "cooperativeGates", true);
                Set(settings, "cooperativeGateDistances", new[] { 6f });
                Set(settings, "cooperativeGateApproachDistance", 12f);
                Set(settings, "cooperativeGateApproachSpeed", 3f); // 이 회귀 검사는 명시적으로 3m/s인 일반 정지 관문.
                float approachStarted = (float)Get(saws, "GameElapsed");
                Call(saws, "SetCooperativeGate", 0, true, approachStarted, -1f);
                Assert.That((float)Get(saws, "CooperativeGateDistance"), Is.EqualTo(18f).Within(.001f));
                yield return new WaitForSeconds(.2f);
                // 저프레임에서도 실제 게임 시계로 3m/s 접근을 확인한다. 프레임 대기가 정확히 0.2초라고 가정하지 않는다.
                float approachElapsed = (float)Get(saws, "GameElapsed") - approachStarted;
                Assert.That(approachElapsed, Is.GreaterThan(0f));
                Assert.That((float)Get(saws, "CooperativeGateDistance"),
                    Is.EqualTo(Mathf.Max(6f, 18f - approachElapsed * 3f)).Within(.001f));
                Call(saws, "UpdateViews");
                Vector3 approachLane = (Vector3)Call(course, "GetLanePosition", (int)Get(saws, "CooperativeGateLane"), 6f);
                Vector3 right = (Vector3)Get(course, "Right");
                Assert.That(Mathf.Abs(Vector3.Dot(saws.transform.Find("Generated/CooperativeGate").position - approachLane, right)),
                    Is.LessThan(.001f), "접근 중에도 정지 줄의 정면에 있는 실제 톱과 충돌체");
                var path = saws.transform.Find("Generated/Saw paths (visual only)/Cooperative path").GetComponent<LineRenderer>();
                Assert.IsTrue(path.enabled);
                for (int i = 0; i < path.positionCount; i++)
                    Assert.That(Mathf.Abs(Vector3.Dot(path.GetPosition(i) - approachLane, right)), Is.LessThan(.001f), "기존 예고선도 실제 직진 경로와 일치");
                object writer = Call(saws, "WriteCooperativeGate");
                byte[] bytes;
                try { bytes = (byte[])Call(writer, "ToArray"); }
                finally { ((IDisposable)writer).Dispose(); }
                float distance = (float)Get(saws, "CooperativeGateDistance");
                int lane = (int)Get(saws, "CooperativeGateLane");
                Call(saws, "ResetCooperativeGates");
                ReplayCooperativeGate(saws, bytes, 777UL);
                Assert.IsFalse((bool)Get(saws, "IsCooperativeGateActive"));
                ReplayCooperativeGate(saws, bytes, 0UL);
                Assert.IsTrue((bool)Get(saws, "IsCooperativeGateActive"));
                Assert.That((float)Get(saws, "CooperativeGateDistance"), Is.EqualTo(distance).Within(.001f));
                Assert.That((int)Get(saws, "CooperativeGateLane"), Is.EqualTo(lane), "호스트가 고른 줄도 함께 동기화한다.");
                yield return Until(() => (bool)Get(saws, "IsCooperativeGateStopped"), 5f, "3m/s로 12m 접근한 뒤 정지");
                Assert.That((float)Get(saws, "CooperativeGateDistance"), Is.EqualTo(6f).Within(.001f));
                yield return new WaitForSeconds(.15f);
                Assert.That((float)Get(saws, "CooperativeGateDistance"), Is.EqualTo(6f).Within(.001f));
                int oldRound = (int)Get(run, "Round");
                var hud = Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
                Set(settings, "cooperativeGateDistances", new[] { 100f }); // 리셋 직후 자동 등장 조건은 만족하지 않는다.
                Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
                yield return Until(() => (int)Get(run, "Round") > oldRound, 5f, "관문 포함 전체 리셋");
                Assert.IsFalse((bool)Get(saws, "IsCooperativeGateActive"));
                ReplayCooperativeGate(saws, bytes, 0UL);
                Assert.IsFalse((bool)Get(saws, "IsCooperativeGateActive"), "이전 시도의 관문 메시지를 거절한다.");
            }
            finally
            {
                Call(saws, "ResetCooperativeGates");
                Set(settings, "cooperativeGates", enabledBefore);
                Set(settings, "cooperativeGateDistances", distancesBefore);
                Set(settings, "cooperativeGateApproachDistance", approachBefore);
            }
        }

        static void ReplayCooperativeGate(Component saws, byte[] bytes, ulong sender)
        {
            object allocator = Enum.Parse(GameType("Unity.Collections.Allocator"), "Temp");
            var reader = Activator.CreateInstance(GameType("Unity.Netcode.FastBufferReader"), bytes, allocator, -1, 0);
            try { Call(saws, "HandleCooperativeGateMessage", sender, reader); }
            finally { ((IDisposable)reader).Dispose(); }
        }

        [UnityTest]
        public IEnumerator CooperativeGateSideBypassWaitsForTeammateThenAllowsNextSaw()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var settings = Get(saws, "Settings");
            bool enabledBefore = (bool)Get(settings, "cooperativeGates");
            var distancesBefore = (float[])Get(settings, "cooperativeGateDistances");
            float approachBefore = (float)Get(settings, "cooperativeGateApproachDistance");
            Component helper = null;
            try
            {
                ConfigureStationaryGateForTest(saws, settings, 6f);
                Set(settings, "cooperativeGateDistances", new[] { 6f, 14f });
                helper = CreatePracticeBotTestPartner(1, 4f);
                yield return StandOnRopeForSawTest(2, 9.5f);
                yield return new WaitForSeconds(.1f);
                Assert.That((int)Get(saws, "CooperativeGateIndex"), Is.EqualTo(0), "뒤에 동료가 남아 있으면 기존 톱과 발판 유지");
                DestroyPracticeBotTestPartner(helper);
                helper = CreatePracticeBotTestPartner(2, 10f);
                yield return Until(() => (int)Get(saws, "CooperativeGateIndex") == 1, 2f, "모두 옆줄로 우회하면 다음 톱 등장");
                Assert.That((int)Get(saws, "CooperativeGateLane"), Is.EqualTo(2));
            }
            finally
            {
                DestroyPracticeBotTestPartner(helper);
                Call(saws, "ResetCooperativeGates");
                Set(settings, "cooperativeGates", enabledBefore);
                Set(settings, "cooperativeGateDistances", distancesBefore);
                Set(settings, "cooperativeGateApproachDistance", approachBefore);
            }
        }

        [UnityTest]
        public IEnumerator CooperativeGateBotBehindDiscOnlyWalksToRearPlateAsSecondFloor()
        {
            yield return BeginPerformance();
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
            var saws = (Component)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current");
            var settings = Get(saws, "Settings");
            bool enabledBefore = (bool)Get(settings, "cooperativeGates");
            var distancesBefore = (float[])Get(settings, "cooperativeGateDistances");
            float approachBefore = (float)Get(settings, "cooperativeGateApproachDistance");
            Component helper = null;
            try
            {
                ConfigureStationaryGateForTest(saws, settings, 6f);
                helper = CreatePracticeBotTestPartner(1, 4f);
                // 확대된 톱의 몸통 접촉 끝(7.35m)과 발판 시작(7.4m) 사이에서 입력을 검사한다.
                yield return StandOnRopeForSawTest(1, 7.39f);
                yield return Until(() => !(bool)Get(player, "IsAirborne"), 2f, "봇 판단 전 공유 착지 상태");
                var brain = Activator.CreateInstance(GameType("CurtainCall.Tightrope.TightropePracticeBotBrain"), new object[] { player, helper });
                var input = Call(brain, "BuildInput");
                Assert.That((float)Get(input, "Move"), Is.Zero, "담당 층이 아닌 봇은 뒤 버튼을 먼저 밟지 않는다.");
                StringAssert.Contains("2층 담당", (string)Get(brain, "Status"));
                Set(brain, "gateLaunchFloor", 2);
                input = Call(brain, "BuildInput");
                Assert.That((float)Get(input, "Move"), Is.EqualTo(1f), "2층 담당은 톱 중심을 지나도 뒤 버튼까지 전진한다.");
            }
            finally
            {
                DestroyPracticeBotTestPartner(helper);
                Call(saws, "ResetCooperativeGates");
                Set(settings, "cooperativeGates", enabledBefore);
                Set(settings, "cooperativeGateDistances", distancesBefore);
                Set(settings, "cooperativeGateApproachDistance", approachBefore);
            }
        }

        void ConfigureStationaryGateForTest(Component saws, object settings, float distance)
        {
            Set(settings, "cooperativeGates", true);
            Set(settings, "cooperativeGateDistances", new[] { distance });
            Set(settings, "cooperativeGateApproachDistance", 0f); // 접근 대기만 생략. 충돌·목마·비행·발판 판정은 실제 Update 사용.
            Set(settings, "cooperativeGateApproachSpeed", 0f); // 이 도우미는 정지 관문 회귀 검사 전용.
            Call(saws, "SetCooperativeGate", 0, true, (float)Get(saws, "GameElapsed"), -1f);
            Set(saws, "cooperativeGateLane", 1);
        }
    }
}
