using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeFactoryArtPass
    {
        static void StyleConcrete()
        {
            var shader = Shader.Find("CurtainCall/Escape/FactoryConcrete");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Factory concrete shader unavailable");
            var damage = floor.GetTexture("_BaseMap");
            var leakSource = AssetDatabase.LoadAssetAtPath<Texture>(Clock + "Textures/Decals/Leak/T_Leak_02_Mask.png");
            if (leakSource == null) throw new InvalidOperationException("Clock Factory runoff mask missing");
            var leakMask = LightTexture(leakSource);
            foreach (var m in new[] { concrete, floor, recess })
            {
                m.shader = shader; m.SetTexture("_DamageMap", damage); m.SetFloat("_TileScale", .4f);
                m.SetTexture("_LeakMask", leakMask);
                m.SetFloat("_Floor", m == floor ? 1 : 0); m.SetFloat("_BumpScale", .65f);
                m.SetFloat("_Smoothness", m == floor ? .15f : .10f);
                m.SetVector("_PanelSize", m == floor ? new Vector4(3,3,0,0) : new Vector4(4,2.8f,0,0));
                m.SetColor("_BaseColor", m == floor ? new Color(.68f,.70f,.67f)
                    : m == recess ? new Color(.34f,.38f,.37f) : new Color(.76f,.78f,.74f));
                EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m);
            }
        }
        static ulong GeometryKey(Mesh mesh)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (var v in mesh.vertices)
                    foreach (float f in new[] { v.x,v.y,v.z }) hash = (hash ^ (uint)BitConverter.SingleToInt32Bits(f)) * 1099511628211UL;
                foreach (int i in mesh.triangles) hash = (hash ^ (uint)i) * 1099511628211UL;
                return hash;
            }
        }
        static int RestoreOriginalConcreteMeshes(Scene scene)
        {
            var filters = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true))
                .Where(f => AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith(Output + "/concrete-uv-", StringComparison.Ordinal)).ToArray();
            if (filters.Length == 0) return 0;
            var needed = filters.Select(f => GeometryKey(f.sharedMesh)).ToHashSet();
            var originals = new Dictionary<ulong, Mesh>();
            void Offer(Mesh m)
            {
                if (m == null) return; ulong hash = GeometryKey(m);
                if (needed.Contains(hash) && !AssetDatabase.GetAssetPath(m).StartsWith(Output, StringComparison.Ordinal)) originals[hash] = m;
            }
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try { Offer(cube.GetComponent<MeshFilter>().sharedMesh); } finally { UnityEngine.Object.DestroyImmediate(cube); }
            foreach (var c in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshCollider>(true))) Offer(c.sharedMesh);
            var deps = File.ReadAllLines(".harness/local/factory-assets/dependencies-before.txt").Skip(1).ToArray();
            foreach (string p in deps.Where(p => p.EndsWith(".asset", StringComparison.Ordinal))) Offer(AssetDatabase.LoadAssetAtPath<Mesh>(p));
            foreach (string p in deps.Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)).OrderByDescending(p => p.Contains("Concrete")))
            {
                if (originals.Count == needed.Count) break;
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(p).OfType<Mesh>()) Offer(mesh);
            }
            int restored = 0;
            foreach (var f in filters)
                if (originals.TryGetValue(GeometryKey(f.sharedMesh), out var original)) { f.sharedMesh = original; restored++; }
            return restored;
        }
        public static string Finish()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit mode only");
            var scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Preserve A edits before finishing");
            try
            {
                background = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "EscapeBackground");
                var before = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true)).ToDictionary(c => c, ColliderState);
                var logic = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
                CollectReferences(scene);
                int discarded = 0;
                foreach (var r in background.GetComponentsInChildren<MeshRenderer>(true).Where(r => !r.enabled && System.Text.RegularExpressions.Regex.IsMatch(r.name,"_LOD[1-9]")).ToArray())
                {
                    if (!PureStatic(r) || r.GetComponentInParent<LODGroup>() != null || r.GetComponent<Collider>() != null
                        || r.GetComponents<Component>().Any(c => !(c is Transform || c is MeshFilter || c is MeshRenderer))) continue;
                    var go = r.gameObject; Unpack(go);
                    UnityEngine.Object.DestroyImmediate(r.GetComponent<MeshFilter>()); UnityEngine.Object.DestroyImmediate(r);
                    if (go.transform.childCount == 0 && !referenced.Contains(go) && !referenced.Contains(go.transform)) UnityEngine.Object.DestroyImmediate(go);
                    discarded++;
                }
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube); var cubeMesh = primitive.GetComponent<MeshFilter>().sharedMesh;
                int sheetRepairs = 0;
                try
                {
                    foreach (var r in background.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "슈트 측면 접힌 철판"))
                    { r.GetComponent<MeshFilter>().sharedMesh = cubeMesh; r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Output + "/factory-steel.mat"); sheetRepairs++; }
                }
                finally { UnityEngine.Object.DestroyImmediate(primitive); }
                int restored = RestoreOriginalConcreteMeshes(scene);
                concrete = AssetDatabase.LoadAssetAtPath<Material>(Output + "/factory-concrete.mat");
                floor = AssetDatabase.LoadAssetAtPath<Material>(Output + "/factory-floor.mat");
                recess = AssetDatabase.LoadAssetAtPath<Material>(Output + "/factory-recess.mat");
                StyleConcrete();
                var pipeMat = AssetDatabase.LoadAssetAtPath<Material>(Output + "/factory-pipe.mat");
                pipeMat.SetColor("_BaseColor",new Color(.66f,.70f,.67f));pipeMat.SetFloat("_Metallic",.4f);pipeMat.SetFloat("_Smoothness",.25f);
                EditorUtility.SetDirty(pipeMat);
                foreach (var m in AssetDatabase.FindAssets("t:Material",new[]{Output}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>))
                {
                    if (!m.name.StartsWith("source-", StringComparison.Ordinal)) continue;
                    m.SetColor("_BaseColor",new Color(.65f,.69f,.66f));m.SetFloat("_Smoothness",.22f);m.SetFloat("_Metallic",.4f);EditorUtility.SetDirty(m);
                }
                foreach (var pair in before) if (pair.Key == null || ColliderState(pair.Key) != pair.Value) throw new InvalidOperationException("Collider changed");
                foreach (var pair in logic) if (pair.Key == null || EditorJsonUtility.ToJson(pair.Key) != pair.Value) throw new InvalidOperationException("Logic changed");
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
                var used = AssetDatabase.GetDependencies(ScenePath,true).ToHashSet(); int deletedAssets = 0;
                foreach (string p in AssetDatabase.FindAssets("",new[]{Output}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".asset")&&!used.Contains(p)).ToArray())
                    if (AssetDatabase.DeleteAsset(p)) deletedAssets++;
                Folder(MeshOutput); int privateMeshes = 0;
                foreach (string p in AssetDatabase.FindAssets("t:Mesh",new[]{Output}).Select(AssetDatabase.GUIDToAssetPath).ToArray())
                {
                    string error = AssetDatabase.MoveAsset(p,MeshOutput + "/" + Path.GetFileName(p));
                    if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                    privateMeshes++;
                }
                string result=$"disabledStaleLODsRemoved={discarded}; sheetGeometryRetained={sheetRepairs}; originalConcreteMeshesRestored={restored}; unusedGeneratedMeshesDeleted={deletedAssets}; collider/logicUnchanged={before.Count}/{logic.Count}";
                result += "; vendorDerivedMeshesPrivate=" + privateMeshes;
                File.WriteAllText(".harness/local/factory-assets/finish-result.txt",result);return result;
            }
            catch(Exception e){File.WriteAllText(".harness/local/factory-assets/finish-error.txt",e.ToString());throw;}
            finally {if(opened&&!scene.isDirty)EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
