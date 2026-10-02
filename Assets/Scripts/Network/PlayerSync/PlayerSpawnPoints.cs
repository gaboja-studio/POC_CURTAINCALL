using UnityEngine;

namespace CurtainCall.Network.PlayerSync
{
    /// <summary>
    /// 씬에 두는 플레이어 출발 위치 목록. 자리 번호(<see cref="NetworkPlayer.Slot"/>) 순서대로 쓴다.
    /// 씬에 없거나 칸이 모자라면 원점에서 옆으로 줄 간격(<see cref="CurtainCall.Player.PlayerMover.LaneSpacing"/>)만큼 나란히 세운다.
    /// </summary>
    public sealed class PlayerSpawnPoints : MonoBehaviour
    {
        [Tooltip("자리 번호 0, 1, 2, 3 순서의 출발 위치. 방향(전진)도 이 Transform을 따른다.")]
        [SerializeField] Transform[] points;

        static PlayerSpawnPoints current;

        void OnEnable() => current = this;

        void OnDisable()
        {
            if (current == this) current = null;
        }

        /// <summary>자리 번호의 출발 위치·방향. 목록이 없으면 <paramref name="fallbackSpacing"/> 간격으로 나란히.</summary>
        public static void GetPose(int slot, float fallbackSpacing, out Vector3 position, out Quaternion rotation)
        {
            if (current == null) current = FindAnyObjectByType<PlayerSpawnPoints>();

            if (current != null && current.points != null && slot >= 0 && slot < current.points.Length && current.points[slot] != null)
            {
                Transform point = current.points[slot];
                position = point.position;
                rotation = point.rotation;
                return;
            }

            position = Vector3.right * ((slot - 1.5f) * fallbackSpacing);
            rotation = Quaternion.identity;
        }
    }
}
