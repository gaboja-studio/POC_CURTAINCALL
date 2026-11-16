using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>
    /// 화면 기준점(정중앙)과 상호작용 대상 사이 거리(2026-11-16 상호작용 고도화).
    /// 거리 = 대상 렌더러가 화면에 차지하는 사각 영역에서 기준점까지 가장 가까운 거리 ÷ 화면 높이. 기준점이 영역 안이면 0.
    /// 범위를 거르지 않고 같은 분류 안의 순서만 정한다(2026-11-16 사용자: 고개를 끝까지 숙여도 바닥 기물이 잡히지 않음).
    /// 내 화면 카메라가 있는 로컬 플레이어에만 적용한다. 호스트의 원격 요청 검사는 기존 거리 판정만 쓴다.
    /// </summary>
    public static class InteractionFocus
    {
        static readonly Dictionary<Transform, float> frameCache = new();
        static readonly List<Renderer> renderers = new();
        static readonly Vector3[] corners = new Vector3[8];
        static int cachedFrame = -1;

        /// <summary>기준점 판정을 쓸 카메라. 로컬 조작 대상이 아니면 null(기존 거리 판정만 사용).</summary>
        public static Camera CameraFor(Component actor)
        {
            if (actor == null) return null;
            if (actor.TryGetComponent(out NetworkPlayer player))
            {
                if (NetworkPlayer.All.Count > 0 && !player.IsLocal) return null;
            }
            else if (NetworkPlayer.All.Count > 0) return null;
            var camera = Camera.main;
            return camera != null && camera.isActiveAndEnabled ? camera : null;
        }

        /// <summary>선택 순서용 기준점 거리. 카메라가 없으면(원격·헤드리스) 0이라 기존 거리 순서만 남는다.</summary>
        public static float Distance(Component actor, Transform root, Vector3 point)
        {
            var camera = CameraFor(actor);
            return camera == null ? 0f : ScreenDistance(camera, root, point);
        }

        /// <summary>화면 높이 대비 기준점-대상 거리. 화면 뒤면 무한대.</summary>
        public static float ScreenDistance(Camera camera, Transform root, Vector3 point)
        {
            if (camera == null) return float.PositiveInfinity;
            if (cachedFrame != Time.frameCount) { frameCache.Clear(); cachedFrame = Time.frameCount; }
            if (root != null && frameCache.TryGetValue(root, out float cached)) return cached;
            float result = Measure(camera, root, point);
            if (root != null) frameCache[root] = result;
            return result;
        }

        static float Measure(Camera camera, Transform root, Vector3 point)
        {
            float height = Mathf.Max(1f, camera.pixelHeight);
            Vector2 center = new(camera.pixelRect.x + camera.pixelWidth * .5f, camera.pixelRect.y + camera.pixelHeight * .5f);
            bool found = false;
            Rect rect = default;
            if (root != null && TryBounds(root, out var bounds))
            {
                Vector3 min = bounds.min, max = bounds.max;
                corners[0] = new Vector3(min.x, min.y, min.z); corners[1] = new Vector3(max.x, min.y, min.z);
                corners[2] = new Vector3(min.x, max.y, min.z); corners[3] = new Vector3(max.x, max.y, min.z);
                corners[4] = new Vector3(min.x, min.y, max.z); corners[5] = new Vector3(max.x, min.y, max.z);
                corners[6] = new Vector3(min.x, max.y, max.z); corners[7] = new Vector3(max.x, max.y, max.z);
                bool anyBehind = false;
                foreach (var corner in corners)
                {
                    Vector3 screen = camera.WorldToScreenPoint(corner);
                    if (screen.z <= camera.nearClipPlane) { anyBehind = true; continue; }
                    rect = found ? Rect.MinMaxRect(Mathf.Min(rect.xMin, screen.x), Mathf.Min(rect.yMin, screen.y),
                        Mathf.Max(rect.xMax, screen.x), Mathf.Max(rect.yMax, screen.y)) : new Rect(screen.x, screen.y, 0f, 0f);
                    found = true;
                }
                // 카메라가 대상 영역 안(벽에 붙음 등)이거나 일부 모서리가 뒤에 있으면, 시선이 상자에 닿는지로 본다.
                if (anyBehind)
                {
                    var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f, 0f));
                    if (bounds.Contains(ray.origin) || bounds.IntersectRay(ray)) return 0f;
                }
            }
            if (!found)
            {
                Vector3 screen = camera.WorldToScreenPoint(point);
                if (screen.z <= camera.nearClipPlane) return float.PositiveInfinity;
                rect = new Rect(screen.x, screen.y, 0f, 0f);
            }
            float dx = Mathf.Max(0f, Mathf.Max(rect.xMin - center.x, center.x - rect.xMax));
            float dy = Mathf.Max(0f, Mathf.Max(rect.yMin - center.y, center.y - rect.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy) / height;
        }

        /// <summary>보이는 메시 렌더러 합집합(이름이 "__"로 시작하는 보조 메시는 제외).</summary>
        public static bool TryBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            renderers.Clear();
            root.GetComponentsInChildren(false, renderers);
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || renderer is not (MeshRenderer or SkinnedMeshRenderer)
                    || renderer.name.StartsWith("__") || renderer.forceRenderingOff) continue;
                if (any) bounds.Encapsulate(renderer.bounds); else { bounds = renderer.bounds; any = true; }
            }
            renderers.Clear();
            return any;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { frameCache.Clear(); cachedFrame = -1; }
    }
}
