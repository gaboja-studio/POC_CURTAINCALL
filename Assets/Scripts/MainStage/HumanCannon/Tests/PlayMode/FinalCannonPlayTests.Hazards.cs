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
            Assert.AreEqual(6, Get(StaticGet("CurtainCall.HumanCannon.HumanCannonPoleSystem", "Current"), "PoleCount"));
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
        public IEnumerator OriginalCourse_WindPresetsMoveGroundBodyHorizontally()
        {
            var hazards = HazardCourse();
            foreach (Behaviour hazard in ((IEnumerable)Get(hazards, "hazards")).Cast<Behaviour>()) hazard.enabled = false;
            var zones = ((IEnumerable)Get(hazards, "windZones")).Cast<Component>().ToArray();
            var zone = zones.First(item => (bool)Get(item, "affectsGround"));
            foreach (var other in zones) ((Behaviour)other).enabled = other == zone;
            var area = (BoxCollider)Get(zone, "Area");
            var ray = new Ray(new Vector3(area.bounds.center.x, area.bounds.max.y + 1, area.bounds.center.z), Vector3.down);
            Assert.IsTrue(Physics.Raycast(ray, out var support, 30, ~0, QueryTriggerInteraction.Ignore), "실제 송풍 구간 바닥");
            Vector3 feet = support.point + Vector3.up * 0.01f;
            Vector3 direction = (Vector3)Get(zone, "Direction");
            Assert.That(direction.sqrMagnitude, Is.EqualTo(1).Within(0.001f));
            for (int level = 1; level <= 3; level++)
            {
                Assert.IsTrue((bool)Call(hazards, "SetDifficulty", level));
                Call(mover, "SetStartPose", feet, Quaternion.identity, true);
                yield return null;
                yield return new WaitForEndOfFrame();
                float speed = 0.6f + (level - 1) * 0.2f;
                var velocity = (Vector3)Call(hazards, "WindVelocity", player.transform.position, (float)Get(mover, "BodyHeight"), false);
                Assert.That(Vector3.Distance(velocity, direction * speed), Is.LessThan(0.001f));
                Vector3 before = player.transform.position;
                float elapsed = 0;
                for (int frame = 0; frame < 12; frame++)
                {
                    yield return new WaitForEndOfFrame();
                    elapsed += Time.deltaTime;
                }
                Vector3 displacement = player.transform.position - before;
                Assert.That(Vector3.Distance(Vector3.ProjectOnPlane(displacement, Vector3.up), direction * speed * elapsed),
                    Is.LessThan(0.025f), "Pilot의 실제 LateUpdate가 난도별 바람을 한 번씩 적용한다.");
                Assert.That(Mathf.Abs(displacement.y), Is.LessThan(0.03f), "바람은 수직 속도를 더하지 않는다.");
                Assert.IsFalse((bool)Get(mover, "IsAirborne"), "추가 이동 뒤에도 바닥 접촉 유지");
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
