using System.Linq;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.WaitingRoom
{
    public enum ExplorationGateKind { Open, Monitor, Keypad, Cooperative, Breakable }

    /// <summary>F 공통 대상. 열쇠/단서/패드 등 선행 대상의 열린 상태를 조건으로 사용한다.</summary>
    public sealed class ExplorationGate : MonoBehaviour, IBareHandTarget, IShopTarget, IShopPresentation
    {
        [SerializeField] string targetId = "gate";
        [SerializeField] string label = "조사";
        [SerializeField] ExplorationGateKind kind;
        [Tooltip("모두 열린 뒤 상호작용 가능. 열쇠 획득, UV 발견, 패드 해제 등의 고유 TargetId.")]
        [SerializeField] string[] requiredTargets;
        [Tooltip("비우면 처음부터 표시. 숨겨진 문은 UV/모니터 등의 TargetId 지정.")]
        [SerializeField] string revealTarget;
        [SerializeField] bool revealPending;
        [SerializeField] bool interactionPending;
        public bool InteractionPending => interactionPending;
        [SerializeField] GameObject[] hiddenVisuals;
        [SerializeField] Collider blocker;
        [SerializeField] GameObject[] showOnOpen;
        [SerializeField] GameObject[] hideOnOpen;
        [SerializeField] string toolId = "";
        [SerializeField, Min(1)] int requiredHits = 1;
        [Tooltip("부수기(Breakable) 대상을 맨몸으로 열 때 필요한 수(통로 기믹 CC-255, 2026-11-10 사용자: 상자 맨몸 5·뽑개 1). 0이면 맨몸 불가. 섞어 치면 비율로 합산.")]
        [SerializeField, Min(0)] int bareHandHits = 5;
        [SerializeField, Min(.1f)] float cooperationWindow = .75f;
        float CommonReach => CurtainCall.Settings.GameSettings.Base.Interaction.Distance;
        [SerializeField] string password = "0000";
        [SerializeField] bool passwordPending, passwordExplicit;
        [SerializeField, TextArea] string monitorText = "";
        [SerializeField] Camera monitorCamera;
        [Tooltip("단말기(관리실 컴퓨터) 전용: 암호가 맞은 뒤 카메라 영상 화면에서 '문이 열렸다'고 표시할 대상. 이 대상이 아직 안 열렸으면 '잠금만 풀렸다'고 표시한다.")]
        [SerializeField] string feedOpenTarget = "";
        [Tooltip("암호가 맞은 뒤 카메라 영상을 보여 주는 시간(초). 그 전에 EXIT·ESC·F로 닫을 수 있다.")]
        [SerializeField, Min(1f)] float feedSeconds = 8f;
        bool feedShown; float feedUntil;
        RenderTexture monitorFeed;
        [Tooltip("감시 화면(Monitor) 전용: 지정하면 판에 영상을 띄우지 않고 이 화면에 영상을 띄운 채 가까이서 직접 들여다본다(2026-11-13 사용자).")]
        [SerializeField] Renderer screenSurface;
        Camera lookCamera; Material screenMaterial;
        [Tooltip("이 대상이 열리면 이 대상은 쓸모가 없어져 사라진다(관리실 문이 다른 방법으로 열린 뒤 남는 빗장·키 카드 단말기).")]
        [SerializeField] string retiredBy = "";
        // 배터리가 꽂힌 소켓은 사라지지 않는다(빼야 하므로). 뺀 뒤에 사라진다.
        bool Retired => !string.IsNullOrEmpty(retiredBy) && ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(retiredBy)
            && !(IsSocket && ExplorationState.Instance.IsOpen(targetId));
        [Tooltip("지정하면 이 Transform의 앞쪽(+Z)에 선 사람만 닿는다(2026-11-14 사용자: 전실 동력 소켓은 방 안쪽에서 못 쓰고 로비 쪽에서만).")]
        [SerializeField] Transform reachFront;
        [Tooltip("동력 소켓 전용: 배터리를 뺐을 때 한 번 띄울 안내 ID(문구 파일 guide.<ID>.title/.body). 비우면 띄우지 않는다.")]
        [SerializeField] string removeGuideId = "";
        bool socketSeen, socketWasIn;
        [Tooltip("열릴 때 hideOnOpen 물체들이 이만큼 밀려난 뒤 사라진다(탈출로 문이 벽 속으로 밀려 열리는 모습). 0초면 바로 사라진다.")]
        [SerializeField] Vector3 openSlide;
        [SerializeField, Min(0f)] float openSlideSeconds;
        [Tooltip("0보다 크면, 열려도 누군가 이 거리(m) 안으로 오거나 단말기 화면으로 문을 보고 있을 때까지 닫힌 채로 기다렸다가 열린다(2026-11-14 사용자: 아무도 없는 사이 열려 버려 열리는 모습을 못 보던 탈출로 문).")]
        [SerializeField, Min(0f)] float openWhenWithin;
        /// <summary>단말기 화면으로 문 카메라 영상을 보는 중(이 시각까지).</summary>
        static float feedWatchedUntil;
        /// <summary>연출 카메라가 문을 비추는 동안에도 본 것으로 친다.</summary>
        public static void WatchDoors(float seconds) => feedWatchedUntil = Mathf.Max(feedWatchedUntil, Time.unscaledTime + seconds);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFeedWatch() => feedWatchedUntil = 0f;
        bool Witnessed()
        {
            if (Time.unscaledTime < feedWatchedUntil) return true;
            bool Near(Vector3 feet) => Mathf.Abs(feet.y - transform.position.y) < 4f
                && new Vector2(feet.x - transform.position.x, feet.z - transform.position.z).sqrMagnitude < openWhenWithin * openWhenWithin;
            foreach (var player in CurtainCall.Network.PlayerSync.NetworkPlayer.All)
                if (player != null && player.IsSpawned && Near(player.transform.position)) return true;
            return ExplorationState.Instance != null && ExplorationState.Instance.LocalFeet is Vector3 local && Near(local);
        }
        float slideT; bool slideReady; Vector3[] slideBase;
        [SerializeField] string rewardItemId = "";
        [SerializeField, Range(1, 99)] int rewardUidOffset = 60;
        [SerializeField] Transform rewardPoint;

        public string TargetId => targetId;
        public string ToolId => ExplorationKeys.ItemEnabled(toolId) ? toolId : "";
        public string ToolName => ItemCatalog.Get(toolId)?.displayName ?? toolId;
        public string Label => WaitingRoomText.Get("target." + targetId + ".name", label);
        string MonitorText => WaitingRoomText.Get("target." + targetId + ".text", monitorText);
        public string RewardItemId => ExplorationKeys.ItemEnabled(rewardItemId) ? rewardItemId : "";
        public Vector3 RewardPoint => rewardPoint != null ? rewardPoint.position : transform.position;
        public int RewardUidOffset => rewardUidOffset;
        public bool IsOpenGate => kind == ExplorationGateKind.Open;
        public static bool IsKeyTarget(string id) => System.Array.IndexOf(ExplorationKeys.Targets, id) >= 0;
        public bool IsKeyPickup => IsKeyTarget(targetId);
        public bool UsesKey => ExplorationKeys.Enabled && (HasKeySlots || (requiredTargets!=null && requiredTargets.Any(IsKeyTarget)));
        public bool AcceptsKey(PropItem item) => ExplorationKeys.Enabled && item.Uid != 0 && item.Uses != 0 && requiredTargets != null
            && requiredTargets.Any(id => IsKeyTarget(id) && id == ExplorationKeys.TargetId(item.ItemId));
        public bool LocalHasKey => PropCarry.Instance != null && AcceptsKey(PropCarry.Instance.InHand(PropCarry.Instance.LocalClientId));
        public string RevealTarget => revealTarget;
        public bool RevealPending => revealPending;
        public bool PasswordPending => passwordPending;
        public bool PasswordExplicit => passwordExplicit;
        public void ConfigureVisibilityPending() => revealPending = true;
        public void ConfigurePasswordPending() { passwordPending = true; passwordExplicit = false; }
        public void ConfigurePassword(string value)
        {
            if (!ExplorationPuzzleRules.IsFourDigits(value)) throw new System.ArgumentException(nameof(value));
            password = value; passwordPending = false; passwordExplicit = true;
        }
        public bool IsMonitor => kind == ExplorationGateKind.Monitor;
        public bool IsBreakable => kind == ExplorationGateKind.Breakable;
        public bool AllowsBareHands => IsBreakable && bareHandHits > 0 && !string.IsNullOrEmpty(toolId);
        public int BareHits => bareHandHits;
        public int ToolHits => requiredHits;
        public bool NeedsPartner => kind == ExplorationGateKind.Cooperative;
        public float CooperationWindow => cooperationWindow;
        public bool NeedsPassword => kind == ExplorationGateKind.Keypad;
        [Tooltip("채우면 단말기 화면(대기실 최종본 관리실 컴퓨터): 숫자 password와 이 글자를 둘 다 맞혀야 열린다.")]
        [SerializeField] string terminalWord = "";
        public bool IsTerminal => NeedsPassword && !string.IsNullOrEmpty(terminalWord);
        [Tooltip("지정하면 그 대상이 열릴 때 함께 열리고 직접 F하지 않는다. 예: 키패드 정답 → 숨겨진 문.")]
        [SerializeField] string openedBy = "";
        public string OpenedBy => openedBy;
        [Tooltip("동력 소켓(2026-11-13 사용자): 배터리(toolId)를 꽂으면 열리고, 다시 F로 빼면 닫힌다. 배터리는 닳지 않고 손으로 돌아온다. kind는 Open으로 둔다.")]
        [SerializeField] bool socket;
        public bool IsSocket => socket && !string.IsNullOrEmpty(toolId);
        public static bool IsAnyOpen(string ids) => AnyOpen(ids);
        static bool AnyOpen(string ids)
        {
            if (string.IsNullOrEmpty(ids) || ExplorationState.Instance == null) return false;
            if (ids.IndexOf('|') < 0) return ExplorationState.Instance.IsOpen(ids);
            foreach (var id in ids.Split('|')) if (ExplorationState.Instance.IsOpen(id)) return true;
            return false;
        }
        public void ConfigureOpenedBy(string id) => openedBy = id ?? "";
        // openedBy는 "|"로 여러 대상을 적을 수 있다(하나만 열려도 열림: 배터리 소켓 또는 회로 고정).
        public bool Opened => ExplorationState.Instance != null && (ExplorationState.Instance.IsOpen(targetId) || AnyOpen(openedBy));
        public bool Visible => (ExplorationKeys.Enabled || !IsKeyPickup) && !revealPending
            && (string.IsNullOrEmpty(revealTarget) || !ExplorationKeys.Enabled && IsKeyTarget(revealTarget)
                || (ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(revealTarget)));
        public bool ConditionsMet
        {
            get
            {
                if (!Visible || interactionPending || (NeedsPassword && passwordPending)) return false;
                if (requiredTargets != null) foreach (var id in requiredTargets)
                {
                    if (IsKeyTarget(id)) continue; // Possession is checked for the interacting player, not the team's history.
                    if (string.IsNullOrEmpty(id) || ExplorationState.Instance == null || !ExplorationState.Instance.IsOpen(id)) return false;
                }
                return true;
            }
        }
        /// <summary>배치와 별개로 경로를 연결한다. 기존 추가 조건은 보존한다.</summary>
        public void ConfigureRoute(string id, string[] prerequisites = null, string revealedBy = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new System.ArgumentException(nameof(id));
            targetId = id;
            var combined = new System.Collections.Generic.List<string>(requiredTargets ?? System.Array.Empty<string>());
            if (prerequisites != null) foreach (var prerequisite in prerequisites)
                if (!combined.Contains(prerequisite)) combined.Add(prerequisite);
            requiredTargets = combined.ToArray();
            if (revealedBy != null) { revealTarget = revealedBy; revealPending = false; }
        }

        public Transform VisualRoot => transform;
        [Tooltip("지정하면 실제 자물쇠/조작면을 상호작용 위치로 사용한다.")]
        [SerializeField] Transform interactionPoint;
        [Tooltip("협력 문의 열쇠 구멍. 두 사람이 서로 다른 구멍에서 동시에 F.")]
        [SerializeField] Transform[] keySlots;
        public bool HasKeySlots => ExplorationKeys.Enabled && keySlots != null && keySlots.Length > 1;
        public Vector3 PresentationPoint
        {
            get
            {
                if (HasKeySlots && ExplorationState.Instance != null && ExplorationState.Instance.LocalFeet is Vector3 feet)
                    return keySlots.Where(s => s != null).OrderBy(s => Vector3.Distance(s.position, feet)).First().position;
                return interactionPoint != null ? interactionPoint.position : transform.position;
            }
        }
        /// <summary>손이 닿는 가장 가까운 열쇠 구멍 번호. 없으면 -1.</summary>
        public int SlotAt(Vector3 feet, Component actor = null) => SlotAt(feet, out _, actor);
        int SlotAt(Vector3 feet, out float distance, Component actor = null)
        {
            int best = -1; distance = -1f;
            for (int i = 0; i < keySlots.Length; i++)
            {
                float d = keySlots[i] != null ? ExplorationReach.From(transform, keySlots[i].position, feet, CommonReach, true, actor: actor) : -1f;
                if (d >= 0f && (best < 0 || d < distance)) { best = i; distance = d; }
            }
            return best;
        }
        public bool IsAvailable => Visible && !Retired && string.IsNullOrEmpty(openedBy) && (!Opened || kind == ExplorationGateKind.Monitor || IsSocket || CanReviewFeed);
        // 암호를 푼 단말기는 다시 열어 문 카메라 영상을 볼 수 있다(2026-11-14 사용자: 배선을 끊기 전에 암호를 풀면 문이 열리는 화면을 볼 방법이 없었다).
        bool CanReviewFeed => IsTerminal && monitorCamera != null;
        // 꽂기·빼기 문구는 대상마다 바꿀 수 있다(문구 파일 target.<ID>.insert / .remove. 예: TV에 테이프 넣기).
        string SocketText(string action, string common, string fallback) => WaitingRoomText.Format(
            WaitingRoomText.Has("target." + targetId + "." + action) ? "target." + targetId + "." + action : common, fallback, ("label", Label), ("tool", ToolName));
        public string ActionLabel => IsSocket && ConditionsMet ? (Opened ? SocketText("remove", "gate.socket-remove", "{label} — {tool} 빼기")
                : ExplorationTargets.LocalHolds(toolId) ? SocketText("insert", "gate.socket-insert", "{label} — {tool} 꽂기")
                : Label + " — " + ExplorationTargets.Hint(this))
            : !ConditionsMet ? WaitingRoomText.Format("gate.conditions-unmet", "{label} — 조건 미충족", ("label", Label))
            : AllowsBareHands ? StrikeLabel()
            // 도구로만 여는 대상(동력 소켓 등): 손에 없으면 무엇이 필요한지 알려 준다(문구 파일 target.<ID>.hint).
            : !string.IsNullOrEmpty(ToolId) && !ExplorationTargets.LocalHolds(toolId) ? Label + " — " + ExplorationTargets.Hint(this)
            : UsesKey && !LocalHasKey ? Label + " — 맞는 열쇠를 선택하세요"
            : NeedsPartner ? (HasKeySlots ? WaitingRoomText.Format("gate.needs-two-keys", "{label} — 두 명이 각자 열쇠 구멍에서 함께 F", ("label", Label))
                : WaitingRoomText.Format("gate.needs-two", "{label} — 두 명이 함께 F", ("label", Label))) : Label;
        // 맨몸 겸용 부수기 대상: 뽑개를 들었으면 친 수를, 맨몸이면 횟수 없이 게이지 하나만 보여 준다(2026-11-13 사용자).
        string StrikeLabel()
        {
            if (!ExplorationTargets.LocalHolds(toolId)) return Label + ShopReach.BareHoldSuffix(this);
            if (ExplorationState.Instance == null || ExplorationState.Instance.Hits(targetId) == 0) return Label;
            var (n, total) = ExplorationTargets.StrikeCount(this, true);
            return WaitingRoomText.Format("gate.progress", "{label} {n}/{total}", ("label", Label), ("n", n), ("total", total));
        }
        public string DescribeStrike(bool opened, int done, int total, bool withTool) => opened
            ? WaitingRoomText.Format("gate.done", "{label} 완료", ("label", Label))
            : WaitingRoomText.Format("gate.progress", "{label} {n}/{total}", ("label", Label), ("n", done), ("total", total));
        UIInputBlocker inputBlocker;
        NetworkPlayer panelOpenedBy;
        bool panelOpenedByNetworkPlayer;
        bool panel;
        ExplorationPanel panelView;
        string entered = "";
        string lastPanelMessage = "";
        int openedFrame;

        void Awake() { inputBlocker = gameObject.AddComponent<UIInputBlocker>(); inputBlocker.BlockWhileActive = false; }
        void Start(){if(IsKeyPickup && GetComponent<CurtainCall.ItemArt.ExplorationKeyVisual>()==null)gameObject.AddComponent<CurtainCall.ItemArt.ExplorationKeyVisual>();}
        void OnEnable() { ExplorationTargets.Add(this); ShopReach.Add(this); }
        void OnDisable() { ExplorationTargets.Remove(this); ShopReach.Remove(this); ClosePanel(); }
        public float Reach(Vector3 feet, Component actor = null)
        {
            if (!IsAvailable) return -1;
            if (HasKeySlots) { SlotAt(feet, out float distance, actor); return distance; }
            if (reachFront != null)
            {
                Vector3 body = actor != null ? PlayerInteraction.Center(actor) : feet;
                if (Vector3.Dot(body - reachFront.position, reachFront.forward) < 0f) return -1f;
            }
            return ExplorationReach.From(transform, PresentationPoint, feet, CommonReach, true, actor: actor);
        }
        public bool MatchesPassword(string value) => !passwordPending && (IsTerminal ? value == password + "\n" + terminalWord
            : ExplorationPuzzleRules.IsFourDigits(password) && value == password);
        public bool Resolve(int attemptNumber, System.Random rng) => attemptNumber >= (kind == ExplorationGateKind.Breakable ? requiredHits : 1);
        public string Describe(bool success, int attemptNumber) => success ? WaitingRoomText.Format("gate.done", "{label} 완료", ("label", Label))
            : WaitingRoomText.Format("gate.progress", "{label} {n}/{total}", ("label", Label), ("n", attemptNumber), ("total", requiredHits));
        public void Interact()
        {
            if (!ConditionsMet || ExplorationState.Instance == null || !LocalCanUsePanel) return;
            if (NeedsPassword || kind == ExplorationGateKind.Monitor)
            {
                if (panel) return;
                panel = true; entered = ""; openedFrame = Time.frameCount; inputBlocker.SetOpen(true);
                panelOpenedBy = NetworkPlayer.Local; panelOpenedByNetworkPlayer = panelOpenedBy != null;
                if (kind == ExplorationGateKind.Monitor && !Opened) ExplorationState.Instance.RequestUse(targetId);
                if (panelView == null) panelView = gameObject.AddComponent<ExplorationPanel>();
                feedShown = false;
                if (monitorCamera != null && !IsTerminal)
                {
                    if (monitorFeed == null) { monitorFeed = new RenderTexture(768, 432, 16); monitorFeed.name = "MachineRoomFeed"; monitorFeed.Create(); }
                    monitorCamera.targetTexture = monitorFeed; monitorCamera.enabled = true;
                }
                lastPanelMessage = ExplorationState.Instance.LastMessage;
                if (NeedsPassword) { ExplorationState.Instance.RequestAnswered -= Answered; ExplorationState.Instance.RequestAnswered += Answered; }
                bool look = kind == ExplorationGateKind.Monitor && screenSurface != null && monitorFeed != null && BeginLook();
                if (look) panelView.Open(Label, MonitorText, false, null, ClosePanel, true);
                else if (IsTerminal) panelView.OpenTerminal(Label, password.Length, value => {
                    if (!PanelOwnerCanUse) { ClosePanel(); return; }
                    ExplorationState.Instance?.RequestCode(targetId, value);
                }, ClosePanel);
                else panelView.Open(Label, NeedsPassword ? WaitingRoomText.Get("gate.enter-code", "숫자 네 자리를 입력하세요.") : MonitorText, NeedsPassword, value => {
                    if (!PanelOwnerCanUse) { ClosePanel(); return; }
                    ExplorationState.Instance?.RequestCode(targetId, value);
                }, ClosePanel, false, monitorFeed);
            }
            else ExplorationState.Instance.RequestUse(targetId);
        }
        void ClosePanel()
        {
            EndLook();
            panel = false; feedShown = false; if (monitorCamera != null) monitorCamera.enabled = false; if (panelView != null) panelView.Hide(); if (inputBlocker != null) inputBlocker.SetOpen(false);
            panelOpenedBy = null; panelOpenedByNetworkPlayer = false;
            if (ExplorationState.Instance != null) ExplorationState.Instance.RequestAnswered -= Answered;
        }
        // 감시 화면을 가까이서 들여다본다: 화면에 영상을 띄우고, 화면 바로 앞에 임시 카메라를 둔다.
        // 화면과 카메라 사이에 선 내 캐릭터가 가리지 않게 화면 근처만 그린다(가까운 쪽을 잘라 낸다).
        bool BeginLook()
        {
            var main = Camera.main; if (main == null) return false;
            if (screenMaterial == null) screenMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "MonitorFeedScreen", hideFlags = HideFlags.DontSave };
            screenMaterial.SetTexture("_BaseMap", monitorFeed); screenMaterial.SetColor("_BaseColor", Color.white);
            screenSurface.sharedMaterial = screenMaterial; screenSurface.enabled = true;
            var b = screenSurface.bounds; Vector3 outward = -screenSurface.transform.forward;
            float distance = b.size.y * .5f / Mathf.Tan(main.fieldOfView * .5f * Mathf.Deg2Rad) / .6f;
            if (lookCamera == null) lookCamera = new GameObject("MonitorLookCamera").AddComponent<Camera>();
            lookCamera.CopyFrom(main); lookCamera.targetTexture = null; lookCamera.depth = main.depth + 10; lookCamera.useOcclusionCulling = false;
            lookCamera.nearClipPlane = Mathf.Max(.03f, distance - .25f);
            // 아래쪽 설명 판에 가리지 않게 화면을 조금 위쪽에 둔다.
            lookCamera.transform.SetPositionAndRotation(b.center + outward * distance + Vector3.down * (b.size.y * .28f), Quaternion.LookRotation(-outward, Vector3.up));
            lookCamera.enabled = true;
            return true;
        }
        void EndLook()
        {
            if (lookCamera != null) { Destroy(lookCamera.gameObject); lookCamera = null; }
            if (screenSurface != null && kind == ExplorationGateKind.Monitor) screenSurface.enabled = false;
        }
        static bool LocalCanUsePanel => NetworkPlayer.Local != null
            ? PlayerInteraction.CanInteract(NetworkPlayer.Local)
            : NetworkPlayer.All.Count == 0 && PlayerInteraction.CanInteract(CurtainCall.Player.OfflineLocalPlayer.Reader);
        bool PanelOwnerCanUse => LocalCanUsePanel
            && (!panelOpenedByNetworkPlayer || panelOpenedBy != null && panelOpenedBy == NetworkPlayer.Local);
        // 오답: 경고음 + 입력 비움, 다시 입력 가능. 정답: 짧은 확인음 뒤 패널이 닫힌다.
        void Answered(string text)
        {
            if (!panel || panelView == null) return;
            panelView.SetMessage(text); lastPanelMessage = text;
            if (text == ExplorationState.WrongCode) { Play(ref buzz, "KeypadWrong", .45f, 180f, true); panelView.ClearCode(); }
        }
        bool FeedDoorOpens => string.IsNullOrEmpty(feedOpenTarget) || (ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(feedOpenTarget));
        bool feedPicture;
        string FeedStatus() => WaitingRoomText.Get("gate.terminal-feed-locked", "> LOCK RELEASED");
        AudioSource sound;
        AudioClip buzz, chime;
        void Play(ref AudioClip clip, string name, float seconds, float pitch, bool square)
        {
            if (sound == null) { sound = gameObject.AddComponent<AudioSource>(); sound.spatialBlend = .6f; sound.maxDistance = 15f; }
            if (clip == null)
            {
                // 임시 합성음. 실제 효과음이 정해지면 바꾼다.
                int rate = 22050, n = Mathf.CeilToInt(rate * seconds); var data = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / rate, wave = Mathf.Sin(2 * Mathf.PI * pitch * t);
                    data[i] = (square ? Mathf.Sign(wave) * (t % .15f < .1f ? 1f : 0f) : wave) * Mathf.Exp(-t * 2f / seconds) * .25f;
                }
                clip = AudioClip.Create(name, n, 1, rate, false); clip.SetData(data, 0);
            }
            sound.PlayOneShot(clip);
        }
        void OnDestroy()
        {
            if (monitorCamera != null) monitorCamera.targetTexture = null;
            if (monitorFeed != null) { monitorFeed.Release(); Destroy(monitorFeed); }
            if (lookCamera != null) Destroy(lookCamera.gameObject);
            if (screenMaterial != null) Destroy(screenMaterial);
        }
        void Update()
        {
            ShopReach.Pump();
            // 배터리를 꽂아 문이 열리면(또는 빼서 닫히면) 문을 고정하는 방법을 한 번 알려 준다(2026-11-14 사용자: 꽂은 직후에 안 나오던 것).
            if (IsSocket && !string.IsNullOrEmpty(removeGuideId))
            {
                bool inserted = ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(targetId);
                if (socketSeen && socketWasIn != inserted) GimmickGuide.ShowOnce(removeGuideId, "잠긴 문", "전력을 끊어 문을 고장내거나, 배터리를 계속 삽입하세요.");
                socketSeen = true; socketWasIn = inserted;
            }
            if (hiddenVisuals != null) foreach (var visual in hiddenVisuals) if (visual != null && visual != gameObject) visual.SetActive(Visible);
            // 열리는 모습: 처음부터 열려 있던 문은 연출 없이 열린 채로 두고, 지금 열린 문만 밀려난 뒤 사라진다.
            bool opened = Opened;
            if (!slideReady) { slideReady = true; slideT = opened ? 1f : 0f; }
            if (openSlideSeconds > 0f && hideOnOpen != null)
            {
                if (opened && slideT <= 0f && openWhenWithin > 0f && !Witnessed()) { }
                else if (opened && slideT < 1f)
                {
                    if (slideBase == null) { slideBase = new Vector3[hideOnOpen.Length]; for (int i = 0; i < hideOnOpen.Length; i++) if (hideOnOpen[i] != null) slideBase[i] = hideOnOpen[i].transform.position; }
                    slideT = Mathf.Min(1f, slideT + Time.deltaTime / openSlideSeconds);
                    var moved = openSlide * Mathf.SmoothStep(0f, 1f, slideT);
                    for (int i = 0; i < hideOnOpen.Length; i++) if (hideOnOpen[i] != null) hideOnOpen[i].transform.position = slideBase[i] + moved;
                }
                else if (!opened && slideBase != null)
                {
                    for (int i = 0; i < hideOnOpen.Length; i++) if (hideOnOpen[i] != null) hideOnOpen[i].transform.position = slideBase[i];
                    slideBase = null; slideT = 0f;
                }
            }
            else slideT = opened ? 1f : 0f;
            bool gone = opened && slideT >= 1f;
            if (blocker != null) blocker.enabled = !gone;
            if (hideOnOpen != null) foreach (var visual in hideOnOpen) if (visual != null && visual != gameObject) visual.SetActive(Visible && !gone && !Retired);
            if (showOnOpen != null) foreach (var visual in showOnOpen) if (visual != null && visual != gameObject) visual.SetActive(Visible && Opened);
            if (!panel) return;
            if (!PanelOwnerCanUse) { ClosePanel(); return; }
            // 단말기: 암호가 맞으면 바로 닫지 않고 탈출로 문 카메라 영상을 잠깐 보여 준다(2026-11-13 사용자).
            bool feed = IsTerminal && monitorCamera != null && Opened;
            if (feed && !feedShown && panelView != null)
            {
                feedShown = true; feedPicture = false; feedUntil = Time.unscaledTime + (FeedDoorOpens ? feedSeconds : 3f); Play(ref chime, "KeypadRight", .3f, 880f, false);
                // 문이 아직 열리지 않는 상황이면 영상 없이 "잠금 해제 완료"만 띄운다(2026-11-14 사용자: 영상은 열리는 화면만).
                if (!FeedDoorOpens) panelView.ShowFeed(Label, null, FeedStatus(), ClosePanel);
            }
            if (feedShown)
            {
                if (!feedPicture && FeedDoorOpens && panelView != null)
                {
                    feedPicture = true; feedUntil = Time.unscaledTime + feedSeconds;
                    if (monitorFeed == null) { monitorFeed = new RenderTexture(768, 432, 16); monitorFeed.name = "ExitDoorFeed"; monitorFeed.Create(); }
                    monitorCamera.targetTexture = monitorFeed; monitorCamera.enabled = true;
                    panelView.ShowFeed(Label, monitorFeed, FeedStatus(), ClosePanel);
                }
                if (feedPicture) feedWatchedUntil = Time.unscaledTime + .5f;
                // 열린 뒤에는 이 대상에 더 닿을 수 없으므로(이미 열림) 거리 판정 대신 시간·키로만 닫는다.
                if (Time.unscaledTime >= feedUntil || ExplorationState.Instance == null
                    || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    || (Time.unscaledTime > feedUntil - feedSeconds + .4f && CurtainCall.Network.PlayerSync.NetworkPlayer.Local != null
                        && CurtainCall.Network.PlayerSync.NetworkPlayer.Local.GetComponent<PlayerInputReader>()?.WasPressedThisFrame("Interact") == true)) ClosePanel();
                return;
            }
            if (NeedsPassword && Opened) Play(ref chime, "KeypadRight", .3f, 880f, false);
            if (panelView != null && NeedsPassword && ExplorationState.Instance != null && lastPanelMessage != ExplorationState.Instance.LastMessage)
            { lastPanelMessage = ExplorationState.Instance.LastMessage; panelView.SetMessage(lastPanelMessage); }
            if (ExplorationState.Instance == null || !ExplorationState.Instance.CanReachLocal(this) || !ConditionsMet
                || (NeedsPassword && Opened) || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || (Time.frameCount > openedFrame + 1 && CurtainCall.Network.PlayerSync.NetworkPlayer.Local != null
                    && CurtainCall.Network.PlayerSync.NetworkPlayer.Local.GetComponent<PlayerInputReader>()?.WasPressedThisFrame("Interact") == true)) ClosePanel();
        }
    }
}
