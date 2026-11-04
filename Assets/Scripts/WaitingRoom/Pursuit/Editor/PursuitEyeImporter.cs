using System.IO;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public static class PursuitEyeImporter
    {
        const string Destination = "Assets/Scripts/WaitingRoom/Pursuit/Resources/PursuitEye/Frames";
        const string Source = "Tasks/Active/Task-20261104-101-pursuit-eye-art/Artifacts";

        [MenuItem("Tools/Curtain Call/Pursuit/Import Eye Sprites")]
        public static void Import()
        {
            EnsureFolder(Destination);
            for (int i = 0; i < 8; i++) {
                string source = $"{Source}/eye-{i:00}.png", path = $"{Destination}/Eye{i:00}.png";
                // Preserve existing GUIDs on a deliberate reimport.
                File.Copy(source, path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 256;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                importer.SetTextureSettings(settings); importer.SaveAndReimport();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null || sprite.rect.size != new Vector2(256,256)) throw new System.InvalidOperationException("Invalid eye sprite: " + path);
            }
            var rules = GameSettings.WaitingRoom;
            rules.Pursuit.Eyes ??= new PursuitEyeSettings();
            EditorUtility.SetDirty(rules); AssetDatabase.SaveAssetIfDirty(rules);
            Debug.Log("Pursuit eyes: 8 white sprites imported; settings saved. Runtime attachment is automatic on PursuitDirector.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\','/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
