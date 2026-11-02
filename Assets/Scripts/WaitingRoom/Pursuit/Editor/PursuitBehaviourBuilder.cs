using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CurtainCall.Damage;
using Unity.Behavior;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public static class PursuitBehaviourBuilder
    {
        const string Folder = "Assets/_ThirdParty/curtaincall/Pursuit/Behaviour";
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static Type TypeNamed(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Flags).Invoke(obj, args);
        static object Get(object obj, string property) => obj.GetType().GetProperty(property, Flags).GetValue(obj);
        static object FirstPort(object node, string property) => ((IEnumerable)Get(node, property)).Cast<object>().First();

        [MenuItem("Tools/Curtain Call/Pursuit/Build Behaviour and Capture Effects")]
        public static void Build()
        {
            var director = UnityEngine.Object.FindFirstObjectByType<PursuitDirector>();
            if (director == null || director.gameObject.scene.path != PursuitAuthoring.ScenePath) throw new InvalidOperationException("Open the pursuit test scene first.");
            EnsureFolder(Folder);
            director.BehaviourGraph = BuildGraph();
            BuildBlood(out var prefab, out var sprite); director.CaptureBloodPrefab = prefab; director.CaptureBloodSprite = sprite;
            EditorUtility.SetDirty(director); EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            EditorSceneManager.SaveScene(director.gameObject.scene);
            Debug.Log("Pursuit Behavior graph and capture blood assets wired.");
        }
        static BehaviorGraph BuildGraph()
        {
            string path = Folder + "/PursuitBehaviour.asset";
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null) return (BehaviorGraph)Call(asset, "BuildRuntimeGraph", true);
            // Behavior 1.0.16 exposes runtime APIs publicly but keeps the graph authoring API internal.
            // Reflection is confined to this one-time editor builder; the game uses the compiled graph.
            var authorType = TypeNamed("Unity.Behavior.BehaviorAuthoringGraph");
            var graph = ScriptableObject.CreateInstance(authorType);
            AssetDatabase.CreateAsset(graph, path);
            Call(graph, "ValidateAsset");
            ((IList)Get(graph, "Nodes")).Clear();
            var registry = authorType.Assembly.GetType("Unity.Behavior.NodeRegistry");
            object Node(Type runtimeType, string model, Vector2 position) {
                var info = registry.GetMethod("GetInfo", Flags).Invoke(null, new object[] { runtimeType });
                return Call(graph, "CreateNode", TypeNamed("Unity.Behavior." + model), position, null, new object[] { info });
            }
            var root = Node(TypeNamed("Unity.Behavior.Start"), "StartNodeModel", new Vector2(700, 50));
            root.GetType().GetField("Repeat", Flags).SetValue(root, true);
            var selector = Node(TypeNamed("Unity.Behavior.SelectorComposite"), "CompositeNodeModel", new Vector2(700, 230));
            Call(graph, "ConnectEdge", FirstPort(root, "OutputPortModels"), FirstPort(selector, "InputPortModels"));
            var actions = new[] { typeof(PursuitPrepareAction), typeof(PursuitChaseAction), typeof(PursuitInvestigateAction), typeof(PursuitListenAction), typeof(PursuitSearchAction), typeof(PursuitPatrolAction) };
            for (int i = 0; i < actions.Length; i++) {
                var node = Node(actions[i], "ActionNodeModel", new Vector2(i * 300, 440));
                Call(graph, "ConnectEdge", FirstPort(selector, "OutputPortModels"), FirstPort(node, "InputPortModels"));
            }
            Call(graph, "ValidateAsset");
            var runtime = (BehaviorGraph)Call(graph, "BuildRuntimeGraph", true);
            EditorUtility.SetDirty(graph); AssetDatabase.SaveAssetIfDirty(graph);
            return runtime;
        }
        static void BuildBlood(out GameObject prefab, out Sprite sprite)
        {
            string texturePath = Folder + "/BloodSplash.png";
            if (!File.Exists(texturePath)) {
                const int size = 256;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var random = new System.Random(9426);
                var circles = new Vector3[42]; circles[0] = new Vector3(.5f, .5f, .18f);
                for (int i = 1; i < circles.Length; i++) {
                    float a = (float)random.NextDouble() * Mathf.PI * 2, r = (float)random.NextDouble() * .43f;
                    circles[i] = new Vector3(.5f + Mathf.Cos(a) * r, .5f + Mathf.Sin(a) * r, Mathf.Lerp(.008f, .065f, (float)random.NextDouble()) * (1 - r));
                }
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                    float alpha = 0;
                    foreach (var c in circles) {
                        float d = Vector2.Distance(new Vector2(x / (float)size, y / (float)size), new Vector2(c.x, c.y));
                        alpha = Mathf.Max(alpha, Mathf.Clamp01((c.z - d) * size * .6f));
                    }
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                texture.Apply(); File.WriteAllBytes(texturePath, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.textureType = TextureImporterType.Sprite; importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
            }
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            string materialPath = Folder + "/BloodSpray.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) {
                var drop = InjuryBloodEffects.CreateDropTexture(64);
                AssetDatabase.CreateAsset(drop, Folder + "/BloodDrop.asset");
                material = InjuryBloodEffects.CreateMaterial(drop); AssetDatabase.CreateAsset(material, materialPath);
            }
            string prefabPath = Folder + "/CaptureBloodSpray.prefab";
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) {
                var go = InjuryBloodEffects.BuildDefault(material, Vector3.zero, Quaternion.identity);
                UnityEngine.Object.DestroyImmediate(go.GetComponent<BloodSpurt>());
                var ps = go.GetComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.loop = false; main.playOnAwake = false; main.maxParticles = 500;
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 4f); main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .9f);
                var emission = ps.emission; emission.enabled = false;
                var shape = ps.shape; shape.angle = 48;
                prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath); UnityEngine.Object.DestroyImmediate(go);
            }
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path[..path.LastIndexOf('/')]; EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path[(path.LastIndexOf('/') + 1)..]);
        }
    }
}
