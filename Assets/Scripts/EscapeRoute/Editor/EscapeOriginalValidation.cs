using System;
using System.Linq;
using System.Text;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Tightrope;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string ValidateOriginalA03()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || Application.isPlaying) throw new InvalidOperationException("A 편집 모드가 필요합니다.");
            var report = new StringBuilder(); int passed = 0, failed = 0;
            void Check(string label, bool value) { if (value) passed++; else { failed++; report.AppendLine("FAIL " + label); } }
            var roots = scene.GetRootGameObjects();
            var review = roots.SingleOrDefault(r => r.name == OriginalReview);
            Check("original review root active", review != null && review.activeSelf);
            Check("temporary Solo inactive", roots.Single(r => r.name == "EscapeSoloReview_A").activeSelf == false);
            Check("preview player inactive", roots.Single(r => r.name == "PlaySetup").activeSelf == false);
            var cannon = UnityEngine.Object.FindObjectsByType<HumanCannonSystem>(FindObjectsSortMode.None);
            Check("one enabled original cannon", cannon.Length == 1 && cannon[0].enabled);
            var courses = UnityEngine.Object.FindObjectsByType<TightropeCourse>(FindObjectsSortMode.None);
            Check("one authored rope course", courses.Length == 1 && courses[0].UsesSceneGeometry);
            if (courses.Length == 1)
            {
                var course = courses[0];
                Check("two independent original-width ropes", course.LaneCount == 2 && course.HasIndependentRopes);
                Check("authored 13.93m length", Mathf.Abs(course.FinishDistance - Mathf.Sqrt(194)) < .001f);
                Check("no tightrope swing variants", !course.HasSwingGap && !course.HasRelayGap && !course.HasTrapezeGap);
                Check("no generated performance stage", course.transform.Find("Generated") == null);
                foreach (var type in new[] { typeof(CourseControlSwitcher), typeof(TightropeRun), typeof(TightropeHangSystem),
                    typeof(TightropeRopeSway), typeof(TightropeCatchSystem), typeof(PiggybackSystem), typeof(PiggybackFollow) })
                    Check(type.Name, course.GetComponent(type) != null);
                var support = course.transform.Find("RopeSupport_0").GetComponent<BoxCollider>();
                Check("original rope support width", Mathf.Approximately(support.size.x, course.Shape.RopeWalkWidth));
                Check("support is not trigger", !support.isTrigger && support.enabled);
                Physics.SyncTransforms();
                foreach (float distance in new[] { 3f, 6f, 9f, 10f })
                {
                    var position = course.GetLanePosition(0, distance);
                    Check("rope floor " + distance, Physics.Raycast(position + Vector3.up * .15f, Vector3.down, out var hit, .3f)
                        && hit.collider == support && Mathf.Abs(hit.point.y + 6) < .01f);
                }
            }
            Check("one spawn roster", UnityEngine.Object.FindObjectsByType<PlayerSpawnPoints>(FindObjectsSortMode.None).Length == 1);
            Check("one player follow camera", UnityEngine.Object.FindObjectsByType<LocalPlayerCamera>(FindObjectsSortMode.None).Length == 1);
            Check("main camera brain", Camera.main != null && Camera.main.GetComponent<CinemachineBrain>() != null);
            Check("automatic solo development", UnityEngine.Object.FindObjectsByType<EscapeDevelopmentSession>(FindObjectsSortMode.None).Length == 1
                && UnityEngine.Object.FindObjectsByType<NetworkSessionTestUI>(FindObjectsSortMode.None).Length == 0);
            Check("original balance display", UnityEngine.Object.FindObjectsByType<EscapeRopeBalanceDisplay>(FindObjectsSortMode.None).Length == 1);
            var behaviours = roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            Check("no active bots", !behaviours.Any(b => b != null && b.isActiveAndEnabled && b.GetType().Name.Contains("PracticeBot")));
            Check("no active tightrope swings", !behaviours.Any(b => b != null && b.isActiveAndEnabled
                && (b is TightropeSwingPole || b is TightropeTrapeze || b is TightropeRelayGate)));
            Check("missing scripts", roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0);
            report.AppendLine($"Passed={passed}; Failed={failed}. Static wiring/geometry only; manual A03 traversal pending.");
            return report.ToString();
        }
    }
}
