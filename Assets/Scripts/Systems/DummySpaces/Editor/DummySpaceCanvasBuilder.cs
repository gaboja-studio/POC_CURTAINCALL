using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurtainCall.DummySpaces;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CurtainCall.DummySpaces.EditorTools
{
    /// <summary>소유한 네 더미 씬에 편집 가능한 Canvas와 최소 토큰을 저장한다.</summary>
    public static class DummySpaceCanvasBuilder
    {
        const string Folder = "Assets/Scenes/Tests/Systems/DummySpaces";
        const string Display = Folder + "/Display";
        static TMP_FontAsset font;
        static readonly Color Navy = new(0.055f, 0.095f, 0.15f);
        static readonly Color Ink = new(0.12f, 0.19f, 0.25f);
        static readonly Color Muted = new(0.36f, 0.43f, 0.48f);
        static readonly Color Teal = new(0.055f, 0.46f, 0.41f);
        static readonly Color Light = new(0.95f, 0.96f, 0.95f);
        static readonly Color Line = new(0.85f, 0.89f, 0.88f);
        static readonly List<DummySpaceCanvas.ActionButton> buttons = new();

        [MenuItem("Tools/CurtainCall/Build Dummy Space Canvas")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료하세요.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("미저장 씬이 있습니다. 먼저 확인하세요.");
            string original = SceneManager.GetActiveScene().path;
            if (!AssetDatabase.IsValidFolder(Display)) AssetDatabase.CreateFolder(Folder, "Display");
            font = Font();
            foreach (string name in new[] { "DummyPerformance", "DummyWaitingRoom", "DummyExploration", "DummyEscapeRoute" }) Build(name);
            AssetDatabase.SaveAssetIfDirty(font);
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
            Debug.Log("[DummySpaces] 네 씬 Canvas·입력·토큰 저장 완료");
        }

        static TMP_FontAsset Font()
        {
            string path = Display + "/dummy-korean.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (asset != null)
            {
                string additions = string.Concat(Directory.GetFiles("Assets/Scripts/Systems/DummySpaces", "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
                asset.TryAddCharacters(new string(additions.Distinct().Where(c => !char.IsControl(c)).ToArray()), out _);
                AssetDatabase.SaveAssetIfDirty(asset);
                return asset;
            }
            var source = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/Resources/Fonts/Pretendard-Medium/Pretendard-Medium 1.otf");
            if (source == null) throw new InvalidOperationException("한글 원본 폰트가 없습니다.");
            asset = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            asset.name = "Dummy Korean";
            asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            AssetDatabase.CreateAsset(asset, path);
            asset.material.name = "Dummy Korean Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var texture in asset.atlasTextures)
            {
                texture.name = "Dummy Korean Atlas";
                AssetDatabase.AddObjectToAsset(texture, asset);
            }
            string text = string.Concat(Directory.GetFiles("Assets/Scripts/Systems/DummySpaces", "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
            text += "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz → · ▶ ≠ / [ ] : - + , . ( )";
            asset.TryAddCharacters(new string(text.Distinct().Where(c => !char.IsControl(c)).ToArray()), out string missing);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        static void Build(string name)
        {
            var scene = EditorSceneManager.OpenScene($"{Folder}/{name}.unity");
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "Dummy Canvas" || go.name == "Dummy Preview" || go.name == "Dummy EventSystem") Object.DestroyImmediate(go);
            buttons.Clear();
            bool performance = name == "DummyPerformance";
            bool waiting = name == "DummyWaitingRoom";
            bool exploration = name == "DummyExploration";
            string title = performance ? "묘기" : waiting ? "대기실" : exploration ? "탐색 · 백스테이지" : "탈출로";
            string subtitle = performance ? "상황 입력 → 공연 판정 → 보상과 시간대" : waiting ? "공동 자금 · 복구 조회 · 밤 준비" : exploration ? "구역 입력 · 타이머 · 안전 조건" : "행 입력 · 수동 결론 · 엔딩과 세대 교체";
            var root = new GameObject("Dummy Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(DummySpaceCanvas));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scale = root.GetComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1600, 900);
            scale.matchWidthOrHeight = 0.5f;
            var view = root.GetComponent<DummySpaceCanvas>();
            var rt = (RectTransform)root.transform;
            Box("Header", rt, 0, 0, 1600, 172, Navy);
            Text("Brand", rt, "CURTAIN CALL   /   SYSTEM FLOW LAB", 32, 23, 1080, 24, 16, new Color(0.42f, 0.79f, 0.72f));
            Text("Title", rt, title, 32, 54, 850, 50, 34, Color.white);
            Text("Subtitle", rt, subtitle, 32, 111, 980, 30, 18, new Color(0.74f, 0.81f, 0.83f));
            Text("DummyBadge", rt, "임시 검증용 · 실제 공간 아님", 32, 148, 460, 24, 17, new Color(0.84f, 0.88f, 0.86f), TextAlignmentOptions.Right);
            AddButton(rt, "details", "연결 상세 / 미완료", 1275, 85, 285, 48, Teal);
            Box("BodyLeft", rt, 0, 172, 550, 728, Light);
            Box("BodyRight", rt, 550, 172, 1050, 728, new Color(0.89f, 0.92f, 0.91f));
            view.status = Text("Status", rt, "상태 연결 대기", 32, 179, 1530, 38, 19, Ink);
            view.steps = Text("Steps", rt, "도입 / 공연 / 쉬는 시간 / 밤 / 탈출 / 엔딩", 585, 141, 970, 27, 17, Color.white, TextAlignmentOptions.Right);

            Text("ChooseHeading", rt, "01   상황 선택", 32, 231, 475, 36, 26, Ink);
            view.selection = Text("Selection", rt, "선택 대상 연결 대기", 32, 278, 475, 108, 19, Ink);
            view.condition = Text("Condition", rt, "현재 단계에서 가능한 버튼만 활성화됩니다.", 32, 390, 475, 68, 16, Muted);
            if (performance)
            {
                AddButton(rt, "person", "다음 참가자 선택", 32, 474, 475, 46, Teal);
                AddButton(rt, "cut", "선택 대상 절단", 32, 528, 231, 46, Line);
                AddButton(rt, "kill", "선택 대상 사망", 275, 528, 232, 46, new Color(0.66f, 0.24f, 0.27f));
                AddButton(rt, "cheer", "환호 +", 32, 582, 231, 46, Teal);
                AddButton(rt, "arrive", "선택 대상 도착", 275, 582, 232, 46, Teal);
            }
            var content = Scroll("Actions", rt, 24, performance ? 644 : 473, 502, performance ? 214 : 385);
            Group(content, "흐름 준비");
            FlowButton(content, "advance", "수동 진행 · 다음 단계 준비", true);
            FlowButton(content, "ready", "밤 준비 / 해제");
            if (performance)
            {
                Group(content, "공연 시작");
                FlowButton(content, "start", "오프라인 런 시작");
                FlowButton(content, "intro", "도입 종료");
                FlowButton(content, "begin", "공연 시작 · 명부 고정", true);
                FlowButton(content, "timeout", "수동 제한 시간 종료", false, true);
            }
            else if (waiting)
            {
                Group(content, "공동 자금 / 출구");
                FlowButton(content, "spend", "지출만 검증 · 물품 없음", true);
                FlowButton(content, "leave", "대기실 출구 신호");
                Group(content, "조회 입력 → 규칙 확인");
                FlowButton(content, "lost", "조회 손실 수 변경  0 ~ 3");
                FlowButton(content, "backpack", "조회 배낭 유무 변경");
                FlowButton(content, "restorePreview", "복구 / 소지 칸 조회", true);
                ServerButtons(content, true);
            }
            else if (exploration)
            {
                Group(content, "구역 선택 / 전환 신호");
                FlowButton(content, "leave", "출구 → 백스테이지", true);
                FlowButton(content, "room", "대기실 구역 입력");
                FlowButton(content, "stage", "무대 입구 + 전원 집결 가정");
                FlowButton(content, "expire", "남은 시간 0 요청", false, true);
                Group(content, "잡힘 연결");
                FlowButton(content, "catchPreview", "잡힘 규칙 조회", true);
                ServerButtons(content, false);
            }
            else
            {
                Group(content, "탈출 개시 / 행 선택");
                FlowButton(content, "escape", "탈출 개시 · 전원 집결 가정", true);
                FlowButton(content, "row", "다음 더미 행 선택");
                FlowButton(content, "rowArrive", "선택 행 도착 입력");
                FlowButton(content, "rowRetire", "선택 행 리타이어 입력");
                Group(content, "별도 수동 결론 · 자동 집계 아님");
                FlowButton(content, "success", "도착 있음 → 엔딩", true);
                FlowButton(content, "retired", "전원 리타이어 → 세대 교체", false, true);
                ServerButtons(content, false);
            }
            Text("ActionHint", rt, "목록을 스크롤해 나머지 입력을 확인하세요 ↓", 32, 867, 485, 24, 15, Muted);

            var liveCard = Box("LiveCard", rt, 574, 231, 994, 140, Color.white);
            Text("LiveHeading", liveCard, "LIVE   지금 진행 중", 24, 10, 650, 25, 18, Muted);
            view.live = Text("LiveText", liveCard, "흐름 연결 대기", 24, 40, 700, 84, 18, Ink);
            view.clock = Text("Clock", liveCard, "--:--", 740, 27, 230, 64, 46, Teal, TextAlignmentOptions.TopRight);
            Box("ClockTrack", liveCard, 24, 128, 946, 5, Line);
            view.clockFill = Box("ClockFill", liveCard, 24, 128, 946, 5, Teal).GetComponent<Image>();
            // 스프라이트 없는 단색 막대는 왼쪽 pivot 기준 크기만 바꾼다.
            view.clockFill.type = Image.Type.Simple;
            var card = Box("ResultCard", rt, 574, 383, 994, 218, Color.white);
            Text("ResultHeading", card, "02   처리 결과", 24, 16, 560, 35, 26, Ink);
            view.resultBadge = Text("ResultBadge", card, "실행 대기", 720, 20, 246, 30, 19, Muted, TextAlignmentOptions.Right);
            view.resultTitle = Text("ResultTitle", card, "상황을 선택하고 버튼을 눌러 보세요", 24, 52, 946, 32, 23, Ink);
            view.resultBody = Text("ResultBody", card, "입력 → 공통 API → 변경값을 이곳에서 확인합니다.", 24, 91, 946, 63, 19, Ink);
            Text("ChangeHeading", card, "변경 전  →  변경 후", 24, 157, 190, 24, 15, Muted);
            var diff = Scroll("Changes", card, 217, 156, 750, 54);
            view.changes = FlowText(diff, "ChangesText", "아직 관찰한 변경값이 없습니다.", 16, Ink, 54);
            view.connection = Text("Connection", rt, "입력 대기 · 실제 처리 / 더미 입력 / 조회를 구분합니다", 592, 608, 960, 33, 16, Teal);

            // 이 영역의 배경은 투명하게 두어 기존 카메라의 토큰을 함께 보여 준다.
            Object.DestroyImmediate(GameObject.Find("BodyRight").GetComponent<Image>());
            Box("RightTopBacking", rt, 550, 172, 1050, 475, new Color(0.89f, 0.92f, 0.91f)).SetAsFirstSibling();
            Box("LogBacking", rt, 550, 735, 1050, 165, Light);
            Text("PreviewLabel", rt, "3D 보조 · 더미 토큰 / 실제 플레이어 아님", 592, 652, 950, 25, 15, new Color(0.71f, 0.85f, 0.80f));
            Text("HistoryHeading", rt, "상황 기록 · 최신순 (원시 API는 상세에서)", 592, 744, 930, 26, 17, Muted);
            var log = Scroll("History", rt, 587, 778, 967, 110);
            view.history = FlowText(log, "HistoryText", "상황 기록 대기", 18, Ink, 110);

            var overlay = Box("DetailsOverlay", rt, 565, 222, 1015, 665, Light);
            Text("DetailsHeading", overlay, "연결 상세 · 구현 경계", 24, 20, 950, 40, 28, Ink);
            var detailScroll = Scroll("DetailsScroll", overlay, 22, 80, 970, 550);
            view.details = FlowText(detailScroll, "DetailsText", "상세 상태 연결 대기", 21, Ink, 550);
            view.detailsPanel = overlay.gameObject;
            overlay.gameObject.SetActive(false);
            view.actions = buttons.ToArray();
            SetupInput();
            view.preview = BuildPreview();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void ServerButtons(RectTransform parent, bool restore)
        {
            Group(parent, "실제 서버 대상 필요");
            FlowButton(parent, "server", "다음 실제 플레이어 선택");
            FlowButton(parent, restore ? "restore" : "catch", restore ? "서버 신체 1부위 복구 · 무료 검증" : "서버 잡힘 적용 · 장소 일치 필요");
        }

        static void SetupInput()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) throw new InvalidOperationException("예상하지 못한 기존 EventSystem이 있습니다.");
            var go = new GameObject("Dummy EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = go.GetComponent<InputSystemUIInputModule>();
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            module.actionsAsset = asset;
            // 기존 inputactions의 subasset 참조를 사용해 씬 재열기 뒤에도 연결을 유지한다.
            InputActionReference Existing(string action)
            {
                var a = asset.FindAction("UI/" + action, true);
                var reference = AssetDatabase.LoadAllAssetsAtPath("Assets/InputSystem_Actions.inputactions").OfType<InputActionReference>().FirstOrDefault(r => r.action != null && r.action.id == a.id);
                if (reference == null) throw new InvalidOperationException("저장된 UI 액션 참조 없음: " + action);
                return reference;
            }
            module.point = Existing("Point");
            module.leftClick = Existing("Click");
            module.scrollWheel = Existing("ScrollWheel");
            module.move = Existing("Navigate");
            module.submit = Existing("Submit");
            module.cancel = Existing("Cancel");
        }

        static DummySpacePreview BuildPreview()
        {
            var root = new GameObject("Dummy Preview", typeof(DummySpacePreview));
            var view = root.GetComponent<DummySpacePreview>();
            view.idle = Material("idle", new Color(0.31f, 0.43f, 0.51f));
            view.active = Material("active", new Color(0.15f, 0.81f, 0.69f));
            view.complete = Material("complete", new Color(0.90f, 0.74f, 0.30f));
            view.danger = Material("danger", new Color(0.87f, 0.29f, 0.34f));
            var floor = Primitive("Preview Platform", root.transform, PrimitiveType.Cube, new Vector3(0, -0.2f, 1), new Vector3(7, 0.2f, 4), Material("platform", new Color(0.13f, 0.22f, 0.27f)));
            view.tokens = new Transform[4];
            view.tokenRenderers = new Renderer[4];
            for (int i = 0; i < 4; i++)
            {
                var token = Primitive("Dummy Token " + (i + 1), root.transform, PrimitiveType.Capsule, new Vector3((i - 1.5f) * 1.4f, 0.65f, 0), new Vector3(0.6f, 0.65f, 0.6f), view.idle);
                view.tokens[i] = token.transform;
                view.tokenRenderers[i] = token.GetComponent<Renderer>();
            }
            var camera = Camera.main;
            camera.transform.position = new Vector3(0, 7, -10);
            camera.transform.LookAt(new Vector3(0, 0, 1));
            camera.orthographic = true;
            camera.orthographicSize = 2.3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Navy;
            camera.rect = new Rect(0.35f, 0.183f, 0.65f, 0.098f);
            return view;
        }

        static GameObject Primitive(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static Material Material(string name, Color color)
        {
            string path = $"{Display}/dummy-{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        static RectTransform Box(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var rect = Rect(name, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static TMP_Text Text(string name, Transform parent, string text, float x, float y, float width, float height, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var label = Rect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.text = text;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        static void AddButton(Transform parent, string id, string label, float x, float y, float width, float height, Color color)
        {
            var rect = Box("Button-" + id, parent, x, y, width, height, color);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.82f, 0.94f, 0.91f);
            colors.pressedColor = new Color(0.62f, 0.80f, 0.75f);
            colors.disabledColor = new Color(0.62f, 0.65f, 0.66f, 0.45f);
            button.colors = colors;
            Text("Label", rect, label, 14, 0, width - 28, height, 19, color == Line ? Ink : Color.white, TextAlignmentOptions.MidlineLeft);
            buttons.Add(new DummySpaceCanvas.ActionButton { id = id, button = button });
        }

        static RectTransform Scroll(string name, Transform parent, float x, float y, float width, float height)
        {
            var root = Rect(name, parent, x, y, width, height);
            var image = root.gameObject.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.005f);
            root.gameObject.AddComponent<RectMask2D>();
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            scroll.viewport = root;
            var content = Rect("Content", root, 0, 0, width - 10, height);
            content.anchorMax = new Vector2(1, 1);
            content.sizeDelta = new Vector2(-10, height);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(8, 8, 4, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return content;
        }

        static void Group(RectTransform parent, string label) => FlowText(parent, "Group", label, 16, Muted, 33);

        static TMP_Text FlowText(RectTransform parent, string name, string text, int size, Color color, int minHeight)
        {
            var label = Text(name, parent, text, 0, 0, parent.rect.width, minHeight, size, color);
            var element = label.gameObject.AddComponent<LayoutElement>();
            element.minHeight = minHeight;
            return label;
        }

        static void FlowButton(RectTransform parent, string id, string label, bool primary = false, bool destructive = false)
        {
            AddButton(parent, id, label, 0, 0, 470, 48, destructive ? new Color(0.66f, 0.24f, 0.27f) : primary ? Teal : Line);
            var layout = buttons[buttons.Count - 1].button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 48;
        }
    }
}
