using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Tightrope.Fire
{
    /// <summary>충돌체 없는 화재 표시. 추격 불은 연결된 파티클 불벽, 구간 발화는 임시 상자(확정 빨강·예고 노랑)다.</summary>
    [RequireComponent(typeof(TightropeFire))]
    public sealed class FireView : MonoBehaviour
    {
        /// <summary>비우면 임시 빨간 상자를 쓴다. 불벽은 너비 1m 기준으로 만들고 X 배율로 코스 폭을 맞춘다.</summary>
        [SerializeField] Transform chaseEffect;
        TightropeFire fire;
        TightropeCourse course;
        Transform chase;
        readonly List<Transform[]> segments = new();
        readonly List<Vector2> segmentSpans = new(FireCourseSpans.MaximumPieces);
        Material flame, warning;
        FireSettings shown;
        int shownRound = -1;
        float[] burnVisibleUntil = System.Array.Empty<float>();

        void Awake()
        {
            // 화재 판정은 TightropeFire가 계속 처리하며, 숨김 봇은 표시만 생략한다.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { enabled = false; return; }
            fire = GetComponent<TightropeFire>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            flame = new Material(shader) { color = new Color(1f, 0.15f, 0.03f) };
            warning = new Material(shader) { color = new Color(1f, 0.8f, 0.02f) };
            chase = chaseEffect != null ? chaseEffect : Box("ChaseFire");
        }

        void Update()
        {
            course = TightropeCourse.Current;
            var settings = fire.Settings;
            bool available = settings != null && course != null && fire.IsPerformanceActive;
            if (!available) { Hide(); return; }
            if (shown != settings || shownRound != fire.Round) Rebuild(settings);
            chase.gameObject.SetActive(fire.IsChasing);
            if (fire.IsChasing)
            {
                float width = (course.LaneCount - 1) * course.LaneSpacing + course.Shape.SideMargin * 2f;
                bool effect = chaseEffect != null;
                chase.position = course.transform.position + course.Forward * fire.ChaseDistance + Vector3.up * (effect ? 0f : 0.8f);
                chase.rotation = Quaternion.LookRotation(course.Forward);
                chase.localScale = effect ? new Vector3(width, 1f, 1f) : new Vector3(width, 1.6f, 0.25f);
            }
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = settings.segments[i];
                int lane = segment.lane - 1;
                bool burning = fire.IsSegmentActive(i);
                bool pending = fire.WarningRemaining(i) >= 0f;
                if (burning && burnVisibleUntil[i] < 0f) burnVisibleUntil[i] = Time.time + 2f;
                bool showingFlame = burning && Time.time < burnVisibleUntil[i];
                bool visible = lane >= 0 && lane < course.LaneCount && segment.to > segment.from && (showingFlame || pending);
                fire.GetSegmentSpans(i, segmentSpans);
                for (int piece = 0; piece < segments[i].Length; piece++)
                {
                    var view = segments[i][piece];
                    bool showPiece = visible && piece < segmentSpans.Count;
                    view.gameObject.SetActive(showPiece);
                    if (!showPiece) continue;
                    var span = segmentSpans[piece];
                    float height = burning ? 0.6f : 0.06f;
                    view.position = course.GetLanePosition(lane, (span.x + span.y) * 0.5f) + Vector3.up * (height * 0.5f + 0.02f);
                    view.rotation = Quaternion.LookRotation(course.Forward);
                    view.localScale = new Vector3(course.Shape.RopeWalkWidth, height, span.y - span.x);
                    view.GetComponent<Renderer>().sharedMaterial = burning ? flame : warning;
                }
            }
        }

        void Rebuild(FireSettings settings)
        {
            foreach (var pieces in segments) foreach (var piece in pieces) Destroy(piece.gameObject);
            segments.Clear();
            for (int i = 0; i < settings.segments.Length; i++)
            {
                var pieces = new Transform[FireCourseSpans.MaximumPieces];
                for (int piece = 0; piece < pieces.Length; piece++) pieces[piece] = Box($"SegmentFire{i + 1}Part{piece + 1}");
                segments.Add(pieces);
            }
            burnVisibleUntil = new float[settings.segments.Length];
            System.Array.Fill(burnVisibleUntil, -1f);
            shown = settings;
            shownRound = fire.Round;
        }

        Transform Box(string label)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = label;
            box.transform.SetParent(transform, false);
            var collider = box.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            box.GetComponent<Renderer>().sharedMaterial = flame;
            box.SetActive(false);
            return box.transform;
        }

        void Hide()
        {
            if (chase != null) chase.gameObject.SetActive(false);
            foreach (var pieces in segments) foreach (var piece in pieces) piece.gameObject.SetActive(false);
        }

        void OnDisable() => Hide();

        void OnDestroy()
        {
            if (flame != null) Destroy(flame);
            if (warning != null) Destroy(warning);
        }
    }
}
