using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>해제봉 케이블 꼭대기에 상시 걸린 전광판. 남은 유지 시간을 검은 패널의 빨간 7세그먼트 초·소수 둘째 자리(SS.ss, 예: 10.00)로 표시한다. 표시 전용이며 판정과 무관하다.</summary>
    public sealed partial class TightropeRelayGate
    {
        sealed class RelayClockView
        {
            public Transform Root;
            public Transform[] Segments; // 자리 4개 × 세그먼트 7개(a,b,c,d,e,f,g)
            public int Shown = -1;
        }

        // 세그먼트 비트: a=위, b=오른쪽 위, c=오른쪽 아래, d=아래, e=왼쪽 아래, f=왼쪽 위, g=가운데
        static readonly byte[] SegmentDigits = { 0b0111111, 0b0000110, 0b1011011, 0b1001111, 0b1100110, 0b1101101, 0b1111101, 0b0000111, 0b1111111, 0b1101111 };
        static readonly Color ClockLit = new(1f, .08f, .05f);
        static readonly Color ClockUnlit = new(.055f, .01f, .01f);
        static readonly Color ClockPanel = new(.015f, .015f, .02f);
        static readonly Color ClockFrame = new(.16f, .16f, .18f);
        const float DigitWidth = .16f, DigitHeight = .28f, SegmentThickness = .036f;
        // 매달림 케이블 높이(봉 위)와 전광판 테두리 절반 높이. 전광판 윗변을 케이블 꼭대기에 맞춘다.
        const float RelayCableHeight = 1.6f, ClockHalfHeight = .22f;
        static readonly float[] DigitX = { -.36f, -.16f, .16f, .36f };

        RelayClockView BuildRelayClock(Transform parent)
        {
            var root = new GameObject("Hold countdown clock").transform;
            root.SetParent(parent, false);
            // 카메라를 향하는 면(-Z)이 앞면이다. 테두리 → 패널 → 세그먼트 순으로 앞에 놓는다.
            Panel(root, "Clock frame", new Vector3(1.04f, .44f, .05f), .03f, ClockFrame);
            Panel(root, "Clock panel", new Vector3(.98f, .38f, .05f), 0f, ClockPanel);
            var segments = new Transform[DigitX.Length * 7];
            for (int digit = 0; digit < DigitX.Length; digit++)
            for (int s = 0; s < 7; s++)
            {
                var seg = RelayBox(root, "Clock segment");
                PlaceSegment(seg, DigitX[digit], s);
                TintRelay(seg, ClockUnlit);
                segments[digit * 7 + s] = seg;
            }
            // 초와 소수 사이의 소수점. 숫자 아래선 높이에 둔다.
            var point = RelayBox(root, "Clock decimal point");
            point.localPosition = new Vector3(0f, -DigitHeight * .5f, -.03f);
            point.localScale = new Vector3(SegmentThickness * 1.2f, SegmentThickness * 1.2f, .01f);
            TintRelay(point, ClockLit);
            root.gameObject.SetActive(false);
            return new RelayClockView { Root = root, Segments = segments };
        }

        void Panel(Transform root, string name, Vector3 size, float z, Color color)
        {
            var panel = RelayBox(root, name);
            panel.localPosition = new Vector3(0f, 0f, z);
            panel.localScale = size;
            TintRelay(panel, color);
        }

        static void PlaceSegment(Transform seg, float x, int index)
        {
            float w = DigitWidth, h = DigitHeight, t = SegmentThickness, gap = t * .6f;
            bool horizontal = index == 0 || index == 3 || index == 6;
            Vector2 at = index switch
            {
                0 => new Vector2(0f, h * .5f), 1 => new Vector2(w * .5f, h * .25f), 2 => new Vector2(w * .5f, -h * .25f),
                3 => new Vector2(0f, -h * .5f), 4 => new Vector2(-w * .5f, -h * .25f), 5 => new Vector2(-w * .5f, h * .25f),
                _ => Vector2.zero
            };
            seg.localPosition = new Vector3(x + at.x, at.y, -.03f);
            seg.localScale = horizontal ? new Vector3(w - gap, t, .01f) : new Vector3(t, h * .5f - gap, .01f);
        }

        /// <summary>앞면(-Z)이 다가오는 플레이어를 보도록 코스 진행 방향으로 고정한다.</summary>
        void ShowRelayClock(RelayClockView clock, float remaining, Vector3 position, Quaternion facing)
        {
            clock.Root.gameObject.SetActive(true);
            clock.Root.SetPositionAndRotation(position, facing);
            // 1/100초 단위(예: 10.00). 유지 시간을 100초 이상으로 늘리면 99.99부터 보인다.
            int hundredths = Mathf.Clamp(Mathf.CeilToInt(remaining * 100f), 0, 9999);
            if (hundredths == clock.Shown) return;
            clock.Shown = hundredths;
            int[] digits = { hundredths / 1000, hundredths / 100 % 10, hundredths / 10 % 10, hundredths % 10 };
            for (int digit = 0; digit < digits.Length; digit++)
            for (int s = 0; s < 7; s++)
                TintRelay(clock.Segments[digit * 7 + s], (SegmentDigits[digits[digit]] & (1 << s)) != 0 ? ClockLit : ClockUnlit);
        }
    }
}
