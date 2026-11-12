using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>
    /// 굽기 뒤 배치가 바뀌어 길찾기 바닥이 물체 몸통 안을 지나는 곳을 실행 중에만 깎는다(씬·NavMesh 에셋은 수정하지 않음).
    /// 2026-11-12 측정: 11-05 굽기 이후 배치 변경으로 물체 50개(기계·팔레트 랙·컨테이너·보일러 등) 안으로 길이 나 있어
    /// 추격자가 물체 속을 걷고 그 안에서 오르기를 시작했다. 다시 구우면 깎을 물체가 없어 아무것도 하지 않는다.
    /// 문(<see cref="PursuitDoorObstacle"/>)·이미 장애물이 있는 물체·움직이는 물체는 건드리지 않는다.
    /// </summary>
    public static class PursuitNavMeshRepair
    {
        /// <summary>몸통 높이(발 위 0.6~1.2m)가 충돌체 안인 NavMesh 표본이 이 수 이상이면 깎는다.</summary>
        public const int MinInsideSamples = 2;
        static readonly string[] Dynamic = { "Door", "door", "Gate", "Leaf", "Breach" };
        static readonly List<GameObject> added = new();
        static readonly Collider[] buffer = new Collider[16];
        static readonly Vector3[] weights = { new(1 / 3f, 1 / 3f, 1 / 3f), new(.7f, .15f, .15f), new(.15f, .7f, .15f), new(.15f, .15f, .7f) };
        public static int Count => added.Count;
        public static bool Applied { get; private set; }
        public static int AppliedFrame { get; private set; } = -1;

        public static void Clear()
        {
            foreach (var go in added) if (go != null) { if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go); }
            added.Clear();
            Applied = false; AppliedFrame = -1;
        }

        /// <summary>현재 NavMesh에서 물체 안을 지나는 곳을 찾아 실행 중 장애물로 깎는다. 깎은 물체 수.</summary>
        public static int Apply()
        {
            Clear();
            Applied = true; AppliedFrame = Time.frameCount;
            var mesh = NavMesh.CalculateTriangulation();
            var vertices = mesh.vertices; var indices = mesh.indices;
            if (vertices == null || indices == null) return 0;
            var skip = new HashSet<Collider>();
            foreach (var door in Object.FindObjectsByType<PursuitDoorObstacle>(FindObjectsSortMode.None)) if (door.Source != null) skip.Add(door.Source);
            var inside = new Dictionary<Collider, int>();
            for (int i = 0; i + 2 < indices.Length; i += 3) {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                foreach (var w in weights) {
                    var p = a * w.x + b * w.y + c * w.z;
                    int n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * .6f, p + Vector3.up * 1.2f, .05f, buffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    for (int k = 0; k < n; k++) inside[buffer[k]] = inside.TryGetValue(buffer[k], out int count) ? count + 1 : 1;
                }
            }
            foreach (var (collider, count) in inside) {
                if (count < MinInsideSamples || skip.Contains(collider) || !Static(collider)) continue;
                var go = new GameObject("PursuitNavMeshRepair");
                var obstacle = go.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.carving = true; obstacle.carveOnlyStationary = true;
                if (collider is BoxCollider box) {
                    go.transform.SetParent(collider.transform, false);
                    obstacle.center = box.center; obstacle.size = box.size;
                } else {
                    var bounds = collider.bounds;
                    go.transform.position = bounds.center;
                    obstacle.size = bounds.size;
                }
                added.Add(go);
            }
            if (added.Count > 0) PursuitNavigation.Invalidate();
            Debug.Log($"[Pursuit] navmesh repair obstacles={added.Count}");
            return added.Count;
        }

        // 움직이거나 상태가 바뀌는 물체(문·출입구·부서지는 벽·장애물이 이미 있는 것·물리 물체·플레이어·추격자)는 제외한다.
        static bool Static(Collider c)
        {
            if (c == null || c.isTrigger || !c.enabled) return false;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return false;
            if (c.GetComponentInParent<NavMeshObstacle>() != null || c.GetComponentInParent<Animator>() != null) return false;
            if (c.GetComponentInParent<PursuitActor>() != null || c.GetComponentInParent<NetworkPlayer>() != null || c.GetComponentInParent<PlayerMover>() != null) return false;
            for (var t = c.transform; t != null; t = t.parent)
                foreach (var word in Dynamic) if (t.name.Contains(word)) return false;
            return c.bounds.size.y >= .5f;
        }
    }
}
