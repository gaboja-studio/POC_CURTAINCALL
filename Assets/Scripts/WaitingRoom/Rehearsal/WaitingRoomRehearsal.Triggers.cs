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
        Material dashMaterial, platformMaterial;

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
            }

            // 구조도의 점선: 트리거 그네가 이어 주기 전까지 끊긴 구간을 보여 준다.
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
                    dash.transform.position = at(gap.Lane, d + .25f) + Vector3.down * .03f;
                    dash.transform.localScale = new Vector3(.08f, .04f, .45f);
                    dash.GetComponent<Renderer>().sharedMaterial = dashMaterial;
                }
            }
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
