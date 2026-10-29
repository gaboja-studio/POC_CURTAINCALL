using CurtainCall.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CurtainCall.WaitingRoom
{
    public enum ExplorationGateKind { Open, Monitor, Keypad, Cooperative, Breakable }

    /// <summary>F 공통 대상. 열쇠/단서/패드 등 선행 대상의 열린 상태를 조건으로 사용한다.</summary>
    public sealed class ExplorationGate : MonoBehaviour, IExplorationTarget, IShopTarget, IShopPresentation
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
        [SerializeField, Min(.1f)] float cooperationWindow = .75f;
        [SerializeField, Min(.5f)] float reach = 1.6f;
        [SerializeField] string password = "0000";
        [SerializeField] bool passwordPending, passwordExplicit;
        [SerializeField, TextArea] string monitorText = "";
        [SerializeField] Camera monitorCamera;
        RenderTexture monitorFeed;
        [SerializeField] string rewardItemId = "";
        [SerializeField, Range(1, 99)] int rewardUidOffset = 60;
        [SerializeField] Transform rewardPoint;

        public string TargetId => targetId;
        public string ToolId => toolId;
        public string ToolName => ItemCatalog.Get(toolId)?.displayName ?? toolId;
        public string Label => label;
        public string RewardItemId => rewardItemId;
        public Vector3 RewardPoint => rewardPoint != null ? rewardPoint.position : transform.position;
        public int RewardUidOffset => rewardUidOffset;
        public bool IsOpenGate => kind == ExplorationGateKind.Open;
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
        public bool NeedsPartner => kind == ExplorationGateKind.Cooperative;
        public float CooperationWindow => cooperationWindow;
        public bool NeedsPassword => kind == ExplorationGateKind.Keypad;
        public bool Opened => ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(targetId);
        public bool Visible => !revealPending && (string.IsNullOrEmpty(revealTarget) || (ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(revealTarget)));
        public bool ConditionsMet
        {
            get
            {
                if (!Visible || interactionPending || (NeedsPassword && passwordPending)) return false;
                if (requiredTargets != null) foreach (var id in requiredTargets)
                    if (string.IsNullOrEmpty(id) || ExplorationState.Instance == null || !ExplorationState.Instance.IsOpen(id)) return false;
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
        public Vector3 PresentationPoint => transform.position;
        public bool IsAvailable => Visible && (!Opened || kind == ExplorationGateKind.Monitor);
        public string ActionLabel => label + (!ConditionsMet ? " — 조건 미충족" : NeedsPartner ? " — 두 명이 함께 F" : "");
        UIInputBlocker inputBlocker;
        bool panel;
        ExplorationPanel panelView;
        string entered = "";
        string lastPanelMessage = "";
        int openedFrame;

        void Awake() { inputBlocker = gameObject.AddComponent<UIInputBlocker>(); inputBlocker.BlockWhileActive = false; }
        void OnEnable() { ExplorationTargets.Add(this); ShopReach.Add(this); }
        void OnDisable() { ExplorationTargets.Remove(this); ShopReach.Remove(this); ClosePanel(); }
        public float Reach(Vector3 feet)
        {
            if (!IsAvailable) return -1;
            float d = Vector3.Distance(feet, transform.position);
            return d <= reach ? d : -1;
        }
        public bool MatchesPassword(string value) => !passwordPending && ExplorationPuzzleRules.IsFourDigits(password) && value == password;
        public bool Resolve(int attemptNumber, System.Random rng) => attemptNumber >= (kind == ExplorationGateKind.Breakable ? requiredHits : 1);
        public string Describe(bool success, int attemptNumber) => success ? label + " 완료" : label + " " + attemptNumber + "/" + requiredHits;
        public void Interact()
        {
            if (!ConditionsMet || ExplorationState.Instance == null) return;
            if (NeedsPassword || kind == ExplorationGateKind.Monitor)
            {
                if (panel) return;
                panel = true; entered = ""; openedFrame = Time.frameCount; inputBlocker.SetOpen(true);
                if (kind == ExplorationGateKind.Monitor && !Opened) ExplorationState.Instance.RequestUse(targetId);
                if (panelView == null) panelView = gameObject.AddComponent<ExplorationPanel>();
                if (monitorCamera != null)
                {
                    if (monitorFeed == null) { monitorFeed = new RenderTexture(768, 432, 16); monitorFeed.name = "MachineRoomFeed"; monitorFeed.Create(); }
                    monitorCamera.targetTexture = monitorFeed; monitorCamera.enabled = true;
                }
                lastPanelMessage = ExplorationState.Instance.LastMessage;
                panelView.Open(label, NeedsPassword ? "숫자 네 자리를 입력하세요." : monitorText, NeedsPassword, value => ExplorationState.Instance?.RequestCode(targetId, value), ClosePanel, false, monitorFeed);
            }
            else ExplorationState.Instance.RequestUse(targetId);
        }
        void ClosePanel() { panel = false; if (monitorCamera != null) monitorCamera.enabled = false; if (panelView != null) panelView.Hide(); if (inputBlocker != null) inputBlocker.SetOpen(false); }
        void OnDestroy()
        {
            if (monitorCamera != null) monitorCamera.targetTexture = null;
            if (monitorFeed != null) { monitorFeed.Release(); Destroy(monitorFeed); }
        }
        void Update()
        {
            ShopReach.Pump();
            if (hiddenVisuals != null) foreach (var visual in hiddenVisuals) if (visual != null && visual != gameObject) visual.SetActive(Visible);
            if (blocker != null) blocker.enabled = !Opened;
            if (hideOnOpen != null) foreach (var visual in hideOnOpen) if (visual != null && visual != gameObject) visual.SetActive(Visible && !Opened);
            if (showOnOpen != null) foreach (var visual in showOnOpen) if (visual != null && visual != gameObject) visual.SetActive(Visible && Opened);
            if (!panel) return;
            if (panelView != null && NeedsPassword && ExplorationState.Instance != null && lastPanelMessage != ExplorationState.Instance.LastMessage)
            { lastPanelMessage = ExplorationState.Instance.LastMessage; panelView.SetMessage(lastPanelMessage); }
            if (ExplorationState.Instance == null || !ExplorationState.Instance.CanReachLocal(this) || !ConditionsMet
                || (NeedsPassword && Opened) || (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame
                || (Time.frameCount > openedFrame + 1 && Keyboard.current.fKey.wasPressedThisFrame)))) ClosePanel();
        }
    }
}
