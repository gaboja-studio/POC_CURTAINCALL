using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string VariantFinishGroup = "20_VariantStructureFinish";

        // Upgrade the saved B/C layouts without regenerating their routes or prefab references.
        [MenuItem("Tools/CurtainCall/탈출로/B·C 구조 마감 적용")]
        public static void ApplyVariantStructureFinish()
        {
            var scene = SceneManager.GetActiveScene();
            bool isB = scene.path == VariantBPath;
            if ((!isB && scene.path != VariantCPath) || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("B 또는 C 씬의 편집 모드가 필요합니다.");
            var background = scene.GetRootGameObjects().First(g => g.name == RootName).transform;
            var existing = background.Find(VariantFinishGroup);
            if (existing != null)
            {
                if (existing.Find("99_Complete") == null) throw new InvalidOperationException("이전 구조 마감이 완료되지 않았습니다. 적용 전 씬으로 복구 후 확인하세요.");
                return;
            }
            string id = isB ? "B" : "C";
            foreach (string prefix in new[] { "01_Understage_", "02_Approach_", "04_Bend_", "06_Regroup_", "07A_FinalRun_", "07B_ExitWorkroom_" })
                if (background.Find(prefix + id) == null) throw new InvalidOperationException("기존 B/C 구조가 완전하지 않습니다: " + prefix + id);
            foreach (string name in isB ? new[] { "03_TrampolineSwing_B", "05_CannonRope_B" } : new[] { "03_CannonRope_C", "05_TrampolineSwing_C" })
                if (background.Find(name) == null) throw new InvalidOperationException("묘기 공동이 없습니다: " + name);

            bConcrete = VariantFinishMaterial("brutal-concrete");
            bRecess = VariantFinishMaterial("brutal-recess");
            bSteel = VariantFinishMaterial("brutal-steel");
            bBulb = VariantFinishMaterial("concrete-service-bulb");
            wornSteel = VariantFinishMaterial("working-steel");
            wornPipe = VariantFinishMaterial("working-pipe");
            patchConcrete = VariantFinishMaterial("working-concrete-repair");
            floorWear = VariantFinishMaterial("working-floor-dirt");
            leakWear = VariantFinishMaterial("working-leak");
            font = GetFont();

            var tiles = background.GetComponentsInChildren<Transform>()
                .Where(t => t.name == "SM_ConcreteFloor" && !VariantIsExterior(t, background))
                .Select(t => new VariantTile(t)).Where(t => t.Valid).ToArray();
            if (tiles.Length < 100) throw new InvalidOperationException("기존 B/C 타일 구조를 확인하지 못했습니다.");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("B/C structural finish");
            var finish = VerticalChild(background, VariantFinishGroup);
            finish.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            finish.localScale = new Vector3(1 / background.lossyScale.x, 1 / background.lossyScale.y, 1 / background.lossyScale.z);
            var shell = VerticalChild(finish, "01_ContinuousConcreteShell");
            VariantFinishShell(shell, tiles);

            foreach (var tile in tiles)
            {
                Undo.RecordObject(tile.Transform.gameObject, "Preserve original tiled structure");
                tile.Transform.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(tile.Transform.gameObject);
            }
            // New slabs are 1.6 m deep. Lower old foundations to their undersides so their
            // exposed side faces do not occupy the same planes as the replacement slabs.
            foreach (var t in background.GetComponentsInChildren<Transform>())
            {
                if (t.name != "플랫폼 하부 기초") continue;
                Undo.RecordObject(t, "Align foundation with slab underside");
                t.localPosition -= Vector3.up * .65f;
                t.localScale -= Vector3.up * 1.3f;
            }
            foreach (var light in background.GetComponentsInChildren<Light>())
            {
                if (VariantIsExterior(light.transform, background) || light.transform.IsChildOf(finish)) continue;
                Undo.RecordObject(light, "Replace floating lights with mounted fittings");
                light.enabled = false;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.36f, .37f, .38f);
            var camera = GameObject.Find("Main Camera")?.GetComponent<Camera>();
            if (camera != null)
            {
                Undo.RecordObject(camera, "Exterior sky visibility and useful depth precision");
                camera.clearFlags = CameraClearFlags.Skybox; camera.nearClipPlane = .15f;
            }

            VariantFinishStart(finish, background, id);
            VariantFinishChamber(finish, background.Find(isB ? "03_TrampolineSwing_B" : "03_CannonRope_C"), !isB, false, isB ? 0 : 2, isB ? 12 : 12.5f);
            VariantFinishChamber(finish, background.Find(isB ? "05_CannonRope_B" : "05_TrampolineSwing_C"), isB, true, isB ? 4 : 0, isB ? 15 : 14);
            VariantFinishConnections(finish, background, id);
            VariantFinishExit(finish, background, id);
            EnsureReviewLabels(background.gameObject);
            VerticalChild(finish, "99_Complete");
            AssetDatabase.SaveAssetIfDirty(font);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
        }

        static Material VariantFinishMaterial(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
            if (material == null) throw new InvalidOperationException("A 마감 재질이 필요합니다: " + name);
            return material;
        }

        static bool VariantIsExterior(Transform t, Transform background)
        {
            for (var p = t; p != null && p != background; p = p.parent)
                if (p.name.StartsWith("08_Reeds", StringComparison.Ordinal)) return true;
            return false;
        }

        sealed class VariantTile
        {
            public readonly Transform Transform;
            public readonly Bounds Bounds;
            public readonly bool Ceiling, Valid;
            public VariantTile(Transform t)
            {
                Transform = t;
                var renderers = t.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                if (renderers.Length == 0) return;
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                Bounds = bounds;
                Ceiling = t.parent.name == "Ceiling";
                Valid = bounds.size.y < .7f || Mathf.Min(bounds.size.x, bounds.size.z) < .8f;
            }
        }

        struct VariantPanel
        {
            public float X1, X2, Z1, Z2;
            public VariantPanel(float x1, float x2, float z1, float z2) { X1 = x1; X2 = x2; Z1 = z1; Z2 = z2; }
        }

        static float VariantSnap(float n) => Mathf.Round(n * 1000) / 1000;

        static void VariantFinishShell(Transform p, VariantTile[] tiles)
        {
            foreach (bool ceiling in new[] { false, true })
            {
                var horizontal = tiles.Where(t => t.Bounds.size.y < .7f && t.Ceiling == ceiling);
                foreach (var level in horizontal.GroupBy(t => VariantSnap(ceiling ? t.Bounds.min.y : t.Bounds.max.y)))
                {
                    var rects = level.Select(t => new VariantPanel(VariantSnap(t.Bounds.min.x), VariantSnap(t.Bounds.max.x),
                        VariantSnap(t.Bounds.min.z), VariantSnap(t.Bounds.max.z))).ToArray();
                    foreach (var rect in VariantMergePanels(rects))
                    {
                        float thickness = ceiling ? 3 : 1.6f;
                        BMass(p, ceiling ? "하중 전달 천장" : "SM_ConcreteFloor",
                            new Vector3((rect.X1 + rect.X2) / 2, level.Key + (ceiling ? 1 : -1) * thickness / 2, (rect.Z1 + rect.Z2) / 2),
                            new Vector3(rect.X2 - rect.X1, thickness, rect.Z2 - rect.Z1), bConcrete);
                    }
                }
            }
            var walls = tiles.Where(t => t.Bounds.size.y >= .7f)
                .GroupBy(t => new { AlongX = t.Bounds.size.x > t.Bounds.size.z,
                    Plane = VariantSnap(t.Bounds.size.x > t.Bounds.size.z ? t.Bounds.center.z : t.Bounds.center.x) });
            foreach (var plane in walls)
            {
                bool alongX = plane.Key.AlongX;
                var rects = plane.Select(t => new VariantPanel(
                    VariantSnap(alongX ? t.Bounds.min.x : t.Bounds.min.z), VariantSnap(alongX ? t.Bounds.max.x : t.Bounds.max.z),
                    VariantSnap(t.Bounds.min.y), VariantSnap(t.Bounds.max.y))).ToArray();
                foreach (var rect in VariantMergePanels(rects))
                {
                    var centre = alongX ? new Vector3((rect.X1 + rect.X2) / 2, (rect.Z1 + rect.Z2) / 2, plane.Key.Plane)
                        : new Vector3(plane.Key.Plane, (rect.Z1 + rect.Z2) / 2, (rect.X1 + rect.X2) / 2);
                    var size = alongX ? new Vector3(rect.X2 - rect.X1, rect.Z2 - rect.Z1, 2)
                        : new Vector3(2, rect.Z2 - rect.Z1, rect.X2 - rect.X1);
                    BMass(p, "연속 타설 벽체", centre, size, bConcrete);
                }
            }
        }

        // Merge the union, rather than a bounding box, to retain openings and L bends.
        static List<VariantPanel> VariantMergePanels(VariantPanel[] input)
        {
            var xs = input.SelectMany(r => new[] { r.X1, r.X2 }).Distinct().OrderBy(n => n).ToArray();
            var zs = input.SelectMany(r => new[] { r.Z1, r.Z2 }).Distinct().OrderBy(n => n).ToArray();
            var result = new List<VariantPanel>();
            for (int z = 0; z < zs.Length - 1; z++)
            {
                float midZ = (zs[z] + zs[z + 1]) / 2;
                int x = 0;
                while (x < xs.Length - 1)
                {
                    float midX = (xs[x] + xs[x + 1]) / 2;
                    if (!input.Any(r => midX > r.X1 && midX < r.X2 && midZ > r.Z1 && midZ < r.Z2)) { x++; continue; }
                    int start = x++;
                    while (x < xs.Length - 1 && input.Any(r => (xs[x] + xs[x + 1]) / 2 > r.X1 && (xs[x] + xs[x + 1]) / 2 < r.X2 && midZ > r.Z1 && midZ < r.Z2)) x++;
                    int previous = result.FindIndex(r => Mathf.Abs(r.X1 - xs[start]) < .001f && Mathf.Abs(r.X2 - xs[x]) < .001f && Mathf.Abs(r.Z2 - zs[z]) < .001f);
                    if (previous >= 0) { var r = result[previous]; r.Z2 = zs[z + 1]; result[previous] = r; }
                    else result.Add(new VariantPanel(xs[start], xs[x], zs[z], zs[z + 1]));
                }
            }
            return result;
        }

        static Transform VariantFinishChunk(Transform finish, Transform original)
        {
            var p = VerticalChild(finish, original.name);
            p.SetPositionAndRotation(original.position, original.rotation);
            return p;
        }

        static void VariantMountedLight(Transform p, string name, Vector3 mount, Vector3 source, Vector3 target, float power = 180, float range = 22, bool shadow = false)
        {
            var direction = source - mount;
            var support = RevealBlock(p, name + " 지지 브래킷", (mount + source) / 2, new Vector3(.085f, direction.magnitude, .085f), wornSteel, false, false);
            support.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            BMass(p, name + " 함체", source + Vector3.up * .1f, new Vector3(.9f, .22f, .38f), wornSteel, false);
            RevealBlock(p, name + " 광원", source - Vector3.up * .055f, new Vector3(.7f, .055f, .21f), bBulb, false, false);
            RevealLight(p, name, source - Vector3.up * .16f, target, power, range, 108);
            var light = p.GetChild(p.childCount - 1).GetComponent<Light>();
            light.innerSpotAngle = 34; light.color = new Color(.97f, .97f, .94f);
            light.shadows = shadow ? LightShadows.Soft : LightShadows.None;
        }

        static void VariantFinishStart(Transform finish, Transform background, string id)
        {
            bool b = id == "B";
            var start = VariantFinishChunk(finish, background.Find("01_Understage_" + id));
            foreach (float z in new[] { 4f, 17.5f })
            {
                BMass(start, "무대 기초 횡보", new Vector3(15, 8.5f, z), new Vector3(29.8f, 3, 2.4f));
                foreach (float x in new[] { 1.7f, 28.3f }) BMass(start, "무대 하중 교각", new Vector3(x, 3.5f, z), new Vector3(2, 7, 2.4f));
            }
            VariantMountedLight(start, "하부 작업등", new Vector3(12, 7, 4), new Vector3(12, 5.3f, 4), new Vector3(15, 0, 11), 230, 23, true);
            VariantMountedLight(start, "진입 방향 작업등", new Vector3(22, 7, 17.5f), new Vector3(22, 4.6f, 17.5f), b ? new Vector3(17.5f, 0, 24) : new Vector3(30, 0, 13.5f), 180, 20);
            FloorWear(start, new Vector3(6, .025f, 10), new Vector2(4, 9), 13);
            var approach = VariantFinishChunk(finish, background.Find("02_Approach_" + id));
            if (b)
            {
                foreach (float z in new[] { 9f, 28f })
                {
                    BMass(approach, "진입로 하중 보", new Vector3(7.5f, 9, z), new Vector3(15, 3, 3));
                    BMass(approach, "좌측 깊은 벽기둥", new Vector3(1.7f, 3.8f, z), new Vector3(2, 7.6f, 3));
                    VariantMountedLight(approach, "진입로 정비등", new Vector3(10, 7.5f, z), new Vector3(10, 5.2f, z), new Vector3(8, 0, z + 7), 230, 23);
                }
                ServicePipe(approach, new Vector3(1.35f, 2.2f, 1), new Vector3(1.35f, 2.2f, 41), .18f);
                FloorWear(approach, new Vector3(3, .025f, 23), new Vector2(3, 14), 0);
            }
            else
            {
                foreach (float x in new[] { 11f, 36f })
                {
                    BMass(approach, "가로 갤러리 하중 보", new Vector3(x, 9, 7.5f), new Vector3(3, 3, 15));
                    BMass(approach, "남측 깊은 벽기둥", new Vector3(x, 3.8f, 1.7f), new Vector3(3, 7.6f, 2));
                    VariantMountedLight(approach, "가로 진입로 정비등", new Vector3(x, 7.5f, 9), new Vector3(x, 5.2f, 9), new Vector3(x + 8, 0, 9), 230, 24);
                }
                ServicePipe(approach, new Vector3(1, 2.2f, 1.35f), new Vector3(52, 2.2f, 1.35f), .18f);
                FloorWear(approach, new Vector3(29, .025f, 3), new Vector2(14, 3), 0);
            }
        }

        static void VariantFinishChamber(Transform finish, Transform original, bool cannon, bool staggered, float landingY, float entryLane)
        {
            var p = VariantFinishChunk(finish, original);
            float width = cannon ? 25 : 37.5f;
            foreach (float x in new[] { 16f, 33f })
            {
                BMass(p, "공동 하중 수직 교각", new Vector3(x, -17, 1.7f), new Vector3(3.2f, 86, 3.4f));
                foreach (float y in new[] { -38f, -18f, 12f })
                {
                    BMass(p, "깊이 기준 콘크리트 인방", new Vector3(x, y, 2.4f), new Vector3(9, 2.2f, 3.2f), bRecess, false);
                    VariantMountedLight(p, "하부 점검층 등", new Vector3(x, y + 1.1f, 3), new Vector3(x, y + 2.2f, 3.5f), new Vector3(x, y - 3, 8), y < 0 ? 120 : 150, 18);
                }
            }
            if (!cannon)
            {
                foreach (var t in original.Cast<Transform>().Where(t => t.name == "SM_Crane_Beam"))
                { Undo.RecordObject(t.gameObject, "Preserve old unconnected crane beams"); t.gameObject.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject); }
                foreach (float x in new[] { 19f, 27f, 35f })
                    BMass(p, "그네 지지선과 연결된 횡보", new Vector3(x, 23.75f, width / 2), new Vector3(1.4f, 1.5f, width + 1), bSteel, false);
                BMass(p, "상부 크레인 주 지지보", new Vector3(26, 25.6f, width - 4), new Vector3(48, 2.2f, 2.2f), bConcrete, false);
                VariantMountedLight(p, "그네 횡단 설비등", new Vector3(19, 23, 7), new Vector3(19, 20.5f, 7), new Vector3(29, 7, 22), 560, 37, true);
            }
            else
            {
                foreach (float z in new[] { 2.8f, width - 2.8f })
                    BMass(p, "밀폐 설비 코어 교각", new Vector3(48.1f, 12, z), new Vector3(2.4f, 24, 2.4f));
                BMass(p, "밀폐 설비 코어 인방", new Vector3(48.1f, 24.2f, width / 2), new Vector3(2.4f, 3.6f, width));
                float islandZ = staggered ? 17.5f : width / 2;
                VariantMountedLight(p, "고립 발판 지주등", new Vector3(23, landingY, islandZ - 3), new Vector3(23, landingY + 3.5f, islandZ - 3), new Vector3(27, landingY, islandZ), 190, 17);
                BMass(p, "코어 사광 연장 지지 암", new Vector3(34.7f, 13.1f, 3), new Vector3(1, .08f, .08f), wornSteel, false);
                VariantMountedLight(p, "설비 코어 사광", new Vector3(35, 13.1f, 3), new Vector3(35, 14.2f, 3.5f), new Vector3(48, 13, width / 2), 420, 30, true);
            }
            VariantMountedLight(p, "진입 발판 지주등", new Vector3(3, 0, entryLane - 4), new Vector3(3, 3.8f, entryLane - 4), new Vector3(10, 0, entryLane), 200, 19);
            VariantMountedLight(p, "도착 발판 지주등", new Vector3(45, landingY, width * .55f + 4), new Vector3(45, landingY + 4, width * .55f + 4), new Vector3(41, landingY, width * .55f), 260, 22);
            FloorWear(p, new Vector3(5.5f, .025f, width - 5), new Vector2(5, 7), 9);
            FloorWear(p, new Vector3(45, landingY + .025f, width * .55f), new Vector2(4, 6), 7);
        }

        static void VariantFinishConnections(Transform finish, Transform background, string id)
        {
            bool b = id == "B";
            var bend = VariantFinishChunk(finish, background.Find("04_Bend_" + id));
            var regroup = VariantFinishChunk(finish, background.Find("06_Regroup_" + id));
            var stairs = b ? regroup : bend;
            int count = b ? 20 : 12;
            for (int i = 0; i < count; i++)
            {
                float top = (b ? 4 - i * .2f : 2 - i / 6f) - 1.62f;
                var centre = b ? new Vector3(12.5f, (top - 8) / 2, (i + .5f) * .6f)
                    : new Vector3(i + .5f, (top - 8) / 2, 10);
                BMass(stairs, "계단 하부 지지 기초", centre, b ? new Vector3(25, top + 8, .6f) : new Vector3(1, top + 8, 20), bRecess);
            }
            if (b)
            {
                VariantMountedLight(bend, "남하 굽이 벽등", new Vector3(1, 4.5f, 23), new Vector3(2, 4.5f, 23), new Vector3(10, 0, 14), 260, 25);
                VariantMountedLight(bend, "대포 공동 진입등", new Vector3(10, 4.2f, 1), new Vector3(10, 4.2f, 2), new Vector3(21, 0, 9), 210, 22);
                VariantMountedLight(regroup, "긴 하강 계단등", new Vector3(1, 6.5f, 4), new Vector3(2, 6.5f, 4), new Vector3(12, 1, 9), 300, 23);
                VariantMountedLight(regroup, "재합류 바닥등", new Vector3(1, 4.5f, 27), new Vector3(2, 4.5f, 27), new Vector3(15, 0, 30), 250, 25);
                BMass(regroup, "재합류 횡보", new Vector3(12.5f, 11, 23), new Vector3(25, 3, 3));
                MaintenanceBay(regroup, new Vector3(3, 0, 34), 8);
            }
            else
            {
                VariantMountedLight(bend, "방향 전환 계단등", new Vector3(5, 5, 19), new Vector3(5, 5, 18), new Vector3(10, .5f, 10), 250, 24);
                VariantMountedLight(bend, "남쪽 횡단 진입등", new Vector3(31, 4.5f, 19), new Vector3(31, 4.5f, 18), new Vector3(34, 0, -1), 280, 26);
                VariantMountedLight(regroup, "반대편 착지등", new Vector3(24, 4.5f, 6), new Vector3(23, 4.5f, 6), new Vector3(9, 0, 8), 240, 23);
                VariantMountedLight(regroup, "북상 방향 작업등", new Vector3(24, 4.5f, 31), new Vector3(23, 4.5f, 31), new Vector3(14, 0, 43), 270, 25);
                BMass(regroup, "재합류 횡보", new Vector3(12.5f, 11, 24), new Vector3(25, 3, 3));
                MaintenanceBay(regroup, new Vector3(3, 0, 27), -6);
            }
        }

        static void VariantFinishExit(Transform finish, Transform background, string id)
        {
            bool b = id == "B";
            var run = VariantFinishChunk(finish, background.Find("07A_FinalRun_" + id));
            if (b)
            {
                foreach (float x in new[] { 155f, 179f })
                {
                    BMass(run, "반출동 횡보", new Vector3(x, 10, 63), new Vector3(3, 3, 20));
                    VariantMountedLight(run, "반출동 작업등", new Vector3(x, 8.5f, 68), new Vector3(x, 5.1f, 68), new Vector3(x + 8, 0, 62), 260, 26);
                }
                VariantMountedLight(run, "출구 굽이 벽등", new Vector3(194, 4.5f, 40), new Vector3(193, 4.5f, 40), new Vector3(184, 0, 30), 260, 25);
            }
            else
            {
                foreach (float z in new[] { 56f, 78f })
                {
                    BMass(run, "북상 반출동 횡보", new Vector3(157.5f, 10, z), new Vector3(20, 3, 3));
                    VariantMountedLight(run, "북상 반출동 작업등", new Vector3(152, 8.5f, z), new Vector3(152, 5.1f, z), new Vector3(158, 0, z + 8), 250, 25);
                }
                VariantMountedLight(run, "외벽 방향 벽등", new Vector3(190, 5, 89), new Vector3(190, 5, 88), new Vector3(193, 0, 73), 280, 25);
            }
            var hall = VariantFinishChunk(finish, background.Find("07B_ExitWorkroom_" + id));
            BMass(hall, "출구 설비 하중 보", new Vector3(17.5f, 12, 9), new Vector3(35, 4, 3));
            VariantMountedLight(hall, "외벽 작업등", new Vector3(34, 5.5f, 11), new Vector3(33, 5.5f, 11), new Vector3(27, 0, 18), 260, 24);
            VariantMountedLight(hall, "후방 재정비등", new Vector3(1, 4.2f, 23), new Vector3(2, 4.2f, 23), new Vector3(14, 0, 20), 220, 23);
            ServicePipe(hall, new Vector3(1.35f, 2.4f, 2), new Vector3(1.35f, 2.4f, 26), .22f);
            MaintenanceBay(hall, new Vector3(4, 0, 25), 11);
            FloorWear(hall, new Vector3(27, .025f, 18.75f), new Vector2(11, 6), 3);
            ContextSign(hall, "외부 반출구", "OUT  >", new Vector3(30, 3.2f, 1.03f), 180, 2.5f);
        }

        // A second, additive pass also works on B/C scenes already finished and saved.
        public static string AddVariantServiceFinish()
        {
            var scene = SceneManager.GetActiveScene();
            bool b = scene.path == VariantBPath;
            if ((!b && scene.path != VariantCPath) || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("B 또는 C 씬의 편집 모드가 필요합니다.");
            var background = scene.GetRootGameObjects().First(g => g.name == RootName).transform;
            var structure = background.Find(VariantFinishGroup);
            if (structure == null || structure.Find("99_Complete") == null)
                throw new InvalidOperationException("먼저 B/C 구조 마감을 완료하세요.");
            if (background.Find("22_VariantServiceFinish") != null) return "B/C service finish already present; manual edits preserved.";
            bConcrete = VariantFinishMaterial("brutal-concrete"); bRecess = VariantFinishMaterial("brutal-recess");
            bSteel = VariantFinishMaterial("brutal-steel"); bBulb = VariantFinishMaterial("concrete-service-bulb");
            wornSteel = VariantFinishMaterial("working-steel"); wornPipe = VariantFinishMaterial("working-pipe");
            patchConcrete = VariantFinishMaterial("working-concrete-repair");
            floorWear = VariantFinishMaterial("working-floor-dirt"); leakWear = VariantFinishMaterial("working-leak");
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("B/C occupied industrial services and lower depth light");
            var finish = VerticalChild(background, "22_VariantServiceFinish");
            finish.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            finish.localScale = new Vector3(1 / background.lossyScale.x, 1 / background.lossyScale.y, 1 / background.lossyScale.z);
            string id = b ? "B" : "C";
            var run = VariantFinishChunk(finish, background.Find("07A_FinalRun_" + id));
            if (b)
            {
                VariantServicePipe(run, new Vector3(148, 2.4f, 71.6f), new Vector3(193.5f, 2.4f, 71.6f), .23f);
                VariantServicePipe(run, new Vector3(193.5f, 2.4f, 71.6f), new Vector3(193.5f, 2.4f, 33), .23f);
                VariantServicePipe(run, new Vector3(148, 1.35f, 71.6f), new Vector3(188, 1.35f, 71.6f), .14f);
                CableTray(run, new Vector3(148, 4.6f, 71.1f), new Vector3(191, 4.6f, 71.1f));
                foreach (float x in new[] { 151f, 167f, 186f })
                {
                    RevealBlock(run, "배관 벽체 고정대", new Vector3(x, 2, 71.8f), new Vector3(.09f, 1.8f, .4f), wornSteel, false, false);
                    WallWear(run, new Vector3(x + 2, 3.1f, 71.98f), new Vector2(4, 6), 0);
                }
                MaintenanceBay(run, new Vector3(162, 0, 69.2f), 90);
                MaintenanceBay(run, new Vector3(189.7f, 0, 42), 0);
                RevealAsset(run, "반출동 벽면 배전함", Industrial + "SM_CS1_WorkCabinet_01a_c1_Nanite.prefab",
                    new Vector3(176, 0, 70.7f), new Vector3(1.4f, 2.1f, .7f), wornSteel);
                FloorWear(run, new Vector3(170, .03f, 69.6f), new Vector2(18, 3), 0);
                FloorWear(run, new Vector3(190.5f, .03f, 45), new Vector2(3, 13), 0);
                VariantMountedLight(run, "배전함 국소 작업등", new Vector3(176, 3.7f, 72), new Vector3(176, 3.7f, 71), new Vector3(173, 0, 68), 145, 15);
                ContextSign(run, "반출 설비 식별", "DISPATCH / B-07", new Vector3(172, 2.4f, 71.96f), 0, 3);
            }
            else
            {
                VariantServicePipe(run, new Vector3(149, 2.4f, 48), new Vector3(149, 2.4f, 87.5f), .23f);
                VariantServicePipe(run, new Vector3(149, 2.4f, 88.6f), new Vector3(207, 2.4f, 88.6f), .23f);
                VariantServicePipe(run, new Vector3(149, 1.35f, 48), new Vector3(149, 1.35f, 81), .14f);
                CableTray(run, new Vector3(149.5f, 4.6f, 48), new Vector3(149.5f, 4.6f, 86));
                CableTray(run, new Vector3(151, 4.6f, 88.1f), new Vector3(205, 4.6f, 88.1f));
                foreach (float z in new[] { 52f, 66f, 81f })
                {
                    RevealBlock(run, "배관 벽체 고정대", new Vector3(148.8f, 2, z), new Vector3(.4f, 1.8f, .09f), wornSteel, false, false);
                    WallWear(run, new Vector3(148.52f, 3.1f, z + 2), new Vector2(4, 6), 90);
                }
                MaintenanceBay(run, new Vector3(151.3f, 0, 61), 0);
                MaintenanceBay(run, new Vector3(180, 0, 86), 90);
                RevealAsset(run, "북상 반출동 배전함", Industrial + "SM_CS1_WorkCabinet_01a_c1_Nanite.prefab",
                    new Vector3(196, 0, 87.3f), new Vector3(1.4f, 2.1f, .7f), wornSteel);
                FloorWear(run, new Vector3(151.5f, .03f, 69), new Vector2(3, 16), 0);
                FloorWear(run, new Vector3(190, .03f, 86.5f), new Vector2(18, 3), 0);
                WallWear(run, new Vector3(187, 3.1f, 88.98f), new Vector2(5, 6), 0);
                VariantMountedLight(run, "배전함 국소 작업등", new Vector3(196, 3.7f, 89), new Vector3(196, 3.7f, 88), new Vector3(191, 0, 85), 145, 16);
                ContextSign(run, "북상 설비 식별", "DISPATCH / C-07", new Vector3(184, 2.4f, 88.96f), 0, 3);
            }

            var originalHall = background.Find("07B_ExitWorkroom_" + id);
            var hall = VariantFinishChunk(finish, originalHall);
            var lockBlock = originalHall.Find("출구 잠금 장치");
            if (lockBlock != null)
            {
                Undo.RecordObject(lockBlock, "Mount exit lock on its side jamb");
                lockBlock.localPosition = new Vector3(33.3f, 1.7f, 12);
                lockBlock.localScale = new Vector3(.5f, 1.2f, .35f);
                var renderer = lockBlock.GetComponent<Renderer>();
                Undo.RecordObject(renderer, "Use worn equipment finish"); renderer.sharedMaterial = wornSteel;
                BMass(hall, "출구 잠금함 벽체 받침", new Vector3(33.77f, 1.7f, 12), new Vector3(.46f, 1.2f, .3f), wornSteel, false);
                VariantServicePipe(hall, new Vector3(33.3f, 2.3f, 12), new Vector3(33.3f, 3.5f, 12), .065f);
                VariantServicePipe(hall, new Vector3(33.3f, 3.5f, 12), new Vector3(34, 3.5f, 12), .065f);
            }
            var workBlock = originalHall.Find("출구 장치 작업 공간");
            if (workBlock != null)
            {
                var anchor = VerticalChild(hall, "Anchor_ExitWorkArea");
                anchor.localPosition = workBlock.localPosition;
                Undo.RecordObject(workBlock.gameObject, "Preserve old work-area placeholder"); workBlock.gameObject.SetActive(false);
            }
            foreach (var t in background.GetComponentsInChildren<Transform>())
            {
                if (t.name != "대포 설치 자리" && t.name != "트램폴린 설치 자리" && t.name != "진행 레버 위치") continue;
                var renderer = t.GetComponent<Renderer>(); if (renderer == null) continue;
                Undo.RecordObject(renderer, "Cohesive installation base finish"); renderer.sharedMaterial = bSteel;
            }

            foreach (string name in b ? new[] { "03_TrampolineSwing_B", "05_CannonRope_B" } : new[] { "03_CannonRope_C", "05_TrampolineSwing_C" })
            {
                var original = background.Find(name);
                var p = VariantFinishChunk(finish, original);
                bool cannon = name.Contains("Cannon"); float width = cannon ? 25 : 37.5f;
                foreach (var t in original.Cast<Transform>())
                {
                    if (!t.name.Contains("하부 외피") && t.name != "공동 암부 바닥" && t.name != "플랫폼 하부 기초") continue;
                    var renderer = t.GetComponent<Renderer>(); if (renderer == null) continue;
                    Undo.RecordObject(renderer, "Readable lower concrete envelope"); renderer.sharedMaterial = bRecess;
                }
                VariantMountedLight(p, "하부 설비를 읽는 측광", new Vector3(16, -16, 3.4f), new Vector3(16, -16, 4.2f), new Vector3(31, -35, width - 1.2f), 900, 55);
                VariantMountedLight(p, "최하층 잔광", new Vector3(33, -37, 3.4f), new Vector3(33, -37, 4.2f), new Vector3(20, -55, width - 1.2f), 540, 48);
                VariantServicePipe(p, new Vector3(16, -17, 3.7f), new Vector3(33, -17, 3.7f), .22f);
                VariantServicePipe(p, new Vector3(16, -49, 3.7f), new Vector3(16, 17, 3.7f), .16f);
                RevealBlock(p, "하부 점검 분전함", new Vector3(16, -15.3f, 3.5f), new Vector3(1.1f, 1.7f, .15f), wornSteel, false, false);
                ContextSign(p, "지하 점검층 표식", "S -18", new Vector3(17, -16.2f, 3.45f), 180, 1.5f);
                if (cannon)
                {
                    var finished = structure.Find(name);
                    foreach (var t in finished.Cast<Transform>().Where(t => t.name.StartsWith("설비 코어 사광", StringComparison.Ordinal)))
                    {
                        if (Mathf.Abs(t.localPosition.x - 33) > .1f) continue;
                        Undo.RecordObject(t, "Separate coincident service light brackets"); t.localPosition += Vector3.right * 2;
                    }
                    if (finished.Find("코어 사광 연장 지지 암") == null)
                        BMass(finished, "코어 사광 연장 지지 암", new Vector3(34.7f, 13.1f, 3), new Vector3(1, .08f, .08f), wornSteel, false);
                }
            }
            AssetDatabase.SaveAssetIfDirty(font);
            EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(undo);
            return "B/C service routes, maintenance clusters, lower depth lighting and separated core lamps added.";
        }

        static void VariantServicePipe(Transform p, Vector3 a, Vector3 b, float diameter)
        {
            void Part(string name, Vector3 start, Vector3 end, float d, Material material)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Undo.RegisterCreatedObjectUndo(go, "Variant service conduit");
                go.name = name; go.transform.SetParent(p, false); go.transform.localPosition = (start + end) / 2;
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (end - start).normalized);
                go.transform.localScale = new Vector3(d, Vector3.Distance(start, end) / 2, d);
                go.GetComponent<Renderer>().sharedMaterial = material;
                Undo.DestroyObjectImmediate(go.GetComponent<Collider>());
            }
            Part("사용 중인 설비 주관", a, b, diameter, wornPipe);
            var direction = (b - a).normalized;
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 8));
            for (int i = 0; i <= count; i++)
            {
                var point = Vector3.Lerp(a, b, (float)i / count);
                Part("설비 주관 이음", point - direction * .06f, point + direction * .06f, diameter * 1.5f, wornSteel);
            }
        }

        // Moving a fixture after surface clipping requires its complete original render mesh.
        // This repair is separate so already-applied B scenes do not need to rerun the finish.
        public static string RepairVariantCoreLampRenderers()
        {
            var scene = SceneManager.GetActiveScene();
            bool b = scene.path == VariantBPath;
            if ((!b && scene.path != VariantCPath) || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("B 또는 C 씬의 편집 모드가 필요합니다.");
            var chamber = GameObject.Find(RootName)?.transform.Find(VariantFinishGroup + "/" + (b ? "05_CannonRope_B" : "03_CannonRope_C"));
            if (chamber == null) throw new InvalidOperationException("마감한 대포 공동이 없습니다.");
            int restored = 0;
            foreach (var t in chamber.Cast<Transform>().Where(t => t.name.StartsWith("설비 코어 사광", StringComparison.Ordinal)))
            {
                var light = t.GetComponent<Light>();
                if (light != null)
                {
                    Undo.RecordObject(t, "Aim relocated core light at equipment");
                    t.localRotation = Quaternion.LookRotation(new Vector3(48, 13, 12.5f) - t.localPosition);
                }
                var filter = t.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.name.StartsWith("polished-", StringComparison.Ordinal)) continue;
                Mesh original = null;
                if (t.name.EndsWith("함체", StringComparison.Ordinal))
                {
                    string key = string.Join("x", new[] { t.localScale.x, t.localScale.y, t.localScale.z }
                        .Select(n => n.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)));
                    original = AssetDatabase.LoadAssetAtPath<Mesh>(BrutalMeshes + "/block-" + key + ".asset");
                }
                if (original == null)
                {
                    var temporary = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    original = temporary.GetComponent<MeshFilter>().sharedMesh;
                    UnityEngine.Object.DestroyImmediate(temporary);
                }
                Undo.RecordObject(filter, "Restore moved fixture render faces"); filter.sharedMesh = original; restored++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return "Relocated core lamp render meshes restored=" + restored;
        }
    }
}
