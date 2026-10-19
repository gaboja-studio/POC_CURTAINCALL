using System;
using System.Collections.Generic;
using CurtainCall.Damage;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// 내 캐릭터가 서 있는 곳에 맞춰 조작을 바꾼다. 줄 위에 서는 순간 줄 중앙으로 옆 위치를 맞추고 줄타기 조작 + 균형 켜기(중앙에서 시작),
    /// 플랫폼(시작·도착)에 서면 일반 이동(8방향·점프) + 균형 끄기. 공중·추락 중에는 바꾸지 않는다.
    /// 조작은 소유자만 하므로 내 캐릭터만 처리한다. 균형 켜짐 여부는 007이 다른 화면에 공유한다.
    /// 코스 프리팹의 <see cref="TightropeCourse"/>와 같은 오브젝트에 둔다.
    /// </summary>
    [RequireComponent(typeof(TightropeCourse))]
    [DefaultExecutionOrder(-60)] // 입력(-100) 다음, 조작 규칙(-50)·균형 계산(0) 전에 표면 전환
    public sealed class CourseControlSwitcher : MonoBehaviour
    {
        [Tooltip("줄 위 조작 규칙. 비우면 기본 외줄 규칙(TightropeControlScheme).")]
        [SerializeField] PlayerControlScheme ropeScheme;

        [Tooltip("플랫폼 조작 규칙. 비우면 공용 플레이어의 시작 → 씬 → 기본 이동 규칙을 그대로 사용한다.")]
        [SerializeField] PlayerControlScheme platformScheme;

        TightropeCourse course;
        NetworkPlayer player;
        PlayerController controller;
        PlayerMover mover;
        PlayerBalance balance;
        bool? onRope; // 아직 정하지 않았으면 null
        PlayerControlScheme generatedRopeScheme;
        PlayerControlScheme appliedScheme;
        bool ownsControls;
        TightropeBodyPenalty injuryPolicy;
        readonly Dictionary<BodyDamageEffects, BodyPenaltyPolicy> previousPolicies = new();
        readonly List<BodyDamageEffects> removedPolicies = new();

        /// <summary>내 캐릭터가 줄타기 조작 중인지.</summary>
        public bool IsLocalOnRope => onRope == true;

        /// <summary>내 캐릭터의 조작이 바뀌었을 때. 인자는 줄 위인지.</summary>
        public event Action<bool> LocalModeChanged;

        void Awake()
        {
            course = GetComponent<TightropeCourse>();
            if (ropeScheme == null) ropeScheme = generatedRopeScheme = CreateDefault<TightropeControlScheme>();
        }

        static PlayerControlScheme CreateDefault<T>() where T : PlayerControlScheme
        {
            var scheme = ScriptableObject.CreateInstance<T>();
            scheme.name = $"{typeof(T).Name} (기본)";
            scheme.hideFlags = HideFlags.DontSave;
            return scheme;
        }

        void OnEnable()
        {
            NetworkPlayer.LocalSpawned += Bind;
            NetworkPlayer.LocalDespawned += Unbind;
            if (NetworkPlayer.Local != null) Bind(NetworkPlayer.Local);
        }

        void OnDisable()
        {
            NetworkPlayer.LocalSpawned -= Bind;
            NetworkPlayer.LocalDespawned -= Unbind;
            Unbind();
            ReleaseInjuryPolicies();
        }

        void OnDestroy()
        {
            DestroyGenerated(generatedRopeScheme);
            DestroyGenerated(injuryPolicy);
        }

        static void DestroyGenerated(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        void Bind(NetworkPlayer local)
        {
            Unbind();
            player = local;
            if (local == null) return;
            controller = local.GetComponent<PlayerController>();
            mover = local.GetComponent<PlayerMover>();
            balance = local.GetComponent<PlayerBalance>();
            // 공용 프리팹의 activeOnStart는 true다. 첫 접지/공연 시작을 기다리며
            // 플랫폼에서 균형 사망하지 않도록 LocalSpawned 때 기본 상태부터 정리한다.
            SuppressUnassignedBalance();
        }

        bool StillOwnsControls => ownsControls && (controller == null || (appliedScheme != null
            ? controller.HasAssignedScheme && controller.Scheme == appliedScheme
            : !controller.HasAssignedScheme));

        bool HasExternalControls => controller != null && controller.HasAssignedScheme && !StillOwnsControls;

        void ReleaseControls()
        {
            // 다른 콘텐츠가 이후에 조작을 지정했다면 그 조작·이동·균형 상태를 보존한다.
            if (StillOwnsControls)
            {
                if (mover != null) mover.ClearPendingInput();
                if (balance != null)
                {
                    balance.SetCorrectionInput(0f);
                    balance.SetBalanceActive(false);
                }
                if (controller != null) controller.SetScheme(null);
                else if (mover != null) mover.FreeMovement = true;
            }
            ownsControls = false;
            appliedScheme = null;
            onRope = null;
        }

        void Unbind()
        {
            ReleaseControls();
            player = null;
            controller = null;
            mover = null;
            balance = null;
        }

        void Update()
        {
            if (course == null || !course.isActiveAndEnabled)
            {
                Unbind();
                ReleaseInjuryPolicies();
                return;
            }
            if (IsCourseActive()) UpdateInjuryPolicies();
            else ReleaseInjuryPolicies();
            if (player != NetworkPlayer.Local) Bind(NetworkPlayer.Local);
            UpdateSurfaceMode();
        }

        void UpdateSurfaceMode()
        {
            if (player == null || mover == null) return;
            if (!course.IsTraversalActive)
            {
                ReleaseControls();
                SuppressUnassignedBalance();
                return;
            }
            if (HasExternalControls)
            {
                ReleaseControls();
                return;
            }
            if (player.State == PlayerState.Fallen || player.State == PlayerState.Retired
                || (balance != null && balance.HasFallen)) return; // 도착 완료는 계속 바꾼다(세리머니 공간은 일반 이동)

            var stack = PiggybackSystem.Current;
            if (IsCourseActive() && stack != null && stack.isActiveAndEnabled && stack.GetBelow(player) != null)
            {
                // 플랫폼에서 올라탄 위층도 균형을 써야 한다. 위치·공중 상태는 Follow가 맡는다.
                if (onRope != true) Apply(true);
                return;
            }
            // LateUpdate의 목마 분리 점프는 다음 이동 Update 전에 CurrentJump부터 바뀐다.
            // 그 사이 머리 높이를 일반 바닥으로 오인해 봉 잡기 조작을 빼지 않는다.
            if (mover.IsAirborne || player.IsAirborne) return;
            if (!mover.Simulated) return; // 위치를 다른 곳에 맡긴 동안(목마 위층)은 조작을 바꾸지 않는다

            bool rope = course.TryGetSupportingRope(mover, out int lane);
            if (onRope == rope)
            {
                // 공용 재시작·초기화 뒤에도 일반 바닥에서는 균형 계산을 시작하지 않는다.
                if (!rope) SuppressUnassignedBalance();
                return;
            }
            if (rope) SnapToLane(lane);
            Apply(rope);
        }

        void SuppressUnassignedBalance()
        {
            if (course == null || !course.isActiveAndEnabled || HasExternalControls || player == null
                || player.State != PlayerState.Normal || balance == null || balance.HasFallen || !balance.IsActive) return;
            balance.SetCorrectionInput(0f);
            balance.SetBalanceActive(false);
        }

        /// <summary>줄 중앙으로 옆 위치만 옮긴다(앞뒤·높이는 그대로). 위치는 소유자 이동이므로 다른 화면에는 NetworkTransform으로 전해진다.</summary>
        void SnapToLane(int lane)
        {
            Vector3 position = player.transform.position;
            Vector3 offset = course.GetLanePosition(lane, course.GetDistance(position)) - position;
            offset.y = 0f;
            if (offset.sqrMagnitude > 0.000001f) mover.Body.Move(offset);
        }

        void Apply(bool rope)
        {
            mover.ClearPendingInput();
            if (balance != null) balance.SetCorrectionInput(0f);
            if (InputGate.IsBlocked && player.TryGetComponent(out PlayerInputReader reader))
                reader.ClearPendingInput();
            onRope = rope;
            appliedScheme = rope ? ropeScheme : platformScheme;
            ownsControls = true;
            if (controller != null) controller.SetScheme(appliedScheme);
            mover.FreeMovement = !rope;
            if (balance != null) balance.SetBalanceActive(rope);
            LocalModeChanged?.Invoke(rope);
        }

        bool IsCourseActive() => course != null && course.IsGameplayActive;

        void BindInjuryPolicy(BodyDamageEffects effects)
        {
            if (effects == null || previousPolicies.ContainsKey(effects)) return;
            if (injuryPolicy == null)
            {
                injuryPolicy = ScriptableObject.CreateInstance<TightropeBodyPenalty>();
                injuryPolicy.hideFlags = HideFlags.DontSave;
            }
            previousPolicies.Add(effects, effects.Policy);
            effects.SetPolicy(injuryPolicy);
        }

        void UpdateInjuryPolicies()
        {
            // 원격 플레이어도 적용해야 호스트의 목마 거리 판정에 같은 부상 배율이 쓰인다.
            foreach (var target in NetworkPlayer.All)
                if (target != null && target.IsSpawned && target.TryGetComponent(out BodyDamageEffects effects))
                    BindInjuryPolicy(effects);
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
            if (effects != null && effects.Policy == injuryPolicy) effects.SetPolicy(previous);
        }

        void ReleaseInjuryPolicies()
        {
            foreach (var pair in previousPolicies) RestoreInjuryPolicy(pair.Key, pair.Value);
            previousPolicies.Clear();
            removedPolicies.Clear();
        }
    }
}
