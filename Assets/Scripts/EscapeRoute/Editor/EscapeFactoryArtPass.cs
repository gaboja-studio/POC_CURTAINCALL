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
    public static partial class EscapeFactoryArtPass
    {
        static HashSet<UnityEngine.Object> referenced;
        static Transform background;
        static void Unpack(GameObject item)
        {
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(item);
            if (root != null) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        static bool PureStatic(MeshRenderer renderer)
        {
            if (referenced.Contains(renderer) || referenced.Contains(renderer.gameObject) || referenced.Contains(renderer.transform)) return false;
            for (var t = renderer.transform; t != null && t != background; t = t.parent)
                if (t.GetComponents<MonoBehaviour>().Length != 0 || t.GetComponent<Animator>() != null
                    || t.GetComponent<Rigidbody>() != null || referenced.Contains(t) || referenced.Contains(t.gameObject)) return false;
            return true;
        }
        static void CollectReferences(Scene scene)
        {
            referenced = new HashSet<UnityEngine.Object>();
            foreach (var component in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if (component == null) continue;
                var property = new SerializedObject(component).GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                        referenced.Add(property.objectReferenceValue);
            }
        }
        static void ClearUnusedVisuals()
        {
            var allowed = new HashSet<Type> { typeof(Transform), typeof(MeshFilter), typeof(MeshRenderer), typeof(LODGroup) };
            foreach (var t in background.GetComponentsInChildren<Transform>(true).OrderBy(t => AnimationUtility.CalculateTransformPath(t, background).Count(c => c == '/')).ToArray())
            {
                if (t == null || t == background || t.gameObject.activeInHierarchy) continue;
                var lodOwner = t.GetComponentInParent<LODGroup>();
                if (lodOwner != null && !lodOwner.transform.IsChildOf(t)) continue;
                var tree = t.GetComponentsInChildren<Transform>(true);
                if (tree.SelectMany(n => n.GetComponents<MeshRenderer>()).Any(r => !PureStatic(r))) continue;
                if (tree.Any(n => referenced.Contains(n) || referenced.Contains(n.gameObject)
                    || n.GetComponents<Component>().Any(c => c == null || !allowed.Contains(c.GetType()) || referenced.Contains(c)))) continue;
                int count = tree.Sum(n => n.GetComponents<MeshRenderer>().Length);
                if (count == 0) continue;
                Unpack(t.gameObject); UnityEngine.Object.DestroyImmediate(t.gameObject); removed += count;
            }
        }
        static MeshFilter Template(string name, int triangleBudget)
        {
            string path = AssetDatabase.FindAssets(name + " t:Prefab").Select(AssetDatabase.GUIDToAssetPath)
                .First(p => p.StartsWith("Assets/_ThirdParty/LeartesStudios/", StringComparison.Ordinal) && Path.GetFileNameWithoutExtension(p) == name);
            var candidates = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshFilter>(true)
                .Where(f => f.sharedMesh != null).OrderByDescending(f => f.sharedMesh.triangles.Length / 3).ToArray();
            return candidates.FirstOrDefault(f => f.sharedMesh.triangles.Length / 3 <= triangleBudget) ?? candidates.Last();
        }
        static void Replace(MeshRenderer renderer, string template, int budget, bool useSourceMaterials)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            var source = Template(template, budget);
            var box = filter.sharedMesh.bounds;
            string key = template + "-" + box.size.ToString("F3");
            if (!meshes.TryGetValue(key, out var mesh))
            {
                mesh = UnityEngine.Object.Instantiate(source.sharedMesh); mesh.name = "A-" + template;
                var bounds = mesh.bounds;
                var scale = new Vector3(box.size.x / bounds.size.x, box.size.y / bounds.size.y, box.size.z / bounds.size.z);
                var vertices = mesh.vertices; var normals = mesh.normals;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = Vector3.Scale(vertices[i] - bounds.center, scale) + box.center;
                for (int i = 0; i < normals.Length; i++) normals[i] = new Vector3(normals[i].x / scale.x, normals[i].y / scale.y, normals[i].z / scale.z).normalized;
                mesh.vertices = vertices; mesh.normals = normals; mesh.RecalculateBounds(); mesh.RecalculateTangents();
                AssetDatabase.CreateAsset(mesh, MeshOutput + "/model-" + meshes.Count + ".asset"); meshes.Add(key, mesh);
            }
            Unpack(renderer.gameObject); filter.sharedMesh = mesh;
            var originals = source.GetComponent<MeshRenderer>().sharedMaterials;
            renderer.sharedMaterials = Enumerable.Range(0, mesh.subMeshCount)
                .Select(i => useSourceMaterials ? Convert(originals[Mathf.Min(i, originals.Length - 1)]) : pipe).ToArray();
            replacements++;
        }
        static void ReplaceDevices()
        {
            foreach (var r in background.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray())
            {
                string material = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                if (r.name == "운반 레일 결합부") Replace(r, "SM_Conveyer_Belt_Gear_01", 500, true);
                else if (r.name == "손잡이 케이블 가로보") Replace(r, "SM_Conveyer_Belt_Base_02", 3000, true);
                else if (material == "vertical-machine") Replace(r, "SM_Machinery_06", 6000, true);
                else if (material == "working-pipe" && r.name.Contains("배관") && r.GetComponent<MeshFilter>()?.sharedMesh.vertexCount < 512 && PureStatic(r))
                    Replace(r, "SM_Pipe1", 500, false);
            }
        }
        static string ColliderState(Collider c)
        {
            string shape = c switch
            {
                BoxCollider b => b.center.ToString("F5") + b.size.ToString("F5"),
                SphereCollider s => s.center.ToString("F5") + s.radius,
                CapsuleCollider p => p.center.ToString("F5") + p.radius + ":" + p.height + ":" + p.direction,
                MeshCollider m => AssetDatabase.GetAssetPath(m.sharedMesh) + ":" + m.convex + ":" + m.cookingOptions,
                _ => c.GetType().FullName
            };
            return c.enabled + ":" + c.isTrigger + ":" + c.transform.localToWorldMatrix.ToString("F5")
                + ":" + c.gameObject.layer + ":" + c.contactOffset + ":" + c.sharedMaterial?.GetInstanceID() + ":" + shape;
        }
        public static string Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit mode only");
            var scene = SceneManager.GetSceneByPath(ScenePath); bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Preserve unsaved A edits first");
            try
            {
                background = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "EscapeBackground");
                if (AssetDatabase.IsValidFolder(Output)) throw new InvalidOperationException("Art pass already exists; review before reapplying");
                Folder(Output); Folder(Private); Folder(MeshOutput); report.Clear(); materials.Clear(); textures.Clear(); meshes.Clear();
                copies = replacements = removed = batches = 0;
                var colliders = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true)).ToDictionary(c => c, ColliderState);
                var logic = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
                int before = background.GetComponentsInChildren<MeshRenderer>(true).Length;
                CollectReferences(scene); ClearUnusedVisuals();
                File.WriteAllText(".harness/local/factory-assets/progress.txt", "Inactive visuals removed=" + removed);
                PrepareMaterials(); ReplaceDevices();
                foreach (var r in background.GetComponentsInChildren<Renderer>(true).Where(r => r.gameObject.activeInHierarchy))
                {
                    var next = r.sharedMaterials.Select(Convert).ToArray();
                    if (!next.SequenceEqual(r.sharedMaterials)) { Unpack(r.gameObject); r.sharedMaterials = next; }
                }
                File.AppendAllText(".harness/local/factory-assets/progress.txt", "\nMaterials ready; textures=" + copies);
                StyleConcrete(); BatchStatic();
                foreach (var pair in colliders) if (pair.Key == null || ColliderState(pair.Key) != pair.Value) throw new InvalidOperationException("Collider changed " + pair.Key?.name);
                foreach (var pair in logic) if (pair.Key == null || EditorJsonUtility.ToJson(pair.Key) != pair.Value) throw new InvalidOperationException("Logic changed " + pair.Key?.name);
                foreach (var r in background.GetComponentsInChildren<Renderer>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                report.Add($"renderers before={before} after={background.GetComponentsInChildren<MeshRenderer>(true).Length}; removedInactive={removed}; sourceVisualsReplaced={replacements}; batches={batches}; textureCopies={copies}");
                report.Add($"collidersUnchanged={colliders.Count}; logicUnchanged={logic.Count}; privateSourceImports=0");
                File.WriteAllLines(".harness/local/factory-assets/apply-result.txt", report);
                return string.Join("\n", report);
            }
            catch (Exception e) { File.WriteAllText(".harness/local/factory-assets/apply-error.txt", e.ToString()); throw; }
            finally { if (opened && !scene.isDirty) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
