using System;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Survival
{
    /// <summary>
    /// 호스트: 쉬는 시간 만료 처리(M2 게임 흐름 3-2). 시간이 끝나면 추격자가 대기실에 풀리고, 잡히면 팔 하나만 남은 채
    /// 무대로 끌려간다(이미 외팔이면 사망 → 공연 시작 때 외팔로 부활 출연, <see cref="InjuryRevival"/>).
    /// 추격자는 대기실 담당이 나중에 만든다. 그 전까지는 만료 순간 무대 입구 통로에 없는 살아 있는 사람을 바로 잡힘 처리한다
    /// (2026-10-28 사용자 결정). 추격자를 붙일 때는 <see cref="Handler"/>를 바꾸면 되고, 런 진행은 그대로 둔다.
    /// </summary>
    public static class RestTimeoutCatch
    {
        /// <summary>
        /// 무대 입구 통로 안인지. 대기실의 집결 판정이 등록한다. 등록이 없으면 아무도 안전하지 않다고 본다.
        /// </summary>
        public static Func<NetworkPlayer, bool> IsAtStageEntrance;

        /// <summary>
        /// 만료 처리 교체 지점. 런 진행이 공연으로 넘어가기 직전에 호스트에서 한 번 부른다. 잡힌 인원을 돌려준다.
        /// 기본값은 <see cref="CatchOutsideStageEntrance"/>(즉시 잡힘). 추격자 구현 시 다른 처리로 바꾼다.
        /// </summary>
        public static Func<CampaignSnapshot, int> Handler = CatchOutsideStageEntrance;

        /// <summary>호스트: 쉬는 시간이 끝났다. 마지막 아침(런 실패로 넘어감)은 잡지 않는다.</summary>
        public static int ServerHandleExpired(CampaignSnapshot step)
        {
            if (step.Phase != CampaignPhase.RestTime || step.IsFinalMorning || Handler == null) return 0;
            int caught = Handler(step);
            if (caught > 0) Debug.Log($"[Survival] {CampaignRun.Describe(step)} 만료: 무대 입구 밖 {caught}명 잡힘");
            return caught;
        }

        /// <summary>기본 처리: 무대 입구 통로 밖에 있는 살아 있는 사람을 모두 잡힘 처리한다(외팔, 이미 외팔이면 사망).</summary>
        public static int CatchOutsideStageEntrance(CampaignSnapshot step)
        {
            int caught = 0;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                if (IsAtStageEntrance != null && IsAtStageEntrance(player)) continue;
                if (player.ServerCatch() != CatchOutcome.None) caught++;
            }
            return caught;
        }
    }
}
