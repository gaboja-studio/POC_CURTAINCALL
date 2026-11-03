using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    /// <summary>A 한정 공장 외형 통합. 원본 패키지·게임 판정은 보존한다.</summary>
    public static partial class EscapeFactoryArtPass
    {
        const string ScenePath = "Assets/Scenes/Levels/escape_route.unity";
        const string Output = "Assets/Scenes/Levels/EscapeFactoryArt";
        const string Private = "Assets/_ThirdParty/Task073/FactoryTextures";
        const string MeshOutput = "Assets/_ThirdParty/Task073/FactoryMeshes";
        const string Abandoned = "Assets/_ThirdParty/LeartesStudios/AbandonedFactory/HDRP/Art/";
        const string Clock = "Assets/_ThirdParty/LeartesStudios/HDRP/Art/";
        static readonly Dictionary<Material, Material> materials = new();
        static readonly Dictionary<Texture, Texture> textures = new();
        static readonly Dictionary<string, Mesh> meshes = new();
        static readonly List<string> report = new();
        static Material concrete, floor, recess, steel, yellow, pipe;
        static int copies, replacements, removed, batches;
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
        static Texture LightTexture(Texture original)
        {
            if (original == null) return null;
            if (textures.TryGetValue(original, out var result)) return result;
            string source = AssetDatabase.GetAssetPath(original);
            var importer = AssetImporter.GetAtPath(source) as TextureImporter;
            if (importer == null) return original;
            // 원본 설정을 보존하고 실제 사용할 텍스처만 비공개 폴더에 경량 파생본을 만든다.
            string dest = Private + "/" + AssetDatabase.AssetPathToGUID(source).Substring(0, 8) + "-" + Path.GetFileName(source);
            if (!File.Exists(dest)) { if (!AssetDatabase.CopyAsset(source, dest)) throw new InvalidOperationException(source); copies++; }
            var target = (TextureImporter)AssetImporter.GetAtPath(dest);
            target.maxTextureSize = 1024;
            target.textureCompression = TextureImporterCompression.CompressedHQ;
            target.isReadable = false; target.mipmapEnabled = true;
            target.streamingMipmaps = true;
            var platform = target.GetPlatformTextureSettings("Standalone"); platform.overridden = false;
            target.SetPlatformTextureSettings(platform);
            target.SaveAndReimport();
            result = AssetDatabase.LoadAssetAtPath<Texture>(dest);
            textures.Add(original, result);
            return result;
        }
        static Material Pbr(string name, string albedo, string normal, Color tint, float metal, float smooth)
        {
            string path = Output + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", tint); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
            m.SetTexture("_BaseMap", LightTexture(AssetDatabase.LoadAssetAtPath<Texture>(albedo)));
            var n = LightTexture(AssetDatabase.LoadAssetAtPath<Texture>(normal));
            m.SetTexture("_BumpMap", n); if (n != null) m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_BumpScale", .7f); m.enableInstancing = true;
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m); return m;
        }
        static void PrepareMaterials()
        {
            string c = Abandoned + "Textures/Concrete/T_Concrete_";
            string damaged = Abandoned + "Textures/DamagedConcrete/T_DamagedConcrete_";
            string rust = Abandoned + "Textures/Rustmetal/T_Rustymetal_";
            concrete = Pbr("factory-concrete", c + "BC.TGA", c + "N.TGA", new Color(.76f,.77f,.75f), 0,.18f);
            floor = Pbr("factory-floor", damaged + "BC.TGA", damaged + "N.TGA", new Color(.66f,.68f,.66f), 0,.23f);
            recess = Pbr("factory-recess", c + "BC.TGA", c + "N.TGA", new Color(.37f,.40f,.40f), 0,.12f);
            steel = Pbr("factory-steel", rust + "BC.TGA", rust + "N.TGA", new Color(.68f,.72f,.72f), .65f,.26f);
            yellow = Pbr("factory-safety-ochre", rust + "BC.TGA", rust + "N.TGA", new Color(.68f,.46f,.13f), .4f,.22f);
            pipe = Pbr("factory-pipe", rust + "BC.TGA", rust + "N.TGA", new Color(.44f,.50f,.48f), .7f,.27f);
        }
        static Material Convert(Material source)
        {
            if (source == null) return null;
            if (materials.TryGetValue(source, out var cached)) return cached;
            Material target = source;
            switch (source.name)
            {
                case "brutal-concrete": case "working-concrete-repair": case "atmosphere-deep-concrete": target = concrete; break;
                case "escape-V4_Floor_WetConcrete": target = floor; break;
                case "brutal-recess": target = recess; break;
                case "working-steel": case "brutal-steel": case "vertical-dark-steel": case "ref-M_Crane_Beam": case "vertical-machine": target = steel; break;
                case "working-faded-ochre": target = yellow; break;
                case "working-pipe": case "atmosphere-pressure-vessel": target = pipe; break;
            }
            if (target == source && AssetDatabase.GetAssetPath(source).StartsWith("Assets/_ThirdParty/LeartesStudios/", StringComparison.Ordinal))
            {
                string key = "source-" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)).Substring(0,8);
                string baseMap = source.HasProperty("_BaseMap") ? AssetDatabase.GetAssetPath(source.GetTexture("_BaseMap")) : "";
                string normal = new[] { "_BumpMap", "_NormalMap", "_Normal" }.Where(source.HasProperty).Select(p => AssetDatabase.GetAssetPath(source.GetTexture(p))).FirstOrDefault(p => !string.IsNullOrEmpty(p)) ?? "";
                bool masonry = source.name.Contains("Concrete") || source.name.Contains("Buildings");
                target = Pbr(key, baseMap, normal, Color.white, masonry ? 0 : .55f, .25f);
            }
            materials[source] = target; return target;
        }
        static bool Concrete(Material m) => m == concrete || m == floor || m == recess;
    }
}
