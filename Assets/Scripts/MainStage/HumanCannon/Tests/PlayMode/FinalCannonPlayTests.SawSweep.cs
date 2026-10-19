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
        /// <summary>실제 1인 호스트의 접촉 판정에 합성 시각을 준다. 실제 지연·키보드 입력 재현 검사는 아니다.</summary>
        [UnityTest]
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

        [UnityTest]
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

        [UnityTest]
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
            var pole = (Component)Call(poles, "Pole", 6); // AddedPole0, x=0인 일반 봉
            anchor = Object.FindObjectsByType(GameType("CurtainCall.HumanCannon.HumanCannonPoleSaw"), FindObjectsSortMode.None)
                .Cast<Component>().Single(item => (Component)Get(item, "Pole") == pole);
            var saw = (Behaviour)Get(anchor, "Hazard");
            Assert.AreEqual("PoleSaw6", saw.name);
            Assert.IsTrue((bool)Call(anchor, "AlignToPole"));
            HazardSet(saw, "phase", 0f);
            Call(saw, "ConfigureSweep", saw.transform.InverseTransformVector(Vector3.right * 24f), 4f);
            saw.enabled = true;
            course = (Component)StaticGet("CurtainCall.HumanCannon.HumanCannonHazardCourse", "Current");
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
