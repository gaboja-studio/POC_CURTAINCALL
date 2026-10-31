using System.Linq;
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
        public static bool IsKeyTarget(string id) => System.Array.IndexOf(ExplorationKeys.Targets, id) >= 0;
        public bool IsKeyPickup => IsKeyTarget(targetId);
        public bool UsesKey => HasKeySlots || (requiredTargets!=null && requiredTargets.Any(IsKeyTarget));
        public bool AcceptsKey(PropItem item) => item.Uid != 0 && item.Uses != 0 && requiredTargets != null
            && requiredTargets.Any(id => IsKeyTarget(id) && id == ExplorationKeys.TargetId(item.ItemId));
        public bool LocalHasKey => PropCarry.Instance != null && AcceptsKey(PropCarry.Instance.Selected(PropCarry.Instance.LocalClientId));
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
        [Tooltip("지정하면 그 대상이 열릴 때 함께 열리고 직접 F하지 않는다. 예: 키패드 정답 → 숨겨진 문.")]
        [SerializeField] string openedBy = "";
        public string OpenedBy => openedBy;
        public void ConfigureOpenedBy(string id) => openedBy = id ?? "";
        public bool Opened => ExplorationState.Instance != null && (ExplorationState.Instance.IsOpen(targetId)
            || (!string.IsNullOrEmpty(openedBy) && ExplorationState.Instance.IsOpen(openedBy)));
        public bool Visible => !revealPending && (string.IsNullOrEmpty(revealTarget) || (ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(revealTarget)));
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
        public bool HasKeySlots => keySlots != null && keySlots.Length > 1;
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
        public int SlotAt(Vector3 feet) => SlotAt(feet, out _);
        int SlotAt(Vector3 feet, out float distance)
        {
            int best = -1; distance = -1f;
            for (int i = 0; i < keySlots.Length; i++)
            {
                float d = keySlots[i] != null ? ExplorationReach.From(transform, keySlots[i].position, feet, reach, true) : -1f;
                if (d >= 0f && (best < 0 || d < distance)) { best = i; distance = d; }
            }
            return best;
        }
        public bool IsAvailable => Visible && string.IsNullOrEmpty(openedBy) && (!Opened || kind == ExplorationGateKind.Monitor);
        public string ActionLabel => !ConditionsMet ? WaitingRoomText.Format("gate.conditions-unmet", "{label} — 조건 미충족", ("label", label))
            : UsesKey && !LocalHasKey ? label + " — 맞는 열쇠를 선택하세요"
            : NeedsPartner ? (HasKeySlots ? WaitingRoomText.Format("gate.needs-two-keys", "{label} — 두 명이 각자 열쇠 구멍에서 함께 F", ("label", label))
                : WaitingRoomText.Format("gate.needs-two", "{label} — 두 명이 함께 F", ("label", label))) : label;
        UIInputBlocker inputBlocker;
        bool panel;
        ExplorationPanel panelView;
        string entered = "";
        string lastPanelMessage = "";
        int openedFrame;

        void Awake() { inputBlocker = gameObject.AddComponent<UIInputBlocker>(); inputBlocker.BlockWhileActive = false; }
        void Start(){if(IsKeyPickup && GetComponent<CurtainCall.ItemArt.ExplorationKeyVisual>()==null)gameObject.AddComponent<CurtainCall.ItemArt.ExplorationKeyVisual>();}
        void OnEnable() { ExplorationTargets.Add(this); ShopReach.Add(this); }
        void OnDisable() { ExplorationTargets.Remove(this); ShopReach.Remove(this); ClosePanel(); }
        public float Reach(Vector3 feet)
        {
            if (!IsAvailable) return -1;
            if (HasKeySlots) { SlotAt(feet, out float distance); return distance; }
            return ExplorationReach.From(transform, PresentationPoint, feet, reach, true);
        }
        public bool MatchesPassword(string value) => !passwordPending && ExplorationPuzzleRules.IsFourDigits(password) && value == password;
        public bool Resolve(int attemptNumber, System.Random rng) => attemptNumber >= (kind == ExplorationGateKind.Breakable ? requiredHits : 1);
        public string Describe(bool success, int attemptNumber) => success ? WaitingRoomText.Format("gate.done", "{label} 완료", ("label", label))
            : WaitingRoomText.Format("gate.progress", "{label} {n}/{total}", ("label", label), ("n", attemptNumber), ("total", requiredHits));
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
                if (NeedsPassword) { ExplorationState.Instance.RequestAnswered -= Answered; ExplorationState.Instance.RequestAnswered += Answered; }
                panelView.Open(label, NeedsPassword ? WaitingRoomText.Get("gate.enter-code", "숫자 네 자리를 입력하세요.") : monitorText, NeedsPassword, value => ExplorationState.Instance?.RequestCode(targetId, value), ClosePanel, false, monitorFeed);
            }
            else ExplorationState.Instance.RequestUse(targetId);
        }
        void ClosePanel()
        {
            panel = false; if (monitorCamera != null) monitorCamera.enabled = false; if (panelView != null) panelView.Hide(); if (inputBlocker != null) inputBlocker.SetOpen(false);
            if (ExplorationState.Instance != null) ExplorationState.Instance.RequestAnswered -= Answered;
        }
        // 오답: 경고음 + 입력 비움, 다시 입력 가능. 정답: 짧은 확인음 뒤 패널이 닫힌다.
        void Answered(string text)
        {
            if (!panel || panelView == null) return;
            panelView.SetMessage(text); lastPanelMessage = text;
            if (text == ExplorationState.WrongCode) { Play(ref buzz, "KeypadWrong", .45f, 180f, true); panelView.ClearCode(); }
        }
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
        }
        void Update()
        {
            ShopReach.Pump();
            if (hiddenVisuals != null) foreach (var visual in hiddenVisuals) if (visual != null && visual != gameObject) visual.SetActive(Visible);
            if (blocker != null) blocker.enabled = !Opened;
            if (hideOnOpen != null) foreach (var visual in hideOnOpen) if (visual != null && visual != gameObject) visual.SetActive(Visible && !Opened);
            if (showOnOpen != null) foreach (var visual in showOnOpen) if (visual != null && visual != gameObject) visual.SetActive(Visible && Opened);
            if (!panel) return;
            if (NeedsPassword && Opened) Play(ref chime, "KeypadRight", .3f, 880f, false);
            if (panelView != null && NeedsPassword && ExplorationState.Instance != null && lastPanelMessage != ExplorationState.Instance.LastMessage)
            { lastPanelMessage = ExplorationState.Instance.LastMessage; panelView.SetMessage(lastPanelMessage); }
            if (ExplorationState.Instance == null || !ExplorationState.Instance.CanReachLocal(this) || !ConditionsMet
                || (NeedsPassword && Opened) || (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame
                || (Time.frameCount > openedFrame + 1 && Keyboard.current.fKey.wasPressedThisFrame)))) ClosePanel();
        }
    }
}
