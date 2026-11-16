using System;
using System.Linq;
using CurtainCall.HumanCannon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapePursuitRemodel
    {
        public static void ApplyConveyorFlow()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("저장된 복사 씬의 편집 모드가 필요합니다.");

            module = Object.FindFirstObjectByType<EscapePursuerRoute>().transform;
            frame = module.GetComponentsInParent<Transform>().Single(t => t.name == "EscapeCampaignFrame");
            var background = frame.Find("EscapeBackground");
            var feedback = frame.Find(FeedbackRoot);
            const string groupName = "Conveyor launch and landing circulation";
            var landing = background.Find("41_AReadableRoute/02_OneWayDisembark");
            var receiver = background.Find("43_AConveyorRecovery");
            var yellow = receiver.Find("노란 하차장 수신 발판").GetComponent<MeshRenderer>().sharedMaterial;
            var steel = landing.Find("대포 전원 표시 전기함").GetComponent<MeshRenderer>().sharedMaterial;
            concrete = feedback.Find("Switchback central concrete divider").GetComponent<MeshRenderer>().sharedMaterial;
            structure = feedback.Find(groupName);
            if (structure)
            {
                if (FinishConveyorLanding(landing, steel, yellow)) Save();
                return;
            }

            var cannon = frame.GetComponentsInChildren<HumanCannonStation>(true).Single(c => c.name == "A04B_ConveyorCannon");
            var deck = background.Find("34_AFusedRoute/03_04A_CannonWireDescent/대포 포획 준비대").GetComponent<BoxCollider>();
            var locks = new SerializedObject(frame.GetComponentInChildren<EscapeCooperativeLocks>(true));
            var checkpoint = locks.FindProperty("checkpoints").GetArrayElementAtIndex(0);
            var control = (Transform)checkpoint.FindPropertyRelative("shoulderControl").objectReferenceValue;
            var indicator = (Renderer)checkpoint.FindPropertyRelative("indicator").objectReferenceValue;
            structure = new GameObject(groupName).transform;
            structure.SetParent(feedback, false);

            // One switch for every party size. Retain the original circuit, shutter and status renderer.
            checkpoint.FindPropertyRelative("lastRunnerControl").objectReferenceValue = null;
            checkpoint.FindPropertyRelative("prompt").stringValue = "복귀 대포 전원 켜기";
            checkpoint.FindPropertyRelative("sourceName").stringValue = "하차장 전원 레버";
            checkpoint.FindPropertyRelative("instruction").stringValue =
                "하차장 전원 레버에 F → 외줄과 톱을 지나 복귀 대포로";
            locks.ApplyModifiedPropertiesWithoutUndo();
            foreach (var t in landing.Cast<Transform>().Where(t => t.name == "전원 버튼 (마지막 주자)"
                || t.name == "횡보 전기함 매다는 브래킷" || t.name == "횡보 전기함 고정부"
                || t.name == "외줄 전원함 내민보" || t.name == "기존 크레인 횡보").ToArray())
                Object.DestroyImmediate(t.gameObject);

            var housing = landing.Find("대포 전원 표시 전기함");
            var console = new Vector3(-241.35f, 7.35f, 189.55f);
            var facing = Quaternion.LookRotation(new Vector3(.64f, 0, -.77f));
            housing.SetPositionAndRotation(console, facing);
            housing.localScale = new Vector3(.74f, .7f, .27f);
            control.name = "하차장 전원 레버";
            control.SetPositionAndRotation(console + facing * new Vector3(-.12f, .02f, .235f), facing);
            indicator.transform.SetPositionAndRotation(console + facing * new Vector3(.19f, .17f, .17f), facing);
            indicator.transform.localScale = new Vector3(.14f, .17f, .07f);
            Block("Power console pedestal", console - Vector3.up * .58f, new(.39f, .54f, .29f), steel, facing);
            Block("Power console top marker", console + Vector3.up * .365f, new(.73f, .025f, .26f), yellow, facing, false);

            // Four supported 17.5cm risers already span the entire platform seam.
            foreach (var t in receiver.Cast<Transform>().Where(t => t.name == "하차장 낮은 단차 1"
                || t.name == "하차장 낮은 단차 2").ToArray())
                Object.DestroyImmediate(t.gameObject);

            // Removed/replaced wall sections that were left dormant by the earlier shaft openings.
            RemoveInactive(background.Find("16_VerticalFacility/01_Shell_50x50_134m"),
                "서측 하부 외벽", "서측 상부 외벽", "입구 남측 벽");
            RemoveInactive(background.Find("35_AOpenFacility/02_ExpandedPumpEnvelope"), "타설 콘크리트 벽");
            RemoveInactive(background.Find("A_FourPlayerRoute"), "그네 회수 벽체 측판");

            Physics.SyncTransforms();
            var bounds = deck.bounds;
            float floor = bounds.max.y;
            float end = bounds.max.z - .08f;
            // The open playing side is the cannon mouth. Low rails on the deck make the void
            // legible without blocking the view of poles or the cannon's launch sweep.
            ConveyorRail("Launch end guard", new(bounds.center.x, floor, end), new(bounds.size.x - .12f, 0, 0), steel, yellow);
            float sideStart = bounds.min.z + 2.65f;
            foreach (float x in new[] { bounds.min.x + .08f, bounds.max.x - .08f })
                ConveyorRail("Launch side guard", new(x, floor, (end + sideStart) * .5f), new(0, 0, end - sideStart), steel, yellow);

            // Exclude any nearby lower geometry and keep the grant scoped to this one cannon.
            var waitDeck = new GameObject("Conveyor wait deck", typeof(BoxCollider)).GetComponent<BoxCollider>();
            waitDeck.transform.SetParent(structure, false);
            waitDeck.transform.position = new Vector3(bounds.center.x, floor + .65f, bounds.center.z);
            waitDeck.size = new Vector3(bounds.size.x - .15f, 2, bounds.size.z - .12f);
            waitDeck.isTrigger = true;
            var chase = new SerializedObject(module.GetComponentInChildren<EscapeUnderstageChase>(true));
            chase.FindProperty("conveyorCannon").objectReferenceValue = cannon;
            chase.FindProperty("conveyorWaitDeck").objectReferenceValue = waitDeck;
            chase.ApplyModifiedPropertiesWithoutUndo();

            FinishConveyorLanding(landing, steel, yellow);
            Save();

            void Save()
            {
                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        static bool FinishConveyorLanding(Transform landing, Material steel, Material yellow)
        {
            if (structure.Find("Disembark rear guard hand rail")) return false;
            // These decorative saw bins had solid box tops in open space alongside the wire.
            // They offered a false jumping route but did not collect or drive either saw.
            foreach (var t in landing.Cast<Transform>().Where(t => t.name == "톱 반출 회수함").ToArray())
                Object.DestroyImmediate(t.gameObject);
            Physics.SyncTransforms();
            var bounds = landing.Find("한 곳으로 모이는 하차장").GetComponent<BoxCollider>().bounds;
            float rear = bounds.max.z - .08f, front = bounds.min.z + .08f;
            ConveyorRail("Disembark rear guard", new(bounds.center.x, bounds.max.y, rear),
                new(bounds.size.x - .16f, 0, 0), steel, yellow);
            ConveyorRail("Disembark side guard", new(bounds.max.x - .08f, bounds.max.y, (rear + front) * .5f),
                new(0, 0, rear - front), steel, yellow);
            return true;
        }

        static void ConveyorRail(string name, Vector3 center, Vector3 span, Material steel, Material yellow)
        {
            bool horizontal = span.x != 0;
            float length = span.magnitude;
            Vector3 Size(float width, float height) => horizontal ? new(length, height, width) : new(width, height, length);
            Block(name + " mid rail", center + Vector3.up * .54f, Size(.11f, .11f), steel, Quaternion.identity);
            Block(name + " hand rail", center + Vector3.up * 1.02f, Size(.11f, .1f), yellow, Quaternion.identity);
            int sections = Mathf.CeilToInt(length / 2.2f);
            Vector3 pole = horizontal ? Vector3.right : Vector3.forward;
            for (int i = 0; i <= sections; i++)
                Block(name + " post", center + pole * ((i / (float)sections - .5f) * (length - .12f))
                    + Vector3.up * .5f, new(.105f, 1, .105f), steel, Quaternion.identity);
        }

        static Transform Block(string name, Vector3 point, Vector3 size, Material material, Quaternion rotation, bool solid = true)
        {
            var block = Box(name, frame.InverseTransformPoint(point), size);
            block.rotation = rotation;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(block.GetComponent<BoxCollider>());
            return block;
        }

        static void RemoveInactive(Transform parent, params string[] names)
        {
            foreach (var t in parent.Cast<Transform>().Where(t => !t.gameObject.activeSelf && names.Contains(t.name)).ToArray())
                Object.DestroyImmediate(t.gameObject);
        }
    }
}
