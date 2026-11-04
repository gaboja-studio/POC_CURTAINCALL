using System.Collections;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom
{
    /// <summary>통행 가능한 바닥 경로 위로 이동하는 개인 튜토리얼 카메라. 경로가 없으면 벽을 관통하는 이동을 하지 않는다.</summary>
    public sealed class WaitingRoomTutorialCamera : MonoBehaviour
    {
        public const float MoveIn = 1.8f, Hold = 2.5f, MoveOut = MoveIn / 3f, Total = MoveIn + Hold + MoveOut;
        const int Priority = 230;
        const float EyeHeight = 1.6f, Clearance = .12f;
        CinemachineCamera view;
        CinemachineBrain brain;
        CinemachineBlendDefinition savedBlend;
        CinemachineBlenderSettings savedCustom;
        bool blendCut, routeReady;
        int restoreAtFrame = -1;
        Vector3 fromPos, toPos, focusPoint;
        Quaternion fromRot, toRot;
        float startedAt, routeLength, moveIn, moveOut;
        readonly List<Vector3> route = new();
        Coroutine preparing;
        NavMeshData navData;
        NavMeshDataInstance navInstance;
        public bool IsActive => view != null;
        /// <summary>이동·복귀뿐 아니라 원래 카메라 블렌드 복구까지 끝나야 다음 안내를 시작할 수 있다.</summary>
        public bool IsBusy => view != null || preparing != null || blendCut || restoreAtFrame >= 0;
        public Vector3[] RoutePoints => route.ToArray();
        public bool RouteCompleted { get; private set; }
        public int CompletedRouteCount { get; private set; }
        public Vector3 LastCompletedFocus { get; private set; }
        public string RouteFailure { get; private set; }

        public bool Begin(Vector3 pos, Quaternion rot) => Begin(pos, rot, pos + rot * Vector3.forward * 4f);

        public bool Begin(Vector3 pos, Quaternion rot, Vector3 focus)
        {
            var main = Camera.main;
            if (main == null || NetworkPlayer.Local == null) return false;
            Stop();
            fromPos = main.transform.position; fromRot = main.transform.rotation;
            toPos = pos; toRot = rot; focusPoint = focus;
            routeReady = false; route.Clear(); RouteCompleted = false; RouteFailure = "";
            CutBlends(main);
            var go = new GameObject("TutorialCloseUpCamera");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(fromPos, fromRot);
            view = go.AddComponent<CinemachineCamera>();
            view.Lens = LensSettings.FromCamera(main); view.Priority = Priority;
            preparing = StartCoroutine(PrepareRoute(NetworkPlayer.Local.transform.position));
            return true;
        }

        IEnumerator PrepareRoute(Vector3 feet)
        {
            var bounds = new Bounds(new Vector3(-10f, 8f, 80f), new Vector3(160f, 60f, 160f));
            bounds.Encapsulate(feet); bounds.Encapsulate(toPos); bounds.Expand(12f);
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(s => s.component is Collider c && (c.isTrigger || !c.enabled || !c.gameObject.activeInHierarchy
                || c.GetComponentInParent<NetworkPlayer>() != null));
            var settings = NavMesh.GetSettingsByIndex(0);
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
            bool valid = NavMesh.SamplePosition(start, out origin, 3f, NavMesh.AllAreas)
                && TryReachableGoal(origin.position, desired, feet.y, out goal, out path);
            if (valid)
            {
                toPos = goal.position + Vector3.up * EyeHeight;
                if ((focusPoint - toPos).sqrMagnitude > .001f) toRot = Quaternion.LookRotation(focusPoint - toPos, Vector3.up);
                route.Add(fromPos);
                // 카메라에서 플레이어 눈높이로 먼저 들어간 뒤 바닥 경로를 따른다(지금 화면이 보던 구간이라 벽 검사를 하지 않는다).
                AddPoint(feet + Vector3.up * EyeHeight);
                foreach (var corner in path.corners) AddPoint(corner + Vector3.up * EyeHeight);
                AddPoint(toPos);
                routeLength = 0f;
                for (int i = 1; i < route.Count; i++)
                {
                    if (i > 1 && !ClearSegment(route[i - 1], route[i])) { RouteFailure = $"경로 구간 {i}: {route[i - 1]} → {route[i]}"; valid = false; break; }
                    routeLength += Vector3.Distance(route[i - 1], route[i]);
                }
            }
            ReleaseNavMesh(); preparing = null;
            if (!valid || route.Count < 2)
            {
                if (string.IsNullOrEmpty(RouteFailure)) RouteFailure = $"바닥 경로 없음: {start} → {desired}, {path?.status.ToString() ?? "후보 없음"}";
                Debug.LogWarning("[TutorialCamera] 통행 가능한 카메라 경로가 없어 이동 연출을 생략합니다. " + RouteFailure);
                Stop(); yield break;
            }
            moveIn = Mathf.Max(MoveIn, routeLength / 14f);
            // 같은 경로를 역재생하되 갈 때보다 정확히 3배 빠르게 복귀한다.
            moveOut = Mathf.Max(.2f, moveIn / 3f);
            startedAt = Time.unscaledTime; routeReady = true;
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
                if (!NavMesh.SamplePosition(candidate, out var sampled, 2.5f, NavMesh.AllAreas)) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(origin, sampled.position, NavMesh.AllAreas, path)
                    || path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2) continue;
                float score = (sampled.position - desired).sqrMagnitude;
                for (int i = 1; i < path.corners.Length; i++) score += Vector3.Distance(path.corners[i - 1], path.corners[i]) * .05f;
                if (score >= bestScore) continue;
                bestScore = score; goal = sampled; bestPath = path;
            }
            return bestPath != null;
        }

        void AddPoint(Vector3 point)
        {
            if (route.Count == 0 || Vector3.Distance(route[route.Count - 1], point) > .03f) route.Add(point);
        }

        static bool ClearSegment(Vector3 a, Vector3 b)
        {
            var delta = b - a;
            if (delta.sqrMagnitude < .0001f) return true;
            foreach (var hit in Physics.SphereCastAll(a, Clearance, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider != null && hit.collider.GetComponentInParent<NetworkPlayer>() == null) return false;
            return true;
        }

        Vector3 PointAt(float fraction, out Vector3 tangent)
        {
            float distance = Mathf.Clamp01(fraction) * routeLength;
            for (int i = 1; i < route.Count; i++)
            {
                var delta = route[i] - route[i - 1];
                float length = delta.magnitude;
                if (distance <= length || i == route.Count - 1)
                { tangent = delta; return Vector3.Lerp(route[i - 1], route[i], length > 0f ? distance / length : 0f); }
                distance -= length;
            }
            tangent = toPos - fromPos; return fromPos;
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
                bool returning = t >= moveIn + Hold;
                float k = t < moveIn ? Mathf.SmoothStep(0f, 1f, t / moveIn)
                    : !returning ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (t - moveIn - Hold) / moveOut);
                var position = PointAt(k, out var tangent);
                // 첫 구간(카메라 → 플레이어 눈높이)은 원래 화면이 보던 구간이라 막힘 검사를 하지 않는다.
                bool firstLeg = route.Count > 1 && k * routeLength <= Vector3.Distance(route[0], route[1]) + .05f;
                if (!firstLeg && !ClearSegment(view.transform.position, position)) { Stop(); return; }
                Quaternion rotation = toRot;
                if (t < moveIn || returning)
                {
                    if (returning) tangent = -tangent;
                    tangent.y = 0f;
                    rotation = tangent.sqrMagnitude > .01f ? Quaternion.LookRotation(tangent) : (returning ? toRot : fromRot);
                    rotation = Quaternion.Slerp(rotation, returning ? fromRot : toRot,
                        Mathf.SmoothStep(0f, 1f, returning ? (1f - k) * 5f - 4f : k * 5f - 4f));
                }
                view.transform.SetPositionAndRotation(position, Quaternion.Slerp(view.transform.rotation, rotation, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime)));
                if (t >= moveIn + Hold + moveOut)
                {
                    RouteCompleted = true; CompletedRouteCount++; LastCompletedFocus = focusPoint; Stop();
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
