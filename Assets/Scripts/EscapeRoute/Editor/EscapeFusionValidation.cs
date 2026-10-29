using System;
using System.Linq;
using System.Text;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string ValidateAFusionLayout()
        {
            if (SceneManager.GetActiveScene().path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A 편집 씬이 필요합니다.");
            var root = GameObject.Find(RootName + "/" + FusionGroup);
            if (root == null) throw new InvalidOperationException("융합 배치가 없습니다.");
            Physics.SyncTransforms();
            var report = new StringBuilder(); int passed = 0, failed = 0;
            void Check(string name, bool ok) { if (ok) passed++; else { failed++; report.AppendLine("FAIL " + name); } }
            void Floor(Vector3 feet) => Check("floor " + feet,
                Physics.Raycast(feet + Vector3.up * .12f, Vector3.down, out var hit, .24f)
                && Mathf.Abs(hit.point.y - feet.y) < .02f);
            bool Clear(Vector3 feet)
            {
                float radius = GameSettings.Base.Character.CapsuleRadius;
                float height = GameSettings.Base.Character.CapsuleHeight;
                return !Physics.CheckCapsule(feet + Vector3.up * (radius + .04f),
                    feet + Vector3.up * (height - radius), radius, ~0, QueryTriggerInteraction.Ignore);
            }
            foreach (var feet in new[] { new Vector3(89,0,28), new Vector3(96.2f,0,28),
                new Vector3(94,-4.6f,31), new Vector3(99,-4.6f,23), new Vector3(119,-8,28), new Vector3(121,-8,43) })
                Floor(feet);
            for (int i = 0; i < 23; i++)
            {
                var feet = new Vector3(90 + (i + .5f) * .35f, -(i + 1) * .2f, 22.7f);
                Floor(feet);
                // Head/torso volume, leaving the next 0.2m riser to the character controller's step handling.
                Check("stair clearance " + i, !Physics.CheckCapsule(feet + Vector3.up * .65f,
                    feet + Vector3.up * 1.2f, .3f, ~0, QueryTriggerInteraction.Ignore));
            }
            var ropeStart = new Vector3(51,-6,77); var ropeEnd = new Vector3(64,-6,82);
            for (int i = 0; i <= 28; i++) Check("03 rope body " + i, Clear(Vector3.Lerp(ropeStart, ropeEnd, i / 28f)));
            for (int i = 0; i <= 16; i++) Check("05 rope body " + i, Clear(Vector3.Lerp(new Vector3(89,0,28), new Vector3(96.2f,0,28), i / 16f)));
            for (int i = 0; i <= 16; i++) Check("drop body " + i, Clear(Vector3.Lerp(new Vector3(97.6f,0,28), new Vector3(98,-4.1f,28), i / 16f)));
            var surface = new Vector3(98,-4.1f,28);
            var firstHang = new Vector3(101,-.5f,28);
            const float provisionalUp = 12, maxAirControl = 3;
            float gravity = GameSettings.Base.Character.Gravity;
            float discriminant = provisionalUp * provisionalUp - 2 * gravity * (firstHang.y - surface.y);
            Check("rebound height budget", gravity > 0 && discriminant > 0);
            if (gravity > 0 && discriminant > 0)
            {
                float time = (provisionalUp + Mathf.Sqrt(discriminant)) / gravity;
                float horizontalSpeed = (firstHang.x - surface.x) / time;
                Check("rebound horizontal budget", horizontalSpeed <= maxAirControl);
                for (int i = 0; i <= 64; i++)
                {
                    float t = time * i / 64f;
                    var feet = surface + Vector3.right * (horizontalSpeed * t)
                        + Vector3.up * (provisionalUp * t - .5f * gravity * t * t);
                    Check("rebound body volume " + i, Clear(feet));
                }
                report.AppendLine($"Provisional rebound: {time:F2}s, horizontal {horizontalSpeed:F2}m/s. Analytical space budget only.");
            }
            foreach (float x in new[] { 104f, 108f, 112f })
                Check("remaining deep void " + x, !Physics.Raycast(new Vector3(x,-3,28), Vector3.down, 20, ~0, QueryTriggerInteraction.Ignore));
            Check("authoring anchors", GameObject.Find(TrickSetup + "/" + FusionAnchors) != null);
            Check("legacy anchors inactive", !GameObject.Find(TrickSetup).transform.Find("05_TrampolineAndSwing").gameObject.activeSelf);
            Check("no production gameplay on visual layout", root.GetComponentsInChildren<MonoBehaviour>().All(b => b is UnityEngine.Rendering.Universal.UniversalAdditionalLightData));
            Check("missing scripts", root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0);
            Check("materials", root.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name != "Hidden/InternalErrorShader")));
            Check("persistent meshes", root.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh != null && EditorUtility.IsPersistent(f.sharedMesh)));
            report.AppendLine($"Passed={passed}; Failed={failed}. Static space checks only. Manual traversal and multiplayer remain untested.");
            return report.ToString();
        }
    }
}
