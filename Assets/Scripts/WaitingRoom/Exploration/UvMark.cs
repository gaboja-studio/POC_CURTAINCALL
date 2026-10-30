using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 서커스 UV 등으로 드러나는 숨은 표식(Task-079, 기물 시스템 초안). 맨눈에는 보이지 않는 형광 글씨·화살표·기호다.
    /// UV 등을 손에 들고 의심스러운 표면에 F → 표식이 보랏빛으로 드러나고, 발견 상태는 런 동안·팀 모두에게 남는다.
    /// 새 단서를 처음 발견할 때만 UV 등 1 소모(이미 발견한 표식·빈 곳은 소모 없음).
    /// </summary>
    public sealed class UvMark : MonoBehaviour, IShopTarget, IShopPresentation, IExplorationTarget
    {
        [SerializeField] string targetId = "uv-mark";
        [SerializeField] string label = "의심스러운 얼룩";
        [SerializeField, Min(.5f)] float reach = 1.8f;
        [Tooltip("발견하면 켜지는 형광 표식(평소에는 꺼 둔다).")]
        [SerializeField] GameObject[] marks;
        [SerializeField] AudioSource sound;

        /// <summary>기존 UV 발견·내구도 규칙을 보존하고 경로 ID만 연결한다.</summary>
        public void ConfigureRoute(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new System.ArgumentException(nameof(id));
            targetId = id;
        }
        public void RevealWhenFound(GameObject content)
        {
            if (content == null || content == gameObject || transform.IsChildOf(content.transform))
                throw new System.ArgumentException(nameof(content));
            var contents = new System.Collections.Generic.List<GameObject>(marks ?? System.Array.Empty<GameObject>());
            if (!contents.Contains(content)) contents.Add(content);
            marks = contents.ToArray();
            content.SetActive(Found);
        }

        public string TargetId => targetId;
        public string ToolId => ItemUses.UvLamp;
        public string ToolName => ItemCatalog.Get(ToolId)?.displayName ?? "서커스 UV 등";
        public string Label => label;
        public string RewardItemId => "";
        public Vector3 RewardPoint => transform.position;
        public int RewardUidOffset => 0;

        public Transform VisualRoot => transform;
        public Vector3 PresentationPoint => transform.position + Vector3.up * .3f;
        public bool IsAvailable => !Found;
        public string ActionLabel => ExplorationTargets.LocalHolds(ToolId) ? $"{ToolName}로 비추기" : label;

        bool Found => ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(targetId);
        bool shown, inReach, first = true;
        AudioClip buzz;

        void Awake()
        {
            buzz = Buzz();
            if (sound == null) { sound = gameObject.AddComponent<AudioSource>(); sound.spatialBlend = 1f; sound.maxDistance = 15f; }
            SetMarks(false);
        }

        void OnEnable() { ExplorationTargets.Add(this); ShopReach.Add(this); }
        void OnDisable() { ExplorationTargets.Remove(this); ShopReach.Remove(this); }

        public float Reach(Vector3 feet)
        {
            if (Found) return -1f;
            return ExplorationReach.From(transform, PresentationPoint, feet, reach);
        }

        public void Interact() { if (!Found && ExplorationState.Instance != null) ExplorationState.Instance.RequestUse(targetId); }

        public bool Resolve(int attemptNumber, System.Random rng) => true;
        public string Describe(bool success, int attemptNumber) => "보랏빛 형광 표식이 드러났습니다.";

        void Update()
        {
            ShopReach.Pump();
            inReach = ShopReach.Focused == (IShopTarget)this;
            bool found = Found;
            if (found != shown || first)
            {
                SetMarks(found);
                if (found && !first && sound != null) sound.PlayOneShot(buzz);
                shown = found; first = false;
            }
        }

        void SetMarks(bool on) { if (marks != null) foreach (var m in marks) if (m != null) m.SetActive(on); }

        // 짧은 전기음(임시).
        static AudioClip Buzz()
        {
            int rate = 22050, n = rate / 4; var data = new float[n];
            for (int i = 0; i < n; i++) { float t = (float)i / rate; data[i] = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 120f * t)) * .15f * Mathf.Exp(-t * 6f); }
            var clip = AudioClip.Create("UvBuzz", n, 1, rate, false); clip.SetData(data, 0); return clip;
        }

        void OnGUI()
        {
            if (inReach && !ShopReach.HasExternalPresentation && !InputGate.IsBlocked)
                GUI.Box(new Rect((Screen.width - 300) / 2f, Screen.height * .62f, 300, 28), "[F] " + ActionLabel);
        }
    }
}
