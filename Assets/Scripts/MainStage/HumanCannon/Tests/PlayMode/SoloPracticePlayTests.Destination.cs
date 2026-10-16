using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.HumanCannon.Tests
{
    public sealed partial class SoloPracticePlayTests
    {
        FieldInfo destinationDevModeCache;
        object destinationPreviousDevMode;
        Component destinationCampaign, destinationRewards;

        [TearDown]
        public void RestoreDestinationTestDevMode()
        {
            destinationDevModeCache?.SetValue(null, destinationPreviousDevMode);
            destinationDevModeCache = null;
            destinationPreviousDevMode = null;
            destinationCampaign = destinationRewards = null;
        }

        [UnityTest]
        public IEnumerator Destination_PracticeLandingCompletesOnlyFinalPad_RetryClearsWithoutCommonSettlement()
        {
            Set(practice, "bodyPreset", 3);
            yield return Enter(9);
            var destination = Current("HumanCannonDestination");
            Assert.IsNotNull(destination);
            var rewards = Singleton("CurtainCall.Reward.PerformanceRewards");
            object latest = Get(rewards, "Latest");
            int historyCount = ResultHistoryCount(rewards);
            Set(Current("HumanCannonFinalSaws"), "warningSeconds", 1000f); // 도착 판정과 부상 보존만 분리해서 검증.
            var cannon = Current("HumanCannonSystem");
            yield return BoardForCamera(cannon, 6);
            Assert.IsFalse((bool)Get(destination, "PracticeCompleted"), "탑승은 목적지 도착이 아님");
            RequestCannon(cannon, 6, "Launch");
            var pilot = player.GetComponent(GameType(Prefix + "HumanCannonPilot"));
            Assert.IsTrue((bool)Get(pilot, "IsFlying"));
            Assert.IsFalse((bool)Get(destination, "PracticeCompleted"), "발사만으로 성공하지 않음");
            yield return Until(() => (bool)Get(destination, "PracticeCompleted"), 8, "실제 최종 비행 후 연습 도착 성공");
            Assert.IsFalse((bool)Get(pilot, "IsFlying"));
            Assert.IsTrue((bool)Call(destination, "HasArrived", player));
            Assert.IsFalse((bool)Get(destination, "PracticeFailed"));
            AssertBody(13);
            Assert.AreEqual("Waiting", Get(session, "GameState").ToString());
            Assert.AreSame(latest, Get(rewards, "Latest"), "연습 성공은 공용 결과를 만들지 않음");
            Assert.AreEqual(historyCount, ResultHistoryCount(rewards));

            var completed = destination;
            yield return Enter(9);
            destination = Current("HumanCannonDestination");
            Assert.IsTrue(completed == null, "재시도 시 이전 연습 도착 판정 제거");
            Assert.IsFalse((bool)Get(destination, "IsResolved"));
            yield return Enter(8);
            destination = Current("HumanCannonDestination");
            cannon = Current("HumanCannonSystem");
            yield return Until(() => (int)Get(cannon, "LocalSafeLandingCount") == 1, 5, "기존 쿠션 실제 안전 착지");
            yield return CameraFrames(3);
            Assert.IsFalse((bool)Call(destination, "HasArrived", player), "쿠션은 최종 목적지와 구분됨");
            Assert.IsFalse((bool)Get(destination, "IsResolved"), "쿠션 착지로 공연 성공을 내지 않음");
            AssertBody(13);
            Assert.AreSame(latest, Get(rewards, "Latest"));
            Assert.AreEqual(historyCount, ResultHistoryCount(rewards));
        }

        [UnityTest]
        public IEnumerator Destination_RealHumanCannonPerformanceSettlesOnceAtLanding_PreservesInjuryAndEntersNight()
        {
            yield return BeginFormalHumanCannonForDestination();
            var destination = Current("HumanCannonDestination");
            var cannon = Current("HumanCannonSystem");
            Set(Current("HumanCannonFinalSaws"), "warningSeconds", 1000f);
            int historyBefore = ResultHistoryCount(destinationRewards);
            int ledgerBefore = (int)Get(Get(destinationRewards, "State"), "LedgerCount");
            long balanceBefore = (long)Get(destinationRewards, "Balance");
            Call(player, "ServerCut", 1);
            AssertBody(4);
            yield return BoardForCamera(cannon, 6);
            RequestCannon(cannon, 6, "Launch");
            var pilot = player.GetComponent(GameType(Prefix + "HumanCannonPilot"));
            Assert.IsTrue((bool)Get(pilot, "IsFlying"));
            Assert.IsNull(Get(destination, "Result"));
            yield return Until(() => Get(destination, "Result") != null, 8, "실제 비행 후 공용 도착 정산");
            var result = Get(destination, "Result");
            Assert.IsTrue((bool)Get(result, "Succeeded"));
            Assert.AreEqual("Completed", Get(result, "EndReason"));
            Assert.AreEqual("HumanCannon", Get(Get(result, "Performance"), "TrickId"));
            Assert.AreEqual("Night", Get(Get(destinationCampaign, "State"), "Phase").ToString());
            Assert.IsFalse((bool)Get(destination, "PerformanceOpen"));
            Assert.IsFalse((bool)Get(pilot, "IsFlying"));
            AssertBody(4);
            var people = ((IEnumerable)Get(result, "Participants")).Cast<object>().ToArray();
            Assert.AreEqual(1, people.Length);
            Assert.AreEqual(Actor, Get(people[0], "ClientId"));
            Assert.AreEqual("Arrived", Get(people[0], "Judgement"));
            Assert.AreEqual(0, Get(people[0], "StartingLostCount"));
            Assert.AreEqual(1, Get(people[0], "EndingLostCount"));
            Assert.AreEqual(balanceBefore + (long)Get(result, "Income"), Get(destinationRewards, "Balance"));
            Assert.AreEqual(historyBefore + 1, ResultHistoryCount(destinationRewards));
            Assert.AreEqual(ledgerBefore + 1, Get(Get(destinationRewards, "State"), "LedgerCount"));

            long settledBalance = (long)Get(destinationRewards, "Balance");
            yield return CameraFrames(10); // 목적지 위에 계속 있어도 다시 지급하지 않는다.
            Assert.AreSame(result, Get(destination, "Result"), "공연 종료 후 결과 스냅샷 유지");
            Assert.AreEqual(historyBefore + 1, ResultHistoryCount(destinationRewards));
            Assert.AreEqual(ledgerBefore + 1, Get(Get(destinationRewards, "State"), "LedgerCount"));
            Assert.AreEqual(settledBalance, Get(destinationRewards, "Balance"));
            Assert.IsFalse((bool)Call(destinationRewards, "FinishPerformance", true, "Completed", null), "이미 정산한 공연 재종료 거절");
            AssertBody(4);

            // Campaign의 Night가 먼저 도착하고 다음 공연 보상 스냅샷이 늦는 수신 순서를 재현한다.
            Set(destination, "performanceId", "previous-human-cannon");
            Call(destination, "Update");
            Assert.AreEqual(Get(Get(result, "Performance"), "Id"), Get(destination, "performanceId"),
                "시간대가 이미 Night여도 신뢰된 새 공연 결과 ID를 채택해야 한다.");
            Assert.AreSame(result, Get(destination, "Result"));
            Assert.AreEqual(historyBefore + 1, ResultHistoryCount(destinationRewards));
            Assert.AreEqual(settledBalance, Get(destinationRewards, "Balance"));
        }

        [UnityTest]
        public IEnumerator Destination_RealLastSurvivorDeathUsesCommonFailureAndNightTransition()
        {
            yield return BeginFormalHumanCannonForDestination();
            var destination = Current("HumanCannonDestination");
            int historyBefore = ResultHistoryCount(destinationRewards);
            long balanceBefore = (long)Get(destinationRewards, "Balance");
            Assert.IsTrue((bool)Call(player, "ServerKill"), "실제 호스트 마지막 생존자 사망");
            yield return Until(() => Get(destination, "Result") != null, 5, "전멸 공용 자동 실패");
            var result = Get(destination, "Result");
            Assert.IsFalse((bool)Get(result, "Succeeded"));
            Assert.AreEqual("NoSurvivors", Get(result, "EndReason"));
            Assert.AreEqual("InsufficientSurvivors", Get(result, "Failure").ToString());
            Assert.AreEqual("HumanCannon", Get(Get(result, "Performance"), "TrickId"));
            Assert.AreEqual(0L, Get(result, "Income"));
            Assert.AreEqual(balanceBefore, Get(destinationRewards, "Balance"));
            Assert.AreEqual("Night", Get(Get(destinationCampaign, "State"), "Phase").ToString());
            Assert.IsFalse((bool)Get(destination, "PerformanceOpen"));
            Assert.IsFalse((bool)Get(destination, "PracticeCompleted"));
            Assert.IsFalse((bool)Get(destination, "PracticeFailed"), "정식 공연 실패와 연습 실패를 섞지 않음");
            yield return CameraFrames(5);
            Assert.AreSame(result, Get(destination, "Result"));
            Assert.AreEqual(historyBefore + 1, ResultHistoryCount(destinationRewards));
            Assert.AreEqual(balanceBefore, Get(destinationRewards, "Balance"));
        }

        IEnumerator BeginFormalHumanCannonForDestination()
        {
            yield return Enter(0); // 실제 LAN 호스트만 준비한다. 정식 공연은 원본 코스에서 시작한다.
            Call(practice, "ReturnToCourse");
            yield return Until(() => !(bool)Get(practice, "IsBusy"), 5, "정식 공연 원본 복귀");
            yield return Until(() => !(bool)StaticGet("CurtainCall.Player.InputGate", "IsBlocked"), 5, "정식 공연 입력 준비");
            destinationCampaign = Singleton("CurtainCall.Campaign.CampaignRun");
            destinationRewards = Singleton("CurtainCall.Reward.PerformanceRewards");
            var destination = Current("HumanCannonDestination");
            Assert.IsNotNull(destination);
            destinationDevModeCache = GameType("CurtainCall.Dev.DevMode").GetField("_enabled", BindingFlags.Static | BindingFlags.NonPublic);
            destinationPreviousDevMode = destinationDevModeCache.GetValue(null);
            // 최소 인원 설정/EditorPrefs는 변경하지 않는다. 실제 1인 StartGame 호출 동안만 임시 메모리 캐시 사용.
            destinationDevModeCache.SetValue(null, true);
            try { Assert.IsTrue((bool)Call(session, "StartGame")); }
            finally { destinationDevModeCache.SetValue(null, false); }
            Assert.IsFalse((bool)StaticGet("CurtainCall.Dev.DevMode", "Enabled"), "공연 판정은 정식 규칙");
            yield return Until(() => Get(Get(destinationCampaign, "State"), "Phase").ToString() == "Intro", 5, "실제 네트워크 런 도입");
            Assert.IsTrue((bool)Call(destinationCampaign, "EndIntro"));
            yield return CameraFrames(3);
            Assert.AreEqual("Tightrope", Get(Get(destinationCampaign, "State"), "TrickId"));
            Assert.IsFalse((bool)Get(destination, "PerformanceOpen"), "다른 묘기에서 인간 대포 명부를 열지 않음");
            Assert.IsFalse((bool)Get(Get(destinationRewards, "State"), "IsOpen"));
            Assert.IsNull(Get(Get(destinationRewards, "State"), "Performance"));

            Assert.IsTrue((bool)Call(destinationRewards, "BeginPerformance", 1, null));
            Assert.IsTrue((bool)Call(destinationRewards, "FinishPerformance", true, "TestPreparation", null));
            Assert.AreEqual("RestTime", Get(Get(destinationCampaign, "State"), "Phase").ToString());
            Assert.IsTrue((bool)Call(destinationCampaign, "NotifyAllAtStageEntrance"));
            Assert.AreEqual("HumanCannon", Get(Get(destinationCampaign, "State"), "TrickId"));
            yield return Until(() => (bool)Get(destination, "PerformanceOpen"), 5, "두 번째 인간 대포 공연 명부 자동 시작");
            Assert.IsNull(Get(destination, "Result"));
            Assert.AreEqual("HumanCannon", Get(Get(Get(destinationRewards, "State"), "Performance"), "TrickId"));
            var segments = ((IEnumerable)Get(Get(destinationRewards, "State"), "Segments")).Cast<object>().ToArray();
            Assert.AreEqual(1, segments.Length);
            Assert.AreEqual(Actor, Get(segments[0], "ClientId"));
            Assert.AreEqual("Playing", Get(session, "GameState").ToString());
        }

        static int ResultHistoryCount(Component rewards) => ((IEnumerable)Get(rewards, "History")).Cast<object>().Count();
    }
}
