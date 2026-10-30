using System.Collections.Generic;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 서커스 UV 등 손전등(2026-10-30 확정 규칙). 씬에 하나.
    /// 로컬 플레이어의 슬롯에 UV 등이 있고 우클릭을 누르는 동안, 가슴 높이에서 시선 방향(캐릭터 정면 쪽만)으로 보라색 원뿔 빛이 나간다. 비추면서 이동할 수 있다. 원뿔 안에 들어온 <see cref="UvMark"/>는 형광으로 보이고,
    /// 처음 비춘 낙서는 호스트에 발견을 요청한다. 발견되면 카메라가 낙서 쪽으로 한 번 돈다(마우스 시선 시점에서만).
    /// 빛은 <see cref="NetworkPlayer.SetUvLight"/>로 공유되어 다른 화면에도 같은 빛이 보이고, 그 빛에 닿은 낙서도 드러난다(발견 요청은 비춘 본인 화면만).
    /// </summary>
    public sealed class UvFlashlight : MonoBehaviour
    {
        [SerializeField] Color color = new(.6f, .25f, 1f);
        [SerializeField, Min(1f)] float range = 12f;
        [SerializeField, Range(10f, 120f)] float spotAngle = 50f;
        [SerializeField, Min(0f)] float intensity = 60f;
        [Tooltip("캐릭터 정면에서 좌우로 이 각도까지만 비춘다(도). 뒤는 못 비춘다.")]
        [SerializeField, Range(10f, 90f)] float frontLimit = 75f;
        [Tooltip("조준점에서 이 거리(m) 안의 낙서는 원뿔 각도와 관계없이 비춘 것으로 본다.")]
        [SerializeField, Min(.1f)] float ringReach = 1f;
        [Tooltip("같은 낙서에 발견 요청을 다시 보내기까지의 간격(초).")]
        [SerializeField, Min(.1f)] float requestInterval = .6f;

        Light spot;
        readonly Dictionary<UvMark, float> requested = new();
        readonly HashSet<UvMark> litNow = new();
        readonly Dictionary<NetworkPlayer, Light> remote = new();
        static readonly RaycastHit[] hits = new RaycastHit[8];

        public bool IsOn { get; private set; }

        void Awake()
        {
            var go = new GameObject("UvSpot") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            spot = go.AddComponent<Light>();
            spot.type = LightType.Spot; spot.color = color; spot.range = range; spot.spotAngle = spotAngle; spot.innerSpotAngle = spotAngle * .55f;
            spot.intensity = intensity; spot.shadows = LightShadows.None; spot.enabled = false;
        }

        void OnEnable() => UvMark.Discovered += OnDiscovered;
        void OnDisable()
        {
            UvMark.Discovered -= OnDiscovered; if (spot != null) spot.enabled = false;
            foreach (var light in remote.Values) if (light != null) Destroy(light.gameObject);
            remote.Clear();
        }

        void Update()
        {
            var player = NetworkPlayer.Local;
            var reader = player != null ? player.GetComponent<PlayerInputReader>() : FindFirstObjectByType<PlayerInputReader>();
            var body = reader != null ? reader.transform : null;
            bool on = body != null && reader.isActiveAndEnabled && reader.Current.UvLightHeld && !InputGate.IsBlocked && Equipped(player, reader);
            IsOn = on;
            litNow.Clear();
            UpdateRemote();
            if (!on) { spot.enabled = false; if (player != null) player.SetUvLight(false, default, default); return; }

            // 마우스가 가리키는 자리: 카메라 시선이 닿는 점(없으면 12m 앞). 가슴에서 그 점을 향해 비춘다(3인칭에서 카메라 기울기를 그대로 쓰면 바닥을 비춘다).
            // 마우스 시선 시점(1인칭·3인칭 비고정)에서는 카메라가 가리키는 자리를, 고정 3인칭에서는 캐릭터가 보는 방향을 비춘다.
            var cam = Camera.main; var director = CameraViewDirector.Current;
            bool mouseLook = director != null && director.UsesMouseLook && cam != null;
            var chest = body.position + Vector3.up * 1.4f;
            var look = mouseLook ? cam.transform.forward : Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
            var eye = mouseLook ? cam.transform.position : chest;
            var aim = AimPoint(eye, look, mouseLook ? Vector3.Distance(eye, chest) : 0f);
            var forward = (aim - chest).sqrMagnitude > .01f ? (aim - chest).normalized : Vector3.ProjectOnPlane(look, Vector3.up).normalized;
            // 캐릭터 정면 쪽으로만 비춘다. 뒤를 비추려면 캐릭터를 돌려야 한다.
            var facing = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
            if (facing.sqrMagnitude > .01f && Vector3.Angle(facing, Vector3.ProjectOnPlane(forward, Vector3.up)) > frontLimit)
            {
                var flat = Vector3.RotateTowards(facing, Vector3.ProjectOnPlane(forward, Vector3.up).normalized, frontLimit * Mathf.Deg2Rad, 0f);
                forward = (flat * Mathf.Cos(Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f))) + Vector3.up * forward.y).normalized;
            }
            var origin = chest + Vector3.ProjectOnPlane(forward, Vector3.up).normalized * .35f;
            LastOrigin = origin; LastForward = forward;
            spot.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(forward));
            spot.enabled = true;
            if (player != null) player.SetUvLight(true, origin, forward);

            float half = spotAngle * .5f;
            foreach (var mark in UvMark.All)
            {
                if (mark == null) continue;
                // 원뿔 안이거나 원형 표시(조준점)가 낙서에 닿아 있으면 비춘 것이다. 시야가 막히면 아니다.
                var to = mark.Center - origin; float distance = to.magnitude;
                bool inCone = Vector3.Angle(forward, to) <= half, onRing = Vector3.Distance(aim, mark.Center) <= ringReach;
                if (distance > range || (!inCone && !onRing) || !Clear(origin, mark.Center, mark)) continue;
                mark.Illuminate(); litNow.Add(mark);
                if (mark.Found || ExplorationState.Instance == null) continue;
                if (requested.TryGetValue(mark, out float last) && Time.unscaledTime - last < requestInterval) continue;
                requested[mark] = Time.unscaledTime;
                ExplorationState.Instance.RequestUse(mark.TargetId);
            }
        }

        // 다른 플레이어의 UV 빛: 공유된 시작점·방향으로 같은 빛을 그리고 닿은 낙서를 드러낸다. 발견 요청은 하지 않는다.
        void UpdateRemote()
        {
            foreach (var p in NetworkPlayer.All)
            {
                if (p == null || p.IsLocal) continue;
                bool on = p.UvOn;
                if (!remote.TryGetValue(p, out var light) || light == null)
                {
                    if (!on) continue;
                    light = Instantiate(spot, transform); light.name = "UvSpotRemote"; remote[p] = light;
                }
                light.enabled = on;
                if (!on) continue;
                var origin = p.UvOrigin; var forward = p.UvForward.sqrMagnitude > .01f ? p.UvForward.normalized : Vector3.forward;
                light.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(forward));
                float half = spotAngle * .5f;
                foreach (var mark in UvMark.All)
                {
                    if (mark == null) continue;
                    var to = mark.Center - origin;
                    if (to.magnitude <= range && Vector3.Angle(forward, to) <= half && Clear(origin, mark.Center, mark)) mark.Illuminate();
                }
            }
            // 나간 플레이어의 빛은 지운다.
            List<NetworkPlayer> gone = null;
            foreach (var pair in remote) if (pair.Key == null || !pair.Key.IsSpawned) (gone ??= new()).Add(pair.Key);
            if (gone != null) foreach (var p in gone) { if (remote[p] != null) Destroy(remote[p].gameObject); remote.Remove(p); }
        }

        /// <summary>다른 플레이어 UV 빛이 이 화면에 켜져 있는 개수(검사용).</summary>
        public int RemoteLitCount { get { int n = 0; foreach (var l in remote.Values) if (l != null && l.enabled) n++; return n; } }

        /// <summary>마지막으로 빛을 가린 물체 이름(검사용).</summary>
        public static string LastBlocker { get; private set; } = "";

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

        static bool Clear(Vector3 from, Vector3 to, UvMark target)
        {
            // 낙서는 벽면에 붙어 있으므로 끝 25cm(벽 타일·마감 두께)는 가림으로 보지 않는다.
            var delta = to - from; int count = Physics.RaycastNonAlloc(from, delta.normalized, hits, Mathf.Max(0f, delta.magnitude - .25f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i].collider.transform;
                if (target.Owns(hit) || hit.GetComponentInParent<PlayerMover>() != null) continue;
                LastBlocker = hit.name;
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
            director.SetLookRotation(Quaternion.LookRotation(mark.Center - cam.transform.position));
        }

    }
}
