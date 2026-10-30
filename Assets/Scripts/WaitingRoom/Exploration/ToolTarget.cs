using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    public enum ToolTargetKind { HairpinLock, Pry }

    /// <summary>
    /// 탐색 도구로 여는 대상(Task-079, 기물 시스템 초안).
    /// - 좌물쇠(마술사의 머리핀): F 한 번이 한 번의 시도. 시도마다 확률(초안 50%)로 열리고, 같은 좌물쇠의 보장 번째(초안 3번째) 시도는
    ///   반드시 열린다. 시도 수는 팀 공유·런 유지. 개방 성공 때만 머리핀 1 소모.
    /// - 판자·덮개·걸린 서랍(천막 말뚝 뽑개): F 한 번에 뜯어 연다. 개방 때 1 소모.
    /// 열리면 막음 충돌이 풀리고(뚜껑·문이 돌아가거나 판자가 떨어짐) 보상 발견품이 있으면 그 자리에 나온다. 모든 화면이 같은 상태를 그린다.
    /// </summary>
    public sealed class ToolTarget : MonoBehaviour, IShopTarget, IShopPresentation, IExplorationTarget
    {
        [SerializeField] string targetId = "locked-box";
        [SerializeField] ToolTargetKind kind = ToolTargetKind.HairpinLock;
        [SerializeField] string label = "잠긴 상자";
        [SerializeField, Min(.5f)] float reach = 1.6f;

        [Tooltip("열리기 전 막는 충돌체(문·판자). 없으면 막지 않는다.")]
        [SerializeField] Collider blocker;
        [Tooltip("열릴 때 돌아가는 뚜껑·문(없으면 생략).")]
        [SerializeField] Transform hinge;
        [SerializeField] Vector3 openEuler = new(-100, 0, 0);
        [Tooltip("열리면 사라지는 것(좌물쇠·걸쇠·못 박힌 판자 등).")]
        [SerializeField] GameObject[] hideOnOpen;
        [Tooltip("열리면 나타나는 것(속 공간 등).")]
        [SerializeField] GameObject[] showOnOpen;

        [Tooltip("열리면 그 자리에 나오는 발견품 ID(비우면 없음).")]
        [SerializeField] string rewardItemId = "";
        [SerializeField] Transform rewardPoint;
        [SerializeField, Range(1, 99)] int rewardUidOffset = 50;
        [SerializeField] AudioSource sound;

        /// <summary>기존 개방 방식·확률·보상·내구도는 그대로 두고 경로 ID만 연결한다.</summary>
        public void ConfigureRoute(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new System.ArgumentException(nameof(id));
            targetId = id;
        }

        /// <summary>기존 개방 연출을 재사용해 내용물을 공개한다. 씬 에셋은 저장하지 않는다.</summary>
        public void RevealWhenOpened(GameObject content)
        {
            if (content == null || content == gameObject || transform.IsChildOf(content.transform))
                throw new System.ArgumentException(nameof(content));
            var contents = new System.Collections.Generic.List<GameObject>(showOnOpen ?? System.Array.Empty<GameObject>());
            if (!contents.Contains(content)) contents.Add(content);
            showOnOpen = contents.ToArray();
            content.SetActive(Opened);
        }

        public string TargetId => targetId;
        public string ToolId => kind == ToolTargetKind.HairpinLock ? ItemUses.MagicianHairpin : ItemUses.StakePuller;
        public string ToolName => ItemCatalog.Get(ToolId)?.displayName ?? (kind == ToolTargetKind.HairpinLock ? "마술사의 머리핀" : "천막 말뚝 뽑개");
        public string Label => label;
        public string RewardItemId => rewardItemId;
        public Vector3 RewardPoint => rewardPoint != null ? rewardPoint.position : transform.position;
        public int RewardUidOffset => rewardUidOffset;

        public Transform VisualRoot => transform;
        public Vector3 PresentationPoint => transform.position + Vector3.up * .6f;
        public bool IsAvailable => !Opened;
        public string ActionLabel => Opened ? label
            : !ExplorationTargets.LocalHolds(ToolId) ? $"{label} — {ToolName}이(가) 필요하다"
            : kind == ToolTargetKind.HairpinLock ? $"{ToolName}로 자물쇠 따기{(Attempts > 0 ? $" (시도 {Attempts})" : "")}" : $"{ToolName}로 뜯기";

        int Attempts => ExplorationState.Instance != null ? ExplorationState.Instance.Hits(targetId) : 0;
        bool Opened => ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(targetId);

        Quaternion closedRot;
        int shownAttempts = -1;
        bool shownOpen, inReach;
        float openT = -1f, jiggle, messageUntil;
        string message = "";
        AudioClip click, fail;

        void Awake()
        {
            if (hinge != null) closedRot = hinge.localRotation;
            click = Tone("Click", .12f, 900f, .6f);
            fail = Tone("Rattle", .22f, 260f, 1.4f);
            if (sound == null) { sound = gameObject.AddComponent<AudioSource>(); sound.spatialBlend = 1f; sound.maxDistance = 20f; }
        }

        void OnEnable() { ExplorationTargets.Add(this); ShopReach.Add(this); }
        void OnDisable()
        {
            ExplorationTargets.Remove(this); ShopReach.Remove(this);
            if (ExplorationState.Instance != null) ExplorationState.Instance.RequestAnswered -= Show;
        }
        void Start() { if (ExplorationState.Instance != null) ExplorationState.Instance.RequestAnswered += Show; }

        public float Reach(Vector3 feet)
        {
            if (Opened) return -1f;
            return ExplorationReach.From(transform, PresentationPoint, feet, reach);
        }

        public void Interact() { if (!Opened && ExplorationState.Instance != null) ExplorationState.Instance.RequestUse(targetId); }

        // ---- 판정(판정 화면) ----
        public bool Resolve(int attemptNumber, System.Random rng)
        {
            if (kind == ToolTargetKind.Pry) return true;
            var e = ItemCatalog.Get(ItemUses.MagicianHairpin);
            float chance = e != null && e.effect > 0 ? e.effect : .5f; int guarantee = e != null && e.effect2 > 0 ? Mathf.RoundToInt(e.effect2) : 3;
            return ExplorationBook.LockOpens(attemptNumber, chance, guarantee, rng.NextDouble());
        }

        public string Describe(bool success, int attemptNumber) => kind == ToolTargetKind.Pry
            ? $"{label}을(를) 뜯어 열었습니다."
            : success ? $"딸깍 — {label}의 자물쇠가 열렸습니다." : $"자물쇠가 헛돌았습니다. (시도 {attemptNumber})";

        void Update()
        {
            ShopReach.Pump();
            inReach = ShopReach.Focused == (IShopTarget)this;
            int attempts = Attempts; bool open = Opened;
            if (attempts != shownAttempts || open != shownOpen) Apply(attempts, open, shownAttempts < 0);
            if (openT >= 0f && openT < 1f) { openT = Mathf.Min(1f, openT + Time.deltaTime / .45f); PoseOpen(openT); }
            if (jiggle > 0f && hinge != null && openT < 0f)
            {
                jiggle -= Time.deltaTime;
                hinge.localRotation = closedRot * Quaternion.Euler(Mathf.Sin(Time.time * 80f) * 2f * Mathf.Max(0, jiggle) * 5f, 0, 0);
                if (jiggle <= 0f) hinge.localRotation = closedRot;
            }
        }

        void Apply(int attempts, bool open, bool initial)
        {
            if (!initial && attempts > shownAttempts && !open) { jiggle = .2f; Play(fail); }
            if (blocker != null) blocker.enabled = !open;
            if (hideOnOpen != null) foreach (var g in hideOnOpen) if (g != null) g.SetActive(!open);
            if (showOnOpen != null) foreach (var g in showOnOpen) if (g != null) g.SetActive(open);
            if (open && !shownOpen) { if (initial) { openT = 1f; PoseOpen(1f); } else { openT = 0f; Play(click); } }
            else if (!open) { openT = -1f; if (hinge != null) hinge.localRotation = closedRot; }
            shownAttempts = attempts; shownOpen = open;
        }

        void PoseOpen(float t) { if (hinge != null) hinge.localRotation = closedRot * Quaternion.Euler(openEuler * Mathf.SmoothStep(0, 1, t)); }

        void Show(string text) { if (inReach && !string.IsNullOrEmpty(text)) { message = text; messageUntil = Time.unscaledTime + 2f; } }
        void Play(AudioClip clip) { if (sound != null && clip != null) sound.PlayOneShot(clip); }

        // 임시 합성음(딸깍·헛도는 소리). 실제 효과음이 정해지면 바꾼다.
        static AudioClip Tone(string name, float seconds, float pitch, float noise)
        {
            int rate = 22050, n = Mathf.CeilToInt(rate * seconds); var data = new float[n]; var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate, env = Mathf.Exp(-t * (10f / seconds));
                data[i] = (Mathf.Sin(2 * Mathf.PI * pitch * t) * .5f + ((float)rng.NextDouble() * 2 - 1) * .3f * noise) * env * .4f;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false); clip.SetData(data, 0); return clip;
        }

        void OnGUI()
        {
            if (Time.unscaledTime < messageUntil) GUI.Box(new Rect((Screen.width - 360) / 2f, Screen.height * .68f, 360, 28), message);
            if (inReach && !ShopReach.HasExternalPresentation && !InputGate.IsBlocked)
                GUI.Box(new Rect((Screen.width - 360) / 2f, Screen.height * .62f, 360, 28), "[F] " + ActionLabel);
        }
    }
}
