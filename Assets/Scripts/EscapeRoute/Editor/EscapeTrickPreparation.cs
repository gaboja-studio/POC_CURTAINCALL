using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string TrickSetup = "EscapeTrickSetup_A";

        [MenuItem("Tools/CurtainCall/탈출로/A 묘기 연결 기준점 준비")]
        public static void PrepareATrickLayout()
        {
            if (SceneManager.GetActiveScene().path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("저장된 A 씬의 편집 모드가 필요합니다.");
            if (GameObject.Find(TrickSetup) != null)
                throw new InvalidOperationException("A 기준점이 이미 있습니다. 기존 기준점을 직접 편집하세요.");
            var setup = new GameObject(TrickSetup).transform;
            Undo.RegisterCreatedObjectUndo(setup.gameObject, "Prepare A trick attachment points");
            var cannon = VerticalChild(setup, "03_CannonToIsland");
            TrickPoint(cannon, "Boarding_Feet", new Vector3(27, 0, 75), Vector3.right);
            TrickPoint(cannon, "Station_Base", new Vector3(29, 0, 75), Vector3.right);
            TrickPoint(cannon, "Landing_Feet", new Vector3(48, -6, 75), Vector3.right);
            var rope = VerticalChild(setup, "03_TightropeToArrival");
            var ropeStart = new Vector3(51, -6, 77); var ropeEnd = new Vector3(64, -6, 82);
            TrickPoint(rope, "Entry_Feet", ropeStart, ropeEnd - ropeStart);
            TrickPoint(rope, "Arrival_Feet", ropeEnd, ropeEnd - ropeStart);
            var transfer = VerticalChild(setup, "05_TrampolineAndSwing");
            TrickPoint(transfer, "Approach_Feet", new Vector3(83, 0, 25), Vector3.right);
            TrickPoint(transfer, "Trampoline_Base", new Vector3(86.25f, 0, 25), Vector3.right);
            TrickPoint(transfer, "Landing_Feet", new Vector3(121, -8, 25), Vector3.right);
            for (int i = 0; i < 3; i++)
            {
                TrickPoint(transfer, "Swing_" + (i + 1) + "_Grip", new Vector3(95 + i * 9, 5 - i * 2, 25), Vector3.right);
                TrickPoint(transfer, "Swing_" + (i + 1) + "_Pivot", new Vector3(95 + i * 9, 19.5f, 25), Vector3.right);
            }
            // Existing final-run anchors remain the single source for that section.
            // These transforms reserve placement only; they do not instantiate mechanics or tune physics.
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = setup.gameObject;
        }

        static void TrickPoint(Transform parent, string name, Vector3 at, Vector3 forward)
        {
            var point = VerticalChild(parent, name);
            point.position = at; point.rotation = Quaternion.LookRotation(forward.normalized);
        }

        public static string ValidateATrickPreparation()
        {
            if (SceneManager.GetActiveScene().path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A 편집 모드가 필요합니다.");
            var setup = GameObject.Find(TrickSetup);
            if (setup == null) throw new InvalidOperationException("먼저 A 기준점을 준비하세요.");
            Physics.SyncTransforms();
            int passed = 0, failed = 0; var result = new StringBuilder();
            Action<string, bool> check = (name, ok) => { if (ok) passed++; else { failed++; result.AppendLine("FAIL " + name); } };
            var points = setup.GetComponentsInChildren<Transform>().Where(t => t.childCount == 0).ToArray();
            bool fusion = setup.transform.Find(FusionAnchors) != null;
            check("placement points", points.Length == (fusion ? 19 : 14));
            check("no gameplay or collision added", setup.GetComponentsInChildren<Collider>().Length == 0
                && setup.GetComponentsInChildren<MonoBehaviour>().Length == 0);
            foreach (var point in points.Where(t => t.name.EndsWith("_Feet", StringComparison.Ordinal)))
                check("floor " + point.parent.name + "/" + point.name,
                    Physics.Raycast(point.position + Vector3.up * .15f, Vector3.down, out var hit, .3f)
                    && Mathf.Abs(hit.point.y - point.position.y) < .03f);
            var runner = GameObject.Find(RootName + "/" + RunnerGroup);
            check("final-run setup present", runner != null);
            if (runner != null)
            {
                var anchors = runner.GetComponentsInChildren<Transform>();
                check("final-run swing grips", anchors.Count(t => t.name.StartsWith("Anchor_SwingGrip_")) == 3);
                check("final-run takeoff/landing", anchors.Count(t => t.name == "Anchor_SwingTakeoff" || t.name == "Anchor_SwingLanding") == 2);
            }
            result.AppendLine("Passed=" + passed + "; Failed=" + failed + ". Placement references only; this does not validate runtime traversal.");
            return result.ToString();
        }
    }
}
