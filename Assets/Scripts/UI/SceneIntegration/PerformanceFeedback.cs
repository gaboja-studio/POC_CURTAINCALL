using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Reward;
using CurtainCall.Tightrope;
using CurtainCall.Voice;
using TMPro;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>C10 표시 전용. 점수를 변경하지 않으며 초기/재동기화와 사망·이탈은 사건으로 표시하지 않는다.</summary>
    public sealed class PerformanceFeedback : MonoBehaviour
    {
        [SerializeField] FeedbackView view;
        [SerializeField] TMP_Text amount;
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

        public void Configure(FeedbackView target, TMP_Text amountLabel) { view = target; amount = amountLabel; }

        public void Observe(RewardSnapshot snapshot)
            => Observe(snapshot, NetworkPlayer.Local != null ? NetworkPlayer.Local.OwnerClientId : (ulong?)null);

        public void Observe(RewardSnapshot snapshot, ulong? localClient)
        {
            if (observingClient != localClient) { Clear(); observingClient = localClient; }
            if (snapshot?.Performance == null || !snapshot.IsOpen) { Clear(); return; }
            bool continuous = identity == snapshot.Performance.Id && rules.Equals(snapshot.Rules);
            if (identity == snapshot.Performance.Id && snapshot.Revision <= revision) return;
            if (!continuous) { pending.Clear(); if (view != null) view.ResetForRuntime(); }
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
            if (!visible) { pending.Clear(); if (view != null) view.ResetForRuntime(); }
        }

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
                voiceOverlay.PresentationSuppressed = !context || previousVoiceSuppression;
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
            if (!context || view == null || view.HasRuntimePresentation) return;
            while (pending.Count > 0)
            {
                var next = pending.Dequeue();
                if (next.expires < Time.unscaledTime) continue;
                bool displayed = next.intended > 0 ? view.TryShowCheer(next.slot, System.Math.Max(1, next.actual))
                    : view.TryShowBoo(next.slot, -next.actual);
                if (displayed && next.actual == 0 && amount != null) amount.text = string.Empty;
                break;
            }
        }

        void ShowHandhold()
        {
            var local = NetworkPlayer.Local;
            if (!context || view == null || amount == null || local == null || local.Slot < 0 || local.Slot > 3) return;
            // 제공 뷰는 양수 변화량을 요구한다. 정보성 손잡기는 같은 연출만 쓰고 숫자를 즉시 비운다.
            // 임의 점수를 지급하거나 +1을 화면에 표시하지 않는다.
            if (view.TryShowCheer(local.Slot, 1, "손 잡아줌")) amount.text = string.Empty;
        }

        public void Clear()
        {
            identity = null; revision = 0; previous.Clear(); pending.Clear();
            if (view != null) view.ResetForRuntime();
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
        void OnDisable() { Unbind(); RestoreVoice(); context = false; Clear(); }
    }
}
