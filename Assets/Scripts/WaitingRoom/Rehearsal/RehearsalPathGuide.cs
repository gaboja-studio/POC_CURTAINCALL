using System.Collections;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 바닥 점선 길안내(기획서 15.3). 내 발밑에서 목적지까지 점선을 그린다. 대기실 씬은 NavMesh를 굽지 않으므로 처음 목적지를 받을 때
    /// 씬 충돌체로 임시 NavMesh를 한 번 굽는다(비동기). 완전한 경로가 없으면 안내를 숨긴다.
    /// </summary>
    public sealed class RehearsalPathGuide : MonoBehaviour
    {
        const float RefreshSeconds = .4f, LineHeight = .12f, DashLength = 1.2f;
        static readonly Bounds WaitingRoomBounds = new(new Vector3(-10f, 8f, 80f), new Vector3(160f, 60f, 160f));

        Vector3? target;
        LineRenderer line;
        readonly List<GameObject> arrows = new();
        Mesh arrowMesh;
        Material arrowMaterial;
        NavMeshData navData;
        NavMeshDataInstance navInstance;
        bool navReady, navBuilding;
        float nextRefresh;
        NavMeshPath path; // Unity: 필드 초기화에서 만들 수 없다(Awake에서).
        readonly List<Vector3> points = new();

        void Awake() => path = new NavMeshPath();

        public void SetTarget(Vector3? destination)
        {
            if (target == destination) return;
            target = destination;
            if (target.HasValue && !navReady && !navBuilding) StartCoroutine(BuildNavMesh());
            if (!target.HasValue)
            {
                if (line != null) line.enabled = false;
                HideArrows();
            }
        }

        void Update()
        {
            if (!target.HasValue || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + RefreshSeconds;
            var local = NetworkPlayer.Local;
            if (local == null) { if (line != null) line.enabled = false; return; }
            points.Clear();
            Vector3 from = local.transform.position + Vector3.up * LineHeight, to = target.Value + Vector3.up * LineHeight;
            if (navReady && NavMesh.SamplePosition(local.transform.position, out var origin, 2f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(target.Value, out var goal, 3f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(origin.position, goal.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete && path.corners.Length > 1)
            {
                points.Add(from);
                for (int i = 1; i < path.corners.Length; i++) points.Add(path.corners[i] + Vector3.up * LineHeight);
            }
            else
            {
                // 벽·철창을 관통하는 직선 안내는 표시하지 않는다.
                if (line != null) line.enabled = false;
                HideArrows();
                return;
            }
            Draw();
        }

        void Draw()
        {
            if (line == null) Build();
            line.enabled = true;
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            float length = 0f;
            for (int i = 1; i < points.Count; i++) length += Vector3.Distance(points[i - 1], points[i]);
            line.material.mainTextureScale = new Vector2(Mathf.Max(1f, length / DashLength), 1f);
            DrawArrows();
        }

        /// <summary>점선 위에 진행 방향 화살표를 놓는다. 길을 따라 걷는 방향을 보이며 충돌체는 만들지 않는다.</summary>
        void DrawArrows()
        {
            if (arrowMesh == null)
            {
                arrowMesh = new Mesh { name = "RehearsalPathArrow" };
                arrowMesh.vertices = new[] { new Vector3(0f, 0f, .55f), new Vector3(-.38f, 0f, -.38f), new Vector3(.38f, 0f, -.38f) };
                arrowMesh.triangles = new[] { 0, 2, 1 };
                arrowMesh.RecalculateNormals();
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                arrowMaterial = new Material(shader) { color = new Color(1f, .85f, .3f, 1f) };
                if (arrowMaterial.HasProperty("_BaseColor")) arrowMaterial.SetColor("_BaseColor", arrowMaterial.color);
            }
            int used = 0;
            float untilNext = 3f;
            for (int i = 1; i < points.Count; i++)
            {
                var a = points[i - 1]; var b = points[i];
                var flat = Vector3.ProjectOnPlane(b - a, Vector3.up);
                float length = flat.magnitude;
                if (length < .1f) continue;
                while (untilNext <= length)
                {
                    if (used == arrows.Count)
                    {
                        var go = new GameObject("RehearsalPathArrow");
                        go.transform.SetParent(transform, false);
                        go.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
                        go.AddComponent<MeshRenderer>().sharedMaterial = arrowMaterial;
                        arrows.Add(go);
                    }
                    var arrow = arrows[used++];
                    arrow.SetActive(true);
                    arrow.transform.SetPositionAndRotation(Vector3.Lerp(a, b, untilNext / length) + Vector3.up * .02f,
                        Quaternion.LookRotation(flat.normalized, Vector3.up));
                    untilNext += 3f;
                }
                untilNext -= length;
            }
            for (int i = used; i < arrows.Count; i++) arrows[i].SetActive(false);
        }

        void HideArrows() { foreach (var arrow in arrows) if (arrow != null) arrow.SetActive(false); }

        void Build()
        {
            var go = new GameObject("RehearsalPathLine");
            go.transform.SetParent(transform, false);
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = .3f;
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.View;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader) { mainTexture = DashTexture() };
            material.color = new Color(1f, .85f, .3f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(1f, .85f, .3f, 1f));
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", material.mainTexture);
            // 투명 구간(점선 사이)을 비우기 위해 URP Unlit의 알파 클립을 켠다.
            if (material.HasProperty("_AlphaClip")) { material.SetFloat("_AlphaClip", 1f); material.EnableKeyword("_ALPHATEST_ON"); }
            line.material = material;
            line.sortingOrder = 10;
        }

        static Texture2D DashTexture()
        {
            var texture = new Texture2D(8, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            for (int x = 0; x < 8; x++) texture.SetPixel(x, 0, x < 5 ? Color.white : Color.clear);
            texture.Apply();
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

        void OnDisable()
        {
            if (line != null) line.enabled = false;
            HideArrows();
        }

        void OnDestroy()
        {
            if (navInstance.valid) NavMesh.RemoveNavMeshData(navInstance);
            if (navData != null) Destroy(navData);
            if (arrowMesh != null) Destroy(arrowMesh);
            if (arrowMaterial != null) Destroy(arrowMaterial);
        }
    }
}
