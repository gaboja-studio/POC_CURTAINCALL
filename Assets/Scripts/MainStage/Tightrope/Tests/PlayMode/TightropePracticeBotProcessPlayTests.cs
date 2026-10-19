using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        [UnityTest, Category("LocalPracticeBotProcess")]
        public IEnumerator PracticeBotRealLanPeerMountsSwapsWalksResetsAndDisconnects()
        {
            if (!File.Exists(".harness/local/tightrope-bot/run-process-tests"))
                Assert.Ignore("로컬 봇 프로세스 검사를 요청한 실행에서만 사용합니다.");
            string executable = (string)Static("CurtainCall.Tightrope.TightropePracticeBot", "ExecutablePath");
            if (!File.Exists(executable)) Assert.Ignore("외줄 연습 봇 빌드가 없습니다: " + executable);
            var panelObject = new GameObject("Explicit practice bot process test");
            var panel = panelObject.AddComponent(GameType("CurtainCall.Tightrope.TightropePracticeBot"));
            var roles = GameType("CurtainCall.Tightrope.TightropeBotRole");
            var stack = (Component)Static("CurtainCall.Tightrope.PiggybackSystem", "Current");
            Component bot = null;
            try
            {
                var start = (Task<bool>)Call(panel, "StartBotAsync");
                yield return Until(() => start.IsCompleted, 15f, "별도 봇 실행 요청");
                Assert.IsTrue(start.GetAwaiter().GetResult(), (string)Get(panel, "Status"));
                yield return Until(() => (bool)Get(panel, "Ready") && (int)Get(session, "PlayerCount") == 2, 45f,
                    "실제 두 번째 LAN 클라이언트 접속");
                ulong botId = (ulong)Get(panel, "BotId");
                yield return Until(() => (bot = FindPracticeProcessBot(botId)) != null, 5f, "호스트에 봇 플레이어 복제");
                Assert.IsFalse((bool)Get(bot, "IsLocal"));
                Assert.AreSame(player, Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local"));
                yield return BeginPerformance();
                // 이 검사는 소유권·입력·목마·R 수명 검증이다. 장애물은 별도 실제 회피 검사에서 확인한다.
                ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
                ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
                yield return StandOnRopeForSawTest(1, 2f);
                Call(panel, "SetRole", Enum.Parse(roles, "Upper"));
                yield return Until(() => Call(stack, "GetBelow", bot) == player, 15f, "봇 실제 F 입력으로 사람 위 탑승");
                Assert.That(Get(bot, "State").ToString(), Is.EqualTo("Normal"));
                int round = (int)Get(run, "Round");

                Call(panel, "SetRole", Enum.Parse(roles, "Lower"));
                yield return Until(() => Get(panel, "Role").ToString() == "Lower"
                    && Call(stack, "GetBelow", bot) == null && !(bool)Get(bot, "IsAirborne"), 10f,
                    "봇 실제 F 길게 하차 후 아래 역할 교대");
                Assert.That((int)Get(run, "Round"), Is.EqualTo(round), "역할 교대는 코스 리셋이 아니다.");
                var interaction = player.GetComponent(GameType("CurtainCall.Player.PlayerInteraction"));
                Call(interaction, "RequestInteract");
                yield return Until(() => Call(stack, "GetBelow", player) == bot, 5f, "사람 F 요청으로 봇 위 탑승");
                float beforeWalk = (float)Call(course, "GetDistance", bot.transform.position);
                yield return WaitForPracticeBot(() => (float)Call(course, "GetDistance", bot.transform.position) > beforeWalk + 1f,
                    5f, "실제 클라이언트 아래층 봇이 목마를 태우고 전진", panel, bot);
                Assert.AreSame(bot, Call(stack, "GetBelow", player));

                var hud = Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
                Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
                yield return Until(() => (int)Get(run, "Round") > round && Call(stack, "GetBelow", player) == null
                    && Call(stack, "GetBelow", bot) == null, 8f, "R 전체 리셋이 두 소유자의 목마를 해제");
                Assert.That(Get(panel, "Role").ToString(), Is.EqualTo("Lower"));
                yield return WaitForPracticeBot(() => Get(bot, "State").ToString() == "Normal"
                    && Vector3.ProjectOnPlane(bot.transform.position - player.transform.position, Vector3.up).magnitude < 1.4f,
                    15f, "R 뒤 봇이 출발점에서 선택 역할로 다시 따라오기", panel, bot);
                Assert.IsFalse(((Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerController"))).enabled,
                    "worker 입력은 호스트의 조작 부품을 변경하지 않는다.");
                Assert.IsFalse(((Behaviour)player.GetComponent(GameType("CurtainCall.Player.PlayerInputReader"))).enabled);

                Call(panel, "StopBot");
                yield return Until(() => (int)Get(session, "PlayerCount") == 1 && !(bool)Get(panel, "Running")
                    && !(bool)Get(panel, "Busy"), 8f, "봇만 접속 종료하고 자식 프로세스 정리");
                Assert.IsTrue((bool)Get(session, "IsHost"));
                Assert.IsTrue((bool)Get(player, "IsSpawned"));
            }
            finally
            {
                Call(panel, "StopBot");
                Object.Destroy(panelObject);
            }
        }

        static Component FindPracticeProcessBot(ulong ownerId)
        {
            foreach (Component candidate in (IEnumerable)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "All"))
                if (candidate != null && (ulong)Get(candidate, "OwnerClientId") == ownerId) return candidate;
            return null;
        }

        IEnumerator WaitForPracticeBot(Func<bool> condition, float seconds, string reason, Component panel, Component bot)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(condition(), $"{reason} 시간 초과: status={Get(panel, "Status")}, role={Get(panel, "Role")}, "
                + $"botPosition={(bot != null ? bot.transform.position.ToString() : "missing")}, botState={(bot != null ? Get(bot, "State") : "missing")}, "
                + $"botAirborne={(bot != null ? Get(bot, "IsAirborne") : "missing")}, hostPosition={(player != null ? player.transform.position.ToString() : "missing")}");
        }
    }
}
