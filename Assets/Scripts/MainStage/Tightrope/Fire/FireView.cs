using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Tightrope.Fire
{
    /// <summary>충돌체 없는 임시 화재 표시. 호스트의 확정 발화는 빨강, 발화 예고는 노랑이다.</summary>
    [RequireComponent(typeof(TightropeFire))]
    public sealed class FireView : MonoBehaviour
    {
        TightropeFire fire;
        TightropeCourse course;
        Transform chase;
        readonly List<Transform> segments = new();
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
            chase = Box("ChaseFire");
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
                chase.position = course.transform.position + course.Forward * fire.ChaseDistance + Vector3.up * 0.8f;
                chase.rotation = Quaternion.LookRotation(course.Forward);
                chase.localScale = new Vector3((course.LaneCount - 1) * course.LaneSpacing + course.Shape.SideMargin * 2f, 1.6f, 0.25f);
            }
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = settings.segments[i];
                int lane = segment.lane - 1;
                bool burning = fire.IsSegmentActive(i);
                bool pending = fire.WarningRemaining(i) >= 0f;
                if (burning && burnVisibleUntil[i] < 0f) burnVisibleUntil[i] = Time.time + 2f;
                bool showingFlame = burning && Time.time < burnVisibleUntil[i];
                var view = segments[i];
                bool visible = lane >= 0 && lane < course.LaneCount && segment.to > segment.from && (showingFlame || pending);
                view.gameObject.SetActive(visible);
                if (!visible) continue;
                float height = burning ? 0.6f : 0.06f;
                view.position = course.GetLanePosition(lane, (segment.from + segment.to) * 0.5f) + Vector3.up * (height * 0.5f + 0.02f);
                view.rotation = Quaternion.LookRotation(course.Forward);
                view.localScale = new Vector3(course.Shape.RopeWalkWidth, height, segment.to - segment.from);
                view.GetComponent<Renderer>().sharedMaterial = burning ? flame : warning;
            }
        }

        void Rebuild(FireSettings settings)
        {
            foreach (var segment in segments) Destroy(segment.gameObject);
            segments.Clear();
            for (int i = 0; i < settings.segments.Length; i++) segments.Add(Box($"SegmentFire{i + 1}"));
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
            foreach (var segment in segments) segment.gameObject.SetActive(false);
        }

        void OnDisable() => Hide();

        void OnDestroy()
        {
            if (flame != null) Destroy(flame);
            if (warning != null) Destroy(warning);
        }
    }
}
