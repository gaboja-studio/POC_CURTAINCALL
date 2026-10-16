using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CurtainCall.DummySpaces.EditorTools
{
    /// <summary>
    /// 더미 공간·SpaceLink 시작 씬 빌더가 같이 쓰는 도구. Canvas 부품(좌상단 기준 1600×900), 3D 더미 공간 부품(Synty 참조, 없으면 기본 도형).
    /// </summary>
    public static class DummyUiKit
    {
        public const string Display = "Assets/Scenes/Tests/Systems/DummySpaces/Display";
        public const string Generic = "Assets/_ThirdParty/Synty/PolygonGeneric/Prefabs/Base/";
        public const string Props = "Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Props/";
        public const float Tile = 2.5f;

        public static readonly Color Navy = new(0.055f, 0.095f, 0.15f);
        public static readonly Color NavyGlass = new(0.055f, 0.095f, 0.15f, 0.86f);
        public static readonly Color Ink = new(0.12f, 0.19f, 0.25f);
        public static readonly Color Muted = new(0.36f, 0.43f, 0.48f);
        public static readonly Color Teal = new(0.055f, 0.46f, 0.41f);
        public static readonly Color Panel = new(0.95f, 0.96f, 0.95f, 0.97f);
        public static readonly Color Line = new(0.85f, 0.89f, 0.88f);
        public static readonly Color Danger = new(0.66f, 0.24f, 0.27f);
        public static readonly Color Mint = new(0.42f, 0.79f, 0.72f);
        public static readonly Color Gold = new(0.95f, 0.80f, 0.40f);

        public static TMP_FontAsset Font { get; private set; }
        static readonly List<string> missing = new();

        // ── 공통 준비 ───────────────────────────────────────

        /// <summary>더미 한글 폰트를 열고 두 기능 폴더 코드의 글자를 미리 채운다.</summary>
        public static TMP_FontAsset LoadFont()
        {
            if (!AssetDatabase.IsValidFolder(Display)) AssetDatabase.CreateFolder("Assets/Scenes/Tests/Systems/DummySpaces", "Display");
            string path = Display + "/dummy-korean.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (asset == null)
            {
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
            }
            string text = string.Concat(new[] { "Assets/Scripts/Systems/DummySpaces", "Assets/Scripts/Systems/SpaceLink" }
                .SelectMany(folder => Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)).Select(File.ReadAllText));
            text += "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz → · ▶ ◀ ■ ≠ / [ ] : - + , . ( )";
            asset.TryAddCharacters(new string(text.Distinct().Where(c => !char.IsControl(c)).ToArray()), out _);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            Font = asset;
            missing.Clear();
            return asset;
        }

        /// <summary>찾지 못해 기본 도형으로 대체한 Synty 프리팹을 한 번에 알린다.</summary>
        public static void ReportMissing(string scene)
        {
            if (missing.Count > 0) Debug.LogWarning($"[DummySpaces] {scene}: Synty 프리팹 {missing.Count}개를 찾지 못해 기본 도형으로 대체 ({string.Join(", ", missing.Distinct())}). Assets/_ThirdParty를 받았는지 확인하세요.");
            missing.Clear();
        }

        public static void DestroyRoots(UnityEngine.SceneManagement.Scene scene, params string[] names)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (names.Contains(go.name)) Object.DestroyImmediate(go);
        }

        /// <summary>씬당 하나인 EventSystem을 기존 UI 입력 액션으로 만든다.</summary>
        public static GameObject EventSystem(string name)
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) throw new InvalidOperationException("예상하지 못한 기존 EventSystem이 있습니다.");
            var go = new GameObject(name, typeof(EventSystem), typeof(InputSystemUIInputModule));
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
            return go;
        }

        // ── Canvas 부품 ─────────────────────────────────────

        public static Canvas Canvas(string name, params Type[] extra)
        {
            var types = new List<Type> { typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster) };
            types.AddRange(extra);
            var root = new GameObject(name, types.ToArray());
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scale = root.GetComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1600, 900);
            scale.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
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

        /// <summary>오른쪽·아래 기준 배치(HUD 시계처럼 화면 모서리에 붙는 것).</summary>
        public static RectTransform Corner(RectTransform rect, Vector2 anchor)
        {
            var position = rect.anchoredPosition;
            var size = rect.sizeDelta;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = new Vector2(anchor.x > 0.5f ? -(1600 - position.x - size.x) : position.x, anchor.y < 0.5f ? 900 + position.y - size.y : position.y);
            return rect;
        }

        public static RectTransform Box(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var rect = Rect(name, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        public static TMP_Text Text(string name, Transform parent, string text, float x, float y, float width, float height, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var label = Rect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Font;
            label.fontSize = size;
            label.color = color;
            label.text = text;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.richText = true;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        public static Button Button(Transform parent, string name, string label, float x, float y, float width, float height, Color color, float size = 18)
        {
            var rect = Box(name, parent, x, y, width, height, color);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.82f, 0.94f, 0.91f);
            colors.pressedColor = new Color(0.62f, 0.80f, 0.75f);
            colors.disabledColor = new Color(0.62f, 0.65f, 0.66f, 0.45f);
            button.colors = colors;
            Text("Label", rect, label, 14, 0, width - 28, height, size, color == Line ? Ink : Color.white, TextAlignmentOptions.MidlineLeft);
            return button;
        }

        public static RectTransform Scroll(string name, Transform parent, float x, float y, float width, float height)
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

        public static TMP_Text FlowText(RectTransform parent, string name, string text, int size, Color color, int minHeight)
        {
            var label = Text(name, parent, text, 0, 0, parent.rect.width, minHeight, size, color);
            var element = label.gameObject.AddComponent<LayoutElement>();
            element.minHeight = minHeight;
            return label;
        }

        public static void Group(RectTransform parent, string label) => FlowText(parent, "Group", label, 15, Muted, 30);

        public static Button FlowButton(RectTransform parent, string name, string label, bool primary = false, bool destructive = false)
        {
            var button = Button(parent, name, label, 0, 0, 470, 46, destructive ? Danger : primary ? Teal : Line);
            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 46;
            return button;
        }

        public static TMP_InputField Input(Transform parent, string name, string value, float x, float y, float width, float height)
        {
            var rect = Box(name, parent, x, y, width, height, Color.white);
            rect.GetComponent<Image>().raycastTarget = true;
            var area = Rect("Text Area", rect, 10, 4, width - 20, height - 8);
            area.gameObject.AddComponent<RectMask2D>();
            var text = Text("Text", area, "", 0, 0, width - 20, height - 8, 18, Ink, TextAlignmentOptions.MidlineLeft);
            var field = rect.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.fontAsset = Font;
            field.text = value;
            return field;
        }

        /// <summary>왼쪽 슬라이드 패널: 오른쪽 가장자리 탭으로 열고 닫고, 위쪽 탭으로 페이지를 바꾼다.</summary>
        public static RectTransform SidePanel(Transform parent, string[] tabs, out UnityEngine.UI.Button toggle, out TMP_Text toggleLabel, out UnityEngine.UI.Button[] tabButtons, out GameObject[] pages)
        {
            var panel = Box("Side Panel", parent, 0, 76, 560, 824, Panel);
            panel.GetComponent<Image>().raycastTarget = true;
            toggle = Button(panel, "Panel Toggle", "", 560, 290, 46, 120, Teal);
            toggleLabel = toggle.GetComponentInChildren<TMP_Text>();
            var labelRect = toggleLabel.rectTransform;
            labelRect.anchoredPosition = new Vector2(0, 0);
            labelRect.sizeDelta = new Vector2(46, 120);
            toggleLabel.alignment = TextAlignmentOptions.Center;
            toggleLabel.fontSize = 16;
            toggleLabel.text = "◀\n접기";
            tabButtons = new UnityEngine.UI.Button[tabs.Length];
            pages = new GameObject[tabs.Length];
            float width = (528f - (tabs.Length - 1) * 8f) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                tabButtons[i] = Button(panel, "Tab-" + i, tabs[i], 16 + i * (width + 8), 12, width, 44, new Color(0.17f, 0.25f, 0.31f));
                var label = tabButtons[i].GetComponentInChildren<TMP_Text>();
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.anchoredPosition = Vector2.zero;
                label.rectTransform.sizeDelta = new Vector2(width, 44);
                pages[i] = Rect("Page-" + i, panel, 0, 64, 560, 760).gameObject;
                pages[i].SetActive(i == 0);
            }
            return panel;
        }

        // ── 3D 더미 공간 부품 ───────────────────────────────

        public static Material Lit(string name, Color color) => MaterialAt(name, color, "Universal Render Pipeline/Lit");
        public static Material Unlit(string name, Color color) => MaterialAt(name, color, "Universal Render Pipeline/Unlit");

        static Material MaterialAt(string name, Color color, string shader)
        {
            string path = $"{Display}/dummy-{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find(shader));
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        public static GameObject Primitive(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material, float rotationY = 0)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, rotationY, 0);
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Synty 프리팹을 참조로 놓는다. 없으면 대략 크기의 상자로 대체한다(기준점은 바닥 가운데).</summary>
        public static GameObject Prop(string path, Transform parent, Vector3 position, float rotationY = 0, float scale = 1, Vector3? fallbackSize = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab");
            GameObject go;
            if (prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.localPosition = position;
                go.transform.localRotation = Quaternion.Euler(0, rotationY, 0);
                go.transform.localScale = Vector3.one * scale;
                foreach (var collider in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                return go;
            }
            missing.Add(Path.GetFileName(path));
            var size = (fallbackSize ?? Vector3.one) * scale;
            go = Primitive(Path.GetFileName(path) + " (대체)", parent, PrimitiveType.Cube, position + Vector3.up * size.y * 0.5f, size, Lit("fallback", new Color(0.45f, 0.42f, 0.38f)), rotationY);
            return go;
        }

        /// <summary>[x0,x1]×[z0,z1]을 Synty 바닥 타일(2.5m, 기준점이 +x 모서리)로 채운다.</summary>
        public static void Floor(Transform parent, float x0, float x1, float z0, float z1, string tile = "SM_Bld_Base_Floor_01")
        {
            var root = new GameObject("Floor").transform;
            root.SetParent(parent, false);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Generic + tile + ".prefab") == null)
            {
                missing.Add(tile);
                Primitive("Floor (대체)", root, PrimitiveType.Cube, new Vector3((x0 + x1) / 2, -0.05f, (z0 + z1) / 2), new Vector3(x1 - x0, 0.1f, z1 - z0), Lit("floor", new Color(0.30f, 0.30f, 0.32f)));
                return;
            }
            for (float x = x0; x < x1 - 0.01f; x += Tile)
                for (float z = z0; z < z1 - 0.01f; z += Tile)
                    Prop(Generic + tile, root, new Vector3(x + Tile, 0, z));
        }

        /// <summary>벽 한 줄. alongX면 z 위치에 x방향으로, 아니면 x 위치에 z방향으로 놓는다. doorAt 칸은 큰 문 벽.</summary>
        public static void Wall(Transform parent, bool alongX, float fixedAt, float from, float to, int doorAt = -1, string tile = "SM_Bld_Base_Wall_01")
        {
            var root = new GameObject(alongX ? "Wall X" : "Wall Z").transform;
            root.SetParent(parent, false);
            int i = 0;
            for (float t = from; t < to - 0.01f; t += Tile, i++)
            {
                string piece = i == doorAt ? "SM_Bld_Base_Wall_Door_Large_01" : tile;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Generic + piece + ".prefab") == null)
                {
                    missing.Add(piece);
                    var center = alongX ? new Vector3(t + Tile / 2, 1.5f, fixedAt) : new Vector3(fixedAt, 1.5f, t + Tile / 2);
                    if (i != doorAt) Primitive("Wall (대체)", root, PrimitiveType.Cube, center, alongX ? new Vector3(Tile, 3, 0.2f) : new Vector3(0.2f, 3, Tile), Lit("wall", new Color(0.55f, 0.52f, 0.48f)));
                    continue;
                }
                // 타일은 기준점에서 -x로 2.5m. Y 90° 회전하면 +z로 2.5m가 된다.
                if (alongX) Prop(Generic + piece, root, new Vector3(t + Tile, 0, fixedAt));
                else Prop(Generic + piece, root, new Vector3(fixedAt, 0, t), 90);
            }
        }

        /// <summary>카메라를 바라보는 3D 글자 표지판.</summary>
        public static TextMeshPro Sign(Transform parent, string text, Vector3 position, Vector3 camera, float size = 3f, Color? color = null)
        {
            var go = new GameObject("Sign " + text.Split('\n')[0], typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.rotation = Quaternion.LookRotation(position - camera);
            var label = go.GetComponent<TextMeshPro>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color ?? Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            // outlineWidth를 직접 바꾸면 편집 중 재질 사본이 씬에 새므로 외곽선 재질 파일을 공유한다.
            label.fontSharedMaterial = SignMaterial();
            label.rectTransform.sizeDelta = new Vector2(12, 3);
            return label;
        }

        static Material SignMaterial()
        {
            string path = Display + "/dummy-korean-sign.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Font.material) { name = "Dummy Korean Sign" };
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat("_OutlineWidth", 0.2f);
            mat.SetColor("_OutlineColor", new Color(0.04f, 0.07f, 0.11f));
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        public static Light AddLight(Transform parent, string name, LightType type, Vector3 position, Vector3 euler, Color color, float intensity, float range = 12, float angle = 50)
        {
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            var light = go.GetComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.shadows = type == LightType.Directional ? LightShadows.Soft : LightShadows.None;
            return light;
        }

        /// <summary>화면 전체 원근 카메라로 공간을 비스듬히 내려다본다. 장면 조명은 평면 주변광 + 방향광.</summary>
        public static void FrameCamera(Camera camera, Vector3 position, Vector3 target, float fov = 50)
        {
            camera.transform.position = position;
            camera.transform.LookAt(target);
            camera.orthographic = false;
            // 왼쪽 패널이 열리면 화면 중심을 패널 오른쪽으로 옮기려고 물리 카메라의 lens shift를 쓴다(높이 기준 화각 유지).
            camera.usePhysicalProperties = true;
            camera.gateFit = Camera.GateFitMode.Vertical;
            camera.fieldOfView = fov;
            camera.rect = new UnityEngine.Rect(0, 0, 1, 1);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.06f, 0.09f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.43f, 0.47f);
        }
    }
}
