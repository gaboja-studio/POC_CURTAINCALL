using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    /// <summary>
    /// 탐색 배치물(구조 A 34개)의 단색 식별 상자를 도형 조합 외형으로 바꾼다(2026-10-30 확정 "배치 오브젝트 외형 생성").
    /// 기능 컴포넌트·충돌체·ID는 건드리지 않고, 각 대상 아래 PropShape 묶음을 만들어 숨김/열림 목록에 같이 넣는다. 다시 실행하면 새로 만든다.
    /// </summary>
    public static class StructureAPropShapes
    {
        static Transform layout;
        static readonly Dictionary<string, Material> materials = new();
        static System.Random rng;

        [MenuItem("CurtainCall/Waiting Room/Apply Exploration Prop Shapes")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.isDirty || !scene.path.EndsWith("/WaitingRoomBase.unity"))
                throw new InvalidOperationException("Saved WaitingRoomBase EditMode required.");
            layout = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom_LayoutA").transform;
            materials.Clear(); foreach (var r in layout.GetComponentsInChildren<Renderer>(true)) foreach (var m in r.sharedMaterials) if (m != null && !materials.ContainsKey(m.name)) materials[m.name] = m;
            rng = new System.Random(7);
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Exploration prop shapes");
            int count = 0;
            try
            {
                foreach (var group in layout.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Exploration_A").ToArray())
                    foreach (Transform target in group.Cast<Transform>().ToArray())
                    {
                        var old = target.Find("PropShape"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                        if (target.GetComponent<ClueInspectable>() is { } clue) { Document(target, clue); count++; }
                        else if (target.GetComponent<ToolTarget>() is { } tool) { Cabinet(target, tool); count++; }
                        else if (target.GetComponent<ExplorationGate>() is { } gate && target.Find("Visual") is { } visual)
                        {
                            var size = visual.localScale;
                            if (gate.IsBreakable) { Crate(target, gate, visual); count++; }
                            else if (size.y < .1f && size.x < .4f) { Key(target, gate, visual); count++; }
                            else if (target.name == "steelBox") { SteelBox(target, gate, visual); count++; }
                        }
                    }
                Undo.CollapseUndoOperations(undo);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Debug.Log($"Exploration prop shapes applied to {count} objects.");
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        // ---- 종류별 외형 ----

        // 파괴 상자: 나무 궤짝(판자 틈·모서리 기둥·철 띠·못).
        static void Crate(Transform root, ExplorationGate gate, Transform visual)
        {
            var b = Bounds(visual); var shape = Shape(root);
            var wood = Mat("LA_Cardboard", "V4_Wood", "LA_Wood"); var dark = Mat("V4_Black", "V4_Iron_Dark"); var iron = Mat("V4_Iron_Dark");
            visual.GetComponent<Renderer>().sharedMaterial = wood;
            var band = visual.Find("Band"); if (band != null) band.GetComponent<Renderer>().sharedMaterial = Mat("V4_RustyMetal");
            float w = b.size.x, h = b.size.y, d = b.size.z; var c = b.center;
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
                Box(shape, "Post", new Vector3(c.x + sx * (w * .5f - .03f), c.y, c.z + sz * (d * .5f - .03f)), new Vector3(.07f, h + .01f, .07f), dark);
            for (int i = 1; i <= 2; i++)
            {
                float y = b.min.y + h * i / 3f;
                Box(shape, "Gap", new Vector3(c.x, y, b.min.z - .002f), new Vector3(w - .14f, .012f, .006f), dark);
                Box(shape, "Gap", new Vector3(c.x, y, b.max.z + .002f), new Vector3(w - .14f, .012f, .006f), dark);
                Box(shape, "Gap", new Vector3(b.min.x - .002f, y, c.z), new Vector3(.006f, .012f, d - .14f), dark);
                Box(shape, "Gap", new Vector3(b.max.x + .002f, y, c.z), new Vector3(.006f, .012f, d - .14f), dark);
            }
            Box(shape, "LidSeam", new Vector3(c.x, b.max.y + .003f, c.z), new Vector3(w * .6f, .006f, .012f), dark);
            for (int i = 0; i < 6; i++) Box(shape, "Nail", new Vector3(b.min.x + .08f + (i % 3) * (w - .16f) * .5f, b.max.y - .06f, i < 3 ? b.min.z - .004f : b.max.z + .004f), new Vector3(.02f, .02f, .006f), iron);
            Register(gate, shape);
        }

        // 캐비넷·서랍·잠긴 상자: 손잡이·자물쇠 판·환기 슬릿·발.
        static void Cabinet(Transform root, ToolTarget tool)
        {
            var body = root.Find("Body"); var hinge = root.Find("Hinge"); if (body == null || hinge == null) return;
            var b = Bounds(body); var shape = Shape(root); var iron = Mat("V4_Iron_Dark"); var brass = Mat("LA_Bronze", "V4_Gold_Trim");
            bool tall = b.size.y > 1.2f;
            var door = hinge.Find("Door"); var doorR = door != null ? door.GetComponent<Renderer>() : null;
            if (doorR != null) doorR.sharedMaterial = Mat("LA_SteelPlate");
            // 문은 경첩 아래라 열릴 때 같이 돈다. 손잡이·슬릿은 문 아래에 둔다.
            var front = door != null ? door : body; var db = Bounds(front); var outward = -root.forward; var face = db.center + outward * (db.extents.magnitude * 0f + .035f);
            var on = door != null ? hinge : shape;
            float handleY = tall ? b.min.y + b.size.y * .55f : db.center.y;
            var handle = Cyl(on, "Handle", new Vector3(db.max.x - .12f * root.right.x - .0f, handleY, 0f), .018f, .14f, brass, root.right);
            handle.position = db.center + root.right * (db.size.x * .5f - .12f) + outward * .04f + Vector3.up * (handleY - db.center.y);
            var plate = Box(on, "LockPlate", db.center + root.right * (db.size.x * .5f - .12f) + outward * .036f + Vector3.up * (handleY - db.center.y - .11f), new Vector3(.06f, .07f, .01f), iron, root.rotation);
            if (tall) for (int i = 0; i < 3; i++) Box(on, "Vent", db.center + outward * .034f + Vector3.up * (db.size.y * .32f - i * .045f), new Vector3(db.size.x * .5f, .012f, .006f), Mat("V4_Black"), root.rotation);
            else for (int i = 0; i < 2; i++) Box(on, "Rail", db.center + outward * .034f + Vector3.up * ((i == 0 ? 1 : -1) * db.size.y * .3f), new Vector3(db.size.x * .8f, .02f, .006f), iron, root.rotation);
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
                Box(shape, "Foot", b.center + root.right * sx * (b.size.x * .5f - .06f) + root.forward * sz * (b.size.z * .5f - .06f) + Vector3.down * (b.size.y * .5f - .02f), new Vector3(.06f, .04f, .06f), iron, root.rotation);
            if (tall) Box(shape, "TopTrim", new Vector3(b.center.x, b.max.y + .01f, b.center.z), new Vector3(b.size.x + .04f, .02f, b.size.z + .04f), iron, root.rotation);
            _ = tool; _ = plate;
        }

        // 강철 상자: 리벳·뚜껑 이음새·걸쇠.
        static void SteelBox(Transform root, ExplorationGate gate, Transform visual)
        {
            var b = Bounds(visual); var shape = Shape(root); var iron = Mat("V4_Iron_Dark"); var brass = Mat("LA_Bronze", "V4_Gold_Trim");
            visual.GetComponent<Renderer>().sharedMaterial = Mat("LA_SteelPlate");
            float lidY = b.max.y - .18f;
            Box(shape, "LidSeam", new Vector3(b.center.x, lidY, b.min.z - .003f), new Vector3(b.size.x, .008f, .006f), Mat("V4_Black"));
            Box(shape, "LidSeam", new Vector3(b.center.x, lidY, b.max.z + .003f), new Vector3(b.size.x, .008f, .006f), Mat("V4_Black"));
            Box(shape, "LidSeam", new Vector3(b.min.x - .003f, lidY, b.center.z), new Vector3(.006f, .008f, b.size.z), Mat("V4_Black"));
            Box(shape, "LidSeam", new Vector3(b.max.x + .003f, lidY, b.center.z), new Vector3(.006f, .008f, b.size.z), Mat("V4_Black"));
            for (int i = 0; i < 5; i++)
            {
                float t = (i + .5f) / 5f;
                Sphere(shape, "Rivet", new Vector3(Mathf.Lerp(b.min.x + .05f, b.max.x - .05f, t), b.max.y - .06f, b.min.z - .004f), .025f, iron);
                Sphere(shape, "Rivet", new Vector3(Mathf.Lerp(b.min.x + .05f, b.max.x - .05f, t), b.max.y - .06f, b.max.z + .004f), .025f, iron);
                Sphere(shape, "Rivet", new Vector3(b.min.x - .004f, b.max.y - .06f, Mathf.Lerp(b.min.z + .05f, b.max.z - .05f, t)), .025f, iron);
                Sphere(shape, "Rivet", new Vector3(b.max.x + .004f, b.max.y - .06f, Mathf.Lerp(b.min.z + .05f, b.max.z - .05f, t)), .025f, iron);
            }
            Box(shape, "Hasp", new Vector3(b.center.x, lidY - .02f, b.min.z - .012f), new Vector3(.08f, .1f, .02f), iron);
            Box(shape, "Padlock", new Vector3(b.center.x, lidY - .09f, b.min.z - .025f), new Vector3(.06f, .07f, .03f), brass);
            foreach (float sx in new[] { -1f, 1f }) Box(shape, "Strap", new Vector3(b.center.x + sx * b.size.x * .3f, b.center.y, b.center.z), new Vector3(.05f, b.size.y + .006f, b.size.z + .006f), Mat("V4_RustyMetal"));
            Register(gate, shape);
        }

        // 열쇠: 고리(원기둥 테)·대(원기둥)·날(상자 2). 바닥에 눕혀 둔다.
        static void Key(Transform root, ExplorationGate gate, Transform visual)
        {
            var b = Bounds(visual); var shape = Shape(root); var brass = Mat("LA_Bronze", "V4_Gold_Trim");
            visual.GetComponent<Renderer>().enabled = false;
            var axis = root.right; var c = b.center; float y = b.min.y + .012f;
            Cyl(shape, "Bow", c - axis * .11f + Vector3.up * (y - c.y), .055f, .02f, brass, Vector3.up);
            Cyl(shape, "BowHole", c - axis * .11f + Vector3.up * (y - c.y + .001f), .028f, .024f, Mat("V4_Black"), Vector3.up);
            Cyl(shape, "Shaft", c + axis * .02f + Vector3.up * (y - c.y), .012f, .2f, brass, axis);
            Box(shape, "Bit", c + axis * .1f + root.forward * .025f + Vector3.up * (y - c.y), new Vector3(.025f, .02f, .04f), brass, root.rotation);
            Box(shape, "Bit", c + axis * .06f + root.forward * .02f + Vector3.up * (y - c.y), new Vector3(.02f, .02f, .03f), brass, root.rotation);
            Register(gate, shape);
        }

        // 문서: 벽 낙서는 스프레이 획, 바닥 문서는 종이 결·글줄·접힘, 지도는 격자·경로, 노트는 스프링.
        static void Document(Transform root, ClueInspectable clue)
        {
            var doc = root.Find("Document"); if (doc == null) return;
            var b = Bounds(doc); var shape = Shape(root); bool wall = b.size.y > b.size.x * .5f && b.size.y > .2f;
            var ink = Mat("V4_Black"); var paint = Mat("LA_TentRed", "V4_RustyMetal"); var paper = Mat("LA_WallConcrete");
            doc.GetComponent<Renderer>().sharedMaterial = wall ? Mat("LA_WallConcrete") : paper;
            string n = root.name;
            if (wall)
            {
                // 벽면 앞 3mm에 스프레이 획. 낙서 번호별로 다른 모양.
                var outward = Outward(doc); var right = Vector3.Cross(Vector3.up, outward).normalized; var face = b.center + outward * (Mathf.Min(b.size.x, b.size.z) * .5f + .003f);
                var color = n.Contains("hidden") || n.Contains("uv") || n.Contains("Prefix") ? ink : paint;
                int strokes = 7 + rng.Next(4);
                for (int i = 0; i < strokes; i++)
                {
                    float u = ((float)rng.NextDouble() - .5f) * .38f, v = ((float)rng.NextDouble() - .5f) * .22f, len = .06f + (float)rng.NextDouble() * .14f, ang = (float)rng.NextDouble() * 180f;
                    var stroke = Box(shape, "Stroke", face + right * u + Vector3.up * v, new Vector3(len, .018f, .004f), color, Quaternion.LookRotation(-outward, Vector3.up) * Quaternion.Euler(0, 0, ang));
                    _ = stroke;
                }
                Box(shape, "Drip", face + right * .1f + Vector3.down * .13f, new Vector3(.012f, .07f, .004f), color, Quaternion.LookRotation(-outward, Vector3.up));
            }
            else
            {
                float top = b.max.y + .002f; var right = root.right; var fwd = root.forward; var c = new Vector3(b.center.x, top, b.center.z);
                float w = Mathf.Max(b.size.x, b.size.z), h = Mathf.Min(b.size.x, b.size.z); bool alongX = b.size.x >= b.size.z;
                var lineAxis = alongX ? Vector3.right : Vector3.forward; var lineStep = alongX ? Vector3.forward : Vector3.right;
                if (n.Contains("map"))
                {
                    var blue = Mat("LA_TentCream", "LA_WallConcrete");
                    for (int i = 1; i < 4; i++) Box(shape, "Grid", c + lineStep * (h * (i / 4f - .5f)), alongX ? new Vector3(w * .9f, .004f, .006f) : new Vector3(.006f, .004f, w * .9f), ink);
                    for (int i = 1; i < 5; i++) Box(shape, "Grid", c + lineAxis * (w * (i / 5f - .5f)), alongX ? new Vector3(.006f, .004f, h * .9f) : new Vector3(h * .9f, .004f, .006f), ink);
                    for (int i = 0; i < 6; i++) Box(shape, "Room", c + lineAxis * (w * ((float)rng.NextDouble() - .5f) * .8f) + lineStep * (h * ((float)rng.NextDouble() - .5f) * .7f), new Vector3(.05f, .005f, .04f), blue);
                    Box(shape, "Route", c + Vector3.up * .001f, alongX ? new Vector3(w * .5f, .005f, .01f) : new Vector3(.01f, .005f, w * .5f), paint);
                }
                else if (n.Contains("Notebook") || n.Contains("notebook"))
                {
                    doc.GetComponent<Renderer>().sharedMaterial = Mat("LA_TentRed", "V4_RustyMetal");
                    for (int i = 0; i < 7; i++) Cyl(shape, "Ring", c + lineStep * (h * .5f - .005f) + lineAxis * (w * (i / 6f - .5f) * .8f), .008f, .025f, Mat("V4_Iron_Dark"), lineStep);
                    Box(shape, "Label", c + Vector3.up * .001f, alongX ? new Vector3(w * .5f, .004f, h * .3f) : new Vector3(h * .3f, .004f, w * .5f), paper);
                }
                else
                {
                    int lines = n.Contains("newspaper") ? 9 : 6; float margin = n.Contains("newspaper") ? .9f : .7f;
                    if (n.Contains("newspaper")) Box(shape, "Headline", c + lineStep * (h * .35f), alongX ? new Vector3(w * .8f, .004f, .03f) : new Vector3(.03f, .004f, w * .8f), ink);
                    for (int i = 0; i < lines; i++)
                    {
                        float t = (i + 1f) / (lines + 1f) - .5f; float len = w * margin * (.6f + (float)rng.NextDouble() * .4f);
                        Box(shape, "Line", c + lineStep * (h * t * .9f) + lineAxis * (w * margin * .5f - len * .5f) * (n.Contains("newspaper") ? 0f : 1f) * -1f, alongX ? new Vector3(len, .004f, .008f) : new Vector3(.008f, .004f, len), ink);
                    }
                    Box(shape, "Crease", c + Vector3.up * .001f, alongX ? new Vector3(.004f, .005f, h) : new Vector3(h, .005f, .004f), Mat("V4_Iron_Dark"));
                }
            }
            _ = clue;
        }

        // ---- 공통 ----
        static Transform Shape(Transform root)
        { var go = new GameObject("PropShape"); Undo.RegisterCreatedObjectUndo(go, "Prop shape"); go.transform.SetParent(root, false); go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; return go.transform; }
        static Bounds Bounds(Transform t) { var rs = t.GetComponentsInChildren<Renderer>(true); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
        static Vector3 Outward(Transform doc)
        {
            // 벽 문서는 얇은 축이 벽 법선. 벽에서 떨어진 쪽(방 안쪽)을 바깥으로 본다.
            var b = Bounds(doc); var axis = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
            return Physics.Raycast(b.center + axis * .2f, -axis, .4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? axis : -axis;
        }
        static Material Mat(params string[] names)
        {
            foreach (var n in names) if (materials.TryGetValue(n, out var m)) return m;
            foreach (var n in names) { var found = materials.FirstOrDefault(kv => kv.Key.Contains(n.Replace("LA_", "").Replace("V4_", ""))).Value; if (found != null) return found; }
            return materials["LA_SteelPlate"];
        }
        static Transform Prim(Transform parent, PrimitiveType type, string name, Vector3 at, Vector3 size, Material material, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; Undo.RegisterCreatedObjectUndo(go, "Prop shape part");
            Object.DestroyImmediate(go.GetComponent<Collider>()); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, rotation); go.transform.localScale = Divide(size, parent.lossyScale);
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return go.transform;
        }
        static Transform Box(Transform parent, string name, Vector3 at, Vector3 size, Material material) => Prim(parent, PrimitiveType.Cube, name, at, size, material, Quaternion.identity);
        static Transform Box(Transform parent, string name, Vector3 at, Vector3 size, Material material, Quaternion rotation) => Prim(parent, PrimitiveType.Cube, name, at, size, material, rotation);
        static Transform Sphere(Transform parent, string name, Vector3 at, float diameter, Material material) => Prim(parent, PrimitiveType.Sphere, name, at, Vector3.one * diameter, material, Quaternion.identity);
        static Transform Cyl(Transform parent, string name, Vector3 at, float radius, float length, Material material, Vector3 axis)
            => Prim(parent, PrimitiveType.Cylinder, name, at, new Vector3(radius * 2, length * .5f, radius * 2), material, Quaternion.FromToRotation(Vector3.up, axis.normalized));
        static Vector3 Divide(Vector3 a, Vector3 b) => new(a.x / Mathf.Max(b.x, 1e-4f), a.y / Mathf.Max(b.y, 1e-4f), a.z / Mathf.Max(b.z, 1e-4f));
        static void Register(ExplorationGate gate, Transform shape)
        {
            var so = new SerializedObject(gate);
            foreach (var field in new[] { "hiddenVisuals", "hideOnOpen" })
            {
                var p = so.FindProperty(field); if (p == null) continue;
                var list = Enumerable.Range(0, p.arraySize).Select(i => p.GetArrayElementAtIndex(i).objectReferenceValue).Where(o => o != null).ToList();
                if (field == "hideOnOpen" && list.Count == 0) continue;
                if (!list.Contains(shape.gameObject)) list.Add(shape.gameObject);
                p.arraySize = list.Count; for (int i = 0; i < list.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }
            so.ApplyModifiedProperties();
        }
    }
}
