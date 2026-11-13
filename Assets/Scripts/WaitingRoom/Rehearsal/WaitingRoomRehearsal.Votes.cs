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
    /// 오른쪽 아래 투표 패널(기획서 v19).
    /// 구간 넘기기: 최소 한 명이 도착한 뒤 전체 인원의 과반이 동의하면 현재 소구간만 완료한다.
    ///   마지막 구간 뒤에는 길안내·집결을 거쳐 첫 공연으로 이동한다.
    /// 판정은 호스트가 하고 기존 상태 메시지로 모두에게 알린다.
    /// </summary>
    public sealed partial class WaitingRoomRehearsal
    {
        /// <summary>"리허설이 종료됩니다." 안내 뒤 화면이 검게 덮일 때까지(초)와 그 뒤 실제 전환까지(초).</summary>
        const float EndingNoticeSeconds = 2f, EndingFadeSeconds = 1f;

        // ── 공유 상태 ──
        readonly HashSet<ulong> skipVotes = new();
        int voterCount;
        bool ending;

        // ── 호스트 ──
        double endingAt;

        // ── 이 화면 ──
        RehearsalVotePanel votePanel;
        RehearsalScreenFader fader;
        bool endingShown;

        /// <summary>리허설 넘기기 투표가 통과해 종료 연출 중이다. 테스트 확인용.</summary>
        public static bool IsEnding => instance != null && instance.ending;
        /// <summary>투표 패널(제목·본문). 테스트 확인용.</summary>
        public static string VotePanelTitle => instance != null && instance.votePanel != null ? instance.votePanel.Title : "";

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
            if (!active || completed) { if (skipVotes.Count > 0) { skipVotes.Clear(); changed = true; } }
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
            // 넘긴 리허설도 이수로 기억한다.
            savedStage = RehearsalStage.All.Length;
            completed = true; version++; Publish();
            if (run == null || !run.EndRehearsal()) Debug.LogWarning("[Rehearsal] 리허설 넘기기 뒤 첫 공연으로 넘기지 못했습니다.");
        }

        void WriteVotes(FastBufferWriter writer)
        {
            writer.WriteValueSafe(voterCount);
            writer.WriteValueSafe(ending);
            writer.WriteValueSafe(skipVotes.Count);
            foreach (ulong id in skipVotes) writer.WriteValueSafe(id);
        }

        void ReadVotes(FastBufferReader reader)
        {
            reader.ReadValueSafe(out voterCount);
            reader.ReadValueSafe(out ending);
            skipVotes.Clear();
            reader.ReadValueSafe(out int skips);
            for (int i = 0; i < skips; i++) { reader.ReadValueSafe(out ulong id); skipVotes.Add(id); }
        }

        // ── 이 화면 ──

        void RequestSkip(bool value)
        {
            // 구간 번호를 함께 보내 이전 구간에서 늦게 온 투표가 다음 구간에 적용되지 않게 한다.
            if (!ending) RequestVote(value);
        }

        /// <summary>패널·종료 연출 갱신. 리허설 밖에서도 매 프레임 부른다.</summary>
        void TickVoteUi()
        {
            if (ending && !endingShown)
            {
                endingShown = true;
                WaitingRoomTutorialPanel.Hide();
                WaitingRoomGuidePanel.Show(WaitingRoomText.Get("rehearsal.skip-ending", "리허설이 종료됩니다."), "", EndingNoticeSeconds + EndingFadeSeconds);
                Invoke(nameof(BeginEndingFade), EndingNoticeSeconds);
            }
            if (!ending && !fader.IsActive) endingShown = false;

            ulong me = LocalId;
            int required = voterCount / 2 + 1;
            bool offerSkip = CanVote && !ending && !SceneCurtainTransition.IsActive;
            if (!offerSkip) { votePanel.Hide(); return; }
            bool voted = votes.Contains(me);
            votePanel.Show(WaitingRoomText.Get("rehearsal.skip.title", "리허설 넘기기"),
                WaitingRoomText.Format("rehearsal.skip.body", "과반수가 동의하면 현재 구간을 마치고 다음 단계로 이동합니다. (동의 {votes}/{required})",
                    ("votes", votes.Count), ("required", required)),
                new HudVoteChoice(WaitingRoomText.Get(voted ? "rehearsal.skip.cancel" : "rehearsal.skip.agree", voted ? "동의 취소" : "리허설 넘기기"), true, () => RequestSkip(!voted)));
        }

        void BeginEndingFade()
        {
            votePanel.Hide();
            // 공연 공간으로 옮겨 씬 전환이 끝나면 걷는다. 그 뒤 커튼은 공연 흐름이 연다.
            fader.Play(() => SpaceLinkDirector.Instance != null && SpaceLinkDirector.Instance.Current != SpaceId.WaitingRoom
                && !SpaceLinkDirector.Instance.IsTransitioning);
        }
    }
}
