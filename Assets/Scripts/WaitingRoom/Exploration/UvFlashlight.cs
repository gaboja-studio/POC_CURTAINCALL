using System.Collections.Generic;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 서커스 UV 등 손전등(2026-10-30 확정 규칙). 씬에 하나.
    /// 로컬 플레이어의 슬롯에 UV 등이 있고 우클릭을 누르는 동안, 가슴 높이에서 시선 방향으로 보라색 원뿔 빛이 나가고
    /// 빛이 닿는 자리에 원형 표시가 생긴다. 비추면서 이동할 수 있다. 원뿔 안에 들어온 <see cref="UvMark"/>는 형광으로 보이고,
    /// 처음 비춘 낙서는 호스트에 발견을 요청한다. 발견되면 카메라가 낙서 쪽으로 한 번 돈다(마우스 시선 시점에서만).
    /// </summary>
    public sealed class UvFlashlight : MonoBehaviour
    {
        [SerializeField] Color color = new(.6f, .25f, 1f);
        [SerializeField, Min(1f)] float range = 12f;
        [SerializeField, Range(10f, 120f)] float spotAngle = 50f;
        [SerializeField, Min(0f)] float intensity = 60f;
        [Tooltip("원형 표시 중심에서 이 거리(m) 안의 낙서는 원뿔 각도와 관계없이 비춘 것으로 본다.")]
        [SerializeField, Min(.1f)] float ringReach = 1f;
        [Tooltip("같은 낙서에 발견 요청을 다시 보내기까지의 간격(초).")]
        [SerializeField, Min(.1f)] float requestInterval = .6f;

        Light spot;
        Transform ring;
        Renderer ringRenderer;
        readonly Dictionary<UvMark, float> requested = new();
        readonly HashSet<UvMark> litNow = new();
        static readonly RaycastHit[] hits = new RaycastHit[8];

        public bool IsOn { get; private set; }

        void Awake()
        {
            var go = new GameObject("UvSpot") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            spot = go.AddComponent<Light>();
            spot.type = LightType.Spot; spot.color = color; spot.range = range; spot.spotAngle = spotAngle; spot.innerSpotAngle = spotAngle * .55f;
            spot.intensity = intensity; spot.shadows = LightShadows.None; spot.enabled = false;
            BuildRing();
        }

        void OnEnable() => UvMark.Discovered += OnDiscovered;
        void OnDisable() { UvMark.Discovered -= OnDiscovered; if (spot != null) spot.enabled = false; if (ringRenderer != null) ringRenderer.enabled = false; }

        void Update()
        {
            var player = NetworkPlayer.Local;
            var reader = player != null ? player.GetComponent<PlayerInputReader>() : FindFirstObjectByType<PlayerInputReader>();
            var body = reader != null ? reader.transform : null;
            bool on = body != null && reader.isActiveAndEnabled && reader.Current.UvLightHeld && !InputGate.IsBlocked && Equipped(player, reader);
            IsOn = on;
            litNow.Clear();
            if (!on) { spot.enabled = false; ringRenderer.enabled = false; return; }

            // 마우스가 가리키는 자리: 카메라 시선이 닿는 점(없으면 12m 앞). 가슴에서 그 점을 향해 비춘다(3인칭에서 카메라 기울기를 그대로 쓰면 바닥을 비춘다).
            // 마우스 시선 시점(1인칭·3인칭 비고정)에서는 카메라가 가리키는 자리를, 고정 3인칭에서는 캐릭터가 보는 방향을 비춘다.
            var cam = Camera.main; var director = CameraViewDirector.Current;
            bool mouseLook = director != null && director.UsesMouseLook && cam != null;
            var chest = body.position + Vector3.up * 1.4f;
            var look = mouseLook ? cam.transform.forward : Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
            var eye = mouseLook ? cam.transform.position : chest;
            var aim = AimPoint(eye, look, mouseLook ? Vector3.Distance(eye, chest) : 0f);
            var forward = (aim - chest).sqrMagnitude > .01f ? (aim - chest).normalized : Vector3.ProjectOnPlane(look, Vector3.up).normalized;
            var origin = chest + Vector3.ProjectOnPlane(forward, Vector3.up).normalized * .35f;
            LastOrigin = origin; LastForward = forward;
            spot.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(forward));
            spot.enabled = true;

            // 빛이 닿는 자리의 원형 표시.
            if (Physics.Raycast(origin, forward, out var hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                ring.SetPositionAndRotation(hit.point + hit.normal * .015f, Quaternion.LookRotation(-hit.normal));
                float size = Mathf.Lerp(.25f, 1.6f, hit.distance / range);
                ring.localScale = new Vector3(size, size, 1f);
                ringRenderer.enabled = true;
            }
            else ringRenderer.enabled = false;

            float half = spotAngle * .5f;
            foreach (var mark in UvMark.All)
            {
                if (mark == null) continue;
                // 원뿔 안이거나 원형 표시(조준점)가 낙서에 닿아 있으면 비춘 것이다. 시야가 막히면 아니다.
                var to = mark.Point - origin; float distance = to.magnitude;
                bool inCone = Vector3.Angle(forward, to) <= half, onRing = Vector3.Distance(aim, mark.Point) <= ringReach;
                if (distance > range || (!inCone && !onRing) || !Clear(origin, mark.Point, mark.transform)) continue;
                mark.Illuminate(); litNow.Add(mark);
                if (mark.Found || ExplorationState.Instance == null) continue;
                if (requested.TryGetValue(mark, out float last) && Time.unscaledTime - last < requestInterval) continue;
                requested[mark] = Time.unscaledTime;
                ExplorationState.Instance.RequestUse(mark.TargetId);
            }
        }

        /// <summary>마지막 프레임의 조준점·시작점·방향(검사·표시용).</summary>
        public Vector3 LastAim { get; private set; }
        public Vector3 LastOrigin { get; private set; }
        public Vector3 LastForward { get; private set; }

        // 카메라 시선이 플레이어 너머에서 처음 닿는 사물. 자기 캐릭터와 카메라·캐릭터 사이에 끼인 벽(3인칭 카메라가 벽 뒤로 넘어간 때)은 건너뛴다.
        Vector3 AimPoint(Vector3 eye, Vector3 look, float toPlayer)
        {
            int count = Physics.RaycastNonAlloc(eye, look, hits, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Vector3 point = eye + look * 12f;
            for (int i = 0; i < count; i++)
                if (hits[i].distance < best && hits[i].distance > toPlayer - .3f && hits[i].collider.GetComponentInParent<PlayerMover>() == null) { best = hits[i].distance; point = hits[i].point; }
            LastAim = point; return point;
        }

        static bool Equipped(NetworkPlayer player, PlayerInputReader reader)
        {
            var carry = PropCarry.Instance;
            if (carry == null) return false;
            ulong id = player != null ? player.OwnerClientId : carry.LocalClientId;
            foreach (var item in carry.Held(id)) if (item.ItemId == ItemUses.UvLamp) return true;
            return false;
        }

        static bool Clear(Vector3 from, Vector3 to, Transform target)
        {
            // 낙서는 벽면에 붙어 있으므로 끝 25cm(벽 타일·마감 두께)는 가림으로 보지 않는다.
            var delta = to - from; int count = Physics.RaycastNonAlloc(from, delta.normalized, hits, Mathf.Max(0f, delta.magnitude - .25f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i].collider.transform;
                if (hit.IsChildOf(target) || hit.GetComponentInParent<PlayerMover>() != null) continue;
                return false;
            }
            return true;
        }

        // 처음 발견한 낙서로 시선을 한 번 돌린다. 이 화면이 비춘 낙서만.
        void OnDiscovered(UvMark mark)
        {
            if (!litNow.Contains(mark)) return;
            var director = CameraViewDirector.Current; var cam = Camera.main;
            if (director == null || cam == null || !director.UsesMouseLook) return;
            director.SetLookRotation(Quaternion.LookRotation(mark.Point - cam.transform.position));
        }

        void BuildRing()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = "UvRing"; go.hideFlags = HideFlags.DontSave;
            Destroy(go.GetComponent<Collider>()); go.transform.SetParent(transform, false); ring = go.transform;
            ringRenderer = go.GetComponent<Renderer>(); ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { name = "UvRing", hideFlags = HideFlags.DontSave };
            material.SetFloat("_Surface", 1f); material.SetFloat("_Blend", 1f); // 투명·가산
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One); material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            material.SetColor("_BaseColor", color); material.SetTexture("_BaseMap", RingTexture());
            ringRenderer.sharedMaterial = material; ringRenderer.enabled = false;
        }

        static Texture2D RingTexture()
        {
            const int n = 96; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "UvRingTex", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float r = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(n * .5f, n * .5f)) / (n * .5f);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(r - .82f) / .09f), fill = r < .8f ? .12f * (1f - r) : 0f;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Max(ring, fill)));
            }
            tex.Apply(); return tex;
        }
    }
}
