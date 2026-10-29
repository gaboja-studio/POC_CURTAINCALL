using System.Collections.Generic;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 숨은 길의 부술 수 있는 벽(Task-079 3단계). 빠루(천막 말뚝 뽑개)를 손에 들고 F를 연타하면 한 번에 한 번 내려친다.
    /// 결정(2026-10-29 사용자): 모든 탐색은 F, 6번이면 무너지고 칠 때마다 금이 늘고 낡아 보인다, 손을 떼도 진행은 남는다.
    /// 판정·공유는 <see cref="ExplorationState"/>, 이 컴포넌트는 받은 진행을 모든 화면에 똑같이 그린다(늦게 들어와도 같은 모습).
    /// </summary>
    public sealed class BreakableWall : MonoBehaviour, IShopTarget, IShopPresentation, IExplorationTarget
    {
        [SerializeField] string targetId = "restroom-breach";
        [SerializeField] string label = "금 간 벽";
        [SerializeField] string toolId = ItemUses.StakePuller;
        [SerializeField] string toolName = "빠루";
        [SerializeField, Min(1)] int requiredHits = 6;

        [Tooltip("닿는 수평 거리(m, 벽 중심에서).")]
        [SerializeField, Min(.5f)] float reach = 1.7f;

        [Tooltip("무너지기 전 길을 막는 충돌체.")]
        [SerializeField] Collider blocker;

        [Tooltip("벽을 이루는 조각. 칠 때마다 일부가 들뜨고 떨어지며, 무너지면 바닥으로 쏟아진다.")]
        [SerializeField] Transform[] pieces;

        [Tooltip("타격마다 하나씩 켜지는 금(앞에서부터 1타, 2타 ...).")]
        [SerializeField] GameObject[] cracks;

        [Tooltip("무너진 뒤 켜는 잔해·먼지.")]
        [SerializeField] GameObject aftermath;

        [Tooltip("무너지기 전에는 숨겨 옆 벽과 똑같이 보이게 하고, 무너진 뒤 드러내는 것(깨진 가장자리 조각·철근 등).")]
        [SerializeField] GameObject[] revealOnOpen;

        [SerializeField] AudioSource sound;

        public string TargetId => targetId;
        public string ToolId => toolId;
        public string ToolName => toolName;
        public string Label => label;
        public int RequiredHits => requiredHits;

        public Transform VisualRoot => transform;
        public Vector3 PresentationPoint => transform.position + Vector3.up * 1.6f;
        public bool IsAvailable => !Opened;
        public string ActionLabel => Opened ? "무너진 벽"
            : HoldsTool() ? $"{toolName}로 내려치기 ({Hits}/{requiredHits})"
            : $"{label} — {toolName}이(가) 필요하다 ({Hits}/{requiredHits})";

        int Hits => ExplorationState.Instance != null ? ExplorationState.Instance.Hits(targetId) : 0;
        bool Opened => ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(targetId);

        Vector3[] basePos;
        Quaternion[] baseRot;
        int shownHits = -1;
        bool shownOpen, inReach;
        float shake, collapse = -1f, messageUntil;
        string message = "";
        AudioClip thud, crash;

        void Awake()
        {
            int n = pieces?.Length ?? 0;
            basePos = new Vector3[n]; baseRot = new Quaternion[n];
            for (int i = 0; i < n; i++) if (pieces[i] != null) { basePos[i] = pieces[i].localPosition; baseRot[i] = pieces[i].localRotation; }
            thud = Tone("WallThud", .18f, 70f, 1.6f);
            crash = Tone("WallCrash", .9f, 45f, 3.2f);
            if (sound == null) { sound = gameObject.AddComponent<AudioSource>(); sound.spatialBlend = 1f; sound.maxDistance = 25f; }
        }

        void OnEnable()
        {
            ExplorationTargets.Add(this);
            ShopReach.Add(this);
        }

        void OnDisable()
        {
            ExplorationTargets.Remove(this);
            ShopReach.Remove(this);
            if (ExplorationState.Instance != null) ExplorationState.Instance.RequestAnswered -= Show;
        }

        void Start() { if (ExplorationState.Instance != null) ExplorationState.Instance.RequestAnswered += Show; }

        public float Reach(Vector3 feet)
        {
            if (Opened) return -1f;
            float d = new Vector2(feet.x - transform.position.x, feet.z - transform.position.z).magnitude;
            return d <= reach ? d : -1f;
        }

        public void Interact()
        {
            if (Opened || ExplorationState.Instance == null) return;
            ExplorationState.Instance.RequestUse(targetId);
        }

        void Update()
        {
            ShopReach.Pump();
            inReach = ShopReach.Focused == (IShopTarget)this;
            int hits = Hits; bool open = Opened;
            if (hits != shownHits || open != shownOpen) Apply(hits, open, shownHits < 0);
            Animate();
        }

        // 진행을 모습으로 옮긴다. 처음(늦게 들어온 화면 포함)은 소리·흔들림 없이 바로 그 상태로.
        void Apply(int hits, bool open, bool initial)
        {
            if (revealOnOpen != null) foreach (var g in revealOnOpen) if (g != null && g.activeSelf != open) g.SetActive(open);
            if (!initial && hits > shownHits && !open) { shake = .18f; Play(thud); }
            // 금은 서 있는 벽 위에만 있다. 무너지면 조각과 함께 사라진다.
            if (cracks != null) for (int i = 0; i < cracks.Length; i++) if (cracks[i] != null) cracks[i].SetActive(i < hits && !open);
            if (pieces != null)
                for (int i = 0; i < pieces.Length; i++)
                {
                    var p = pieces[i]; if (p == null) continue;
                    int loosenAt = 1 + i % Mathf.Max(1, requiredHits - 1);
                    bool loose = hits >= loosenAt, chipped = hits >= loosenAt + 2 && i % 4 == 0;
                    if (collapse < 0f) Pose(i, loose ? 1f : 0f, 0f);
                    p.gameObject.SetActive(!chipped || open);
                }
            if (open && !shownOpen)
            {
                if (blocker != null) blocker.enabled = false;
                if (aftermath != null) aftermath.SetActive(true);
                if (pieces != null) foreach (var p in pieces) if (p != null) p.gameObject.SetActive(true);
                if (initial) { collapse = 1f; Animate(); }
                else { collapse = 0f; Play(crash); }
            }
            else if (!open)
            {
                if (blocker != null) blocker.enabled = true;
                if (aftermath != null) aftermath.SetActive(false);
                collapse = -1f;
            }
            shownHits = hits; shownOpen = open;
        }

        void Animate()
        {
            if (shake > 0f)
            {
                shake -= Time.deltaTime;
                // 맞은 직후 조각이 잠깐 떨리고, 끝나면 그 타격의 자세로 돌아간다.
                float s = Mathf.Max(0f, shake) * .12f;
                if (pieces != null && collapse < 0f) foreach (var p in pieces) if (p != null && p.gameObject.activeSelf) p.localPosition += Random.insideUnitSphere * s * .05f;
                if (shake <= 0f && collapse < 0f) Apply(shownHits, shownOpen, true);
            }
            if (collapse >= 0f && collapse < 1f) collapse = Mathf.Min(1f, collapse + Time.deltaTime / .7f);
            if (collapse >= 0f && pieces != null) for (int i = 0; i < pieces.Length; i++) if (pieces[i] != null) Pose(i, 1f, collapse);
        }

        // 조각 i의 자세: loose(0~1)만큼 들뜨고 기울며, fall(0~1)만큼 바닥 쪽으로 쏟아진다. 조각마다 정해진 값이라 모든 화면이 같다.
        void Pose(int i, float loose, float fall)
        {
            var p = pieces[i];
            float a = Hash(i, 1), b = Hash(i, 2), c = Hash(i, 3);
            var tilt = Quaternion.Euler((a - .5f) * 14f * loose, (b - .5f) * 10f * loose, (c - .5f) * 18f * loose);
            var pos = basePos[i] + new Vector3(0, 0, (a - .3f) * .05f * loose);
            if (fall > 0f)
            {
                // 벽 기준(바닥 y=0, z는 벽 두께 방향): 양쪽 바닥으로 흩어진다. 떨어질수록 빨라진다.
                float t = fall * fall;
                var rest = new Vector3(basePos[i].x * (1f + b * .3f), .06f + c * .12f, (a - .5f) * 1.4f + Mathf.Sign(a - .5f) * .3f);
                pos = Vector3.Lerp(pos, rest, t);
                tilt = Quaternion.Slerp(tilt, Quaternion.Euler(a * 80f - 40f, b * 120f, c * 90f - 45f), t);
            }
            p.localPosition = pos; p.localRotation = baseRot[i] * tilt;
        }

        static float Hash(int i, int salt) => Mathf.Repeat(Mathf.Sin(i * 12.9898f + salt * 78.233f) * 43758.5453f, 1f);

        bool HoldsTool() => ExplorationTargets.LocalHolds(toolId);

        // ---- 탐색 대상 판정(판정 화면) ----
        public bool Resolve(int attemptNumber, System.Random rng) => attemptNumber >= requiredHits;
        public string Describe(bool success, int attemptNumber) => success ? $"{label}이(가) 무너졌습니다." : $"{label} 내려침 {attemptNumber}/{requiredHits}";
        public string RewardItemId => "";
        public Vector3 RewardPoint => transform.position;
        public int RewardUidOffset => 0;

        void Show(string text)
        {
            if (!inReach || string.IsNullOrEmpty(text)) return;
            message = text; messageUntil = Time.unscaledTime + 2f;
        }

        void Play(AudioClip clip) { if (sound != null && clip != null) sound.PlayOneShot(clip); }

        // 둔탁한 타격음·무너지는 소리(임시 합성음). 실제 효과음이 정해지면 바꾼다.
        static AudioClip Tone(string name, float seconds, float pitch, float noise)
        {
            int rate = 22050, n = Mathf.CeilToInt(rate * seconds); var data = new float[n]; var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate, env = Mathf.Exp(-t * (8f / seconds));
                data[i] = (Mathf.Sin(2 * Mathf.PI * pitch * t) * .6f + ((float)rng.NextDouble() * 2 - 1) * .4f * noise * Mathf.Exp(-t * 20f)) * env * .5f;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false); clip.SetData(data, 0); return clip;
        }

        void OnGUI()
        {
            if (Time.unscaledTime < messageUntil)
                GUI.Box(new Rect((Screen.width - 360) / 2f, Screen.height * .68f, 360, 28), message);
            if (inReach && !ShopReach.HasExternalPresentation && !InputGate.IsBlocked)
                GUI.Box(new Rect((Screen.width - 360) / 2f, Screen.height * .62f, 360, 28), "[F] " + ActionLabel);
        }
    }
}
