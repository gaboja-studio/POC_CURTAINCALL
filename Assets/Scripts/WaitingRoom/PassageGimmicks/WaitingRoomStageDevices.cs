using CurtainCall.Campaign;
using CurtainCall.HumanCannon;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 대기실 통로 기믹의 공연 장치(CC-252·253, 2026-11-10 사용자: 새로 만들지 않고 공연 시스템·에셋을 그대로 가져온다).
    /// - 인간대포 공연 시스템(대포 좌석 없음)을 대기실 씬에 켜 둬, 운반 조작(트램펄린·휴대 절단기)을 공연 규칙 그대로 쓴다.
    /// - 인간대포가 해금되면 연습실의 장식 트램펄린 자리에 공연용 운반 트램펄린(RoundTrampolinePrepared)을 둔다. 해금 전에는 없다.
    /// - 인간대포 시스템·트램펄린은 씬당 1개라, 인간대포 리허설이 코스를 만들기 직전에 비우고(<see cref="ReleaseForRehearsal"/>)
    ///   리허설이 끝나면 원래 자리에 다시 만든다.
    /// 장치는 대기실 씬 안에 만들어 다른 공간으로 갈 때 씬과 함께 닫힌다.
    /// </summary>
    public sealed class WaitingRoomStageDevices : MonoBehaviour
    {
        const string TrampolinePrefab = "Prefabs/Objects/ETCs/HumanCannonArtKit/Prepared/RoundTrampolinePrepared";
        const string PracticeTrampolinePath = "LegacyRing/PracticeLanes/PracticeTrampoline";

        static WaitingRoomStageDevices instance;
        Transform room;
        GameObject systemRoot, trampoline, practiceTrampoline;
        bool hidPracticeTrampoline, warned;
        float nextSearch;

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

        void Update()
        {
            if (room == null && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 1f;
                room = WaitingRoomScenes.FindPracticeRoom();
                practiceTrampoline = room != null ? room.Find(PracticeTrampolinePath)?.gameObject : null;
            }
            if (room == null || WaitingRoomCannonRehearsal.IsActive) { Release(); return; }

            // 리허설 코스는 프레임 끝에 사라진다. 그 전에는 다시 만들지 않는다(씬당 1개).
            if (systemRoot == null)
            {
                if (HumanCannonSystem.Current != null) return;
                systemRoot = new GameObject("WaitingRoomCannonDevices");
                SceneManager.MoveGameObjectToScene(systemRoot, room.gameObject.scene);
                systemRoot.AddComponent<HumanCannonSystem>();
            }

            bool unlocked = CampaignRun.Instance != null && CampaignRun.Instance.State.CannonUnlocked;
            if (unlocked && trampoline == null && HumanCannonTrampoline.Current == null) BuildTrampoline();
            else if (!unlocked && trampoline != null) ReleaseTrampoline();
            // 장식 트램펄린은 해금 연출·리허설 정리가 다시 켤 수 있다. 운반 트램펄린이 있는 동안 숨긴다.
            if (trampoline != null && practiceTrampoline != null && practiceTrampoline.activeSelf)
            {
                practiceTrampoline.SetActive(false);
                hidPracticeTrampoline = true;
            }
        }

        void BuildTrampoline()
        {
            var prefab = Resources.Load<GameObject>(TrampolinePrefab);
            if (practiceTrampoline == null || prefab == null)
            {
                if (!warned) Debug.LogWarning($"[StageDevices] 연습실 트램펄린 자리({PracticeTrampolinePath}) 또는 공연 트램펄린({TrampolinePrefab})을 찾지 못했습니다.");
                warned = true;
                return;
            }
            var pose = practiceTrampoline.transform;
            trampoline = Instantiate(prefab, pose.position, pose.rotation);
            trampoline.name = "WaitingRoomCarryTrampoline";
            SceneManager.MoveGameObjectToScene(trampoline, room.gameObject.scene);
        }

        void ReleaseTrampoline()
        {
            if (trampoline != null) DestroyImmediate(trampoline);
            trampoline = null;
            if (hidPracticeTrampoline && practiceTrampoline != null) practiceTrampoline.SetActive(true);
            hidPracticeTrampoline = false;
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
