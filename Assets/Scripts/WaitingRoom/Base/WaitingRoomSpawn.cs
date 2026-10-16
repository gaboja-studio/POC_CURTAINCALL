using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 대기실 ① 로비 스폰. 대기실 씬이 열릴 때(처음 진입·공연 후 복귀) 내 캐릭터를 자리 번호의 로비 출발 위치로 옮긴다.
    /// 출발 위치는 같은 씬의 공용 <see cref="PlayerSpawnPoints"/>를 쓴다. 처음 생성될 때는 <see cref="NetworkPlayer"/>가 직접 옮기므로
    /// 여기서는 이미 생성된 캐릭터가 씬 전환으로 넘어온 경우만 맡는다. 이동은 소유자 화면에서 하고 위치 동기화가 다른 화면에 알린다.
    /// </summary>
    public sealed class WaitingRoomSpawn : MonoBehaviour
    {
        void Start()
        {
            var local = NetworkPlayer.Local;
            if (local == null || local.Slot == NetworkPlayer.NoSlot) return;

            var mover = local.GetComponent<PlayerMover>();
            PlayerSpawnPoints.GetPose(local.Slot, mover.LaneSpacing, out Vector3 position, out Quaternion rotation);
            mover.SetStartPose(position, rotation);
        }
    }
}
