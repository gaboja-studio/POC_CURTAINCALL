using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        [UnityTest]
        public IEnumerator HazardHostCannotCutRemoteOwnerWithoutDisplayedContactReport()
        {
            DisableEnhancementThreats();
            var course = (Component)StaticGet("CurtainCall.HumanCannon.HumanCannonHazardCourse", "Current");
            var fixture = new GameObject("RemoteContactHazardFixture"); fixture.transform.SetParent(course.transform);
            fixture.transform.position = player.transform.position + Vector3.up * ((float)Get(mover, "BodyHeight") * .5f);
            var hazard = fixture.AddComponent(GameType("CurtainCall.HumanCannon.HumanCannonHazard"));
            Call(hazard, "ConfigureSweep", Vector3.zero, 4f);
            var hazards = System.Array.CreateInstance(hazard.GetType(), 1); hazards.SetValue(hazard, 0); HazardSet(course, "hazards", hazards);
            var prefab = (GameObject)Get(Get(network, "NetworkConfig"), "PlayerPrefab");
            var remote = Object.Instantiate(prefab);
            var netObject = remote.GetComponent(GameType("Unity.Netcode.NetworkObject"));
            try {
                Call(netObject, "SpawnWithOwnership", 777UL, true);
                var remotePlayer = remote.GetComponent(GameType("CurtainCall.Network.PlayerSync.NetworkPlayer"));
                var remoteMover = remote.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
                Call(remoteMover, "SetStartPose", player.transform.position, Quaternion.identity, true);
                Call(mover, "SetStartPose", player.transform.position + Vector3.right * 10, Quaternion.identity, true);
                Call(course, "HostContacts", (double)Get(course, "Elapsed"), 1f);
                Assert.AreEqual(0, System.Convert.ToInt32(Get(remotePlayer, "LostParts")));
            } finally { Call(netObject, "Despawn", true); }
            yield return null;
        }
        [UnityTest]
        public IEnumerator HazardOwnerReportUsesDisplayedTimeRejectsAvoidanceAndDoesNotCutTwice()
        {
            DisableEnhancementThreats();
            var course = (Component)StaticGet("CurtainCall.HumanCannon.HumanCannonHazardCourse", "Current");
            var fixture = new GameObject("OwnerContactHazardFixture"); fixture.transform.SetParent(course.transform);
            fixture.transform.position = player.transform.position + Vector3.up * ((float)Get(mover, "BodyHeight") * .5f);
            var saw = fixture.AddComponent(GameType("CurtainCall.HumanCannon.HumanCannonHazard"));
            Call(saw, "ConfigureSweep", Vector3.right * 12f, 4f);
            var hazards = System.Array.CreateInstance(saw.GetType(), 1); hazards.SetValue(saw, 0);
            HazardSet(course, "hazards", hazards);
            int index = 0;
            double at = (double)Get(course, "Elapsed");
            HazardSet(saw, "phase", (float)(-at * Mathf.PI * 2 / 4));
            var center = (Vector3)Call(saw, "Position", at, 1f);
            var contact = center - Vector3.up * ((float)Get(mover, "BodyHeight") * .5f);
            Call(mover, "SetStartPose", contact, Quaternion.identity, true);
            int restart = (int)Get(player, "RestartVersion"); double epoch = (double)Get(course, "epoch");
            Assert.IsFalse((bool)Call(course, "ValidateOwnerContact", Actor, index, true, 1u, restart, epoch, at, contact + Vector3.up * 100));
            AssertBody(0);
            Call(mover, "SetStartPose", contact + Vector3.forward * 5f, Quaternion.identity, true);
            yield return new WaitForSeconds(.3f);
            Assert.IsFalse((bool)Call(saw, "Touches", contact, (float)Get(mover, "BodyHeight"), (float)Get(mover, "BodyRadius"), (double)Get(course, "Elapsed"), 1f), "The moving hazard has already passed the contact point by receipt.");
            Assert.IsTrue((bool)Call(course, "ValidateOwnerContact", Actor, index, true, 2u, restart, epoch, at, contact));
            AssertBody(4);
            Assert.IsFalse((bool)Call(course, "ValidateOwnerContact", Actor, index, true, 2u, restart, epoch, at, contact));
            Assert.IsTrue((bool)Call(course, "ValidateOwnerContact", Actor, index, true, 3u, restart, epoch, at, contact));
            AssertBody(4);
            Assert.IsFalse((bool)Call(course, "ValidateOwnerContact", Actor, index, true, 4u, restart - 1, epoch, at, contact));
            Assert.IsFalse((bool)Call(course, "ValidateOwnerContact", Actor, index, true, 4u, restart, epoch - 1, at, contact));
            Assert.IsTrue((bool)Call(course, "ValidateOwnerContact", Actor, index, false, 4u, restart, epoch, at, contact));
        }
        [UnityTest, Category("PerformanceBalance")]
        public IEnumerator FastSaw_FixedLevelOneHidesUnusedSawDamage_AndKeepsVisibleSawDamage()
        {
            DisableEnhancementThreats();
            var course = (Component)StaticGet("CurtainCall.HumanCannon.HumanCannonHazardCourse", "Current");
            Assert.That((int)Get(course, "Difficulty"), Is.EqualTo(1));
            Component hidden = null;
            Component visible = null;
            for (int i = 0; i < (int)Get(course, "HazardCount"); i++)
            {
                var hazard = (Component)Call(course, "Hazard", i);
                if (Get(hazard, "Kind").ToString() != "GroundSaw" || (bool)Get(hazard, "IsPoleSaw")) continue;
                if ((bool)Call(course, "IsHazardEnabled", i)) visible ??= hazard;
                else hidden ??= hazard;
            }
            Assert.IsNotNull(hidden);
            Assert.IsNotNull(visible);
            ((Behaviour)hidden).enabled = true;
            Call(course, "LateUpdate");
            Assert.IsFalse(((Transform)Get(hidden, "visual")).gameObject.activeSelf);
            double elapsed = (double)Get(course, "Elapsed");
            float height = (float)Get(mover, "BodyHeight");
            var center = (Vector3)Call(hidden, "Position", elapsed, 1f);
            Call(mover, "SetStartPose", center - Vector3.up * (height * .5f), Quaternion.identity, true);
            Call(course, "ClearContacts");
            Call(course, "HostContacts", elapsed, 1f);
            AssertBody(0);

            ((Behaviour)hidden).enabled = false;
            ((Behaviour)visible).enabled = true;
            Assert.IsTrue(((Transform)Get(visible, "visual")).gameObject.activeSelf);
            elapsed = (double)Get(course, "Elapsed");
            center = (Vector3)Call(visible, "Position", elapsed, 1f);
            Call(mover, "SetStartPose", center - Vector3.up * (height * .5f), Quaternion.identity, true);
            Call(course, "ClearContacts");
            Call(course, "HostContacts", elapsed, 1f);
            AssertBody(4);
            yield break;
        }

        /// <summary>실제 1인 호스트의 접촉 판정에 합성 시각을 준다. 실제 지연·키보드 입력 재현 검사는 아니다.</summary>
        [UnityTest, Category("PerformanceBalance")]
        public IEnumerator FastSaw_HalfSecondGapStillCutsBetweenClearEndpoints_WithoutDuplicateCut()
        {
            var saw = PrepareSweepSaw(out var course, out var anchor);
            Vector3 feet = SweepRestFeet(anchor);
            PrepareSweepBody(course, feet, feet, 1.75);
            AssertSweepEndpointsClear(saw, feet, 1.75, 2.25);
            Call(course, "HostContacts", 2.25, 1f);
            AssertBody(4);
            Call(course, "HostContacts", 2.25, 1f);
            AssertBody(4); // 같은 접촉 구간을 반복 처리해도 두 번째 부위를 자르지 않는다.
            yield break;
        }

        [UnityTest, Category("PerformanceBalance")]
        public IEnumerator FastSaw_LongGapChecksRecentHalfSecond_AndCutsStationaryBody()
        {
            var saw = PrepareSweepSaw(out var course, out var anchor);
            Vector3 feet = SweepRestFeet(anchor);
            PrepareSweepBody(course, feet, feet, 0.25);
            AssertSweepEndpointsClear(saw, feet, 1.75, 2.25);
            Call(course, "HostContacts", 2.25, 1f);
            AssertBody(4);
            yield break;
        }

        [UnityTest, Category("PerformanceBalance")]
        public IEnumerator FastSaw_LongGapClipsBodyPathToSameTimeWindow_WithoutInventingLateCrossing()
        {
            PrepareSweepSaw(out var course, out var anchor);
            Vector3 center = SweepRestFeet(anchor);
            // 0.25→2.25 동안 3m 전진했다. 최근 0.5초에는 톱 중심 앞 0.75→1.5m에 있다.
            // 몸 경로를 자르지 않으면 과거의 중앙 통과를 t=2.0으로 밀어 잘못 절단한다.
            Vector3 before = center + Vector3.back * 1.5f;
            Vector3 after = center + Vector3.forward * 1.5f;
            PrepareSweepBody(course, before, after, 0.25);
            Call(course, "HostContacts", 2.25, 1f);
            AssertBody(0);
            yield break;
        }

        Component PrepareSweepSaw(out Component course, out Component anchor)
        {
            DisableEnhancementThreats();
            var poles = StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current");
            ShowHiddenPole(poles, 1);
            var pole = (Component)Call(poles, "Pole", 1);
            // Authored pole saws were removed from the current course. Keep the sweep
            // regression independent of that layout without restoring obsolete scene objects.
            var fixture = new GameObject("SweepSawRegressionFixture");
            var saw = (Behaviour)fixture.AddComponent(GameType("CurtainCall.HumanCannon.HumanCannonHazard"));
            HazardSet(saw, "kind", System.Enum.Parse(GameType("CurtainCall.HumanCannon.CannonHazardKind"), "GroundSaw"));
            anchor = fixture.AddComponent(GameType("CurtainCall.HumanCannon.HumanCannonPoleSaw"));
            Call(anchor, "Configure", pole, saw, 0f);
            Assert.IsTrue((bool)Call(anchor, "AlignToPole"));
            HazardSet(saw, "phase", 0f);
            Call(saw, "ConfigureSweep", saw.transform.InverseTransformVector(Vector3.right * 24f), 4f);
            saw.enabled = true;
            course = (Component)StaticGet("CurtainCall.HumanCannon.HumanCannonHazardCourse", "Current");
            var original = (System.Array)Get(course, "hazards");
            var hazards = System.Array.CreateInstance(saw.GetType(), original.Length + 1);
            System.Array.Copy(original, hazards, original.Length); hazards.SetValue(saw, original.Length);
            HazardSet(course, "hazards", hazards);
            Call(course, "ClearContacts");
            return saw;
        }

        static Vector3 SweepRestFeet(Component anchor)
        {
            Vector3 rail = (Vector3)Get(anchor, "RailCenter");
            return rail - Vector3.up * (float)Get(anchor, "BodyCenterHeight");
        }

        void PrepareSweepBody(Component course, Vector3 before, Vector3 feet, double beforeTime)
        {
            Call(mover, "SetStartPose", feet, Quaternion.identity, true);
            course.GetType().GetField("previousElapsed", InstanceMembers).SetValue(course, beforeTime);
            var positions = (IDictionary)Get(course, "previousFeet");
            positions[Actor] = before;
            AssertBody(0);
        }

        void AssertSweepEndpointsClear(Component saw, Vector3 feet, double before, double after)
        {
            float height = (float)Get(mover, "BodyHeight"), radius = (float)Get(mover, "BodyRadius");
            Assert.IsFalse((bool)Call(saw, "Touches", feet, height, radius, before, 1f));
            Assert.IsFalse((bool)Call(saw, "Touches", feet, height, radius, after, 1f));
        }
    }
}
