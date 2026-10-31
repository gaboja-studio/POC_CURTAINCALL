using System;
using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// UV 등으로만 보이는 숨겨진 낙서(2026-10-30 확정 규칙).
    /// 평소에는 안 보이고, 로컬 플레이어의 UV 손전등에 닿으면 노란 형광으로 드러나 5초 동안 남아 조사할 수 있다. 다시 비추면 다시 드러난다.
    /// 처음 비춘 때 호스트가 발견을 기록(런 동안·팀 공유)하고, 발견 화면에서는 0.5초 전기음과 함께 카메라가 낙서 쪽으로 한 번 돈다.
    /// F 상호작용은 없다. 판정은 <see cref="ExplorationState"/>(장비는 어느 슬롯이든 인정, 소모 없음).
    /// </summary>
    public sealed class UvMark : MonoBehaviour, IExplorationTarget
    {
        [SerializeField] string targetId = "uv-mark";
        [SerializeField] string label = "숨겨진 낙서";
        [Tooltip("손전등 빛이 닿는 최대 거리(m). 시야가 막히면 닿지 않는다.")]
        [SerializeField, Min(.5f)] float reach = 10f;
        [Tooltip("비추는 동안 켜지는 형광 낙서(평소에는 꺼 둔다).")]
        [SerializeField] GameObject[] marks;
        [SerializeField] AudioSource sound;
        [SerializeField] Color glow = new(1f, .95f, .25f);

        public static readonly List<UvMark> All = new();
        /// <summary>어느 화면에서든 새로 발견된 낙서(발견 상태가 켜진 프레임).</summary>
        public static event Action<UvMark> Discovered;

        static Material glowMaterial;

        /// <summary>기존 UV 발견 규칙을 보존하고 경로 ID만 연결한다.</summary>
        public void ConfigureRoute(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(nameof(id));
            targetId = id;
        }
        public void RevealWhenFound(GameObject content)
        {
            if (content == null || content == gameObject || transform.IsChildOf(content.transform))
                throw new ArgumentException(nameof(content));
            var contents = new List<GameObject>(marks ?? Array.Empty<GameObject>());
            if (!contents.Contains(content)) contents.Add(content);
            marks = contents.ToArray();
            content.SetActive(Lit);
        }

        public string TargetId => targetId;
        public string ToolId => ItemUses.UvLamp;
        public string ToolName => ItemCatalog.Get(ToolId)?.displayName ?? WaitingRoomText.Get("uv.name-lamp", "서커스 UV 등");
        public string Label => label;
        public string RewardItemId => "";
        public Vector3 RewardPoint => transform.position;
        public int RewardUidOffset => 0;
        public Vector3 Point => transform.position + Vector3.up * .3f;
        /// <summary>낙서 그림이 실제로 그려진 자리(손전등 판정 기준, 2026-10-30).</summary>
        public Vector3 Center => transform.position;

        /// <summary>이 낙서의 일부(그림·함께 드러나는 단서)인지. 손전등 가림 판정에서 뺀다.</summary>
        public bool Owns(Transform t)
        {
            if (t == null) return false;
            if (t.IsChildOf(transform)) return true;
            if (marks != null) foreach (var m in marks) if (m != null && t.IsChildOf(m.transform)) return true;
            return false;
        }

        public bool Found => ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(targetId);
        /// <summary>이 화면의 UV 손전등이 지금 비추고 있는지(로컬 표시 전용).</summary>
        public bool Lit { get; private set; }
        [Tooltip("빛이 떠난 뒤에도 낙서가 남아 조사할 수 있는 시간(초). 다시 비추면 연장된다.")]
        [SerializeField, Min(0f)] float holdSeconds = 5f;
        bool shown, first = true;
        float litUntil = -1f;
        AudioClip buzz;

        void Awake()
        {
            buzz = Buzz();
            if (sound == null) { sound = gameObject.AddComponent<AudioSource>(); sound.spatialBlend = 1f; sound.maxDistance = 15f; }
            ApplyGlow();
            SetMarks(false);
        }

        void OnEnable() { ExplorationTargets.Add(this); All.Add(this); }
        void OnDisable() { ExplorationTargets.Remove(this); All.Remove(this); }

        /// <summary>호스트 판정: 손전등 거리 안이고 시야가 막히지 않으면 닿는다.</summary>
        public float Reach(Vector3 feet) => Found ? -1f : ExplorationReach.From(transform, Point, feet, reach, true);

        /// <summary>UV 손전등이 이번 프레임에 비췄다. 빛이 떠나도 holdSeconds 동안 남아 조사할 수 있다.</summary>
        public void Illuminate() { litUntil = Time.time + holdSeconds; }

        public bool Resolve(int attemptNumber, System.Random rng) => true;
        public string Describe(bool success, int attemptNumber) => WaitingRoomText.Get("uv.revealed", "숨겨진 낙서가 형광으로 드러났습니다.");

        void Update()
        {
            Lit = Time.time <= litUntil;
            bool found = Found;
            if (found && !shown && !first)
            {
                if (sound != null) sound.PlayOneShot(buzz);
                Discovered?.Invoke(this);
            }
            shown = found; first = false;
            SetMarks(Lit);
        }

        void SetMarks(bool on) { if (marks != null) foreach (var m in marks) if (m != null && m.activeSelf != on) m.SetActive(on); }

        // 낙서는 노란 형광(자체 발광) 재질로 보인다. 원본 재질은 건드리지 않는다.
        // 그림이 있는 재질(화살표·X 같은 투명 그림)은 그림을 지키고 색만 노란 형광으로 바꾼 사본을 쓴다(2026-10-30: 사각형으로 덮이던 문제).
        static readonly Dictionary<Material, Material> tinted = new();
        void ApplyGlow()
        {
            if (marks == null) return;
            if (glowMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) return;
                glowMaterial = new Material(shader) { name = "UvGlowYellow", hideFlags = HideFlags.DontSave };
                glowMaterial.SetColor("_BaseColor", glow);
                glowMaterial.EnableKeyword("_EMISSION");
                glowMaterial.SetColor("_EmissionColor", glow * 2.2f);
                glowMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            foreach (var m in marks)
                if (m != null) foreach (var r in m.GetComponentsInChildren<Renderer>(true))
                    if (r.GetComponent<TextMesh>() == null) r.sharedMaterial = Glow(r.sharedMaterial);
        }

        Material Glow(Material source)
        {
            if (source == null || source.mainTexture == null || source.renderQueue < (int)UnityEngine.Rendering.RenderQueue.AlphaTest) return glowMaterial;
            if (tinted.TryGetValue(source, out var copy) && copy != null) return copy;
            copy = new Material(source) { name = "UvGlow_" + source.name, hideFlags = HideFlags.DontSave };
            if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", glow);
            if (copy.HasProperty("_EmissionColor")) { copy.EnableKeyword("_EMISSION"); copy.SetColor("_EmissionColor", glow * 2.2f); }
            return tinted[source] = copy;
        }

        // 0.5초 전기음(임시 합성음). 실제 효과음이 정해지면 바꾼다.
        static AudioClip Buzz()
        {
            int rate = 22050, n = rate / 2; var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate, crackle = Mathf.Sin(2 * Mathf.PI * 120f * t) * Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 37f * t));
                data[i] = crackle * .18f * Mathf.Exp(-t * 3f);
            }
            var clip = AudioClip.Create("UvBuzz", n, 1, rate, false); clip.SetData(data, 0); return clip;
        }
    }
}
