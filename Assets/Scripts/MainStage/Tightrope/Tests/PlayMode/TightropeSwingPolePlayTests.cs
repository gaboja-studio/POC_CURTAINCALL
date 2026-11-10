using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.Tightrope.SystemIntegration.Tests
{
    public sealed partial class TightropeCampaignPlayTests
    {
        // 봉 점유·비행 검사는 TightropeCommonPolesPlayTests와 공통 HumanCannon 검사로 이동했다.
        [UnityTest]
        public IEnumerator SwingGapHasNoHiddenSupportAndSurvivesWholeCourseRestart()
        {
            yield return BeginPerformance(); DisableSwingHazards();
            Assert.IsTrue((bool)Get(course, "HasSwingGap"));
            float from = (float)Get(course, "SwingGapFrom"), to = (float)Get(course, "SwingGapTo");
            yield return StandOnRopeForSawTest(1, from - 1f);
            var middle = (Vector3)Call(course, "GetLanePosition", 1, (from + to) * .5f);
            Assert.IsFalse(Physics.Raycast(middle + Vector3.up * 3f, Vector3.down, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                "줄 메시만 숨기지 않고 실제 지지면도 제거한다.");
            Assert.IsFalse((bool)Call(course, "IsLaneUsable", 1, (from + to) * .5f));
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Get(bridge, "Result") == null && Time.realtimeSinceStartup < deadline)
            { Call(mover, "SetMoveInput", 1f); yield return null; }
            Assert.IsNotNull(Get(bridge, "Result"), $"봉 없이 걸어가면 줄 아래로 추락해 공연 결과가 나야 한다. pos={player.transform.position}");
            Assert.IsFalse((bool)Get(Get(bridge, "Result"), "Succeeded"), "혼자 추락하면 공용 공연 실패로 끝난다. 종료 후 캐릭터 Normal 복구와 구분한다.");
            int generation = (int)Get(run, "Round");
            var hud = UnityEngine.Object.FindAnyObjectByType(GameType("CurtainCall.Tightrope.UI.TightropeCampaignHud"));
            Assert.IsTrue((bool)Call(hud, "RestartCourseByHost"));
            yield return Until(() => (int)Get(run, "Round") > generation, 5f, "구간 초기화");
            Assert.IsFalse((bool)Call(course, "IsLaneUsable", 1, (from + to) * .5f), "화재를 초기화해도 구조적인 끊어진 줄은 복구되지 않는다.");
        }
        [UnityTest]
        public IEnumerator NativeRopeJumpIgnoresCannonAirInput()
        {
            yield return BeginPerformance(); DisableSwingHazards();
            yield return StandOnRopeForSawTest(2, 4f);
            var scheme = ScriptableObject.CreateInstance(GameType("CurtainCall.Player.TightropeControlScheme"));
            var context = Activator.CreateInstance(GameType("CurtainCall.Player.PlayerControlContext"), new object[] { player.gameObject });
            float before = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                Call(scheme, "Enter", context);
                Call(scheme, "Tick", context, CatchFrame(jump: true)); Call(mover, "Update");
                Assert.IsTrue((bool)Get(mover, "IsAirborne"));
                float side = (float)Call(course, "GetSide", player.transform.position);
                var input = CatchFrame(move: 1f); Set(input, "Posture", 1f);
                Call(scheme, "Tick", context, input); Call(mover, "Update");
                float changed = (float)Call(course, "GetSide", player.transform.position);
                Assert.That(changed, Is.EqualTo(side).Within(.002f), "일반 줄 위 점프에는 A/D 공중 조작이 적용되지 않는다.");
                Assert.That((float)Call(course, "GetDistance", player.transform.position), Is.EqualTo(4f).Within(.002f), "이륙 후 W 입력도 궤적을 바꾸지 않는다.");
                Call(scheme, "Tick", context, CatchFrame()); Call(mover, "Update");
                Assert.That((float)Call(course, "GetSide", player.transform.position), Is.EqualTo(changed).Within(.002f), "키를 놓으면 전 프레임 보정이 남지 않는다.");
                Assert.That((float)Get(mover, "JumpGravity"), Is.EqualTo((float)Get(Get(Static("CurtainCall.Settings.GameSettings", "Base"), "Character"), "Gravity")));
            }
            finally { Time.captureDeltaTime = before; Call(scheme, "Exit", context); UnityEngine.Object.Destroy(scheme); }
        }
        void DisableSwingHazards()
        {
            ((Behaviour)Static("CurtainCall.Tightrope.Saws.TightropeSaws", "Current")).enabled = false;
            ((Behaviour)Static("CurtainCall.Tightrope.Fire.TightropeFire", "Current")).enabled = false;
        }
    }
}
