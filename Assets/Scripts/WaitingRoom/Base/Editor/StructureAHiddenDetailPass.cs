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
    /// 숨겨진 길·단서 외형 보정(2026-11-01 사용자 피드백). 다시 실행해도 같은 결과가 나온다.
    /// - 화장실 녹슨 벽: 옆 벽과 타일 무늬를 잇고, 벽 앞으로 튀어나온 문틀 철판을 넣고, 외곽선·조각 들뜸을 끄고, 벽 전체에 녹과 거미줄 금.
    /// - 기계실 배관함: 철창 대신 스테인리스 문이 달린 배관함. 부서지기 전에는 문이 키패드를 가린다.
    /// - 화장실 UV 낙서: 화살표·X 판은 빼고 단서 그림(<see cref="StructureAPropShapes"/>)으로 대신한다.
    /// </summary>
    public static class StructureAHiddenDetailPass
    {
        const string Folder = "Assets/Scenes/Tests/WaitingRoom/Base/LayoutA/";
        const string CrackAsset = Folder + "LA_RestroomBreachCracks.asset";
        const string CrackMaterial = Folder + "LA_CrackLine.mat";
        const string StainlessMaterial = Folder + "LA_StainlessCabinet.mat";
        const string LatchMaterial = Folder + "LA_CabinetLatch.mat";

        [MenuItem("CurtainCall/Waiting Room/Apply Hidden Detail Pass")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || !scene.path.EndsWith("/WaitingRoomBase.unity"))
                throw new InvalidOperationException("WaitingRoomBase EditMode required.");
            var layout = scene.GetRootGameObjects().Single(g => g.name == "WaitingRoom_LayoutA").transform;
            RestroomWall(layout.Find("S02_Dormitory/Restroom"));
            PipeBox(layout.Find("B1_MachineRoom/Exploration_A/PipeBox"));
            UvBoard(layout.Find("S02_Dormitory/Restroom/TrialUvMark"));
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Hidden detail pass applied.");
        }

        // ---- 화장실 녹슨 벽 ----

        static void RestroomWall(Transform restroom)
        {
            var wall = restroom.Find("BreachWall");
            var faces = wall.GetComponentsInChildren<Transform>(true).Where(t => t.name == "RestroomFace").ToArray();
            // 1) 무늬 잇기: 옆 벽(WallSurfaces) 같은 재질의 UV 규칙(u, v = 세계 x·y의 일차식)을 그대로 쓰고,
            //    옆 벽과 같은 평면에 둔다(1.8cm 뒤에 있어 경계가 선처럼 보이고 녹이 옆 벽에 잘려 보였다).
            var surfaces = restroom.Find("Shell/WallSurfaces");
            float faceZ = surfaces.GetComponent<Renderer>().bounds.min.z;
            var maps = UvMaps(surfaces, faceZ);
            foreach (var face in faces)
            {
                var r = face.GetComponent<Renderer>(); var mf = face.GetComponent<MeshFilter>();
                if (!maps.TryGetValue(r.sharedMaterial, out var map) || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh; var v = mesh.vertices; var uv = new Vector2[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    var p = face.TransformPoint(v[i]); p.z = faceZ; v[i] = face.InverseTransformPoint(p);
                    uv[i] = new Vector2(map.u.x * p.x + map.u.y * p.y + map.u.z, map.v.x * p.x + map.v.y * p.y + map.v.z);
                }
                mesh.vertices = v; mesh.uv = uv; mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            }

            // 2) 테두리 없애기: 벽 양옆 문틀 철판이 벽면보다 6cm 나와 어두운 테두리로 보였다. 벽면 3cm 안쪽에서 멈춘다.
            foreach (var reveal in restroom.Find("Shell/WallStructure").Cast<Transform>().Where(t => t.name == "DoorReveal"))
            {
                var b = reveal.GetComponent<Renderer>().bounds; float front = faceZ - .03f;
                if (b.max.z <= front + .001f) continue;
                Undo.RecordObject(reveal, "Recess door reveal");
                if (Quaternion.Angle(reveal.rotation, Quaternion.identity) > .1f) { reveal.GetComponent<Renderer>().enabled = false; continue; }
                var s = reveal.localScale; s.z *= (front - b.min.z) / b.size.z; reveal.localScale = s;
                reveal.position += Vector3.forward * ((front - b.max.z) * .5f);
            }

            // 외곽선 깊이판도 그리지 않는다(외곽선은 꺼 둠). 바닥 문턱 타일은 무늬가 어긋나 문 자리를 드러내므로 바닥 아래로 내린다.
            var proxy = wall.Find("OutlineProxy"); if (proxy != null) { var pr = proxy.GetComponent<Renderer>(); Undo.RecordObject(pr, "Proxy"); pr.enabled = false; }
            // 같은 이름의 문턱이 여러 개라 이 벽 바로 아래 것만 고른다.
            var wallX = wall.position.x;
            var sill = restroom.Find("Shell/WallStructure").Cast<Transform>().Where(t => t.name == "DoorSill")
                .FirstOrDefault(t => { var sb = t.GetComponent<Renderer>().bounds; return sb.min.x < wallX && sb.max.x > wallX && Mathf.Abs(sb.center.z - wall.position.z) < .6f; });
            if (sill != null && sill.GetComponent<Renderer>().bounds.max.y > -.004f)
            { Undo.RecordObject(sill, "Sill"); sill.position += Vector3.down * (sill.GetComponent<Renderer>().bounds.max.y + .006f); }

            // 3) 녹: 조각마다 붙여 무너질 때 같이 떨어진다. 옛 옅은 얼룩은 지우고, 가장자리가 부드러운 녹을 겹겹이 칠해 벽 전체를 덮는다.
            var quad = wall.GetComponentsInChildren<MeshFilter>(true).First(m => m.name == "RustStain").sharedMesh;
            foreach (var old in wall.GetComponentsInChildren<Transform>(true).Where(t => t.name == "RustStain").ToArray()) Undo.DestroyObjectImmediate(old.gameObject);
            var rng = new System.Random(11);
            var patina = Load<Material>(Folder + "LA_RustPatina.mat"); var streak = Load<Material>(Folder + "LA_RustStreak.mat");
            var blocks = wall.Cast<Transform>().Where(t => t.name.StartsWith("Block_")).ToArray();
            int layer = 0;
            foreach (var block in blocks)
            {
                var parts = block.name.Split('_'); int row = int.Parse(parts[1]);
                if (row > 7) continue;
                // 블록마다 개수·자리를 흩뜨려 격자처럼 보이지 않게 한다. 가장자리 열은 덜 칠해 경계가 들쭉날쭉하다.
                bool edge = parts[2] == "0" || parts[2] == "3";
                int count = row <= 4 ? rng.Next(edge ? 1 : 2, edge ? 3 : 5) : rng.Next(0, 2);
                for (int i = 0; i < count; i++)
                {
                    var mat = (row == 4 || row == 2) && i == 0 && rng.NextDouble() < .6 ? streak : patina;
                    float size = .35f + (float)rng.NextDouble() * .45f;
                    var go = new GameObject("RustStain"); Undo.RegisterCreatedObjectUndo(go, "Rust");
                    go.transform.SetParent(block, false);
                    go.transform.localPosition = new Vector3(((float)rng.NextDouble() - .5f) * .4f, ((float)rng.NextDouble() - .5f) * .4f - (mat == streak ? .12f : 0f), wall.InverseTransformPoint(new Vector3(0, 0, faceZ)).z + .002f + (layer++ % 8) * .0007f);
                    go.transform.localRotation = Quaternion.Euler(0, 180, mat == streak ? 0f : (float)rng.NextDouble() * 360f);
                    go.transform.localScale = mat == streak ? new Vector3(size * .8f, size * 1.4f, 1) : new Vector3(size, size, 1);
                    go.AddComponent<MeshFilter>().sharedMesh = quad;
                    var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }

            // 4) 금: 큐브 격자가 아니라 거미줄처럼 불규칙하게. 서 있는 동안 벽 전체의 실금, 칠 때마다 가운데에서 퍼지는 금이 늘어난다.
            var crackMat = CrackLineMaterial();
            var bounds = blocks.Select(b => b.GetComponent<Renderer>().bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            var lo = wall.InverseTransformPoint(bounds.min); var hi = wall.InverseTransformPoint(bounds.max);
            var rect = new Rect(Mathf.Min(lo.x, hi.x) + .02f, .03f, Mathf.Abs(hi.x - lo.x) - .04f, 2.17f); // 타일 벽(띠 아래)
            float z = wall.InverseTransformPoint(new Vector3(0, 0, faceZ)).z + .009f;
            var web = CrackWeb(new Vector2(rect.center.x, 1.15f), rect, 5, new System.Random(23));
            var hair = Hairlines(rect, new System.Random(31));
            if (AssetDatabase.LoadMainAssetAtPath(CrackAsset) != null) AssetDatabase.DeleteAsset(CrackAsset);
            var meshes = new List<Mesh>();
            var so = new SerializedObject(wall.GetComponent<BreakableWall>());
            var cracks = so.FindProperty("cracks");
            for (int i = 0; i < cracks.arraySize; i++)
            {
                var crack = (cracks.GetArrayElementAtIndex(i).objectReferenceValue as GameObject).transform;
                foreach (Transform child in crack.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
                Undo.RecordObject(crack, "Crack"); crack.localPosition = Vector3.zero; crack.localRotation = Quaternion.identity; crack.localScale = Vector3.one;
                meshes.Add(Line(crack, "Web", web[i], z, crackMat));
            }
            var standing = wall.Find("CrackWeb"); if (standing != null) Undo.DestroyObjectImmediate(standing.gameObject);
            var standingGo = new GameObject("CrackWeb"); Undo.RegisterCreatedObjectUndo(standingGo, "Crack web");
            standingGo.transform.SetParent(wall, false);
            meshes.Add(Line(standingGo.transform, "Hairline", hair, z - .001f, crackMat));
            AssetDatabase.CreateAsset(meshes[0], CrackAsset);
            foreach (var m in meshes.Skip(1)) AssetDatabase.AddObjectToAsset(m, CrackAsset);

            so.FindProperty("showOutline").boolValue = false;
            so.FindProperty("loosenPieces").boolValue = false;
            so.FindProperty("standingDetail").objectReferenceValue = standingGo;
            so.ApplyModifiedProperties();
        }

        // 옆 벽 재질별 UV 규칙. 벽면(faceZ) 위 삼각형 하나에서 u·v를 x·y의 일차식으로 푼다.
        static Dictionary<Material, (Vector3 u, Vector3 v)> UvMaps(Transform surfaces, float faceZ)
        {
            var result = new Dictionary<Material, (Vector3, Vector3)>();
            var mesh = surfaces.GetComponent<MeshFilter>().sharedMesh; var mats = surfaces.GetComponent<Renderer>().sharedMaterials;
            var v = mesh.vertices.Select(surfaces.TransformPoint).ToArray(); var uv = mesh.uv;
            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                var t = mesh.GetTriangles(s);
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    if (Mathf.Abs(a.z - faceZ) > .06f || Mathf.Abs(b.z - faceZ) > .06f || Mathf.Abs(c.z - faceZ) > .06f) continue;
                    if (!Solve(a, b, c, uv[t[i]].x, uv[t[i + 1]].x, uv[t[i + 2]].x, out var u) || !Solve(a, b, c, uv[t[i]].y, uv[t[i + 1]].y, uv[t[i + 2]].y, out var w)) continue;
                    result[mats[s]] = (u, w); break;
                }
            }
            return result;
        }

        static bool Solve(Vector3 a, Vector3 b, Vector3 c, float fa, float fb, float fc, out Vector3 k)
        {
            float det = a.x * (b.y - c.y) - a.y * (b.x - c.x) + (b.x * c.y - c.x * b.y);
            k = default; if (Mathf.Abs(det) < 1e-4f) return false;
            float kx = (fa * (b.y - c.y) - a.y * (fb - fc) + (fb * c.y - fc * b.y)) / det;
            float ky = (a.x * (fb - fc) - fa * (b.x - c.x) + (b.x * fc - c.x * fb)) / det;
            float k0 = (a.x * (b.y * fc - c.y * fb) - a.y * (b.x * fc - c.x * fb) + fa * (b.x * c.y - c.x * b.y)) / det;
            k = new Vector3(kx, ky, k0); return true;
        }

        // 거미줄 금: 맞은 자리에서 굽어 나가는 방사선 + 그 사이를 잇는 고리 + 잔가지. 단계(타격)마다 바깥으로 퍼진다.
        // 반환: 단계별 선분 목록(앞 단계는 이미 켜져 있으므로 각 단계는 새로 생기는 선만).
        static List<(Vector2 a, Vector2 b, float w)>[] CrackWeb(Vector2 center, Rect rect, int stages, System.Random rng)
        {
            var result = Enumerable.Range(0, stages).Select(_ => new List<(Vector2, Vector2, float)>()).ToArray();
            float[] radius = { .22f, .42f, .62f, .85f, 1.15f };
            int rays = 9; var rayPoints = new List<Vector2>[rays];
            float start = (float)rng.NextDouble() * 360f;
            for (int r = 0; r < rays; r++)
            {
                float angle = (start + r * 360f / rays + ((float)rng.NextDouble() - .5f) * 26f) * Mathf.Deg2Rad;
                var points = rayPoints[r] = new List<Vector2> { center }; var p = center; float heading = angle;
                while ((p - center).magnitude < radius[stages - 1])
                {
                    heading += ((float)rng.NextDouble() - .5f) * .7f;
                    var next = p + new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * (.06f + (float)rng.NextDouble() * .09f);
                    if (!rect.Contains(next)) break;
                    float d = (next - center).magnitude; int stage = Stage(d, radius);
                    result[stage].Add((p, next, Mathf.Lerp(.007f, .0035f, d / radius[stages - 1])));
                    if (rng.NextDouble() < .28) Branch(next, heading + (rng.NextDouble() < .5 ? .9f : -.9f), stage, rect, rng, result[stage], 3);
                    points.Add(next); p = next;
                }
            }
            // 고리: 이웃한 방사선의 같은 거리쯤을 약간 처지게 잇는다. 모두 잇지 않아 불규칙해 보인다.
            for (int s = 0; s < stages; s++)
                for (int r = 0; r < rays; r++)
                {
                    if (rng.NextDouble() < .35) continue;
                    float d = radius[s] * (.75f + (float)rng.NextDouble() * .2f);
                    if (!At(rayPoints[r], center, d, out var a) || !At(rayPoints[(r + 1) % rays], center, d, out var b)) continue;
                    var mid = (a + b) * .5f; mid += (center - mid).normalized * (a - b).magnitude * .12f;
                    result[s].Add((a, mid, .003f)); result[s].Add((mid, b, .003f));
                }
            return result;
        }

        static int Stage(float d, float[] radius) { for (int i = 0; i < radius.Length; i++) if (d <= radius[i]) return i; return radius.Length - 1; }

        static void Branch(Vector2 p, float heading, int stage, Rect rect, System.Random rng, List<(Vector2, Vector2, float)> into, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                heading += ((float)rng.NextDouble() - .5f) * .8f;
                var next = p + new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * (.04f + (float)rng.NextDouble() * .05f);
                if (!rect.Contains(next)) return;
                into.Add((p, next, .0025f)); p = next;
            }
        }

        static bool At(List<Vector2> ray, Vector2 center, float d, out Vector2 point)
        {
            for (int i = 1; i < ray.Count; i++)
            {
                float d0 = (ray[i - 1] - center).magnitude, d1 = (ray[i] - center).magnitude;
                if (d0 <= d && d1 >= d) { point = Vector2.Lerp(ray[i - 1], ray[i], (d - d0) / Mathf.Max(1e-4f, d1 - d0)); return true; }
            }
            point = default; return false;
        }

        // 서 있는 벽 전체의 실금: 가장자리·모서리에서 시작해 굽이치며 뻗고 가끔 갈라진다.
        static List<(Vector2 a, Vector2 b, float w)> Hairlines(Rect rect, System.Random rng)
        {
            var lines = new List<(Vector2, Vector2, float)>();
            for (int n = 0; n < 9; n++)
            {
                var p = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, (float)rng.NextDouble()), Mathf.Lerp(rect.yMin, rect.yMax, (float)rng.NextDouble()));
                float heading = (float)rng.NextDouble() * Mathf.PI * 2f; int steps = 6 + rng.Next(8);
                for (int i = 0; i < steps; i++)
                {
                    heading += ((float)rng.NextDouble() - .5f) * .9f;
                    var next = p + new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * (.05f + (float)rng.NextDouble() * .08f);
                    if (!rect.Contains(next)) break;
                    lines.Add((p, next, .0025f));
                    if (rng.NextDouble() < .2) Branch(next, heading + .8f, 0, rect, rng, lines, 2);
                    p = next;
                }
            }
            return lines;
        }

        // 선분 목록을 벽 앞면(z)에 납작한 띠 메시로 만든다(재질 하나, 그림자 없음).
        static Mesh Line(Transform parent, string name, List<(Vector2 a, Vector2 b, float w)> lines, float z, Material material)
        {
            var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
            foreach (var (a, b, w) in lines)
            {
                var dir = (b - a).normalized; var side = new Vector2(-dir.y, dir.x) * w * .5f; var ext = dir * w * .5f; int i = verts.Count;
                verts.Add(new Vector3(a.x - side.x - ext.x, a.y - side.y - ext.y, z)); verts.Add(new Vector3(a.x + side.x - ext.x, a.y + side.y - ext.y, z));
                verts.Add(new Vector3(b.x + side.x + ext.x, b.y + side.y + ext.y, z)); verts.Add(new Vector3(b.x - side.x + ext.x, b.y - side.y + ext.y, z));
                uvs.Add(Vector2.zero); uvs.Add(Vector2.right); uvs.Add(Vector2.one); uvs.Add(Vector2.up);
                // 양면으로 감고 법선은 화장실 쪽(+z)으로 둔다.
                tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2, i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            var mesh = new Mesh { name = "LA_" + parent.name + "_" + name };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.SetUVs(0, uvs);
            mesh.SetNormals(Enumerable.Repeat(Vector3.forward, verts.Count).ToList()); mesh.RecalculateBounds();
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Crack line");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return mesh;
        }

        static Material CrackLineMaterial() => MakeMaterial(CrackMaterial, new Color(.07f, .055f, .045f), 0f, .05f);

        // ---- 기계실 배관함 ----

        static void PipeBox(Transform box)
        {
            var wall = box.GetComponent<BreakableWall>();
            var so = new SerializedObject(wall); var piecesProp = so.FindProperty("pieces");
            var pieces = Enumerable.Range(0, piecesProp.arraySize).Select(i => piecesProp.GetArrayElementAtIndex(i).objectReferenceValue as Transform).Where(t => t != null).ToList();
            // 철창·옛 문은 지운다.
            foreach (var t in box.Cast<Transform>().Where(t => t.name == "GratingBar" || t.name.StartsWith("Door")).ToArray())
            { pieces.Remove(t); Undo.DestroyObjectImmediate(t.gameObject); }

            var steel = MakeMaterial(StainlessMaterial, new Color(.5f, .51f, .51f), .8f, .42f);
            var latch = MakeMaterial(LatchMaterial, new Color(.04f, .04f, .045f), .2f, .35f);
            foreach (var n in new[] { "BoxTop", "BoxSideL", "BoxSideR", "BoxPlinth" })
            { var r = box.Find(n).GetComponent<Renderer>(); Undo.RecordObject(r, "Steel"); r.sharedMaterial = steel; }

            // 몸체 바깥 크기(앞면 = 벽 반대쪽). 문은 앞면에 딱 맞는 판 + 접힌 테두리 + 세로 손잡이 + 경첩.
            var body = new[] { "BoxTop", "BoxSideL", "BoxSideR", "BoxPlinth" }.Select(n => box.Find(n).GetComponent<Renderer>().bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            var back = box.Find("BoxBack").GetComponent<Renderer>().bounds.center;
            var outward = Vector3.ProjectOnPlane(body.center - back, Vector3.up); outward = Mathf.Abs(outward.x) > Mathf.Abs(outward.z) ? new Vector3(Mathf.Sign(outward.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(outward.z));
            var across = Vector3.Cross(Vector3.up, outward); // 정면에서 볼 때 왼쪽
            float depth = Vector3.Dot(body.extents, new Vector3(Mathf.Abs(outward.x), 0, Mathf.Abs(outward.z)));
            float width = Vector3.Dot(body.size, new Vector3(Mathf.Abs(across.x), 0, Mathf.Abs(across.z)));
            var front = body.center + outward * (depth + .011f);
            var rot = Quaternion.LookRotation(outward, Vector3.up);
            float w = width - .03f, h = body.size.y - .14f; var c = front + Vector3.up * .02f;
            var made = new List<Transform>
            {
                Part(box, "Door", PrimitiveType.Cube, c, rot, new Vector3(w, h, .02f), steel),
                // 가장자리를 한 번 접은 테두리(이미지처럼 판 둘레가 살짝 도드라진다)
                Part(box, "DoorRim", PrimitiveType.Cube, c + Vector3.up * (h * .5f - .015f) + outward * .015f, rot, new Vector3(w, .03f, .006f), steel),
                Part(box, "DoorRim", PrimitiveType.Cube, c - Vector3.up * (h * .5f - .015f) + outward * .015f, rot, new Vector3(w, .03f, .006f), steel),
                Part(box, "DoorRim", PrimitiveType.Cube, c + across * (w * .5f - .015f) + outward * .015f, rot, new Vector3(.03f, h - .06f, .006f), steel),
                Part(box, "DoorRim", PrimitiveType.Cube, c - across * (w * .5f - .015f) + outward * .015f, rot, new Vector3(.03f, h - .06f, .006f), steel),
                // 열리는 쪽(오른쪽) 가운데 높이의 검은 세로 손잡이
                Part(box, "DoorLatch", PrimitiveType.Cube, c - across * (w * .5f - .1f) + outward * .02f, rot, new Vector3(.035f, .17f, .022f), latch),
                Part(box, "DoorLatchGrip", PrimitiveType.Cube, c - across * (w * .5f - .1f) + outward * .034f, rot, new Vector3(.018f, .12f, .012f), latch),
            };
            foreach (float y in new[] { h * .5f - .14f, -h * .5f + .14f })
                made.Add(Part(box, "DoorHinge", PrimitiveType.Cylinder, c + across * (w * .5f + .004f) + Vector3.up * y + outward * .006f, Quaternion.identity, new Vector3(.022f, .045f, .022f), steel));
            pieces.AddRange(made);
            piecesProp.arraySize = pieces.Count; for (int i = 0; i < pieces.Count; i++) piecesProp.GetArrayElementAtIndex(i).objectReferenceValue = pieces[i];
            // 친 자리에서 조각이 들뜨면 틈으로 키패드가 보인다. 무너질 때만 쏟아진다.
            so.FindProperty("loosenPieces").boolValue = false;
            so.ApplyModifiedProperties();
        }

        static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 at, Quaternion rotation, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; Undo.RegisterCreatedObjectUndo(go, "Pipe box part");
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(at, rotation); go.transform.localScale = size;
            go.transform.SetParent(parent, true);
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = material;
            if (name != "Door") r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 얇은 덧판의 그림자 점선 방지
            return go.transform;
        }

        // ---- 화장실 UV 낙서 ----

        static void UvBoard(Transform mark)
        {
            var uv = mark.GetComponent<UvMark>(); var so = new SerializedObject(uv); var marks = so.FindProperty("marks");
            var keep = Enumerable.Range(0, marks.arraySize).Select(i => marks.GetArrayElementAtIndex(i).objectReferenceValue as GameObject)
                .Where(g => g != null && g.name != "UvArrow" && g.name != "UvCross").ToList();
            marks.arraySize = keep.Count; for (int i = 0; i < keep.Count; i++) marks.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
            so.ApplyModifiedProperties();
            foreach (var n in new[] { "UvArrow", "UvCross" }) { var t = mark.Find(n); if (t != null) { Undo.RecordObject(t.gameObject, "UV board"); t.gameObject.SetActive(false); } }
        }

        // ---- 공통 ----

        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing " + path);

        static Material MakeMaterial(string path, Color color, float metallic, float smoothness)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(m); return m;
        }
    }
}
