using System.Collections;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 바닥 길안내(기획서 5장). 내 발밑에서 목적지까지 흰 눈금선을 그리고 끝에만 화살표를 둔다. 대기실 씬은 NavMesh를 굽지 않으므로
    /// 처음 목적지를 받을 때 씬 충돌체로 임시 NavMesh를 한 번 굽는다(비동기). 완전한 경로가 없으면 안내를 숨긴다.
    /// 경로는 자주 다시 계산하되 시작점은 매 프레임 내 위치를 따라가 끊기지 않게 보인다(사용자 2026-11-03).
    /// </summary>
    public sealed class RehearsalPathGuide : MonoBehaviour
    {
        const float RefreshSeconds = .1f, LineHeight = .12f, TickSpacing = 1f, LineWidth = .22f;
        /// <summary>선이 발밑에서 떨어져 시작하는 거리(m).</summary>
        const float StartGap = .7f;
        /// <summary>발밑 바로 앞의 모서리는 버린다. 지나치는 순간 선이 튀는 것을 막는다.</summary>
        const float NearCornerSkip = .9f;
        static readonly Bounds WaitingRoomBounds = new(new Vector3(-10f, 8f, 80f), new Vector3(160f, 60f, 160f));
        static readonly Color LineColor = new(1f, 1f, 1f, .95f);

        Vector3? target;
        LineRenderer line;
        GameObject arrow;
        Mesh arrowMesh;
        Material arrowMaterial;
        NavMeshData navData;
        NavMeshDataInstance navInstance;
        bool navReady, navBuilding;
        float nextRefresh;
        NavMeshPath path; // Unity: 필드 초기화에서 만들 수 없다(Awake에서).
        readonly List<Vector3> corners = new();
        readonly List<Vector3> points = new();
        bool routeValid;
        /// <summary>경로 계산이 잠깐 실패해도(벽 옆·점프·소품 위) 마지막 경로를 유지하는 시간(초). 4인 깜빡임 대응(2026-11-04).</summary>
        const float FailureGraceSeconds = 1f;
        float lastRouteAt = -999f;
        bool failureLogged;

        void Awake() => path = new NavMeshPath();

        public void SetTarget(Vector3? destination)
        {
            if (target == destination) return;
            target = destination;
            // 목적지가 바뀌면 이전 경로를 유예 대상으로 쓰지 않는다.
            corners.Clear(); routeValid = false; lastRouteAt = -999f; failureLogged = false; nextRefresh = 0f;
            if (target.HasValue && !navReady && !navBuilding) StartCoroutine(BuildNavMesh());
            if (!target.HasValue) Hide();
        }

        void Update()
        {
            if (!target.HasValue) return;
            var local = NetworkPlayer.Local;
            if (local == null) { Hide(); return; }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + RefreshSeconds;
                Recalculate(local.transform.position);
            }
            if (!routeValid) { Hide(); return; }
            Draw(local.transform.position);
        }

        /// <summary>NavMesh 경로를 다시 계산한다. 시작점은 그리는 쪽이 매 프레임 내 위치로 둔다.</summary>
        void Recalculate(Vector3 feet)
        {
            bool fromOk = false, toOk = false, pathOk = false;
            NavMeshHit origin = default, goal = default;
            bool found = navReady && (fromOk = NavMesh.SamplePosition(feet, out origin, 2f, NavMesh.AllAreas))
                && (toOk = NavMesh.SamplePosition(target.Value, out goal, 3f, NavMesh.AllAreas))
                && (pathOk = NavMesh.CalculatePath(origin.position, goal.position, NavMesh.AllAreas, path))
                && path.status == NavMeshPathStatus.PathComplete && path.corners.Length > 1;
            if (!found)
            {
                // 벽·철창을 관통하는 직선 안내는 표시하지 않는다. 직전에 찾은 경로는 잠깐 유지해 깜빡이지 않게 한다.
                if (navReady && !failureLogged)
                {
                    failureLogged = true;
                    Debug.Log($"[PathGuide] 경로 계산 실패(직전 경로 {FailureGraceSeconds}초 유지): 발밑 샘플={fromOk}, 목적지 샘플={toOk}, 계산={pathOk}, 상태={(pathOk ? path.status.ToString() : "-")}, 위치={feet}");
                }
                routeValid = corners.Count > 0 && Time.unscaledTime - lastRouteAt <= FailureGraceSeconds;
                return;
            }
            failureLogged = false;
            routeValid = true; lastRouteAt = Time.unscaledTime;
            corners.Clear();
            for (int i = 1; i < path.corners.Length; i++) corners.Add(path.corners[i]);
        }

        void Draw(Vector3 feet)
        {
            if (line == null) Build();
            points.Clear();
            var from = feet + Vector3.up * LineHeight;
            points.Add(from);
            for (int i = 0; i < corners.Count; i++)
            {
                var corner = corners[i] + Vector3.up * LineHeight;
                // 마지막 모서리(목적지)는 늘 남긴다.
                if (i < corners.Count - 1 && Vector3.ProjectOnPlane(corner - from, Vector3.up).magnitude < NearCornerSkip) continue;
                points.Add(corner);
            }
            if (points.Count < 2) { Hide(); return; }
            // 발밑에는 아무것도 그리지 않는다. 첫 구간을 조금 앞에서 시작한다.
            var first = points[1] - points[0];
            float firstLength = first.magnitude;
            if (firstLength <= StartGap + .2f) points.RemoveAt(0);
            else points[0] += first / firstLength * StartGap;
            if (points.Count < 2) { Hide(); return; }
            line.enabled = true;
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            float length = 0f;
            for (int i = 1; i < points.Count; i++) length += Vector3.Distance(points[i - 1], points[i]);
            // 눈금 간격을 월드 길이에 맞춘다(타일 1칸 = TickSpacing).
            line.material.mainTextureScale = new Vector2(Mathf.Max(1f, length / TickSpacing), 1f);
            DrawEndArrow();
        }

        /// <summary>목적지 끝에만 진행 방향 화살표 하나를 둔다. 충돌체는 만들지 않는다.</summary>
        void DrawEndArrow()
        {
            if (arrow == null)
            {
                arrowMesh = new Mesh { name = "RehearsalPathArrow" };
                arrowMesh.vertices = new[] { new Vector3(0f, 0f, .7f), new Vector3(-.28f, 0f, -.25f), new Vector3(0f, 0f, -.05f), new Vector3(.28f, 0f, -.25f) };
                arrowMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                arrowMesh.RecalculateNormals();
                arrowMaterial = NewUnlit(LineColor);
                arrow = new GameObject("RehearsalPathArrow");
                arrow.transform.SetParent(transform, false);
                arrow.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
                var renderer = arrow.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = arrowMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            var a = points[points.Count - 2]; var b = points[points.Count - 1];
            var flat = Vector3.ProjectOnPlane(b - a, Vector3.up);
            if (flat.sqrMagnitude < .01f) { arrow.SetActive(false); return; }
            arrow.SetActive(true);
            arrow.transform.SetPositionAndRotation(b + Vector3.up * .02f, Quaternion.LookRotation(flat.normalized, Vector3.up));
        }

        void Hide()
        {
            if (line != null) line.enabled = false;
            if (arrow != null) arrow.SetActive(false);
        }

        static Material NewUnlit(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            return material;
        }

        void Build()
        {
            var go = new GameObject("RehearsalPathLine");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = LineWidth;
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.View;
            line.numCornerVertices = 4;
            // 끝 캡을 두지 않는다(시작 쪽 캡이 발밑에서 화살촉처럼 보였다).
            line.numCapVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            // 흰 실선에 어두운 눈금을 새긴다. 투명 구간이 없어 멀리서도 점점이 깨지지 않는다.
            var material = NewUnlit(Color.white);
            material.mainTexture = RulerTexture();
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", material.mainTexture);
            line.material = material;
            line.sortingOrder = 10;
        }

        /// <summary>자 모양: 흰 바탕에 1m마다 전체 높이 눈금, 0.5m에 절반 높이 눈금(어두운 색). 타일 1칸 = 1m.</summary>
        static Texture2D RulerTexture()
        {
            const int w = 64, h = 8;
            var ink = new Color(.18f, .18f, .2f, 1f);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    bool longTick = x < 3;                                   // 1m 눈금: 전체 높이
                    bool shortTick = x >= 32 && x < 35 && y >= 2 && y <= 5;  // 0.5m 눈금: 절반 높이
                    texture.SetPixel(x, y, longTick || shortTick ? ink : Color.white);
                }
            texture.Apply(true);
            return texture;
        }

        /// <summary>대기실 충돌체로 임시 NavMesh를 굽는다(플레이 테스트와 같은 방식). 문·철창은 닫힌 것만 막힌다.</summary>
        IEnumerator BuildNavMesh()
        {
            navBuilding = true;
            // 성공 직후 철창이 열리는 중의 충돌체를 길막으로 굽지 않는다.
            yield return new WaitForSecondsRealtime(3f);
            if (!target.HasValue) { navBuilding = false; yield break; }
            // 이동된 무대 통로의 집결 지점은 기존 고정 범위(z >= 0) 밖에 있다.
            // 출발·목적지와 여유 구간까지 포함해야 실제 통로 끝까지 구울 수 있다.
            var bounds = WaitingRoomBounds;
            bounds.Encapsulate(target.Value);
            if (NetworkPlayer.Local != null) bounds.Encapsulate(NetworkPlayer.Local.transform.position);
            bounds.Expand(12f);
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(s => s.component is Collider c && (c.isTrigger || !c.enabled || !c.gameObject.activeInHierarchy
                || c.GetComponentInParent<NetworkPlayer>() != null));
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = .35f; settings.agentHeight = 1.8f; settings.agentClimb = .4f; settings.agentSlope = 45f;
            settings.overrideVoxelSize = true; settings.voxelSize = .15f; settings.overrideTileSize = true; settings.tileSize = 128;
            navData = new NavMeshData(settings.agentTypeID) { hideFlags = HideFlags.HideAndDontSave };
            yield return NavMeshBuilder.UpdateNavMeshDataAsync(navData, settings, sources, bounds);
            navInstance = NavMesh.AddNavMeshData(navData);
            navReady = true; navBuilding = false;
        }

        void OnDisable() => Hide();

        void OnDestroy()
        {
            if (navInstance.valid) NavMesh.RemoveNavMeshData(navInstance);
            if (navData != null) Destroy(navData);
            if (arrowMesh != null) Destroy(arrowMesh);
            if (arrowMaterial != null) Destroy(arrowMaterial);
        }
    }
}
