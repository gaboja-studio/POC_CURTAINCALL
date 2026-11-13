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
