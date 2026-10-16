using System.Collections;
using System.Linq;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.HumanCannon
{
    /// <summary>HumanCannon 테스트 씬 전용. 원본과 오른쪽 연습 복사본의 시스템을 동시에 켜지 않는다.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed partial class HumanCannonSoloPractice : MonoBehaviour
    {
        [SerializeField] GameObject[] originalRoots;
        [SerializeField] GameObject practiceTemplate;
        GameObject runtime;
        HumanCannonSystem cannon;
        HumanCannonPoleSystem poles;
        HumanCannonTrampoline trampoline;
        HumanCannonPole selectedPole;
        HumanCannonSawGate selectedGate;
        readonly PoleGateState helperHold = new();
        const ulong HelperActor = ulong.MaxValue - 1;
        int section = -1, bodyPreset;
        bool busy, panel, catchAssist = true, partnerAssist = true, fireEnabled;
        Vector3 originalPlayerPosition;
        Quaternion originalPlayerRotation;
        bool hasOriginalPose;
        string message = "";
        float floorY;
        public bool IsPracticing => runtime != null;
        public bool IsBusy => busy;
        public int SelectedSection => section;
        public GameObject PracticeInstance => runtime;

        void Awake()
        {
            // 에디터에서는 옆 코스가 보인다. Play 시작 때 원본보다 먼저 복사본을 끈다.
            if (practiceTemplate != null) practiceTemplate.SetActive(false);
            if (!Application.isEditor) enabled = false;
            InputGate.AddReleaseKeys(Key.R, Key.F8, Key.F9);
        }

        bool SoloHost => NetworkSessionManager.Instance != null && NetworkSessionManager.Instance.IsHost
            && NetworkSessionManager.Instance.NetworkManager.ConnectedClientsIds.Count == 1
            && NetworkSessionManager.Instance.GameState == GameSessionState.Waiting;

        public void SelectSection(int index)
        {
            if (busy || index < 0 || index >= SectionNames.Length || practiceTemplate == null) return;
            StartCoroutine(Enter(index));
        }

        IEnumerator Enter(int index)
        {
            busy = true;
            try
            {
                var session = NetworkSessionManager.EnsureExists();
                if (!session.NetworkManager.IsListening)
                {
                    var host = session.HostLanAsync();
                    while (!host.IsCompleted) yield return null;
                    if (host.IsFaulted || host.IsCanceled || !host.Result)
                    { message = "혼자 테스트 방을 열지 못했습니다. 기존 LAN 연결을 확인하세요."; yield break; }
                }
                if (!SoloHost) { message = "이 기능은 대기 중인 호스트 1명일 때 사용합니다."; yield break; }
                float until = Time.realtimeSinceStartup + 10;
                while (NetworkPlayer.Local == null && Time.realtimeSinceStartup < until) yield return null;
                var player = NetworkPlayer.Local;
                if (player == null) { message = "플레이어 생성 대기 시간이 지났습니다."; yield break; }
                SetPanel(false);
                while (InputGate.IsBlocked) yield return null;
                if (!hasOriginalPose)
                {
                    originalPlayerPosition = player.transform.position;
                    originalPlayerRotation = player.transform.rotation;
                    hasOriginalPose = true;
                    if (HumanCannonTrampoline.Current?.Carrier == player.OwnerClientId)
                        HumanCannonTrampoline.Current.RequestDrop();
                }
                DisableCourse();
                yield return null; // 이전 pilot의 지연 Destroy가 새 조작 전략을 해제하지 않게 한다.
                runtime = Instantiate(practiceTemplate, practiceTemplate.transform.parent);
                runtime.name = "SoloPracticeRuntime";
                section = index;
                cannon = runtime.GetComponentInChildren<HumanCannonSystem>(true);
                poles = runtime.GetComponentInChildren<HumanCannonPoleSystem>(true);
                trampoline = runtime.GetComponentInChildren<HumanCannonTrampoline>(true);
                selectedPole = null; selectedGate = null; helperHold.Reset();
                if (IsCarrierSection) poles.enabled = false; // 복사본만: 아래 보조 상태가 상대의 봉 유지를 대신한다.
                foreach (var hud in runtime.GetComponentsInChildren<HumanCannonTestHud>(true)) hud.enabled = false;
                var mover = player.GetComponent<PlayerMover>();
                PrepareSection(out Vector3 start);
                mover.SetStartPose(start, Quaternion.identity, true);
                player.ServerRestoreAll();
                if (bodyPreset > 0) player.ServerCut(bodyPreset);
                runtime.SetActive(true);
                yield return null;
                yield return null; // 새 장치의 세션 메시지 등록과 조작 전략 바인딩.
                mover.SetStartPose(start, Quaternion.identity, true);
                InputGate.RequireRelease();
                if (section == 8)
                {
                    while (InputGate.IsBlocked) yield return null;
                    cannon.Request(6, CannonAction.Board);
                    cannon.Request(6, CannonAction.Launch);
                    var cushion = Find("LandingCushion").GetComponent<BoxCollider>();
                    player.GetComponent<HumanCannonPilot>().PracticeDrop(
                        new Vector3(cushion.bounds.center.x, cushion.bounds.max.y + 3, cushion.bounds.center.z));
                }
                message = SectionNames[index] + " / R: 이 구간 다시 시작";
            }
            finally { busy = false; }
        }

        void DisableCourse()
        {
            // 바인딩 의존성 역순. 모든 원본 GameObject/에셋/위치는 보존한다.
            HumanCannonRouteSystem.Current?.gameObject.SetActive(false);
            HumanCannonPoleSystem.Current?.gameObject.SetActive(false);
            HumanCannonTrampoline.Current?.gameObject.SetActive(false);
            HumanCannonSystem.Current?.gameObject.SetActive(false);
            foreach (var root in originalRoots) if (root != null) root.SetActive(false);
            if (runtime != null) { runtime.SetActive(false); Destroy(runtime); runtime = null; }
        }

        public void ReturnToCourse()
        {
            if (!busy && IsPracticing) StartCoroutine(Return());
        }

        IEnumerator Return()
        {
            busy = true;
            DisableCourse();
            yield return null;
            foreach (var root in originalRoots) if (root != null) root.SetActive(true);
            yield return null;
            var player = NetworkPlayer.Local;
            if (player != null && hasOriginalPose)
            {
                var mover = player.GetComponent<PlayerMover>();
                mover.SetStartPose(originalPlayerPosition, originalPlayerRotation, true);
                PlayerSpawnPoints.GetPose(player.Slot, mover.LaneSpacing, out var spawn, out var rotation);
                mover.SetStartPose(spawn, rotation, false);
            }
            hasOriginalPose = false;
            section = -1;
            busy = false;
            SetPanel(false);
            InputGate.RequireRelease();
            message = "기존 연속 코스로 돌아왔습니다.";
        }

        void PrepareSection(out Vector3 start)
        {
            var floor = Find(section is 6 or 7 ? "SplitRouteCourse/SecondCourseFloor" : "SplitRouteCourse/FirstCourseFloor");
            // 042 바닥은 루트 Collider를 끄고 타일로 나뉘었다. 비활성 템플릿도 실제 타일의 윗면을 읽는다.
            floorY = floor.GetComponentsInChildren<BoxCollider>(true).Where(c => c.enabled && !c.isTrigger)
                .Max(c => c.transform.TransformPoint(c.center + Vector3.up * c.size.y * 0.5f).y);
            if (section is 0 or 8 or 9)
            {
                start = cannon.Station(section == 0 ? 0 : 6).BoardingPoint.position;
                if (section == 0) trampoline.transform.position = Find("SplitRouteCourse/FirstCatch").position;
                return;
            }
            if (section is 3 or 4 or 5)
            {
                string[] names = { "DepartureTransport", "ShelterLift", "ShelterDeparture" };
                var transport = Find("SplitRouteCourse/" + names[section - 3]).GetComponent<HumanCannonTransport>();
                // 손잡이에서 앞쪽으로 돌출된 트램폴린까지 발판 안에 들어가도록 뒤쪽에서 시작한다.
                start = transport.StartPosition - transport.Rotation * Vector3.forward * 1.2f;
                PlaceHandle(start + Vector3.forward * 0.3f);
                return;
            }
            selectedPole = Find(section >= 6 ? "SplitRouteCourse/SecondSectionPoleGate" : "PoleGateCourse")
                .GetComponentsInChildren<HumanCannonPole>(true).First(p => p.LinkedGate != null);
            selectedGate = selectedPole.LinkedGate;
            if (IsCarrierSection)
            {
                start = selectedGate.transform.position - selectedGate.transform.forward * 5;
                start.y = floorY;
                PlaceHandle(start + Vector3.forward * 0.5f);
            }
            else
            {
                start = selectedPole.HangingPosition(0);
                start.y = floorY;
                trampoline.transform.SetPositionAndRotation(start, Quaternion.identity);
            }
        }

        void PlaceHandle(Vector3 handlePosition)
        {
            var localHandle = Vector3.Scale(trampoline.transform.InverseTransformPoint(trampoline.Handle.position),
                trampoline.transform.lossyScale);
            if (TrampolineCarryPose.TryCalculate(handlePosition, Vector3.forward, localHandle, out var position, out var rotation))
                trampoline.transform.SetPositionAndRotation(position, rotation);
        }

        Transform Find(string path) => runtime.transform.Find(path);
        bool IsCarrierSection => section is 1 or 6;

        void Update()
        {
            if (!Application.isEditor) return;
            if (Keyboard.current?.f8Key.wasPressedThisFrame == true) SetPanel(!panel);
            if (Keyboard.current?.f9Key.wasPressedThisFrame == true) TogglePartnerHold();
            if (IsPracticing && !busy && !SoloHost) { ReturnToCourse(); return; }
            if (IsPracticing && !busy && !InputGate.IsBlocked && Keyboard.current?.rKey.wasPressedThisFrame == true)
                SelectSection(section);
        }

        void OnDestroy()
        {
            InputGate.SetBlocking(this, false);
        }
    }
}
