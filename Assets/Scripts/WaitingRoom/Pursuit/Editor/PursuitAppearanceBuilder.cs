using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public static class PursuitAppearanceBuilder
    {
        const string Root = "Assets/_ThirdParty/curtaincall/Pursuit/Reclaimed";
        [MenuItem("Tools/Curtain Call/Pursuit/Build reclaimed troupe appearance")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/_ThirdParty/curtaincall/Pursuit", "Reclaimed");
            var shell = Material("Reclaimed ivory", Shader.Find("CurtainCall/Pursuit/ReclaimedShell"), new Color(.72f,.65f,.51f));
            var cloth = Material("Faded troupe velvet", Shader.Find("Universal Render Pipeline/Lit"), new Color(.16f,.024f,.033f));
            cloth.SetFloat("_Cull", 0);
            var brass = Material("Tarnished repairs", Shader.Find("Universal Render Pipeline/Lit"), new Color(.28f,.18f,.065f));
            brass.SetFloat("_Metallic", .6f); brass.SetFloat("_Smoothness", .25f);
            var charcoal = Material("Old costume straps", Shader.Find("Universal Render Pipeline/Lit"), new Color(.035f,.027f,.021f));
            var cue = Material("Facing light", Shader.Find("CurtainCall/Pursuit/FacingLight"), Color.white);
            var collarMesh = SaveMesh("Torn collar", Collar());
            var cubeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cube = SaveMesh("Repair plate", Object.Instantiate(cubeObject.GetComponent<MeshFilter>().sharedMesh)); Object.DestroyImmediate(cubeObject);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[]{"Assets/_ThirdParty/curtaincall/Pursuit/Prefabs"})) {
                var path = AssetDatabase.GUIDToAssetPath(guid); var go = PrefabUtility.LoadPrefabContents(path);
                try {
                    var actor = go.GetComponent<PursuitActor>(); if (actor == null) continue;
                    foreach (var t in go.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Reclaimed_")).ToArray())
                        if (t != null) Object.DestroyImmediate(t.gameObject);
                    var appearance = go.GetComponent<PursuitAppearance>() ?? go.AddComponent<PursuitAppearance>();
                    appearance.GroundMaterial = cue;
                    var bones = go.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                    var chest = bones["Chest"]; var head = bones["Head"];
                    var skins = go.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (var skin in skins) {
                        var mesh = Object.Instantiate(skin.sharedMesh);
                        var box = mesh.bounds; var uv = new List<Vector3>();
                        foreach (var vertex in mesh.vertices) {
                            var p = vertex-box.min;
                            // FBX bind mesh is Z-up. Store undeformed coordinates so the skin cannot swim.
                            uv.Add(new Vector3(p.x/box.size.x,p.z/box.size.z,1-p.y/box.size.y));
                        }
                        mesh.SetUVs(2,uv);skin.sharedMesh=SaveMesh(go.name+" repaired shell",mesh);
                        var modelShell = Material(go.name + " shell", shell.shader, shell.GetColor("_BaseColor"));
                        EditorUtility.SetDirty(modelShell); AssetDatabase.SaveAssetIfDirty(modelShell);
                        skin.sharedMaterials = Enumerable.Repeat(modelShell, skin.sharedMaterials.Length).ToArray();
                    }
                    // The source rig has 100x FBX transform scale. Author dimensions in world metres.
                    var baseCollar = Part("Reclaimed_Collar", chest, head.position - Vector3.up * .07f, new Vector3(.8f,.8f,.8f), collarMesh, cloth, go.transform.rotation);
                    var advanced = Part("Reclaimed_UnfurledCollar", chest, head.position - Vector3.up * .09f, new Vector3(1.25f,1.1f,1.25f), collarMesh, cloth, go.transform.rotation);
                    advanced.SetActive(false); appearance.AdvancedDetails = new[]{advanced};
                    var brace = Part("Reclaimed_ChestStrap", chest, chest.position + go.transform.forward*.13f + Vector3.up*.11f,
                        new Vector3(.045f,.36f,.025f),cube,charcoal,go.transform.rotation*Quaternion.Euler(0,0,22));
                    Part("Reclaimed_RepairPlate", chest, chest.position + go.transform.forward*.15f + Vector3.up*.08f,
                        new Vector3(.12f,.08f,.025f),cube,brass,go.transform.rotation*Quaternion.Euler(0,0,-12));
                    foreach (string name in new[]{"UpperArm_L","Forearm_R","Shin_L"}) if (bones.TryGetValue(name,out var bone))
                        Part("Reclaimed_JointBrace",bone,bone.position + go.transform.forward*.045f,new Vector3(.07f,.12f,.035f),cube,brass,go.transform.rotation);
                    appearance.AdvancedWalk = Clip(go, PursuitMotion.Walk);
                    appearance.AdvancedRun = Clip(go, PursuitMotion.Run);
                    PrefabUtility.SaveAsPrefabAsset(go,path);
                } finally { PrefabUtility.UnloadPrefabContents(go); }
            }
            foreach(var material in new[]{shell,cloth,brass,charcoal,cue}) { EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); }
        }
        static AnimationClip Clip(GameObject go, PursuitMotion motion)
        {
            var made = PursuitMotionBuilder.Build(go,motion,true); made.name = "Advanced"+motion;
            string path=$"{Root}/{go.name}_Advanced{motion}.anim"; var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(existing==null) {AssetDatabase.CreateAsset(made,path);return made;}
            EditorUtility.CopySerialized(made,existing);Object.DestroyImmediate(made);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);return existing;
        }
        static Material Material(string name, Shader shader, Color color)
        {
            if (shader == null) throw new System.InvalidOperationException("Shader not imported: " + name);
            string path=$"{Root}/{name}.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);} else m.shader=shader;
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);return m;
        }
        static Mesh SaveMesh(string name,Mesh mesh)
        {
            mesh.name=name;string path=$"{Root}/{name}.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);AssetDatabase.SaveAssetIfDirty(old);return old;
        }
        static GameObject Part(string name, Transform parent, Vector3 position, Vector3 size, Mesh mesh, Material material, Quaternion rotation)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            go.transform.SetPositionAndRotation(position,rotation);var scale=parent.lossyScale;
            go.transform.localScale=new Vector3(size.x/scale.x,size.y/scale.y,size.z/scale.z);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        static Mesh Collar()
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<16;i++) {
                float a=i*Mathf.PI*2/16,b=(i+1)*Mathf.PI*2/16;
                int j=vertices.Count; float drop=i%2==0?.14f:.08f;
                vertices.Add(new Vector3(Mathf.Cos(a)*.075f,0,Mathf.Sin(a)*.075f));
                vertices.Add(new Vector3(Mathf.Cos(b)*.075f,0,Mathf.Sin(b)*.075f));
                vertices.Add(new Vector3(Mathf.Cos((a+b)*.5f)*.24f,-drop,Mathf.Sin((a+b)*.5f)*.24f));
                triangles.AddRange(new[]{j,j+1,j+2});
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
