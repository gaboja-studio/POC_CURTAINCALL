#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.UI.ScheduleV1;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    /// <summary>GPT Image 원본은 private 키트에, 데이터/입력은 공개 코드에 보존한다.</summary>
    public static class ScheduleTicketKitBuilder
    {
        public const string Kit = "Assets/_ThirdParty/curtaincall_schedule_v2";
        public const string PrefabPath = "Assets/Resources/Prefabs/UIs/Roles/Retained/ScheduleUi/SchedulePanelV2.prefab";
        static TMP_FontAsset font;
        static readonly Color Ink = new Color32(62, 24, 23, 255), Cream = new Color32(245, 231, 196, 255);
        static readonly Dictionary<string, Sprite> sprites = new();

        [MenuItem("Tools/Curtain Call UI/Build schedule ticket kit v2")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            AssetDatabase.Refresh();
            Import("board", 1, 1, new[] { "board" });
            Import("ticket", 1, 1, new[] { "ticket" });
            Import("ticket-focus", 1, 1, new[] { "ticket-focus" });
            Import("plates", 2, 2, new[] { "title-plate", "day-plate", "close-plate", "current-ribbon" });
            Import("controls", 4, 2, new[] { "arrow-normal", "arrow-hover", "arrow-pressed", "arrow-disabled", "lock", "check", "unused-glow", "hint-pill" });
            font = MakeFont();
            var root = new GameObject("SchedulePanelCanvasV2", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.SetActive(false);
            try
            {
                root.layer = LayerMask.NameToLayer("UI");
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 110;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                var viewRoot = Rect(root.transform, "W3_ScheduleV2", 0, 0, 1920, 1080);
                Stretch(viewRoot);
                var view = viewRoot.gameObject.AddComponent<SchedulePanelView>();
                var backdrop = Rect(viewRoot, "FocusBackdrop", 0, 0, 1920, 1080); Stretch(backdrop);
                backdrop.gameObject.AddComponent<Image>().color = new Color(.015f, .025f, .025f, .92f);
                backdrop.gameObject.SetActive(false);
                var visuals = Rect(viewRoot, "00_Visuals", 0, 0, 1920, 1080); Stretch(visuals);
                var group = visuals.gameObject.AddComponent<CanvasGroup>();
                var stage = Rect(visuals, "10_LayoutStage", 0, 0, 1920, 1080);
                stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(.5f, .5f); stage.anchoredPosition = Vector2.zero;
                var panel = Rect(stage, "00_SchedulePanel", 60, 60, 1800, 960);
                Picture(panel, "00_Board", "board", 0, 0, 1800, 960, true);
                Picture(panel, "TitlePlate", "title-plate", 520, 24, 760, 122);
                var title = Text(panel, "Title", "공연 스케줄", 650, 39, 500, 82, 58, Ink);
                var context = Text(panel, "Context", "1일차 밤 · 준비 0/4", 1300, 48, 430, 52, 28, Cream);
                var days = new SchedulePanelView.DayLayers[3];
                var edits = new Button[6];
                for (int d = 0; d < 3; d++)
                {
                    var column = Rect(panel, $"Day{d + 1}", 80 + d * 558, 164, 524, 674);
                    Picture(column, "HeaderPlate", "day-plate", 75, 0, 374, 65);
                    var label = Text(column, "DayLabel", $"{d + 1}일차", 90, 4, 230, 54, 38, Cream);
                    var current = Text(column, "Current", "현재", 328, 12, 100, 40, 24, Cream);
                    var viewport = Rect(column, "Viewport", 0, 86, 524, 584);
                    var content = Rect(viewport, "Content", 0, 0, 524, 584);
                    var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
                    scroll.horizontal = false; scroll.vertical = false; scroll.inertia = false;
                    var empty = Text(column, "Empty", "", 0, 100, 524, 30, 22, Cream);
                    var cards = new List<ScheduleCardVisual>();
                    for (int s = 0; s < 2; s++)
                    {
                        var card = Rect(content, $"Card{s + 1}", 0, s * 304, 524, 280);
                        var body = Picture(card, "Paper", "ticket", 0, 0, 524, 280, true);
                        var slot = card.gameObject.AddComponent<Button>(); slot.targetGraphic = body; slot.transition = Selectable.Transition.None;
                        var ticket = card.gameObject.AddComponent<ScheduleTicketCard>(); ticket.slotButton = slot;
                        ticket.slotLabel = Text(card, "SlotLabel", $"공연 {s + 1}", 30, 8, 464, 42, 25, Ink);
                        ticket.performanceName = Text(card, "PerformanceName", s == 0 ? "줄타기" : "인간 대포", 28, 43, 468, 86, 62, Ink);
                        Picture(card, "HintBacking", "hint-pill", 113, 130, 298, 29);
                        ticket.hint = Text(card, "ClickHint", "슬롯 클릭으로 공연 변경", 55, 129, 414, 31, 21, Ink);
                        Text(card, "DifficultyCaption", "난이도", 32, 180, 82, 40, 23, Ink);
                        ticket.previousButton = Arrow(card, "PreviousTier", 120, true);
                        ticket.tierLabel = Text(card, "TierLabel", "1단계", 179, 180, 104, 40, 30, Ink);
                        ticket.nextButton = Arrow(card, "NextTier", 290, false);
                        ticket.feedback = Text(card, "AppliedFeedback", "", 367, 180, 132, 40, 19, Ink);
                        ticket.lockIcon = Picture(card, "Lock", "lock", 347, 191, 16, 20);
                        ticket.successIcon = Picture(card, "Success", "check", 344, 190, 20, 20);
                        ticket.successIcon.enabled = false;
                        ticket.rewards = Text(card, "Rewards", "호응 상한 100   |   완주 보너스 100", 25, 232, 474, 32, 23, Ink);
                        ticket.hoverOutline = Picture(card, "HoverOutline", "ticket-focus", -2, -2, 528, 284);
                        ticket.hoverOutline.enabled = false;
                        // Legacy read-only view keeps the existing public Open/Close/presentation contract.
                        var hidden = Rect(card, "LegacyPresentation", 0, 0, 1, 1); hidden.gameObject.SetActive(false);
                        var unusedImage = hidden.gameObject.AddComponent<Image>(); unusedImage.raycastTarget = false;
                        var unusedText = Text(hidden, "Unused", "", 0, 0, 1, 1, 1, Ink);
                        var legacy = card.gameObject.AddComponent<ScheduleCardVisual>();
                        legacy.Configure(unusedImage, unusedImage, unusedImage, unusedImage, ticket.performanceName, unusedText, unusedText, unusedText, unusedText);
                        cards.Add(legacy); edits[d * 2 + s] = slot;
                    }
                    days[d] = new SchedulePanelView.DayLayers { label = label, currentLabel = current, emptyMessage = empty, scroll = scroll, content = content, cards = cards };
                }
                var closeRect = Rect(panel, "Close", 75, 864, 260, 66);
                var closeBody = Picture(closeRect, "Body", "close-plate", 0, 0, 260, 66, true);
                var closeContent = Rect(closeRect, "Content", 0, 0, 260, 66);
                var closeLabel = Text(closeContent, "Label", "닫기  ESC", 15, 8, 230, 50, 32, Cream);
                var close = closeRect.gameObject.AddComponent<CurtainCall.UI.ScheduleV1.CurtainCallButton>();
                close.Configure(closeBody, null, closeContent, closeLabel, null, S("close-plate"), S("close-plate"), S("close-plate"), S("close-plate"));
                var footer = Text(panel, "Footer", "슬롯 클릭: 공연 변경   |   좌우 화살표: 난이도 변경   ·   선택 즉시 적용", 380, 864, 1320, 66, 26, Cream);
                footer.alignment = TextAlignmentOptions.MidlineLeft;
                var unused = Rect(viewRoot, "UnusedActions", 0, 0, 1, 1); unused.gameObject.SetActive(false);
                var confirm = Rect(unused, "Confirm", 0, 0, 1, 1).gameObject.AddComponent<Button>();
                var ready = Rect(unused, "Ready", 0, 0, 1, 1).gameObject.AddComponent<Button>();
                view.Configure(group, stage, title, context, footer, days, close);
                root.AddComponent<WaitingRoomScheduleUI>().Configure(view, edits, confirm, ready);
                root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssetIfDirty(font);
            Debug.Log("[ScheduleV2] Built layered ticket prefab: " + PrefabPath);
        }

        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Editor must be stopped.");
            var scene = SceneManager.GetSceneByPath(CommonWaitingRoomUI.ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(CommonWaitingRoomUI.ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("CommonWaitingRoomUI has unsaved user changes.");
            var old = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WaitingRoomScheduleUI>(true)).Single();
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), old.transform.parent);
            replacement.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
            var adapter = replacement.GetComponent<WaitingRoomScheduleUI>();
            foreach (var tooltip in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PanelHoverTooltip>(true)))
                if (tooltip.schedule == old) { tooltip.schedule = adapter; EditorUtility.SetDirty(tooltip); PrefabUtility.RecordPrefabInstancePropertyModifications(tooltip); }
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Schedule scene save failed.");
            if (opened) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("[ScheduleV2] Replaced schedule prefab and rebound tooltip adapter.");
        }

        static Sprite S(string name) => sprites[name];
        static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.gameObject.layer = LayerMask.NameToLayer("UI"); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(width, height); return r;
        }
        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        static Image Picture(Transform p, string name, string sprite, float x, float y, float w, float h, bool raycast = false)
        { var i = Rect(p, name, x, y, w, h).gameObject.AddComponent<Image>(); i.sprite = S(sprite); i.raycastTarget = raycast; return i; }
        static TextMeshProUGUI Text(Transform p, string name, string text, float x, float y, float w, float h, float size, Color color)
        {
            var t = Rect(p, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>(); t.font = font; t.fontSize = size;
            t.text = text; t.color = color; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis; t.richText = false; return t;
        }
        static Button Arrow(Transform parent, string name, float x, bool left)
        {
            var r = Rect(parent, name, x, 180, 56, 40);
            var image = r.gameObject.AddComponent<Image>(); image.sprite = S("arrow-normal");
            if (left) { r.pivot = new Vector2(.5f, 1); r.anchoredPosition += new Vector2(28, 0); r.localScale = new Vector3(-1, 1, 1); }
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = image; b.transition = Selectable.Transition.SpriteSwap;
            b.spriteState = new SpriteState { highlightedSprite = S("arrow-hover"), selectedSprite = S("arrow-hover"), pressedSprite = S("arrow-pressed"), disabledSprite = S("arrow-disabled") };
            return b;
        }
        static TMP_FontAsset MakeFont()
        {
            const string path = "Assets/Resources/Fonts/ScheduleUi/ScheduleTicket-Bold SDF.asset";
            var result = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (result != null) return result;
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_results_v1/Source/Fonts/Pretendard-Bold.ttf");
            result = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            result.name = "ScheduleTicket-Bold SDF";
            string chars = "공연스케줄일차밤준비현재인간대포타기슬롯클릭으로변경난이도호응상한완주보너스적용됨중고정불가미해금선택즉시스트만가능닫수면숙소침대에서입장전까지좌우화살표다음날아침넘어갑니다데모종료출을진행하세요새편성보기정보를받는0/123456789 ·:|ESC<>!-()";
            result.TryAddCharacters(chars, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("Schedule font missing: " + missing);
            result.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(result, path);
            foreach (var atlas in result.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, result);
            AssetDatabase.AddObjectToAsset(result.material, result);
            AssetDatabase.SaveAssetIfDirty(result); return result;
        }
        static void Import(string file, int columns, int rows, string[] names)
        {
            string path = $"{Kit}/Sprites/{file}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
            importer.spritePixelsPerUnit = 100; importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var pixels = texture.GetPixels32();
            var rects = new List<SpriteMetaData>();
            for (int i = 0; i < names.Length; i++)
            {
                int cw = texture.width / columns, ch = texture.height / rows;
                int ox = (i % columns) * cw, oy = (rows - 1 - i / columns) * ch;
                int minX = ox + cw, minY = oy + ch, maxX = ox, maxY = oy;
                for (int y = oy; y < oy + ch; y++) for (int x = ox; x < ox + cw; x++)
                    if (pixels[y * texture.width + x].a > 15) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
                if (minX > maxX) throw new InvalidOperationException("Empty generated sprite: " + names[i]);
                minX = Math.Max(ox, minX - 2); minY = Math.Max(oy, minY - 2); maxX = Math.Min(ox + cw - 1, maxX + 2); maxY = Math.Min(oy + ch - 1, maxY + 2);
                rects.Add(new SpriteMetaData { name = names[i], rect = new UnityEngine.Rect(minX, minY, maxX - minX + 1, maxY - minY + 1), pivot = new Vector2(.5f, .5f), alignment = (int)SpriteAlignment.Center });
            }
            // This project intentionally has no 2D Sprite Editor package; use its supported importer API.
#pragma warning disable CS0618
            importer.spritesheet = rects.ToArray();
#pragma warning restore CS0618
            importer.isReadable = false; importer.SaveAndReimport();
            foreach (var s in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()) sprites[s.name] = s;
        }
    }
}
#endif
