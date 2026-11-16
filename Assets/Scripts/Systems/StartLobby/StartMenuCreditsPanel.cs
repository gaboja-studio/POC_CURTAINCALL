using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CurtainCall.StartLobby
{
    /// <summary>Data-driven programme credits; all content lives in StartMenuCreditsSettings.</summary>
    public sealed class StartMenuCreditsPanel : MonoBehaviour
    {
        static readonly Color Ink = new Color32(16, 26, 28, 255);
        static readonly Color Paper = new Color32(245, 230, 196, 255);
        static readonly Color Gold = new Color32(184, 142, 72, 255);
        StartMenu owner;
        StartMenuCreditsSettings data;
        TMP_FontAsset font;
        bool ownsFont;
        RectTransform panel, content;
        ScrollRect scroll;
        TMP_Text heading;
        UnityEngine.UI.Button menuButton, close, back;
        GameObject previousSelection;
        bool showingLicense;

        public static StartMenuCreditsPanel Create(StartMenu owner, StartMenuCreditsSettings data)
        {
            var root = Rect("DevelopmentAndLicenses", owner.transform, Vector2.zero);
            Stretch(root);
            var view = root.gameObject.AddComponent<StartMenuCreditsPanel>();
            view.owner = owner;
            view.data = data;
            view.font = owner.host.GetComponentInChildren<TMP_Text>(true).font;
            if (data != null && data.sourceFont != null)
            {
                view.font = TMP_FontAsset.CreateFontAsset(data.sourceFont, 48, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
                view.font.name = "Start credits runtime font";
                view.ownsFont = true;
            }
            view.Build();
            root.gameObject.SetActive(false);
            return view;
        }

        void Build()
        {
            // Reuse the authored main button's skin, animation and font.
            var copy = Instantiate(owner.quit.gameObject, owner.mainPage.transform);
            copy.name = "Button_DevelopmentAndLicenses";
            menuButton = copy.GetComponent<UnityEngine.UI.Button>();
            menuButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            menuButton.onClick.AddListener(() => owner.Show(StartMenu.Page.Credits));
            var menuLabel = copy.GetComponentInChildren<TMP_Text>(true);
            menuLabel.text = "개발 및 라이선스";
            menuLabel.font = font;
            var quitRect = (RectTransform)owner.quit.transform;
            var settingsRect = (RectTransform)owner.settings.transform;
            var step = quitRect.anchoredPosition - settingsRect.anchoredPosition;
            if (step.sqrMagnitude < 1f) step = new Vector2(0, -70);
            quitRect.anchoredPosition += step;
            var main = (RectTransform)owner.mainPage.transform;
            main.sizeDelta = new Vector2(main.sizeDelta.x, main.sizeDelta.y + Mathf.Abs(step.y));

            var dim = gameObject.AddComponent<UnityEngine.UI.Image>();
            dim.color = new Color(0.015f, .01f, .015f, .9f);
            dim.raycastTarget = true;
            panel = Rect("Programme", transform, new Vector2(1000, 840));
            Fill(panel, Gold);
            var inside = Rect("Inset", panel, Vector2.zero);
            Stretch(inside, 3);
            Fill(inside, Ink);
            var line = Rect("HeaderRule", panel, new Vector2(900, 2));
            Top(line, 0, -142);
            Fill(line, Gold);
            var kicker = Label("Kicker", panel, "CURTAIN CALL  /  PROGRAMME", 16, Gold);
            Top(kicker.rectTransform, 0, -34);
            kicker.rectTransform.sizeDelta = new Vector2(880, 28);
            kicker.alignment = TextAlignmentOptions.Center;
            heading = Label("Heading", panel, "개발 및 라이선스", 38, Paper);
            Top(heading.rectTransform, 0, -78);
            heading.rectTransform.sizeDelta = new Vector2(880, 52);
            heading.alignment = TextAlignmentOptions.Center;

            var scrollRoot = Rect("Scroll", panel, new Vector2(900, 580));
            Top(scrollRoot, 0, -164);
            scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42;
            var viewport = Rect("Viewport", scrollRoot, Vector2.zero);
            Stretch(viewport);
            Fill(viewport, new Color(0, 0, 0, .01f));
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            viewport.offsetMax = new Vector2(-18, 0);
            content = Rect("Content", viewport, Vector2.zero);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.padding = new RectOffset(12, 24, 10, 18);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            var rail = Rect("Scrollbar", scrollRoot, new Vector2(6, 0));
            rail.anchorMin = new Vector2(1, 0);
            rail.anchorMax = Vector2.one;
            rail.pivot = new Vector2(1, .5f);
            Fill(rail, new Color32(51, 61, 55, 255));
            var handle = Rect("Handle", rail, Vector2.zero);
            Stretch(handle);
            Fill(handle, Gold);
            var bar = rail.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            close = Button("Close", panel, "닫기", () => owner.Show(StartMenu.Page.Main));
            close.GetComponent<RectTransform>().anchoredPosition = new Vector2(280, -374);
            back = Button("Back", panel, "목록으로", BuildOverview);
            back.GetComponent<RectTransform>().anchoredPosition = new Vector2(-280, -374);
        }

        public void SetVisible(bool visible)
        {
            if (visible)
            {
                if (gameObject.activeSelf) return;
                previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                gameObject.SetActive(true);
                transform.SetAsLastSibling();
                BuildOverview();
                close.Select();
            }
            else
            {
                bool wasOpen = gameObject.activeSelf;
                gameObject.SetActive(false);
                if (wasOpen && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection :
                        menuButton != null && menuButton.gameObject.activeInHierarchy ? menuButton.gameObject : null);
            }
        }

        void Update()
        {
            // Fit smaller editor/game viewports while keeping the authored programme proportions.
            var root = (RectTransform)transform;
            float fit = Mathf.Min(1f, (root.rect.width - 48) / 1000f, (root.rect.height - 48) / 840f);
            panel.localScale = Vector3.one * Mathf.Max(.2f, fit);
            if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame))
            {
                if (showingLicense) { BuildOverview(); close.Select(); }
                else owner.Show(StartMenu.Page.Main);
            }
        }

        void Clear()
        {
            foreach (Transform child in content)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            scroll.StopMovement();
        }

        void BuildOverview()
        {
            Clear();
            showingLicense = false;
            heading.text = "개발 및 라이선스";
            back.gameObject.SetActive(false);
            Section("DEVELOPED BY", "개발에 참여한 사람들");
            if (data != null)
                foreach (var person in data.contributors)
                {
                    if (person == null || string.IsNullOrWhiteSpace(person.nickname)) continue;
                    var card = Card();
                    Body(card, person.nickname, 28, Paper);
                    if (!string.IsNullOrWhiteSpace(person.role)) Body(card, person.role, 19, Gold);
                }
            Section("ASSETS & LICENSES", "함께 사용한 에셋과 도구");
            if (data != null)
                foreach (var item in data.licenses)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.asset)) continue;
                    var card = Card();
                    Body(card, item.asset, 26, Paper);
                    Body(card, item.author + "  ·  " + item.license, 20, Gold);
                    if (!string.IsNullOrWhiteSpace(item.notice)) Body(card, item.notice, 19, new Color32(191, 199, 192, 255));
                    if (item.fullLicense != null)
                    {
                        var button = Button("LicenseText", card, "라이선스 전문 보기", () => ShowLicense(item));
                        button.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
                    }
                    if (IsWebAddress(item.url))
                    {
                        var button = Button("SourceLink", card, "제공처 · 라이선스 원문 ↗", () => Application.OpenURL(item.url));
                        button.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
                    }
                }
            ResetScroll();
        }

        void ShowLicense(StartMenuCreditsSettings.AssetLicense item)
        {
            Clear();
            showingLicense = true;
            heading.text = item.asset;
            back.gameObject.SetActive(true);
            var card = Card();
            Body(card, item.license, 24, Gold);
            Body(card, item.fullLicense != null ? item.fullLicense.text : item.notice, 20, Paper);
            ResetScroll();
            back.Select();
        }

        void ResetScroll()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = 1f;
        }

        void Section(string eyebrow, string title)
        {
            var card = Card();
            Body(card, eyebrow, 16, Gold);
            Body(card, title, 24, Paper);
        }

        RectTransform Card()
        {
            var card = Rect("Entry", content, Vector2.zero);
            Fill(card, new Color32(24, 38, 39, 255));
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 18, 18);
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return card;
        }

        void Body(Transform parent, string text, float size, Color color)
        {
            var label = Label("Text", parent, text, size, color);
            label.richText = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
        }

        TMP_Text Label(string name, Transform parent, string text, float size, Color color)
        {
            var rect = Rect(name, parent, new Vector2(300, 40));
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.text = text;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        UnityEngine.UI.Button Button(string name, Transform parent, string text, UnityEngine.Events.UnityAction clicked)
        {
            var rect = Rect(name, parent, new Vector2(250, 56));
            Fill(rect, new Color32(40, 57, 54, 255));
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.3f, 1.2f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.7f, .7f, .7f);
            button.colors = colors;
            button.onClick.AddListener(clicked);
            var label = Label("Label", rect, text, 22, Paper);
            Stretch(label.rectTransform, 8);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        static bool IsWebAddress(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

        void OnDestroy()
        {
            if (!ownsFont || font == null) return;
            foreach (var atlas in font.atlasTextures) if (atlas != null) Destroy(atlas);
            if (font.material != null) Destroy(font.material);
            Destroy(font);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = size;
            return rect;
        }

        static void Stretch(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
        }

        static void Top(RectTransform rect, float x, float y)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(x, y);
        }

        static void Fill(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = true;
        }
    }
}
