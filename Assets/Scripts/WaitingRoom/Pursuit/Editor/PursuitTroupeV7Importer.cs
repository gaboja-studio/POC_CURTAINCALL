using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    /// <summary>Only UV seam duplicates are added. Every position, normal, skin weight and triangle comes from the existing Unity body.</summary>
    public static class PursuitTroupeV7Importer
    {
        public const string Root = "Assets/_ThirdParty/curtaincall/Pursuit/TroupeV7";
        public const string Source = "Assets/_ThirdParty/curtaincall/Pursuit/Concepts/JointSeams/Source~/V7";
        [Serializable] public sealed class UvData { public string name; public int[] sourceIndices; public float[][] uv; public int sourceTriangles; }
        [MenuItem("Tools/Curtain Call/Pursuit/Apply V7 baked remnants (preserve original body)")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before applying art.");
            var shader = Shader.Find("CurtainCall/Pursuit/TroupeV7");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("V7 shader missing or invalid.");
            int applied = 0;
            foreach (var file in Directory.GetFiles(Source, "Pursuit_*-uv.json").OrderBy(p => p))
            {
                var data = JsonConvert.DeserializeObject<UvData>(File.ReadAllText(file));
                var original = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_ThirdParty/curtaincall/Pursuit/Reclaimed/" + data.name + " repaired shell.asset");
                if (original == null || data.sourceIndices.Length != original.triangles.Length) throw new InvalidOperationException("Original topology mismatch: " + data.name);
                var canonical = original.vertices;
                var sourceNormals = original.normals;
                var sourceWeights = original.boneWeights;
                var vertices = canonical.ToList();
                var normals = sourceNormals.ToList();
                var weights = sourceWeights.ToList();
                var originalIndices = Enumerable.Range(0, canonical.Length).ToList();
                var uv = Enumerable.Repeat(Vector2.zero, canonical.Length).ToList();
                var assigned = new bool[canonical.Length];
                var remap = new Dictionary<(int, int, int), int>();
                var triangles = new int[data.sourceIndices.Length];
                for (int corner = 0; corner < triangles.Length; corner++)
                {
                    int sourceIndex = data.sourceIndices[corner];
                    var value = new Vector2(data.uv[corner][0], data.uv[corner][1]);
                    var key = (sourceIndex, Mathf.RoundToInt(value.x * 10000000), Mathf.RoundToInt(value.y * 10000000));
                    if (!remap.TryGetValue(key, out int index))
                    {
                        if (!assigned[sourceIndex]) { index = sourceIndex; uv[index] = value; assigned[sourceIndex] = true; }
                        else { index = vertices.Count; vertices.Add(canonical[sourceIndex]); normals.Add(sourceNormals[sourceIndex]); weights.Add(sourceWeights[sourceIndex]); originalIndices.Add(sourceIndex); uv.Add(value); }
                        remap.Add(key, index);
                    }
                    triangles[corner] = index;
                }
                // Compare oriented triangle sets, tolerating only cyclic vertex rotation.
                string Signature(int a, int b, int c) => a <= b && a <= c ? a+","+b+","+c : b <= c ? b+","+c+","+a : c+","+a+","+b;
                var before = original.triangles;
                var originalFaces = Enumerable.Range(0, before.Length / 3).Select(i => Signature(before[i*3], before[i*3+1], before[i*3+2])).OrderBy(x => x);
                var importedFaces = Enumerable.Range(0, triangles.Length / 3).Select(i => Signature(data.sourceIndices[i*3], data.sourceIndices[i*3+1], data.sourceIndices[i*3+2])).OrderBy(x => x);
                if (!originalFaces.SequenceEqual(importedFaces)) throw new InvalidOperationException("Triangle identities changed: " + data.name);
                var mesh = new Mesh { name = data.name + " V7 original body UV" };
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.boneWeights = weights.ToArray(); mesh.bindposes = original.bindposes;
                mesh.SetUVs(0, uv); mesh.SetUVs(2, vertices.Select(v => new Vector3(-v.x*100, v.y*100, v.z*100)).ToList());
                mesh.triangles = triangles; mesh.RecalculateTangents(); mesh.bounds = original.bounds;
                string meshPath = Root + "/" + data.name + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (asset == null) { AssetDatabase.CreateAsset(mesh, meshPath); asset = mesh; }
                else { EditorUtility.CopySerialized(mesh, asset); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
                File.WriteAllText(Source + "/" + data.name + "-canonical-indices.json", JsonConvert.SerializeObject(originalIndices));

                var matPath = Root + "/" + data.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, matPath); }
                material.shader = shader;
                foreach (var type in new[] { "Albedo", "Normal", "Surface" })
                {
                    string texturePath = Root + "/LayerTextures/" + data.name + "-" + type + ".png";
                    if (!File.Exists(texturePath)) texturePath = Root + "/Textures/" + data.name + "-" + type + ".png";
                    var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                    importer.textureType = type == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    importer.sRGBTexture = type == "Albedo";
                    importer.maxTextureSize = type == "Surface" ? 1024 : 2048;
                    importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.anisoLevel = 4;
                    importer.SaveAndReimport();
                    material.SetTexture(type == "Albedo" ? "_BaseMap" : type == "Normal" ? "_BumpMap" : "_SurfaceMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                }
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_DirtAmount", .4f); material.SetFloat("_WearAmount", .4f); material.SetFloat("_BaseBlood", .6f); material.SetFloat("_BloodVisible", 1);
                EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
                string prefabPath = "Assets/_ThirdParty/curtaincall/Pursuit/Prefabs/" + data.name + ".prefab";
                var go = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var skin = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    skin.sharedMesh = asset; skin.sharedMaterial = material;
                    // All transforms, renderer bounds, bones, root bone, actor, original clips and surface state remain unchanged.
                    PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(go); }
                applied++;
            }
            Debug.Log("V7 baked materials applied to " + applied + " original pursuit bodies.");
        }
    }
}
