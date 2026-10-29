using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string ConcreteTextures = "Assets/_ThirdParty/LeartesStudios/AbandonedFactory/HDRP/Art/Textures/Concrete/";

        // An in-place look pass: keep the authored route, prefab geometry and gameplay intact.
        [MenuItem("Tools/CurtainCall/탈출로/중성 콘크리트 재질과 조명 적용")]
        public static void ApplyConcreteLook()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("escape_route 편집 모드가 필요합니다.");
            var background = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (background == null) throw new InvalidOperationException("탈출로 배경이 없습니다.");

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(ConcreteTextures + "T_Concrete_BC.TGA");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(ConcreteTextures + "T_Concrete_N.TGA");
            if (albedo == null || normal == null) throw new InvalidOperationException("기존 콘크리트 텍스처를 찾을 수 없습니다.");

            // Retain the material GUIDs already used by the scene; source assets stay untouched.
            ConfigureConcrete("escape-V4_Wall_DamagedConcrete", albedo, normal, new Color(.66f, .69f, .71f), .10f, .23f);
            ConfigureConcrete("escape-V4_Floor_WetConcrete", albedo, normal, new Color(.56f, .59f, .61f), .15f, .30f);

            foreach (string name in new[] { "ref-M_Pipe_Big", "ref-M_Pipe_Medium", "ref-M_WorldAlign_Bronze", "ref-M_Crane_Beam" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
                if (material == null) continue;
                Undo.RecordObject(material, "Mute escape infrastructure metal");
                material.SetTexture("_BaseMap", null);
                material.SetTexture("_MetallicGlossMap", null);
                material.DisableKeyword("_METALLICSPECGLOSSMAP");
                material.SetColor("_BaseColor", new Color(.20f, .22f, .23f));
                material.SetFloat("_Metallic", .55f);
                material.SetFloat("_Smoothness", .24f);
                material.SetFloat("_BumpScale", .18f);
                SaveLookMaterial(material);
            }

            var fixture = Mat("concrete-service-fixture", new Color(.19f, .21f, .22f));
            var bulb = Mat("concrete-service-bulb", new Color(.86f, .89f, .90f));
            Undo.RecordObject(bulb, "Neutral escape service bulb");
            bulb.SetColor("_BaseColor", new Color(.64f, .67f, .68f));
            bulb.SetColor("_EmissionColor", new Color(.65f, .68f, .70f));
            bulb.EnableKeyword("_EMISSION");
            SaveLookMaterial(fixture); SaveLookMaterial(bulb);
            foreach (var renderer in background.GetComponentsInChildren<Renderer>(true))
            {
                Material replacement = renderer.name.StartsWith("SM_Prop_Light_Industrial_Wall_01", StringComparison.Ordinal)
                    ? fixture : renderer.name == "작업등 전구" ? bulb : null;
                if (replacement == null) continue;
                Undo.RecordObject(renderer, "Neutral escape fixture material");
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => replacement).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }

            foreach (var light in background.GetComponentsInChildren<Light>(true))
            {
                bool service = light.name == "Warm service light" || light.name == "Neutral service light";
                bool deep = light.name == "Deep service light" || light.name == "Level depth light";
                if (!service && !deep) continue;
                Undo.RecordObject(light, "Neutral escape lighting");
                light.color = new Color(.94f, .97f, 1f);
                light.intensity = service ? 9f : light.name == "Level depth light" ? 1f : .35f;
                light.range = (service ? 20f : 9f) * background.transform.lossyScale.x;
                if (service) { Undo.RecordObject(light.gameObject, "Name neutral escape lighting"); light.name = "Neutral service light"; }
            }

            // Sparse upper fill reveals the concrete planes without illuminating the abyss floor.
            var lighting = background.transform.Find("12_ConcreteLighting");
            if (lighting == null)
            {
                var group = new GameObject("12_ConcreteLighting");
                Undo.RegisterCreatedObjectUndo(group, "Concrete upper lighting");
                group.transform.SetParent(background.transform, false);
                lighting = group.transform;
            }
            Vector3[] fills = {
                new Vector3(12, 15, 32), new Vector3(12, 15, 57), new Vector3(38, 18, 61),
                new Vector3(54, 15, 32), new Vector3(82, 18, 21), new Vector3(102, 15, 54),
                new Vector3(133, 12, 62), new Vector3(149, 12, 42), new Vector3(155, 14, 16)
            };
            for (int i = 0; i < fills.Length; i++)
            {
                var child = lighting.Find("Concrete upper fill " + i);
                Light light;
                if (child == null)
                {
                    var go = new GameObject("Concrete upper fill " + i, typeof(Light));
                    Undo.RegisterCreatedObjectUndo(go, "Concrete upper fill");
                    go.transform.SetParent(lighting, false);
                    light = go.GetComponent<Light>();
                }
                else light = child.GetComponent<Light>();
                Undo.RecordObject(light, "Concrete upper fill");
                Undo.RecordObject(light.transform, "Concrete upper fill position");
                light.transform.localPosition = fills[i];
                light.type = LightType.Point;
                light.color = new Color(.94f, .97f, 1f);
                light.intensity = 24f;
                light.range = 29f * background.transform.lossyScale.x;
                light.shadows = LightShadows.None;
            }

            var sun = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>())
                .FirstOrDefault(l => l.type == LightType.Directional && l.name == "Directional Light");
            if (sun != null)
            {
                Undo.RecordObject(sun, "Neutral escape daylight");
                sun.color = new Color(1f, .99f, .95f);
                sun.intensity = 1.65f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = .9f;
            }
            // Ambient is deliberately close to neutral; blue tint must not replace the orange cast.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.21f, .22f, .23f);
            ApplySunnyExteriorSky();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.43f, .48f, .50f);
            RenderSettings.fogStartDistance = 65f;
            RenderSettings.fogEndDistance = 450f;
            foreach (var label in background.GetComponentsInChildren<TextMeshPro>(true))
            {
                Undo.RecordObject(label, "Neutral escape labels");
                label.color = new Color(.82f, .88f, .91f);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void ConfigureConcrete(string name, Texture2D albedo, Texture2D normal, Color tint, float bump, float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
            if (material == null) throw new InvalidOperationException("탈출로 전용 재질이 없습니다: " + name);
            Undo.RecordObject(material, "Cast concrete escape material");
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_MetallicGlossMap", null);
            material.SetTexture("_OcclusionMap", null);
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_OCCLUSIONMAP");
            material.EnableKeyword("_NORMALMAP");
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_BumpScale", bump);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            SaveLookMaterial(material);
        }

        static void SaveLookMaterial(Material material)
        {
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }

        // Enclosed halls keep the dim neutral fill above; the open reed field gets direct sun.
        static void ApplySunnyExteriorSky()
        {
            const string path = Materials + "escape-sunny-exterior.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural")) { name = "escape-sunny-exterior" };
                AssetDatabase.CreateAsset(sky, path);
            }
            Undo.RecordObject(sky, "Sunny escape exterior");
            sky.SetColor("_SkyTint", new Color(.48f, .55f, .65f));
            sky.SetColor("_GroundColor", new Color(.38f, .42f, .35f));
            sky.SetFloat("_Exposure", 1.2f);
            sky.SetFloat("_AtmosphereThickness", 1f);
            sky.SetFloat("_SunSize", .035f);
            SaveLookMaterial(sky);
            RenderSettings.skybox = sky;
            RenderSettings.reflectionIntensity = .25f;
        }
    }
}
