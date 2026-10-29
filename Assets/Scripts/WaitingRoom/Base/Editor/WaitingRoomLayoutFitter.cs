using System.Collections.Generic;
using System.Linq;
using CurtainCall.Cameras;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.EditorTools
{
    /// <summary>
    /// 3인칭 카메라(뒤 4.5m)가 좁은 방에서 벽 밖으로 나가 벽이 반투명해지는 문제를 줄인다.
    /// A) 좁은 방 넓히기: ② 숙소·화장실 깊이 7→12m, ④ 소품실 6.5→11.5m, ⑤ 폐기물 보관소 폭 8→12m, ⑧ 전문가 대기실 5→9m.
    ///    안쪽 벽을 밀고, 방 안쪽 절반에 있던 물건은 같은 만큼 함께 민다(앞쪽 절반·출입구 쪽은 그대로).
    /// B) 좁은 방(②④⑤⑥⑧)에 공용 <see cref="CameraViewZone"/>으로 가까운 마우스 시점(<see cref="ClosePresetPath"/>)을 건다.
    /// 그레이박스 로컬 좌표 기준이며, 이미 넓힌 씬에는 레이아웃을 다시 적용하지 않는다(카메라 구역은 다시 만든다).
    /// </summary>
    public static class WaitingRoomLayoutFitter
    {
        const string GreyboxName = "WaitingRoomGreybox";
        const string ZonesName = "CameraZones";
        const string MouseLookName = "ExpertServiceMouseLook";
        const string FreeLookPreset = "Assets/Resources/Settings/Camera/FreeThirdPerson.asset";
        public const string ClosePresetPath = "Assets/Scenes/Tests/WaitingRoom/Base/Prefabs/WaitingRoomCloseThirdPerson.asset";
        const int ClosePriority = 20; // 대기실 전체 마우스 시점(기본 10)보다 높게

        // 넓히기 전·후 경계
        const float DormBack = 16f, DormBackNew = 21f, DormFront = 9f;
        const float PropBack = -15.5f, PropBackNew = -20.5f, PropFront = -9f;
        const float JunkBack = -18f, JunkBackNew = -22f, JunkFront = -10f;
        const float ExpertBack = 9f, ExpertBackNew = 13f;

        // 넓힌 뒤 방 범위(x0, z0, x1, z1) — 카메라 구역·분위기 배치가 같이 쓴다
        public static readonly Rect Dormitory = Rect.MinMaxRect(-10f, DormFront, 3f, DormBackNew);
        public static readonly Rect Restroom = Rect.MinMaxRect(3f, DormFront, 10f, DormBackNew);
        public static readonly Rect PropRoom = Rect.MinMaxRect(-10f, PropBackNew, 10f, PropFront);
        public static readonly Rect JunkStorage = Rect.MinMaxRect(JunkBackNew, -9f, JunkFront, 9f);
        public static readonly Rect Corridor = WaitingRoomTunnelLayout.Tunnel; // 프로토타입 터널 구성(x 10~52)
        public static readonly Rect ExpertRoom = Rect.MinMaxRect(10f, 4f, 18f, ExpertBackNew);

        [MenuItem("Tools/CurtainCall/대기실/좁은 방 넓히기·가까운 카메라 구역")]
        static void ApplyMenu()
        {
            var scene = SceneManager.GetActiveScene();
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == GreyboxName);
            if (root == null) { Debug.LogError($"[WaitingRoomLayout] '{GreyboxName}' 없음"); return; }
            bool expanded = Fit(root.transform);
            FitMouseLookZone(scene, root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(expanded ? "[WaitingRoomLayout] 좁은 방을 넓히고 카메라 구역을 만들었습니다." : "[WaitingRoomLayout] 이미 넓힌 레이아웃 — 카메라 구역만 다시 만들었습니다.");
        }

        /// <summary>레이아웃을 넓히고(한 번만) 카메라 구역을 다시 만든다. 넓혔으면 true.</summary>
        public static bool Fit(Transform greybox)
        {
            bool expand = IsOriginalLayout(greybox);
            if (expand)
            {
                ExpandWalls(greybox);
                ExpandFloors(greybox);
                ShiftBackHalf(greybox.Find("S02_Dormitory"), Axis.Z, DormFront, DormBack, DormBackNew - DormBack);
                ShiftBackHalf(greybox.Find("S04_PropRoom"), Axis.Z, PropFront, PropBack, PropBackNew - PropBack);
                ShiftBackHalf(greybox.Find("S05_JunkStorage"), Axis.X, JunkFront, JunkBack, JunkBackNew - JunkBack);
            }
            BuildZones(greybox);
            return expand;
        }

        static bool IsOriginalLayout(Transform greybox)
        {
            var north = greybox.Find("Walls/W_02_North/Seg0");
            return north != null && Mathf.Abs(north.localPosition.z - DormBack) < 0.01f;
        }

        // ── 벽 ─────────────────────────────────────────────

        enum Axis { X, Z }

        static void ExpandWalls(Transform greybox)
        {
            var walls = greybox.Find("Walls");
            if (walls == null) return;
            foreach (Transform wall in walls)
                foreach (Transform seg in wall)
                {
                    var s = seg.localScale;
                    var p = seg.localPosition;
                    bool alongX = s.x > s.z;
                    float half = (alongX ? s.x : s.z) / 2f;
                    float a0 = (alongX ? p.x : p.z) - half, a1 = (alongX ? p.x : p.z) + half;
                    float k = alongX ? p.z : p.x;

                    // 벽 위치(고정 축) 이동: 숙소 북벽, 소품실 남벽, 폐기물 보관소 서벽, 전문가 대기실 북벽
                    if (alongX && Near(k, DormBack) && wall.name == "W_02_North") k = DormBackNew;
                    else if (alongX && Near(k, PropBack)) k = PropBackNew;
                    else if (!alongX && Near(k, JunkBack)) k = JunkBackNew;
                    else if (alongX && Near(k, ExpertBack) && wall.name == "W_08_North") k = ExpertBackNew;

                    // 벽 길이 끝점 이동
                    if (alongX)
                    {
                        if (Near(a0, JunkBack)) a0 = JunkBackNew;
                    }
                    else
                    {
                        if (Near(a1, DormBack)) a1 = DormBackNew;
                        if (Near(a0, PropBack)) a0 = PropBackNew;
                        if (wall.name == "W_08_East" && Near(a1, ExpertBack)) a1 = ExpertBackNew;
                    }

                    Undo.RecordObject(seg, "Fit Waiting Room Layout");
                    float mid = (a0 + a1) / 2f, len = a1 - a0;
                    seg.localPosition = alongX ? new Vector3(mid, p.y, k) : new Vector3(k, p.y, mid);
                    seg.localScale = alongX ? new Vector3(len, s.y, s.z) : new Vector3(s.x, s.y, len);
                }
        }

        static void ExpandFloors(Transform greybox)
        {
            SetFloor(greybox.Find("S02_Dormitory/Floor"), -10f, 10f, DormFront, DormBackNew);
            SetFloor(greybox.Find("S04_PropRoom/Floor"), -10f, 10f, PropBackNew, PropFront);
            SetFloor(greybox.Find("S05_JunkStorage/Floor"), JunkBackNew, JunkFront, -9f, 9f);
            SetFloor(greybox.Find("S08_ExpertRoom/Floor"), 10f, 18f, 4f, ExpertBackNew);
        }

        static void SetFloor(Transform floor, float x0, float x1, float z0, float z1)
        {
            if (floor == null) return;
            Undo.RecordObject(floor, "Fit Waiting Room Layout");
            floor.localPosition = new Vector3((x0 + x1) / 2f, floor.localPosition.y, (z0 + z1) / 2f);
            floor.localScale = new Vector3(x1 - x0, floor.localScale.y, z1 - z0);
        }

        // ── 방 안 물건 ─────────────────────────────────────────

        /// <summary>
        /// 방 중심선보다 안쪽(벽 쪽)에 중심이 있는 물건을 <paramref name="shift"/>만큼 민다.
        /// 깊이의 60% 이상에 걸친 묶음(예: 화장실 = 칸막이 + 세면대)은 자식 단위로 판단한다.
        /// 표지·바닥 글씨(TextMesh만 있는 오브젝트)는 가장 가까운 물건을 따라간다. 바닥은 따로 맞춘다.
        /// </summary>
        static void ShiftBackHalf(Transform room, Axis axis, float front, float back, float shift)
        {
            if (room == null) return;
            float mid = (front + back) / 2f, depth = Mathf.Abs(back - front);
            bool BackSide(float v) => back > front ? v > mid : v < mid;

            var objects = new List<(Transform t, Bounds b)>();
            var texts = new List<Transform>();
            void Collect(Transform t)
            {
                foreach (Transform c in t)
                {
                    if (c.name == "Floor") continue;
                    if (IsText(c)) { texts.Add(c); continue; }
                    if (!TryLocalBounds(room.parent, c, out var b)) continue;
                    float extent = axis == Axis.Z ? b.size.z : b.size.x;
                    if (extent >= depth * 0.6f && c.childCount > 0) { Collect(c); continue; }
                    objects.Add((c, b));
                }
            }
            Collect(room);

            var moved = new HashSet<Transform>();
            foreach (var (t, b) in objects)
                if (BackSide(axis == Axis.Z ? b.center.z : b.center.x)) moved.Add(t);

            foreach (var text in texts)
            {
                var lp = room.parent.InverseTransformPoint(text.position);
                var nearest = objects.OrderBy(o => Vector2.Distance(new Vector2(o.b.center.x, o.b.center.z), new Vector2(lp.x, lp.z))).FirstOrDefault();
                if (nearest.t != null && moved.Contains(nearest.t)) moved.Add(text);
            }

            var offset = room.parent.TransformVector(axis == Axis.Z ? new Vector3(0f, 0f, shift) : new Vector3(shift, 0f, 0f));
            foreach (var t in moved)
            {
                Undo.RecordObject(t, "Fit Waiting Room Layout");
                t.position += offset;
            }
        }

        static bool IsText(Transform t) =>
            t.childCount == 0 && t.GetComponent<TextMesh>() != null;

        static bool TryLocalBounds(Transform space, Transform t, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<TextMesh>() != null) continue;
                var b = r.bounds;
                var c = space.InverseTransformPoint(b.center);
                var lb = new Bounds(c, b.size);
                if (!found) { bounds = lb; found = true; }
                else bounds.Encapsulate(lb);
            }
            if (!found && t.childCount == 0)
            {
                bounds = new Bounds(space.InverseTransformPoint(t.position), Vector3.zero);
                found = true;
            }
            return found;
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.05f;

        // ── 카메라 구역 ─────────────────────────────────────────

        static void BuildZones(Transform greybox)
        {
            var old = greybox.Find(ZonesName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var preset = ClosePreset();
            var root = new GameObject(ZonesName).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Waiting Room Camera Zones");
            root.SetParent(greybox, false);

            Zone(root, "Zone_Dormitory", Dormitory, preset);
            Zone(root, "Zone_Restroom", Restroom, preset);
            Zone(root, "Zone_PropRoom", PropRoom, preset);
            Zone(root, "Zone_JunkStorage", JunkStorage, preset);
            Zone(root, "Zone_Corridor", Corridor, preset);
            Zone(root, "Zone_ExpertRoom", ExpertRoom, preset);
        }

        static void Zone(Transform parent, string name, Rect r, CameraViewPreset preset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            // 벽 두께 안쪽으로 조금 줄여 이웃 방과 겹치지 않게 한다
            go.transform.localPosition = new Vector3(r.center.x, 3f, r.center.y);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(r.width - 0.4f, 6f, r.height - 0.4f);
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var view = go.AddComponent<CameraViewZone>();
            var so = new SerializedObject(view);
            so.FindProperty("preset").objectReferenceValue = preset;
            so.FindProperty("priority").intValue = ClosePriority;
            so.FindProperty("exitPadding").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>공용 FreeThirdPerson을 바탕으로 거리만 줄인 테스트 씬 전용 시점. 이미 있으면 그대로 쓴다(값 조정 보존).</summary>
        static CameraViewPreset ClosePreset()
        {
            var preset = AssetDatabase.LoadAssetAtPath<CameraViewPreset>(ClosePresetPath);
            if (preset != null) return preset;
            var source = AssetDatabase.LoadAssetAtPath<CameraViewPreset>(FreeLookPreset);
            preset = source != null ? Object.Instantiate(source) : ScriptableObject.CreateInstance<CameraViewPreset>();
            preset.Kind = CameraViewKind.FreeThirdPerson;
            preset.Distance = 2.8f;
            preset.Height = 2.6f;
            preset.TransitionDuration = 0.35f;
            AssetDatabase.CreateAsset(preset, ClosePresetPath);
            AssetDatabase.SaveAssets();
            return preset;
        }

        /// <summary>넓힌 방까지 대기실 전체 마우스 시점 구역을 늘린다(ExpertServiceSceneBuilder와 같은 기준).</summary>
        public static void FitMouseLookZone(Scene scene, Transform greybox)
        {
            var zone = scene.GetRootGameObjects().FirstOrDefault(g => g.name == MouseLookName);
            if (zone == null || !zone.TryGetComponent<BoxCollider>(out var box)) return;
            var bounds = new Bounds(greybox.position, Vector3.zero);
            foreach (var renderer in greybox.GetComponentsInChildren<Renderer>())
                if (!(renderer is ParticleSystemRenderer)) bounds.Encapsulate(renderer.bounds); // 안개 파티클 경계는 원점까지 퍼져 있어 제외
            bounds.Expand(new Vector3(2f, 6f, 2f));
            Undo.RecordObject(zone.transform, "Fit Waiting Room Layout");
            Undo.RecordObject(box, "Fit Waiting Room Layout");
            zone.transform.position = bounds.center;
            box.center = Vector3.zero;
            box.size = bounds.size;
        }
    }
}
