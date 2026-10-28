using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// 기존 HUD 테두리 위에 겹치는 둥근 테두리. 부모 영역을 가득 채우고 바깥선에서 안쪽으로 두께만큼 칠한다.
    /// 외부 HUD 스프라이트는 선 두께를 바꿀 수 없어 실행 중 9분할 스프라이트를 만든다. 생성한 텍스처·스프라이트는 Dispose로 해제한다.
    /// </summary>
    public sealed class HudRing
    {
        const int Size = 32, Slice = 12;

        public Image Image { get; }
        readonly Texture2D texture;
        readonly Sprite sprite;

        public HudRing(Transform parent, string name, float thickness, float radius)
        {
            texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name + " (runtime)", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
            };
            float half = Size * 0.5f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // 둥근 사각형 거리(안쪽 음수). 바깥선에서 안쪽으로 thickness만큼 칠하고 가장자리는 1px로 부드럽게.
                var q = new Vector2(Mathf.Abs(x + 0.5f - half), Mathf.Abs(y + 0.5f - half)) - new Vector2(half - radius, half - radius);
                float d = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                float a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + thickness + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(Slice, Slice, Slice, Slice));
            sprite.hideFlags = HideFlags.HideAndDontSave;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image = go.AddComponent<Image>();
            Image.sprite = sprite;
            Image.type = Image.Type.Sliced;
            Image.raycastTarget = false;
            // 스프라이트 1px이 UI 1단위가 되게 맞춰 두께를 px 그대로 쓴다.
            var canvas = parent.GetComponentInParent<Canvas>();
            Image.pixelsPerUnitMultiplier = canvas != null ? canvas.rootCanvas.referencePixelsPerUnit / 100f : 1f;
        }

        public void Dispose()
        {
            if (Image != null) Object.Destroy(Image.gameObject);
            if (sprite != null) Object.Destroy(sprite);
            if (texture != null) Object.Destroy(texture);
        }
    }
}
