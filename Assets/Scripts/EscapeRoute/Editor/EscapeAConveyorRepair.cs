using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.Tightrope;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        // The old east-going exit bridge crossed the live east rail and its cannon-room walls.
        // Keep the complete sequence south of the rail. The latest pass removes its old recovery layer.
        public static string RepairAConveyorFlow()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Saved A edit scene required");
            var bg = GameObject.Find(RootName).transform;
            if (bg.Find("43_AConveyorRecovery") != null) throw new InvalidOperationException("Conveyor repair already applied");
            CompletionMaterials();
            var root = VerticalChild(bg, "43_AConveyorRecovery");
            root.localScale = new Vector3(1 / bg.lossyScale.x, 1 / bg.lossyScale.y, 1 / bg.lossyScale.z);
            var exit = bg.Find("41_AReadableRoute/02_OneWayDisembark");
            Vector3 shift = new(0, 0, -38);
            Undo.RecordObject(exit, "Separate exit from moving conveyor");
            exit.position += shift;
            foreach (var cue in exit.GetComponentsInChildren<HumanCannonHazardCue>(true))
            {
                Undo.RecordObject(cue, "Move authoritative saw trajectory with its mechanism");
                cue.origin += shift; cue.destination += shift;
                EditorUtility.SetDirty(cue);
            }
            foreach (var loop in bg.GetComponentsInChildren<HumanCannonPoleLoop>())
            {
                var data = new SerializedObject(loop);
                data.FindProperty("reverseDirection").boolValue = true;
                data.ApplyModifiedProperties();
                Undo.RecordObject(loop.Carriage, "Present reverse conveyor direction");
                loop.RefreshPose();
                loop.Carriage.GetComponentInChildren<HumanCannonPole>().PresentSwing(0);
            }
            // A broad receiver is met on the south-west bend before the exit cannon is visible.
            AFloor(root, "노란 하차장 수신 발판", 32.5f, 44, -70, -62, -19.2f);
            for (int i = 0; i < 4; i++)
                LandingStep(root, "하차장 낮은 연결단 " + i, 32.5f, 44, -68.6f - (i + 1) * .35f, -68.6f - i * .35f, -19.2f + (i + 1) * .175f);
            // Remove only the old north railing across the new receiving entrance.
            foreach (Transform t in exit)
                if (t.name.StartsWith("정비대") && Mathf.Abs(t.position.z + 70) < .06f)
                { Undo.RecordObject(t.gameObject, "Open landing entrance"); t.gameObject.SetActive(false); }
            ConveyorArrow(root, new Vector3(39, -19.18f, -65), Vector3.back);
            ConveyorArrow(root, new Vector3(39, -18.48f, -73.5f), Vector3.right);
            BLight(root, new Vector3(40, -11, -67), new Vector3(39, -19, -67), 650, 17);

            // These are visible physical recovery decks, below the puzzle. They cannot reach the
            // exit or shoulder control; their sole stair returns to the conveyor boarding point.
            var recovery = VerticalChild(root, "추락 후 재탑승 정비로");
            AFloor(recovery, "서측 회수 정비대", 29, 42, -58, -12, -25);
            AFloor(recovery, "동측 회수 정비대", 60, 90, -58, -12, -25);
            AFloor(recovery, "북측 회수 정비대", 29, 90, -12, 3, -25);
            AFloor(recovery, "하차 대포 아래 회수 정비대", 29, 90, -79, -58, -25);
            for (int i = 0; i < 29; i++)
                LandingStep(recovery, "재탑승 연결계단 " + i, 74, 78, -30 + i * .35f, -30 + (i + 1) * .35f, -25 + (i + 1) * .2f);
            AFloor(root, "그네 재탑승 대기대", 63, 78, -19.85f, -16, -19.2f);
            ConveyorArrow(root, new Vector3(67, -19.18f, -17.5f), Vector3.forward);
            foreach (float z in new[] { -71f, -55f, -39f })
            {
                ConveyorArrow(recovery, new Vector3(82, -24.98f, z), Vector3.forward);
                BLight(recovery, new Vector3(86, -21, z), new Vector3(81, -25, z), 280, 19);
            }
            foreach (float x in new[] { 36f, 49f, 62f, 75f })
                ConveyorArrow(recovery, new Vector3(x, -24.98f, -77.5f), Vector3.right);
            ConveyorArrow(recovery, new Vector3(77, -24.98f, -32), Vector3.forward);
            BLight(root, new Vector3(76, -13, -18), new Vector3(67, -19.2f, -18), 450, 18);
            ConveyorGuard(recovery, "회수 정비로 서측 난간", new Vector3(29, -25, -38), new Vector3(.16f, 1.15f, 82));
            ConveyorGuard(recovery, "회수 정비로 동측 난간", new Vector3(90, -25, -38), new Vector3(.16f, 1.15f, 82));
            ConveyorGuard(recovery, "회수 정비로 남측 난간", new Vector3(59.5f, -25, -79), new Vector3(61, 1.15f, .16f));
            ConveyorGuard(recovery, "회수 정비로 북측 난간", new Vector3(59.5f, -25, 3), new Vector3(61, 1.15f, .16f));
            ConveyorGuard(recovery, "중앙 심부 서측 난간", new Vector3(42, -25, -35), new Vector3(.16f, 1.15f, 46));
            ConveyorGuard(recovery, "중앙 심부 동측 난간", new Vector3(60, -25, -35), new Vector3(.16f, 1.15f, 46));
            ConveyorGuard(recovery, "중앙 심부 북측 난간", new Vector3(51, -25, -12), new Vector3(18, 1.15f, .16f));
            ConveyorGuard(recovery, "중앙 심부 남측 난간", new Vector3(51, -25, -58), new Vector3(18, 1.15f, .16f));
            foreach (float x in new[] { 32f, 85f })
                foreach (float z in new[] { -72f, -36f, -2f })
                    BMass(recovery, "회수층 구조 기둥", new Vector3(x, -56, z), new Vector3(.7f, 61.2f, .7f), bRecess);

            var cannon = exit.GetComponentInChildren<HumanCannonStation>();
            FeedbackAim(cannon, new Vector3(85, 0, 16.5f), 3.8f);
            var locks = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<EscapeCooperativeLocks>());
            locks.FindProperty("checkpoints").GetArrayElementAtIndex(0).FindPropertyRelative("instruction").stringValue =
                "노란 하차장 → 전원 버튼에 F → 톱 통과 → 대포 탑승. 떨어지면 아래 화살표를 따라 계단에서 그네 재탑승";
            locks.ApplyModifiedProperties();
            FinishAConveyorRecovery();
            RemoveAConveyorRecovery();
            UnityEngine.Object.FindFirstObjectByType<TightropeCourse>().Rebuild();
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root.gameObject;
            return "Reverse loop; exit sequence shifted 38m south outside rail; recovery decks removed for a lethal fall void";
        }

        static void LandingStep(Transform p, string name, float x1, float x2, float z1, float z2, float y)
        {
            var step = BMass(p, name, new Vector3((x1 + x2) * .5f, y - .25f, (z1 + z2) * .5f), new Vector3(x2 - x1, .5f, z2 - z1), wornSteel);
            Ensure<HumanCannonLandingZone>(step);
        }

        public static string FinishAConveyorRecovery()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("A edit scene required");
            CompletionMaterials();
            var bg = GameObject.Find(RootName).transform;
            var root = bg.Find("43_AConveyorRecovery");
            if (root.Find("컨베이어 낙사 영역") != null) return "Recovery route removed by latest design; retain the fall void";
            if (root.Find("명확한 승하차 마감") != null) return "Already finished";
            var p = VerticalChild(root, "명확한 승하차 마감");
            var exit = bg.Find("41_AReadableRoute/02_OneWayDisembark");
            foreach (Transform t in exit)
                if (t.name.Contains("가림벽") || t.name.StartsWith("대포 발사 개구"))
                { Undo.RecordObject(t.gameObject, "Retire obstructing oversized cannon screens"); t.gameObject.SetActive(false); }
            // The hall's enclosing walls stay intact. Only the old internal screens are retired.
            OpenConveyorRail(p, "발사대 북측 난간", new Vector3(81, -18.5f, -70), 10, true);
            OpenConveyorRail(p, "발사대 남측 난간", new Vector3(81, -18.5f, -78), 10, true);
            OpenConveyorRail(p, "발사대 끝 난간", new Vector3(86, -18.5f, -74), 8, false);
            OpenConveyorRail(p, "정비교 북측 난간", new Vector3(65, -18.5f, -72), 22, true);
            OpenConveyorRail(p, "정비교 남측 난간", new Vector3(65, -18.5f, -76), 22, true);
            // Also catch a missed shot or fall from the initial cannon's launch apron.
            AFloor(p, "진입 대포 아래 회수대", 60, 72, 3, 18, -25);
            OpenConveyorRail(p, "진입 회수대 서측 난간", new Vector3(60, -25, 10.5f), 15, false);
            OpenConveyorRail(p, "진입 회수대 동측 난간", new Vector3(72, -25, 10.5f), 15, false);
            OpenConveyorRail(p, "진입 회수대 북측 난간", new Vector3(66, -25, 18), 12, true);
            // Open the prior lower north rail only at the joined recovery apron.
            var recovery = root.Find("추락 후 재탑승 정비로");
            foreach (Transform t in recovery)
                if (t.name.StartsWith("회수 정비로 북측 난간"))
                { Undo.RecordObject(t.gameObject, "Connect cannon recovery apron"); t.gameObject.SetActive(false); }
            OpenConveyorRail(p, "북측 회수 연결 왼쪽", new Vector3(44.5f, -25, 3), 31, true);
            OpenConveyorRail(p, "북측 회수 연결 오른쪽", new Vector3(81, -25, 3), 18, true);
            foreach (float z in new[] { -20f, -39f, -55f })
                ConveyorArrow(p, new Vector3(35, -24.98f, z), Vector3.back);
            ConveyorArrow(p, new Vector3(66, -24.98f, 8), Vector3.back);
            // Edge paint announces where to release and where to catch the passing handhold.
            BMass(p, "하차 수신 가장자리 도색", new Vector3(38.25f, -19.18f, -62.2f), new Vector3(11.5f, .02f, .22f), maintenancePaint, false);
            BMass(p, "재탑승 노란 대기선", new Vector3(67, -19.18f, -18), new Vector3(6, .02f, .22f), maintenancePaint, false);
            BLight(p, new Vector3(80, -21, -28), new Vector3(76, -24.5f, -29), 330, 13);
            Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return "Old cannon screens retired; physical open rails; initial-cannon recovery connected";
        }

        static void OpenConveyorRail(Transform p, string name, Vector3 feet, float length, bool alongX)
        {
            Vector3 axis = alongX ? Vector3.right : Vector3.forward;
            Vector3 Size(float h, float thick) => alongX ? new Vector3(length, h, thick) : new Vector3(thick, h, length);
            BMass(p, name + " 발끝 막이", feet + Vector3.up * .075f, Size(.15f, .12f), wornSteel);
            BMass(p, name + " 중간대", feet + Vector3.up * .55f, Size(.07f, .07f), wornSteel);
            BMass(p, name + " 손잡이", feet + Vector3.up * 1.1f, Size(.07f, .07f), maintenancePaint);
            int count = Mathf.CeilToInt(length / 2);
            for (int i = 0; i <= count; i++)
                BMass(p, name + " 지주 " + i, feet + axis * (length * i / count - length * .5f) + Vector3.up * .55f,
                    new Vector3(.07f, 1.1f, .07f), wornSteel);
        }

        static void ConveyorGuard(Transform p, string name, Vector3 feet, Vector3 size)
        {
            // The visible barrier and its collision have exactly the same dimensions.
            BMass(p, name, feet + Vector3.up * size.y * .5f, size, wornSteel);
            BMass(p, name + " 노란 상단", feet + Vector3.up * (size.y + .025f), new Vector3(size.x, .05f, size.z), maintenancePaint, false);
        }

        static void ConveyorArrow(Transform p, Vector3 point, Vector3 forward)
        {
            var arrow = VerticalChild(p, "진행 방향 화살표");
            arrow.position = point; arrow.rotation = Quaternion.LookRotation(forward);
            BMass(arrow, "화살표 중심", Vector3.zero, new Vector3(.16f, .015f, 1.5f), maintenancePaint, false);
            foreach (float side in new[] { -1f, 1f })
            {
                var tip = BMass(arrow, "화살표 머리", new Vector3(side * .27f, 0, .53f), new Vector3(.16f, .015f, .8f), maintenancePaint, false);
                tip.transform.localRotation = Quaternion.Euler(0, -side * 45, 0);
            }
        }
    }
}
