using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurtainCall.Network.PlayerSync;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    // Task-075: copy only the existing lobby. Never rebuild or edit the preserved Greybox.
    public static class WaitingRoomLayoutAStage02
    {
        const string ScenePath = "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity";
        const string ReportDir = "Tasks/Active/Task-20261027-075-waiting-room-design-rooms";
        static readonly Vector3 Center = new(0, 0, 70);
        static readonly Dictionary<Object, Object> Copies = new();
        static readonly List<string> Manifest = new();

        public static string Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlaying)
                throw new InvalidOperationException("Open WaitingRoomBase in Edit mode.");
            var source = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoomGreybox").transform;
            var target = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom_LayoutA").transform;
            if (source.gameObject.activeSelf || target.Find("S01_Lobby") != null)
                throw new InvalidOperationException("Expected inactive original and an unbuilt lobby.");
            var original = Snapshot(source);
            Copies.Clear(); Manifest.Clear();
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Layout A stage 2: duplicate existing lobby");
            try
            {
                Undo.RecordObject(target, "Set layout center");
                target.position = Center;
                var lobby = Group(target, "S01_Lobby");
                var contents = Copy(Required(source, "S01_Lobby"), lobby, "Contents");
                var art = Group(lobby, "Art");
                var overhead = Group(lobby, "Overhead");
                var lighting = Group(lobby, "Lighting");
                var walls = Group(lobby, "Walls");
                var connections = Group(lobby, "FutureConnections");
                var gameplay = Group(lobby, "Gameplay");

                Copy(Required(source, "Mood/Ceilings/Ceiling_Lobby"), overhead);
                foreach (var path in new[] { "Mood/Dressing/Lobby_TentPile", "Mood/Dressing/Lobby_FloorStains",
                    "Mood/ProtoFill/Lobby", "Mood/Furnishings/Lobby", "Mood/InteriorArt/WR01_SlumpedTent",
                    "Mood/InteriorArt/WR02_LobbyCostumeRail" }) Copy(Required(source, path), art, path.Replace('/', '_'));
                Copy(Required(source, "Mood/Rooms/Lobby"), overhead, "Lobby_FlagsAndCloth");
                Copy(Required(source, "Mood/Ceilingworks/Chains_Lobby"), overhead);
                foreach (Transform item in Required(source, "Mood/InteriorArt"))
                {
                    if (item.name.StartsWith("Lobby_SaggingCanvas") || item.name.StartsWith("Lobby_RustCrossBeam")) Copy(item, overhead);
                    if (new[] { "CircusStorageCrate_0", "StorageTrunk_0", "CanvasOnCrate_0", "CircusStorageCrate_1", "StorageTrunk_1", "CanvasOnCrate_1" }.Contains(item.name)) Copy(item, art);
                    if (item.name == "Lobby_WarmPractical") Copy(item, lighting);
                }
                foreach (Transform item in Required(source, "Mood/Lights"))
                    if (item.name.StartsWith("Lobby_")) Copy(item, item.name.Contains("Skylight") ? overhead : lighting);
                foreach (Transform item in Required(source, "Mood/VisibilityLighting"))
                    if (item.name.StartsWith("Lobby_")) Copy(item, lighting);
                foreach (var name in new[] { "Spot_TentPile", "Spot_ScheduleBoard", "Spot_InfoBoard" })
                    Copy(Required(source, "Mood/LightingPass/" + name), lighting);
                // These pipe pieces are entirely over the lobby; shared runs beyond it stay in the source.
                foreach (Transform item in Required(source, "Mood/Ceilingworks/Pipes"))
                    if (item.position.z >= 61.3f && item.position.z <= 78.7f && item.position.x >= -9.1f && item.position.x <= 10f)
                        Copy(item, overhead);

                // Keep the existing central tent/prop composition, moving the copied assembly to the hub center.
                var tentOffset = new Vector3(.7f, 0, -2f);
                art.Find("Mood_InteriorArt_WR01_SlumpedTent").position += tentOffset;
                art.Find("Mood_Dressing_Lobby_TentPile").position += tentOffset;
                var furniture = art.Find("Mood_Furnishings_Lobby");
                furniture.Find("PackedTrunk").position = Center + new Vector3(-8.4f, 0, -2.8f);
                furniture.Find("WaitingChair").position = Center + new Vector3(-8.5f, 0, -.9f);

                // Copied wall modules are fitted around planned connections; no other rooms are built.
                WallSide(source, walls, connections, Vector3.forward, new[] { -4f, 5f }, new[] { "09 연습실", "02 숙소 / 화장실" });
                WallSide(source, walls, connections, Vector3.back, new[] { -5f, 5f }, new[] { "11 무대 장치 보관실", "12 화물 적재 장소" });
                WallSide(source, walls, connections, Vector3.left, new[] { -5.5f, 5.5f }, new[] { "03 상점", "10 작업실 / 지하" });
                WallSide(source, walls, connections, Vector3.right, new[] { -5.5f, 0f, 6f }, new[] { "04 소품실", "06 무대 통로", "08 전문가 대기실" });
                foreach (Transform child in contents)
                    if (child.name.StartsWith("Sign_") || child.name == "WaitingRoomDoor_Frame") child.gameObject.SetActive(false);

                var spawn = Copy(Required(source, "WaitingRoomGameplay/SpawnPoints"), gameplay);
                var spawnNames = new[] { "Spawn_1", "Spawn_2", "Spawn_3", "Spawn_4" };
                var spawnX = new[] { -1f, 1f, -3f, 3f };
                for (int i = 0; i < spawnNames.Length; i++) spawn.Find(spawnNames[i]).position = Center + new Vector3(spawnX[i], .1f, -5.4f);
                Copy(Required(source, "WaitingRoomGameplay/ScheduleBoard"), gameplay);
                Copy(Required(source, "NightSchedule/ScheduleCloseUp"), gameplay);
                var lounge = Group(lobby, "LoungeReservation");
                lounge.localPosition = new Vector3(0, 0, -6.8f);
                // Location only: lounge construction is a later stage.

                int remapped = RemapReferences(lobby, source);
                var external = RemapExistingReferences(scene, source, lobby);
                Physics.SyncTransforms();
                AssertUnchanged(original, source);
                ValidateStructure(lobby, source);
                Undo.CollapseUndoOperations(undo);
                EditorSceneManager.MarkSceneDirty(scene);
                File.WriteAllLines(Path.Combine(ReportDir, "stage-02-copy-manifest.md"),
                    new[] { "# 2단계 로비 복제 목록", "", "원본: WaitingRoomGreybox. 복제 후 원본 ID·직렬화·하위 활성값 보존 확인.",
                        "", $"새 로비 오브젝트: {lobby.GetComponentsInChildren<Transform>(true).Length}, 내부 참조 재연결: {remapped}, 바깥 참조 재연결: {external}", "" }.Concat(Manifest));
                return $"Lobby built; objects={lobby.GetComponentsInChildren<Transform>(true).Length}; remapped={remapped}; outerRefs={external}; sourceUnchanged=true";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static Transform Required(Transform root, string path) => root.Find(path) ?? throw new InvalidOperationException("Missing original: " + path);
        static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create lobby group");
            go.transform.SetParent(parent, false);
            return go.transform;
        }
        static Transform Copy(Transform original, Transform parent, string name = null, bool registerMap = true)
        {
            var copy = Object.Instantiate(original.gameObject, parent, true);
            copy.name = name ?? original.name;
            Undo.RegisterCreatedObjectUndo(copy, "Duplicate lobby source");
            if (registerMap) Map(original, copy.transform);
            Manifest.Add("- " + AnimationUtility.CalculateTransformPath(original, null) + " → " + AnimationUtility.CalculateTransformPath(copy.transform, null));
            return copy.transform;
        }
        static void Map(Transform original, Transform copy)
        {
            Copies[original.gameObject] = copy.gameObject;
            var a = original.GetComponents<Component>(); var b = copy.GetComponents<Component>();
            if (a.Length != b.Length || original.childCount != copy.childCount) throw new InvalidOperationException("Clone mismatch");
            for (int i = 0; i < a.Length; i++) if (a[i] != null) Copies[a[i]] = b[i];
            for (int i = 0; i < original.childCount; i++) Map(original.GetChild(i), copy.GetChild(i));
        }
        static Transform TargetTransform(Object target) => target is GameObject go ? go.transform : (target as Component)?.transform;
        static int RemapReferences(Transform lobby, Transform original)
        {
            int changed = 0;
            foreach (var component in lobby.GetComponentsInChildren<Component>(true))
            {
                if (component == null) throw new InvalidOperationException("Missing copied script");
                var so = new SerializedObject(component); var p = so.GetIterator();
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null) continue;
                    if (Copies.TryGetValue(p.objectReferenceValue, out var replacement)) { p.objectReferenceValue = replacement; changed++; }
                    else if (TargetTransform(p.objectReferenceValue) is Transform tr && tr.IsChildOf(original))
                        throw new InvalidOperationException($"Uncopied reference: {component.name}.{p.propertyPath} → {tr.name}");
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return changed;
        }
        static int RemapExistingReferences(Scene scene, Transform original, Transform lobby)
        {
            int changed = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || component.transform.IsChildOf(original) || component.transform.IsChildOf(lobby)) continue;
                var so = new SerializedObject(component); var p = so.GetIterator();
                while (p.Next(true))
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue != null && Copies.TryGetValue(p.objectReferenceValue, out var replacement))
                    { Undo.RecordObject(component, "Connect copied lobby"); p.objectReferenceValue = replacement; changed++; }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return changed;
        }

        static void WallSide(Transform source, Transform walls, Transform connections, Vector3 outward, float[] doors, string[] labels)
        {
            bool northSouth = Mathf.Abs(outward.z) > .5f;
            float half = northSouth ? 10 : 9;
            Vector3 tangent = northSouth ? Vector3.right : Vector3.forward;
            Vector3 edge = Center + outward * (northSouth ? 9 : 10);
            var template = Required(source, northSouth ? "Walls/W_z+9/Seg0" : "Walls/W_x-10/Seg0");
            float cursor = -half;
            for (int i = 0; i <= doors.Length; i++)
            {
                float end = i == doors.Length ? half : doors[i] - 1.5f;
                Segment(template, walls, edge + tangent * ((cursor + end) * .5f), end - cursor, 10, 5, northSouth, "Wall");
                if (i == doors.Length) break;
                Vector3 point = edge + tangent * doors[i];
                Segment(template, walls, point, 3, 7, 6.5f, northSouth, "DoorHeader");
                var portal = Group(connections, "Portal_" + labels[i].Split(' ')[0]);
                portal.position = point;
                var barrier = Segment(template, portal, point, 3, 3, 1.5f, northSouth, "TemporaryClosure");
                barrier.GetComponent<Renderer>().sharedMaterial = Required(source, "S07_StageEntrance_Temp/CurtainFold_0").GetComponent<Renderer>().sharedMaterial;
                var sign = Copy(Required(source, "S01_Lobby/Sign_② 숙소"), portal, "DestinationSign", false);
                sign.gameObject.SetActive(true);
                sign.SetPositionAndRotation(point - outward * .23f + Vector3.up * 3.45f, Quaternion.LookRotation(outward));
                var text = sign.GetComponent<TextMesh>(); text.text = labels[i] + "\n준비 중"; text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center; text.fontSize = 64; text.characterSize = .085f;
                text.color = new Color(.86f, .75f, .55f);
                var approach = Group(portal, "Approach"); approach.position = point - outward * 2f + Vector3.up * .1f;
                cursor = doors[i] + 1.5f;
            }
        }
        static Transform Segment(Transform template, Transform parent, Vector3 position, float length, float height, float y, bool northSouth, string name)
        {
            if (length <= .01f) throw new InvalidOperationException("Overlapping doorway layout");
            var t = Copy(template, parent, name, false);
            t.position = new Vector3(position.x, y, position.z);
            t.rotation = Quaternion.identity;
            t.localScale = northSouth ? new Vector3(length, height, .3f) : new Vector3(.3f, height, length);
            return t;
        }

        static Dictionary<int, string> Snapshot(Transform source) => source.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.GetInstanceID(), State);
        static string State(Transform t) => t.name + "|" + t.gameObject.activeSelf + "|" + t.position.ToString("R") + "|" +
            string.Join("|", t.GetComponents<Component>().Select(c => c == null ? "MISSING" : EditorJsonUtility.ToJson(c)));
        static void AssertUnchanged(Dictionary<int, string> before, Transform source)
        {
            var after = Snapshot(source);
            if (before.Count != after.Count || before.Any(p => !after.TryGetValue(p.Key, out var value) || p.Value != value))
                throw new InvalidOperationException("Preserved Greybox content changed");
        }
        static void ValidateStructure(Transform lobby, Transform source)
        {
            if (source.gameObject.activeSelf || !lobby.gameObject.activeInHierarchy) throw new InvalidOperationException("Activation mismatch");
            if (lobby.GetComponentsInChildren<PlayerSpawnPoints>().Length != 1 || lobby.GetComponentsInChildren<WaitingRoomSpawn>().Length != 1)
                throw new InvalidOperationException("Expected one active lobby spawn provider");
            var spawn = lobby.Find("Gameplay/SpawnPoints").GetComponent<PlayerSpawnPoints>();
            var points = new SerializedObject(spawn).FindProperty("points");
            if (points.arraySize != 4) throw new InvalidOperationException("Expected four spawn references");
            for (int i = 0; i < 4; i++)
                if (!(points.GetArrayElementAtIndex(i).objectReferenceValue is Transform point) || !point.IsChildOf(lobby))
                    throw new InvalidOperationException("Spawn still references original");
            var floor = lobby.Find("Contents/Floor").GetComponent<Collider>();
            foreach (Transform p in lobby.Find("Gameplay/SpawnPoints"))
                if (p.name.StartsWith("Spawn_"))
                {
                    if (!floor.Raycast(new Ray(p.position + Vector3.up, Vector3.down), out _, 3)) throw new InvalidOperationException("No spawn floor: " + p.name);
                    if (Physics.OverlapCapsule(p.position + Vector3.up * .4f, p.position + Vector3.up * 1.5f, .3f, ~0, QueryTriggerInteraction.Ignore).Any(c => c != floor && c.transform.IsChildOf(lobby)))
                        throw new InvalidOperationException("Spawn obstructed: " + p.name);
                }
        }

        public static string Validate()
        {
            var scene = SceneManager.GetActiveScene();
            var source = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoomGreybox").transform;
            var lobby = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom_LayoutA").transform.Find("S01_Lobby");
            Physics.SyncTransforms(); ValidateStructure(lobby, source);
            return $"Structure PASS; active objects={lobby.GetComponentsInChildren<Transform>().Length}; four floor-supported spawns; legacy inactive";
        }

        public static string Capture(string name, bool overheadView)
        {
            var root = GameObject.Find("WaitingRoom_LayoutA").transform.Find("S01_Lobby");
            var roof = root.Find("Overhead"); bool roofActive = roof.gameObject.activeSelf;
            var cameraObject = new GameObject("LayoutA_TemporaryCapture") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .022f, .025f);
            camera.nearClipPlane = .05f; camera.farClipPlane = 120;
            camera.orthographic = overheadView; camera.orthographicSize = 12;
            camera.fieldOfView = 72;
            camera.transform.position = overheadView ? Center + new Vector3(0, 30, 0) : Center + new Vector3(0, 1.65f, -7.5f);
            camera.transform.rotation = overheadView ? Quaternion.Euler(90, 0, 0) : Quaternion.LookRotation(new Vector3(0, .5f, 3) - new Vector3(0, 1.65f, -7.5f));
            var rt = new RenderTexture(1440, 1080, 24); rt.Create();
            var previous = RenderTexture.active; Texture2D texture = null;
            try
            {
                if (overheadView) roof.gameObject.SetActive(false);
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                texture = new Texture2D(1440, 1080, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1440, 1080), 0, 0); texture.Apply();
                Directory.CreateDirectory("Temp/Task075");
                string file = "Temp/Task075/" + name + ".png"; File.WriteAllBytes(file, texture.EncodeToPNG()); return Path.GetFullPath(file);
            }
            finally
            {
                roof.gameObject.SetActive(roofActive); RenderTexture.active = previous;
                camera.targetTexture = null; Object.DestroyImmediate(cameraObject);
                if (texture != null) Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt);
            }
        }
    }
}
