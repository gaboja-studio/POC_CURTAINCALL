using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.Tightrope
{
    /// <summary>기존 손잡기 착지 보조 범위를 동료가 서 있는 줄의 청록색 띠로 표시한다. 착지 예측·충돌체는 없다.</summary>
    [DisallowMultipleComponent]
    public sealed class TightropeHandholdRangeView : MonoBehaviour
    {
        const int Segments = 24;
        readonly Dictionary<PlayerMover, Marker> markers = new();
        readonly HashSet<PlayerMover> seen = new();
        readonly List<PlayerMover> removed = new();
        RopeLaneJumpRules rules;
        Material material;
        public int VisibleRangeCount { get; private set; }
        public int ReadyRangeCount { get; private set; }

        sealed class Marker
        {
            public GameObject Root;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public readonly MaterialPropertyBlock Properties = new();
            public readonly Vector3[] Vertices = new Vector3[Segments * 4];
            public readonly List<int> Triangles = new(Segments * 6);
        }

        void Awake()
        {
            // 화면을 렌더링하지 않는 연습 봇에는 시각 전용 셰이더가 없다.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { enabled = false; return; }
            rules = GetComponent<RopeLaneJumpRules>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader)
            {
                name = "Tightrope handhold range (runtime)",
                color = new Color(0.1f, 0.95f, 1f),
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        }

        void LateUpdate()
        {
            VisibleRangeCount = 0;
            ReadyRangeCount = 0;
            var course = TightropeCourse.Current;
            var local = NetworkPlayer.Local;
            if (rules == null || !rules.isActiveAndEnabled || course == null || !course.IsGameplayActive
                || local == null || local.State != PlayerState.Normal)
            {
                Hide();
                return;
            }

            seen.Clear();
            foreach (var other in PlayerMover.All)
            {
                var player = other.GetComponent<NetworkPlayer>();
                var catches = TightropeCatchSystem.Current;
                bool ready = catches != null && catches.IsReady(player);
                bool success = catches != null && catches.SuccessRemaining(player) > 0f;
                int lane = TightropeCourse.NoLane;
                float from = 0f, to = 0f;
                bool activeRange = catches != null && catches.TryGetCatchRange(player, out lane, out from, out to);
                if (!activeRange && !rules.TryGetHandholdRange(other, out lane, out from, out to)) continue;
                seen.Add(other);
                if (!markers.TryGetValue(other, out var marker))
                    markers.Add(other, marker = CreateMarker());
                Color tint = success ? new Color(.15f, 1f, .35f) : ready
                    ? (catches.ReadyRemaining(player) < 1f ? new Color(1f, .65f, .1f) : new Color(.15f, .95f, 1f))
                    : new Color(.1f, .95f, 1f);
                marker.Properties.SetColor("_BaseColor", tint);
                marker.Properties.SetColor("_Color", tint);
                marker.Renderer.SetPropertyBlock(marker.Properties);
                if (Draw(marker, course, lane, from, to))
                { VisibleRangeCount++; if (ready) ReadyRangeCount++; }
            }
            removed.Clear();
            foreach (var pair in markers)
                if (!seen.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var key in removed)
            {
                DestroyMarker(markers[key]);
                markers.Remove(key);
            }
        }

        Marker CreateMarker()
        {
            var marker = new Marker
            {
                Root = new GameObject("HandholdRange"),
                Mesh = new Mesh { name = "Handhold range strip", hideFlags = HideFlags.HideAndDontSave }
            };
            marker.Root.transform.SetParent(transform, false);
            marker.Root.AddComponent<MeshFilter>().sharedMesh = marker.Mesh;
            var renderer = marker.Renderer = marker.Root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            marker.Mesh.MarkDynamic();
            return marker;
        }

        static bool Draw(Marker marker, TightropeCourse course, int lane, float from, float to)
        {
            marker.Triangles.Clear();
            marker.Root.transform.SetPositionAndRotation(course.GetLanePosition(lane, from), Quaternion.LookRotation(course.Forward));
            // 작은 조각별로 검사하여 끊어진 줄·불타는 구간을 보조 범위로 표시하지 않는다.
            for (int i = 0; i < Segments; i++)
            {
                float start = Mathf.Lerp(from, to, (float)i / Segments);
                float end = Mathf.Lerp(from, to, (float)(i + 1) / Segments);
                if (!course.IsLaneUsable(lane, start) || !course.IsLaneUsable(lane, end)
                    || !course.IsLaneChangeAllowed(start) || !course.IsLaneChangeAllowed(end)) continue;
                float halfWidth = course.Shape.RopeWalkWidth * (i == 0 || i == Segments - 1 ? .5f : .3f);
                int vertex = i * 4;
                marker.Vertices[vertex] = new Vector3(-halfWidth, .09f, start - from);
                marker.Vertices[vertex + 1] = new Vector3(halfWidth, .09f, start - from);
                marker.Vertices[vertex + 2] = new Vector3(-halfWidth, .09f, end - from);
                marker.Vertices[vertex + 3] = new Vector3(halfWidth, .09f, end - from);
                marker.Triangles.Add(vertex); marker.Triangles.Add(vertex + 2); marker.Triangles.Add(vertex + 1);
                marker.Triangles.Add(vertex + 1); marker.Triangles.Add(vertex + 2); marker.Triangles.Add(vertex + 3);
            }
            bool visible = marker.Triangles.Count > 0;
            marker.Root.SetActive(visible);
            if (!visible) return false;
            marker.Mesh.vertices = marker.Vertices;
            marker.Mesh.SetTriangles(marker.Triangles, 0);
            marker.Mesh.RecalculateBounds();
            return true;
        }

        void Hide()
        {
            foreach (var marker in markers.Values) marker.Root.SetActive(false);
            VisibleRangeCount = 0;
            ReadyRangeCount = 0;
        }

        void OnDisable() => Hide();

        static void DestroyMarker(Marker marker)
        {
            Destroy(marker.Root);
            Destroy(marker.Mesh);
        }

        void OnDestroy()
        {
            foreach (var marker in markers.Values) DestroyMarker(marker);
            markers.Clear();
            if (material != null) Destroy(material);
        }
    }
}
