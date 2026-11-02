using System;
using System.Linq;
using System.Collections.Generic;
using CurtainCall.Settings;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace CurtainCall.WaitingRoom.Pursuit.Editor
{
    public static class PursuitSceneBuilder
    {
        const string AssetsRoot = "Assets/_ThirdParty/curtaincall/Pursuit";
        const string ScenePath = "Assets/Scenes/Tests/WaitingRoom/Pursuit/WaitingRoomPursuit.unity";
        public static string LastReport;
        static string[] Models => Enumerable.Range(1, 4).Select(n => $"Pursuit_Male_{n:00}")
            .Concat(Enumerable.Range(1, 6).Select(n => $"Pursuit_Female_{n:00}")).ToArray();

        [MenuItem("Tools/Curtain Call/Pursuit/1 Build mannequin assets")]
        public static void BuildModels()
        {
            var results = new List<string>();
            foreach (string name in Models) {
                string path = $"{AssetsRoot}/Models/{name}.fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Missing rig: " + path);
                if (importer.animationType != ModelImporterAnimationType.Legacy) {
                    importer.animationType = ModelImporterAnimationType.Legacy;
                    importer.importAnimation = false; importer.SaveAndReimport();
                }
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
                instance.name = name;
                // Reuse the original Synty materials; keep UVs and source materials untouched.
                string original = $"Assets/_ThirdParty/Synty/PolygonApocalypse/Models/SM_Prop_Mannequin_{name.Substring(8)}.fbx";
                var sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(original);
                var materialSource = sourceModel != null ? sourceModel.GetComponentInChildren<Renderer>() : null;
                if (materialSource != null) foreach (var r in instance.GetComponentsInChildren<Renderer>()) r.sharedMaterials = materialSource.sharedMaterials;
                var motion = instance.GetComponent<Animation>();
                if (motion == null) motion = instance.AddComponent<Animation>();
                motion.playAutomatically = true;
                var actor = instance.AddComponent<PursuitActor>();
                actor.Motion = motion;
                actor.Chest = instance.GetComponentsInChildren<Transform>().First(t => t.name == "Chest");
                actor.Clips = new AnimationClip[7];
                for (int i = 0; i < actor.Clips.Length; i++) {
                    string clipPath = $"{AssetsRoot}/Animations/{name}_{(PursuitMotion)i}.anim";
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (clip == null) {
                        clip = MakeClip(instance, (PursuitMotion)i);
                        AssetDatabase.CreateAsset(clip, clipPath);
                    }
                    else { var updated = MakeClip(instance, (PursuitMotion)i); EditorUtility.CopySerialized(updated, clip); UnityEngine.Object.DestroyImmediate(updated); EditorUtility.SetDirty(clip); }
                    actor.Clips[i] = clip; motion.AddClip(clip, ((PursuitMotion)i).ToString());
                }
                motion.clip = actor.Clips[0];
                var skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var skin in skins) skin.updateWhenOffscreen = true;
                PrefabUtility.SaveAsPrefabAsset(instance, $"{AssetsRoot}/Prefabs/{name}.prefab");
                results.Add($"{name}: skins={skins.Length}, bones={skins.Sum(s => s.bones.Length)}");
                UnityEngine.Object.DestroyImmediate(instance);
            }
            AssetDatabase.SaveAssets();
            LastReport = string.Join("\n", results);
            Debug.Log("[Pursuit] " + LastReport);
        }
        static AnimationClip MakeClip(GameObject root, PursuitMotion motion) => PursuitMotionBuilder.Build(root, motion);
        [MenuItem("Tools/Curtain Call/Pursuit/2 Bake navigation")]
        public static void BakeNavigation()
        {
            RequireScene();
            var root = GameObject.Find("PursuitSystem") ?? new GameObject("PursuitSystem");
            var surface = root.GetComponent<NavMeshSurface>();
            if (surface == null) surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true; surface.voxelSize = .12f;
            var settings = surface.GetBuildSettings();
            settings.agentRadius = .24f; settings.agentHeight = 1.7f; settings.agentClimb = .55f; settings.agentSlope = 55;
            settings.overrideVoxelSize = true; settings.voxelSize = .08f;
            var sources = new List<NavMeshBuildSource>();
            var moving = DynamicBlockers();
            var bounds = new Bounds(root.transform.position, Vector3.one);
            foreach (var c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (c.enabled && !c.isTrigger) bounds.Encapsulate(c.bounds);
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(s => s.component is Collider collider && moving.Contains(collider));
            surface.RemoveData();
            surface.navMeshData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (surface.navMeshData == null) throw new InvalidOperationException("Navigation bake failed");
            string path = AssetsRoot + "/Navigation/WaitingRoomPursuit.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing == null) AssetDatabase.CreateAsset(surface.navMeshData, path);
            else { EditorUtility.CopySerialized(surface.navMeshData, existing); surface.RemoveData(); surface.navMeshData = existing; surface.AddData(); EditorUtility.SetDirty(existing); }
            surface.AddData();
            BuildTraversalLinks(root);
            AssetDatabase.SaveAssetIfDirty(surface.navMeshData);
            EditorSceneManager.SaveScene(root.scene);
            LastReport = "NavMesh vertices=" + NavMesh.CalculateTriangulation().vertices.Length;
            Debug.Log("[Pursuit] " + LastReport);
        }
        static void BuildTraversalLinks(GameObject root)
        {
            if (root.transform.Find("TraversalLinks") != null) return;
            var mesh = NavMesh.CalculateTriangulation();
            int[] parent = Enumerable.Range(0, mesh.vertices.Length).ToArray();
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            void Join(int a, int b) { parent[Find(a)] = Find(b); }
            var positions = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < mesh.vertices.Length; i++) {
                var v = mesh.vertices[i]; var key = new Vector3Int(Mathf.RoundToInt(v.x * 100), Mathf.RoundToInt(v.y * 100), Mathf.RoundToInt(v.z * 100));
                if (positions.TryGetValue(key, out int previous)) Join(i, previous); else positions[key] = i;
            }
            var edges = new Dictionary<(int,int), int>();
            for (int i = 0; i < mesh.indices.Length; i += 3) {
                int a = mesh.indices[i], b = mesh.indices[i+1], c = mesh.indices[i+2]; Join(a,b); Join(b,c);
                foreach (var pair in new[] { (a,b), (b,c), (c,a) }) { var key = pair.Item1 < pair.Item2 ? pair : (pair.Item2,pair.Item1); edges.TryGetValue(key,out int n); edges[key]=n+1; }
            }
            var boundary = edges.Where(e=>e.Value==1).Select(e=>(vertex:e.Key.Item1, point:(mesh.vertices[e.Key.Item1]+mesh.vertices[e.Key.Item2])*.5f)).ToArray();
            var candidates = new List<(int a,int b,float d)>();
            for(int i=0;i<boundary.Length;i++) for(int j=i+1;j<boundary.Length;j++) {
                if(Find(boundary[i].vertex)==Find(boundary[j].vertex)) continue;
                var delta=boundary[i].point-boundary[j].point;
                if(Mathf.Abs(delta.y)>.75f || delta.sqrMagnitude>4 || delta.sqrMagnitude<.01f) continue;
                candidates.Add((i,j,delta.sqrMagnitude));
            }
            var group = new GameObject("TraversalLinks"); group.transform.SetParent(root.transform);
            int links=0;
            foreach(var candidate in candidates.OrderBy(c=>c.d)) {
                var a=boundary[candidate.a]; var b=boundary[candidate.b];
                if(Find(a.vertex)==Find(b.vertex)) continue;
                bool Clear(float h) => !Physics.Linecast(a.point+Vector3.up*h,b.point+Vector3.up*h,~0,QueryTriggerInteraction.Ignore);
                if(!Clear(.65f) || !Clear(1.3f)) continue;
                var mid=(a.point+b.point)*.5f;
                if(!Physics.Raycast(mid+Vector3.up,Vector3.down,out var floor,2,~0,QueryTriggerInteraction.Ignore) || Mathf.Abs(floor.point.y-mid.y)>.8f) continue;
                var link=new GameObject($"StepLink_{links:00}").AddComponent<NavMeshLink>(); link.transform.SetParent(group.transform);
                link.startPoint=a.point; link.endPoint=b.point; link.width=.4f; link.bidirectional=true;
                Join(a.vertex,b.vertex); links++;
                if(links>=64) break;
            }
            Debug.Log($"[Pursuit] collision-checked traversal links={links}");
        }
        static HashSet<Collider> DynamicBlockers()
        {
            var colliders = new HashSet<Collider>();
            foreach (var gate in UnityEngine.Object.FindObjectsByType<ExplorationGate>(FindObjectsSortMode.None)) {
                var so = new SerializedObject(gate);
                var blocker = so.FindProperty("blocker").objectReferenceValue as Collider;
                if (blocker != null) colliders.Add(blocker);
                var hide = so.FindProperty("hideOnOpen");
                for (int i = 0; i < hide.arraySize; i++)
                    if (hide.GetArrayElementAtIndex(i).objectReferenceValue is GameObject go)
                        foreach (var c in go.GetComponentsInChildren<Collider>(true)) colliders.Add(c);
            }
            foreach (var slide in UnityEngine.Object.FindObjectsByType<ExplorationSlide>(FindObjectsSortMode.None))
                foreach (var c in slide.GetComponentsInChildren<Collider>(true)) colliders.Add(c);
            foreach (var wall in UnityEngine.Object.FindObjectsByType<BreakableWall>(FindObjectsSortMode.None)) {
                var blocker = new SerializedObject(wall).FindProperty("blocker").objectReferenceValue as Collider;
                if (blocker != null) colliders.Add(blocker);
            }
            foreach (var door in UnityEngine.Object.FindObjectsByType<NightDoorLock>(FindObjectsSortMode.None)) {
                var shutter = new SerializedObject(door).FindProperty("shutter").objectReferenceValue as Transform;
                if (shutter != null) foreach (var c in shutter.GetComponentsInChildren<Collider>(true)) colliders.Add(c);
            }
            foreach (var collider in colliders) {
                var existing = collider.GetComponent<PursuitDoorObstacle>();
                if (existing == null) existing = collider.gameObject.AddComponent<PursuitDoorObstacle>();
                existing.Source = collider;
            }
            return colliders;
        }
        [MenuItem("Tools/Curtain Call/Pursuit/3 Install room spawns")]
        public static void Install()
        {
            RequireScene(); Physics.SyncTransforms();
            var root = GameObject.Find("PursuitSystem") ?? throw new InvalidOperationException("Bake navigation first");
            if (root.GetComponent<PursuitDirector>() != null) throw new InvalidOperationException("Already installed; edit room components instead of overwriting them.");
            var director = root.AddComponent<PursuitDirector>();
            director.Mannequins = Models.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"{AssetsRoot}/Prefabs/{n}.prefab")).ToArray();
            var timers = UnityEngine.Object.FindFirstObjectByType<ExplorationTimerZones>();
            if (timers == null) throw new InvalidOperationException("Exploration timer zones missing");
            var timerSource = new SerializedObject(timers).FindProperty("runningZones");
            var runningNames = Enumerable.Range(0, timerSource.arraySize).Select(i => timerSource.GetArrayElementAtIndex(i).stringValue).ToArray();
            var rooms = new List<PursuitRoom>();
            var failures = new List<string>();
            foreach (var box in timers.GetComponentsInChildren<BoxCollider>(false).OrderBy(b => b.name)) {
                var b = box.bounds;
                bool spawns = box.name.StartsWith("Area_") && runningNames.Contains(box.name);
                var points = new List<Vector3>();
                float y = b.min.y + 1.65f;
                foreach (var offset in new[] { Vector2.zero, new Vector2(-.28f,-.28f), new Vector2(.28f,.28f), new Vector2(-.28f,.28f), new Vector2(.28f,-.28f) }) {
                    var p = new Vector3(b.center.x + b.size.x * offset.x, y, b.center.z + b.size.z * offset.y);
                    if (NavMesh.SamplePosition(p, out var hit, 4, NavMesh.AllAreas) && b.Contains(hit.position + Vector3.up * .5f)
                        && points.All(q => Vector3.Distance(q, hit.position) > 1)) points.Add(hit.position);
                }
                if (points.Count == 0 && spawns) failures.Add(box.name);
                var room = new GameObject(box.name.StartsWith("Area_") ? box.name.Substring(5) : box.name).AddComponent<PursuitRoom>();
                room.transform.SetParent(root.transform); room.Area = box; room.Count = spawns && points.Count > 0 ? 1 : 0;
                room.PatrolPoints = points.Select((p, i) => { var go = new GameObject($"Patrol_{i:00}"); go.transform.SetParent(room.transform); go.transform.position = p; return go.transform; }).ToArray();
                var spawn = new GameObject("Spawn_00").transform; spawn.SetParent(room.transform); spawn.position = points.Count > 0 ? points[0] : b.center;
                room.SpawnPoints = new[] { spawn }; rooms.Add(room);
            }
            director.Rooms = rooms.ToArray();
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type != LightType.Directional).ToArray();
            foreach (var room in rooms) room.Lights = lights.Where(l => rooms.Where(r => r.Area.bounds.Contains(l.transform.position)).OrderBy(r => r.Area.bounds.size.sqrMagnitude).FirstOrDefault() == room).ToArray();
            var settings = GameSettings.WaitingRoom;
            settings.Pursuit.Enabled = true;
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            EditorSceneManager.MarkSceneDirty(root.scene); EditorSceneManager.SaveScene(root.scene);
            LastReport = $"Rooms={rooms.Count}, variations={director.Mannequins.Count(m => m != null)}, lights={rooms.Sum(r => r.Lights.Length)}, no-nav={string.Join(",", failures)}";
            Debug.Log("[Pursuit] " + LastReport);
        }
        static void RequireScene()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("Open the pursuit scene first");
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode first");
        }
    }
}
