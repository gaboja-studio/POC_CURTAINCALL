using System;
using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.EditorTools.Tests
{
    /// <summary>
    /// 2026-11-26 Task-204: 높은 그네(목마 전용) 바닥 잡기 판정과 그네 앞뒤 반동·도약 방향 재검사.
    /// 호스트 요청 처리(HandleRequest)·공통 진자(PoleSwingMotion)·실제 목마 연결을 그대로 쓴다. 다른 PC 플레이 검증은 아니다.
    /// </summary>
    [Category("CommonRulesRegression")]
    public sealed class HighPoleAndSwingDirectionTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, AnyInstance).Invoke(target, args);
        static object Get(object target, string name) => target.GetType().GetField(name, AnyInstance).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, AnyInstance).SetValue(target, value);

        GameObject root;
        HumanCannonSystem cannon;
        HumanCannonPoleSystem poles;
        PiggybackSystem stack;
        NetworkSessionManager session;
        object savedCannon, savedPoles, savedStack;
        bool savedSuppressed;
        string savedPoleJson;
        readonly List<NetworkPlayer> crew = new();
        Type requestType;
        Vector3 Base => root.transform.position;

        [SetUp]
        public void SetUp()
        {
            savedCannon = typeof(HumanCannonSystem).GetProperty("Current").GetValue(null);
            savedPoles = typeof(HumanCannonPoleSystem).GetProperty("Current").GetValue(null);
            savedStack = typeof(PiggybackSystem).GetProperty("Current").GetValue(null);
            savedSuppressed = PiggybackSystem.Suppressed;
            savedPoleJson = JsonUtility.ToJson(GameSettings.Poles);
            PiggybackSystem.Suppressed = false;
            root = new GameObject("High pole rules");
            root.transform.position = new Vector3(15200f, 60f, 15200f);
            session = Child("Session", false).AddComponent<NetworkSessionManager>();
            session.GetType().GetProperty("ConnectionState").SetValue(session, SessionConnectionState.Host);
            cannon = Child("Cannon", false).AddComponent<HumanCannonSystem>();
            Call(cannon, "ResetSeats");
            typeof(HumanCannonSystem).GetProperty("Current").SetValue(null, cannon);
            poles = Child("Pole system", false).AddComponent<HumanCannonPoleSystem>();
            Set(poles, "session", session);
            typeof(HumanCannonPoleSystem).GetProperty("Current").SetValue(null, poles);
            stack = Child("Piggyback").AddComponent<PiggybackSystem>();
            Set(stack, "session", session);
            typeof(PiggybackSystem).GetProperty("Current").SetValue(null, stack);
            requestType = typeof(HumanCannonPoleSystem).GetNestedType("PoleAction", BindingFlags.NonPublic);
        }

        [TearDown]
        public void TearDown()
        {
            typeof(HumanCannonSystem).GetProperty("Current").SetValue(null, savedCannon);
            typeof(HumanCannonPoleSystem).GetProperty("Current").SetValue(null, savedPoles);
            typeof(PiggybackSystem).GetProperty("Current").SetValue(null, savedStack);
            PiggybackSystem.Suppressed = savedSuppressed;
            JsonUtility.FromJsonOverwrite(savedPoleJson, GameSettings.Poles);
            var all = (List<NetworkPlayer>)NetworkPlayer.All;
            foreach (var player in crew)
            {
                typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, false);
                all.Remove(player);
            }
            crew.Clear();
            Object.DestroyImmediate(root);
        }

        GameObject Child(string name, bool active = true)
        {
            var child = new GameObject(name);
            child.SetActive(active);
            child.transform.SetParent(root.transform, false);
            return child;
        }

        NetworkPlayer Actor(ulong id, Vector3 feet)
        {
            var player = Child("Actor " + id).AddComponent<NetworkPlayer>();
            player.transform.position = feet;
            Call(player.GetComponent<PlayerMover>(), "Awake");
            typeof(NetworkPlayer).GetProperty("IsSpawned").SetValue(player, true);
            typeof(NetworkPlayer).GetProperty("OwnerClientId").SetValue(player, id);
            crew.Add(player);
            ((List<NetworkPlayer>)NetworkPlayer.All).Add(player);
            return player;
        }

        HumanCannonPole Pole(Vector3 bar, bool piggybackOnly)
        {
            var pole = Child("Pole").AddComponent<HumanCannonPole>();
            pole.transform.position = bar;
            pole.PiggybackOnly = piggybackOnly;
            return pole;
        }

        void Link(NetworkPlayer upper, NetworkPlayer lower)
        {
            ((Dictionary<ulong, ulong>)Get(stack, "belowOf"))[upper.OwnerClientId] = lower.OwnerClientId;
            ((Dictionary<ulong, ulong>)Get(stack, "aboveOf"))[lower.OwnerClientId] = upper.OwnerClientId;
        }

        void Request(NetworkPlayer actor, string action, int index, float input = 0f, uint sequence = 0u)
            => Call(poles, "HandleRequest", actor.OwnerClientId, Enum.Parse(requestType, action), index,
                poles.State(index).Revision, CannonFlightState.PoleEntrySource, 1u, 0u, input, sequence, -1d);

        // 줄 위 발 3.5m + 매달림 1.5m = 봉 5m(외줄 공중그네 첫 봉과 같은 높이). 바닥은 Base.
        Vector3 HighBar => Base + Vector3.up * 5f;
        static Vector3 FeetForCenter(Vector3 floorPoint, float centerHeight) => floorPoint + Vector3.up * (centerHeight - GameSettings.Base.Character.CapsuleHeight * .5f);

        [Test]
        public void HighPoleRejectsSoloJumpButKeepsPiggybackFlightAndOrdinaryPoles()
        {
            var high = Pole(HighBar, piggybackOnly: true);
            int index = poles.Register(high);
            // 2단 점프(근사) 몸 중심: 공통 반경 3m 안이라 예전에는 혼자 잡혔다.
            var solo = Actor(1801, FeetForCenter(Base, PoleReachReference.SoloDoubleJump));
            Physics.SyncTransforms();
            Assert.That((PlayerInteraction.Center(solo) - high.BarPosition(0f)).magnitude, Is.LessThan(GameSettings.Poles.GrabRadius),
                "재현 조건: 혼자 2단 점프한 몸 중심이 기존 구 판정 안");
            Assert.IsFalse(poles.CanApproachPole(index, solo), "목마 전용 높은 봉은 혼자 점프로 잡히지 않는다.");
            Request(solo, "PlatformGrab", index);
            Assert.That(poles.HeldBy(solo.OwnerClientId), Is.EqualTo(-1), "호스트도 같은 규칙으로 거부한다.");

            // 같은 위치라도 대포·봉 비행 중이면 기존 공중 잡기 반경을 그대로 쓴다.
            Call(cannon, "BeginPoleFlight", solo.OwnerClientId, 1u);
            Assert.IsTrue(poles.CanApproachPole(index, solo), "공중 비행 잡기 체감은 바뀌지 않는다.");
            Call(cannon, "AbandonPole", solo.OwnerClientId);

            // 목마 위에 선 사람은 점프 없이도 잡을 수 있다(대기실 연습의 'F로 첫 봉 잡기' 유지).
            var carrier = Actor(1802, Base + Vector3.right * .3f);
            var rider = Actor(1803, Base + Vector3.right * .3f + Vector3.up * GameSettings.Base.Character.CapsuleHeight);
            Link(rider, carrier);
            Physics.SyncTransforms();
            Assert.IsTrue(poles.CanApproachPole(index, rider), "목마 맨 위 사람은 판정 상자 안에서 잡는다.");
            Assert.IsFalse(poles.CanApproachPole(index, carrier), "아래 사람(목마를 태운 사람)은 바닥에서 높은 봉을 잡지 못한다.");
            Request(rider, "PlatformGrab", index);
            Assert.That(poles.State(index).SeatOf(rider.OwnerClientId), Is.Zero);
            Assert.IsFalse(stack.IsInStack(rider), "잡는 순간 기존 장치 인계로 목마가 풀린다.");

            // 같은 2단 점프 높이라도 일반 봉은 예전처럼 잡힌다.
            var ordinary = Pole(HighBar + Vector3.right * 20f, piggybackOnly: false);
            int ordinaryIndex = poles.Register(ordinary);
            var other = Actor(1804, FeetForCenter(Base + Vector3.right * 20f, PoleReachReference.SoloDoubleJump));
            Physics.SyncTransforms();
            Assert.IsTrue(poles.CanApproachPole(ordinaryIndex, other), "목마 전용이 아닌 봉의 판정은 그대로다.");
        }

        [Test]
        public void CustomGrabZoneAndStackRuleCanBeTunedFromTheInspector()
        {
            var high = Pole(HighBar, piggybackOnly: true);
            int index = poles.Register(high);
            var carrier = Actor(1811, Base);
            var rider = Actor(1812, Base + Vector3.up * GameSettings.Base.Character.CapsuleHeight);
            Link(rider, carrier);
            Physics.SyncTransforms();
            Assert.IsTrue(poles.CanApproachPole(index, rider));

            // 개별 상자 바닥을 봉 아래 2m로 올리면 목마 위에 서서는 닿지 않고, 목마 점프 높이에서만 닿는다.
            var serialized = new UnityEditor.SerializedObject(high);
            serialized.FindProperty("customGrabZone").boolValue = true;
            serialized.FindProperty("grabZone").FindPropertyRelative("center").vector3Value = new Vector3(0f, -.5f, 0f);
            serialized.FindProperty("grabZone").FindPropertyRelative("size").vector3Value = new Vector3(3f, 3f, 4.6f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsFalse(poles.CanApproachPole(index, rider), "개별 판정 상자가 기본 영역을 대신한다.");
            rider.transform.position = FeetForCenter(Base, PoleReachReference.PiggybackJump);
            Physics.SyncTransforms();
            Assert.IsTrue(poles.CanApproachPole(index, rider), "목마 점프 높이는 좁힌 상자 안이다.");

            // 목마 조건을 끄면 상자만 본다: 상자 안 높이의 혼자 점프는 잡히고, 상자 밖은 여전히 안 잡힌다.
            JsonUtility.FromJsonOverwrite("{\"piggybackOnlyRequiresStack\":false}", GameSettings.Poles);
            var solo = Actor(1813, FeetForCenter(Base + Vector3.forward * .2f, PoleReachReference.SoloDoubleJump));
            Physics.SyncTransforms();
            Assert.IsFalse(poles.CanApproachPole(index, solo), "봉 아래 2m보다 낮은 2단 점프는 상자 밖");
            solo.transform.position = FeetForCenter(Base + Vector3.forward * .2f, 3.2f);
            Physics.SyncTransforms();
            Assert.IsTrue(poles.CanApproachPole(index, solo), "목마 조건을 끄면 상자 안의 누구나 잡는다.");
        }

        static IEnumerable<TestCaseData> DirectionCases()
        {
            yield return new TestCaseData(false, 1f).SetName("SwingDirection_FacingForward_W");
            yield return new TestCaseData(false, -1f).SetName("SwingDirection_FacingForward_S");
            yield return new TestCaseData(true, 1f).SetName("SwingDirection_FacingBackward_W");
            yield return new TestCaseData(true, -1f).SetName("SwingDirection_FacingBackward_S");
        }

        [TestCaseSource(nameof(DirectionCases))]
        public void PumpAndReleaseFollowTheRiderRelativeInput(bool reverse, float input)
        {
            var pole = Pole(HighBar, piggybackOnly: false);
            int index = poles.Register(pole);
            var rider = Actor(1821, pole.HangingPosition(0f));
            Physics.SyncTransforms();
            Request(rider, "PlatformGrab", index, 0f, reverse ? 1u : 0u);
            Assert.That(poles.State(index).SeatOf(rider.OwnerClientId), Is.Zero);
            Vector3 facing = poles.RiderRotation(index, rider.OwnerClientId) * Vector3.forward;
            Assert.That(Vector3.Dot(facing, pole.Forward), Is.EqualTo(reverse ? -1f : 1f).Within(.001f));

            float firstSwing = Pump(rider, pole, index, input, 180, out _);
            Assert.That(Mathf.Sign(firstSwing) * (reverse ? -1f : 1f), Is.EqualTo(Mathf.Sign(input)),
                "W는 바라보는 쪽, S는 등 쪽으로 처음 흔들린다(역방향 탑승 포함).");
            Request(rider, "Release", index);
            var runtime = ((Array)Get(poles, "states")).GetValue(index);
            var launch = Vector3.ProjectOnPlane((Vector3)Get(runtime, "ReleaseVelocity"), Vector3.up);
            Assert.That(launch.magnitude, Is.GreaterThan(1f), "모은 반동으로 수평 도약");
            Assert.That(Mathf.Sign(Vector3.Dot(launch, facing)), Is.EqualTo(Mathf.Sign(input)),
                "도약은 마지막 반동 입력 방향(바라보는 쪽 기준 W=앞, S=뒤)");
        }

        [TestCase(false)] [TestCase(true)]
        public void PassengerJumpsWithTheSwingRegardlessOfTheirFacing(bool passengerReverse)
        {
            var pole = Pole(HighBar, piggybackOnly: false);
            int index = poles.Register(pole);
            var top = Actor(1831, pole.HangingPosition(0f));
            Physics.SyncTransforms();
            Request(top, "PlatformGrab", index, 0f, 0u);
            var lower = Actor(1832, pole.HangingPosition(0f, 1));
            Physics.SyncTransforms();
            Request(lower, "PlatformGrab", index, 0f, passengerReverse ? 1u : 0u);
            Assert.That(poles.State(index).SeatOf(lower.OwnerClientId), Is.EqualTo(1));
            Pump(top, pole, index, 1f, 180, out _);
            Request(lower, "Release", index);
            var runtime = ((Array)Get(poles, "states")).GetValue(index);
            var launch = Vector3.ProjectOnPlane((Vector3)Get(runtime, "ReleaseVelocity"), Vector3.up);
            Assert.That(Vector3.Dot(launch, pole.Forward), Is.GreaterThan(1f),
                "아래 사람은 자신이 바라보는 쪽과 관계없이 위 사람이 만든 반동 방향으로 도약한다.");
        }

        /// <summary>호스트 입력·공통 진자로 프레임을 진행한다. 처음 의미 있게 흔들린 각도를 반환한다.</summary>
        float Pump(NetworkPlayer driver, HumanCannonPole pole, int index, float input, int frames, out float maxAngle)
        {
            var runtime = ((Array)Get(poles, "states")).GetValue(index);
            float first = 0f; maxAngle = 0f;
            uint sequence = 10;
            for (int frame = 0; frame < frames; frame++)
            {
                if (frame % 3 == 0) Request(driver, "Swing", index, input, ++sequence);
                float drive = (float)Call(poles, "RefreshSwingDrive", runtime);
                float angle = (float)Get(runtime, "Angle"), speed = (float)Get(runtime, "Speed");
                pole.Step(ref angle, ref speed, drive, 1f / 60f, GameSettings.Base.Character.Gravity);
                Set(runtime, "Angle", angle); Set(runtime, "Speed", speed);
                if (first == 0f && Mathf.Abs(angle) > 1f) first = angle;
                maxAngle = Mathf.Max(maxAngle, Mathf.Abs(angle));
            }
            Assert.That(maxAngle, Is.GreaterThan(10f), "입력으로 반동이 쌓인다.");
            return first;
        }
    }
}
