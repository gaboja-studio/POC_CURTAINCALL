using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class SoloPracticePlayTests
    {
        [UnityTest]
        public IEnumerator FinalSaws_RestartIgnoresOldRemoteEntranceUntilExit_LocalCanStartImmediately()
        {
            yield return Enter(0);
            var saws = Current("HumanCannonFinalSaws");
            var entry = (BoxCollider)Get(saws, "entryZone");
            Vector3 inside = entry.bounds.center - Vector3.up * 0.2f;
            Vector3 outside = inside + Vector3.right * (entry.bounds.size.x + 10);
            const ulong remoteActor = ulong.MaxValue - 7;

            // 한 호스트에서 원격 위치 관찰값을 주입한다. 실제 멀티플레이 검증은 아니다.
            Call(saws, "ResetSaws");
            Call(saws, "RememberRemoteEntranceAtRestart", remoteActor, false, inside);
            for (int frame = 0; frame < 3; frame++)
            {
                Assert.IsFalse((bool)Call(saws, "TryStartFromEntrance", remoteActor, inside), "재시작 전 원격 위치는 새 진입이 아님");
                Assert.IsFalse((bool)Get(saws, "HasStarted"));
            }
            Assert.IsFalse((bool)Call(saws, "TryStartFromEntrance", remoteActor, outside), "영역 밖 관찰은 차단만 해제");
            Assert.IsFalse((bool)Get(saws, "HasStarted"));
            Assert.IsTrue((bool)Call(saws, "TryStartFromEntrance", remoteActor, inside), "실제 이탈 후 새 진입은 시작");
            Assert.IsTrue((bool)Get(saws, "HasStarted"));

            Call(saws, "ResetSaws");
            Call(saws, "RememberRemoteEntranceAtRestart", remoteActor, false, outside);
            Assert.IsTrue((bool)Call(saws, "TryStartFromEntrance", remoteActor, inside), "리셋 때 영역 밖이던 원격은 바로 진입 가능");

            Call(saws, "ResetSaws");
            ulong localActor = (ulong)Get(player, "OwnerClientId");
            Call(saws, "RememberRemoteEntranceAtRestart", localActor, true, inside);
            Assert.IsTrue((bool)Call(saws, "TryStartFromEntrance", localActor, inside), "로컬 재진입 연습은 차단하지 않음");

            Call(saws, "ResetSaws");
            Call(saws, "RememberRemoteEntranceAtRestart", remoteActor, false, inside);
            Call(saws, "ResetSaws");
            Assert.IsTrue((bool)Call(saws, "TryStartFromEntrance", remoteActor, inside), "공용 초기화 경로는 이전 차단도 정리");
        }

        [UnityTest]
        public IEnumerator FinalSaws_FirstEntranceStartsSharedClock_LaterLaunchKeepsItAndVerticalExitsBeforeWarning()
        {
            yield return Enter(0);
            Assert.IsFalse((bool)Get(Current("HumanCannonFinalSaws"), "HasStarted"), "최종 준비 구역 밖에서는 대기");
            yield return Enter(9);
            var saws = Current("HumanCannonFinalSaws");
            yield return Until(() => (bool)Get(saws, "HasStarted"), 3, "최종 진입 자동 시작");
            double started = (double)Get(saws, "StartedAt");
            Assert.AreEqual("Warning", Get(Get(saws, "Sample"), "Phase").ToString(), "첫 진입은 피해 전 예고");
            Assert.IsTrue((bool)Call(saws, "IsInEntrance", player.transform.position));
            Assert.IsFalse((bool)Get(saws, "HorizontalActive"));

            var cannon = Current("HumanCannonSystem");
            yield return BoardForCamera(cannon, 7);
            Assert.AreEqual(started, Get(saws, "StartedAt"), "뒤에 탑승한 사람 때문에 시작 시각이 바뀌지 않음");
            RequestCannon(cannon, 7, "Launch");
            var pilot = player.GetComponent(GameType(Prefix + "HumanCannonPilot"));
            Assert.IsTrue((bool)Get(pilot, "IsFlying"));
            Assert.AreEqual(started, Get(saws, "StartedAt"), "후발 발사도 공유 시각 유지");

            // 시각/표시 검사는 실제 피해 경로에서 떨어진 곳에서 진행한다. 저장된 씬 값은 바꾸지 않는다.
            var mover = player.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
            Call(mover, "SetStartPose", player.transform.position + Vector3.right * 40 + Vector3.up * 10, Quaternion.identity, true);
            Set(saws, "warningSeconds", 0.15f);
            Set(saws, "travelSeconds", 0.4f);
            var vertical = (Transform)Get(saws, "verticalView");
            var warning = (Transform)Get(saws, "warningView");
            yield return Until(() => (bool)Get(saws, "VerticalActive") && vertical.gameObject.activeSelf, 3, "수직 톱 실제 표시");
            long pass = (long)Get(Get(saws, "Sample"), "CycleIndex");
            Assert.IsFalse(warning.gameObject.activeSelf, "수직 톱 이동 중 예고 링은 꺼짐");
            yield return Until(() => Get(Get(saws, "Sample"), "Phase").ToString() == "Warning"
                && (long)Get(Get(saws, "Sample"), "CycleIndex") > pass && !vertical.gameObject.activeSelf
                && warning.gameObject.activeSelf, 3, "수직 톱 퇴장 후 다음 예고");
            Assert.IsFalse((bool)Get(saws, "VerticalActive"));
            Assert.IsTrue((bool)Get(saws, "HorizontalActive"), "가로 톱은 수직 톱 재예고 중에도 계속 진행");
            Assert.AreEqual(started, Get(saws, "StartedAt"));
            AssertBody(0);
        }

        [UnityTest]
        public IEnumerator FinalSaws_HorizontalContactCutsOnceUntilSeparated_InitialWarningHasNoDamage()
        {
            yield return Enter(9);
            var saws = Current("HumanCannonFinalSaws");
            yield return Until(() => (bool)Get(saws, "HasStarted"), 3, "최종 톱 시작");
            var mover = player.GetComponent(GameType("CurtainCall.Player.PlayerMover"));
            var simulated = mover.GetType().GetProperty("Simulated");
            bool wasSimulated = (bool)simulated.GetValue(mover);
            simulated.SetValue(mover, false);
            try
            {
                Set(saws, "warningSeconds", 1000f);
                Set(saws, "travelSeconds", 1000f);
                double now = (double)Get(saws, "Clock");
                Vector3 center = (Vector3)Call(saws, "HorizontalPosition", 0, now);
                Call(mover, "SetStartPose", center - Vector3.up * 0.45f, Quaternion.identity, true);
                Call(saws, "HostContacts", now);
                Call(saws, "HostContacts", now);
                AssertBody(0); // 실제 접촉 위치지만 초기 예고에서는 절단 요청이 나가지 않는다.

                Call(mover, "SetStartPose", center + Vector3.right * 10, Quaternion.identity, true);
                Call(saws, "HostContacts", now);
                Set(saws, "warningSeconds", 0.01f);
                yield return Until(() => (bool)Get(saws, "HorizontalActive"), 3, "가로 톱 활성화");
                now = (double)Get(saws, "Clock");
                center = (Vector3)Call(saws, "HorizontalPosition", 0, now);
                Vector3 touchingFeet = center - Vector3.up * 0.45f;
                Call(mover, "SetStartPose", touchingFeet, Quaternion.identity, true);
                Call(saws, "HostContacts", now);
                AssertBody(4); // 공통 순서 첫 접촉: 왼다리 하나.
                for (int frame = 0; frame < 5; frame++) Call(saws, "HostContacts", now);
                AssertBody(4); // 붙어 있는 동안 같은 톱이 매 프레임 추가 절단하지 않는다.

                // 순간이동 거리 > 4m: 이동 sweep가 이전 접촉을 다시 포함하지 않는 실제 이탈이다.
                Call(mover, "SetStartPose", touchingFeet + Vector3.right * 10, Quaternion.identity, true);
                Call(saws, "HostContacts", now);
                AssertBody(4);
                Call(mover, "SetStartPose", touchingFeet, Quaternion.identity, true);
                Call(saws, "HostContacts", now);
                AssertBody(12); // 다음 독립 접촉: 남은 다리 하나.
                Call(saws, "HostContacts", now);
                AssertBody(12);
            }
            finally { if (mover != null) simulated.SetValue(mover, wasSimulated); }
        }

        [UnityTest]
        public IEnumerator FinalSaws_SideCopyOwnsItsReferences_OnlyOneRunsAndRetryStartsFreshTimeline()
        {
            var source = Current("HumanCannonFinalSaws");
            var copy = In(template, "HumanCannonFinalSaws");
            Assert.IsNotNull(source, "원본 최종 톱 구간");
            Assert.IsNotNull(copy, "저장된 오른쪽 연습 최종 톱 구간");
            Assert.AreEqual(2, Get(source, "HorizontalCount"));
            Assert.AreEqual(Get(source, "HorizontalCount"), Get(copy, "HorizontalCount"));
            foreach (string field in new[] { "verticalStart", "verticalEnd" })
                AssertSideTransform((Transform)Get(source, field), (Transform)Get(copy, field), field);
            foreach (string field in new[] { "verticalView", "warningView" })
                AssertSideReference((Transform)Get(source, field), (Transform)Get(copy, field), field);
            foreach (string field in new[] { "horizontalOrigins", "horizontalViews" })
            {
                var sourcePoints = (Transform[])Get(source, field);
                var copyPoints = (Transform[])Get(copy, field);
                Assert.AreEqual(sourcePoints.Length, copyPoints.Length);
                for (int i = 0; i < sourcePoints.Length; i++)
                    if (field == "horizontalOrigins") AssertSideTransform(sourcePoints[i], copyPoints[i], field + i);
                    else AssertSideReference(sourcePoints[i], copyPoints[i], field + i); // 원본 뷰만 이미 LateUpdate로 움직였을 수 있다.
            }
            AssertSideTransform(((BoxCollider)Get(source, "entryZone")).transform,
                ((BoxCollider)Get(copy, "entryZone")).transform, "entryZone");

            yield return Enter(9);
            var first = Current("HumanCannonFinalSaws");
            yield return Until(() => (bool)Get(first, "HasStarted"), 3, "첫 연습 시작");
            AssertOnlyRuntimeSaws(first);
            Assert.IsFalse(((Behaviour)source).isActiveAndEnabled, "원본은 연습과 동시에 진행하지 않음");
            double firstStart = (double)Get(first, "StartedAt");
            yield return Enter(9);
            var retry = Current("HumanCannonFinalSaws");
            yield return Until(() => (bool)Get(retry, "HasStarted"), 3, "재시도 새 타임라인 시작");
            Assert.IsTrue(first == null, "이전 연습의 톱 컴포넌트 제거");
            Assert.That((double)Get(retry, "StartedAt"), Is.GreaterThan(firstStart), "재시도는 이전 진행 시각을 이어받지 않음");
            AssertOnlyRuntimeSaws(retry);

            yield return Enter(0);
            var dormant = Current("HumanCannonFinalSaws");
            Assert.IsTrue(retry == null);
            Assert.IsFalse((bool)Get(dormant, "HasStarted"), "최종 진입 전 구간의 새 복사본은 대기");
            AssertOnlyRuntimeSaws(dormant);
        }

        void AssertSideTransform(Transform source, Transform copy, string field)
        {
            AssertSideReference(source, copy, field);
            Assert.That(Vector3.Distance(source.position + Vector3.right * 50, copy.position), Is.LessThan(0.001f), field + " 오른쪽 50m 배치");
        }

        void AssertSideReference(Transform source, Transform copy, string field)
        {
            Assert.IsNotNull(source, field);
            Assert.IsNotNull(copy, field);
            Assert.AreNotSame(source, copy, field);
            Assert.IsTrue(copy.IsChildOf(template.transform), field + " 복사본 내부 참조");
        }

        void AssertOnlyRuntimeSaws(Component expected)
        {
            var all = Object.FindObjectsByType(GameType(Prefix + "HumanCannonFinalSaws"), FindObjectsSortMode.None);
            Assert.AreEqual(1, all.Length, "활성 코스의 최종 톱 시스템은 하나");
            Assert.AreSame(expected, all[0]);
            Assert.IsTrue(expected.transform.IsChildOf(Runtime.transform));
        }
    }
}
