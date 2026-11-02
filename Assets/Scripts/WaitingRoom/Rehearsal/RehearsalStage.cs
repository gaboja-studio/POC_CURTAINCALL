using System.Globalization;
using CurtainCall.Tightrope;

namespace CurtainCall.WaitingRoom
{
    /// <summary>리허설 소구간에서 가르치는 동작. 소개 문구와 1단계 출발 위치가 종류마다 다르다.</summary>
    public enum RehearsalStageKind : byte
    {
        /// <summary>1단계: 균형 잡기·Q/E+Space로 옆줄 건너가기.</summary>
        LaneChange,
        /// <summary>2-1·2-2: 끊긴 줄을 인간 그네로 건넌다. F 잡기 · W/S 반동 · Space 놓기.</summary>
        Swing,
        /// <summary>2-3·3단계: 트리거 그네. 한 명이 잡고 있는 동안 정해진 줄의 끊긴 구간이 이어진다.</summary>
        Trigger,
    }

    /// <summary>소구간 완료 인원(기획서 3장: 1단계 전원, 2·3단계 마지막 도착대 1명 이상).</summary>
    public enum RehearsalClearRule : byte { Everyone, Anyone }

    /// <summary>줄의 끊긴 구간(m). 트리거 그네가 잡혀 있는 동안만 이어진다.</summary>
    public readonly struct RehearsalGap
    {
        public readonly int Lane;
        public readonly float From, To;
        public RehearsalGap(int lane, float from, float to) { Lane = lane; From = from; To = to; }
    }

    /// <summary>트리거 그네. 줄 <see cref="Lane"/>의 <see cref="Distance"/>에 매달려 있고, 잡는 동안 <see cref="Gap"/>번 끊긴 구간을 잇는다.</summary>
    public readonly struct RehearsalTrigger
    {
        public readonly int Lane, Gap;
        public readonly float Distance;
        public RehearsalTrigger(int lane, float distance, int gap) { Lane = lane; Distance = distance; Gap = gap; }
    }

    /// <summary>줄 끝에 놓는 발판(1단계 중간 지점).</summary>
    public readonly struct RehearsalPlatform
    {
        public readonly int Lane;
        public readonly float From, To;
        public RehearsalPlatform(int lane, float from, float to) { Lane = lane; From = from; To = to; }
    }

    /// <summary>
    /// 최초 리허설 소구간 하나(Task-097, 기획서 5~7장 구조도). 줄 모양은 외줄 코스 생성 모드(<see cref="TightropeCourse"/>) 값으로 만들고,
    /// 그네·트리거 그네·끊긴 구간은 구조도 위치대로 따로 놓는다(코스 값의 끊김 하나·같은 위치 제약을 쓰지 않는다).
    /// 줄 번호는 구조도 위에서부터 0번(코스 왼쪽)이다.
    /// </summary>
    public sealed class RehearsalStage
    {
        public string Id, Title, Intro;
        public RehearsalStageKind Kind;
        public RehearsalClearRule Rule;
        /// <summary>외줄 코스 모양(<c>TightropeSettings.CourseSettings</c> JSON). 빠진 값은 공통 세팅 기본값.</summary>
        public string CourseJson;
        public string DisplayTitle => WaitingRoomText.Get("rehearsal.stage." + Id + ".title", Title);
        public string DisplayIntro => WaitingRoomText.Get("rehearsal.stage." + Id + ".intro", Intro);
        /// <summary>도착 거리(m). 코스를 링 가운데에 놓을 때 쓴다.</summary>
        public float Finish;
        /// <summary>1단계: 이 거리부터 옆줄 건너가기 안내.</summary>
        public float CheckpointDistance;
        public SwingSpot[] Swings = System.Array.Empty<SwingSpot>();
        public RehearsalGap[] Gaps = System.Array.Empty<RehearsalGap>();
        public RehearsalTrigger[] Triggers = System.Array.Empty<RehearsalTrigger>();
        public RehearsalPlatform[] Platforms = System.Array.Empty<RehearsalPlatform>();

        /// <summary>그네 끊김 길이(m). 외줄 공연 회전봉과 같은 거리(TightropeSettings swingGapLength 8)로 건너편 줄에 착지한다.</summary>
        const float SwingGap = 8f;

        public static readonly RehearsalStage[] All =
        {
            // 1단계 구조도: 시작 지점 ─ 위쪽 줄 ─ 중간 지점(막다른 발판). 아래쪽 줄만 도착 장소로 이어진다. 위쪽 줄 중간에서 점프로 줄 이동.
            new()
            {
                Id = "1", Kind = RehearsalStageKind.LaneChange, Rule = RehearsalClearRule.Everyone, Finish = 26f, CheckpointDistance = 6f,
                Title = "1단계 · 외줄타기",
                Intro = "줄 위에서는 A/D로 균형을 잡고 W로 나아갑니다. 위쪽 줄은 중간 지점에서 끝나니, 줄 중간의 녹색 구간에서 Q/E + Space로 아래쪽 줄로 넘어가세요. 모든 공연 준비자가 도착 장소에 서면 다음 단계로 갑니다.",
                CourseJson = Course(2, new[] { 14f, 26f }, 26f, "{\"x\":6,\"y\":13}"),
                Platforms = new[] { new RehearsalPlatform(0, 14f, 17f) },
            },
            // 2단계 구조도 1번: 출발대 ─ 외줄 ─ 인간 그네 ─ [끊김] ─ 외줄 ─ 도착대.
            new()
            {
                Id = "2-1", Kind = RehearsalStageKind.Swing, Rule = RehearsalClearRule.Everyone, Finish = 26f,
                Title = "2-1 · 기본 인간 그네",
                Intro = "줄이 끊긴 곳에 인간 그네가 있습니다. 줄 끝에서 F로 그네를 잡고 W/S로 반동을 키운 뒤 Space로 놓아 건너편 줄에 내리세요.",
                CourseJson = Course(1, null, 26f, null),
                Swings = new[] { new SwingSpot(0, 8f, 8f + SwingGap) },
            },
            // 2단계 구조도 2번: 출발대 ─ 외줄 ─ 인간 그네 ─ 인간 그네 ─ 외줄 ─ 도착대. 첫 그네를 놓고 날아가며 두 번째 그네를 잡는다.
            new()
            {
                Id = "2-2", Kind = RehearsalStageKind.Swing, Rule = RehearsalClearRule.Everyone, Finish = 27f,
                Title = "2-2 · 연속 인간 그네",
                Intro = "인간 그네가 두 개 이어져 있습니다. 첫 그네에서 Space로 놓고 날아가는 동안 두 번째 그네 가까이에서 F를 눌러 잡습니다. 다시 반동을 키워 건너편 줄로 넘어가세요.",
                CourseJson = Course(1, null, 27f, null),
                Swings = new[] { new SwingSpot(0, 4f, 4f + SwingGap, true), new SwingSpot(0, 4f + SwingGap, 4f + SwingGap * 2f) },
            },
            // 2단계 구조도 3번: 위쪽 줄은 트리거 그네 1에서 끝나고, 아래쪽 줄 가운데가 끊겨 있다. 한 명이 트리거 그네 1을 잡는 동안 아래쪽 줄이 이어진다.
            new()
            {
                Id = "2-3", Kind = RehearsalStageKind.Trigger, Rule = RehearsalClearRule.Anyone, Finish = 28f,
                Title = "2-3 · 트리거 그네",
                Intro = "아래쪽 줄 가운데가 끊겨 있습니다. 한 명이 위쪽 줄 끝의 트리거 그네를 F로 잡고 있는 동안 아래쪽 줄이 이어집니다. 동료가 이어진 줄로 도착 장소에 가면 클리어입니다.",
                CourseJson = Course(2, new[] { 12f, 28f }, 28f, null),
                Gaps = new[] { new RehearsalGap(1, 12f, 18f) },
                Triggers = new[] { new RehearsalTrigger(0, 11.4f, 0) },
            },
            // 3단계 구조도: 나란한 줄 4개. 줄마다 인간 그네 → 트리거 그네·끊김이 엇갈린다.
            // 1줄 트리거 → 2줄 앞쪽 끊김, 2줄 트리거 → 1줄 뒤쪽 끊김. 3·4줄도 같은 짝.
            new()
            {
                Id = "3", Kind = RehearsalStageKind.Trigger, Rule = RehearsalClearRule.Anyone, Finish = 29f,
                Title = "3단계 · 종합 테스트",
                Intro = "줄마다 인간 그네로 첫 끊김을 건넌 뒤, 짝 줄의 동료와 트리거 그네를 번갈아 잡아 서로의 줄을 이어 주세요(1·2번 줄, 3·4번 줄). 한 명 이상 도착 장소에 서면 리허설이 끝납니다.",
                CourseJson = Course(4, null, 29f, null),
                Swings = new[]
                {
                    new SwingSpot(0, 3f, 3f + SwingGap), new SwingSpot(1, 3f, 3f + SwingGap),
                    new SwingSpot(2, 3f, 3f + SwingGap), new SwingSpot(3, 3f, 3f + SwingGap),
                },
                Gaps = new[]
                {
                    new RehearsalGap(0, 17.5f, 22.5f), new RehearsalGap(1, 14.5f, 21.5f),
                    new RehearsalGap(2, 17.5f, 22.5f), new RehearsalGap(3, 14.5f, 21.5f),
                },
                Triggers = new[]
                {
                    new RehearsalTrigger(0, 16.6f, 1), new RehearsalTrigger(1, 23.1f, 0),
                    new RehearsalTrigger(2, 16.6f, 3), new RehearsalTrigger(3, 23.1f, 2),
                },
            },
        };

        static string F(float value) => value.ToString(CultureInfo.InvariantCulture);

        // 짧은 코스 공통값: 출발대 4m·도착대 3m, 안전망 없음, 줄 아래 1m면 추락. 코스 값의 그네·해제봉·공중 봉 교대는 쓰지 않는다.
        static string Course(int lanes, float[] laneEnds, float finish, string changeZone)
        {
            string ends = laneEnds == null ? "[]" : "[" + string.Join(",", System.Array.ConvertAll(laneEnds, F)) + "]";
            return "{" +
                $"\"laneCount\":{lanes},\"laneSpacing\":3,\"laneEndDistances\":{ends},\"finishDistance\":{F(finish)}," +
                $"\"laneChangeZones\":[{changeZone ?? ""}]," +
                "\"swingGapEnabled\":false,\"relayGapEnabled\":false,\"trapezeEnabled\":false," +
                "\"startPlatformLength\":4,\"finishPlatformLength\":3,\"sideMargin\":1.5,\"platformThickness\":0.5," +
                "\"spawnDistance\":-1.5,\"safetyNetDepth\":0,\"fallDepth\":1" +
                "}";
        }
    }
}
