using CurtainCall.Campaign;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 대기실 통로 기믹의 공연 장치(CC-252·253, 2026-11-10 사용자: 새로 만들지 않고 공연 시스템·에셋을 그대로 가져온다).
    /// - 인간대포 공연 시스템(대포 좌석 없음)을 대기실 씬에 켜 둬, 운반 조작(트램펄린·휴대 절단기)을 공연 규칙 그대로 쓴다.
    /// - 연습실 자유 연습의 인간 대포 구간이 열리면(인간대포 해금 + 대포 리허설 이수, <see cref="PracticeRoomFreePlay"/>)
    ///   그 구간의 대포 2문을 이 시스템에 연결하고, 표시된 자리에 공연용 운반 트램펄린(RoundTrampolinePrepared)을 둔다. 그 전에는 없다(사용자 2026-11-13).
    /// - 인간대포 시스템·트램펄린은 씬당 1개라, 인간대포 리허설이 코스를 만들기 직전에 비우고(<see cref="ReleaseForRehearsal"/>)
    ///   리허설이 끝나면 원래 자리에 다시 만든다.
    /// 장치는 대기실 씬 안에 만들어 다른 공간으로 갈 때 씬과 함께 닫힌다.
    /// </summary>
    public sealed class WaitingRoomStageDevices : MonoBehaviour
    {
        const string TrampolinePrefab = "Prefabs/Objects/ETCs/HumanCannonArtKit/Prepared/RoundTrampolinePrepared";

        static WaitingRoomStageDevices instance;
        Transform room;
        GameObject systemRoot, trampoline;
        bool systemHasCannons, warned;
        float nextSearch;
        NetworkPlayer reservedCarrier;
        Transform reservationRoom;
        Vector3 reservationFeet;
        float resumeUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject(nameof(WaitingRoomStageDevices));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<WaitingRoomStageDevices>();
        }

        /// <summary>인간대포 리허설이 자기 코스(대포 시스템·트램펄린)를 만들기 직전에 부른다. 같은 프레임에 비워야 겹치지 않는다.</summary>
        public static void ReleaseForRehearsal() { if (instance != null) instance.Release(); }

        internal static void ReserveCarrierForRehearsal()
        {
            var devices = instance;
            if (devices == null) return;
            devices.reservedCarrier = null;
            if (devices.trampoline == null || !devices.trampoline.TryGetComponent(out HumanCannonTrampoline prop)) return;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned || player.OwnerClientId != prop.Carrier) continue;
                devices.reservedCarrier = player;
                devices.reservationRoom = devices.room;
                devices.reservationFeet = player.transform.position;
                devices.resumeUntil = 0f;
                break;
            }
        }

        void Update()
        {
            if (room == null && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 1f;
                room = WaitingRoomScenes.FindPracticeRoom();
            }
            if (room == null || WaitingRoomCannonRehearsal.IsActive) { Release(); return; }

            var freePlay = PracticeRoomFreePlay.Current;
            bool cannonLane = freePlay != null && freePlay.CannonLaneOpen;
            // 대포 목록은 켜진 시스템에서 바꿀 수 없다. 대포 구간이 열리거나 닫히면 시스템을 다시 만든다.
            if (systemRoot != null && systemHasCannons != cannonLane) Release();

            // 리허설 코스는 프레임 끝에 사라진다. 그 전에는 다시 만들지 않는다(씬당 1개).
            if (systemRoot == null)
            {
                if (HumanCannonSystem.Current != null) return;
                systemRoot = new GameObject("WaitingRoomCannonDevices");
                systemRoot.SetActive(false);
                SceneManager.MoveGameObjectToScene(systemRoot, room.gameObject.scene);
                var system = systemRoot.AddComponent<HumanCannonSystem>();
                if (cannonLane) system.ConfigurePractice(freePlay.Cannons, freePlay.transform.position.y);
                systemHasCannons = cannonLane;
                systemRoot.SetActive(true);
            }

            if (cannonLane && trampoline == null && HumanCannonTrampoline.Current == null) BuildTrampoline(freePlay.TrampolineSpot);
            else if (!cannonLane && trampoline != null) ReleaseTrampoline();
            RestoreReservedCarrier();
        }

        void RestoreReservedCarrier()
        {
            if (reservedCarrier == null) return;
            if (resumeUntil == 0f) resumeUntil = Time.unscaledTime + 6f;
            if (room != reservationRoom || !reservedCarrier.IsSpawned || Time.unscaledTime > resumeUntil)
            { reservedCarrier = null; return; }
            if (trampoline != null && trampoline.TryGetComponent(out HumanCannonTrampoline prop)
                && (reservedCarrier.transform.position - reservationFeet).sqrMagnitude <= 1f
                && prop.ServerRestoreRehearsalCarrier(reservedCarrier)) reservedCarrier = null;
        }

        void BuildTrampoline(Transform pose)
        {
            var prefab = Resources.Load<GameObject>(TrampolinePrefab);
            if (pose == null || prefab == null)
            {
                if (!warned) Debug.LogWarning($"[StageDevices] 연습실 트램펄린 자리 또는 공연 트램펄린({TrampolinePrefab})을 찾지 못했습니다.");
                warned = true;
                return;
            }
            trampoline = Instantiate(prefab, pose.position, pose.rotation);
            trampoline.name = "WaitingRoomCarryTrampoline";
            SceneManager.MoveGameObjectToScene(trampoline, room.gameObject.scene);
        }

        void ReleaseTrampoline()
        {
            if (trampoline != null) DestroyImmediate(trampoline);
            trampoline = null;
        }

        void Release()
        {
            ReleaseTrampoline();
            // 비활성화만으로는 HumanCannonSystem.Current가 남는다. 바로 없애야 리허설 시스템이 자리를 잡는다.
            if (systemRoot != null) DestroyImmediate(systemRoot);
            systemRoot = null;
        }
    }
}
