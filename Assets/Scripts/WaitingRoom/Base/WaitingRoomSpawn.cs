using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 대기실 진입 준비. 대기실 씬이 열릴 때(처음 진입·공연 후 복귀) 내 캐릭터를 자리 번호의 ① 로비 출발 위치로 옮기고,
    /// 줄 위 전용인 균형(<see cref="PlayerBalance"/>)을 끈다(켜 두면 서 있기만 해도 기울어 추락한다).
    /// 출발 위치는 같은 씬의 공용 <see cref="PlayerSpawnPoints"/>를 쓴다. 처음 생성될 때 위치는 <see cref="NetworkPlayer"/>가 직접 정한다.
    /// 이동은 소유자 화면에서 하고 위치 동기화가 다른 화면에 알린다.
    /// </summary>
    public sealed class WaitingRoomSpawn : MonoBehaviour
    {
        NetworkPlayer waitingSlot;
        /// <summary>이 화면에서 내 캐릭터의 대기실 출발 배치가 끝났다.</summary>
        public static event System.Action<NetworkPlayer> PlayerPlaced;
        void OnEnable() => NetworkPlayer.LocalSpawned += Spawned;

        void OnDisable() { NetworkPlayer.LocalSpawned -= Spawned; StopWaiting(); }

        void Start()
        {
            var local = NetworkPlayer.Local;
            if (local == null) return;
            DisableBalance(local);
            if (local.Slot == NetworkPlayer.NoSlot) { Spawned(local); return; }

            var mover = local.GetComponent<PlayerMover>();
            PlayerSpawnPoints.GetPose(local.Slot, mover.LaneSpacing, out Vector3 position, out Quaternion rotation);
            mover.SetStartPose(position, rotation);
            Placed(local);
        }

        void Spawned(NetworkPlayer player)
        {
            DisableBalance(player);
            StopWaiting();
            if (player.Slot != NetworkPlayer.NoSlot) Placed(player);
            else { waitingSlot = player; waitingSlot.SlotChanged += SlotAssigned; }
        }
        void SlotAssigned(int slot) { if (slot == NetworkPlayer.NoSlot) return; var player = waitingSlot; StopWaiting(); Placed(player); }
        void StopWaiting() { if (waitingSlot != null) waitingSlot.SlotChanged -= SlotAssigned; waitingSlot = null; }
        static void Placed(NetworkPlayer player)
        {
            if (PlayerPlaced != null)
                foreach (System.Action<NetworkPlayer> observer in PlayerPlaced.GetInvocationList())
                    try { observer(player); } catch (System.Exception error) { Debug.LogException(error); }
        }

        static void DisableBalance(NetworkPlayer player)
        {
            if (player.TryGetComponent(out PlayerBalance balance)) balance.SetBalanceActive(false);
        }
    }
}
