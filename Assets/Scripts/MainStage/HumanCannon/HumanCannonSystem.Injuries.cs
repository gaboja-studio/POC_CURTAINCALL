using System;
using System.Collections.Generic;
using CurtainCall.Damage;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public sealed partial class HumanCannonSystem
    {
        [Tooltip("기존 코스 전체에 적용하는 부상 시험값. 공통 설정/외줄 정책은 수정하지 않는다.")]
        [SerializeField] HumanCannonInjurySettings injuries = new();
        readonly Dictionary<BodyDamageEffects, BodyPenaltyPolicy> previousPolicies = new();
        readonly List<BodyDamageEffects> removedPolicies = new();
        readonly CannonInteractionDelay injuryInteractions = new();
        HumanCannonBodyPenalty injuryPolicy;
        public HumanCannonInjurySettings Injuries => injuries;

        void CreateInjuryPolicy()
        {
            injuryPolicy = ScriptableObject.CreateInstance<HumanCannonBodyPenalty>();
            injuryPolicy.hideFlags = HideFlags.DontSave;
            injuryPolicy.Initialize(injuries);
        }

        void BindInjuryPolicy(NetworkPlayer target)
        {
            if (target == null || !target.IsSpawned || !target.TryGetComponent(out BodyDamageEffects effects)
                || previousPolicies.ContainsKey(effects)) return;
            previousPolicies.Add(effects, effects.Policy);
            effects.SetPolicy(injuryPolicy);
        }

        void UpdateInjuryPolicies()
        {
            // 원격 플레이어도 같은 정책을 사용해야 호스트의 잡기/탑승 거리 검사가 일치한다.
            foreach (var target in NetworkPlayer.All) BindInjuryPolicy(target);
            removedPolicies.Clear();
            foreach (var pair in previousPolicies)
                if (pair.Key == null || !pair.Key.TryGetComponent(out NetworkPlayer target) || !target.IsSpawned)
                {
                    RestoreInjuryPolicy(pair.Key, pair.Value);
                    removedPolicies.Add(pair.Key);
                }
            foreach (var key in removedPolicies) previousPolicies.Remove(key);
        }

        void RestoreInjuryPolicy(BodyDamageEffects effects, BodyPenaltyPolicy previous)
        {
            // 그 사이 다른 콘텐츠가 정식으로 정책을 바꿨다면 덮어쓰지 않는다.
            if (effects != null && effects.Policy == injuryPolicy) effects.SetPolicy(previous);
        }

        void ReleaseInjuryPolicies()
        {
            injuryInteractions.Clear();
            foreach (var pair in previousPolicies) RestoreInjuryPolicy(pair.Key, pair.Value);
            previousPolicies.Clear();
            removedPolicies.Clear();
            if (injuryPolicy != null) Destroy(injuryPolicy);
            injuryPolicy = null;
        }

        internal bool DelayInjuredInteraction(NetworkPlayer target, int device, uint revision,
            Func<bool> isValid, Action complete)
        {
            var condition = target.GetComponent<PlayerCondition>();
            float delay = injuries.InteractionSeconds(condition != null && condition.IsOneArm);
            if (delay <= 0f) { injuryInteractions.Cancel(target.OwnerClientId); return false; }
            injuryInteractions.Schedule(target.OwnerClientId, device, revision, Time.timeAsDouble, delay, isValid, complete);
            return true;
        }

        internal bool HasPendingTrampolineGrab(NetworkPlayer target, uint revision) =>
            target != null && injuryInteractions.HasPending(target.OwnerClientId, -1, revision);
    }
}
