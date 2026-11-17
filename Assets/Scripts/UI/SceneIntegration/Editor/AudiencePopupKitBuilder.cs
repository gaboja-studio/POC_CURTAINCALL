#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    /// <summary>Assembles GPT-generated transparent art layers; live numbers remain TMP text.</summary>
    public static class AudiencePopupKitBuilder
    {
        public const string KitPath = "Assets/Resources/Prefabs/UIs/Roles/Retained/Feedback/AudiencePopupKit.prefab";
        const string Art = "Assets/_ThirdParty/AudienceReactionsTask144/PopupKit/";
        public static void Apply(NumericFeedbackView view)
        {
            var root = (RectTransform)view.transform;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0, 1);
            root.anchoredPosition = new Vector2(218, -96); root.sizeDelta = new Vector2(184, 104);
            var fill = Layer(view.transform, "Background", Import("ReactionFill"), 0);
            var frame = Layer(view.transform, "Frame", Import("ReactionFrame"), 1);
            foreach (var label in view.GetComponentsInChildren<TMP_Text>(true))
            {
                var rect = label.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
                label.alignment = TextAlignmentOptions.Center;
                rect.pivot = new Vector2(.5f, .5f);
                if (label.name == "Handhold") rect.anchoredPosition = new Vector2(0, -180);
                else if (label.name != "Subject") rect.anchoredPosition = new Vector2(0, -65);
            }
            view.ConfigureSkin(fill, frame); view.ResetForRuntime();
            // Export a reusable kit without replacing the existing live feedback/audio hierarchy.
            PrefabUtility.SaveAsPrefabAsset(view.gameObject, KitPath);
        }
        static Sprite Import(string name)
        {
            string path = Art + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Missing generated art: " + path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 512;
            importer.spritePixelsPerUnit = 100; importer.spriteBorder = new Vector4(170, 160, 170, 160);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static UnityEngine.UI.Image Layer(Transform parent, string name, Sprite sprite, int order)
        {
            var child = parent.Find(name);
            if (child == null) { child = new GameObject(name, typeof(RectTransform)).transform; child.SetParent(parent, false); }
            var image = child.GetComponent<UnityEngine.UI.Image>();
            if (image == null) image = child.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite; image.type = UnityEngine.UI.Image.Type.Sliced; image.pixelsPerUnitMultiplier = 12;
            image.raycastTarget = false;
            var rect = image.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; child.SetSiblingIndex(order);
            return image;
        }
    }
}
#endif
