using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.HumanCannon
{
    /// <summary>각 화면에서 모든 공중 참가자의 바로 아래 착지면을 표시한다. 이동/네트워크 상태는 변경하지 않는다.</summary>
    [DefaultExecutionOrder(500), DisallowMultipleComponent]
    public sealed class HumanCannonGroundMarkers : MonoBehaviour
    {
        [SerializeField] Material markerMaterial;
        [Tooltip("착지 성공 판정과 별개로 발밑 표시에 사용할 코스 바닥. 이 목록은 게임 규칙을 바꾸지 않는다.")]
        [SerializeField] Collider[] courseFloors = System.Array.Empty<Collider>();
        [Min(0.1f), SerializeField] float radius = 0.65f;
        [Min(1f), SerializeField] float maxDistance = 100f;
        readonly Dictionary<NetworkPlayer, Transform> markers = new();
        readonly List<NetworkPlayer> removed = new();
        RaycastHit[] hits = new RaycastHit[32];
        Mesh quad;

        void LateUpdate()
        {
            var cannon = HumanCannonSystem.Current;
            if (cannon == null || markerMaterial == null) { HideAll(); return; }
            foreach (var player in NetworkPlayer.All)
            {
                if (player == null || !player.IsSpawned) continue;
                bool airborne = player.State == PlayerState.Normal && !cannon.IsSeated(player.OwnerClientId)
                    && (player.IsAirborne || cannon.IsFlying(player.OwnerClientId))
                    && (HumanCannonRouteSystem.Current == null || !HumanCannonRouteSystem.Current.IsRiding(player.OwnerClientId));
                Vector3 point = default, normal = Vector3.up;
                bool visible = airborne && TryProjectGround(player.transform.position, out point, out normal)
                    && player.transform.position.y - point.y > 0.12f;
                if (!markers.TryGetValue(player, out var marker))
                {
                    if (!visible) continue;
                    marker = CreateMarker(player.OwnerClientId);
                    markers.Add(player, marker);
                }
                marker.gameObject.SetActive(visible);
                if (visible)
                {
                    marker.SetPositionAndRotation(point + normal * 0.025f, Quaternion.FromToRotation(Vector3.up, normal));
                    marker.localScale = Vector3.one * radius;
                }
            }
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
        {
            point = default;
            normal = Vector3.up;
            Vector3 origin = feet + Vector3.up * 0.05f;
            int count;
            do
            {
                count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, maxDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count < hits.Length) break;
                System.Array.Resize(ref hits, hits.Length * 2);
            } while (true);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.distance >= nearest || hit.normal.y < 0.7f
                    || hit.collider.GetComponentInParent<NetworkPlayer>() != null) continue;
                bool surface = System.Array.IndexOf(courseFloors, hit.collider) >= 0
                    || HumanCannonLandingZone.ContainsLanding(hit.point);
                if (!surface)
                {
                    var transport = hit.collider.GetComponentInParent<HumanCannonTransport>();
                    surface = transport != null && transport.isActiveAndEnabled && transport.Platform != null
                        && hit.collider.transform.IsChildOf(transport.Platform) && transport.ContainsFeet(hit.point, 0);
                }
                if (!surface) continue;
                nearest = hit.distance;
                point = hit.point;
                normal = hit.normal;
            }
            // 트램폴린은 물리 Collider 없이 별도 반동 면으로 동작한다. 천 아래로 표시가 가려지지 않게 그 면을 쓴다.
            var trampoline = HumanCannonTrampoline.Current;
            if (trampoline != null && trampoline.isActiveAndEnabled)
            {
                var candidate = new Vector3(feet.x, trampoline.Surface.y, feet.z);
                var local = trampoline.transform.InverseTransformPoint(candidate);
                float distance = origin.y - candidate.y;
                bool blocked = HumanCannonRouteSystem.Current != null && HumanCannonRouteSystem.Current.BlocksTrampolineBounce(candidate);
                if (!blocked && distance >= 0 && distance < nearest && distance <= maxDistance
                    && Mathf.Abs(local.x) <= trampoline.HalfSize.x && Mathf.Abs(local.z) <= trampoline.HalfSize.y)
                { nearest = distance; point = candidate; normal = Vector3.up; }
            }
            return !float.IsPositiveInfinity(nearest);
        }

        Transform CreateMarker(ulong actor)
        {
            if (quad == null)
            {
                quad = new Mesh { name = "HumanCannonGroundMarkerQuad" };
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

        void HideAll() { foreach (var marker in markers.Values) if (marker != null) marker.gameObject.SetActive(false); }
        void OnDisable() => HideAll();
        void OnDestroy()
        {
            foreach (var marker in markers.Values) if (marker != null) Destroy(marker.gameObject);
            markers.Clear();
            if (quad != null) Destroy(quad);
        }
    }
}
