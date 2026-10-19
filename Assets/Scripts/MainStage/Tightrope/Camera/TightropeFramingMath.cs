using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Cameras
{
    /// <summary>고정 3인칭의 방향을 유지하면서 가까운 협동 대상이 들어올 만큼만 구도를 넓힌다.</summary>
    public static class TightropeFramingMath
    {
        public readonly struct Frame
        {
            public readonly float Height, Distance, LookHeight, LookAhead;

            public Frame(float height, float distance, float lookHeight, float lookAhead)
            {
                Height = height;
                Distance = distance;
                LookHeight = lookHeight;
                LookAhead = lookAhead;
            }
        }

        public readonly struct LayerSubject
        {
            public readonly ulong Id;
            public readonly int Layer;
            public readonly Vector3 Feet;
            public LayerSubject(ulong id, int layer, Vector3 feet) { Id = id; Layer = layer; Feet = feet; }
        }

        /// <summary>
        /// 같은 층에서 서로 반경 안에 있는 작은 그룹을 고른다. 로컬 플레이어가 누구인지와
        /// 수신 목록 순서에 의존하지 않으며, 이웃을 연쇄로 이어 먼 사람까지 묶지 않는다.
        /// 입력에는 활성 상태의 살아 있는 플레이어만 넣는다. 단독이면 자신의 위치를 반환한다.
        /// </summary>
        public static bool SharedLayerGroup(IReadOnlyList<LayerSubject> subjects, ulong focusId, float radius,
            List<ulong> members, out Vector3 center)
        {
            members.Clear();
            center = Vector3.zero;
            var sorted = new List<LayerSubject>(subjects.Count);
            for (int i = 0; i < subjects.Count; i++) sorted.Add(subjects[i]);
            sorted.Sort((a, b) => a.Id.CompareTo(b.Id));
            var groups = new List<List<LayerSubject>>();
            foreach (var subject in sorted)
            {
                List<LayerSubject> selected = null;
                foreach (var group in groups)
                {
                    if (group[0].Layer != subject.Layer) continue;
                    bool nearby = true;
                    foreach (var member in group)
                        if (!IsNearby(member.Feet, subject.Feet, radius, false)) { nearby = false; break; }
                    if (nearby) { selected = group; break; }
                }
                if (selected == null) { selected = new List<LayerSubject>(); groups.Add(selected); }
                selected.Add(subject);
            }
            foreach (var group in groups)
            {
                bool containsFocus = false;
                foreach (var subject in group) containsFocus |= subject.Id == focusId;
                if (!containsFocus) continue;
                foreach (var subject in group) { members.Add(subject.Id); center += subject.Feet; }
                center /= group.Count;
                return group.Count > 1;
            }
            return false;
        }

        /// <summary>공용 고정 시점과 동일한 월드 카메라 위치·캐릭터 로컬 바라보기 오프셋 의미.</summary>
        public static Pose CameraPose(Vector3 anchor, Quaternion targetRotation, Vector3 courseForward,
            Frame frame, out Vector3 lookTarget)
        {
            courseForward.y = 0f;
            courseForward = courseForward.sqrMagnitude > 0.0001f ? courseForward.normalized : Vector3.forward;
            Vector3 position = anchor + Vector3.up * frame.Height - courseForward * frame.Distance;
            lookTarget = anchor + targetRotation * new Vector3(0f, frame.LookHeight, frame.LookAhead);
            Vector3 forward = lookTarget - position;
            return new Pose(position, Quaternion.LookRotation(
                forward.sqrMagnitude > 0.0001f ? forward : courseForward, Vector3.up));
        }

        public static bool IsNearby(Vector3 origin, Vector3 subject, float radius, bool sameStack)
        {
            if (sameStack) return true;
            Vector3 offset = subject - origin;
            radius = Mathf.Max(0f, radius);
            return Mathf.Abs(offset.y) <= radius && offset.x * offset.x + offset.z * offset.z <= radius * radius;
        }

        public static Frame Fit(Bounds subjects, Vector3 feet, Quaternion targetRotation, Vector3 courseForward,
            Frame baseline, float verticalFov, float aspect, float maximumDistance, float padding, float extraLookAhead)
        {
            Vector3 center = Quaternion.Inverse(targetRotation) * (subjects.center - feet);
            float lookHeight = subjects.min.y < feet.y - 0.1f ? center.y : Mathf.Max(baseline.LookHeight, center.y);
            float lookAhead = Mathf.Clamp(center.z, baseline.LookAhead, baseline.LookAhead + Mathf.Max(0f, extraLookAhead));
            float minimum = Mathf.Max(0.5f, baseline.Distance);
            float maximum = Mathf.Max(minimum, maximumDistance);
            subjects.Expand(Mathf.Max(0f, padding) * 2f);
            var nearest = AtDistance(minimum);
            if (Fits(subjects, feet, targetRotation, courseForward, nearest, verticalFov, aspect)) return nearest;

            float low = minimum, high = maximum;
            for (int i = 0; i < 12; i++)
            {
                float mid = (low + high) * 0.5f;
                if (Fits(subjects, feet, targetRotation, courseForward, AtDistance(mid), verticalFov, aspect)) high = mid;
                else low = mid;
            }
            return AtDistance(high);

            Frame AtDistance(float distance)
            {
                // 멀어질 때도 원래 내려다보는 각도를 유지한다.
                float slope = Mathf.Max(0f, baseline.Height - baseline.LookHeight)
                    / Mathf.Max(0.5f, baseline.Distance + baseline.LookAhead);
                return new Frame(baseline.Height + (lookHeight - baseline.LookHeight)
                    + (distance - baseline.Distance) * slope, distance, lookHeight, lookAhead);
            }
        }

        /// <summary>프레임의 세로·가로 시야각 양쪽으로 경계의 모든 꼭짓점이 들어오는지.</summary>
        public static bool Fits(Bounds subjects, Vector3 feet, Quaternion targetRotation, Vector3 courseForward,
            Frame frame, float verticalFov, float aspect)
        {
            var pose = CameraPose(feet, targetRotation, courseForward, frame, out _);
            Vector3 forward = pose.rotation * Vector3.forward;
            Vector3 right = pose.rotation * Vector3.right;
            Vector3 up = pose.rotation * Vector3.up;
            float vertical = Mathf.Tan(Mathf.Clamp(verticalFov, 1f, 179f) * Mathf.Deg2Rad * 0.5f);
            float horizontal = vertical * Mathf.Max(0.01f, aspect);
            for (int i = 0; i < 8; i++)
            {
                Vector3 sign = new((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                Vector3 offset = subjects.center + Vector3.Scale(subjects.extents, sign) - pose.position;
                float depth = Vector3.Dot(offset, forward);
                if (depth <= 0.01f || Mathf.Abs(Vector3.Dot(offset, right)) > depth * horizontal
                    || Mathf.Abs(Vector3.Dot(offset, up)) > depth * vertical) return false;
            }
            return true;
        }

        public static Frame Smooth(Frame current, Frame target, float smoothTime, float deltaTime)
        {
            float blend = smoothTime <= 0f ? 1f : 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / smoothTime);
            return new Frame(Mathf.Lerp(current.Height, target.Height, blend), Mathf.Lerp(current.Distance, target.Distance, blend),
                Mathf.Lerp(current.LookHeight, target.LookHeight, blend), Mathf.Lerp(current.LookAhead, target.LookAhead, blend));
        }
    }
}
