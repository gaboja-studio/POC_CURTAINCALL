using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class FinalCannonPlayTests
    {
        [UnityTest]
        public IEnumerator OriginalCourse_DifficultyChangesBeforeLaunch_ThenLocks()
        {
            var hazards = HazardCourse();
            Assert.AreEqual(10, Get(StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current"), "PoleCount"));
            Assert.IsFalse((bool)Call(hazards, "SetDifficulty", 0));
            Assert.IsFalse((bool)Call(hazards, "SetDifficulty", 4));
            foreach (int level in new[] { 2, 3, 1 })
            {
                Assert.IsTrue((bool)Call(hazards, "SetDifficulty", level));
                Assert.AreEqual(level, Get(hazards, "Difficulty"));
            }
            yield return Board(6);
            yield return ReleaseThenArm();
            Input(tapped: true);
            Assert.IsTrue(Flying);
            Assert.IsFalse((bool)Get(hazards, "CanChangeDifficulty"));
            Assert.IsFalse((bool)Call(hazards, "SetDifficulty", 3));
            Assert.AreEqual(1, Get(hazards, "Difficulty"));
            yield return WaitUntil(() => (double)Get(hazards, "RunSeconds") > 0, 2, "첫 발사 후 공유 시계 진행");
        }

        [UnityTest]
        public IEnumerator OriginalCourse_FormalPerformanceUsesSelectedDifficulty_AndLocksBeforeLaunch()
        {
            var hazards = HazardCourse();
            var destination = StaticGet("CurtainCall.HumanCannon.HumanCannonDestination", "Current");
            Assert.IsTrue((bool)Call(hazards, "SetDifficulty", 3));
            var devCache = GameType("CurtainCall.Dev.DevMode").GetField("_enabled", BindingFlags.Static | BindingFlags.NonPublic);
            object previousDevMode = devCache.GetValue(null);
            try
            {
                // 기존 실제 LAN 테스트 방식: StartGame 호출에만 인원 우회용 메모리 캐시를 쓴다.
                devCache.SetValue(null, true);
                Assert.IsTrue((bool)Call(session, "StartGame"));
                devCache.SetValue(null, false);
                yield return WaitUntil(() => Get(Get(campaign, "State"), "Phase").ToString() == "Intro", 5, "공용 런 시작");
                Assert.IsTrue((bool)Call(campaign, "EndIntro"));
                yield return null;
                Assert.AreEqual("Tightrope", Get(Get(campaign, "State"), "TrickId"));
                Assert.IsTrue((bool)Call(rewards, "BeginPerformance", 1, null));
                Assert.IsTrue((bool)Call(rewards, "FinishPerformance", true, "TestPreparation", null));
                Assert.IsTrue((bool)Call(campaign, "NotifyAllAtStageEntrance"));
                yield return WaitUntil(() => (bool)Get(destination, "PerformanceOpen"), 5, "인간 대포 공용 명부 열림");
                Assert.AreEqual("HumanCannon", Get(Get(Get(rewards, "State"), "Performance"), "TrickId"));
                Assert.AreEqual(3, Get(rewards, "difficulty"), "목적지가 선택한 난도를 공용 정산 시작에 전달한다.");
                Assert.IsFalse(Flying);
                Assert.AreEqual(0d, Get(hazards, "RunSeconds"));
                Assert.IsFalse((bool)Get(hazards, "CanChangeDifficulty"), "첫 발사 전이어도 명부가 열리면 난도 고정");
                Assert.IsFalse((bool)Call(hazards, "SetDifficulty", 1));
                Assert.AreEqual(3, Get(hazards, "Difficulty"));
                Assert.AreEqual(3, Get(rewards, "difficulty"));
            }
            finally { devCache.SetValue(null, previousDevMode); }
        }

        [UnityTest]
        public IEnumerator OriginalCourse_AllDifficultiesHaveNoWindOrGroundDrift()
        {
            var hazards = HazardCourse();
            foreach (Behaviour hazard in ((IEnumerable)Get(hazards, "hazards")).Cast<Behaviour>()) hazard.enabled = false;
            Vector3 feet = new Vector3(0, 0.01f, 57);
            for (int level = 1; level <= 3; level++)
            {
                Assert.IsTrue((bool)Call(hazards, "SetDifficulty", level));
                Call(mover, "SetStartPose", feet, Quaternion.identity, true);
                yield return null;
                yield return new WaitForEndOfFrame();
                float height = (float)Get(mover, "BodyHeight");
                var groundWindVelocity = (Vector3)Call(hazards, "WindVelocity", player.transform.position, height, false);
                var airWindVelocity = (Vector3)Call(hazards, "WindVelocity", feet + Vector3.up * 3, height, true);
                Assert.That(groundWindVelocity, Is.EqualTo(Vector3.zero), "난도와 관계없이 지상 강풍이 없어야 한다: " + level);
                Assert.That(airWindVelocity, Is.EqualTo(Vector3.zero), "난도와 관계없이 공중 강풍이 없어야 한다: " + level);
                Assert.IsTrue((bool)Get(mover, "Simulated"), "실제 이동 처리를 켠 상태에서 검사한다.");
                Assert.IsFalse((bool)Get(mover, "IsAirborne"), "이전 송풍 구간의 바닥에서 시작한다.");
                Vector3 before = player.transform.position;
                for (int frame = 0; frame < 12; frame++)
                    yield return new WaitForEndOfFrame();
                Vector3 displacement = player.transform.position - before;
                Assert.That(Vector3.ProjectOnPlane(displacement, Vector3.up).magnitude,
                    Is.LessThan(0.001f), "입력 없이 이전 송풍 구간에 서 있어도 수평으로 밀리지 않는다: " + level);
                Assert.That(Mathf.Abs(displacement.y), Is.LessThan(0.03f), "서 있는 바닥 높이를 유지한다.");
                Assert.IsFalse((bool)Get(mover, "IsAirborne"), "검사 중에도 바닥 접촉 유지");
            }
        }

        [UnityTest]
        public IEnumerator OriginalCourse_GeneralHazardWaitsForRestartPose_CutsOncePerContact()
        {
            var hazards = HazardCourse();
            var all = ((IEnumerable)Get(hazards, "hazards")).Cast<Component>().ToArray();
            var saw = all.First(item => Get(item, "Kind").ToString() == "GroundSaw");
            foreach (var other in all) ((Behaviour)other).enabled = other == saw;
            foreach (Behaviour zone in ((IEnumerable)Get(hazards, "windZones")).Cast<Behaviour>()) zone.enabled = false;
            HazardSet(saw, "travel", Vector3.zero);
            HazardSet(mover, "Simulated", false);
            Vector3 spawn = player.transform.position;
            Vector3 contact = saw.transform.position - Vector3.up * 0.4f;
            var pending = (IDictionary)Get(hazards, "restartPositions");
            pending[Actor] = spawn; // 원격 재시작의 옛 위치 대기 조건만 재현한다. 다중 클라이언트 검사는 아니다.
            Call(mover, "SetStartPose", contact, Quaternion.identity, true);
            yield return new WaitForSeconds(0.15f);
            AssertBody(0);
            Assert.IsTrue(pending.Contains(Actor), "옛 함정 위치에서 새 시작 위치를 추정하지 않는다.");
            Call(mover, "SetStartPose", spawn, Quaternion.identity, true);
            yield return WaitUntil(() => !pending.Contains(Actor), 2, "실제 시작 위치 확인");
            Call(mover, "SetStartPose", contact, Quaternion.identity, true);
            yield return WaitUntil(() => Convert.ToInt32(Get(player, "LostParts")) == 4, 2, "첫 실제 접촉 왼다리 절단");
            yield return new WaitForSeconds(0.2f);
            AssertBody(4);
            Call(mover, "SetStartPose", contact + Vector3.right * 8, Quaternion.identity, true);
            yield return null;
            yield return null;
            Call(mover, "SetStartPose", contact, Quaternion.identity, true);
            yield return WaitUntil(() => Convert.ToInt32(Get(player, "LostParts")) == 12, 2, "벗어난 뒤 재접촉 오른다리 절단");
            yield return new WaitForSeconds(0.2f);
            AssertBody(12);
        }

        static Component HazardCourse()
        {
            var course = StaticGet("CurtainCall.HumanCannon.HumanCannonHazardCourse", "Current") as Component;
            Assert.IsNotNull(course, "044 일반 함정이 기존 코스에 저장되어 있어야 한다.");
            Assert.IsTrue((bool)Get(course, "IsSynchronized"));
            return course;
        }

        static void HazardSet(object target, string name, object value)
        {
            var property = target.GetType().GetProperty(name, InstanceMembers);
            if (property != null) property.SetValue(target, value);
            else target.GetType().GetField(name, InstanceMembers).SetValue(target, value);
        }
    }
}
