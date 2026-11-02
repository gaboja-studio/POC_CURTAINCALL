using System.Globalization;

namespace CurtainCall.WaitingRoom
{
    /// <summary>리허설 소구간에서 가르치는 동작. 퀘스트 문구와 체크포인트 판정이 종류마다 다르다.</summary>
    public enum RehearsalStageKind : byte
    {
        /// <summary>1단계: 균형 잡기·Q/E+Space로 옆줄 건너가기.</summary>
        LaneChange,
        /// <summary>2-1·2-2: 끊긴 줄을 인간 그네(회전봉)로 건넌다. F 잡기 · W/S 반동 · Space 놓기.</summary>
        Swing,
        /// <summary>2-3·3단계: 트리거 그네(해제봉). 한 명이 잡고 있는 동안 동료의 줄이 이어진다.</summary>
        Relay,
    }

    /// <summary>소구간 완료 인원(기획서 3장: 1단계 전원, 2·3단계 마지막 도착대 1명 이상).</summary>
    public enum RehearsalClearRule : byte { Everyone, Anyone }

    /// <summary>
    /// 최초 리허설 소구간 하나(Task-097, 기획서 5~7장). 코스는 기존 외줄 코스 생성 모드(<see cref="Tightrope.TightropeCourse"/>) 값으로 만든다.
    /// 2-2 "연속 인간 그네 2개"는 현재 그네 시스템이 코스당 끊김 1개만 지원해 더 긴 끊김 하나로 둔다(후속 보완).
    /// </summary>
    public sealed class RehearsalStage
    {
        public string Id, Title, Intro;
        public RehearsalStageKind Kind;
        public RehearsalClearRule Rule;
        /// <summary>외줄 코스 모양(<c>TightropeSettings.CourseSettings</c> JSON). 빠진 값은 공통 세팅 기본값.</summary>
        public string CourseJson;
        /// <summary>도착 거리(m). 코스를 링 가운데에 놓을 때 쓴다.</summary>
        public float Finish;
        /// <summary>1단계: 이 거리부터 옆줄 건너가기 안내. 그네·봉 단계는 코스 값에서 읽는다.</summary>
        public float CheckpointDistance;

        public static readonly RehearsalStage[] All =
        {
            new()
            {
                Id = "1", Kind = RehearsalStageKind.LaneChange, Rule = RehearsalClearRule.Everyone, Finish = 26f, CheckpointDistance = 8f,
                Title = "1단계 · 외줄타기",
                Intro = "줄 위에서는 A/D로 균형을 잡고 W로 나아갑니다. 왼쪽 줄은 중간에서 끝나니 녹색 구간에서 Q/E + Space로 옆줄로 넘어가세요. 모든 공연 준비자가 도착대에 서면 다음 단계로 갑니다.",
                CourseJson = Course(2, new[] { 14f, 26f }, 26f, "{\"x\":8,\"y\":14}", false, false, 1, 10f, 6f),
            },
            new()
            {
                Id = "2-1", Kind = RehearsalStageKind.Swing, Rule = RehearsalClearRule.Everyone, Finish = 28f,
                Title = "2-1 · 기본 인간 그네",
                Intro = "줄이 끊긴 곳에 인간 그네가 있습니다. 줄 끝에서 F로 그네를 잡고 W/S로 반동을 키운 뒤 Space로 놓아 건너편 줄에 내리세요.",
                CourseJson = Course(1, null, 28f, "{\"x\":0,\"y\":28}", true, false, 1, 10f, 6f),
            },
            new()
            {
                Id = "2-2", Kind = RehearsalStageKind.Swing, Rule = RehearsalClearRule.Everyone, Finish = 32f,
                Title = "2-2 · 연속 인간 그네",
                Intro = "이번 끊김은 더 깁니다. 반동을 충분히 키운 뒤 가장 높이 올라갔을 때 Space로 놓으세요.",
                CourseJson = Course(1, null, 32f, "{\"x\":0,\"y\":32}", true, false, 1, 10f, 10f),
            },
            new()
            {
                Id = "2-3", Kind = RehearsalStageKind.Relay, Rule = RehearsalClearRule.Anyone, Finish = 28f,
                Title = "2-3 · 트리거 그네",
                Intro = "모든 줄이 끊겨 있습니다. 한 명이 바깥 줄 끝의 트리거 그네를 F로 잡고 있는 동안 가운데 줄이 이어집니다. 동료가 건너간 뒤 건너편 트리거 그네를 잡으면 남은 사람의 줄이 이어집니다.",
                CourseJson = Course(3, null, 28f, "{\"x\":0,\"y\":28}", true, true, 1, 10f, 6f),
            },
            new()
            {
                Id = "3", Kind = RehearsalStageKind.Relay, Rule = RehearsalClearRule.Anyone, Finish = 30f,
                Title = "3단계 · 종합 테스트",
                Intro = "네 줄 모두 끊겨 있습니다. 배운 대로 트리거 그네를 서로 잡아 줄을 이어 주고, 한 명 이상이 도착대에 서면 리허설이 끝납니다.",
                CourseJson = Course(4, null, 30f, "{\"x\":0,\"y\":30}", true, true, 1, 10f, 6f),
            },
        };

        static string F(float value) => value.ToString(CultureInfo.InvariantCulture);

        // 짧은 코스 공통값: 출발대 4m·도착대 3m, 안전망 없음, 줄 아래 1m면 추락. 공중 봉 교대(트라페즈)는 쓰지 않는다.
        static string Course(int lanes, float[] laneEnds, float finish, string changeZone, bool swing, bool relay, int holders, float gapStart, float gapLength)
        {
            string ends = laneEnds == null ? "[]" : "[" + string.Join(",", System.Array.ConvertAll(laneEnds, F)) + "]";
            return "{" +
                $"\"laneCount\":{lanes},\"laneSpacing\":3,\"laneEndDistances\":{ends},\"finishDistance\":{F(finish)}," +
                $"\"laneChangeZones\":[{changeZone}]," +
                $"\"swingGapEnabled\":{(swing ? "true" : "false")},\"relayGapEnabled\":{(relay ? "true" : "false")}," +
                $"\"relayRequiredHolders\":{holders},\"relayHoldSeconds\":30,\"swingGapLanes\":[1,2,3,4]," +
                $"\"swingGapStart\":{F(gapStart)},\"swingGapLength\":{F(gapLength)},\"trapezeEnabled\":false," +
                "\"startPlatformLength\":4,\"finishPlatformLength\":3,\"sideMargin\":1.5,\"platformThickness\":0.5," +
                "\"spawnDistance\":-1.5,\"safetyNetDepth\":0,\"fallDepth\":1" +
                "}";
        }
    }
}
