using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// 외줄에서는 현재 균형값과 모델 기울기를 같은 프레임에 맞춘다.
    /// 공용 기울기 연출을 잠시 맡고 코스를 떠나면 원래 enabled 상태를 복원한다.
    /// 모델 슬롯만 돌리며, 목마 추종은 이 프레임의 슬롯 방향을 읽는다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)] // 균형·공용 연출 뒤, PiggybackFollow(1000) 앞
    public sealed class TightropeBalancePoseView : MonoBehaviour
    {
        sealed class Binding
        {
            public PlayerBalance Balance;
            public PlayerModelSlot Model;
            public BalanceTiltView PreviousView;
            public bool PreviousEnabled;
        }

        readonly Dictionary<NetworkPlayer, Binding> bindings = new();
        readonly List<NetworkPlayer> removed = new();
        TightropeCourse course;

        bool IsActive => course != null && course.IsTraversalActive;

        void Awake() => course = GetComponent<TightropeCourse>();

        void Update()
        {
            if (!IsActive) { Release(); return; }
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || !player.isActiveAndEnabled) continue;
                if (!bindings.TryGetValue(player, out var binding))
                {
                    if (!player.TryGetComponent(out PlayerBalance balance)
                        || !player.TryGetComponent(out PlayerModelSlot model)
                        || !player.TryGetComponent(out BalanceTiltView previous)) continue;
                    binding = new Binding { Balance = balance, Model = model,
                        PreviousView = previous, PreviousEnabled = previous.enabled };
                    bindings.Add(player, binding);
                }
                // LateUpdate의 슬롯 회전은 한 연출만 소유한다.
                if (binding.PreviousView != null) binding.PreviousView.enabled = false;
            }

            removed.Clear();
            foreach (var pair in bindings)
                if (pair.Key == null || !pair.Key.IsSpawned || !pair.Key.isActiveAndEnabled)
                {
                    Restore(pair.Value);
                    removed.Add(pair.Key);
                }
            foreach (var player in removed) bindings.Remove(player);
        }

        void LateUpdate()
        {
            if (!IsActive) { Release(); return; }
            float maximumAngle = GameSettings.Base.Presentation.MaxTiltAngle;
            foreach (var pair in bindings)
            {
                var player = pair.Key;
                var binding = pair.Value;
                if (player == null || !player.IsSpawned || binding.Balance == null || binding.Model == null
                    || binding.Balance.HasFallen || player.State == PlayerState.Fallen || player.State == PlayerState.Retired)
                    continue; // 래그돌로 분리된 모델과 마지막 추락 자세는 그대로 둔다.
                float angle = binding.Balance.IsActive
                    ? binding.Balance.Value / PlayerBalance.MaxValue * maximumAngle : 0f;
                binding.Model.Slot.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
        }

        static void Restore(Binding binding)
        {
            if (binding.PreviousView != null) binding.PreviousView.enabled = binding.PreviousEnabled;
        }

        void Release()
        {
            foreach (var binding in bindings.Values) Restore(binding);
            bindings.Clear();
            removed.Clear();
        }

        void OnDisable() => Release();
    }
}
