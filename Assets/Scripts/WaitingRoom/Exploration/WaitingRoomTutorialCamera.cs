using System.Collections;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 통행 가능한 바닥 경로 위로 이동하는 개인 튜토리얼 카메라. 잠긴 장소의 안내는 <see cref="WaitingRoomTutorial"/>이 해금 뒤로 미루므로 문을 뚫지 않는다.
    /// 바닥 경로가 없으면 안내를 건너뛰지 않고 목적지까지 곧장 날아간다. 다 보여 준 뒤에는 되돌아가는 이동 없이 바로 플레이어 시점으로 바뀐다(2026-11-17 사용자).
    /// </summary>
    public sealed class WaitingRoomTutorialCamera : MonoBehaviour
    {
        /// <summary>복귀는 화면 전환(컷)이라 이동 시간이 없다.</summary>
        public const float MoveIn = 1.8f, Hold = 2.5f, MoveOut = 0f, Total = MoveIn + Hold + MoveOut;
        const int Priority = 230;
        const float EyeHeight = 1.6f, Clearance = .12f;
        /// <summary>모퉁이를 둥글게 도는 최대 반경(m)·앞을 내다보는 거리(m)·회전이 따라가는 빠르기·호를 나누는 수.</summary>
        const float CornerRadius = 2.5f, LookAhead = 3f, TurnRate = 5f;
        const int CornerSteps = 6;
        CinemachineCamera view;
        CinemachineBrain brain;
        CinemachineBlendDefinition savedBlend;
        CinemachineBlenderSettings savedCustom;
        bool blendCut, routeReady, chained;
        int restoreAtFrame = -1;
        Vector3 fromPos, toPos, focusPoint, homeFeet;
        Quaternion fromRot, toRot;
        float startedAt, routeLength, moveIn;
        readonly List<Vector3> route = new();
        Coroutine preparing;
        NavMeshData navData;
        NavMeshDataInstance navInstance;
        NavMeshQueryFilter navFilter;
        static bool Ignored(Collider c) => c == null || passThrough.Contains(c) || c.GetComponentInParent<NetworkPlayer>() != null;
        /// <summary>카메라가 돌아가지 않고 그대로 뚫고 지나가는 장식(로비 가운데 원뿔 천막과 그 둘레 소품, 2026-11-17 사용자). 경로를 준비할 때마다 다시 모은다.</summary>
        const string TentRootName = "ShabbyChristmasTent";
        static readonly HashSet<Collider> passThrough = new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => passThrough.Clear();
        static void CollectPassThrough()
        {
            passThrough.Clear();
            var tent = GameObject.Find(TentRootName);
            if (tent != null) passThrough.UnionWith(tent.GetComponentsInChildren<Collider>(true));
        }

        public bool IsActive => view != null;
        /// <summary>이동·복귀뿐 아니라 원래 카메라 블렌드 복구까지 끝나야 다음 안내를 시작할 수 있다.</summary>
        public bool IsBusy => view != null || preparing != null || blendCut || restoreAtFrame >= 0;
        public Vector3[] RoutePoints => route.ToArray();
        public bool RouteCompleted { get; private set; }
        public int CompletedRouteCount { get; private set; }
        public Vector3 LastCompletedFocus { get; private set; }
        public string RouteFailure { get; private set; }
        /// <summary>지금 장소를 다 보여줘 복귀를 시작할 차례다. 이 프레임에 <see cref="Continue"/>를 부르면 복귀 없이 다음 장소로 간다.</summary>
        public bool HoldEnded => view != null && routeReady && Time.unscaledTime - startedAt >= moveIn + Hold;
        /// <summary>바닥 경로 없이 곧장 날아가는 중(막힘 검사를 하지 않는다).</summary>
        bool direct;
        int holdEndFrame = -1;

        public bool Begin(Vector3 pos, Quaternion rot) => Begin(pos, rot, pos + rot * Vector3.forward * 4f);

        public bool Begin(Vector3 pos, Quaternion rot, Vector3 focus)
        {
            var main = Camera.main;
            if (main == null || NetworkPlayer.Local == null) return false;
            Stop();
            fromPos = main.transform.position; fromRot = main.transform.rotation;
            homeFeet = NetworkPlayer.Local.transform.position;
            toPos = pos; toRot = rot; focusPoint = focus;
            routeReady = chained = false; route.Clear(); RouteCompleted = false; RouteFailure = ""; holdEndFrame = -1;
            CutBlends(main);
            var go = new GameObject("TutorialCloseUpCamera");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(fromPos, fromRot);
            view = go.AddComponent<CinemachineCamera>();
            view.Lens = LensSettings.FromCamera(main); view.Priority = Priority;
            preparing = StartCoroutine(PrepareRoute(homeFeet));
            return true;
        }

        /// <summary>플레이어에게 돌아가지 않고 지금 장소에서 다음 장소로 이어 간다. 경로를 못 찾으면 그대로 플레이어에게 돌아간다.</summary>
        public bool Continue(Vector3 pos, Quaternion rot, Vector3 focus)
        {
            if (!HoldEnded) return false;
            RouteCompleted = false; CompletedRouteCount++; LastCompletedFocus = focusPoint;
            fromPos = view.transform.position; fromRot = view.transform.rotation;
            toPos = pos; toRot = rot; focusPoint = focus;
            routeReady = false; chained = true; RouteFailure = ""; holdEndFrame = -1;
            preparing = StartCoroutine(PrepareRoute(fromPos + Vector3.down * EyeHeight));
            return true;
        }

        IEnumerator PrepareRoute(Vector3 feet)
        {
            direct = false; CollectPassThrough();
            Vector3 wantedPos = toPos; Quaternion wantedRot = toRot;
            var bounds = new Bounds(new Vector3(-10f, 8f, 80f), new Vector3(160f, 60f, 160f));
            bounds.Encapsulate(feet); bounds.Encapsulate(toPos); bounds.Encapsulate(homeFeet); bounds.Expand(12f);
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(s => s.component is Collider c && (c.isTrigger || !c.enabled || !c.gameObject.activeInHierarchy
                || Ignored(c)));
            // 전용 에이전트로 굽고 조회한다. 같은 에이전트면 리허설 때 구운 길안내 NavMesh(철창이 닫힌 옛 맵)가 경로에 섞인다.
            var settings = NavMesh.CreateSettings();
            navFilter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = NavMesh.AllAreas };
            settings.agentRadius = .35f; settings.agentHeight = 1.8f; settings.agentClimb = .4f; settings.agentSlope = 45f;
            settings.overrideVoxelSize = true; settings.voxelSize = .15f;
            settings.overrideTileSize = true; settings.tileSize = 128;
            navData = new NavMeshData(settings.agentTypeID) { hideFlags = HideFlags.HideAndDontSave };
            yield return NavMeshBuilder.UpdateNavMeshDataAsync(navData, settings, sources, bounds);
            if (view == null) yield break;
            navInstance = NavMesh.AddNavMeshData(navData);
            // 출발점은 카메라가 아니라 플레이어 발이다. 3인칭 카메라는 벽 너머·틈에 있을 수 있어 다른 공간의 바닥에 붙는다(2026-11-04 G10 실패).
            var start = feet;
            var desired = new Vector3(toPos.x, feet.y, toPos.z);
            NavMeshHit origin = default, goal = default;
            NavMeshPath path = null;
            bool valid = NavMesh.SamplePosition(start, out origin, 3f, navFilter)
                && TryReachableGoal(origin.position, desired, feet.y, out goal, out path);
            var leg = new List<Vector3>();
            if (valid)
            {
                toPos = goal.position + Vector3.up * EyeHeight;
                if ((focusPoint - toPos).sqrMagnitude > .001f) toRot = Quaternion.LookRotation(focusPoint - toPos, Vector3.up);
                leg.Add(fromPos);
                // 처음에는 카메라에서 플레이어 눈높이로 먼저 들어간 뒤 바닥 경로를 따른다(지금 화면이 보던 구간이라 벽 검사를 하지 않는다).
                if (!chained) AddPoint(leg, feet + Vector3.up * EyeHeight);
                foreach (var corner in path.corners) AddPoint(leg, corner + Vector3.up * EyeHeight);
                AddPoint(leg, toPos);
                for (int i = chained ? 1 : 2; i < leg.Count; i++)
                    if (!ClearSegment(leg[i - 1], leg[i])) { RouteFailure = $"경로 구간 {i}: {leg[i - 1]} → {leg[i]}"; valid = false; break; }
            }
            if (valid && leg.Count >= 2)
            {
                // 벽을 따라 꺾이는 경로점을 그대로 타면 카메라가 튄다. 막히지 않는 만큼 곧게 잇고 모퉁이는 둥글게 돈다.
                var smooth = RoundCorners(Simplify(leg));
                route.Clear(); route.AddRange(smooth);
            }
            ReleaseNavMesh(); preparing = null;
            if (!valid || leg.Count < 2)
            {
                if (string.IsNullOrEmpty(RouteFailure)) RouteFailure = $"바닥 경로 없음: {start} → {desired}, {path?.status.ToString() ?? "후보 없음"}";
                Debug.LogWarning("[TutorialCamera] 통행 가능한 카메라 경로가 없어 목적지까지 곧장 이동합니다. " + RouteFailure);
                // 안내를 건너뛰거나 연달아 넘기지 않게, 원래 잡은 자리까지 곧장 날아가 보여 준다.
                direct = true; toPos = wantedPos; toRot = wantedRot;
                route.Clear(); route.Add(fromPos); route.Add(toPos);
                if (Vector3.Distance(fromPos, toPos) < .05f) route[1] = toPos + Vector3.up * .05f;
            }
            routeLength = Length(route);
            moveIn = Mathf.Max(MoveIn, routeLength / 14f);
            startedAt = Time.unscaledTime; routeReady = true; holdEndFrame = -1;
        }

        static float Length(List<Vector3> points)
        {
            float length = 0f;
            for (int i = 1; i < points.Count; i++) length += Vector3.Distance(points[i - 1], points[i]);
            return length;
        }

        bool TryReachableGoal(Vector3 origin, Vector3 desired, float floorY, out NavMeshHit goal, out NavMeshPath bestPath)
        {
            goal = default; bestPath = null;
            var candidates = new List<Vector3> { desired };
            Vector3 center = new(focusPoint.x, floorY, focusPoint.z);
            Vector3 preferred = desired - center; preferred.y = 0f;
            if (preferred.sqrMagnitude < .01f) preferred = origin - center;
            if (preferred.sqrMagnitude < .01f) preferred = Vector3.back;
            preferred.Normalize();
            // 목표 오브젝트 위·뒤의 고립된 NavMesh가 먼저 잡혀도, 플레이어가 실제 갈 수 있는 둘레 지점을 찾는다.
            foreach (float radius in new[] { 2.5f, 3.5f, 4.5f, 6f })
                for (int i = 0; i < 16; i++)
                {
                    float angle = i == 0 ? 0f : (i % 2 == 1 ? (i + 1) / 2f : -i / 2f) * 22.5f;
                    candidates.Add(center + Quaternion.Euler(0f, angle, 0f) * preferred * radius);
                }

            float bestScore = float.PositiveInfinity;
            foreach (var candidate in candidates)
            {
                if (!NavMesh.SamplePosition(candidate, out var sampled, 2.5f, navFilter)) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(origin, sampled.position, navFilter, path)
                    || path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2) continue;
                float score = (sampled.position - desired).sqrMagnitude;
                // 같은 대상을 비추면 돌아가지 않는 지점을 고른다.
                for (int i = 1; i < path.corners.Length; i++) score += Vector3.Distance(path.corners[i - 1], path.corners[i]) * .25f;
                if (score >= bestScore) continue;
                bestScore = score; goal = sampled; bestPath = path;
            }
            return bestPath != null;
        }

        static void AddPoint(List<Vector3> points, Vector3 point)
        {
            if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], point) > .03f) points.Add(point);
        }

        /// <summary>앞 지점에서 막힘 없이 곧장 갈 수 있는 가장 먼 경로점으로 건너뛴다. 곧장 못 가면 다음 점을 그대로 쓴다.</summary>
        static List<Vector3> Simplify(List<Vector3> points)
        {
            var result = new List<Vector3> { points[0] };
            int anchor = 0;
            while (anchor < points.Count - 1)
            {
                int next = anchor + 1;
                for (int i = points.Count - 1; i > anchor + 1; i--)
                    if (ClearSegment(points[anchor], points[i])) { next = i; break; }
                result.Add(points[next]);
                anchor = next;
            }
            return result;
        }

        /// <summary>꺾이는 점을 호로 바꾼다. 호가 막히면 그 모퉁이만 그대로 둔다.</summary>
        static List<Vector3> RoundCorners(List<Vector3> points)
        {
            var result = new List<Vector3> { points[0] };
            var arc = new List<Vector3>();
            for (int i = 1; i < points.Count - 1; i++)
            {
                Vector3 corner = points[i], before = points[i - 1] - corner, after = points[i + 1] - corner;
                float radius = Mathf.Min(CornerRadius, before.magnitude * .45f, after.magnitude * .45f);
                Vector3 enter = corner + before.normalized * radius, exit = corner + after.normalized * radius;
                arc.Clear(); arc.Add(enter);
                bool clear = true;
                for (int step = 1; step <= CornerSteps && clear; step++)
                {
                    float t = step / (float)CornerSteps;
                    var point = Vector3.Lerp(Vector3.Lerp(enter, corner, t), Vector3.Lerp(corner, exit, t), t);
                    clear = ClearSegment(arc[arc.Count - 1], point);
                    arc.Add(point);
                }
                if (clear) result.AddRange(arc); else result.Add(corner);
            }
            result.Add(points[points.Count - 1]);
            return result;
        }

        static bool ClearSegment(Vector3 a, Vector3 b)
        {
            var delta = b - a;
            if (delta.sqrMagnitude < .0001f) return true;
            foreach (var hit in Physics.SphereCastAll(a, Clearance, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!Ignored(hit.collider)) return false;
            return true;
        }

        static Vector3 PointAt(List<Vector3> points, float total, float fraction)
        {
            float distance = Mathf.Clamp01(fraction) * total;
            for (int i = 1; i < points.Count; i++)
            {
                float length = Vector3.Distance(points[i - 1], points[i]);
                if (distance <= length || i == points.Count - 1)
                    return Vector3.Lerp(points[i - 1], points[i], length > 0f ? distance / length : 0f);
                distance -= length;
            }
            return points[0];
        }

        public void Stop()
        {
            if (preparing != null) { StopCoroutine(preparing); preparing = null; }
            ReleaseNavMesh(); routeReady = false;
            if (view != null) Destroy(view.gameObject);
            view = null;
            if (blendCut) restoreAtFrame = Time.frameCount + 2;
        }

        void LateUpdate()
        {
            if (view != null && routeReady)
            {
                float t = Time.unscaledTime - startedAt;
                if (t >= moveIn + Hold)
                {
                    // 다 보여 줬다. 한 프레임은 다음 장소로 이어 갈 기회(Continue)를 주고, 이어 가지 않으면 되돌아가는 이동 없이 바로 플레이어 시점으로 바꾼다.
                    if (holdEndFrame < 0) holdEndFrame = Time.frameCount;
                    else if (Time.frameCount > holdEndFrame)
                    { RouteCompleted = true; CompletedRouteCount++; LastCompletedFocus = focusPoint; Stop(); }
                }
                else
                {
                    float progress = Mathf.SmoothStep(0f, 1f, t / moveIn);
                    var position = PointAt(route, routeLength, progress);
                    // 플레이어 쪽 첫 구간(카메라 ↔ 플레이어 눈높이)은 원래 화면이 보던 구간이라 막힘 검사를 하지 않는다.
                    bool nearPlayer = !chained && progress * routeLength <= Vector3.Distance(route[0], route[1]) + .05f;
                    // 가는 길이 막혀도(그 사이 닫힌 문 등) 연출을 끊지 않고 그대로 지나간다.
                    if (!direct && !nearPlayer && !ClearSegment(view.transform.position, position)) direct = true;
                    Quaternion rotation = toRot;
                    if (t < moveIn)
                    {
                        // 지금 구간이 아니라 조금 앞의 경로를 바라본다. 모퉁이에서 고개가 꺾이지 않고 미리 돌아간다.
                        var look = PointAt(route, routeLength, progress + LookAhead / Mathf.Max(routeLength, .01f)) - position;
                        look.y = 0f;
                        rotation = look.sqrMagnitude > .01f ? Quaternion.LookRotation(look) : view.transform.rotation;
                        rotation = Quaternion.Slerp(rotation, toRot, Mathf.SmoothStep(0f, 1f, progress * 5f - 4f));
                    }
                    view.transform.SetPositionAndRotation(position, Quaternion.Slerp(view.transform.rotation, rotation, 1f - Mathf.Exp(-TurnRate * Time.unscaledDeltaTime)));
                }
            }
            if (restoreAtFrame >= 0 && Time.frameCount >= restoreAtFrame) { restoreAtFrame = -1; RestoreBlends(); }
        }

        void CutBlends(Camera main)
        {
            restoreAtFrame = -1;
            if (blendCut) return;
            brain = main.GetComponent<CinemachineBrain>();
            if (brain == null) return;
            savedBlend = brain.DefaultBlend; savedCustom = brain.CustomBlends;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            brain.CustomBlends = null; blendCut = true;
        }
        void RestoreBlends()
        {
            if (!blendCut) return;
            if (brain != null) { brain.DefaultBlend = savedBlend; brain.CustomBlends = savedCustom; }
            brain = null; blendCut = false;
        }
        void ReleaseNavMesh()
        {
            if (navInstance.valid) NavMesh.RemoveNavMeshData(navInstance);
            if (navData != null) { NavMeshBuilder.Cancel(navData); Destroy(navData); }
            navData = null;
            if (navFilter.agentTypeID != 0) NavMesh.RemoveSettings(navFilter.agentTypeID);
            navFilter = default;
        }
        void OnDisable() { Stop(); RestoreBlends(); }

        public static void FramePose(Bounds focus, Vector3 viewer, out Vector3 pos, out Quaternion rot)
        {
            var center = focus.center;
            var away = viewer - center; away.y = 0f;
            if (away.sqrMagnitude < .01f) away = Vector3.back;
            away.Normalize();
            float radius = Mathf.Max(focus.extents.x, focus.extents.z);
            float distance = Mathf.Clamp(radius * 2.2f + 1.5f, 2.5f, 7f);
            var destination = center + away * distance;
            destination.y = NetworkPlayer.Local != null ? NetworkPlayer.Local.transform.position.y + EyeHeight : center.y + 1f;
            var delta = destination - center;
            var dir = delta.normalized;
            float length = delta.magnitude;
            var start = center + dir * Mathf.Min(radius, length * .5f);
            float free = length;
            foreach (var hit in Physics.RaycastAll(start, dir, length, ~0, QueryTriggerInteraction.Ignore))
                if (!focus.Contains(hit.point) && hit.distance + Mathf.Min(radius, length * .5f) - .35f < free)
                    free = Mathf.Max(.2f, hit.distance + Mathf.Min(radius, length * .5f) - .35f);
            pos = center + dir * free;
            rot = Quaternion.LookRotation(center - pos, Vector3.up);
        }
    }
}
