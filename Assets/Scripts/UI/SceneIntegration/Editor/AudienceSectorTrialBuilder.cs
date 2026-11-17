#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.UI.SceneIntegration.Editor
{
    public static class AudienceSectorTrialBuilder
    {
        const string Folder = "Assets/Resources/Prefabs/UIs/Roles/Retained/Feedback";
        public static GameObject Build()
        {
            var vertices = new List<Vector3>(); var cheer = new List<Vector3>(); var boo = new List<Vector3>();
            var phase = new List<Vector2>(); var triangles = new List<int>();
            for (int row = 0; row < 3; row++)
            for (int person = 0; person < 24; person++)
            {
                float angle = Mathf.Deg2Rad * (2 + person * 1.2f);
                Vector3 outward = new(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                Vector3 right = new(Mathf.Cos(angle), 0, -Mathf.Sin(angle));
                Vector3 origin = new Vector3(0, 27 + row * 5, 80) + outward * (348 + row * 6);
                float height = .92f + ((person * 17 + row * 7) % 9) * .02f;
                void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, bool arm = false, int side = 0)
                {
                    int first = vertices.Count;
                    foreach (var v in new[] { a, b, c, d })
                    {
                        // Broad silhouettes survive the normal distant performance camera.
                        var local = right * (v.x * 1.35f) + Vector3.up * v.y * height;
                        var raised = local + Vector3.up * .32f;
                        float upper = Mathf.InverseLerp(.5f, 1.9f, v.y);
                        var leaning = local - outward * upper * .95f - Vector3.up * upper * .42f;
                        if (arm)
                        {
                            float hand = Mathf.Clamp01((1.45f - v.y) / .8f);
                            // Cheer: high open V. Boo: wide low arms pushing toward the stage.
                            raised += right * side * hand * .65f + Vector3.up * hand * 1.95f;
                            leaning += right * side * hand * .75f + Vector3.up * hand * .55f - outward * hand * .6f;
                        }
                        vertices.Add(origin + local * 3); cheer.Add((raised-local) * 3);
                        boo.Add((leaning-local) * 3);
                        phase.Add(new Vector2(person / 23f, (person * 13 + row * 7) % 19));
                    }
                    triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                    triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
                }
                Quad(new(-.36f,.7f), new(.36f,.7f), new(.3f,1.5f), new(-.3f,1.5f));
                Quad(new(-.32f,0), new(-.06f,0), new(-.03f,.8f), new(-.32f,.8f));
                Quad(new(.06f,0), new(.32f,0), new(.32f,.8f), new(.03f,.8f));
                Quad(new(-.25f,1.5f), new(.25f,1.5f), new(.25f,1.95f), new(-.25f,1.95f));
                Quad(new(-.57f,.65f), new(-.37f,.65f), new(-.24f,1.45f), new(-.42f,1.45f), true, -1);
                Quad(new(.37f,.65f), new(.57f,.65f), new(.42f,1.45f), new(.24f,1.45f), true, 1);
            }
            string meshPath = Folder + "/AudienceTrialMesh.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null) { mesh = new Mesh { name = "AudienceTrial_72People" }; AssetDatabase.CreateAsset(mesh, meshPath); }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(1, cheer); mesh.SetUVs(2, boo); mesh.SetUVs(3, phase);
            mesh.RecalculateBounds(); var bounds = mesh.bounds; bounds.Expand(12); mesh.bounds = bounds;
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            string materialPath = Folder + "/AudienceTrialMaterial.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("CurtainCall/Audience Sector Trial")); AssetDatabase.CreateAsset(material, materialPath); }
            material.SetColor("_Color", new Color(.018f,.016f,.014f,1));
            var saved = new SerializedObject(material);
            var colors = saved.FindProperty("m_SavedProperties.m_Colors");
            for (int i = colors.arraySize-1; i >= 0; i--)
            {
                string name = colors.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                if (name == "_CheerColor" || name == "_BooColor") colors.DeleteArrayElementAtIndex(i);
            }
            saved.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            var root = new GameObject("AudienceTrial_OneSector", typeof(MeshFilter), typeof(MeshRenderer));
            try
            {
                root.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                // One local light above the seating washes the seats, not the unlit people.
                var lamp = new GameObject("AudienceSeatingSpot", typeof(Light));
                lamp.transform.SetParent(root.transform, false);
                lamp.transform.localPosition = new Vector3(95,75,375);
                lamp.transform.localRotation = Quaternion.LookRotation(new Vector3(95,35,432)-lamp.transform.localPosition);
                var light = lamp.GetComponent<Light>(); light.type = LightType.Spot;
                light.color = new Color(1,.88f,.74f); light.range = 40;
                light.spotAngle = 85; light.innerSpotAngle = 45; light.intensity = 9000;
                light.shadows = LightShadows.None; light.renderMode = LightRenderMode.ForcePixel;
                light.enabled = false;
                return PrefabUtility.SaveAsPrefabAsset(root, Folder + "/AudienceTrial.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
#endif
