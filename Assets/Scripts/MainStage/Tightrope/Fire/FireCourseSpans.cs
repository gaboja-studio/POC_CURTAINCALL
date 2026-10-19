using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.Tightrope.Fire
{
    /// <summary>협동 관문의 접근·교대 공간을 뺀 소각 범위. 지지면·피격·불 표시가 같은 결과를 쓴다.</summary>
    public static class FireCourseSpans
    {
        public const float GateMargin = 8f;
        // 하나의 원래 구간에서 서로 다른 관문 두 곳을 빼면 최대 세 조각이다.
        public const int MaximumPieces = 3;

        public static void Get(FireSegment segment, TightropeCourse course, List<Vector2> result)
        {
            result.Clear();
            if (segment.to <= segment.from) return;
            result.Add(new Vector2(segment.from, segment.to));
            if (course == null) return;
            if (course.HasRelayGap) Subtract(result, course.SwingGapFrom - GateMargin, course.SwingGapTo + GateMargin);
            if (course.HasTrapezeGap) Subtract(result, course.TrapezeGapFrom - GateMargin, course.TrapezeGapTo + GateMargin);
        }

        static void Subtract(List<Vector2> spans, float from, float to)
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
    }
}
