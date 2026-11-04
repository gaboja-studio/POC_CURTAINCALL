using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace CurtainCall.UI.SceneIntegration.Tests
{
    /// <summary>2026-11-04 사용자: 리허설 균형바가 공연과 다르다는 보고. 공연·리허설에서 줄 위 균형바 화면을 같은 조건으로 남긴다.</summary>
    public sealed partial class DevStartLauncherPlayTests
    {
        static IEnumerator CaptureBalance(string name)
        {
            System.IO.Directory.CreateDirectory(".harness/local/rehearsal/balance");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath(".harness/local/rehearsal/balance/" + name + ".png"));
            yield return null;
        }

        static string DescribeBalanceViews()
        {
            var text = new System.Text.StringBuilder();
            foreach (var view in Object.FindObjectsByType(T("CurtainCall.UI.PerformanceHudV1.TightropeBalanceView"), FindObjectsSortMode.None))
            {
                var component = (Component)view;
                var canvas = component.GetComponentInParent<Canvas>();
                var scaler = canvas != null ? canvas.rootCanvas.GetComponent<UnityEngine.UI.CanvasScaler>() : null;
                var rect = (RectTransform)component.transform;
                text.Append(component.transform.root.name).Append('/').Append(component.name)
                    .Append(" active=").Append(component.gameObject.activeInHierarchy)
                    .Append(" match=").Append(scaler != null ? scaler.screenMatchMode + ":" + scaler.matchWidthOrHeight : "-")
                    .Append(" pos=").Append(rect.anchoredPosition).Append(" size=").Append(rect.rect.size).Append('\n');
            }
            return text.ToString();
        }

        [UnityTest]
        public IEnumerator W6_Rehearsal_BalanceBarMatchesPerformanceCapture()
        {
            T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, 0);
            try
            {
                yield return Launch(1, "Performance", 0, "Tightrope", "Tightrope");
                float until = Time.realtimeSinceStartup + 30f;
                while ((bool)Static("CurtainCall.UI.SceneIntegration.SceneCurtainTransition", "IsActive") && Time.realtimeSinceStartup < until) yield return null;
                yield return new WaitForSecondsRealtime(6f);
                var course = (Component)Find("CurtainCall.Tightrope.TightropeCourse");
                var localPlayer = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
                var mover = localPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
                var at = (Vector3)Call(course, "GetLanePosition", 0, 4f);
                Call(mover, "Relocate", at, Quaternion.LookRotation((Vector3)Get(course, "Forward")));
                yield return new WaitForSecondsRealtime(1.5f);
                System.IO.Directory.CreateDirectory(".harness/local/rehearsal/balance");
                System.IO.File.WriteAllText(".harness/local/rehearsal/balance/performance.txt", DescribeBalanceViews());
                yield return CaptureBalance("performance");

                yield return Launch(1, "Rehearsal", 0, "Tightrope", "WaitingRoomBase");
                yield return WaitStage(0, 10f);
                yield return new WaitForSecondsRealtime(1f);
                course = (Component)Priv(Rehearsal, "course");
                localPlayer = (Component)Static("CurtainCall.Network.PlayerSync.NetworkPlayer", "Local");
                mover = localPlayer.GetComponent(T("CurtainCall.Player.PlayerMover"));
                at = (Vector3)Call(course, "GetLanePosition", 0, 4f);
                Call(mover, "Relocate", at, Quaternion.LookRotation((Vector3)Get(course, "Forward")));
                yield return new WaitForSecondsRealtime(1.5f);
                System.IO.File.WriteAllText(".harness/local/rehearsal/balance/rehearsal.txt", DescribeBalanceViews());
                yield return CaptureBalance("rehearsal");
            }
            finally
            {
                T(RehearsalType).GetField("savedStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, 0);
            }
        }
    }
}
