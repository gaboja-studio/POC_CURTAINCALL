using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Editor
{
    /// <summary>
    /// 대기실 겹친 면 정리(2026-10-31): 다른 물체의 면이 같은 평면·같은 방향으로 겹치면 카메라가 움직일 때 번갈아 보인다(z-fighting).
    /// 통로 천장판은 지붕 상자보다 1cm 아래로, 바깥 껍데기(OuterWall)·커튼 뒤 검은 방·셔터 덮개·무대 진입 바닥은 겹친 면만 1cm 물린다.
    /// 같은 메시·재질·위치로 완전히 겹친 중복은 하나만 남긴다. 다시 실행해도 같은 결과다.
    /// </summary>
    public static class WaitingRoomOverlapFix
    {
        const float Inset = .01f, Tolerance = .005f, MinArea = .05f, PlayerFarClip = 150f;

        [MenuItem("Tools/CurtainCall/대기실/겹친 면 정리")]
        public static void ApplyMenu() => Debug.Log(Apply());

        public static string Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var root = scene.GetRootGameObjects().First(g => g.name == "WaitingRoom_LayoutA").transform;
            int ceilings = LowerPassageCeilings(root) + RaiseFreightCarFloor(root);
            var index = new PlaneIndex(root.GetComponentsInChildren<MeshRenderer>(false).Where(r => r.enabled && !IsLoser(r)));
            int trimmed = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(false).Where(r => r.enabled && IsLoser(r)).ToArray())
                trimmed += TrimCoplanarFaces(r, index);
            int removed = RemoveDuplicates(scene, root);
            int cameras = SetPlayerFarClip(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            return $"passage ceilings/roofs and lift floor fixed {ceilings},faces trimmed {trimmed}, duplicates removed {removed}, far clip set {cameras}";
        }

        // 겹칠 때 물러나는 쪽: 방 바깥을 덮는 껍데기와 보조 면. 방 안쪽 벽·바닥이 항상 이긴다.
        static bool IsLoser(Renderer r)
        {
            var t = r.transform; var p = t.parent; if (p == null || t.childCount > 0) return false;
            if (t.name == "OuterWall" && (p.name.StartsWith("OuterSkin_") || p.name.StartsWith("Skin_"))) return true;
            if (p.name.StartsWith("Room_") && p.parent != null && p.parent.name == "CurtainBayRooms") return true;
            if (t.name == "Housing" && p.name == "WaitingRoomShutter") return true;
            return t.name == "ApproachFloor" && p.name == "StageProcessionalRoute";
        }

        // 통로 바닥·천장 메시의 천장판이 지붕 상자 밑면·원통 벽 개구부 윗면과 같은 높이라 천장판은 1cm 내리고(바닥판 높이는 그대로),
        // 원통 벽 두께 안까지 들어간 지붕 밑면은 1cm 올린다(벽 개구부 윗면이 보이게). 지붕과 벽 사이가 뜨지 않게 통로 벽 윗면도 같이 올린다.
        static int LowerPassageCeilings(Transform root)
        {
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(false).Where(t => t.name == "PassageFloorCeiling" && t.parent.name.StartsWith("Passage_")).ToArray())
            {
                var roof = t.parent.Find("PassageRoof"); if (roof == null) continue;
                var roofR = roof.GetComponent<Renderer>();
                float target = roofR.bounds.min.y - Inset, top = t.GetComponent<Renderer>().bounds.max.y, y0 = t.position.y;
                if (top > target + .001f)
                {
                    Undo.RecordObject(t, "Lower passage ceiling");
                    var s = t.localScale; s.y *= (target - y0) / (top - y0); t.localScale = s; n++;
                    top = target;
                }
                float bottom = roofR.bounds.min.y; if (bottom - top >= Inset * 1.5f) continue;
                float rise = Inset * 2 - (bottom - top);
                Undo.RecordObject(roof, "Raise passage roof underside");
                float h = roofR.bounds.size.y; var rs = roof.localScale; rs.y *= (h - rise) / h; roof.localScale = rs; roof.position += Vector3.up * rise * .5f;
                foreach (var w in t.parent.Cast<Transform>().Where(w => w.name == "PassageWall"))
                {
                    var wb = w.GetComponent<Renderer>().bounds; if (wb.max.y >= roofR.bounds.min.y - .001f) continue;
                    Undo.RecordObject(w, "Raise passage wall"); float add = roofR.bounds.min.y - wb.max.y;
                    var ws = w.localScale; ws.y *= (wb.size.y + add) / wb.size.y; w.localScale = ws; w.position += Vector3.up * add * .5f;
                }
                n++;
            }
            return n;
        }

        // 화물 승강기 바닥 윗면이 복도 바닥과 같은 높이라 1cm 올린다(정지한 승강기, 스크립트 없음).
        static int RaiseFreightCarFloor(Transform root)
        {
            var car = root.Find("S08_ElevatorHall/FreightLift/CarFloor"); if (car == null || car.GetComponent<Renderer>().bounds.max.y >= Inset * .5f) return 0;
            Undo.RecordObject(car, "Raise freight car floor"); car.position += Vector3.up * Inset; return 1;
        }

        static int TrimCoplanarFaces(MeshRenderer r, PlaneIndex index)
        {
            var t = r.transform; if (!AxisAligned(t.rotation)) return 0;
            int n = 0;
            for (int axis = 0; axis < 3; axis++)
                foreach (int sign in new[] { -1, 1 })
                {
                    var b = r.bounds; if (b.size[axis] <= Inset * 3) continue;
                    float plane = sign > 0 ? b.max[axis] : b.min[axis];
                    if (index.Overlap(axis, sign, plane, b) < MinArea) continue;
                    // 물린 자리에 또 다른 면이 있으면(1cm 간격으로 붙은 부재) 빈 자리까지 더 물린다. 그래야 다시 실행해도 그대로다.
                    float cut = Inset;
                    while (cut < Inset * 4 && index.Overlap(axis, sign, plane - sign * cut, b) >= MinArea) cut += Inset;
                    if (b.size[axis] <= cut * 3) continue;
                    Undo.RecordObject(t, "Trim coplanar face");
                    float keep = sign > 0 ? b.min[axis] : b.max[axis];
                    var local = t.InverseTransformDirection(Axis(axis));
                    int i = Mathf.Abs(local.x) > .9f ? 0 : Mathf.Abs(local.y) > .9f ? 1 : 2;
                    var s = t.localScale; s[i] *= (b.size[axis] - cut) / b.size[axis]; t.localScale = s;
                    var nb = r.bounds; t.position += Axis(axis) * (keep - (sign > 0 ? nb.min[axis] : nb.max[axis]));
                    n++;
                }
            return n;
        }

        // 같은 메시·재질·월드 행렬로 겹친 중복. 스크립트가 붙었거나 참조하는 물체는 건드리지 않는다.
        static int RemoveDuplicates(UnityEngine.SceneManagement.Scene scene, Transform root)
        {
            var referenced = new HashSet<GameObject>();
            foreach (var mb in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)))
            {
                if (mb == null) continue;
                var it = new SerializedObject(mb).GetIterator();
                while (it.Next(true))
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null)
                    {
                        if (it.objectReferenceValue is GameObject g) referenced.Add(g);
                        else if (it.objectReferenceValue is Component c) referenced.Add(c.gameObject);
                    }
            }
            var seen = new HashSet<string>(); int n = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(false).ToArray())
            {
                var mf = r.GetComponent<MeshFilter>();
                if (!r.enabled || mf == null || mf.sharedMesh == null || r.transform.childCount > 0) continue;
                var m = r.transform.localToWorldMatrix;
                string key = mf.sharedMesh.GetInstanceID() + "|" + string.Join(",", r.sharedMaterials.Select(x => x ? x.GetInstanceID() : 0))
                    + "|" + string.Join(",", Enumerable.Range(0, 16).Select(i => Mathf.Round(m[i] * 1000)));
                if (seen.Add(key)) continue;
                if (referenced.Contains(r.gameObject) || r.GetComponents<MonoBehaviour>().Length > 0) continue;
                Undo.DestroyObjectImmediate(r.gameObject); n++;
            }
            return n;
        }

        // Cinemachine 렌즈가 편집 중에도 Main Camera를 덮어써서 카메라만 바꾸면 5000으로 돌아간다. 렌즈와 카메라를 같이 맞춘다.
        // 먼 거리 깊이 정밀도가 좋아져 몇 mm 간격의 면도 덜 깜빡인다.
        static int SetPlayerFarClip(UnityEngine.SceneManagement.Scene scene)
        {
            int n = 0;
            foreach (var c in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)))
            {
                if (c == null) continue;
                if (c is Camera cam && cam.CompareTag("MainCamera") && cam.farClipPlane != PlayerFarClip)
                { Undo.RecordObject(cam, "Far clip"); cam.farClipPlane = PlayerFarClip; n++; continue; }
                if (c.GetType().Name != "CinemachineCamera") continue;
                var so = new SerializedObject(c); var far = so.FindProperty("Lens.FarClipPlane");
                if (far == null || far.floatValue == PlayerFarClip) continue;
                far.floatValue = PlayerFarClip; so.ApplyModifiedProperties(); n++;
            }
            return n;
        }

        static bool AxisAligned(Quaternion q)
        {
            for (int a = 0; a < 3; a++) { var v = q * Axis(a); if (Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)) < .999f) return false; }
            return true;
        }

        static Vector3 Axis(int a) => a == 0 ? Vector3.right : a == 1 ? Vector3.up : Vector3.forward;
        static Vector2 Flat(Vector3 p, int axis) => axis == 0 ? new Vector2(p.y, p.z) : axis == 1 ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);

        // 축에 나란한 삼각형을 (축, 방향, 평면 위치)로 묶어 두고, 상자 한 면과 겹치는 넓이를 잰다.
        sealed class PlaneIndex
        {
            readonly Dictionary<(int, int, int), List<Vector2[]>> tris = new();

            public PlaneIndex(IEnumerable<MeshRenderer> renderers)
            {
                foreach (var r in renderers)
                {
                    var mf = r.GetComponent<MeshFilter>(); var mesh = mf ? mf.sharedMesh : null;
                    if (mesh == null || !mesh.isReadable || mesh.vertexCount > 20000) continue;
                    var M = r.transform.localToWorldMatrix; var v = mesh.vertices.Select(p => M.MultiplyPoint3x4(p)).ToArray(); var idx = mesh.triangles;
                    for (int i = 0; i < idx.Length; i += 3)
                    {
                        Vector3 A = v[idx[i]], B = v[idx[i + 1]], C = v[idx[i + 2]]; var n = Vector3.Cross(B - A, C - A);
                        if (n.sqrMagnitude < 1e-6f) continue; n.Normalize();
                        int axis = Mathf.Abs(n.x) > .999f ? 0 : Mathf.Abs(n.y) > .999f ? 1 : Mathf.Abs(n.z) > .999f ? 2 : -1; if (axis < 0) continue;
                        var key = (axis, n[axis] > 0 ? 1 : -1, Mathf.RoundToInt(A[axis] / Tolerance));
                        if (!tris.TryGetValue(key, out var list)) tris[key] = list = new List<Vector2[]>();
                        list.Add(new[] { Flat(A, axis), Flat(B, axis), Flat(C, axis) });
                    }
                }
            }

            public float Overlap(int axis, int sign, float plane, Bounds b)
            {
                Vector2 lo = Flat(b.min, axis), hi = Flat(b.max, axis); float area = 0; int k = Mathf.RoundToInt(plane / Tolerance);
                for (int d = -1; d <= 1; d++)
                    if (tris.TryGetValue((axis, sign, k + d), out var list))
                        foreach (var tri in list) area += ClipArea(tri, lo, hi);
                return area;
            }

            static float ClipArea(Vector2[] tri, Vector2 lo, Vector2 hi)
            {
                var poly = new List<Vector2>(tri);
                for (int e = 0; e < 4 && poly.Count > 0; e++)
                {
                    int c = e % 2; float lim = e < 2 ? lo[c] : hi[c]; bool keepAbove = e < 2;
                    var next = new List<Vector2>();
                    for (int i = 0; i < poly.Count; i++)
                    {
                        Vector2 p = poly[i], q = poly[(i + 1) % poly.Count];
                        bool pin = keepAbove ? p[c] >= lim : p[c] <= lim, qin = keepAbove ? q[c] >= lim : q[c] <= lim;
                        if (pin) next.Add(p);
                        if (pin != qin) next.Add(p + (q - p) * ((lim - p[c]) / (q[c] - p[c])));
                    }
                    poly = next;
                }
                float a = 0; for (int i = 0; i < poly.Count; i++) a += poly[i].x * poly[(i + 1) % poly.Count].y - poly[(i + 1) % poly.Count].x * poly[i].y;
                return Mathf.Abs(a) * .5f;
            }
        }
    }
}
