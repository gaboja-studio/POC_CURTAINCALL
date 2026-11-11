using System.Globalization;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    public enum RehearsalStageKind : byte { Balance, LaneChange, Relay, Piggyback, Trapeze, SinglePole, ConsecutivePoles }

    /// <summary>도착·투표 흐름은 유지한다. 2-3만 한 명 도착으로 완료한다.</summary>
    public enum RehearsalClearRule : byte { Everyone, Anyone, EveryoneOrMajority }

    /// <summary>Start에서 연결되는 Tightrope 공연의 활성 규칙을 다섯 소구간으로 나눠 배운다.</summary>
    public sealed class RehearsalStage
    {
        public string Id, Title, Intro;
        public RehearsalStageKind Kind;
        public RehearsalClearRule Rule;
        public float CheckpointDistance = 14f;
        public bool UsesPoles => Kind is RehearsalStageKind.SinglePole or RehearsalStageKind.ConsecutivePoles or RehearsalStageKind.Trapeze;
        // 1단계의 기획상 완료 조건은 실제 도착이다. 순간 점프 복제 기록은 완료의 필수 조건이 아니다.
        public bool RequiresSkillForArrival => Kind is not (RehearsalStageKind.Balance or RehearsalStageKind.LaneChange);
        public bool CanAdvance(int participants, int arrivals, int votes) => participants > 0 && arrivals > 0
            && (arrivals >= participants || Rule == RehearsalClearRule.Anyone
                || Rule == RehearsalClearRule.EveryoneOrMajority && votes > participants / 2);
        public string DisplayTitle => WaitingRoomText.Get("rehearsal.stage." + Id + ".title", Title);
        public string DisplayGuideTitle => WaitingRoomText.Get("rehearsal.stage." + Id + ".guide-title", DisplayTitle);
        public string DisplayIntro => WaitingRoomText.Get("rehearsal.stage." + Id + ".intro", Intro);

        // v19 구조도의 기존 임시 길이. 3단계는 사용자 2026-11-05 정정으로 현행을 유지한다.
        public float Finish => Kind == RehearsalStageKind.Trapeze ? 22f + GameSettings.Tightrope.Course.TrapezeLength
            : Kind is RehearsalStageKind.LaneChange or RehearsalStageKind.ConsecutivePoles ? 38f : 32f;

        public string CourseJson
        {
            get
            {
                var shape = GameSettings.Tightrope.Course.Clone();
                string finish = Finish.ToString(CultureInfo.InvariantCulture);
                string trapeze = UsesPoles ? "true" : "false";
                int lanes = Kind == RehearsalStageKind.Trapeze ? 4 : UsesPoles ? 1 : 2;
                string ends = Kind == RehearsalStageKind.LaneChange ? "22,38" : Kind == RehearsalStageKind.Relay ? "12,32" : "";
                string lesson = Kind == RehearsalStageKind.Trapeze ? "" :
                    $",\"startPlatformLength\":4,\"trapezeLane\":1,\"trapezeLength\":{(Kind == RehearsalStageKind.ConsecutivePoles ? 12 : 8)}";
                JsonUtility.FromJsonOverwrite("{" +
                    // 모든 수업은 같은 소형 도착대를 배치하고, 실제 방 경계 안에서만 확장한다.
                    // 공연의 코스 길이가 늘어도 공연용 도착대 크기까지 복제하지 않는다.
                    $"\"laneCount\":{lanes},\"laneEndDistances\":[{ends}],\"finishDistance\":{finish},\"finishPlatformLength\":3," +
                    $"\"swingGapEnabled\":false,\"relayGapEnabled\":false,\"swingGapStart\":{(Kind == RehearsalStageKind.Relay ? 12 : 10)}," +
                    $"\"trapezeEnabled\":{trapeze},\"trapezeStart\":10" + lesson + "}", shape);
                return shape.ToJson();
            }
        }

        public static readonly RehearsalStage[] All =
        {
            new() { Id = "1", Kind = RehearsalStageKind.LaneChange, Rule = RehearsalClearRule.EveryoneOrMajority,
                Title = "1단계 · 외줄타기", Intro = "줄 위에서는 A/D로 균형을 잡고 W로 나아갑니다. S를 누르면 뒤로 이동합니다." },
            new() { Id = "2-1", Kind = RehearsalStageKind.SinglePole, Rule = RehearsalClearRule.EveryoneOrMajority,
                Title = "2-1 · 인간 그네", Intro = "줄 끝에서 F를 이용하면 인간 그네를 잡습니다. W/S로 그네를 조작한 뒤 Space를 누르면 건너편 줄로 점프합니다." },
            new() { Id = "2-2", Kind = RehearsalStageKind.ConsecutivePoles, Rule = RehearsalClearRule.EveryoneOrMajority,
                Title = "2-2 · 연속 인간 그네", Intro = "체공 중 F키를 눌러 그네에서 다음 그네로 넘어갈 수 있습니다." },
            new() { Id = "2-3", Kind = RehearsalStageKind.Relay, Rule = RehearsalClearRule.Anyone,
                Title = "2-3 · 트리거 그네", Intro = "아래쪽 줄이 끊어져 있습니다. 한 명이 트리거 그네를 잡고 있는 동안 끊어진 줄이 연결됩니다." },
            new() { Id = "3", Kind = RehearsalStageKind.Trapeze, Rule = RehearsalClearRule.EveryoneOrMajority,
                Title = "3단계 · 공중그네 잡기와 도약", Intro = "목마 위 사람이 F로 첫 봉을 잡고, 아래 동료도 F로 매달리세요. W/S로 반동을 만들고 Space로 도약해 다음 봉을 F로 잡으면 학습 완료입니다." },
        };
    }
}
