using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    /// <summary>
    /// 상점 프리팹 설명 칸에 신체 복구 인체 픽토그램(<see cref="LimbBookingFigure"/>)을 넣는다(2026-11-26 M3 A안).
    /// - 그림: LimbFigure 폴더의 흰색 레이어 6장(머리·몸통·팔 둘·다리 둘, 같은 크기 캔버스 160×256). 색은 게임이 부위마다 입힌다.
    ///   limb-spot(뒤 조명)·limb-floor(발밑 그림자)·limb-dot(범례 점)도 같은 폴더.
    /// - 다시 실행하면 07_LimbFigure를 새로 만든다(배치를 손으로 고쳤다면 다시 실행하지 말 것).
    /// </summary>
    public static class LimbBookingFigureBuilder
    {
        const string PrefabPath = "Assets/Resources/Prefabs/UIs/Roles/Retained/ShopUi/ShopPanel.prefab";
        const string ArtFolder = "Assets/Resources/Prefabs/UIs/Roles/Retained/ShopUi/LimbFigure";
        const string ContentPath = "60_Panels/W2_Shop/00_Visuals/10_LayoutStage/00_ShopPanel/40_Content/40_DetailTooltip/40_Content";
        const string NodeName = "07_LimbFigure";

        [MenuItem("Tools/Curtain Call/상점/신체 복구 인체 그림 넣기")]
        public static void Install()
        {
            ImportSprites();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var content = root.transform.Find(ContentPath) ?? throw new FileNotFoundException("상점 설명 칸을 찾지 못함: " + ContentPath);
                var old = content.Find(NodeName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var title = content.Find("01_TitleTMP").GetComponent<TextMeshProUGUI>();

                // 배치(설명 칸 기준): 왼쪽 세로 한 줄 = 조명 + 인체 그림 + 발밑 그림자, 오른쪽 위 = 색 설명·안내. 설명 글은 그 아래(LimbBookingFigure.descriptionInset).
                var node = Rect(NodeName, content, new Vector2(24, -62), new Vector2(328, 243));
                node.SetSiblingIndex(content.Find("02_DescriptionViewport").GetSiblingIndex());
                Image Fx(string name, string sprite, Vector2 position, Vector2 size, Color color)
                {
                    var image = Rect(name, node, position, size).gameObject.AddComponent<Image>();
                    image.sprite = Sprite(sprite); image.color = color; image.raycastTarget = false;
                    return image;
                }
                Fx("Spotlight", "limb-spot", new Vector2(0, -6), new Vector2(118, 232), new Color(.62f, .82f, .84f, .20f));
                Fx("FloorShadow", "limb-floor", new Vector2(20, -222), new Vector2(80, 16), new Color(0, 0, 0, .55f));
                // 그림 캔버스(160×256)는 좌우 여백이 있어 영역보다 넓게 둔다(몸은 왼쪽 세로 줄 안에 들어옴).
                var figure = Rect("Figure", node, new Vector2(-14, -2), new Vector2(147, 236));
                Image Layer(string name)
                {
                    var image = Rect(name, figure, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
                    var rt = (RectTransform)image.transform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                    image.sprite = Sprite("limb-" + name); image.preserveAspect = true; image.raycastTarget = false;
                    var shadow = image.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = new Color(0, 0, 0, .45f); shadow.effectDistance = new Vector2(1.5f, -1.5f);
                    return image;
                }
                var parts = new[] { Layer("leg-right"), Layer("leg-left"), Layer("arm-right"), Layer("arm-left"), Layer("torso"), Layer("head") };

                var legend = Rect("Legend", node, new Vector2(118, -6), new Vector2(210, 122));
                var dots = new Image[3];
                string[] labels = { "이번에 복구", "복구 안 함", "원래 있음" };
                TextMeshProUGUI Label(string name, Transform parent, Vector2 position, Vector2 size, string value, float fontSize, Color color)
                {
                    var text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
                    text.font = title.font; text.fontSize = fontSize; text.text = value; text.raycastTarget = false; text.color = color;
                    text.alignment = TextAlignmentOptions.MidlineLeft; text.textWrappingMode = TextWrappingModes.Normal;
                    return text;
                }
                for (int i = 0; i < 3; i++)
                {
                    var row = Rect("Row" + i, legend, new Vector2(0, -30 * i), new Vector2(210, 28));
                    dots[i] = Rect("Dot", row, new Vector2(0, -6), new Vector2(16, 16)).gameObject.AddComponent<Image>();
                    dots[i].sprite = Sprite("limb-dot"); dots[i].preserveAspect = true; dots[i].raycastTarget = false;
                    Label("LabelTMP", row, new Vector2(26, 0), new Vector2(184, 28), labels[i], 19, new Color(.86f, .90f, .89f));
                }
                // 개수 버튼 안내(고정 문구: 여기 프리팹에서 고침).
                Label("HintTMP", legend, new Vector2(0, -90), new Vector2(210, 36), "아래 '팔·다리' 버튼으로\n하나씩 줄일 수 있어요", 15, new Color(.62f, .70f, .70f));

                var view = node.gameObject.AddComponent<LimbBookingFigure>();
                var so = new SerializedObject(view);
                so.FindProperty("rightLeg").objectReferenceValue = parts[0];
                so.FindProperty("leftLeg").objectReferenceValue = parts[1];
                so.FindProperty("rightArm").objectReferenceValue = parts[2];
                so.FindProperty("leftArm").objectReferenceValue = parts[3];
                so.FindProperty("torso").objectReferenceValue = parts[4];
                so.FindProperty("head").objectReferenceValue = parts[5];
                so.FindProperty("descriptionViewport").objectReferenceValue = content.Find("02_DescriptionViewport");
                so.FindProperty("descriptionInset").vector2Value = new Vector2(112, 138);
                var list = so.FindProperty("legendDots"); list.arraySize = 3;
                for (int i = 0; i < 3; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = dots[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                // 편집용 미리보기 색(게임에서는 상태에 맞춰 다시 칠함): 왼팔·오른다리 복구, 왼다리 복구 안 함.
                Color green = new(.42f, .86f, .54f), red = new(.93f, .37f, .33f), white = new(.86f, .90f, .89f);
                foreach (var part in parts) part.color = white;
                parts[3].color = parts[0].color = green; parts[1].color = red;
                dots[0].color = green; dots[1].color = red; dots[2].color = white;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("[상점] 신체 복구 인체 그림을 상점 프리팹 설명 칸에 넣음: " + ContentPath + "/" + NodeName);
        }

        static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = position; rt.sizeDelta = size;
            return rt;
        }

        static Sprite Sprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + name + ".png") ?? throw new FileNotFoundException(ArtFolder + "/" + name + ".png");

        static void ImportSprites()
        {
            foreach (var path in Directory.GetFiles(ArtFolder, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                // 작게 줄여 보이는 그림이라 밉맵(삼선형)으로 가는 선·가장자리를 매끈하게.
                if (importer.textureType == TextureImporterType.Sprite && importer.mipmapEnabled && importer.filterMode == FilterMode.Trilinear) continue;
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = true; importer.filterMode = FilterMode.Trilinear;
                importer.SaveAndReimport();
            }
        }
    }
}
