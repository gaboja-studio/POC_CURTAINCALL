using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public const string TraversalSpacePrefix = "23_TraversalSpaces_";

        // Save a copy through Unity so existing B/C scene GUIDs and A's source file survive.
        // This is deliberately separate from the old B/C builders, which used a different shell.
        public static void BuildVariantFromCurrentA(string id)
        {
            if (id != "B" && id != "C") throw new ArgumentException("B 또는 C만 제작합니다.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("편집 모드에서 현재 씬을 저장한 후 제작하세요.");
            string destination = id == "B" ? VariantBPath : VariantCPath;
            if (SceneManager.GetActiveScene().path == destination && GameObject.Find(RootName + "/" + TraversalSpacePrefix + id) != null)
                throw new InvalidOperationException("A 기반 공간이 이미 있습니다. 재생성 대신 저장 씬을 편집하세요.");
            var source = EditorSceneManager.OpenScene(ScenePath);
            if (GameObject.Find(RootName + "/" + RunnerGroup) == null || GameObject.Find(RootName + "/21_PlacementPolish") == null)
                throw new InvalidOperationException("최신 A와260m 탈출 복도·폴리싱이 필요합니다.");
            if (!EditorSceneManager.SaveScene(source, destination, false))
                throw new InvalidOperationException("A 복사본을 B/C 씬으로 저장하지 못했습니다.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != destination) throw new InvalidOperationException("대상 씬 전환 실패");
            var background = GameObject.Find(RootName).transform;
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("A-based " + id + " traversal spaces");
            PrepareVariantMiddle(background);
            CreateVariantSpaceGeometry(background, id);
            EnsureReviewLabels(background.gameObject);
            if (font != null) AssetDatabase.SaveAssetIfDirty(font);
            Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
        }

        static void CreateVariantSpaceGeometry(Transform background, string id)
        {
            bConcrete = RunnerMaterial("brutal-concrete"); bRecess = RunnerMaterial("brutal-recess");
            bSteel = RunnerMaterial("brutal-steel"); bBulb = RunnerMaterial("concrete-service-bulb");
            wornSteel = RunnerMaterial("working-steel"); wornPipe = RunnerMaterial("working-pipe");
            patchConcrete = RunnerMaterial("working-concrete-repair");
            floorWear = RunnerMaterial("working-floor-dirt"); leakWear = RunnerMaterial("working-leak");
            maintenancePaint = RunnerMaterial("working-faded-ochre");
            var spaces = VerticalChild(background, TraversalSpacePrefix + id);
            spaces.localScale = new Vector3(1 / background.lossyScale.x, 1 / background.lossyScale.y, 1 / background.lossyScale.z);
            var entry = VerticalChild(spaces, "00_TransferEntryJoints");
            // Butt against the unchanged 04 floor and walls, without restoring their overlapping faces.
            BFloor(entry, "공동 진입 접합 바닥", 79, 90, 22, 34, 0, 3);
            BFloor(entry, "진입 남측 정비 바닥", 79.5f, 90, 17.5f, 22, 0, 3);
            if (id == "B") BuildVariantBSpaces(spaces); else BuildVariantCSpaces(spaces);
            VerticalChild(spaces, "99_Complete");
        }

        static void PrepareVariantMiddle(Transform background)
        {
            var route = background.Find(VerticalGroup + "/02_Route_0_to_minus6_to_plus2");
            // Restore full render faces before hiding the old island and its installation guides.
            // Only local 03/05 surfaces change; A's common route and final run keep their exact meshes.
            foreach (var filter in route.GetComponentsInChildren<MeshFilter>()) RestoreSpaceSourceMesh(filter, false);
            var bottom = background.Find(VerticalGroup + "/01_Shell_50x50_134m/90m 아래 집수 바닥");
            RestoreSpaceSourceMesh(bottom.GetComponent<MeshFilter>(), false);
            foreach (var child in route.Cast<Transform>().ToArray())
            {
                var at = child.position;
                bool island = child.name == "6m 아래 고립 정비 발판" || child.name == "고립 정비대 지지각" ||
                    (child.name == "발판 하중 보" && at.x > 40 && at.x < 55) ||
                    (child.name == "1.1m 점검 난간" && at.x > 40 && at.x < 55);
                if (island || child.name == "대포 설치 자리" || child.name == "외줄 연결 시각 표시")
                    PreserveSpaceGuide(child);
            }
            var transfer = background.Find(BrutalGroup + "/05_TransferChasm");
            foreach (var child in transfer.Cast<Transform>().ToArray())
                if (child.name == "트램폴린 진입 대" || child.name == "트램폴린 설치 자리" || child.name == "그네 설치 지지선" || child.name == "그네 설치 가이드")
                    PreserveSpaceGuide(child);
        }

        static void PreserveSpaceGuide(Transform t)
        {
            Undo.RecordObject(t.gameObject, "Preserve old A middle installation layout"); t.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
        }

        static void RestoreSpaceSourceMesh(MeshFilter filter, bool tiled)
        {
            if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.name.StartsWith("polished-")) return;
            Mesh original = null;
            if (tiled)
            {
                var size = filter.transform.localScale;
                string key = string.Join("x", new[] { size.x, size.y, size.z }.Select(n => n.ToString("0.###", CultureInfo.InvariantCulture)));
                original = AssetDatabase.LoadAssetAtPath<Mesh>(BrutalMeshes + "/block-" + key + ".asset");
            }
            if (original == null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                original = cube.GetComponent<MeshFilter>().sharedMesh;
                UnityEngine.Object.DestroyImmediate(cube);
            }
            Undo.RecordObject(filter, "Restore faces before changing neighbouring geometry"); filter.sharedMesh = original;
        }

        static void BuildVariantBSpaces(Transform group)
        {
            var first = VerticalChild(group, "03_StaggeredServiceTerraces");
            SpaceTerrace(first, "01 상부 점검대", 35, 41, 71, 79, 2, -90);
            SpaceTerrace(first, "02 높은 설비 발판", 44, 50, 74, 82, 5, -90);
            SpaceTerrace(first, "03 하강 정비대", 54, 59, 78, 86, .5f, -90);
            SpaceEdgeRail(first, new Vector3(35, 2, 71), new Vector3(41, 2, 71));
            SpaceEdgeRail(first, new Vector3(44, 5, 74), new Vector3(50, 5, 74));
            SpaceEdgeRail(first, new Vector3(54, .5f, 86), new Vector3(59, .5f, 86));
            SpaceLamp(first, new Vector3(35.4f, 2, 71.5f), new Vector3(38, 2, 75), 230);
            SpaceLamp(first, new Vector3(49.5f, 5, 81.3f), new Vector3(46, 5, 78), 260);
            SpaceLamp(first, new Vector3(58.5f, .5f, 85.5f), new Vector3(55, .5f, 82), 240);
            SpaceReserve(first, "Landing_01", new Vector3(38, 2, 75));
            SpaceReserve(first, "Landing_02", new Vector3(47, 5, 78));
            SpaceReserve(first, "Landing_03", new Vector3(56.5f, .5f, 82));
            FloorWear(first, new Vector3(38, 2.025f, 73), new Vector2(3, 2.5f), 9);
            FloorWear(first, new Vector3(47, 5.025f, 80), new Vector2(3, 2.5f), -8);

            var second = VerticalChild(group, "05_SteppedTransferWorks");
            SpaceTerrace(second, "01 높아지는 중계대", 94, 99, 20, 29, 2, -100);
            SpaceTerrace(second, "02 낮아지는 중계대", 103, 108, 23, 32, -2.5f, -100);
            SpaceTerrace(second, "03 도착 전 넓은 회복대", 111, 115, 28, 34.5f, -5.5f, -100);
            SpaceEdgeRail(second, new Vector3(94, 2, 20), new Vector3(99, 2, 20));
            SpaceEdgeRail(second, new Vector3(103, -2.5f, 32), new Vector3(108, -2.5f, 32));
            SpaceLamp(second, new Vector3(94.5f, 2, 20.5f), new Vector3(97, 2, 25), 260);
            SpaceLamp(second, new Vector3(107.5f, -2.5f, 31.5f), new Vector3(105, -2.5f, 27), 230);
            SpaceLamp(second, new Vector3(114.3f, -5.5f, 34), new Vector3(112, -5.5f, 31), 200);
            SpaceReserve(second, "Landing_04", new Vector3(96.5f, 2, 25));
            SpaceReserve(second, "Landing_05", new Vector3(105.5f, -2.5f, 27.5f));
            SpaceReserve(second, "Landing_06", new Vector3(113, -5.5f, 32));
            // These are spatial reservations, not jump distances or a fixed stunt sequence.
            FloorWear(second, new Vector3(96.5f, 2.025f, 22.5f), new Vector2(3, 3), 6);
            FloorWear(second, new Vector3(105.5f, -2.475f, 30), new Vector2(3, 2), -11);
        }

        static void SpaceTerrace(Transform p, string name, float x1, float x2, float z1, float z2, float top, float bottom)
        {
            BFloor(p, name, x1, x2, z1, z2, top, 1.6f);
            float supportTop = top - 2.6f;
            BMass(p, name + " 하부 하중 코어", new Vector3((x1 + x2) / 2, (bottom + supportTop) / 2, (z1 + z2) / 2),
                new Vector3(2, supportTop - bottom, 2.4f), bRecess);
            BMass(p, name + " 슬래브 받침", new Vector3((x1 + x2) / 2, top - 2.1f, (z1 + z2) / 2),
                new Vector3(x2 - x1, 1, 1.8f), bConcrete);
        }

        static void SpaceEdgeRail(Transform p, Vector3 a, Vector3 b)
        {
            VerticalCable(p, "정비대 외곽 손잡이", a + Vector3.up * 1.1f, b + Vector3.up * 1.1f, .055f, bSteel);
            int count = Mathf.CeilToInt(Vector3.Distance(a, b) / 1.8f);
            for (int i = 0; i <= count; i++)
            {
                var at = Vector3.Lerp(a, b, (float)i / count);
                VerticalCable(p, "정비대 손잡이 기둥", at, at + Vector3.up * 1.1f, .045f, bSteel);
            }
        }

        static void SpaceLamp(Transform p, Vector3 feet, Vector3 target, float power)
        {
            var source = feet + Vector3.up * 3.5f;
            VerticalCable(p, "정비대 작업등 지주", feet, source + Vector3.up * .25f, .08f, bSteel);
            BLight(p, source, target, power, 18);
        }

        static void SpaceReserve(Transform p, string name, Vector3 position)
        {
            var anchor = VerticalChild(p, "Reserve_" + name); anchor.localPosition = position;
        }
    }
}
