using System;
using System.IO;
using System.Linq;
using CurtainCall.Fonts.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CurtainCall.StartLobby.EditorTools
{
    /// <summary>
    /// 시작·로비 씬 빌더의 부품(Task-052). 화면 스타일: 어두운 무대 뒤 + 붉은 벨벳 커튼 + 금빛 조명, 메뉴는 핵심만 글자 버튼으로.
    /// 재질은 <see cref="Display"/>(씬 폴더)에 두고, 글자는 프로젝트 한글 Static 폰트(Pretendard)를 쓴다.
    /// </summary>
    public static class StartLobbyKit
    {
        public const string Folder = "Assets/Scenes/Start_Lobby";
        public const string Display = Folder + "/Display";
        public const string FontPath = "Assets/Resources/Fonts/Pretendard-Medium/Pretendard-Medium SDF.asset";
        public const string Props = "Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/Props/";
        public const string GenericProps = "Assets/_ThirdParty/Synty/PolygonGeneric/Prefabs/Props/";

        // 무대 색: 벨벳 빨강, 금빛, 크림, 어두운 무대 뒤
        public static readonly Color Velvet = new(0.47f, 0.05f, 0.09f);
        public static readonly Color VelvetDark = new(0.28f, 0.02f, 0.05f);
        public static readonly Color Gold = new(0.95f, 0.80f, 0.40f);
        public static readonly Color Cream = new(0.98f, 0.93f, 0.82f);
        public static readonly Color Dim = new(0.62f, 0.56f, 0.50f);
        public static readonly Color Night = new(0.035f, 0.025f, 0.03f);
        public static readonly Color Glass = new(0.05f, 0.03f, 0.035f, 0.88f);
        public static readonly Color Crimson = new(0.78f, 0.06f, 0.18f);

        public static TMP_FontAsset Font { get; private set; }

        /// <summary>폰트를 열고, 코드에 새 글자가 있으면 Static으로 다시 굽는다(함정 tmp-dynamic-font-churn).</summary>
        public static void Prepare()
        {
            if (!AssetDatabase.IsValidFolder(Display)) AssetDatabase.CreateFolder(Folder, "Display");
            Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) ?? throw new InvalidOperationException("한글 폰트가 없습니다: " + FontPath);
            string code = StaticFontBaker.CodeCharacters();
            if (!code.All(c => Font.HasCharacter(c))) StaticFontBaker.Bake(Font, code);
        }

        // ── 3D ──────────────────────────────────────────────

        public static Material Mat(string name, Color color, float smoothness = 0.25f, float metallic = 0f, Color? emission = null)
        {
            string path = $"{Display}/lobby-{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Transform Group(string name, Transform parent, Vector3 position = default, float rotationY = 0f)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = Quaternion.Euler(0, rotationY, 0);
            return t;
        }

        /// <summary>기본 도형. <paramref name="solid"/>면 콜라이더를 남겨 부딪힌다.</summary>
        public static GameObject Shape(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool solid = false, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = scale;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        /// <summary>보이지 않는 벽·바닥(콜라이더만).</summary>
        public static void Blocker(string name, Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name, typeof(BoxCollider));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.GetComponent<BoxCollider>().size = size;
        }

        /// <summary>Synty 프리팹을 참조로 놓는다. 원래 콜라이더는 떼고, <paramref name="solid"/>면 겉 크기 상자 콜라이더를 붙인다. 없으면 상자로 대체.</summary>
        public static GameObject Prop(string path, Transform parent, Vector3 position, float rotationY = 0f, float scale = 1f, bool solid = false, Vector3? fallback = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[StartLobby] Synty 프리팹 없음 → 상자로 대체: {Path.GetFileName(path)} (Assets/_ThirdParty를 받았는지 확인)");
                var size = (fallback ?? Vector3.one) * scale;
                return Shape(Path.GetFileName(path) + " (대체)", parent, PrimitiveType.Cube, position + Vector3.up * size.y * 0.5f, size, Mat("fallback", new Color(0.4f, 0.32f, 0.26f)), solid, new Vector3(0, rotationY, 0));
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, rotationY, 0);
            go.transform.localScale = Vector3.one * scale;
            foreach (var collider in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            if (solid) AddBoundsCollider(go);
            return go;
        }

        static void AddBoundsCollider(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(bounds.center);
            Vector3 s = go.transform.lossyScale;
            box.size = new Vector3(bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(s.x)), bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(s.y)), bounds.size.z / Mathf.Max(0.001f, Mathf.Abs(s.z)));
        }

        /// <summary>
        /// 벨벳 커튼 한 폭: 세로 원기둥을 촘촘히 세워 주름을 만든다. 기준점은 바닥 가운데. 돌려받은 Transform을 옮기면 커튼이 열린다.
        /// </summary>
        public static Transform Curtain(string name, Transform parent, Vector3 center, float width, float height, Material velvet, Material shade)
        {
            var root = Group(name, parent, center);
            const float fold = 0.34f;
            int count = Mathf.Max(1, Mathf.RoundToInt(width / fold));
            for (int i = 0; i < count; i++)
            {
                float x = -width / 2 + fold * (i + 0.5f);
                float depth = (i % 2 == 0) ? 0f : 0.09f;
                Shape("Fold", root, PrimitiveType.Cylinder, new Vector3(x, height / 2, depth), new Vector3(fold * 1.15f, height / 2, 0.26f), i % 2 == 0 ? velvet : shade);
            }
            return root;
        }

        public static Light AddLight(Transform parent, string name, LightType type, Vector3 position, Vector3 lookAt, Color color, float intensity, float range = 12f, float angle = 45f, bool shadows = false)
        {
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            if (lookAt != position) go.transform.LookAt(parent != null ? parent.TransformPoint(lookAt) : lookAt);
            var light = go.GetComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.55f;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return light;
        }

        /// <summary>월드 글자(TextMeshPro). <paramref name="facing"/> 쪽(보통 카메라)을 향한다.</summary>
        public static TextMeshPro Label3D(string name, Transform parent, string text, Vector3 position, Vector3 facing, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center, float width = 6f, float height = 1.2f)
        {
            var go = new GameObject(name, typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            Vector3 world = go.transform.position;
            go.transform.rotation = Quaternion.LookRotation(world - facing);
            var label = go.GetComponent<TextMeshPro>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(width, height);
            return label;
        }

        public static Camera MainCamera(Vector3 position, Vector3 lookAt, float fov)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            go.transform.position = position;
            go.transform.LookAt(lookAt);
            var camera = go.GetComponent<Camera>();
            camera.fieldOfView = fov;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Night;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120f;
            return camera;
        }

        public static void Ambient(Color color)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = color;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; // 하늘 반사로 바닥이 푸르게 뜨지 않게
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0.2f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Night;
            RenderSettings.fogStartDistance = 14f;
            RenderSettings.fogEndDistance = 40f;
        }

        // ── Canvas ──────────────────────────────────────────

        public static RectTransform Canvas(string name, out Canvas canvas)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scale = root.GetComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1600, 900);
            scale.matchWidthOrHeight = 0.5f;
            return (RectTransform)root.transform;
        }

        /// <summary>앵커 한 점 기준 사각형. anchor·pivot은 (0,0)=왼쪽 아래, (1,1)=오른쪽 위.</summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        public static Image Fill(RectTransform rt, Color color, bool raycast = false)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static TMP_Text Text(string name, Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Left, Vector2? pivot = null)
        {
            var rt = Rect(name, parent, anchor, position, size, pivot);
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        /// <summary>글자 메뉴 버튼: 평소엔 글자만, 올리면 금빛 띠. 왼쪽 정렬.</summary>
        public static Button MenuButton(string name, Transform parent, string label, Vector2 anchor, Vector2 position, float width, float fontSize = 30f, Vector2? pivot = null)
        {
            var rt = Rect(name, parent, anchor, position, new Vector2(width, fontSize * 1.75f), pivot);
            var image = Fill(rt, Color.white, true);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = new Color(0, 0, 0, 0),
                highlightedColor = new Color(Gold.r, Gold.g, Gold.b, 0.2f),
                pressedColor = new Color(Gold.r, Gold.g, Gold.b, 0.36f),
                selectedColor = new Color(0, 0, 0, 0),
                disabledColor = new Color(0, 0, 0, 0),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
            var text = Text("Label", rt, label, new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(width - 24, fontSize * 1.5f), fontSize, Cream, TextAlignmentOptions.Left, new Vector2(0, 0.5f));
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        /// <summary>채운 버튼(확인·참가 등).</summary>
        public static Button SolidButton(string name, Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size, Color color, float fontSize = 22f, Vector2? pivot = null)
        {
            var rt = Rect(name, parent, anchor, position, size, pivot);
            var image = Fill(rt, color, true);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(1.15f, 1.1f, 1.05f);
            colors.disabledColor = new Color(0.45f, 0.42f, 0.42f, 0.6f);
            button.colors = colors;
            var text = Text("Label", rt, label, new Vector2(0.5f, 0.5f), Vector2.zero, size, fontSize, Cream, TextAlignmentOptions.Center);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        public static TMP_InputField Input(string name, Transform parent, string value, string placeholder, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources { inputField = Sprite("UI/Skin/InputFieldBackground.psd") });
            go.name = name;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.12f, 0.08f, 0.08f, 0.95f);
            var field = go.GetComponent<TMP_InputField>();
            field.fontAsset = Font;
            field.pointSize = 24f;
            var text = (TextMeshProUGUI)field.textComponent;
            text.font = Font;
            text.fontSize = 24f;
            text.color = Cream;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var hint = (TextMeshProUGUI)field.placeholder;
            hint.font = Font;
            hint.fontSize = 22f;
            hint.text = placeholder;
            hint.color = new Color(Dim.r, Dim.g, Dim.b, 0.8f);
            hint.alignment = TextAlignmentOptions.MidlineLeft;
            field.caretColor = Gold;
            field.selectionColor = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
            field.text = value;
            return field;
        }

        public static Slider Slider(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            var go = DefaultControls.CreateSlider(new DefaultControls.Resources
            {
                standard = Sprite("UI/Skin/UISprite.psd"),
                background = Sprite("UI/Skin/Background.psd"),
                knob = Sprite("UI/Skin/Knob.psd"),
            });
            go.name = name;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            go.transform.Find("Background").GetComponent<Image>().color = new Color(0.2f, 0.13f, 0.13f);
            go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = Crimson;
            go.transform.Find("Handle Slide Area/Handle").GetComponent<Image>().color = Gold;
            return slider;
        }

        static Sprite Sprite(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        /// <summary>설정 화면(시작·로비 공용). 가운데 패널.</summary>
        public static SettingsPanel Settings(Transform parent)
        {
            var dim = Stretch("Settings", parent);
            Fill(dim, new Color(0, 0, 0, 0.55f), true);
            var panel = Rect("Panel", dim, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 520));
            Fill(panel, Glass, true);
            Rule(panel, new Vector2(0.5f, 1f), new Vector2(0, -2), 640);
            Text("Heading", panel, "설정", new Vector2(0, 1), new Vector2(48, -36), new Vector2(400, 50), 36, Gold, TextAlignmentOptions.Left, new Vector2(0, 1));
            var view = dim.gameObject.AddComponent<SettingsPanel>();

            Text("VolumeLabel", panel, "전체 음량", new Vector2(0, 1), new Vector2(48, -126), new Vector2(200, 36), 24, Cream, TextAlignmentOptions.Left, new Vector2(0, 1));
            view.volume = Slider("Volume", panel, new Vector2(0, 1), new Vector2(250, -130), new Vector2(240, 28), new Vector2(0, 1));
            view.volumeValue = Text("VolumeValue", panel, "80%", new Vector2(1, 1), new Vector2(-48, -126), new Vector2(100, 36), 24, Gold, TextAlignmentOptions.Right, new Vector2(1, 1));

            Text("ScreenLabel", panel, "화면 모드", new Vector2(0, 1), new Vector2(48, -196), new Vector2(200, 36), 24, Cream, TextAlignmentOptions.Left, new Vector2(0, 1));
            view.screenMode = SolidButton("ScreenMode", panel, "전체 화면", new Vector2(1, 1), new Vector2(-48, -190), new Vector2(260, 46), VelvetDark, 22, new Vector2(1, 1));
            view.screenModeLabel = view.screenMode.GetComponentInChildren<TMP_Text>();

            Text("ControlsLabel", panel, "조작", new Vector2(0, 1), new Vector2(48, -270), new Vector2(200, 36), 24, Cream, TextAlignmentOptions.Left, new Vector2(0, 1));
            Text("Controls", panel, "WASD 이동   ·   Space 점프   ·   F 상호작용   ·   ESC 메뉴", new Vector2(0, 1), new Vector2(48, -310), new Vector2(544, 80), 20, Dim, TextAlignmentOptions.TopLeft, new Vector2(0, 1));

            view.close = SolidButton("Close", panel, "닫기", new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(220, 52), Crimson, 24, new Vector2(0.5f, 0));
            return view;
        }

        /// <summary>금빛 가는 줄(장식).</summary>
        public static void Rule(Transform parent, Vector2 anchor, Vector2 position, float width)
        {
            var rt = Rect("Rule", parent, anchor, position, new Vector2(width, 3));
            Fill(rt, new Color(Gold.r, Gold.g, Gold.b, 0.85f));
        }
    }
}
