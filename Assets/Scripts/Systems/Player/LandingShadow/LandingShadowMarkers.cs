using System.Collections.Generic;
using CurtainCall.Cameras;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using CurtainCall.SpaceLink;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using LandingMarkerSpace = CurtainCall.Settings.BaseGameSettings.LandingMarkerSpace;

namespace CurtainCall.Player
{
    /// <summary>
    /// 공통 착지 그림자(Task-20261113-126). 각 화면에서 모든 공중 플레이어의 바로 아래 착지면에 원형 그림자를 표시한다. 이동/네트워크 상태는 변경하지 않는다.
    /// 높이 차이가 공통 설정(<see cref="BaseGameSettings.LandingMarkerSettings"/>)의 선명 높이를 넘으면 뿌옇게 번져 사라진다.
    /// 선명 높이 이하에서는 내 캐릭터(관전 중이면 관전 대상)의 그림자에만 옷 색 외곽선을 그리고, 안전 체크 높이 이하에서는 안전/비안전 색으로 바꾼다.
    /// 그 플레이어가 트램펄린을 잡고 있으면 설정에 따라 모든 플레이어의 그림자에 옷 색 외곽선을 그린다.
    /// 착지면이 박스(외줄·발판 등)면 좁은 면은 원을 면 모양으로 잘라 그 경계에 얇아진 외곽선을 그리고,
    /// 넓은 면은 설정에 따라 원 모양을 유지한 채 면 밖 부분만 옅게 그린다.
    /// 표시 여부는 공간(공연·탈출로·대기실·로비·기타)별 설정을 따른다.
    /// 씬에 배치한 전용 표시(예: <see cref="HumanCannonGroundMarkers"/>)가 동작 중이면 그쪽이 그리고, 없으면 자동 생성되는 공통 표시가 그린다.
    /// </summary>
    [DefaultExecutionOrder(500), DisallowMultipleComponent]
    public class LandingShadowMarkers : MonoBehaviour
    {
        const string FallbackMaterialPath = "Materials/LandingShadow/LandingShadow";
        const float MinShownHeight = 0.12f;

        [Tooltip("착지 그림자 머티리얼(CurtainCall/HumanCannon/GroundMarker 셰이더). 그림자 바탕색·투명도는 이 머티리얼의 '그림자 바탕색·투명도'로 조정한다. 높이별 번짐·외곽선은 공통 설정(Base Game Settings > 착지 표시)에서 조정한다.")]
        [SerializeField] protected Material markerMaterial;
        [Tooltip("선명할 때 착지 그림자의 반지름(m). 번질 때는 공통 설정의 번짐 확산 배율만큼 커진다. 외곽선 두께(m)는 이 반지름 안쪽으로 그린다.")]
        [Min(0.1f), SerializeField] protected float radius = 0.65f;
        [Tooltip("발 아래로 착지면을 찾는 최대 거리(m). 이보다 아래에 있는 바닥에는 그림자를 표시하지 않는다.")]
        [Min(1f), SerializeField] protected float maxDistance = 100f;

        protected readonly Dictionary<NetworkPlayer, Transform> markers = new();
        protected RaycastHit[] hits = new RaycastHit[32];
        protected Mesh quad;
        protected MaterialPropertyBlock tint;
        readonly List<NetworkPlayer> removed = new();

        static readonly int RimColor = Shader.PropertyToID("_RimColor");
        static readonly int FillColor = Shader.PropertyToID("_FillColor");
        static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        static readonly int Blur = Shader.PropertyToID("_Blur");
        static readonly int ClipMode = Shader.PropertyToID("_ClipMode");
        static readonly int ClipOutsideAlpha = Shader.PropertyToID("_ClipOutsideAlpha");
        static readonly int ClipSize = Shader.PropertyToID("_ClipSize");
        static readonly int ClipMatrix = Shader.PropertyToID("_ClipMatrix");

        static readonly List<LandingShadowMarkers> authored = new();
        static LandingShadowMarkers fallback;
        bool isFallback;

        /// <summary>씬에 전용 표시가 없을 때 그릴 공통 표시를 한 번 만든다. 머티리얼은 Resources에서 읽는다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateFallback()
        {
            if (fallback != null) return;
            var material = Resources.Load<Material>(FallbackMaterialPath);
            if (material == null)
            {
                Debug.LogWarning($"[LandingShadow] Resources/{FallbackMaterialPath} 머티리얼이 없어 공통 착지 그림자를 끕니다.");
                return;
            }
            var host = new GameObject("LandingShadowMarkers (Common)");
            host.SetActive(false); // OnEnable 전에 공통 표시임을 표시한다
            DontDestroyOnLoad(host);
            fallback = host.AddComponent<LandingShadowMarkers>();
            fallback.isFallback = true;
            fallback.markerMaterial = material;
            host.SetActive(true);
        }

        /// <summary>외곽선을 그릴 플레이어. 관전 중이면 관전 대상(없으면 아무도 없음), 아니면 내 캐릭터.</summary>
        protected static NetworkPlayer FocusPlayer => CameraSpectator.IsSpectating ? CameraSpectator.Target : NetworkPlayer.Local;

        /// <summary>이 표시가 지금 그릴 수 있는지. 그림자·번짐은 공간과 무관하게 그린다.</summary>
        protected virtual bool Ready => markerMaterial != null;

        /// <summary>지금 공간에서 외곽선(옷 색·안전/비안전)을 그리는지. 꺼진 공간은 그림자·번짐만 보인다.</summary>
        protected bool OutlineShown => GameSettings.Base.LandingMarker.ShowIn(
            CurrentSpace(isFallback ? SceneManager.GetActiveScene() : gameObject.scene));

        /// <summary>그림자를 표시할 공중 상태인지.</summary>
        protected virtual bool IsAirborne(NetworkPlayer player) => player.State == PlayerState.Normal && player.IsAirborne;

        /// <summary>그림자를 올릴 수 있는 면인지. 위를 향한 면(외줄 윗면 포함)만 쓰고 플레이어 몸은 제외한다.</summary>
        protected virtual bool AcceptsSurface(RaycastHit hit) => hit.normal.y >= 0.7f
            && hit.collider.GetComponentInParent<NetworkPlayer>() == null;

        /// <summary>트램펄린 반동 면을 착지면으로 쓰지 못하게 막는지(전용 표시가 코스 규칙으로 덮어쓴다).</summary>
        protected virtual bool TrampolineBlocked(Vector3 candidate) => false;

        /// <summary>안전 체크 구간에서 내 캐릭터(관전 대상) 추가 안내를 띄운다. 띄웠으면 true.</summary>
        protected virtual bool ShowFocusTarget(bool safe, Collider surface, Vector3 point) => false;
        protected virtual void HideFocusTarget() { }

        protected virtual void OnEnable()
        {
            if (!isFallback && !authored.Contains(this)) authored.Add(this);
        }

        protected virtual void OnDisable()
        {
            authored.Remove(this);
            HideAll();
        }

        static bool AuthoredDrawing()
        {
            foreach (var item in authored)
                if (item != null && item.isActiveAndEnabled && item.Ready) return true;
            return false;
        }

        protected void LateUpdate()
        {
            if (!Ready || isFallback && AuthoredDrawing()) { HideAll(); return; }
            var settings = GameSettings.Base.LandingMarker;
            bool outlined = OutlineShown;
            var focus = outlined ? FocusPlayer : null;
            bool outlineAll = outlined && settings.OutlineAllForTrampolineCarrier && HumanCannonTrampoline.IsCarriedBy(focus);
            bool targetShown = false;
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned) continue;
                var feet = player.transform.position;
                Vector3 point = default, normal = Vector3.up;
                Collider surface = null;
                bool trampoline = false;
                bool visible = IsAirborne(player) && TryProjectGround(feet, out point, out normal, out surface, out trampoline)
                    && feet.y - point.y > MinShownHeight;
                float height = feet.y - point.y;
                float blur = visible ? settings.BlurAt(height) : 1;
                visible &= blur < 1;
                if (!markers.TryGetValue(player, out var marker))
                {
                    if (!visible) continue;
                    marker = CreateMarker(player.OwnerClientId);
                    markers.Add(player, marker);
                }
                marker.gameObject.SetActive(visible);
                if (!visible) continue;
                marker.SetPositionAndRotation(point + normal * 0.025f, Quaternion.FromToRotation(Vector3.up, normal));
                marker.localScale = Vector3.one * (radius * Mathf.Lerp(1, settings.BlurSpread, blur));
                // 안전 판정은 외곽선을 그리는 공간에서 내 캐릭터(관전 대상)만, 안전 체크 높이 안에서만 한다.
                var band = settings.BandAt(height);
                bool check = focus != null && player == focus && band == BaseGameSettings.LandingOutlineBand.SafetyCheck;
                bool safe = check && IsSafeLanding(player, feet, point, surface, trampoline);
                Color outline = Color.clear;
                if (check) outline = safe ? settings.SafeColor : settings.UnsafeColor;
                else if (outlined && band != BaseGameSettings.LandingOutlineBand.None && (outlineAll || player == focus))
                    outline = settings.OutlineColor(player.OutfitColor);
                Paint(marker, outline, settings.OutlineWidth / radius, blur, clipSurface: surface, clipNormal: normal);
                if (check) targetShown |= ShowFocusTarget(safe, surface, point);
            }
            if (!targetShown) HideFocusTarget();
            removed.Clear();
            foreach (var pair in markers)
                if (pair.Key == null || !pair.Key.IsSpawned) removed.Add(pair.Key);
            foreach (var player in removed)
            {
                if (markers[player] != null) Destroy(markers[player].gameObject);
                markers.Remove(player);
            }
        }

        /// <summary>현재 발 위치를 수직 투영한다. 구멍이나 장외에는 가상의 바닥을 표시하지 않는다.</summary>
        public bool TryProjectGround(Vector3 feet, out Vector3 point, out Vector3 normal)
            => TryProjectGround(feet, out point, out normal, out _);

        protected bool TryProjectGround(Vector3 feet, out Vector3 point, out Vector3 normal, out Collider surface)
            => TryProjectGround(feet, out point, out normal, out surface, out _);

        /// <param name="trampoline">착지 지점이 트램펄린 반동 면이면 true(이때 <paramref name="surface"/>는 null).</param>
        protected bool TryProjectGround(Vector3 feet, out Vector3 point, out Vector3 normal, out Collider surface, out bool trampoline)
        {
            point = default;
            normal = Vector3.up;
            surface = null;
            trampoline = false;
            Vector3 origin = feet + Vector3.up * 0.05f;
            int count = RaycastDown(origin, maxDistance);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.distance >= nearest || !AcceptsSurface(hit)) continue;
                nearest = hit.distance;
                point = hit.point;
                normal = hit.normal;
                surface = hit.collider;
            }
            // 트램펄린은 물리 Collider 없이 별도 반동 면으로 동작한다. 천 아래로 표시가 가려지지 않게 그 면을 쓴다.
            var pad = HumanCannonTrampoline.Current;
            if (pad != null && pad.isActiveAndEnabled)
            {
                var candidate = new Vector3(feet.x, pad.Surface.y, feet.z);
                var local = pad.transform.InverseTransformPoint(candidate);
                float distance = origin.y - candidate.y;
                if (!TrampolineBlocked(candidate) && distance >= 0 && distance < nearest && distance <= maxDistance
                    && Mathf.Abs(local.x) <= pad.HalfSize.x && Mathf.Abs(local.z) <= pad.HalfSize.y)
                { nearest = distance; point = candidate; normal = Vector3.up; surface = null; trampoline = true; }
            }
            return !float.IsPositiveInfinity(nearest);
        }

        /// <summary>
        /// 이 착지가 안전한지(외곽선 안전 색). 반동하는 트램펄린 위이거나, 발밑 면이 실제 지지면이고 가로막히지 않았으며
        /// 지금 낙하 높이로 내려서도 부상이 없을 때(<see cref="NetworkPlayer.PredictsSafeLanding"/>) 안전이다.
        /// </summary>
        protected bool IsSafeLanding(NetworkPlayer player, Vector3 feet, Vector3 point, Collider surface, bool trampoline)
        {
            if (trampoline)
                return HumanCannonTrampoline.Current != null && HumanCannonTrampoline.Current.BounceReady;
            return player.TryGetComponent(out PlayerMover mover) && HasSafeLandingBelow(mover, feet, point, surface)
                && player.PredictsSafeLanding(point);
        }

        /// <summary>발밑 면이 실제 지지면이고, 발과 그 면 사이를 다른 구조물이 가로막지 않는지(낙하 높이는 보지 않음).</summary>
        protected bool HasSafeLandingBelow(PlayerMover mover, Vector3 feet, Vector3 point, Collider surface)
        {
            if (mover == null || surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy
                || !WalkableSupport.IsSafeLanding(mover, point)) return false;
            var origin = feet + Vector3.up * .05f;
            float distance = origin.y - point.y;
            int count = RaycastDown(origin, distance);
            for (int i = 0; i < count; i++)
                if (hits[i].collider != surface && hits[i].distance < distance - .03f
                    && hits[i].collider.GetComponentInParent<NetworkPlayer>() == null) return false;
            return true;
        }

        int RaycastDown(Vector3 origin, float distance)
        {
            int count;
            do
            {
                count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count < hits.Length) break;
                System.Array.Resize(ref hits, hits.Length * 2);
            } while (true);
            return count;
        }

        /// <summary>
        /// 공유 머티리얼은 그대로 두고 표시별 외곽선 색(알파 = 투명도)·폭(반지름 1 기준)·번짐·바탕색을 PropertyBlock으로 바꾼다.
        /// <paramref name="clipSurface"/>가 박스면 공통 설정에 따라 그 면 모양으로 자르거나(좁은 면, 외곽선 자동 얇게) 면 밖만 옅게 그린다(넓은 면).
        /// </summary>
        protected void Paint(Transform marker, Color outline, float outlineWidth, float blur, Color? fill = null,
            Collider clipSurface = null, Vector3 clipNormal = default)
        {
            tint ??= new MaterialPropertyBlock();
            var renderer = marker.GetComponent<MeshRenderer>();
            renderer.GetPropertyBlock(tint);
            tint.SetColor(RimColor, outline);
            tint.SetFloat(OutlineWidth, Mathf.Clamp(outlineWidth, 0, .96f));
            tint.SetFloat(Blur, Mathf.Clamp01(blur));
            if (fill.HasValue) tint.SetColor(FillColor, fill.Value);
            var shape = BaseGameSettings.LandingSurfaceShape.Circle;
            if (TryGetSurfaceClip(clipSurface, clipNormal, out var toSurface, out var half))
            {
                var settings = GameSettings.Base.LandingMarker;
                float shownRadius = Mathf.Max(1e-4f, marker.lossyScale.x);
                shape = settings.ShapeFor(half);
                // 좁은 면은 외곽선을 면 폭에 맞춰 얇게 해 띠 가운데에 그림자 바탕을 남긴다.
                if (shape == BaseGameSettings.LandingSurfaceShape.Clip)
                    tint.SetFloat(OutlineWidth, Mathf.Clamp(Mathf.Min(outlineWidth, settings.ClippedOutlineWidth(half) / shownRadius), 0, .96f));
                tint.SetMatrix(ClipMatrix, toSurface);
                tint.SetVector(ClipSize, new Vector4(half.x, half.y, shownRadius, 0));
                tint.SetFloat(ClipOutsideAlpha, settings.OutsideOpacity);
            }
            tint.SetFloat(ClipMode, (float)shape);
            renderer.SetPropertyBlock(tint);
        }

        /// <summary>
        /// 착지면이 박스면 월드 좌표를 그 면의 정규 좌표(x·z가 -1~1이면 면 안)로 바꾸는 행렬과 면의 반폭(m)을 구한다.
        /// 맞닿은 면은 법선과 가장 가까운 박스 축으로 고르며, 나머지 두 축이 x·z가 된다. 박스가 아니면 false(자르지 않음).
        /// </summary>
        public static bool TryGetSurfaceClip(Collider surface, Vector3 normal, out Matrix4x4 toSurface, out Vector2 halfMeters)
        {
            toSurface = Matrix4x4.identity;
            halfMeters = Vector2.zero;
            if (surface is not BoxCollider box || normal == Vector3.zero) return false;
            var t = box.transform;
            var half = box.size * .5f;
            if (half.x <= 0 || half.y <= 0 || half.z <= 0) return false;
            var localNormal = t.InverseTransformDirection(normal);
            int up = Mathf.Abs(localNormal.x) >= Mathf.Abs(localNormal.y)
                ? (Mathf.Abs(localNormal.x) >= Mathf.Abs(localNormal.z) ? 0 : 2)
                : (Mathf.Abs(localNormal.y) >= Mathf.Abs(localNormal.z) ? 1 : 2);
            int a = (up + 1) % 3, b = (up + 2) % 3;
            var normalize = Matrix4x4.Scale(new Vector3(1 / half.x, 1 / half.y, 1 / half.z))
                * Matrix4x4.Translate(-box.center) * t.worldToLocalMatrix;
            var permute = Matrix4x4.zero;
            permute[0, a] = 1; permute[1, up] = 1; permute[2, b] = 1; permute[3, 3] = 1;
            toSurface = permute * normalize;
            var scale = t.lossyScale;
            halfMeters = new Vector2(half[a] * Mathf.Abs(scale[a]), half[b] * Mathf.Abs(scale[b]));
            return true;
        }

        protected Transform CreateMarker(ulong actor)
        {
            if (quad == null)
            {
                quad = new Mesh { name = "LandingShadowQuad" };
                quad.vertices = new[] { new Vector3(-1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, -1) };
                quad.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                quad.RecalculateBounds();
            }
            var marker = new GameObject("GroundMarker_" + actor);
            marker.transform.SetParent(transform, false);
            marker.AddComponent<MeshFilter>().sharedMesh = quad;
            var renderer = marker.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = markerMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return marker.transform;
        }

        protected virtual void HideAll()
        {
            foreach (var marker in markers.Values) if (marker != null) marker.gameObject.SetActive(false);
            HideFocusTarget();
        }

        protected virtual void OnDestroy()
        {
            foreach (var marker in markers.Values) if (marker != null) Destroy(marker.gameObject);
            markers.Clear();
            if (quad != null) Destroy(quad);
        }

        /// <summary>
        /// 씬이 속한 공간. 공통 설정 "장소 전환"의 씬 이름을 먼저 보고, 등록되지 않은 씬은 이름·경로로 추정한다.
        /// 대기실 씬 안의 연습실도 대기실로 본다.
        /// </summary>
        public static LandingMarkerSpace SpaceOf(Scene scene) => SpaceOf(scene.name, scene.path);

        /// <summary>지금의 공간. 대기실 씬에서 외줄·인간대포 리허설이 진행 중이면 리허설로 본다.</summary>
        public static LandingMarkerSpace CurrentSpace(Scene scene) => RehearsalAware(SpaceOf(scene),
            CurtainCall.WaitingRoom.WaitingRoomRehearsal.IsActive || CurtainCall.WaitingRoom.WaitingRoomCannonRehearsal.IsActive);

        /// <summary>대기실 공간을 리허설 진행 여부에 따라 리허설로 바꾼다.</summary>
        public static LandingMarkerSpace RehearsalAware(LandingMarkerSpace space, bool rehearsalActive) =>
            space == LandingMarkerSpace.WaitingRoom && rehearsalActive ? LandingMarkerSpace.Rehearsal : space;

        /// <inheritdoc cref="SpaceOf(Scene)"/>
        public static LandingMarkerSpace SpaceOf(string sceneName, string scenePath)
        {
            string name = sceneName ?? string.Empty;
            string path = (scenePath ?? string.Empty).Replace('\\', '/');
            if (SpaceRoute.TryFind(name, GameSettings.Base.SpaceTransition, out var id))
                return id switch
                {
                    SpaceId.Performance => LandingMarkerSpace.Performance,
                    SpaceId.EscapeRoute => LandingMarkerSpace.EscapeRoute,
                    SpaceId.WaitingRoom or SpaceId.Exploration => LandingMarkerSpace.WaitingRoom,
                    _ => LandingMarkerSpace.Lobby,
                };
            if (name == "Start" || name == "Lobby") return LandingMarkerSpace.Lobby;
            if (name.StartsWith("WaitingRoom") || path.Contains("/WaitingRoom/")) return LandingMarkerSpace.WaitingRoom;
            if (name.ToLowerInvariant().Contains("escape") || path.Contains("/EscapeRoute/")) return LandingMarkerSpace.EscapeRoute;
            if (path.Contains("/MainStage/")) return LandingMarkerSpace.Performance;
            return LandingMarkerSpace.Other;
        }
    }
}
