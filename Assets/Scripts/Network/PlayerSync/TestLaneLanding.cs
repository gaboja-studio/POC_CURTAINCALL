using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Network.PlayerSync
{
    /// <summary>
    /// 테스트 전용(외줄 코스 005가 대체): 내 캐릭터가 점프(제자리·옆줄) 후 줄이 아닌 곳에 착지하면 바로 추락시킨다.
    /// 줄 위치는 <see cref="lanes"/>의 옆 좌표(코스 오른쪽 방향)로 판단한다. 추락하면 004 래그돌·조작 잠금(테스트 HUD)이 이어진다.
    /// 지금은 내 컴퓨터에서 바로 추락시키고, 호스트 판정 통로(⑤)가 생기면 그쪽으로 요청하게 바꾼다.
    /// </summary>
    public sealed class TestLaneLanding : MonoBehaviour
    {
        [Tooltip("줄 위치. 각 Transform의 옆 좌표가 한 줄이다(보통 출발 위치).")]
        [SerializeField] Transform[] lanes;

        [Tooltip("줄 중심에서 이 거리(m) 안이면 줄 위로 본다.")]
        [SerializeField, Min(0.01f)] float tolerance = 0.25f;

        PlayerMover mover;
        PlayerBalance balance;

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

        void Bind(NetworkPlayer player)
        {
            Unbind();
            mover = player.GetComponent<PlayerMover>();
            balance = player.GetComponent<PlayerBalance>();
            mover.AirborneChanged += HandleAirborne;
        }

        void Unbind()
        {
            if (mover != null) mover.AirborneChanged -= HandleAirborne;
            mover = null;
            balance = null;
        }

        void HandleAirborne(bool airborne)
        {
            // 착지 알림 때는 CurrentJump가 아직 남아 있다. 점프 없이 떨어진 경우(생성 직후 등)는 보지 않는다
            if (airborne || balance == null) return;
            if (mover.CurrentJump != JumpKind.InPlace && mover.CurrentJump != JumpKind.Lane) return;
            if (!IsOnLane(mover.transform.position)) balance.ForceFall();
        }

        bool IsOnLane(Vector3 position)
        {
            if (lanes == null || lanes.Length == 0) return true;
            Vector3 right = mover.CourseRight;
            float side = Vector3.Dot(position, right);
            foreach (var lane in lanes)
                if (lane != null && Mathf.Abs(Vector3.Dot(lane.position, right) - side) <= tolerance) return true;
            return false;
        }
    }
}
