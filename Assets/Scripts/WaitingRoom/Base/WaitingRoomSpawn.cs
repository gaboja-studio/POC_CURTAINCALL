using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
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
        NetworkPlayer morningRevival;
        int revivalGeneration, revivalDay;
        /// <summary>이 화면에서 내 캐릭터의 대기실 출발 배치가 끝났다.</summary>
        public static event System.Action<NetworkPlayer> PlayerPlaced;
        public static int LastPlacedScene { get; private set; } = -1;
        public static NetworkPlayer LastPlacedPlayer { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPlacement() { LastPlacedScene = -1; LastPlacedPlayer = null; }
        void OnEnable()
        {
            NetworkPlayer.LocalSpawned += Spawned;
            NetworkPlayer.Revived += Revived;
        }

        void OnDisable()
        {
            NetworkPlayer.LocalSpawned -= Spawned;
            NetworkPlayer.Revived -= Revived;
            morningRevival = null;
            StopWaiting();
        }

        // Night and the following morning use the same loaded scene, so Start does not run again.
        // Revival and campaign/Normal updates travel separately; reconcile either arrival order locally.
        void Revived(NetworkPlayer player)
        {
            var run = CampaignRun.Instance;
            if (player == null || player != NetworkPlayer.Local || run == null || PracticeOwnsPlacement) return;
            var state = run.State;
            if (state.Phase != CampaignPhase.Dawn && !(state.Phase == CampaignPhase.RestTime && state.Rest == RestKind.Morning)) return;
            morningRevival = player;
            revivalGeneration = state.Generation;
            revivalDay = state.Day + (state.Phase == CampaignPhase.Dawn ? 1 : 0);
            TryPlaceMorningRevival();
        }

        void Update() => TryPlaceMorningRevival();

        static bool PracticeOwnsPlacement => WaitingRoomRehearsal.IsActive || WaitingRoomCannonRehearsal.IsActive;

        void TryPlaceMorningRevival()
        {
            if (morningRevival == null) return;
            var run = CampaignRun.Instance;
            if (run == null || morningRevival != NetworkPlayer.Local || !morningRevival.IsSpawned || PracticeOwnsPlacement)
            { morningRevival = null; return; }
            var state = run.State;
            if (state.Generation != revivalGeneration || state.Day > revivalDay
                || (state.Phase != CampaignPhase.Dawn && !(state.Phase == CampaignPhase.RestTime && state.Rest == RestKind.Morning)))
            { morningRevival = null; return; }
            if (state.Phase != CampaignPhase.RestTime || state.Day != revivalDay
                || morningRevival.State != PlayerState.Normal || morningRevival.Slot == NetworkPlayer.NoSlot) return;
            var player = morningRevival;
            morningRevival = null;
            DisableBalance(player);
            var mover = player.GetComponent<PlayerMover>();
            PlayerSpawnPoints.GetPose(player.Slot, mover.LaneSpacing, out Vector3 position, out Quaternion rotation);
            mover.SetStartPose(position, rotation);
            Placed(player);
        }

        void Start()
        {
            var local = NetworkPlayer.Local;
            if (local == null) return;
            DisableBalance(local);
            if (local.Slot == NetworkPlayer.NoSlot) { Spawned(local); return; }

            if (InInitialRehearsal()) { Placed(local); return; }

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
        void Placed(NetworkPlayer player)
        {
            LastPlacedScene = gameObject.scene.handle; LastPlacedPlayer = player;
            if (InInitialRehearsal()) PlaceOnPracticeFloor(player);
            if (PlayerPlaced != null)
                foreach (System.Action<NetworkPlayer> observer in PlayerPlaced.GetInvocationList())
                    try { observer(player); } catch (System.Exception error) { Debug.LogException(error); }
        }

        static bool InInitialRehearsal() => CampaignRun.Instance != null
            && CampaignRun.Instance.State.Phase == CampaignPhase.Rehearsal;

        void PlaceOnPracticeFloor(NetworkPlayer player)
        {
            if (player == null || !player.TryGetComponent(out PlayerMover mover)) return;
            var room = WaitingRoomScenes.FindPracticeRoom(gameObject.scene);
            var deck = room != null ? room.Find("LegacyRing/PracticeRing/DeckCollider") : null;
            var floor = deck != null ? deck.GetComponent<Collider>() : null;
            if (floor == null)
            {
                Debug.LogWarning("[WaitingRoom] 연습실 바닥을 찾지 못해 리허설 배치를 기다립니다.");
                return;
            }
            var bounds = floor.bounds;
            var x = bounds.center.x + Mathf.Max(0, player.Slot) * .8f;
            var z = bounds.center.z - Mathf.Min(14f, bounds.extents.z * .5f);
            if (!floor.Raycast(new Ray(new Vector3(x, bounds.max.y + 3f, z), Vector3.down), out var hit, 6f))
            {
                Debug.LogWarning("[WaitingRoom] 연습실 출발 위치 아래에 바닥이 없어 리허설 배치를 기다립니다.");
                return;
            }
            var position = hit.point + Vector3.up * .1f;
            mover.SetStartPose(position, Quaternion.LookRotation(Vector3.forward), true);
        }

        static void DisableBalance(NetworkPlayer player)
        {
            if (player.TryGetComponent(out PlayerBalance balance)) balance.SetBalanceActive(false);
        }
    }
}
