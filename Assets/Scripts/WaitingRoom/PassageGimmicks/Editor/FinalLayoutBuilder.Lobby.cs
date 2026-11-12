using System.Collections.Generic;
using System.Linq;
using CurtainCall.WaitingRoom.Pursuit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.PassageGimmicks
{
    // 2단계: 옮긴 방에 맞춰 로비 원형 벽의 출입구를 다시 낸다.
    // 북쪽에 무대 입구 통로 입구를 내고, 남쪽 옛 입구를 막고 작업실·소품실 문 두 개를 낸다.
    public static partial class FinalLayoutBuilder
    {
        internal const string RootName = "FinalLayout";
        const string AssetFolder = "Assets/Scenes/Tests/WaitingRoom/Base/LayoutA/";
        const string WallMeshPath = AssetFolder + "LA_Frame8LobbyWall_Final.asset";
        const string MeshPath = AssetFolder + "LA_FinalLayout.asset";
        const float WallT = .5f, DrumH = 10f, DoorH = 4.2f, StageOpenH = 9.2f;
        // 옮긴 무대 입구 통로 입구(벽 포함) x 범위와 로비 쪽 끝 z.
        const float StageX0 = -14.43f, StageX1 = -4.97f, StageMouthZ = 91.65f;
        static readonly List<Mesh> lobbyMeshes = new();

        [MenuItem("CurtainCall/Waiting Room/Final Layout/2. Connect Lobby Openings")]
        static void ConnectMenu() => Debug.Log(ConnectLobby());

        public static string ConnectLobby()
        {
            var layout = GameObject.Find(LayoutPath)?.transform;
            if (layout == null || layout.Find(SwapMarker) == null) return "1단계(방 이동)를 먼저 실행하세요.";
            var scene = layout.gameObject.scene;
            var lobby = layout.Find("S01_Lobby/Frame8Lobby");
            var drum = lobby.Find("DrumWall"); var floorMat = lobby.Find("RoundFloor").GetComponent<Renderer>().sharedMaterial;
            var wallMats = drum.GetComponent<Renderer>().sharedMaterials; var wallMat = wallMats[0];
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Final layout: lobby openings");
            var old = layout.Find(RootName + "/LobbyConnections"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = Child(Child(layout, RootName), "LobbyConnections");
            lobbyMeshes.Clear();
            bool North(Vector3 c) => c.z > DomeC.y && c.x > StageX0 - .02f && c.x < StageX1 + .02f;
            bool Door(Vector3 c, float height) => false; // 남쪽은 옛 입구를 그대로 쓰므로 새로 뚫지 않는다

            // 벽 메시: 원본은 그대로 두고 구멍 낸 사본을 이 씬에서만 쓴다.
            var filter = drum.GetComponent<MeshFilter>();
            var source = AssetDatabase.LoadAssetAtPath<Mesh>(AssetFolder + "LA_Frame8LobbyWall.asset");
            var cut = Object.Instantiate(source); cut.name = "LA_Frame8LobbyWall_Final";
            var verts = source.vertices; int removed = 0;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                var tris = source.GetTriangles(s); var keep = new List<int>(tris.Length);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var c = (verts[tris[i]] + verts[tris[i + 1]] + verts[tris[i + 2]]) / 3f;
                    if (North(c) || Door(c, 99f)) { removed++; continue; }
                    keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
                }
                cut.SetTriangles(keep, s);
            }
            AssetDatabase.DeleteAsset(WallMeshPath); AssetDatabase.CreateAsset(cut, WallMeshPath);
            Undo.RecordObject(filter, "Cut lobby wall"); filter.sharedMesh = cut; PrefabUtility.RecordPrefabInstancePropertyModifications(filter);

            // 벽 충돌체와 구멍을 가로지르는 벽 장식을 끈다.
            int muted = 0;
            foreach (Transform c in lobby.Find("WallColliders"))
            {
                var b = c.GetComponent<Collider>().bounds;
                if (North(b.center) || Door(new Vector3(b.center.x, b.min.y, b.center.z), DoorH)) muted += Mute(c);
            }
            foreach (var r in lobby.GetComponentsInChildren<Renderer>(false))
            {
                if (r.transform == drum || r.name is "RoundFloor" or "DomeCap" or "DomeRib" or "CrownRing" or "RingBeam") continue;
                var b = r.bounds; var low = new Vector3(b.center.x, b.min.y, b.center.z);
                bool nearWall = Mathf.Abs(Vector2.Distance(new Vector2(b.center.x, b.center.z), DomeC) - LobbyR) < 1.2f;
                if (nearWall && ((North(low) && b.min.y < StageOpenH) || Door(low, DoorH))) muted += Mute(r.transform);
            }

            // 북쪽: 원형 벽과 통로 입구 사이 쐐기 바닥·천장, 서쪽·동쪽 마감 벽.
            var north = Child(root, "StageThreshold");
            ArcSlab(north, "Floor", floorMat, StageX0, StageX1, StageMouthZ + .05f, true, 0f, true);
            ArcSlab(north, "Soffit", wallMat, StageX0 - WallT * .5f, StageX1, StageMouthZ + .05f, true, StageOpenH, false);
            Box(north, "ReturnWall", wallMat, new(StageX0 - WallT * .5f, 0, ArcZ(StageX0, true) - .3f), new(StageX0, DrumH, StageMouthZ + .25f), true);
            Box(north, "ReturnWall", wallMat, new(StageX1, 0, ArcZ(StageX1, true) - .15f), new(StageX1 + WallT * .5f, DrumH, StageMouthZ + .25f), true);
            Box(north, "RoofCap", wallMat, new(StageX0 - WallT * .5f, StageOpenH, ArcZ(StageX0, true) - .3f), new(StageX1 + WallT * .5f, StageOpenH + .3f, StageMouthZ + .25f), false);

            // 남쪽: 옛 무대 입구는 전실 폭만 남기고 벽으로 막는다. 전실 위는 문 높이까지만 연다.
            var seal = Child(root, "SouthSeal");
            foreach (var (a, b) in new[] { (-10.6f, VestibuleX0), (VestibuleX1, -1.4f) })
            {
                int parts = Mathf.Max(1, Mathf.CeilToInt((b - a) / .65f));
                for (int i = 0; i < parts; i++) ChordWall(seal, "SealWall", wallMat, Mathf.Lerp(a, b, i / (float)parts), Mathf.Lerp(a, b, (i + 1) / (float)parts), false, 0, DrumH, true);
            }
            ChordWall(seal, "Lintel", wallMat, VestibuleX0, VestibuleX1, false, DoorH, DrumH, true);

            // 전실: 로비 남쪽 문 → 서쪽 작업실 문·동쪽 소품실 문. 옆벽은 두 방의 바깥 벽이고, 방 북벽보다 북쪽 구간만 새로 막는다.
            var hall = Child(root, "CraftVestibule");
            float arc0 = ArcZ(VestibuleX0, false), arc1 = ArcZ(VestibuleX1, false);
            Box(hall, "Floor", floorMat, new(VestibuleX0, -.2f, CraftSouthZ), new(VestibuleX1, 0, Mathf.Min(arc0, arc1)), true);
            ArcSlab(hall, "FloorArc", floorMat, VestibuleX0, VestibuleX1, Mathf.Min(arc0, arc1), false, 0f, true);
            Box(hall, "SideWall", wallMat, new(VestibuleX0 - .3f, 0, WorkshopTo.yMax - .05f), new(VestibuleX0, DoorH + .4f, arc0 + .3f), true);
            Box(hall, "SideWall", wallMat, new(VestibuleX1, 0, PropTo.yMax - .05f), new(VestibuleX1 + .3f, DoorH + .4f, arc1 + .3f), true);
            // 전실 남쪽 끝은 사무실 복도 북벽이 막는다. 같은 자리에 벽을 하나 더 세우면 두 벽이 겹쳐 깜빡이므로 세우지 않는다.
            Box(hall, "Roof", wallMat, new(VestibuleX0 - .3f, DoorH + .1f, CraftSouthZ - .5f), new(VestibuleX1 + .3f, DoorH + .4f, Mathf.Max(arc0, arc1) + .3f), true);
            SaveLobbyMeshes();

            // 추격·탐색 영역과 밤 철창을 전실로 옮긴다.
            float mid = (CraftSouthZ + arc0) * .5f;
            FitPassage(layout, scene, "F8_PropRoom", Rect.MinMaxRect(VestibuleX0, mid, VestibuleX1, arc1 + .6f));
            FitPassage(layout, scene, "WorkshopBranch", Rect.MinMaxRect(VestibuleX0, CraftSouthZ, VestibuleX1, mid));
            var gate = layout.Find("NightMaintenance/WorkshopGate");
            if (gate != null)
            {
                // 작업실 문(전실 서벽, 방 북벽에서 3.47m 남쪽)에 세운다. 철창 살은 원래부터 남북으로 늘어서 있다.
                var gb = Bounds(gate); Undo.RecordObject(gate, "Move workshop gate");
                gate.position += new Vector3(VestibuleX0 + .15f - gb.center.x, 0, WorkshopTo.yMax - 3.47f - gb.center.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(gate);
            }
            // 통로가 들어선 자리 위에 남은 깃발 줄을 끈다.
            var corridor = Rect.MinMaxRect(-19f, StageMouthZ, -.4f, 172f);
            foreach (Transform t in layout.Find("SupportFinishing"))
            {
                var rs = t.GetComponentsInChildren<Renderer>(false); if (rs.Length == 0) continue;
                var c = Bounds(t).center; if (corridor.Contains(new Vector2(c.x, c.z))) muted += Mute(t);
            }
            Physics.SyncTransforms();
            Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene);
            return $"로비 벽 삼각형 {removed}개 제거, 벽 조각·장식 {muted}개 끔, 북쪽 입구·남쪽 문 2개 연결";
        }

        // 원형 벽 안쪽 면이 x에서 지나는 z(북쪽 호 또는 남쪽 호).
        static float ArcZ(float x, bool north, float radius = LobbyR)
        {
            float d = Mathf.Sqrt(Mathf.Max(0, radius * radius - (x - DomeC.x) * (x - DomeC.x)));
            return north ? DomeC.y + d : DomeC.y - d;
        }

        // 원형 벽(안쪽 면)과 직선 z = edgeZ 사이를 메우는 판.
        static void ArcSlab(Transform parent, string name, Material material, float x0, float x1, float edgeZ, bool north, float y, bool faceUp)
        {
            int n = Mathf.CeilToInt((x1 - x0) / .4f);
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>(); var normals = new List<Vector3>();
            for (int i = 0; i <= n; i++)
            {
                float x = Mathf.Lerp(x0, x1, i / (float)n);
                foreach (float z in new[] { ArcZ(x, north) - (north ? .02f : -.02f), edgeZ })
                { v.Add(new Vector3(x, y, z)); uv.Add(new Vector2(x, z) * .25f); normals.Add(faceUp ? Vector3.up : Vector3.down); }
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                bool flip = faceUp == north; // 감는 방향을 위·아래 보는 면에 맞춘다
                if (flip) { tris.AddRange(new[] { a, b, c, c, b, d }); } else { tris.AddRange(new[] { a, c, b, c, d, b }); }
            }
            var mesh = new Mesh { name = parent.name + "_" + name }; mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(normals); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            lobbyMeshes.Add(mesh);
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (faceUp)
            {
                // 걸어 다니는 바닥: 판 아래로 두께를 둔 상자 충돌체.
                var box = go.AddComponent<BoxCollider>(); var b = mesh.bounds;
                box.center = new Vector3(b.center.x, y - .1f, b.center.z); box.size = new Vector3(b.size.x, .2f, b.size.z);
            }
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        // 원형 벽을 따라 x0~x1을 잇는 곧은 벽 조각(벽 두께만큼 바깥쪽).
        static void ChordWall(Transform parent, string name, Material material, float x0, float x1, bool north, float y0, float y1, bool collider)
        {
            float r = LobbyR + WallT * .5f;
            var a = new Vector3(x0, 0, ArcZ(x0, north, r)); var b = new Vector3(x1, 0, ArcZ(x1, north, r));
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation((a + b) * .5f + Vector3.up * (y0 + y1) * .5f, Quaternion.LookRotation((b - a).normalized));
            go.transform.localScale = new Vector3(WallT, y1 - y0, (b - a).magnitude + .04f);
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        internal static Transform Box(Transform parent, string name, Material material, Vector3 min, Vector3 max, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.position = (min + max) * .5f; go.transform.localScale = max - min;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go.transform;
        }

        internal static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name); if (t != null) return t;
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Final layout");
            go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); go.transform.localScale = Vector3.one;
            return go.transform;
        }

        internal static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        static void SaveLobbyMeshes()
        {
            AssetDatabase.DeleteAsset(MeshPath);
            if (lobbyMeshes.Count == 0) return;
            AssetDatabase.CreateAsset(lobbyMeshes[0], MeshPath);
            for (int i = 1; i < lobbyMeshes.Count; i++) AssetDatabase.AddObjectToAsset(lobbyMeshes[i], MeshPath);
            AssetDatabase.SaveAssets();
        }

        // 옛 연결 통로의 탐색 영역 상자와 추격 방 지점(같은 이름)을 새 자리(x·z 범위)로 옮긴다.
        static void FitPassage(Transform layout, UnityEngine.SceneManagement.Scene scene, string name, Rect to)
        {
            var area = layout.Find("ExplorationTimer/Frame8Areas/" + name);
            if (area != null)
            {
                var box = area.GetComponent<BoxCollider>(); Undo.RecordObject(area, "Fit passage area"); Undo.RecordObject(box, "Fit passage area");
                area.SetPositionAndRotation(new Vector3(to.center.x, 3f, to.center.y), Quaternion.identity);
                var s = area.lossyScale; box.center = Vector3.zero; box.size = new Vector3((to.width + .4f) / s.x, 8f / s.y, to.height / s.z);
            }
            var room = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "PursuitSystem")?.transform.Find(name);
            if (room == null) return;
            int i = 0;
            foreach (Transform point in room)
            {
                if (point.GetComponent<PursuitPlacement>() != null) continue;
                Undo.RecordObject(point, "Fit passage points");
                point.position = new Vector3(to.center.x + ((i % 2) * 2 - 1) * .7f, 0, Mathf.Lerp(to.yMin + .6f, to.yMax - .6f, i / 2 % 2));
                i++;
            }
        }
    }
}
