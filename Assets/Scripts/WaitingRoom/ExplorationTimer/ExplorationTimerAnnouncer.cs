using System;
using CurtainCall.Campaign;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>탐색 제한 시간 방송 한 건.</summary>
    public readonly struct ExplorationAnnouncement
    {
        /// <summary>방송 시점의 남은 시간(초, 문턱 값).</summary>
        public readonly int SecondsLeft;
        /// <summary>소집 경보(종소리 + 방송)인지. 아니면 방송만.</summary>
        public readonly bool IsRecallAlarm;
        public readonly string Message;
        public readonly string Title;

        public ExplorationAnnouncement(int secondsLeft, bool isRecallAlarm, string message) : this(secondsLeft, isRecallAlarm, message, null) { }
        public ExplorationAnnouncement(int secondsLeft, bool isRecallAlarm, string message, string title)
        {
            SecondsLeft = secondsLeft;
            IsRecallAlarm = isRecallAlarm;
            Message = message;
            Title = title;
        }
    }

    /// <summary>
    /// 탐색 제한 시간 경보·방송의 공개 진입점. 모든 화면에서 각자 돈다(판정은 공용 런 진행이 하고, 여기는 알림만).
    /// - 종료 60초 전 소집 경보: 공용 <see cref="CampaignRun.RecallAlarm"/>(세팅 RecallAlarmLeadTime) → 종소리 + 방송.
    /// - 30·10초 전 방송: 공용 신호가 없어 동기화된 남은 시간으로 이 화면이 판정(<see cref="ExplorationTimerCues"/>).
    /// 연출(<see cref="ExplorationTimerView"/>)은 <see cref="Announced"/>만 구독한다.
    /// </summary>
    public sealed class ExplorationTimerAnnouncer : MonoBehaviour
    {
        [Tooltip("방송(종소리 없음) 문턱(초). 공용 세팅에 칸이 생기면 옮긴다.")]
        [SerializeField] float[] broadcastSeconds = { 30f, 10f };

        // 경보·방송 문구는 문구 표(WaitingRoomTexts.txt)의 broadcast.recall / broadcast.notice에서 고친다({seconds} = 남은 초).

        CampaignRun run;
        ExplorationTimerCues cues;

        /// <summary>경보·방송이 났다(이 화면).</summary>
        public event Action<ExplorationAnnouncement> Announced;

        void Awake() => cues = new ExplorationTimerCues(broadcastSeconds);

        void OnEnable()
        {
            run = CampaignRun.EnsureExists();
            run.RecallAlarm += HandleRecallAlarm;
            run.StepChanged += HandleStep;
        }

        void OnDisable()
        {
            if (run == null) return;
            run.RecallAlarm -= HandleRecallAlarm;
            run.StepChanged -= HandleStep;
        }

        void Update()
        {
            if (run == null) return;
            var s = run.State;
            float hit = cues.Step((s.Phase == CampaignPhase.RestTime || s.Phase == CampaignPhase.Night) && s.Timer == RestTimerState.Running, s.TimeRemaining);
            if (hit != ExplorationTimerCues.None) Raise(Mathf.RoundToInt(hit), false);
        }

        void HandleStep(CampaignSnapshot _) => cues.Reset();

        void HandleRecallAlarm() => Raise(Mathf.CeilToInt(run.State.TimeRemaining), true);

        void Raise(int seconds, bool recall)
        {
            var phase = run != null ? run.State.Phase : CampaignPhase.RestTime;
            var notice = Settings.GameSettings.WaitingRoom.Presentation(phase).Notice(recall ? 60 : seconds);
            var text = notice.Body.Replace("{seconds}", seconds.ToString());
            Debug.Log($"[WaitingRoom] {(recall ? "소집 경보" : "방송")}: {text}");
            Announced?.Invoke(new ExplorationAnnouncement(seconds, recall, text, notice.Title.Replace("{seconds}", seconds.ToString())));
        }
    }
}
