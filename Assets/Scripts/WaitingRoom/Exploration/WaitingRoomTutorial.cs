using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 대기실 튜토리얼(Task-086, 2026-10-31 사용자 확정). 이미 있는 기능의 상태·이벤트만 보고 <see cref="WaitingRoomTutorialPanel"/>(가이드 전용, 나레이션 패널과 구분)로 안내를 하나씩 띄운다.
    /// 개인별·비강제. 연계 튜토리얼과 클로즈업 안내는 끝날 때까지 내 조작을 막는다(2026-10-31 사용자, 아래 <see cref="RestChain"/> 참고).
    /// 기물 안내는 막지 않고 바로 띄우되, 연계 튜토리얼 중이면 시점·조작이 돌아온 뒤 띄운다. 본 안내는 앱을 켜 둔 동안 기억한다(사용자 2026-10-31: 게임을 완전히 종료하고 다시 시작했을 때만 처음부터). 날·세대가 바뀌거나 로비에서 새 게임을 시작해도 다시 띄우지 않는다. PC에는 끄기 설정만 저장한다.
    /// 설정(게임 탭)에서 끄기·처음부터 다시 보기·본 안내 다시 읽기를 한다.
    /// 현재 개발된 동작만 안내하며 추격자는 안내하지 않는다. 게임 규칙·판정은 바꾸지 않는다.
    /// </summary>
    public sealed class WaitingRoomTutorial : MonoBehaviour
    {
        const string EnabledKey = "CurtainCall.WaitingRoom.Tutorial.Enabled";
        /// <summary>예전 PC 저장 본 안내 기록. 이제 쓰지 않으므로 지운다.</summary>
        const string LegacyProgressKey = "CurtainCall.WaitingRoom.Tutorial.v1";
        /// <summary>문구 파일: Assets/Resources/Settings/WaitingRoom/WaitingRoomTutorialTexts.txt (`ID | 제목 | 본문`).</summary>
        const string TextsPath = "Settings/WaitingRoom/WaitingRoomTutorialTexts";
        /// <summary>쉬는 시간 시작 뒤 숨은 공연 장비 안내까지(초, 사용자 결정 60초).</summary>
        const float HiddenGearDelay = 60f;
        /// <summary>클로즈업 없는 연계 안내가 조작을 막는 시간(초, 사용자 결정 4초). 클로즈업 안내는 <see cref="WaitingRoomTutorialCamera.Total"/>.</summary>
        const float TextLockSeconds = 4f;
        /// <summary>침대 등 상황 알림 표시 시간(초).</summary>
        const float NoticeSeconds = 5f;

        /// <summary>
        /// 연계 튜토리얼(2026-10-31 사용자 확정). 시간대가 시작되면 순서대로 띄우고, 모두 끝날 때까지 조작을 막는다.
        /// 클로즈업: G11 무대 입구 통로 · N02 상점 수화기·배송구 · N04 물려줄 선반 · N03 스케줄표. 단일 클로즈업: N05 숙소 침대(일정 확정 직후) · G10 신체 복구(상점 수화기).
        /// </summary>
        /// 2026-10-31 사용자: G03 상호작용은 연계에서 빼고 상호작용 가능한 대상을 처음 발견할 때 띄운다.
        /// N01(오늘 밤 할 일)은 연계 패널이 아니라 오른쪽 [tutorial] 패널로 밤 내내 띄운다.
        static readonly string[] RestChain = { "G01", "G11" }, NightChain = { "N02", "N04", "N03" };
        static readonly HashSet<string> CloseUpIds = new() { "G11", "N02", "N04", "N03", "N05", "G10" };
        static readonly HashSet<string> LockedIds = new(CloseUpIds) { "G01" };
        /// <summary>오른쪽 오늘 할 일 패널 위치·너비(1920×1080 UI 단위).</summary>
        const float TodoTop = 300f, TodoWidth = 400f;

        static WaitingRoomTutorial instance;
        static WaitingRoomTutorialBook book;

        StageEntranceGather gather;
        ExplorationTimerZones zones;
        bool sceneDirty = true;
        float hiddenAt, restStartedAt;
        string stepKey = "";
        int lastHeldCount = -1;
        bool pendingRevived, pendingTeammateDown;
        PropCarry carry;
        ExplorationState exploration;
        string interactKey = "F", uvKey = "우클릭";
        WaitingRoomTutorialCamera closeUp;
        bool sequence;
        float stepEndsAt;
        WaitingRoomRightPanel todo;
        string todoStep = "";
        ScheduleBoardView board;

        /// <summary>오늘 밤 할 일 패널(오른쪽)이 보이는지·제목. 테스트 확인용.</summary>
        public static bool TodoVisible => instance != null && instance.todo != null && instance.todo.Visible;
        public static string TodoTitle => TodoVisible ? instance.todo.Title : "";

        /// <summary>연계·클로즈업 튜토리얼이 진행 중이다(조작 막힘).</summary>
        public static bool IsSequenceActive => instance != null && instance.sequence;

        static bool memoryOnly, memoryEnabled = true;

        /// <summary>
        /// 테스트용: 켜 두는 동안 이 PC의 끄기 설정을 읽지도 쓰지도 않고 메모리에만 둔다.
        /// 테스트가 중간에 끊겨도 다음 플레이 시작 때 꺼지므로 PC 설정이 바뀐 채 남지 않는다(2026-10-31: 끊긴 테스트가 안내 끄기를 남김).
        /// </summary>
        public static bool MemoryOnly
        {
            get => memoryOnly;
            set { memoryOnly = value; memoryEnabled = true; book = null; HidePanel(); }
        }

        /// <summary>안내 표시 여부(이 PC). 끄면 새 안내를 띄우지 않는다.</summary>
        public static bool Enabled
        {
            get => memoryOnly ? memoryEnabled : PlayerPrefs.GetInt(EnabledKey, 1) != 0;
            set
            {
                if (memoryOnly) { memoryEnabled = value; return; }
                PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0); PlayerPrefs.Save();
            }
        }

        static WaitingRoomTutorialBook Book
        {
            get
            {
                if (book != null) return book;
                // 사람이 고치는 문구 파일. 없거나 빠진 줄은 코드 기본 문구.
                var texts = Resources.Load<TextAsset>(TextsPath);
                book = new WaitingRoomTutorialBook(texts != null ? WaitingRoomTutorialBook.ParseTexts(texts.text) : null);
                return book;
            }
        }

        /// <summary>지금 런의 본 안내를 모두 지워 처음부터 다시 띄운다.</summary>
        public static void ResetProgress()
        {
            Book.ResetAll();
            HidePanel();
        }

        static void HidePanel()
        {
            if (instance != null) instance.EndSequence();
            // 상황 알림(침대 등)은 안내 끄기와 상관없이 정해진 시간 동안 둔다.
            if (WaitingRoomTutorialPanel.IsVisible && Time.unscaledTime >= noticeUntil) WaitingRoomTutorialPanel.Hide();
        }

        static float noticeUntil;

        /// <summary>
        /// 튜토리얼과 별개인 상황 알림(침대 등)을 가이드 패널에 띄운다. 안내 끄기와 상관없이 뜨고, 기록하지 않는다.
        /// 연계 튜토리얼 중이면 띄우지 않는다.
        /// </summary>
        public static void ShowNotice(string title, string body)
        {
            if (IsSequenceActive) return;
            if (WaitingRoomTutorialPanel.Show(title, body, NoticeSeconds)) noticeUntil = Time.unscaledTime + NoticeSeconds;
        }

        /// <summary>설정의 다시 보기 목록(본 순서가 아니라 안내 순서).</summary>
        public static string SeenSummary()
        {
            var lines = new System.Text.StringBuilder();
            foreach (var entry in Book.Seen())
            {
                if (lines.Length > 0) lines.Append("\n\n");
                lines.Append(entry.title).Append('\n')
                    .Append(WaitingRoomTutorialBook.Format(entry.body, instance != null ? instance.interactKey : "F", instance != null ? instance.uvKey : "우클릭"));
            }
            return lines.Length > 0 ? lines.ToString() : "아직 본 안내가 없습니다.";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; book = null; memoryOnly = false; memoryEnabled = true; noticeUntil = 0f; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            if (PlayerPrefs.HasKey(LegacyProgressKey)) { PlayerPrefs.DeleteKey(LegacyProgressKey); PlayerPrefs.Save(); }
            var go = new GameObject(nameof(WaitingRoomTutorial));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<WaitingRoomTutorial>();
        }

        void Awake()
        {
            closeUp = gameObject.AddComponent<WaitingRoomTutorialCamera>();
            todo = new WaitingRoomRightPanel(transform, "TonightTodoCanvas", TodoTop, TodoWidth);
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneChanged;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            ClueInspectable.Inspected += OnInspected;
            UvMark.Discovered += OnUvFound;
            NetworkPlayer.Revived += OnRevived;
            InputBindingPreferences.Changed += RefreshKeys;
            RefreshKeys();
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneChanged;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            ClueInspectable.Inspected -= OnInspected;
            UvMark.Discovered -= OnUvFound;
            NetworkPlayer.Revived -= OnRevived;
            InputBindingPreferences.Changed -= RefreshKeys;
            Watch(null, null);
            EndSequence();
        }

        void OnDestroy() { if (instance == this) instance = null; }
        void OnSceneChanged(Scene scene, LoadSceneMode mode) => sceneDirty = true;
        void OnSceneUnloaded(Scene scene) => sceneDirty = true;

        void Update()
        {
            var run = CampaignRun.Instance;
            if (run == null) return;
            var s = run.State;
            if (s.Phase == CampaignPhase.Performance) WatchTeammates();
            if (!Enabled) { HidePanel(); todo.Hide(); return; }
            if (sceneDirty)
            {
                sceneDirty = false;
                gather = FindFirstObjectByType<StageEntranceGather>();
                zones = FindFirstObjectByType<ExplorationTimerZones>();
                board = FindFirstObjectByType<ScheduleBoardView>();
            }
            Watch(PropCarry.Instance, ExplorationState.Instance);
            var local = NetworkPlayer.Local;
            bool rest = s.Phase == CampaignPhase.RestTime, night = s.Phase == CampaignPhase.Night;
            // 대기실 씬의 쉬는 시간·밤에, 살아 있는 내 캐릭터가 화면 전환 중이 아닐 때만 안내한다.
            if (gather == null || local == null || local.State != PlayerState.Normal || (!rest && !night) || SceneCurtainTransition.IsActive)
            { lastHeldCount = -1; HidePanel(); todo.Hide(); return; }

            string key = s.Generation + "/" + s.Day + "/" + s.Phase + "/" + s.Rest + "/" + s.PerformanceIndex;
            // 시간대가 바뀌면 지난 시간대의 가이드는 닫고, 못 띄운 안내는 버린다(조건이 맞으면 다시 줄 선다).
            if (key != stepKey) { stepKey = key; restStartedAt = Time.time; Book.ClearQueue(); HidePanel(); }

            var b = Book;
            if (pendingRevived) { pendingRevived = false; b.Enqueue("S01"); }
            if (pendingTeammateDown) { pendingTeammateDown = false; b.Enqueue("S02"); }
            if (rest) CheckRest(s);
            if (night)
            {
                // 처음 맞는 밤에 오늘 할 일(N01)을 오른쪽에 띄워 이 밤이 끝날 때(모두 잠듦)까지 둔다.
                if (!b.IsDone("N01")) { b.MarkDone("N01"); todoStep = key; }
                foreach (var id in NightChain) b.Enqueue(id);
                // 잠자기는 스케줄표 일정이 확정된 뒤에 숙소를 비추며 안내한다(확정 전에는 침대를 쓸 수 없다).
                if (NightBed.ScheduleReady) b.Enqueue("N05");
            }
            UpdateTodo(night && key == todoStep);
            CheckNearby();
            CheckHeld();
            Pump();
        }

        /// <summary>오늘 밤 할 일 패널. 스케줄표를 보는 동안에는 표를 가리지 않게 숨긴다.</summary>
        void UpdateTodo(bool tonight)
        {
            var entry = Book.Get("N01");
            // 씬을 다시 불러오면 앞 스케줄표는 사라진다. 비면 다시 찾는다.
            if (tonight && board == null) board = FindFirstObjectByType<ScheduleBoardView>();
            if (!tonight || entry == null || (board != null && board.IsOpen)) { todo.Hide(); return; }
            todo.Show(entry.title, WaitingRoomTutorialBook.Format(entry.body, interactKey, uvKey));
        }

        /// <summary>지금 런의 본 안내 ID 목록. 테스트 확인용.</summary>
        public static string SeenIds => Book.Serialize();

        void CheckRest(CampaignSnapshot s)
        {
            var b = Book;
            if (s.Rest == RestKind.Guide) foreach (var id in RestChain) b.Enqueue(id);
            if (s.Timer == RestTimerState.Running && zones != null && ShopReach.TryLocalFeet(out var feet) && zones.IsRunningZone(feet)) b.Enqueue("G02");
            if (s.AlarmRaised) b.Enqueue("G11A");
            if (ExpertService.LocalLostLimbs > 0) b.Enqueue("G10");
            // 이미 찾았으면 숨은 장비 안내는 필요 없다.
            if (s.CannonUnlocked) b.MarkDone("G08");
            else if (Time.time - restStartedAt >= HiddenGearDelay) b.Enqueue("G08");
        }

        void CheckNearby()
        {
            var b = Book;
            var focused = ShopReach.Focused;
            // 상호작용 가능한 대상을 처음 발견(바라봄)했을 때 상호작용 안내.
            if (focused != null) b.Enqueue("G03");
            if (focused is ShopPhone && CampaignRun.Instance.State.Phase == CampaignPhase.RestTime) b.Enqueue("G09");
            else if (focused is HandoverShelfView) b.Enqueue("N04");
            else if (focused is ExplorationGate gate && gate.NeedsPartner && gate.HasKeySlots) b.Enqueue("G12");
        }

        void CheckHeld()
        {
            if (carry == null) return;
            var held = carry.Held(carry.LocalClientId);
            if (lastHeldCount >= 0 && held.Count > lastHeldCount) Book.Enqueue("G05");
            lastHeldCount = held.Count;
            foreach (var item in held)
            {
                if (item.ItemId == ItemUses.MagicianHairpin) Book.Enqueue("G06a");
                else if (item.ItemId == ItemUses.StakePuller) Book.Enqueue("G06b");
                else if (item.ItemId == ItemUses.UvLamp) Book.Enqueue("G06c");
            }
        }

        static bool IsLocked(string id) => LockedIds.Contains(id);

        void Pump()
        {
            var b = Book;
            if (sequence)
            {
                // 지금 안내(패널·클로즈업)가 끝날 때까지 조작을 막은 채 기다린다. 이어지는 연계 안내가 없으면 조작을 돌려준다.
                if (Time.unscaledTime < stepEndsAt || closeUp.IsActive) return;
                if (!b.TryNext(false, IsLocked, out _)) { EndSequence(); return; }
            }
            else
            {
                if (WaitingRoomTutorialPanel.IsVisible) { hiddenAt = Time.unscaledTime; return; }
                // 앞 안내가 사라진 뒤 쉬는 시간은 패널 설정 파일의 "간격".
                if (Time.unscaledTime - hiddenAt < WaitingRoomTutorialPanel.Settings.Gap) return;
            }
            // 연계 중에는 연계·클로즈업 안내만 꺼낸다. 기물 안내 등은 끝난 뒤 시점·조작이 돌아오면 뜬다.
            // 연계·클로즈업 안내(조작 잠금)를 일반 안내보다 먼저 꺼낸다(예: 확정 직후 잠자기가 상호작용 안내 뒤로 밀리지 않게).
            bool blocked = !sequence && InputGate.IsBlocked;
            if (!b.TryNext(blocked, IsLocked, out var entry) && (sequence || !b.TryNext(blocked, null, out entry))) return;
            string body = WaitingRoomTutorialBook.Format(entry.body, interactKey, uvKey);
            if (!IsLocked(entry.id))
            {
                // 패널이 아직 사라지는 중이면 다음 프레임에 다시 시도한다.
                if (!WaitingRoomTutorialPanel.Show(entry.title, body, entry.seconds)) return;
                b.MarkDone(entry.id);
                return;
            }
            bool framed = CloseUpIds.Contains(entry.id) && TryFocus(entry.id, out var pos, out var rot) && closeUp.Begin(pos, rot);
            float seconds = framed ? WaitingRoomTutorialCamera.Total : TextLockSeconds;
            if (!WaitingRoomTutorialPanel.Show(entry.title, body, seconds)) { closeUp.Stop(); return; }
            if (!sequence) { sequence = true; InputGate.SetBlocking(this, true); }
            stepEndsAt = Time.unscaledTime + seconds;
            b.MarkDone(entry.id);
        }

        /// <summary>연계 튜토리얼을 끝낸다(다 봤거나 시간대 전환·대기실 이탈·끄기). 시점과 조작을 돌려준다.</summary>
        void EndSequence()
        {
            if (closeUp != null) closeUp.Stop();
            if (!sequence) return;
            sequence = false;
            InputGate.SetBlocking(this, false);
            hiddenAt = Time.unscaledTime;
        }

        /// <summary>안내가 가리키는 곳을 비출 카메라 자리.</summary>
        bool TryFocus(string id, out Vector3 pos, out Quaternion rot)
        {
            pos = default; rot = Quaternion.identity;
            var main = Camera.main;
            if (main == null) return false;
            Bounds? focus = null;
            switch (id)
            {
                case "G11":
                    var box = gather != null ? gather.GetComponent<Collider>() : null;
                    if (box != null) focus = box.bounds;
                    break;
                case "N02":
                    var phone = FindFirstObjectByType<ShopPhone>();
                    if (phone == null) break;
                    var shop = RendererBounds(phone.transform);
                    var slot = FindFirstObjectByType<ShopDeliverySlot>();
                    if (slot != null) shop.Encapsulate(RendererBounds(slot.transform));
                    focus = shop;
                    break;
                case "N04":
                    var shelf = FindFirstObjectByType<HandoverShelfView>();
                    if (shelf != null) focus = RendererBounds(shelf.VisualRoot);
                    break;
                case "N03":
                    var board = FindFirstObjectByType<ScheduleBoardView>();
                    if (board == null) break;
                    // 스케줄표 창이 쓰는 클로즈업 자리를 그대로 쓴다.
                    if (board.CloseUpPose != null) { pos = board.CloseUpPose.position; rot = board.CloseUpPose.rotation; return true; }
                    focus = RendererBounds(board.VisualRoot);
                    break;
                case "N05":
                    foreach (var bed in NightBed.AllBeds())
                        if (focus == null) focus = bed.Footprint;
                        else { var all = focus.Value; all.Encapsulate(bed.Footprint); focus = all; }
                    break;
                case "G10":
                    // 의료용 서비스는 상점 수화기에서 산다(사용자 2026-10-31: 승강기가 아니라 상점을 비춘다).
                    var servicePhone = FindFirstObjectByType<ShopPhone>();
                    if (servicePhone != null) focus = RendererBounds(servicePhone.transform);
                    break;
            }
            if (focus == null) return false;
            WaitingRoomTutorialCamera.FramePose(focus.Value, main.transform.position, out pos, out rot);
            return true;
        }

        static Bounds RendererBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.position, Vector3.one);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>공연 중 다른 단원이 쓰러지면 다음 대기실에서 한 번 알린다.</summary>
        void WatchTeammates()
        {
            if (pendingTeammateDown || Book.IsDone("S02")) return;
            foreach (var player in NetworkPlayer.All)
                if (player != null && !player.IsLocal && (player.State == PlayerState.Fallen || player.State == PlayerState.Retired))
                { pendingTeammateDown = true; return; }
        }

        void Watch(PropCarry nextCarry, ExplorationState nextExploration)
        {
            if (carry != nextCarry)
            {
                if (carry != null) carry.RequestAnswered -= OnCarryAnswered;
                carry = nextCarry; lastHeldCount = -1;
                if (carry != null) carry.RequestAnswered += OnCarryAnswered;
            }
            if (exploration != nextExploration)
            {
                if (exploration != null) exploration.RequestAnswered -= OnExplorationAnswered;
                exploration = nextExploration;
                if (exploration != null) exploration.RequestAnswered += OnExplorationAnswered;
            }
        }

        void OnCarryAnswered(string text) { if (text == PropCarry.Explain(CarryRefusal.HandsFull)) Book.Enqueue("S03"); }
        // 도구 소진은 결과 문구가 아니라 결과 표시로 안다(문구는 사용자가 고칠 수 있다).
        void OnExplorationAnswered(string text) { if (exploration != null && exploration.LastUsedUp) Book.Enqueue("S04"); }
        void OnInspected(ClueInspectable clue) { Book.Enqueue("G04"); Book.Enqueue("G07"); }
        void OnUvFound(UvMark mark) => Book.Enqueue("G07");
        void OnRevived(NetworkPlayer player) { if (player != null && player.IsLocal) pendingRevived = true; }

        void RefreshKeys()
        {
            InputActionAsset draft = null;
            try
            {
                draft = InputBindingPreferences.CreateDraft();
                var map = draft != null ? draft.FindActionMap("Common", false) : null;
                interactKey = Display(map?.FindAction("Interact", false), "F");
                uvKey = Display(map?.FindAction("UvLight", false), "우클릭");
            }
            catch (System.Exception e) { Debug.LogWarning("[Tutorial] 키 이름을 읽지 못해 기본값을 씁니다: " + e.Message); }
            finally { if (draft != null) Destroy(draft); }
        }

        static string Display(InputAction action, string fallback)
        {
            // 자판 배치 표시(한글 자판이면 'ㄹ')가 아니라 키 위치 이름(F)을 쓴다. 설정 화면의 키 이름과 같다.
            string path = action != null && action.bindings.Count > 0 ? action.bindings[0].effectivePath : "";
            string text = string.IsNullOrEmpty(path) ? "" : InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            return text switch { "RMB" or "Right Button" => "우클릭", "MMB" or "Middle Button" => "휠 클릭", _ => text };
        }
    }
}
