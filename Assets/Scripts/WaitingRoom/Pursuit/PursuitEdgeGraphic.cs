using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>중앙은 완전히 비워 두는 부드러운 화면 테두리. 텍스처/후처리 볼륨을 변경하지 않는다.</summary>
    public sealed class PursuitEdgeGraphic : MaskableGraphic
    {
        float strength, width = .19f;
        public void SetThreat(float value, float edgeWidth)
        {
            if (Mathf.Abs(strength - value) < .002f && Mathf.Approximately(width, edgeWidth)) return;
            strength = value; width = edgeWidth; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (strength <= .001f) return;
            Rect r = GetPixelAdjustedRect();
            const int rings = 12, sides = 64;
            // Concentric rounded rectangles, with a squared alpha falloff towards the clear centre.
            for (int ring = 0; ring <= rings; ring++) {
                float t = ring / (float)rings;
                var tint = new Color(.8f, .018f, .012f, strength * (1 - t) * (1 - t));
                for (int i = 0; i <= sides; i++) {
                    float angle = i * Mathf.PI * 2 / sides;
                    float x = Mathf.Cos(angle), y = Mathf.Sin(angle);
                    float scale = 1 / Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                    vh.AddVert(new Vector3(r.center.x + x * scale * r.width * (.5f - t * width),
                        r.center.y + y * scale * r.height * (.5f - t * width)), tint, Vector2.zero);
                    if (ring == 0 || i == 0) continue;
                    int a = ring * (sides + 1) + i, b = a - (sides + 1);
                    vh.AddTriangle(a - 1, b - 1, b); vh.AddTriangle(a - 1, b, a);
                }
            }
        }
    }
}
