using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Tightrope;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 구조도대로 놓는 리허설 장치(Task-097): 줄별 끊긴 구간·트리거 그네·중간 발판·인간 그네 자리.
    /// 그네와 트리거 그네는 공연 외줄 장치(<see cref="TightropeSwingPole"/>·<see cref="TightropeRelayGate"/>)를 그대로 쓰고 위치만 지정한다.
    /// 조작(F 잡기·매달리기·F/Space 놓기)은 공연과 같다. 트리거 그네는 시간 제한이 없다(사용자 결정 2026-11-02).
    /// 끊긴 구간은 <see cref="TightropeCourse.BlockSegment"/>로 줄과 발판을 빼고, 해제봉이 그 줄을 잇는 동안만 다시 잇는다.
    /// </summary>
    public sealed partial class WaitingRoomRehearsal
    {
        readonly List<GameObject> stageProps = new();
        readonly List<GameObject> gapDashes = new();
        readonly List<Material> propMaterials = new();
        Material dashMaterial, platformMaterial, supportMaterial;

        static TightropeRelayGate Gate => TightropeRelayGate.Current;

        /// <summary>내가 잡고 있는 트리거 그네(구조도 순서 = 해제봉 번호). 없으면 -1.</summary>
        static int HeldTriggerIndex(NetworkPlayer player) => Gate != null ? Gate.HeldPole(player) : -1;

        static bool IsTriggerHeld(int index) => Gate != null && index < Gate.PoleCount && Gate.HolderAt(index) != ulong.MaxValue;

        bool IsGapConnected(int gap)
        {
            var s = Stage;
            return s != null && gap >= 0 && gap < s.Gaps.Length && Gate != null && Gate.IsLaneConnected(s.Gaps[gap].Lane);
        }

        /// <summary>구조도의 트리거 그네를 공연 해제봉 자리로 바꾼다.</summary>
        static RelaySpot[] RelaySpots(RehearsalStage s)
        {
            var spots = new RelaySpot[s.Triggers.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var trigger = s.Triggers[i];
                var gap = s.Gaps[trigger.Gap];
                spots[i] = new RelaySpot(trigger.Lane, trigger.Distance, gap.Lane, gap.From, gap.To);
            }
            return spots;
        }

        // ── 끊긴 구간(모든 화면) ──

        /// <summary>그네 자리와 닫힌 끊긴 구간을 줄에서 뺀다. 코스 재시작 등으로 풀리면 다시 뺀다.</summary>
        void ApplyGaps()
        {
            var s = Stage;
            if (course == null || s == null) return;
            bool dirty = false;
            foreach (var spot in s.Swings)
                if (!course.IsSegmentBlocked(spot.Lane, (spot.From + spot.To) * .5f)) dirty = true;
            for (int i = 0; i < s.Gaps.Length; i++)
            {
                var gap = s.Gaps[i];
                if (course.IsSegmentBlocked(gap.Lane, (gap.From + gap.To) * .5f) == IsGapConnected(i)) dirty = true;
            }
            if (!dirty) return;
            course.ClearBlockedSegments();
            foreach (var spot in s.Swings) course.BlockSegment(spot.Lane, spot.From, spot.To);
            for (int i = 0; i < s.Gaps.Length; i++)
                if (!IsGapConnected(i)) course.BlockSegment(s.Gaps[i].Lane, s.Gaps[i].From, s.Gaps[i].To);
        }

        // ── 장치 모양(모든 화면) ──

        void BuildStageProps(RehearsalStage s)
        {
            DestroyStageProps();
            if (course == null) return;
            EnsurePropMaterials();
            Vector3 at(int lane, float distance) => course.GetLanePosition(lane, distance);

            foreach (var platform in s.Platforms)
            {
                var top = at(platform.Lane, (platform.From + platform.To) * .5f);
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "RehearsalPlatform";
                box.transform.SetParent(courseRoot.transform, true);
                box.transform.SetPositionAndRotation(top + Vector3.down * .25f, Quaternion.LookRotation(course.Forward));
                box.transform.localScale = new Vector3(2.4f, .5f, platform.To - platform.From);
                box.GetComponent<Renderer>().sharedMaterial = platformMaterial;
                stageProps.Add(box);
                AddLegs(top + Vector3.down * .5f, course.Forward, 2.4f, platform.To - platform.From);
            }

            // 코스 출발대·도착대도 링 바닥까지 받친다(허공에 뜬 발판 금지, 2026-11-03 사용자).
            var shape = course.Shape;
            float width = (course.LaneCount - 1) * course.LaneSpacing + shape.SideMargin * 2f;
            float courseTop = course.transform.position.y;
            // 발판 윗면은 실제 충돌면을 재서 맞춘다(코스 값만으로 추정하지 않는다).
            Vector3 Along(float distance)
            {
                var p = new Vector3(course.transform.position.x, courseTop, course.transform.position.z) + course.Forward * distance;
                if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 4.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
                return p;
            }
            AddLegs(Along(-shape.StartPlatformLength * .5f) + Vector3.down * shape.PlatformThickness, course.Forward, width, shape.StartPlatformLength);
            AddLegs(Along(course.FinishDistance + shape.FinishPlatformLength * .5f) + Vector3.down * shape.PlatformThickness, course.Forward, width, shape.FinishPlatformLength);

            // 공연 장치 모양(그네 틀·해제봉 매다는 줄)은 줄 높이에서 끝난다. 리허설 코스에서만 링 바닥까지 받친다(2026-11-03 사용자).
            Vector3 right = Vector3.Cross(Vector3.up, course.Forward).normalized;
            foreach (var spot in s.Swings)
            {
                // 그네 틀 기둥: 줄 옆 ±1.0m(공연 그네 틀 폭 1.4m + 0.3m), 매다는 자리(끊김 시작 0.8m 앞).
                var anchor = at(spot.Lane, spot.From - SwingPoleMath.EntryOffset);
                foreach (float side in new[] { -1f, 1f })
                    AddPost(anchor + right * side * SwingFrameHalf, anchor.y);
            }
            var gate = courseRoot.GetComponent<TightropeRelayGate>();
            for (int i = 0; gate != null && i < gate.PoleCount && i < s.Triggers.Length; i++)
            {
                var pole = gate.PhysicalPole(i);
                if (pole == null) continue;
                // 해제봉 매다는 줄 위 끝(손잡이 + 1.6m)을 가로대로 잇고, 양 끝을 기둥으로 링 바닥까지 받친다.
                Vector3 top = pole.Grip.position + Vector3.up * RelayCableTop;
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = "RehearsalTriggerBeam";
                Destroy(bar.GetComponent<Collider>());
                bar.transform.SetParent(courseRoot.transform, true);
                bar.transform.SetPositionAndRotation(top, Quaternion.LookRotation(course.Forward));
                bar.transform.localScale = new Vector3(RelayFrameHalf * 2f + .2f, .14f, .14f);
                bar.GetComponent<Renderer>().sharedMaterial = supportMaterial;
                stageProps.Add(bar);
                foreach (float side in new[] { -1f, 1f })
                    AddPost(top + right * side * RelayFrameHalf, top.y);
            }

            // 구조도의 점선: 트리거 그네가 이어 주기 전까지 끊긴 구간을 줄 바로 아래 링 바닥에 그린다(허공에 띄우지 않는다).
            for (int i = 0; i < s.Gaps.Length; i++)
            {
                var gap = s.Gaps[i];
                var holder = new GameObject($"RehearsalGap_{i + 1}");
                holder.transform.SetParent(courseRoot.transform, false);
                stageProps.Add(holder);
                gapDashes.Add(holder);
                for (float d = gap.From + .25f; d < gap.To - .2f; d += .9f)
                {
                    var dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    dash.name = "Dash";
                    Destroy(dash.GetComponent<Collider>());
                    dash.transform.SetParent(holder.transform, false);
                    var above = at(gap.Lane, d + .25f);
                    dash.transform.position = new Vector3(above.x, ringCenter.y + .01f, above.z);
                    dash.transform.rotation = Quaternion.LookRotation(course.Forward);
                    dash.transform.localScale = new Vector3(.12f, .02f, .45f);
                    dash.GetComponent<Renderer>().sharedMaterial = dashMaterial;
                }
            }
        }

        /// <summary>발판 밑면 가운데(<paramref name="bottomCenter"/>)에서 링 바닥까지 네 모서리 받침 기둥.</summary>
        void AddLegs(Vector3 bottomCenter, Vector3 forward, float width, float length)
        {
            float floor = ringCenter.y;
            float height = bottomCenter.y - floor;
            if (height <= .05f) return;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            const float inset = .3f, size = .22f;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leg.name = "RehearsalSupport";
                    leg.transform.SetParent(courseRoot.transform, true);
                    Vector3 foot = bottomCenter + right * sx * (width * .5f - inset) + forward * sz * (length * .5f - inset);
                    leg.transform.SetPositionAndRotation(new Vector3(foot.x, floor + height * .5f, foot.z), Quaternion.LookRotation(forward));
                    leg.transform.localScale = new Vector3(size, height, size);
                    leg.GetComponent<Renderer>().sharedMaterial = supportMaterial;
                    stageProps.Add(leg);
                }
        }

        /// <summary>그네 틀 기둥이 줄 중심에서 떨어진 거리(m, 공연 그네 틀 폭 1.4m의 절반 + 0.3m).</summary>
        const float SwingFrameHalf = 1f;
        /// <summary>해제봉 매다는 줄 위 끝 높이(손잡이 기준, 공연 해제봉 표시 1.6m)와 받침 기둥 간격(줄 중심에서).</summary>
        const float RelayCableTop = 1.6f, RelayFrameHalf = 1f;

        /// <summary>링 바닥에서 <paramref name="topY"/>까지 서는 기둥(충돌 없음 — 줄 이동·그네 비행을 막지 않는다).</summary>
        void AddPost(Vector3 at, float topY)
        {
            float floor = ringCenter.y, height = topY - floor;
            if (height <= .05f) return;
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "RehearsalPost";
            Destroy(post.GetComponent<Collider>());
            post.transform.SetParent(courseRoot.transform, true);
            post.transform.SetPositionAndRotation(new Vector3(at.x, floor + height * .5f, at.z), Quaternion.LookRotation(course.Forward));
            post.transform.localScale = new Vector3(.16f, height, .16f);
            post.GetComponent<Renderer>().sharedMaterial = supportMaterial;
            stageProps.Add(post);
        }

        void TickStageProps()
        {
            var s = Stage;
            if (s == null) return;
            for (int i = 0; i < gapDashes.Count && i < s.Gaps.Length; i++)
                if (gapDashes[i] != null) gapDashes[i].SetActive(!IsGapConnected(i));
        }

        void DestroyStageProps()
        {
            foreach (var go in stageProps) if (go != null) Destroy(go);
            stageProps.Clear(); gapDashes.Clear();
        }

        void EnsurePropMaterials()
        {
            if (dashMaterial != null) return;
            dashMaterial = PropMaterial(new Color(.85f, .85f, .8f), true);
            platformMaterial = PropMaterial(new Color(.42f, .45f, .55f), false);
            supportMaterial = PropMaterial(new Color(.22f, .22f, .24f), false);
        }

        Material PropMaterial(Color color, bool unlit)
        {
            var shader = (unlit ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Universal Render Pipeline/Lit")) ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = color, hideFlags = HideFlags.HideAndDontSave };
            propMaterials.Add(material);
            return material;
        }
    }
}
