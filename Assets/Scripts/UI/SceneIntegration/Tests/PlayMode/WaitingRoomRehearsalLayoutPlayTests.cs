using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static object Priv(object target, string name)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, Any);
                if (field != null) return field.GetValue(target);
                var prop = type.GetProperty(name, Any);
                if (prop != null) return prop.GetValue(target);
            }
            throw new MissingMemberException(target.GetType().Name, name);
        }
        static void SetPriv(object target, string name, object value) => target.GetType().GetField(name, Any).SetValue(target, value);
        static object Invoke(object target, string name, params object[] args)
        {
            foreach (var method in target.GetType().GetMethods(Any))
                if (method.Name == name && method.GetParameters().Length == args.Length) return method.Invoke(target, args);
            throw new MissingMethodException(target.GetType().Name, name);
        }
        static object NestedEnum(object owner, string nested, string value) =>
            System.Enum.Parse(owner.GetType().GetNestedType(nested, BindingFlags.NonPublic), value);

        static object Rehearsal => T(RehearsalType).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        static Component LocalPlayer => (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
        static ulong LocalId => (ulong)Get(LocalPlayer, "OwnerClientId");

        /// <summary>호스트 리허설을 그 소구간으로 바로 넘긴다(새 회차).</summary>
        static IEnumerator JumpStage(int stage)
        {
            var r = Rehearsal;
            SetPriv(r, "stage", stage);
            SetPriv(r, "round", (int)Priv(r, "round") + 1);
            SetPriv(r, "version", (int)Priv(r, "version") + 1);
            yield return WaitStage(stage, 5f);
            yield return new WaitForSecondsRealtime(.6f);
        }

        static void PlaceOnRope(Component course, int lane, float distance)
        {
            var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
            var position = (Vector3)Call(course, "GetLanePosition", lane, distance);
            Call(mover, "Relocate", position, Quaternion.LookRotation((Vector3)Get(course, "Forward")));
        }

        static bool Blocked(Component course, int lane, float distance) => (bool)Call(course, "IsSegmentBlocked", lane, distance);

        [UnityTest]
        public IEnumerator W6_Rehearsal_CoursesFollowStageDiagrams()
        {
            // Task-097: 기획서 5~7장 구조도대로 줄·발판·그네·트리거 그네·끊긴 구간을 놓고, 연습실 옛 소품은 숨긴다.
            FreshTutorial();
            T(RehearsalType).GetField("savedStage", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 0);
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return WaitStage(0, 10f);
                yield return new WaitForSecondsRealtime(.6f);
                var room = GameObject.Find("S09_PracticeRoom").transform;
                Assert.IsFalse(room.Find("LegacyRing/PracticeLanes").gameObject.activeSelf, "리허설 중 연습실 옛 소품은 숨긴다");

                // 1단계: 위쪽 줄은 중간 지점 발판에서 끝나고, 아래쪽 줄만 도착 장소로 간다.
                var course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                var layout = Get(course, "Layout");
                Assert.AreEqual(2, Get(course, "LaneCount"));
                Assert.AreEqual(14f, (float)Call(layout, "GetLaneEnd", 0), .01f, "위쪽 줄은 중간 지점에서 끝");
                Assert.AreEqual(26f, (float)Call(layout, "GetLaneEnd", 1), .01f);
                Assert.IsNotNull(course.transform.Find("RehearsalPlatform"), "중간 지점 발판");

                // 2-1: 그네 1개.
                yield return JumpStage(1);
                course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                Assert.IsTrue(Blocked(course, 0, 13f) && !Blocked(course, 0, 6f) && !Blocked(course, 0, 18f), "2-1 그네 자리(공연과 같은 8m)만 끊김");

                // 2-2: 그네 2개가 이어져 있고, 첫 그네를 놓으면 날아가며 두 번째 그네를 잡는다.
                yield return JumpStage(2);
                course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                var root = course.GetComponent(T("CurtainCall.Tightrope.TightropeSwingPole"));
                var poles = (IList)Priv(root, "poles");
                Assert.AreEqual(2, poles.Count, "2-2 그네 2개");
                Assert.IsTrue(Blocked(course, 0, 11f) && Blocked(course, 0, 17f) && !Blocked(course, 0, 22f));
                var first = poles[0]; var second = poles[1];
                Assert.AreSame(second, Priv(first, "next"));
                yield return SwingAcross(course, first, second);

                // 2-3: 위쪽 줄 끝 트리거 그네 1을 잡는 동안만 아래쪽 줄이 이어진다.
                yield return JumpStage(3);
                course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                Assert.AreEqual(12f, (float)Call(Get(course, "Layout"), "GetLaneEnd", 0), .01f, "위쪽 줄은 트리거 그네에서 끝");
                Assert.IsTrue(Blocked(course, 1, 15f), "아래쪽 줄 가운데가 끊김");
                yield return HoldTrigger(course, 0, 0, 11.4f);
                Assert.IsFalse(Blocked(course, 1, 15f), "트리거를 잡는 동안 이어짐");
                ReleaseTrigger(0);
                yield return null; yield return null;
                Assert.IsTrue(Blocked(course, 1, 15f), "놓으면 다시 끊김");

                // 3단계: 줄마다 그네, 1줄 트리거 → 2줄 앞쪽 끊김, 2줄 트리거 → 1줄 뒤쪽 끊김(3·4줄 같은 짝).
                yield return JumpStage(4);
                course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                root = course.GetComponent(T("CurtainCall.Tightrope.TightropeSwingPole"));
                Assert.AreEqual(4, ((IList)Priv(root, "poles")).Count, "줄마다 인간 그네");
                for (int lane = 0; lane < 4; lane++) Assert.IsTrue(Blocked(course, lane, 7f), $"{lane + 1}줄 그네 끊김");
                Assert.IsTrue(Blocked(course, 0, 20f) && Blocked(course, 1, 18f) && !Blocked(course, 0, 15f) && !Blocked(course, 1, 23f), "엇갈린 끊김");
                yield return HoldTrigger(course, 0, 0, 16.6f);
                Assert.IsFalse(Blocked(course, 1, 18f), "1줄 트리거 → 2줄 연결");
                Assert.IsTrue(Blocked(course, 0, 20f) && Blocked(course, 3, 18f), "다른 줄은 그대로");
                ReleaseTrigger(0);
                yield return HoldTrigger(course, 3, 3, 23.1f);
                Assert.IsFalse(Blocked(course, 2, 20f), "4줄 트리거 → 3줄 연결");
                Assert.IsTrue(Blocked(course, 1, 18f));
            }
            finally
            {
                T(RehearsalType).GetField("savedStage", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 0);
                EndTutorial();
            }
        }

        static object RelayGate => Static("CurtainCall.Tightrope.TightropeRelayGate", "Current");
        static uint PoleRevision(object gate, int index) =>
            (uint)Priv(Priv(((Array)Priv(gate, "stations")).GetValue(index), "State"), "Revision");

        /// <summary>공연 해제봉과 같은 F 잡기 요청(호스트 처리)으로 트리거 그네를 잡는다.</summary>
        IEnumerator HoldTrigger(Component course, int index, int lane, float distance)
        {
            PlaceOnRope(course, lane, distance);
            yield return new WaitForSecondsRealtime(.4f);
            var gate = RelayGate;
            Invoke(gate, "SubmitGrab", LocalId, index, PoleRevision(gate, index));
            yield return null; yield return null;
            Assert.AreEqual(LocalId, (ulong)Call(gate, "HolderAt", index), $"트리거 그네 {index + 1}을 잡아야 한다");
            Assert.IsTrue((bool)Call(gate, "IsHeld", LocalPlayer), "공연 해제봉처럼 봉에 매달린다");
        }

        static void ReleaseTrigger(int index)
        {
            var gate = RelayGate;
            Invoke(gate, "ServerHandle", LocalId, NestedEnum(gate, "Operation", "Release"), index, PoleRevision(gate, index));
        }

        IEnumerator SwingAcross(Component course, object first, object second)
        {
            float entry = (float)Priv(first, "EntryDistance");
            PlaceOnRope(course, 0, entry);
            yield return new WaitForSecondsRealtime(.5f);
            Invoke(first, "ServerHandle", LocalId, NestedEnum(first, "Operation", "Grab"), 0f);
            yield return null; yield return null;
            Assert.AreEqual("Holding", Priv(first, "mode").ToString(), "첫 그네를 잡는다");
            // W를 누른 것처럼 반동을 키우다가 놓을 수 있게 되면 놓는다.
            float until = Time.realtimeSinceStartup + 12f;
            float target = (float)Priv(second, "EntryDistance");
            // 테스트는 공중 W 조절이 없으므로, 조절 없이도 두 번째 그네 매달림 위치에 닿는 순간 놓는다.
            while ((float)Call(course, "GetDistance", (Vector3)Invoke(first, "PredictLanding", false)) < target - .2f && Time.realtimeSinceStartup < until)
            {
                float speed = (float)Priv(first, "angularSpeed");
                Invoke(first, "ServerHandle", LocalId, NestedEnum(first, "Operation", "Swing"), speed >= 0f ? 1f : -1f);
                yield return null;
            }
            Assert.IsTrue((bool)Priv(first, "OwnCanLetGo"), "반동을 키우면 놓을 수 있어야 한다");
            Debug.Log($"[Test] 놓기 예상 착지 {(float)Call(course, "GetDistance", (Vector3)Invoke(first, "PredictLanding", false)):0.00} / 두 번째 그네 {target:0.00}");
            Invoke(first, "ServerHandle", LocalId, NestedEnum(first, "Operation", "LetGo"), 0f);
            // 공연 공중그네처럼 날아가는 동안 F를 눌러야 잡는다(누르지 않으면 잡지 않는다).
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual("Idle", Priv(second, "mode").ToString(), "F 없이 자동으로 잡지 않는다");
            until = Time.realtimeSinceStartup + 4f;
            while (Priv(second, "mode").ToString() != "Holding" && Time.realtimeSinceStartup < until)
            {
                SetPriv(first, "airGrabRequestedUntil", (double)Priv(first, "Clock") + .3d);
                yield return null;
            }
            until = Time.realtimeSinceStartup + 4f;
            while (Priv(second, "mode").ToString() != "Holding" && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual("Holding", Priv(second, "mode").ToString(), "날아가는 동안 두 번째 그네를 잡는다");
            Assert.AreEqual(LocalId, (ulong)Priv(second, "holder"));
            Assert.AreEqual("Normal", Get(LocalPlayer, "State").ToString(), "떨어지지 않는다");
        }
    }
}
