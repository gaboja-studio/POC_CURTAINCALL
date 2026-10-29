using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string FusionGroup = "24_TrickFusion_A";
        const string FusionAnchors = "05_RopeDropRebound";

        [MenuItem("Tools/CurtainCall/탈출로/A 묘기 융합 동선 배치")]
        public static void BuildAFusionLayout()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A 편집 씬에서 실행하세요.");
            var background = GameObject.Find(RootName).transform;
            var setup = GameObject.Find(TrickSetup)?.transform;
            if (setup == null || background.Find(FusionGroup) != null)
                throw new InvalidOperationException("기준점이 필요합니다. 기존 융합 배치는 재생성하지 않고 직접 편집하세요.");
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefabs/Objects/ETCs/HumanCannonArtKit/RoundCarryTrampolineVisual.prefab");
            if (visual == null) throw new InvalidOperationException("기존 트램폴린 시각 프리팹이 없습니다.");
            bConcrete = AssetDatabase.LoadAssetAtPath<Material>(Materials + "brutal-concrete.mat");
            bRecess = AssetDatabase.LoadAssetAtPath<Material>(Materials + "brutal-recess.mat");
            bSteel = AssetDatabase.LoadAssetAtPath<Material>(Materials + "brutal-steel.mat");
            bBulb = AssetDatabase.LoadAssetAtPath<Material>(Materials + "concrete-service-bulb.mat");
            if (new[] { bConcrete, bRecess, bSteel, bBulb }.Any(m => m == null))
                throw new InvalidOperationException("A 전용 재질이 필요합니다.");

            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("A trick fusion spatial layout");
            var group = VerticalChild(background, FusionGroup);
            group.localScale = new Vector3(1 / background.lossyScale.x, 1 / background.lossyScale.y, 1 / background.lossyScale.z);
            var first = VerticalChild(group, "03_CannonLandingToRope");
            var transfer = VerticalChild(group, "05_RopeDropTrampolineSwing");
            var oldRope = background.Find(VerticalGroup + "/02_Route_0_to_minus6_to_plus2/외줄 연결 시각 표시");
            PreserveFusionGuide(oldRope);
            var oldTransfer = background.Find(BrutalGroup + "/05_TransferChasm");
            foreach (Transform t in oldTransfer)
                if (t.name == "트램폴린 설치 자리" || t.name.StartsWith("그네 설치", StringComparison.Ordinal))
                    PreserveFusionGuide(t);
            PreserveFusionGuide(setup.Find("05_TrampolineAndSwing"));

            // A03 is the readable introduction. The landing island gives room to turn and regroup.
            FusionRope(first, "03 외줄", new Vector3(51, -5.95f, 77), new Vector3(64, -5.95f, 82));
            FusionSocket(first, new Vector3(50.6f, -6, 76.85f));
            FusionSocket(first, new Vector3(64.5f, -6, 82.2f));
            FusionFloorMarks(first, new Vector3(50.2f, -5.98f, 76.7f), new Vector3(13, 0, 5));
            FusionFloorMarks(first, new Vector3(65, -5.98f, 82.4f), new Vector3(3, 0, -4));

            BuildFusionTransferSpace(transfer, visual);
            var points = VerticalChild(setup, FusionAnchors);
            TrickPoint(points, "Approach_Feet", new Vector3(86, 0, 28), Vector3.right);
            TrickPoint(points, "RopeEntry_Feet", new Vector3(89, 0, 28), Vector3.right);
            TrickPoint(points, "RopeEnd_Feet", new Vector3(96.2f, 0, 28), Vector3.right);
            TrickPoint(points, "DropEdge_Feet", new Vector3(96.8f, 0, 28), Vector3.right);
            TrickPoint(points, "Trampoline_Base", new Vector3(98, -4.6f, 28), Vector3.right);
            TrickPoint(points, "Bounce_Surface", new Vector3(98, -4.1f, 28), Vector3.right);
            TrickPoint(points, "SupportStaging_Feet", new Vector3(94, -4.6f, 31), Vector3.right);
            TrickPoint(points, "Landing_Feet", new Vector3(119, -8, 28), Vector3.right);
            for (int i = 0; i < 3; i++)
            {
                TrickPoint(points, "Swing_" + (i + 1) + "_Grip", new Vector3(101 + i * 6, 1 - i, 28), Vector3.right);
                TrickPoint(points, "Swing_" + (i + 1) + "_Pivot", new Vector3(101 + i * 6, 19.5f, 28), Vector3.right);
            }
            EnsureReviewLabels(background.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = transfer.gameObject;
            Undo.CollapseUndoOperations(undo);
        }

        static void PreserveFusionGuide(Transform t)
        {
            if (t == null) return;
            Undo.RecordObject(t.gameObject, "Preserve superseded spatial guide");
            t.gameObject.SetActive(false);
        }

        [MenuItem("Tools/CurtainCall/탈출로/A 묘기 융합 03 보기")]
        public static void ViewAFusion03() => ViewAFusion(new Vector3(28, 3, 76), new Vector3(52, -5, 79));

        [MenuItem("Tools/CurtainCall/탈출로/A 묘기 융합 05 보기")]
        public static void ViewAFusion05() => ViewAFusion(new Vector3(86, 3.2f, 32), new Vector3(103, -3, 28));

        static void ViewAFusion(Vector3 eye, Vector3 target)
        {
            if (SceneManager.GetActiveScene().path != ScenePath) throw new InvalidOperationException("A 씬을 먼저 여세요.");
            var view = EditorWindow.GetWindow<SceneView>(); view.Show();
            var rotation = Quaternion.LookRotation(target - eye);
            view.LookAtDirect(eye + rotation * Vector3.forward * 10, rotation,
                10 * Mathf.Sin(view.cameraSettings.fieldOfView * Mathf.Deg2Rad * .5f));
            view.Repaint();
        }
    }
}
