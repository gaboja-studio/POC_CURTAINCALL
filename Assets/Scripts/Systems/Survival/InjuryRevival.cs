using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.Survival
{
    /// <summary>
    /// 런 진행(<see cref="CampaignRun.StepChanged"/>)을 받아 호스트에서 부상·생존 규칙을 적용한다. 연결 약속: 통합 meta 2026-10-14 (2).
    /// - 쉬는 시간·밤 정비 시작: 사망자를 외팔 상태로 부활(<see cref="NetworkPlayer.ServerReviveAllDead"/>). 생존자는 부상 그대로.
    /// - 쉬는 시간 만료: <see cref="RestTimeoutCatch"/>로 무대 입구 밖 인원을 잡힘 처리한다(추격자 구현 전 임시, 교체 가능).
    /// - 공연 시작: 쉬는 시간에 잡혀 사망한 사람을 외팔 상태로 부활해 출연시킨다(보상 박탈 없음, 2026-10-28).
    /// - 새 세대 도입(세대 2+): 모두 온전한 몸(<see cref="NetworkPlayer.ServerRestoreEveryone"/>).
    /// - 지금 장소(<see cref="SurvivalRules.ServerPlace"/>)의 기본값도 시간대로 맞춘다(공연 = 공연, 쉬는 시간·밤 = 탐색, 탈출 = 탈출로). 공간 연결(032)이 더 자세히 정해도 된다.
    /// 대기실로 옮기는 일은 공간 연결(032)이 한다. 게임을 시작하면 스스로 만들어져 씬이 바뀌어도 남는다.
    /// </summary>
    public sealed class InjuryRevival : MonoBehaviour
    {
        CampaignRun hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject(nameof(InjuryRevival));
            DontDestroyOnLoad(go);
            go.AddComponent<InjuryRevival>();
        }

        /// <summary>
        /// 호스트: 시간대 시작 규칙을 적용한다. 런 진행이 알릴 때 자동으로 부르며, 테스트 씬에서 시간대 시작을 흉내 낼 때도 쓴다.
        /// </summary>
        public static void ServerApplyStep(CampaignSnapshot step)
        {
            switch (step.Phase)
            {
                case CampaignPhase.Performance:
                    SurvivalRules.ServerPlace = SurvivalPlace.Performance;
                    // 런 실패는 도입/로비를 건너뛰므로 첫 공연에서 새 세대의 몸을 복구한다.
                    if (step.Generation > 1 && step.Day == 1 && step.PerformanceIndex == 0 && step.LastEnd != RunEndReason.None)
                        NetworkPlayer.ServerRestoreEveryone();
                    // 공연 사망자는 이미 쉬는 시간·밤 정비에 부활했으므로 여기 남은 사망자는 쉬는 시간에 잡혀 죽은 사람이다.
                    int caught = NetworkPlayer.ServerReviveAllDead();
                    if (caught > 0) Debug.Log($"[Survival] {CampaignRun.Describe(step)} 시작: 잡혀 사망한 {caught}명 외팔 상태로 부활 출연");
                    break;
                case CampaignPhase.RestTime:
                case CampaignPhase.Night:
                    SurvivalRules.ServerPlace = SurvivalPlace.Exploration;
                    int revived = NetworkPlayer.ServerReviveAllDead();
                    if (revived > 0) Debug.Log($"[Survival] {CampaignRun.Describe(step)} 시작: 사망자 {revived}명 외팔 상태로 부활");
                    break;
                case CampaignPhase.Escape:
                    SurvivalRules.ServerPlace = SurvivalPlace.EscapeRoute;
                    break;
                case CampaignPhase.Intro when step.IsGenerationChangeIntro:
                    NetworkPlayer.ServerRestoreEveryone();
                    Debug.Log($"[Survival] 세대 {step.Generation} 도입: 모두 온전한 몸으로");
                    break;
            }
        }

        void Update()
        {
            var run = CampaignRun.Instance;
            if (run == hooked) return;
            Unhook();
            hooked = run;
            if (hooked == null) return;
            hooked.StepChanged += HandleStep;
            hooked.RestExpired += HandleRestExpired;
        }

        void OnDestroy() => Unhook();

        void Unhook()
        {
            if (hooked != null)
            {
                hooked.StepChanged -= HandleStep;
                hooked.RestExpired -= HandleRestExpired;
            }
            hooked = null;
        }

        void HandleStep(CampaignSnapshot step)
        {
            if (hooked != null && hooked.HasAuthority) ServerApplyStep(step);
        }

        // 공연으로 넘어가기 전(아직 탐색 장소)에 잡힘을 정한다.
        void HandleRestExpired(CampaignSnapshot step)
        {
            if (hooked != null && hooked.HasAuthority) RestTimeoutCatch.ServerHandleExpired(step);
        }
    }
}
