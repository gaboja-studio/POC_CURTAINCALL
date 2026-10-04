using System;
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
    public sealed class CourseControlSwitcher : MonoBehaviour
    {
        [Tooltip("줄 위 조작 규칙. 비우면 기본 외줄 규칙(TightropeControlScheme).")]
        [SerializeField] PlayerControlScheme ropeScheme;

        [Tooltip("플랫폼 조작 규칙. 비우면 기본 플랫폼 규칙(PlatformControlScheme).")]
        [SerializeField] PlayerControlScheme platformScheme;

        TightropeCourse course;
        NetworkPlayer player;
        PlayerController controller;
        PlayerMover mover;
        PlayerBalance balance;
        bool? onRope; // 아직 정하지 않았으면 null

        /// <summary>내 캐릭터가 줄타기 조작 중인지.</summary>
        public bool IsLocalOnRope => onRope == true;

        /// <summary>내 캐릭터의 조작이 바뀌었을 때. 인자는 줄 위인지.</summary>
        public event Action<bool> LocalModeChanged;

        void Awake()
        {
            course = GetComponent<TightropeCourse>();
            if (ropeScheme == null) ropeScheme = CreateDefault<TightropeControlScheme>();
            if (platformScheme == null) platformScheme = CreateDefault<PlatformControlScheme>();
        }

        static PlayerControlScheme CreateDefault<T>() where T : PlayerControlScheme
        {
            var scheme = ScriptableObject.CreateInstance<T>();
            scheme.name = $"{typeof(T).Name} (기본)";
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
        }

        void Bind(NetworkPlayer local)
        {
            Unbind();
            player = local;
            controller = local.GetComponent<PlayerController>();
            mover = local.GetComponent<PlayerMover>();
            balance = local.GetComponent<PlayerBalance>();
        }

        void Unbind()
        {
            player = null;
            controller = null;
            mover = null;
            balance = null;
            onRope = null;
        }

        void Update()
        {
            if (player == null || mover == null) return;
            if (player.State == PlayerState.Fallen || (balance != null && balance.HasFallen)) return; // 도착 완료는 계속 바꾼다(세리머니 공간은 일반 이동)
            if (mover.IsAirborne) return; // 줄·플랫폼에 선 순간에만 바꾼다
            if (!mover.Simulated) return; // 위치를 다른 곳에 맡긴 동안(목마 위층)은 조작을 바꾸지 않는다

            bool rope = course.IsOnRope(player.transform.position, mover.BodyRadius, out int lane);
            if (onRope == rope) return;
            if (rope) SnapToLane(lane);
            Apply(rope);
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
            onRope = rope;
            if (controller != null) controller.SetScheme(rope ? ropeScheme : platformScheme);
            mover.FreeMovement = !rope;
            if (balance != null) balance.SetBalanceActive(rope);
            LocalModeChanged?.Invoke(rope);
        }
    }
}
