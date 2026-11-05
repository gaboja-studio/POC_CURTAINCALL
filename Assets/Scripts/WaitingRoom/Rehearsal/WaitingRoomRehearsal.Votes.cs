using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.SpaceLink;
using CurtainCall.UI.SceneIntegration;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 오른쪽 아래 투표 패널 두 가지(기획서 v19).
    /// ① 구간 넘기기: 최소 한 명이 도착한 뒤 전체 인원의 과반이 동의하면 현재 소구간만 완료한다.
    ///   마지막 구간 뒤에는 길안내·집결을 거쳐 첫 공연으로 이동한다.
    /// ② 리허설 다시 하기: 이수 뒤 연습실 외줄을 상호작용하면 열린다. 전원이 동의하면 외줄 리허설 재연습(1일차·공연으로 이어지지 않음),
    ///   한 명이라도 미동의하면 취소한다.
    /// 판정은 호스트가 하고 기존 상태 메시지로 모두에게 알린다.
    /// </summary>
    public sealed partial class WaitingRoomRehearsal
    {
        /// <summary>"리허설이 종료됩니다." 안내 뒤 화면이 검게 덮일 때까지(초)와 그 뒤 실제 전환까지(초).</summary>
        const float EndingNoticeSeconds = 2f, EndingFadeSeconds = 1f;

        // ── 공유 상태 ──
        readonly HashSet<ulong> skipVotes = new(), practiceYes = new();
        int voterCount;
        bool ending, practiceVote;

        // ── 호스트 ──
        double endingAt;

        // ── 이 화면 ──
        RehearsalVotePanel votePanel;
        RehearsalScreenFader fader;
        bool endingShown, practiceVoteShown;

        /// <summary>리허설 넘기기 투표가 통과해 종료 연출 중이다. 테스트 확인용.</summary>
        public static bool IsEnding => instance != null && instance.ending;
        /// <summary>리허설 다시 하기 투표가 열려 있다. 테스트 확인용.</summary>
        public static bool IsPracticeVoteOpen => instance != null && instance.practiceVote;
        /// <summary>투표 패널(제목·본문). 테스트 확인용.</summary>
        public static string VotePanelTitle => instance != null && instance.votePanel != null ? instance.votePanel.Title : "";

        /// <summary>이수 뒤 대기실 쉬는 시간·밤에 연습실 외줄로 재연습 투표를 열 수 있다(모든 화면에서 같은 조건).</summary>
        public static bool CanProposePractice => instance != null && !instance.active && !instance.practiceVote && HasMastered
            && instance.run != null && PracticePhase(instance.run.State.Phase) && !SceneCurtainTransition.IsActive
            && SpaceLinkDirector.Instance != null && SpaceLinkDirector.Instance.Current == SpaceId.WaitingRoom
            && !SpaceLinkDirector.Instance.IsTransitioning;

        /// <summary>연습실 외줄 상호작용: 재연습 투표를 연다(연 사람은 동의한 것으로 센다).</summary>
        public static void ProposePractice()
        {
            var r = instance;
            if (r == null || !CanProposePractice) return;
            if (r.HasAuthority) r.HostOpenPracticeVote(r.LocalId);
            else r.SendFlag(Operation.PracticePropose, true);
        }

        ulong LocalId => NetworkPlayer.Local != null ? NetworkPlayer.Local.OwnerClientId : NetworkManager.ServerClientId;

        void AwakeVotes()
        {
            votePanel = gameObject.AddComponent<RehearsalVotePanel>();
            fader = gameObject.AddComponent<RehearsalScreenFader>();
        }

        // ── 호스트 ──

        static int CountVoters()
        {
            int count = 0;
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.State != PlayerState.Retired) count++;
            return count;
        }

        static bool IsVoter(ulong id)
        {
            foreach (var player in NetworkPlayer.All)
                if (player != null && player.IsSpawned && player.OwnerClientId == id && player.State != PlayerState.Retired) return true;
            return false;
        }

        void HostTickVotes()
        {
            if (!HasAuthority) return;
            bool changed = false;
            int voters = CountVoters();
            if (voterCount != voters) { voterCount = voters; changed = true; }
            changed |= skipVotes.RemoveWhere(id => !IsVoter(id)) > 0;
            changed |= practiceYes.RemoveWhere(id => !IsVoter(id)) > 0;
            if (!active || completed) { if (skipVotes.Count > 0) { skipVotes.Clear(); changed = true; } }
            if (practiceVote && (active || !HasMastered || run == null || !PracticePhase(run.State.Phase)))
            { practiceVote = false; practiceYes.Clear(); changed = true; }
            if (practiceVote && voters > 0 && practiceYes.Count >= voters)
            {
                practiceVote = false; practiceYes.Clear(); changed = true;
                practice = true; practiceStage = 0;
                Debug.Log("[Rehearsal] 전원 동의 → 외줄 리허설 재연습 시작");
            }
            if (changed) { version++; Publish(); }
            if (ending && Clock >= endingAt) HostFinishSkip();
        }

        void HostSetSkipVote(ulong id, bool value)
        {
            // v19: 한 명이 도착한 현재 소구간만 넘긴다. 첫 공연은 완료 뒤 집결로 진입한다.
            if (ending || !IsVoter(id)) return;
            SetVote(id, value);
        }

        void HostFinishSkip()
        {
            ending = false; skipVotes.Clear();
            if (practice) { EndPractice(); return; }
            // 넘긴 리허설도 이수로 기억한다(다시 하기는 연습실 외줄 상호작용으로).
            savedStage = RehearsalStage.All.Length;
            completed = true; version++; Publish();
            if (run == null || !run.EndRehearsal()) Debug.LogWarning("[Rehearsal] 리허설 넘기기 뒤 첫 공연으로 넘기지 못했습니다.");
        }

        void HostOpenPracticeVote(ulong proposer)
        {
            if (!CanProposePractice || !IsVoter(proposer)) return;
            practiceVote = true; practiceYes.Clear(); practiceYes.Add(proposer);
            RecordAction("VOTE_CHANGED", proposer, "REPLAY_PROPOSE", true);
            voterCount = CountVoters();
            version++; Publish();
        }

        void HostAnswerPractice(ulong id, bool agree)
        {
            if (!practiceVote || !IsVoter(id)) return;
            if (!agree)
            {
                practiceVote = false; practiceYes.Clear();
                Debug.Log("[Rehearsal] 리허설 다시 하기 미동의 → 취소");
            }
            else if (!practiceYes.Add(id)) return;
            RecordAction("VOTE_CHANGED", id, "REPLAY", agree);
            version++; Publish();
        }

        void HandleVoteRequest(ulong sender, Operation op, FastBufferReader reader)
        {
            reader.ReadValueSafe(out bool value);
            if (op == Operation.SkipVote) HostSetSkipVote(sender, value);
            else if (op == Operation.PracticePropose) HostOpenPracticeVote(sender);
            else if (op == Operation.PracticeAnswer) HostAnswerPractice(sender, value);
        }

        void SendFlag(Operation op, bool value)
        {
            if (messaging == null) return;
            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe((byte)op);
            writer.WriteValueSafe(value);
            messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void WriteVotes(FastBufferWriter writer)
        {
            writer.WriteValueSafe(voterCount);
            writer.WriteValueSafe(ending);
            writer.WriteValueSafe(practiceVote);
            writer.WriteValueSafe(skipVotes.Count);
            foreach (ulong id in skipVotes) writer.WriteValueSafe(id);
            writer.WriteValueSafe(practiceYes.Count);
            foreach (ulong id in practiceYes) writer.WriteValueSafe(id);
        }

        void ReadVotes(FastBufferReader reader)
        {
            reader.ReadValueSafe(out voterCount);
            reader.ReadValueSafe(out ending);
            reader.ReadValueSafe(out practiceVote);
            skipVotes.Clear(); practiceYes.Clear();
            reader.ReadValueSafe(out int skips);
            for (int i = 0; i < skips; i++) { reader.ReadValueSafe(out ulong id); skipVotes.Add(id); }
            reader.ReadValueSafe(out int yes);
            for (int i = 0; i < yes; i++) { reader.ReadValueSafe(out ulong id); practiceYes.Add(id); }
        }

        // ── 이 화면 ──

        void RequestSkip(bool value)
        {
            // 구간 번호를 함께 보내 이전 구간에서 늦게 온 투표가 다음 구간에 적용되지 않게 한다.
            if (!ending) RequestVote(value);
        }

        void AnswerPractice(bool agree)
        {
            if (HasAuthority) HostAnswerPractice(LocalId, agree);
            else SendFlag(Operation.PracticeAnswer, agree);
        }

        /// <summary>패널·종료 연출 갱신. 리허설 밖(재연습 투표)에서도 매 프레임 부른다.</summary>
        void TickVoteUi()
        {
            if (ending && !endingShown)
            {
                endingShown = true;
                WaitingRoomTutorialPanel.Hide();
                WaitingRoomGuidePanel.Show(WaitingRoomText.Get("rehearsal.skip-ending", "리허설이 종료됩니다."), "", EndingNoticeSeconds + EndingFadeSeconds);
                endingPractice = practice;
                Invoke(nameof(BeginEndingFade), EndingNoticeSeconds);
            }
            if (!ending && !fader.IsActive) endingShown = false;

            bool closedWithoutStart = practiceVoteShown && !practiceVote && !practice;
            if (closedWithoutStart && !active)
                WaitingRoomGuidePanel.Show(WaitingRoomText.Get("rehearsal.practice-vote.cancel-title", "리허설 다시 하기 취소"),
                    WaitingRoomText.Get("rehearsal.practice-vote.cancel", "동의하지 않은 공연 준비자가 있어 리허설 다시 하기를 취소했습니다."), 4f);
            practiceVoteShown = practiceVote;

            ulong me = LocalId;
            int required = voterCount / 2 + 1;
            if (practiceVote)
            {
                bool agreed = practiceYes.Contains(me);
                votePanel.Show(WaitingRoomText.Get("rehearsal.practice-vote.title", "리허설 다시 하기"),
                    WaitingRoomText.Format("rehearsal.practice-vote.body", "모든 공연 준비자가 동의하면 외줄타기 리허설을 다시 연습합니다. (동의 {votes}/{total})",
                        ("votes", practiceYes.Count), ("total", Mathf.Max(voterCount, practiceYes.Count))),
                    new RehearsalVotePanel.Choice(WaitingRoomText.Get(agreed ? "rehearsal.practice-vote.agreed" : "rehearsal.practice-vote.agree", agreed ? "동의함" : "동의"), !agreed, () => AnswerPractice(true)),
                    new RehearsalVotePanel.Choice(WaitingRoomText.Get("rehearsal.practice-vote.disagree", "미동의"), true, () => AnswerPractice(false)));
                return;
            }
            bool offerSkip = CanVote && !ending && !SceneCurtainTransition.IsActive;
            if (!offerSkip) { votePanel.Hide(); return; }
            bool voted = votes.Contains(me);
            votePanel.Show(WaitingRoomText.Get("rehearsal.skip.title", "리허설 넘기기"),
                WaitingRoomText.Format("rehearsal.skip.body", "과반수가 동의하면 현재 구간을 마치고 다음 단계로 이동합니다. (동의 {votes}/{required})",
                    ("votes", votes.Count), ("required", required)),
                new RehearsalVotePanel.Choice(WaitingRoomText.Get(voted ? "rehearsal.skip.cancel" : "rehearsal.skip.agree", voted ? "동의 취소" : "리허설 넘기기"), true, () => RequestSkip(!voted)));
        }

        bool endingPractice;

        void BeginEndingFade()
        {
            votePanel.Hide();
            bool wasPractice = endingPractice;
            fader.Play(() => wasPractice
                ? !practice && !active
                // 공연 공간으로 옮겨 씬 전환이 끝나면 걷는다. 그 뒤 커튼은 공연 흐름이 연다.
                : SpaceLinkDirector.Instance != null && SpaceLinkDirector.Instance.Current != SpaceId.WaitingRoom
                  && !SpaceLinkDirector.Instance.IsTransitioning);
        }
    }
}
