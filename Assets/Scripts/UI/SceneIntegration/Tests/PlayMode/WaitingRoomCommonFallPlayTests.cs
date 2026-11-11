using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    public sealed partial class DevStartLauncherPlayTests
    {
        [UnityTest, Category("TutorialCommonFalls")]
        public IEnumerator TutorialCommonFalls_EndingLaterPracticeKeepsDeadPlayerDeadAndControlDisabled()
        {
            var saved = T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            object previousMastery = saved.GetValue(null);
            try
            {
                yield return Launch(2, "Morning", 0, "Tightrope", "WaitingRoomBase");
                saved.SetValue(null, 5);
                var ring = GameObject.Find("S09_PracticeRoom").transform.Find("LegacyRing/PracticeRing/DeckCollider").GetComponent<Collider>();
                var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
                var feet = new Vector3(ring.bounds.center.x, ring.bounds.max.y, ring.bounds.center.z);
                Call(mover, "Relocate", feet, Quaternion.identity);
                yield return null; yield return null;
                Assert.IsTrue((bool)T(RehearsalType).GetMethod("StartPractice").Invoke(null, new object[] { 0 }));
                yield return WaitStage(0, 10f);
                yield return new WaitForSecondsRealtime(.5f);
                Assert.IsTrue((bool)Static(RehearsalType, "IsPracticing"));
                Assert.IsFalse((bool)Static(RehearsalType, "UsesInitialTutorialRules"));
                Call(mover, "SetStartPose", feet, Quaternion.identity, true);
                Physics.SyncTransforms();
                Call(LocalPlayer, "CompleteFall", 10f, feet, null, null);
                Assert.AreEqual("Fallen", Get(LocalPlayer, "State").ToString(), "첫 공연 이후 재연습은 공통 치명 낙하 판정");
                var parts = Get(LocalPlayer, "LostParts");
                var ragdoll = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerRagdoll"));
                var body = (CharacterController)Get(mover, "Body");
                bool bodyEnabled = body.enabled;
                Assert.IsTrue((bool)Get(ragdoll, "IsLimp"));
                Assert.IsFalse((bool)Get(mover, "ControlEnabled"));

                Assert.IsTrue((bool)T(RehearsalType).GetMethod("EndPractice").Invoke(null, null));
                float until = Time.realtimeSinceStartup + 5f;
                while ((bool)Static(RehearsalType, "IsActive") && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsFalse((bool)Static(RehearsalType, "IsActive"));
                yield return null;
                Assert.AreEqual("Fallen", Get(LocalPlayer, "State").ToString());
                Assert.AreEqual(parts, Get(LocalPlayer, "LostParts"));
                Assert.IsFalse((bool)Get(mover, "ControlEnabled"), "연습 종료 배치가 사망자의 조작을 다시 켜면 안 된다.");
                Assert.AreEqual(bodyEnabled, body.enabled, "종료 배치 전 충돌체 활성 상태 유지");
                Assert.IsTrue((bool)Get(ragdoll, "IsLimp"), "공식 부활 전에는 래그돌 유지");
            }
            finally
            {
                if ((bool)Static(RehearsalType, "IsPracticing")) T(RehearsalType).GetMethod("EndPractice").Invoke(null, null);
                saved.SetValue(null, previousMastery);
            }
        }

        [UnityTest, Category("TutorialCommonFalls")]
        public IEnumerator TutorialCommonFalls_HighLandingOnActualRopeIsSafe()
        {
            FreshTutorial();
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return WaitStage(0, 10f);
                yield return new WaitForSecondsRealtime(.5f);
                var course = (Component)Priv(Rehearsal, "course");
                var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
                var feet = (Vector3)Call(course, "GetLanePosition", 0, 4f);
                Call(mover, "SetStartPose", feet, course.transform.rotation, true);
                Physics.SyncTransforms();
                Assert.IsTrue((bool)Invoke(course, "TryGetSupportingRopeAt", mover, feet, -1), "실제 줄 지지면 착지 fixture");
                Call(LocalPlayer, "CompleteFall", 20f, feet, null, null);
                Assert.AreEqual("Normal", Get(LocalPlayer, "State").ToString(), "외줄 위 정상 착지는 높이와 무관하게 생존");
                Assert.AreEqual(0, Convert.ToInt32(Get(LocalPlayer, "LostParts")));
                yield return null;
            }
            finally { EndTutorial(); }
        }

        [UnityTest, Category("TutorialCommonFalls")]
        public IEnumerator TutorialCommonFalls_LowerFloorKillsAndRestartsWithoutInjuryTwice()
        {
            FreshTutorial();
            try
            {
                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return WaitStage(0, 10f);
                yield return new WaitForSecondsRealtime(.5f);
                var course = (Component)Priv(Rehearsal, "course");
                var mover = LocalPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
                var room = (Transform)Priv(Rehearsal, "ringRoom");
                var deck = room.Find("LegacyRing/PracticeRing/DeckCollider").GetComponent<Collider>();
                var feet = new Vector3(deck.bounds.center.x, deck.bounds.max.y, deck.bounds.center.z);
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    Call(mover, "SetStartPose", feet, course.transform.rotation, true);
                    Physics.SyncTransforms();
                    Call(LocalPlayer, "CompleteFall", 1f, feet, null, null);
                    Assert.AreEqual("Fallen", Get(LocalPlayer, "State").ToString(), "작은 낙하도 튜토리얼 아래 바닥이면 재시도 사망");
                    yield return new WaitForSecondsRealtime(2.1f);
                    Assert.AreEqual("Normal", Get(LocalPlayer, "State").ToString());
                    Assert.AreEqual(0, Convert.ToInt32(Get(LocalPlayer, "LostParts")));
                    Assert.Less((float)Call(course, "GetDistance", LocalPlayer.transform.position), 0f);
                    Assert.AreEqual(0, Static(RehearsalType, "CurrentStage"));
                }
            }
            finally { EndTutorial(); }
        }
    }
}
