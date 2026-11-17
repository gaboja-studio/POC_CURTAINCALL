using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Cameras;
using CurtainCall.Reward;
using CurtainCall.Tightrope;
using CurtainCall.Voice;
using TMPro;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>C10 표시 전용. 점수를 변경하지 않으며 초기/재동기화와 사망·이탈은 사건으로 표시하지 않는다.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class PerformanceFeedback : MonoBehaviour
    {
        [SerializeField] FeedbackView view;
        [SerializeField] TMP_Text amount;
        [SerializeField] NumericFeedbackView numbers;
        [SerializeField] AudienceReactionAudio reactionAudio;
        [SerializeField] AudienceSectorTrial crowdTrial;
        readonly Dictionary<string, RewardSegment> previous = new();
        readonly Queue<(int slot, int intended, int actual, float expires)> pending = new();
        ulong? observingClient;
        string identity;
        long revision;
        RewardRules rules;
        PiggybackSystem piggyback;
        bool previousSuppression;
        bool context;
        VoiceChatDevOverlay voiceOverlay;
        float previousVoiceTop;
        bool previousVoiceSuppression;
        public FeedbackView View => view;
        public NumericFeedbackView Numbers => numbers;
        public AudienceReactionAudio ReactionAudio => reactionAudio;
        public static NetworkPlayer FocusPlayer => CameraSpectator.IsSpectating ? CameraSpectator.Target : NetworkPlayer.Local;
        public void ConfigureCrowd(AudienceSectorTrial trial) => crowdTrial = trial;
        public void ConfigureNumbers(NumericFeedbackView numericView, AudienceReactionAudio audio)
        { numbers = numericView; reactionAudio = audio; }

        public void Configure(FeedbackView target, TMP_Text amountLabel) { view = target; amount = amountLabel; }

        public void Observe(RewardSnapshot snapshot)
        {
            var focus = FocusPlayer;
            Observe(snapshot, focus != null ? focus.OwnerClientId : (ulong?)null);
            numbers?.SetSubject(CameraSpectator.IsSpectating, focus != null ? focus.Slot : -1);
        }

        /// <summary>Switches focus even without a new reward packet; old audio/queues cannot leak into the next target.</summary>
        public void RefreshFocus(RewardSnapshot snapshot)
        {
            var focus = FocusPlayer;
            ulong? client = focus != null ? focus.OwnerClientId : (ulong?)null;
            if (observingClient != client) Observe(snapshot);
            numbers?.SetSubject(CameraSpectator.IsSpectating, focus != null ? focus.Slot : -1);
        }

        public void Observe(RewardSnapshot snapshot, ulong? localClient)
        {
            if (crowdTrial != null) crowdTrial.Observe(snapshot);
            if (observingClient != localClient) { Clear(); observingClient = localClient; }
            if (snapshot?.Performance == null || !snapshot.IsOpen) { Clear(); return; }
            bool continuous = identity == snapshot.Performance.Id && rules.Equals(snapshot.Rules);
            if (identity == snapshot.Performance.Id && snapshot.Revision <= revision) return;
            if (!continuous) { pending.Clear(); ResetPresentation(); }
            foreach (var person in snapshot.Segments)
            {
                if (continuous && context && localClient.HasValue && person.ClientId == localClient.Value
                    && previous.TryGetValue(person.ParticipantId, out var before)
                    && before.Connected && person.Connected && !before.Dead && !person.Dead && before.Slot == person.Slot)
                {
                    if (person.ReactionSequence > before.ReactionSequence && person.ReactionDelta != 0
                        && (person.ReactionDelta > 0 || person.ReactionAmount < 0)
                        && person.Slot >= 0 && person.Slot < 4)
                    {
                        if (pending.Count >= 4) pending.Dequeue();
                        pending.Enqueue((person.Slot, person.ReactionDelta, person.ReactionAmount, Time.unscaledTime + 3f));
                        // Audio follows the new event immediately, independently of the original popup's display queue.
                        reactionAudio?.Play(person.ReactionDelta > 0);
                    }
                }
            }
            previous.Clear();
            foreach (var person in snapshot.Segments) previous[person.ParticipantId] = person;
            identity = snapshot.Performance.Id; revision = snapshot.Revision; rules = snapshot.Rules;
        }

        public void SetContext(bool visible)
        {
            context = visible;
            if (!visible) { pending.Clear(); ResetPresentation(); }
        }
        public void SetVenueContext(bool active) => reactionAudio?.SetVenueActive(active);

        void LateUpdate()
        {
            var nextVoice = VoiceChat.Instance != null ? VoiceChat.Instance.GetComponent<VoiceChatDevOverlay>() : null;
            if (voiceOverlay != nextVoice)
            {
                RestoreVoice(); voiceOverlay = nextVoice;
                if (voiceOverlay != null) { previousVoiceTop = voiceOverlay.PresentationTop; previousVoiceSuppression = voiceOverlay.PresentationSuppressed; }
            }
            if (voiceOverlay != null)
            {
                // C3 아래로 이동. 메뉴·관전·결과에서는 개발 음성 상자도 화면을 가리지 않는다.
                voiceOverlay.PresentationTop = 160f * Mathf.Sqrt(Screen.width / 1920f * Screen.height / 1080f);
                voiceOverlay.PresentationSuppressed = !context || CameraSpectator.IsSpectating || previousVoiceSuppression;
            }
            if (piggyback != PiggybackSystem.Current)
            {
                Unbind(); piggyback = PiggybackSystem.Current;
                if (piggyback != null)
                {
                    previousSuppression = piggyback.SuppressLegacyFeedback;
                    piggyback.SuppressLegacyFeedback = true;
                    piggyback.HandholdLanded += ShowHandhold;
                }
            }
            if (!context) return;
            if (numbers != null)
            {
                while (pending.Count > 0)
                {
                    var next = pending.Dequeue();
                    if (next.expires < Time.unscaledTime) continue;
                    numbers.Show(next.actual, next.expires - 3f);
                    if (next.actual != 0) CommonPerformanceUI.Current?.Cheer?.PulseReaction(next.actual > 0);
                }
                return;
            }
            // Keep the original art/motion, but refresh it for a new event so it agrees with the immediate one-shot.
            if (view == null) return;
            while (pending.Count > 0)
            {
                var next = pending.Dequeue();
                if (next.expires < Time.unscaledTime) continue;
                bool displayed = HudUIManager.ShowScoreFeedback(null,view,next.slot,next.intended > 0 ? System.Math.Max(1,next.actual) : -next.actual,next.intended > 0);
                if (displayed && next.actual == 0 && amount != null) amount.text = string.Empty;
                break;
            }
        }

        void ShowHandhold()
        {
            var local = NetworkPlayer.Local;
            if (numbers != null)
            {
                if (context && !CameraSpectator.IsSpectating && local != null && local.Slot >= 0 && local.Slot < 4) numbers.ShowHandhold();
                return;
            }
            if (!context || CameraSpectator.IsSpectating || view == null || amount == null || local == null || local.Slot < 0 || local.Slot > 3) return;
            // 제공 뷰는 양수 변화량을 요구한다. 정보성 손잡기는 같은 연출만 쓰고 숫자를 즉시 비운다.
            // 임의 점수를 지급하거나 +1을 화면에 표시하지 않는다.
            if (HudUIManager.ShowScoreFeedback("손 잡아줌",view,local.Slot,1,true)) amount.text = string.Empty;
        }

        public void Clear()
        {
            identity = null; revision = 0; previous.Clear(); pending.Clear();
            ResetPresentation();
        }
        void ResetPresentation()
        {
            if (view != null) view.ResetForRuntime();
            if (numbers != null) numbers.ResetForRuntime();
            if (reactionAudio != null) reactionAudio.Stop();
        }
        void Unbind()
        {
            if (piggyback == null) return;
            piggyback.HandholdLanded -= ShowHandhold;
            piggyback.SuppressLegacyFeedback = previousSuppression;
            piggyback = null;
        }
        void RestoreVoice()
        {
            if (voiceOverlay == null) return;
            voiceOverlay.PresentationTop = previousVoiceTop; voiceOverlay.PresentationSuppressed = previousVoiceSuppression;
            voiceOverlay = null;
        }
        void OnDisable() { Unbind(); RestoreVoice(); context = false; Clear(); SetVenueContext(false); }
    }
}
