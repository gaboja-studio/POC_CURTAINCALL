using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// 화면 정중앙 상호작용 기준점(2026-11-16 상호작용 고도화). FPS 표시점처럼 작게 그린다.
    /// 모양·크기·색은 <see cref="InteractionFocusSettings"/>(Resources/Settings/Interaction)에서 바꾸며 플레이 중에도 바로 반영된다.
    /// 기준점은 범위를 거르지 않고, 같은 순위 대상 중 어느 쪽을 먼저 고를지만 정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionCrosshair : MonoBehaviour
    {
        const int TextureSize = 128;
        RectTransform root, dot, horizontal, vertical, ring;
        Image dotImage, horizontalImage, verticalImage, ringImage;
        Sprite disc, ringSprite;
        Image[] marks;
        float ringRatio = -1f;
        bool targeted, visible;

        /// <summary>캔버스 아래에 기준점을 만든다(이미 있으면 그대로 쓴다).</summary>
        public static InteractionCrosshair Ensure(Canvas canvas)
        {
            if (canvas == null) return null;
            var existing = canvas.GetComponentInChildren<InteractionCrosshair>(true);
            if (existing != null) return existing;
            var go = new GameObject("InteractionCrosshair", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsFirstSibling(); // 안내 문구·패널 아래에 그린다.
            var crosshair = go.AddComponent<InteractionCrosshair>();
            crosshair.Build();
            return crosshair;
        }

        void Build()
        {
            root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
            root.anchoredPosition = Vector2.zero; root.sizeDelta = Vector2.zero;
            disc = CreateDisc(0f);
            dot = Part("Dot", out dotImage); dotImage.sprite = disc;
            horizontal = Part("Horizontal", out horizontalImage);
            vertical = Part("Vertical", out verticalImage);
            ring = Part("Ring", out ringImage);
            marks = new[] { dotImage, horizontalImage, verticalImage, ringImage };
            Apply();
        }

        RectTransform Part(string name, out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            image = go.GetComponent<Image>();
            image.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, .55f);
            outline.effectDistance = new Vector2(1f, -1f);
            return rect;
        }

        /// <summary>F가 작동할 대상이 기준점에 잡혀 있는지(색 변경).</summary>
        public void SetTargeted(bool value) { if (targeted == value) return; targeted = value; Apply(); }

        public void SetVisible(bool value) { if (visible == value) return; visible = value; Apply(); }

        // 플레이 중 설정 수정이 바로 보이게 매 프레임 크기·색을 맞춘다(오브젝트 몇 개라 비용이 작다).
        void LateUpdate() => Apply();

        void Apply()
        {
            if (root == null) return;
            var settings = InteractionFocusSettings.Current;
            var look = settings.Crosshair;
            bool show = visible && look.visible;
            Color color = targeted ? look.targetColor : look.idleColor;
            float size = Mathf.Max(1f, look.size), thickness = Mathf.Clamp(look.thickness, 1f, size);
            SetActive(dot, show && look.shape == CrosshairShape.Dot);
            SetActive(horizontal, show && look.shape == CrosshairShape.Cross);
            SetActive(vertical, show && look.shape == CrosshairShape.Cross);
            SetActive(ring, show && look.shape == CrosshairShape.Ring);
            if (!show) return;
            dot.sizeDelta = new Vector2(size, size);
            horizontal.sizeDelta = new Vector2(size, thickness);
            vertical.sizeDelta = new Vector2(thickness, size);
            ring.sizeDelta = new Vector2(size, size);
            float ratio = Mathf.Clamp01(1f - 2f * thickness / size);
            if (!Mathf.Approximately(ringRatio, ratio))
            {
                if (ringSprite != null) DestroySprite(ringSprite);
                ringSprite = CreateDisc(ratio); ringRatio = ratio; ringImage.sprite = ringSprite;
            }
            foreach (var image in marks)
            {
                image.color = color;
                image.GetComponent<Outline>().enabled = look.shadow;
            }
        }

        static void SetActive(RectTransform part, bool value) { if (part.gameObject.activeSelf != value) part.gameObject.SetActive(value); }

        /// <summary>원형 스프라이트. inner가 0이면 꽉 찬 점, 0~1이면 그 비율만큼 비운 고리.</summary>
        static Sprite CreateDisc(float inner)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            { name = "Interaction crosshair (runtime)", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[TextureSize * TextureSize];
            float half = TextureSize * .5f, outer = half - 1f, hole = outer * inner;
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(half, half));
                    float a = Mathf.Clamp01(outer - d + .5f) * (inner > 0f ? Mathf.Clamp01(d - hole + .5f) : 1f);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(.5f, .5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static void DestroySprite(Sprite sprite)
        {
            if (sprite == null) return;
            var texture = sprite.texture;
            Destroy(sprite);
            if (texture != null) Destroy(texture);
        }

        void OnDestroy() { DestroySprite(disc); DestroySprite(ringSprite); }
    }
}
