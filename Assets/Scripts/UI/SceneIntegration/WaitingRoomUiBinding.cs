using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.WaitingRoom;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>공간의 참조만 소유한다. 공통 HUD에는 게임 판정이나 입력을 두지 않는다.</summary>
    public sealed class WaitingRoomUiBinding : MonoBehaviour
    {
        [SerializeField] StageEntranceGather gather;
        [SerializeField] ExplorationTimerAnnouncer announcer;
        PlayerMover offlinePlayer;
        public StageEntranceGather Gather => gather;
        public ExplorationTimerAnnouncer Announcer => announcer;
        public void Configure(StageEntranceGather area, ExplorationTimerAnnouncer broadcasts) { gather = area; announcer = broadcasts; }
        void OnEnable() => CommonUiSceneLoader.Register(this);
        void OnDisable() => CommonUiSceneLoader.Unregister(this);

        public bool LocalInGatherArea
        {
            get
            {
                if (gather == null || !gather.isActiveAndEnabled) return false;
                Transform actor;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    var local = NetworkPlayer.Local;
                    if (local == null || local.State == PlayerState.Fallen || local.State == PlayerState.Retired) return false;
                    actor = local.transform;
                }
                else
                {
                    if (offlinePlayer == null)
                    {
                        var input = FindFirstObjectByType<PlayerInputReader>();
                        offlinePlayer = input != null ? input.GetComponent<PlayerMover>() : FindFirstObjectByType<PlayerMover>();
                    }
                    if (offlinePlayer == null || !offlinePlayer.isActiveAndEnabled) return false;
                    actor = offlinePlayer.transform;
                }
                var box = gather.GetComponent<BoxCollider>();
                return box != null && box.bounds.Contains(actor.position + Vector3.up * .5f);
            }
        }
    }
}
