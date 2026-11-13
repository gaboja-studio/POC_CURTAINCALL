using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.UI.EscapeResultsKit;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static class EscapeEndingIntegration
    {
        const string ScenePath = "Assets/Scenes/Levels/escape_route.unity";
        const string Art = "Assets/Scenes/Levels/EscapeEndingArt/Cinematic";
        static Transform Node(string name, Transform parent)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
        static void Set(SerializedObject so, string field, UnityEngine.Object value) => so.FindProperty(field).objectReferenceValue = value;
        static Material Material(string name, Color color, bool ground = false)
        {
            string path = Art + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("CurtainCall/Escape/Meadow"));
            material.SetColor("_BaseColor", color); material.SetFloat("_Wind", ground ? 0 : .23f);
            material.SetFloat("_Ground", ground ? 1 : 0);
            AssetDatabase.CreateAsset(material, path); return material;
        }
        static void MeshObject(string name, Transform parent, Mesh mesh, params Material[] materials)
        {
            var node = Node(name, parent);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        [MenuItem("Tools/CurtainCall/탈출로/엔딩 결과창·Cinemachine 연결")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit mode required");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var frame = scene.GetRootGameObjects().Single(r => r.name == "EscapeCampaignFrame").transform;
            var ending = frame.GetComponentInChildren<EscapeEndingPresentation>(true);
            if (ending == null) throw new InvalidOperationException("Existing ending missing");
            if (ending.GetComponent<EscapeEndingResults>() != null) throw new InvalidOperationException("Already integrated; edit the existing rig");
            if (!AssetDatabase.IsValidFolder(Art)) AssetDatabase.CreateFolder("Assets/Scenes/Levels/EscapeEndingArt", "Cinematic");
            var rig = Node("Ending cinematic scenery", frame);
            // Keep the real locomotion stage clear of the completed route's walls and hazards.
            rig.localPosition = new Vector3(0,0,500);
            var path = Node("Run start", rig); path.localPosition = new Vector3(432,.08f,78); path.localRotation = Quaternion.Euler(0,90,0);
            var target = Node("Survivors camera target", rig); target.position = path.position;
            var cam = Node("Cinemachine front tracking", rig).gameObject.AddComponent<CinemachineCamera>();
            cam.Priority = 150; cam.Lens.FieldOfView = 48; cam.Lens.NearClipPlane = .1f; cam.enabled = false;
            cam.Follow = target; cam.LookAt = target;
            var follow = cam.gameObject.AddComponent<CinemachineFollow>();
            follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = new Vector3(.25f,.45f,.25f);
            follow.FollowOffset = frame.TransformDirection(new Vector3(10,1.05f,0));
            var composer = cam.gameObject.AddComponent<CinemachineRotationComposer>();
            composer.TargetOffset = new Vector3(0,1.25f,0); composer.Damping = new Vector2(.35f,.35f);
            composer.Composition.ScreenPosition = new Vector2(.25f,-.02f);
            var moon = Node("Moonlight", rig).gameObject.AddComponent<Light>();
            moon.type = LightType.Directional; moon.color = new Color(.52f,.66f,1); moon.intensity = 1.3f;
            moon.transform.localRotation = Quaternion.Euler(35,-90,0); moon.shadows = LightShadows.Soft;
            var reeds = Material("night-reeds", new Color(.40f,.42f,.35f));
            var ground = Material("night-ground", new Color(.12f,.16f,.17f), true);
            var source = frame.Find("EscapeBackground/56_ExitMeadow");
            var meshes = new List<Mesh>();
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.name.StartsWith("Wind reeds")) continue;
                var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                mesh.name = "clear-path-" + meshes.Count;
                var vertices = mesh.vertices; var indices = mesh.triangles; var kept = new List<int>();
                for (int i = 0; i < indices.Length; i += 3)
                {
                    float z = (vertices[indices[i]].z + vertices[indices[i+1]].z + vertices[indices[i+2]].z) / 3;
                    if (Mathf.Abs(z - 78) < 4.2f) continue;
                    kept.Add(indices[i]); kept.Add(indices[i+1]); kept.Add(indices[i+2]);
                }
                mesh.SetTriangles(kept, 0); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, Art + "/" + mesh.name + ".asset"); meshes.Add(mesh);
            }
            var tiles = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                var tile = Node("Meadow tile " + i, rig); tile.localPosition = new Vector3(404 + 120*i,0,0); tiles.Add(tile);
                foreach (var mesh in meshes)
                {
                    MeshObject(mesh.name, tile, mesh, reeds);
                    tile.GetChild(tile.childCount-1).localPosition = new Vector3(-404,0,0);
                }
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Running ground";
                floor.transform.SetParent(tile,false); floor.transform.localPosition = new Vector3(60,-.3f,78);
                floor.transform.localScale = new Vector3(120,.4f,130);
                floor.GetComponent<MeshRenderer>().sharedMaterial = ground;
            }
            // Reuse the game's striped circus roof mesh and materials as the distant exterior silhouette.
            var tentMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Scenes/Tests/WaitingRoom/Base/LayoutA/LA_XmasTentCone.asset");
            var red = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Tests/WaitingRoom/Base/LayoutA/LA_TentRed.mat");
            var cream = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Tests/WaitingRoom/Base/LayoutA/LA_TentCream.mat");
            if (tentMesh == null || red == null || cream == null) throw new InvalidOperationException("Existing circus assets missing");
            MeshObject("Distant circus roof", rig, tentMesh, red, cream);
            var tent = rig.GetChild(rig.childCount-1);
            tent.localScale = new Vector3(52 / tentMesh.bounds.size.x, 24 / tentMesh.bounds.size.y, 52 / tentMesh.bounds.size.z);
            tent.localPosition = new Vector3(320,8 - tentMesh.bounds.min.y * tent.localScale.y,78);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cylinder); wall.name = "Distant circus wall";
            wall.transform.SetParent(rig,false); wall.transform.localPosition = new Vector3(320,4,78);
            wall.transform.localScale = new Vector3(44,4,44);
            wall.GetComponent<MeshRenderer>().sharedMaterial = red;
            UnityEngine.Object.DestroyImmediate(wall.GetComponent<Collider>());
            var rim = Node("Circus warm glow",rig).gameObject.AddComponent<Light>();
            rim.type = LightType.Point; rim.range = 65; rim.intensity = 22; rim.color = new Color(1,.55f,.22f);
            rim.transform.localPosition = new Vector3(344,10,78); rim.shadows = LightShadows.None;
            var results = ending.gameObject.AddComponent<EscapeEndingResults>();
            var resultsData = new SerializedObject(results);
            var resultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/EscapeResultsKit/EscapeResultsCanvas.prefab");
            if (resultPrefab == null || resultPrefab.GetComponentInChildren<EscapeResultsView>(true) == null)
                throw new InvalidOperationException("Result canvas/view missing");
            Set(resultsData,"prefab",resultPrefab);
            var atlas = AssetDatabase.LoadAllAssetsAtPath("Assets/_ThirdParty/EscapeResultsKit/Sprites/portraits.png").OfType<Sprite>().ToArray();
            var portraitProperty = resultsData.FindProperty("portraits"); portraitProperty.arraySize = 4;
            string[] portraitNames = { "rabbit", "bear", "pierrot", "girl" };
            for (int i = 0; i < 4; i++) portraitProperty.GetArrayElementAtIndex(i).objectReferenceValue = atlas.Single(s => s.name == portraitNames[i]);
            resultsData.ApplyModifiedPropertiesWithoutUndo();
            var cinematic = ending.gameObject.AddComponent<EscapeEndingCinematic>(); var data = new SerializedObject(cinematic);
            Set(data,"pathStart",path); Set(data,"cameraTarget",target); Set(data,"cinematicCamera",cam); Set(data,"cinematicScenery",rig.gameObject);
            var tileProperty = data.FindProperty("meadowTiles"); tileProperty.arraySize = tiles.Count;
            for (int i = 0; i < tiles.Count; i++) tileProperty.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];
            var hide = data.FindProperty("sceneryToHide"); hide.arraySize = 1; hide.GetArrayElementAtIndex(0).objectReferenceValue = source.gameObject;
            data.ApplyModifiedPropertiesWithoutUndo();
            var endingData = new SerializedObject(ending); Set(endingData,"endingCamera",cam); endingData.ApplyModifiedPropertiesWithoutUndo();
            rig.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[EscapeEndingIntegration] Saved result binding, owner locomotion and Cinemachine meadow rig");
        }
    }
}
