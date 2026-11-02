using System.Collections;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 바닥 점선 길안내(기획서 15.3). 내 발밑에서 목적지까지 점선을 그린다. 대기실 씬은 NavMesh를 굽지 않으므로 처음 목적지를 받을 때
    /// 씬 충돌체로 임시 NavMesh를 한 번 굽고(비동기), 그 전에는 직선으로 그린다. 길이 끊기면 가장 가까운 지점까지만 그린다.
    /// </summary>
    public sealed class RehearsalPathGuide : MonoBehaviour
    {
        const float RefreshSeconds = .4f, LineHeight = .12f, DashLength = 1.2f;
        static readonly Bounds WaitingRoomBounds = new(new Vector3(-10f, 8f, 80f), new Vector3(160f, 60f, 160f));

        Vector3? target;
        LineRenderer line;
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
            if (!target.HasValue && line != null) line.enabled = false;
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
                && NavMesh.CalculatePath(origin.position, goal.position, NavMesh.AllAreas, path) && path.corners.Length > 1)
            {
                points.Add(from);
                for (int i = 1; i < path.corners.Length; i++) points.Add(path.corners[i] + Vector3.up * LineHeight);
            }
            else { points.Add(from); points.Add(to); }
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
        }

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
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(WaitingRoomBounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(s => s.component is Collider c && (c.isTrigger || !c.enabled || !c.gameObject.activeInHierarchy
                || c.GetComponentInParent<NetworkPlayer>() != null));
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = .35f; settings.agentHeight = 1.8f; settings.agentClimb = .4f; settings.agentSlope = 45f;
            settings.overrideVoxelSize = true; settings.voxelSize = .15f; settings.overrideTileSize = true; settings.tileSize = 128;
            navData = new NavMeshData(settings.agentTypeID) { hideFlags = HideFlags.HideAndDontSave };
            yield return NavMeshBuilder.UpdateNavMeshDataAsync(navData, settings, sources, WaitingRoomBounds);
            navInstance = NavMesh.AddNavMeshData(navData);
            navReady = true; navBuilding = false;
        }

        void OnDisable()
        {
            if (line != null) line.enabled = false;
        }

        void OnDestroy()
        {
            if (navInstance.valid) NavMesh.RemoveNavMeshData(navInstance);
            if (navData != null) Destroy(navData);
        }
    }
}
