using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// 내 캐릭터의 옆줄 점프(Q/E 홀드 + Space)에 코스 규칙을 붙인다. 아래 중 하나라도 아니면 입력을 무시한다(아무 일도 없음).
    /// 뛰는 지점과 착지 예상 지점이 모두 옆줄 이동 가능 구간 안, 착지 예상 거리에 그 방향 옆 줄이 있고 막히지 않음(불타는 구간 포함).
    /// 판정은 조작하는 소유자 화면에서만 하고, 결과 움직임은 007 공유를 그대로 쓴다. 씬에 코스(<see cref="TightropeCourse.Current"/>)가 없으면 막지 않는다.
    /// 씬에 하나 둔다(보통 코스 옆).
    /// </summary>
    public sealed class RopeLaneJumpRules : MonoBehaviour
    {
        PlayerMover mover;

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
            mover = local.GetComponent<PlayerMover>();
            if (mover != null) mover.LaneJumpFilter = CanLaneJump;
        }

        void Unbind()
        {
            if (mover != null && mover.LaneJumpFilter == CanLaneJump) mover.LaneJumpFilter = null;
            mover = null;
        }

        /// <summary>내 캐릭터가 지금 그 방향(-1 왼쪽, +1 오른쪽)으로 옆줄 점프해도 되는지.</summary>
        public bool CanLaneJump(int direction)
        {
            var course = TightropeCourse.Current;
            if (course == null || mover == null) return true;

            Vector3 position = mover.transform.position;
            float from = course.GetDistance(position);
            float to = course.GetDistance(mover.GetLaneLandingPosition(direction));
            if (!course.IsLaneChangeAllowed(from) || !course.IsLaneChangeAllowed(to)) return false;

            // 옆 위치는 지금 그대로, 앞뒤만 착지 예상 거리로 옮겨 그 거리의 옆 줄을 찾는다
            Vector3 along = position + course.Forward * (to - from);
            return course.FindLandingLane(along, direction) != TightropeCourse.NoLane;
        }
    }
}
