using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>
    /// 규칙 기반 오르기 통로. 씬·오브젝트·NavMesh 에셋을 수정하지 않고, 실행 중 로드된 NavMesh의 가장자리에서
    /// 걸어서 넘지 못하는 단차(약 0.55m 초과 ~ <see cref="PursuitSettings.ClimbMaxHeight"/> 이하, 0이면 제한 없음)를 찾아 메모리 링크만 추가한다.
    /// 대기실 배치가 바뀌어도 NavMesh만 다시 구우면 다음 실행에서 자동으로 따라간다.
    /// 링크는 내장 Jump 구역이며, 추격자는 단계가 <see cref="PursuitSettings.ClimbFromStage"/> 이상일 때만 이 구역을 지난다.
    /// </summary>
    public static class PursuitClimbLinks
    {
        /// <summary>내장 NavMesh 구역 "Jump"(ProjectSettings/NavMeshAreas 2번).</summary>
        public const int Area = 2;
        public const int AreaMask = 1 << Area;
        /// <summary>걸어서 넘는 높이(베이크 agentClimb 0.55)보다 조금 높은 곳부터 오른다.</summary>
        public const float MinHeight = .6f;
        const int MaxLinks = 512, MaxSamplesPerEdge = 24;
        // 가장자리 바깥 바닥을 찾는 거리, 위쪽 여유(몸이 넘어갈 높이), 바닥 탐색 반경.
        // 가장자리와 바닥 NavMesh는 각각 에이전트 반경만큼 물러나 있으므로 바깥 거리를 몇 단계로 넓혀 본다.
        static readonly float[] ProbeOut = { .55f, .9f, 1.25f };
        const float LipClearance = 1.2f, SampleRadius = .45f;

        static readonly List<NavMeshLinkInstance> links = new();
        public static int Count => links.Count;
        public static double BuildMs { get; private set; }
        public static bool Built { get; private set; }
        static readonly RaycastHit[] hits = new RaycastHit[16];

        public static void Clear()
        {
            foreach (var link in links) NavMesh.RemoveLink(link);
            links.Clear();
            Built = false;
        }

        /// <summary>현재 NavMesh로 오르기 링크를 만든다. 오르기 단계가 꺼져 있으면 만들지 않는다.</summary>
        public static int Build(int agentTypeID, PursuitSettings rules)
        {
            Clear();
            Built = true;
            if (rules == null || rules.ClimbFromStage <= 0) return 0;
            long started = Stopwatch.GetTimestamp();
            var candidates = Candidates(rules.ResolvedClimbMaxHeight, agentTypeID);
            var walk = new NavMeshQueryFilter { agentTypeID = agentTypeID, areaMask = NavMesh.AllAreas & ~AreaMask };
            var path = new NavMeshPath();
            var accepted = new List<Vector3>();
            float spacing = Mathf.Max(.5f, rules.ClimbLinkSpacing);
            // 소품 높이(2.5m 이하) 링크가 높은 벽 링크에 밀려 상한(MaxLinks)에서 빠지지 않게 먼저 받는다(각 묶음 안 순서는 유지).
            foreach (var (bottom, top) in candidates.OrderBy(c => c.top.y - c.bottom.y > PursuitClimbPath.WallClimbFrom ? 1 : 0)) {
                if (links.Count >= MaxLinks) break;
                if (Near(accepted, top, spacing)) continue;
                // 계단·경사로로 가까이 이어진 곳은 오르기가 필요 없다.
                float straight = Vector3.Distance(bottom, top);
                if (NavMesh.CalculatePath(bottom, top, walk, path) && path.status == NavMeshPathStatus.PathComplete
                    && PursuitNavigation.Length(path.corners) <= Mathf.Max(straight * 3, straight + 6)) continue;
                // 위에서 뛰어내릴 쪽에 난간·턱(발 위 0.5m)이 있으면 오르기 전용: 내려갈 때는 계단 등 다른 길로 간다(난간을 뚫고 뛰어내리지 않게).
                bool drop = PursuitDirectClimb.Open(top + Vector3.up * .5f, new Vector3(bottom.x, top.y + .5f, bottom.z), null);
                var instance = NavMesh.AddLink(new NavMeshLinkData {
                    startPosition = bottom, endPosition = top, width = 0, bidirectional = drop,
                    area = Area, agentTypeID = agentTypeID,
                    // 오르는 시간(높을수록 김) 동안 달릴 거리만큼 비싸게: 짧은 우회가 있으면 우회한다.
                    costModifier = PursuitActor.TraversalSeconds(rules, true, top.y - bottom.y) * 6f / Mathf.Max(.1f, straight) });
                if (!NavMesh.IsLinkValid(instance)) continue;
                links.Add(instance); accepted.Add(top);
            }
            BuildMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            UnityEngine.Debug.Log($"[Pursuit] climb links={links.Count} candidates={candidates.Count} {BuildMs:0.0}ms");
            return links.Count;
        }

        /// <summary>
        /// NavMesh 가장자리(삼각형 하나에만 속한 변)의 바깥 아래에서 바닥을 찾아 (아래, 위) 쌍을 만든다.
        /// 위쪽 끝은 가장자리 위, 아래쪽 끝은 바깥 바닥. 몸이 지나갈 공간이 막히면(벽·천장·선반) 버린다.
        /// 난간(약 1.1m 이하)은 넘어갈 수 있다.
        /// </summary>
        public static List<(Vector3 bottom, Vector3 top)> Candidates(float maxHeight, int agentTypeID)
        {
            var result = new List<(Vector3, Vector3)>();
            var mesh = NavMesh.CalculateTriangulation();
            var vertices = mesh.vertices; var indices = mesh.indices;
            if (vertices == null || indices == null || indices.Length < 3) return result;
            // 같은 위치의 정점을 하나로 묶어 타일 경계의 가짜 가장자리를 없앤다.
            var weld = new int[vertices.Length];
            var byPosition = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < vertices.Length; i++) {
                var v = vertices[i];
                var key = new Vector3Int(Mathf.RoundToInt(v.x * 50), Mathf.RoundToInt(v.y * 50), Mathf.RoundToInt(v.z * 50));
                if (!byPosition.TryGetValue(key, out weld[i])) { weld[i] = i; byPosition[key] = i; }
            }
            var edges = new Dictionary<(int, int), (int count, int a, int b, int c)>();
            for (int i = 0; i + 2 < indices.Length; i += 3) {
                int a = weld[indices[i]], b = weld[indices[i + 1]], c = weld[indices[i + 2]];
                Add(a, b, c); Add(b, c, a); Add(c, a, b);
            }
            void Add(int a, int b, int c)
            {
                var key = a < b ? (a, b) : (b, a);
                edges[key] = edges.TryGetValue(key, out var e) ? (e.count + 1, e.a, e.b, e.c) : (1, a, b, c);
            }
            var query = new NavMeshQueryFilter { agentTypeID = agentTypeID, areaMask = NavMesh.AllAreas & ~AreaMask };
            // 높이 제한 없음(무한대)이어도 NavMesh 전체 높이보다 깊이 찾을 필요는 없다.
            float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
            foreach (var v in vertices) { lowest = Mathf.Min(lowest, v.y); highest = Mathf.Max(highest, v.y); }
            float maxDrop = Mathf.Max(MinHeight, Mathf.Min(maxHeight, highest - lowest + SampleRadius));
            foreach (var edge in edges.Values) {
                if (edge.count != 1) continue;
                Vector3 a = vertices[edge.a], b = vertices[edge.b], inside = vertices[edge.c];
                var along = b - a; along.y = 0;
                float length = along.magnitude;
                if (length < .2f) continue;
                var outward = Vector3.Cross(Vector3.up, along / length);
                if (Vector3.Dot(outward, inside - a) > 0) outward = -outward;
                int samples = Mathf.Clamp(Mathf.FloorToInt(length / .75f), 1, MaxSamplesPerEdge);
                for (int s = 0; s < samples; s++) {
                    var top = Vector3.Lerp(a, b, (s + .5f) / samples);
                    if (TryBottom(top, outward, maxDrop, query, out var bottom) && Clear(bottom, top, outward))
                        result.Add((bottom, top));
                }
            }
            return result;
        }

        static bool TryBottom(Vector3 top, Vector3 outward, float maxDrop, NavMeshQueryFilter query, out Vector3 bottom)
        {
            bottom = default;
            foreach (float distance in ProbeOut) {
                var probe = top + outward * distance;
                // 위쪽 끝(가장자리 자신)은 반경 밖이 되도록 MinHeight 아래부터 내려가며 찾는다.
                float near = Mathf.Min(maxDrop, PursuitClimbPath.WallClimbFrom + SampleRadius);
                for (float drop = MinHeight; drop <= near + .01f; drop += SampleRadius * .8f)
                    if (Accept(top, probe + Vector3.down * drop, maxDrop, query, out bottom)) return true;
                // 그보다 깊은 바닥(높이 제한 없음)은 수직 아래 실제 표면에서만 찾는다(빈 공중을 촘촘히 훑지 않음).
                if (maxDrop <= near) continue;
                int count = Physics.RaycastNonAlloc(probe + Vector3.down * near, Vector3.down, hits, maxDrop - near + SampleRadius, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, 0, count, HitDistance);
                for (int i = 0; i < count; i++) if (Accept(top, hits[i].point, maxDrop, query, out bottom)) return true;
            }
            return false;
        }

        // point(가장자리 바깥 수직선 위) 근처 바닥 NavMesh가 오르기 높이 범위면 아래쪽 끝으로 쓴다.
        static bool Accept(Vector3 top, Vector3 point, float maxDrop, NavMeshQueryFilter query, out Vector3 bottom)
        {
            bottom = default;
            if (!NavMesh.SamplePosition(point, out var hit, SampleRadius, query)) return false;
            float height = top.y - hit.position.y;
            var flat = hit.position - point; flat.y = 0;
            if (height < MinHeight || height > maxDrop || flat.magnitude > SampleRadius) return false;
            bottom = hit.position;
            return true;
        }
        static readonly IComparer<RaycastHit> HitDistance = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        // 바닥에서 위로 서서 손을 뻗을 공간, 그리고 가장자리 위로 몸이 넘어갈 공간이 비어 있는지.
        static bool Clear(Vector3 bottom, Vector3 top, Vector3 outward)
        {
            var column = new Vector3(bottom.x, top.y + LipClearance, bottom.z);
            // 서서 뻗는 기둥은 몸 두께로 검사한다(통로 바닥 밑·난간 옆에서 몸이 겹친 채 오르지 않게).
            return PursuitDirectClimb.BodyOpen(bottom + Vector3.up * PursuitDirectClimb.BodyLift, column, null)
                && Open(column, top - outward * .3f + Vector3.up * LipClearance);
        }

        static bool Open(Vector3 from, Vector3 to)
        {
            var direction = to - from;
            int count = Physics.RaycastNonAlloc(from, direction, hits, direction.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) {
                var t = hits[i].transform;
                // 생성 시점에 서 있던 플레이어·추격자는 지형이 아니다.
                if (t.GetComponentInParent<NetworkPlayer>() != null || t.GetComponentInParent<PursuitActor>() != null) continue;
                return false;
            }
            return true;
        }

        static bool Near(List<Vector3> points, Vector3 p, float spacing)
        {
            foreach (var q in points) if ((q - p).sqrMagnitude < spacing * spacing) return true;
            return false;
        }
    }
}
