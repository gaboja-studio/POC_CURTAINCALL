using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeCourse
    {
        readonly Dictionary<int, Transform> laneGeometry = new();
        readonly Dictionary<Color, Material> courseMaterials = new();
        readonly List<Transform> sectionSigns = new();
        [Tooltip("코스 오른쪽에 'N구간 · 이름 / 시작 거리' 표지판을 띄운다. 꺼도 표지판은 만들어 두고 숨기기만 한다.")]
        [SerializeField] bool showSectionSigns;

        Material CourseMaterial(Material source, Color color)
        {
            if (courseMaterials.TryGetValue(color, out var material)) return material;
            material = new Material(source) { name = "Tightrope course (runtime)", color = color,
                hideFlags = HideFlags.HideAndDontSave };
            courseMaterials.Add(color, material);
            return material;
        }

        /// <summary>막힌 구간을 제외한 줄의 실제 지지 범위. 구간 경계는 물리 경계를 만들지 않는다.</summary>
        public List<Vector2> GetUsableSpans(int lane)
        {
            var spans = new List<Vector2>();
            if (lane < 0 || lane >= LaneCount || IsLaneBlocked(lane)) return spans;
            float end = Layout.GetLaneEnd(lane);
            if (end <= 0f) return spans;
            spans.Add(new Vector2(0f, end));
            if (HasSwingGapOnLane(lane)) SubtractSpan(spans, SwingGapFrom, SwingGapTo);
            if (IsRelayGapClosed(lane)) SubtractSpan(spans, SwingGapFrom, SwingGapTo);
            if (IsTrapezeGapOnLane(lane)) SubtractSpan(spans, TrapezeGapFrom, TrapezeGapTo);
            foreach (var block in blockedSegments)
            {
                if (block.lane != lane) continue;
                SubtractSpan(spans, block.from, block.to);
            }
            return spans;
        }

        static void SubtractSpan(List<Vector2> spans, float from, float to)
        {
            for (int i = spans.Count - 1; i >= 0; i--)
            {
                var span = spans[i];
                if (to <= span.x || from >= span.y) continue;
                spans.RemoveAt(i);
                if (to < span.y) spans.Insert(i, new Vector2(Mathf.Max(span.x, to), span.y));
                if (from > span.x) spans.Insert(i, new Vector2(span.x, Mathf.Min(span.y, from)));
            }
        }

        void RefreshLaneGeometry(int lane)
        {
            if (!Application.isPlaying || generated == null || lane < 0 || lane >= LaneCount) return;
            if (laneGeometry.TryGetValue(lane, out var old) && old != null)
            {
                foreach (var collider in old.GetComponentsInChildren<Collider>()) ropeSurfaces.Remove(collider);
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
            var root = new GameObject($"Rope{lane}").transform;
            root.SetParent(generated, false);
            laneGeometry[lane] = root;
            var shape = Shape;
            foreach (var span in GetUsableSpans(lane))
            {
                var support = new GameObject("ContinuousSupport");
                support.transform.SetParent(root, false);
                support.transform.localPosition = new Vector3(Layout.GetLaneSide(lane), -shape.RopeThickness * .5f, (span.x + span.y) * .5f);
                var collider = support.AddComponent<BoxCollider>();
                collider.size = new Vector3(shape.RopeWalkWidth, shape.RopeThickness, span.y - span.x);
                ropeSurfaces.Add(collider, lane);

                float start = 0f;
                var sections = shape.Sections;
                if (sections != null)
                    for (int i = 0; i < sections.Length && start < span.y; i++)
                    {
                        float end = Mathf.Clamp(sections[i].endFraction * FinishDistance, start, FinishDistance);
                        AddRopeSpan(root, lane, Mathf.Max(start, span.x), Mathf.Min(end, span.y), i);
                        start = end;
                    }
                if (start < span.y) AddRopeSpan(root, lane, Mathf.Max(start, span.x), span.y, -1);
            }
        }

        void AddRopeSpan(Transform parent, int lane, float from, float to, int section)
        {
            if (to - from <= .001f) return;
            // Split only the visible mesh at the relay boundary; support stays continuous.
            if (HasRelayGap && from < SwingGapFrom && to > SwingGapFrom)
            {
                AddRopeSpan(parent, lane, from, SwingGapFrom, section);
                AddRopeSpan(parent, lane, SwingGapFrom, to, section);
                return;
            }
            if (HasRelayGap && from < SwingGapTo && to > SwingGapTo)
            {
                AddRopeSpan(parent, lane, from, SwingGapTo, section);
                AddRopeSpan(parent, lane, SwingGapTo, to, section);
                return;
            }
            var shape = Shape;
            bool relay = HasRelayGap && from >= SwingGapFrom && to <= SwingGapTo;
            var piece = CreateBox(relay ? $"RelayRope{section + 1}" : $"Section{section + 1}",
                new Vector3(Layout.GetLaneSide(lane), -shape.RopeThickness * .5f, (from + to) * .5f),
                new Vector3(shape.RopeThickness, shape.RopeThickness, to - from), ropeColor, false);
            piece.transform.SetParent(parent, false);
        }

        public int GetSectionIndex(float distance)
        {
            var sections = Shape.Sections;
            if (sections == null || sections.Length == 0) return -1;
            for (int i = 0; i < sections.Length; i++)
                if (distance < sections[i].endFraction * FinishDistance) return i;
            return sections.Length - 1;
        }

        void BuildSectionSigns()
        {
            sectionSigns.Clear();
            var sections = Shape.Sections;
            if (sections == null) return;
            float start = 0f;
            float side = (LaneCount - 1) * LaneSpacing * .5f + 1.5f;
            for (int i = 0; i < sections.Length; i++)
            {
                var sign = new GameObject($"SectionSign{i + 1}");
                sign.transform.SetParent(generated, false);
                sign.transform.localPosition = new Vector3(side, 1.1f, start);
                var text = sign.AddComponent<TextMesh>();
                text.text = $"{i + 1}구간 · {sections[i].label}\n{start:0}m";
                text.fontSize = 64;
                text.characterSize = .035f;
                text.anchor = TextAnchor.MiddleCenter;
                text.color = new Color(.9f, .9f, .9f);
                sign.SetActive(showSectionSigns);
                sectionSigns.Add(sign.transform);
                start = Mathf.Clamp(sections[i].endFraction * FinishDistance, start, FinishDistance);
            }
        }

        void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null) return;
            foreach (var sign in sectionSigns) if (sign != null) sign.rotation = camera.transform.rotation;
        }

        void OnDestroy()
        {
            foreach (var material in courseMaterials.Values)
                if (material != null) Destroy(material);
            courseMaterials.Clear();
        }
    }
}
