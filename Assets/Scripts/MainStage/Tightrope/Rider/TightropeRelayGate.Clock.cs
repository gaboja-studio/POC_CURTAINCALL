using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.Tightrope
{
    /// <summary>외줄 해제봉의 빨간 7세그먼트 전광판. 모든 공통 봉이 같은 표시를 사용한다.</summary>
    internal sealed class PoleCountdownView
    {
        readonly Transform root;
        readonly Renderer[] segments = new Renderer[28];
        int shown = -1;
        static Mesh boxMesh;
        static Material material;
        static MaterialPropertyBlock tint;

        // 세그먼트 비트: a=위, b=오른쪽 위, c=오른쪽 아래, d=아래, e=왼쪽 아래, f=왼쪽 위, g=가운데
        static readonly byte[] SegmentDigits = { 0b0111111, 0b0000110, 0b1011011, 0b1001111, 0b1100110, 0b1101101, 0b1111101, 0b0000111, 0b1111111, 0b1101111 };
        static readonly Color ClockLit = new(1f, .08f, .05f);
        static readonly Color ClockUnlit = new(.055f, .01f, .01f);
        static readonly Color ClockPanel = new(.015f, .015f, .02f);
        static readonly Color ClockFrame = new(.16f, .16f, .18f);
        const float DigitWidth = .16f, DigitHeight = .28f, SegmentThickness = .036f;
        // 매달림 케이블 높이(봉 위)와 전광판 테두리 절반 높이. 전광판 윗변을 케이블 꼭대기에 맞춘다.
        internal const float CableHeight = 1.6f, HalfHeight = .22f;
        static readonly float[] DigitX = { -.36f, -.16f, .16f, .36f };
        static readonly int[] DigitDivisors = { 1000, 100, 10, 1 };

        internal static PoleCountdownView Create(Transform parent)
        {
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if (shader == null) return null;
                material = new Material(shader) { hideFlags = HideFlags.DontSave };
                boxMesh = CreateBoxMesh();
                tint = new MaterialPropertyBlock();
            }
            return new PoleCountdownView(parent);
        }

        PoleCountdownView(Transform parent)
        {
            root = new GameObject("Hold countdown clock").transform;
            root.SetParent(parent, false);
            // 카메라를 향하는 면(-Z)이 앞면이다. 테두리 → 패널 → 세그먼트 순으로 앞에 놓는다.
            Panel(root, "Clock frame", new Vector3(1.04f, .44f, .05f), .03f, ClockFrame);
            Panel(root, "Clock panel", new Vector3(.98f, .38f, .05f), 0f, ClockPanel);
            for (int digit = 0; digit < DigitX.Length; digit++)
            for (int s = 0; s < 7; s++)
            {
                var seg = Box(root, "Clock segment");
                PlaceSegment(seg.transform, DigitX[digit], s);
                Tint(seg, ClockUnlit);
                segments[digit * 7 + s] = seg;
            }
            // 초와 소수 사이의 소수점. 숫자 아래선 높이에 둔다.
            var point = Box(root, "Clock decimal point");
            point.transform.localPosition = new Vector3(0f, -DigitHeight * .5f, -.03f);
            point.transform.localScale = new Vector3(SegmentThickness * 1.2f, SegmentThickness * 1.2f, .01f);
            Tint(point, ClockLit);
        }

        static void Panel(Transform root, string name, Vector3 size, float z, Color color)
        {
            var panel = Box(root, name);
            panel.transform.localPosition = new Vector3(0f, 0f, z);
            panel.transform.localScale = size;
            Tint(panel, color);
        }

        static Renderer Box(Transform parent, string name)
        {
            var box = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            box.transform.SetParent(parent, false);
            box.GetComponent<MeshFilter>().sharedMesh = boxMesh;
            var renderer = box.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        internal static Mesh CreateBoxMesh()
        {
            var mesh = new Mesh { name = "Pole display box", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f) };
            mesh.triangles = new[] { 0,2,1,0,3,2, 4,5,6,4,6,7, 0,4,7,0,7,3, 1,2,6,1,6,5, 3,7,6,3,6,2, 0,1,5,0,5,4 };
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Tint(Renderer renderer, Color color)
        {
            tint.SetColor("_BaseColor", color);
            tint.SetColor("_Color", color);
            renderer.SetPropertyBlock(tint);
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
        internal void Show(float remaining, Vector3 position, Quaternion facing)
        {
            if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
            root.SetPositionAndRotation(position, facing);
            // 1/100초 단위(예: 10.00). 유지 시간을 100초 이상으로 늘리면 99.99부터 보인다.
            int hundredths = Mathf.Clamp(Mathf.CeilToInt(remaining * 100f), 0, 9999);
            if (hundredths == shown) return;
            shown = hundredths;
            for (int digit = 0; digit < DigitDivisors.Length; digit++)
            for (int s = 0; s < 7; s++)
                Tint(segments[digit * 7 + s], (SegmentDigits[hundredths / DigitDivisors[digit] % 10] & (1 << s)) != 0 ? ClockLit : ClockUnlit);
        }

        internal void Hide() { if (root.gameObject.activeSelf) root.gameObject.SetActive(false); }
    }
}
