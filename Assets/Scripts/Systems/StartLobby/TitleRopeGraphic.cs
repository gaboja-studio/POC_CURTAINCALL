using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.StartLobby
{
    /// <summary>Resolution-independent braided theatre rope with bindings and brass eyelets.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TitleRopeGraphic : MaskableGraphic
    {
        [SerializeField] Vector2 leftTop = new(-380, 300), leftEnd = new(-380, 100);
        [SerializeField] Vector2 rightTop = new(380, 300), rightEnd = new(380, 100);
        float breeze;

        public void SetEndpoints(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float sway)
        {
            leftTop = a; leftEnd = b; rightTop = c; rightEnd = d; breeze = sway;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rope(vh, leftTop, leftEnd, 0f);
            Rope(vh, rightTop, rightEnd, 1.7f);
        }

        void Rope(VertexHelper vh, Vector2 top, Vector2 end, float phase)
        {
            Vector2 down = (end - top).normalized;
            Vector2 across = new(-down.y, down.x);
            const int segments = 80;
            float length = Vector2.Distance(top, end);
            for (int strand = -1; strand <= 1; strand++)
            {
                Vector2 previous = top;
                for (int i = 1; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float twist = t * length * .19f + strand * Mathf.PI * 2f / 3f;
                    Vector2 point = Vector2.Lerp(top, end, t) + across *
                        (Mathf.Sin(twist) * 2.5f + Mathf.Sin(t * Mathf.PI) * Mathf.Sin(breeze + phase) * 1.5f);
                    float light = .5f + .5f * Mathf.Cos(twist);
                    Line(vh, previous + new Vector2(2, -1), point + new Vector2(2, -1), 4.8f, new Color32(23, 13, 8, 210));
                    Line(vh, previous, point, 3.8f, Color.Lerp(new Color32(87, 58, 30, 255), new Color32(211, 180, 117, 255), light));
                    Line(vh, previous - across * .65f, point - across * .65f, .7f, new Color32(242, 216, 156, 150));
                    previous = point;
                }
            }
            // Tight whipping binds the fibres just above the metal attachment.
            for (int wrap = 0; wrap < 6; wrap++)
            {
                Vector2 at = end - down * (11 + wrap * 2.3f);
                Line(vh, at - across * 5, at + across * 5, 2.5f, new Color32(110, 75, 37, 255));
                Line(vh, at - across * 4, at + across * 4, .8f, new Color32(224, 188, 116, 255));
            }
            for (int i = 0; i < 28; i++)
            {
                float a = i * 2f * Mathf.PI / 28, b = (i + 1) * 2f * Mathf.PI / 28;
                Vector2 center = end + down * 4;
                Vector2 p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 9;
                Vector2 q = center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 9;
                Line(vh, p + Vector2.down * 1.5f, q + Vector2.down * 1.5f, 5, new Color32(48, 29, 14, 255));
                Line(vh, p, q, 3, Color.Lerp(new Color32(119, 76, 31, 255), new Color32(245, 215, 137, 255), .5f + .5f * Mathf.Sin(a)));
            }
        }

        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 d = (b - a).normalized;
            Vector2 n = new Vector2(-d.y, d.x) * width * .5f;
            int index = vh.currentVertCount;
            vh.AddVert(a - n, color, Vector2.zero);
            vh.AddVert(a + n, color, Vector2.up);
            vh.AddVert(b + n, color, Vector2.one);
            vh.AddVert(b - n, color, Vector2.right);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
