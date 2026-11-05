using System.Globalization;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    public enum RehearsalStageKind : byte { Balance, LaneChange, Relay, Piggyback, Trapeze }

    /// <summary>도착·투표 흐름은 유지한다. 2-3만 한 명 도착으로 완료한다.</summary>
    public enum RehearsalClearRule : byte { Everyone, Anyone, EveryoneOrMajority }

    /// <summary>Start에서 연결되는 Tightrope 공연의 활성 규칙을 다섯 소구간으로 나눠 배운다.</summary>
    public sealed class RehearsalStage
    {
        public string Id, Title, Intro;
        public RehearsalStageKind Kind;
        public RehearsalClearRule Rule;
        public float CheckpointDistance = 1f;
        public string DisplayTitle => WaitingRoomText.Get("rehearsal.stage." + Id + ".title", Title);
        public string DisplayGuideTitle => WaitingRoomText.Get("rehearsal.stage." + Id + ".guide-title", DisplayTitle);
        public string DisplayIntro => WaitingRoomText.Get("rehearsal.stage." + Id + ".intro", Intro);

        // 조작/기물은 공연 원본. 네 줄의 연습장은 기존 30/32/34m 길이를 사용한다.
        public float Finish => Kind == RehearsalStageKind.Relay ? 24f + GameSettings.Tightrope.Course.SwingGapLength
            : Kind == RehearsalStageKind.Trapeze ? 22f + GameSettings.Tightrope.Course.TrapezeLength : 30f;

        public string CourseJson
        {
            get
            {
                var shape = GameSettings.Tightrope.Course.Clone();
                string finish = Finish.ToString(CultureInfo.InvariantCulture);
                string relay = Kind == RehearsalStageKind.Relay ? "true" : "false";
                string trapeze = Kind == RehearsalStageKind.Trapeze ? "true" : "false";
                JsonUtility.FromJsonOverwrite("{" +
                    $"\"laneCount\":4,\"finishDistance\":{finish},\"laneChangeZones\":[{{\"x\":0,\"y\":{finish}}}]," +
                    $"\"swingGapEnabled\":{relay},\"relayGapEnabled\":{relay},\"swingGapStart\":10," +
                    $"\"trapezeEnabled\":{trapeze},\"trapezeStart\":10" + "}", shape);
                return shape.ToJson();
            }
        }

        public static readonly RehearsalStage[] All =
        {
            new() { Id = "1", Kind = RehearsalStageKind.Balance, Rule = RehearsalClearRule.EveryoneOrMajority,
                Title = "1단계 · 외줄타기", Intro = "W/S로 이동하고 A/D로 균형을 잡으며 도착대까지 외줄을 건너세요." },
            new() { Id = "2-1", Kind = RehearsalStageKind.LaneChange, Rule = RehearsalClearRule.EveryoneOrMajority,
                Title = "2-1 · 줄 전환과 손잡기", Intro = "Q/E를 누른 채 Space로 옆줄로 건너세요. 동료가 서 있는 줄의 손잡기 범위에 착지하면 충격과 흔들림이 줄어듭니다." },
            new() { Id = "2-2", Kind = RehearsalStageKind.Relay, Rule = RehearsalClearRule.EveryoneOrMajority,
                Title = "2-2 · 해제봉 교대", Intro = "청록색 출발 봉을 F로 잡으면 줄 하나가 무작위로 이어집니다. 먼저 건넌 동료는 초록색 교대 봉을 잡아 남은 동료의 길을 열어 주세요. F나 Space로 놓습니다." },
            new() { Id = "2-3", Kind = RehearsalStageKind.Piggyback, Rule = RehearsalClearRule.Anyone,
                Title = "2-3 · 목마 도약", Intro = "F로 목마를 연결하고 함께 2m 이동하세요. 위 사람은 W + Space로 목마에서 도약해 도착대에 착지하세요. 혼자 걸어가면 완료되지 않습니다." },
            new() { Id = "3", Kind = RehearsalStageKind.Trapeze, Rule = RehearsalClearRule.EveryoneOrMajority,
                Title = "3단계 · 공중그네 잡기와 도약", Intro = "목마 위 사람이 F로 첫 봉을 잡고, 아래 동료도 F로 매달리세요. W/S로 반동을 만들고 Space로 도약해 다음 봉을 F로 잡으면 학습 완료입니다." },
        };
    }
}
