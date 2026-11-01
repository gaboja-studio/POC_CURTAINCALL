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
            // 다듬기(2026-11-01): 뚜껑 판자 이음새.
            foreach (float k in new[] { -1f, 1f }) Box(shape, "TopSeam", new Vector3(c.x, b.max.y + .003f, c.z + k * d / 6f), new Vector3(w - .1f, .006f, .008f), dark);
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
            // 다듬기(2026-11-01): 문 둘레를 한 단 눌린 판처럼 보이게 테두리를 두른다.
            foreach (float sy in new[] { -1f, 1f }) Box(on, "PanelEdge", db.center + outward * .032f + Vector3.up * sy * (db.size.y * .5f - .03f), new Vector3(db.size.x * .9f, .012f, .006f), iron, root.rotation);
            foreach (float sx in new[] { -1f, 1f }) Box(on, "PanelEdge", db.center + outward * .032f + root.right * sx * (db.size.x * .45f), new Vector3(.012f, db.size.y - .06f, .006f), iron, root.rotation);
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
            // 다듬기(2026-11-01): 여덟 모서리에 덧댄 철 모서리.
            foreach (float sx in new[] { -1f, 1f }) foreach (float sy in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
                Box(shape, "Corner", b.center + Vector3.Scale(b.extents, new Vector3(sx, sy, sz)) - new Vector3(sx, sy, sz) * .02f, new Vector3(.055f, .055f, .055f), iron);
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
                // 벽 낙서는 받침판 없이 벽에 직접 그린 그림이다. 오버뷰에도 그림만 보인다(2026-11-01 사용자: 도형 그림, 숨은 단서는 내용 그림).
                doc.GetComponent<Renderer>().enabled = false;
                var outward = Outward(doc); var right = Vector3.Cross(outward, Vector3.up).normalized; // 정면에서 볼 때 오른쪽
                var face = WallFace(root, doc, outward) + outward * .004f;
                var color = n.Contains("hidden") || n.Contains("uv") || n.Contains("Prefix") ? ink : paint;
                var strokes = Drawing(root, n, right);
                foreach (var (a, c, width) in strokes) Stroke(shape, face, right, outward, a, c, width, color);
                // 스프레이가 흘러내린 자국 두 줄
                for (int i = 0; i < 2; i++)
                {
                    var (a, c, _) = strokes[rng.Next(strokes.Count)]; var low = a.y < c.y ? a : c; float len = .04f + (float)rng.NextDouble() * .05f;
                    Stroke(shape, face, right, outward, low, low + Vector2.down * len, .01f, color);
                }
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
                    if (n.Contains("confiscated")) Secret(shape, clue, new Vector3(c.x, b.min.y - .002f, c.z) + lineAxis * (w * .5f - .075f) - lineStep * (h * .5f - .055f), ink,
                        "노트 구석에 \"34\"라는 숫자가 새겨져 있다.", "34");
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

        // 낙서 그림(정면 기준 오른쪽·위 좌표, m). 손으로 그린 듯 끝점을 조금 흔든다.
        static List<(Vector2 a, Vector2 b, float w)> Drawing(Transform root, string n, Vector3 right)
        {
            var lines = new List<(Vector2, Vector2, float)>();
            void Poly(float w, params Vector2[] p) { for (int i = 1; i < p.Length; i++) lines.Add((Jitter(p[i - 1]), Jitter(p[i]), w)); }
            const float W = .026f;
            if (n == "graffiti1") Poly(W, new(0, .16f), new(-.17f, -.13f), new(.17f, -.13f), new(0, .16f));                       // 세모
            else if (n == "graffiti2") Poly(W, new(-.15f, -.15f), new(-.15f, .15f), new(.15f, .15f), new(.15f, -.15f), new(-.15f, -.15f)); // 네모
            else if (n == "graffiti-3") Poly(W, Enumerable.Range(0, 19).Select(i => new Vector2(Mathf.Cos(i * Mathf.PI / 9f), Mathf.Sin(i * Mathf.PI / 9f)) * .15f).ToArray()); // 동그라미
            else if (n == "graffiti4") { Poly(W, new(-.15f, -.15f), new(.15f, .15f)); Poly(W, new(-.15f, .15f), new(.15f, -.15f)); } // 엑스
            else if (n == "uv-1-detail")
            {
                // 무너뜨릴 녹슨 벽 쪽을 가리키는 화살표와 X
                var breach = root.parent != null && root.parent.parent != null ? root.parent.parent.Find("BreachWall") : null;
                float dir = breach != null && Vector3.Dot(breach.position - root.position, right) < 0 ? -1f : 1f;
                Poly(W, new(-.17f * dir, .04f), new(.12f * dir, .04f)); Poly(W, new(.03f * dir, .12f), new(.13f * dir, .04f), new(.03f * dir, -.04f));
                Poly(.022f, new(-.06f, -.06f), new(.04f, -.15f)); Poly(.022f, new(-.06f, -.15f), new(.04f, -.06f));
            }
            else if (n == "passwordPrefixClue")
            {
                // 숫자 "12". 왼쪽 기둥에 "1"이 가리지 않게 오른쪽으로 비켜 그린다.
                Poly(W, new(-.04f, .07f), new(.01f, .12f), new(.01f, -.12f));
                Poly(W, new(.1f, .12f), new(.23f, .12f), new(.23f, .0f), new(.1f, .0f), new(.1f, -.12f), new(.24f, -.12f));
            }
            else if (n == "hiddenGraffiti3")
            {
                // 캐비넷 그림 + 숫자 "3"
                Poly(.02f, new(-.18f, -.12f), new(-.18f, .13f), new(-.02f, .13f), new(-.02f, -.12f), new(-.18f, -.12f));
                Poly(.016f, new(-.1f, -.12f), new(-.1f, .13f));
                Poly(.016f, new(-.125f, -.01f), new(-.125f, .04f)); Poly(.016f, new(-.075f, -.01f), new(-.075f, .04f));
                Poly(.016f, new(-.17f, -.12f), new(-.17f, -.15f)); Poly(.016f, new(-.03f, -.12f), new(-.03f, -.15f));
                Poly(W, new(.04f, .12f), new(.17f, .12f), new(.17f, -.12f), new(.04f, -.12f)); Poly(W, new(.07f, .0f), new(.17f, .0f));
            }
            else
            {
                // 알 수 없는 낙서는 예전처럼 스프레이 획
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector2(((float)rng.NextDouble() - .5f) * .3f, ((float)rng.NextDouble() - .5f) * .2f); float ang = (float)rng.NextDouble() * Mathf.PI, len = .06f + (float)rng.NextDouble() * .12f;
                    var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len * .5f; lines.Add((c - d, c + d, .018f));
                }
            }
            return lines;
        }

        // 뒷면(아래쪽) 구석에 새긴 숫자. 오버뷰에서 뒤집어 마주 보면 찾는다(2026-11-01 사용자). 정면에서 볼 때 로컬 x가 오른쪽, z가 위쪽.
        static void Secret(Transform shape, ClueInspectable clue, Vector3 at, Material ink, string hint, string digits)
        {
            var mark = new GameObject("SecretMark").transform; Undo.RegisterCreatedObjectUndo(mark.gameObject, "Secret mark");
            mark.SetParent(shape, false); mark.SetPositionAndRotation(at, Quaternion.Euler(180f, 0f, 0f));
            const float H = .04f, Gap = .032f;
            var glyphs = new Dictionary<char, Vector2[][]>
            {
                ['3'] = new[] { new Vector2[] { new(0, 1), new(.6f, 1), new(.6f, .5f), new(.1f, .5f) }, new Vector2[] { new(.6f, .5f), new(.6f, 0), new(0, 0) } },
                ['4'] = new[] { new Vector2[] { new(0, 1), new(0, .42f), new(.68f, .42f) }, new Vector2[] { new(.5f, 1), new(.5f, 0) } },
            };
            float x0 = -(digits.Length * Gap) * .5f;
            for (int i = 0; i < digits.Length; i++)
                foreach (var line in glyphs[digits[i]])
                    for (int k = 1; k < line.Length; k++)
                    {
                        Vector2 a = new Vector2(x0 + i * Gap, -H * .5f) + line[k - 1] * H, e = new Vector2(x0 + i * Gap, -H * .5f) + line[k] * H, d = e - a, mid = (a + e) * .5f;
                        Box(mark, "Engrave", mark.TransformPoint(new Vector3(mid.x, 0, mid.y)), new Vector3(d.magnitude + .006f, .002f, .006f), ink,
                            mark.rotation * Quaternion.Euler(0, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0));
                    }
            var so = new SerializedObject(clue);
            so.FindProperty("secretMark").objectReferenceValue = mark;
            so.FindProperty("secretText").stringValue = hint;
            so.FindProperty("description").stringValue = "";
            so.ApplyModifiedProperties();
        }

        static Vector2 Jitter(Vector2 p) => p + new Vector2(((float)rng.NextDouble() - .5f) * .012f, ((float)rng.NextDouble() - .5f) * .012f);

        // 그림 한 획: 벽면(face)에 붙은 납작한 막대. 끝을 조금 겹쳐 꺾인 자리가 비지 않는다.
        static void Stroke(Transform shape, Vector3 face, Vector3 right, Vector3 outward, Vector2 a, Vector2 b, float width, Material color)
        {
            var d = b - a; float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; var mid = (a + b) * .5f;
            Box(shape, "Stroke", face + right * mid.x + Vector3.up * mid.y, new Vector3(d.magnitude + width, width, .004f), color,
                Quaternion.LookRotation(-outward, Vector3.up) * Quaternion.Euler(0, 0, angle));
        }

        // 낙서 자리 뒤의 실제 벽면(받침판 등 이 단서 자신의 충돌체는 뺀다). 없으면 받침판 뒷면.
        static Vector3 WallFace(Transform root, Transform doc, Vector3 outward)
        {
            var b = Bounds(doc); var from = b.center + outward * .3f;
            var hits = Physics.RaycastAll(from, -outward, .6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Where(h => !h.collider.transform.IsChildOf(root)).OrderBy(h => h.distance).ToArray();
            if (hits.Length > 0) return b.center + outward * Vector3.Dot(hits[0].point - b.center, outward);
            return b.center - outward * (Mathf.Min(b.size.x, b.size.z) * .5f);
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
