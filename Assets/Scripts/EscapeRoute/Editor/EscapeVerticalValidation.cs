using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string ValidateVerticalCavern()
        {
            if (SceneManager.GetActiveScene().path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A 편집 모드가 필요합니다.");
            var group = GameObject.Find(RootName + "/" + VerticalGroup);
            if (group == null) throw new InvalidOperationException("수직 설비실이 없습니다.");
            Physics.SyncTransforms();
            var report = new StringBuilder();
            int failures = 0;
            Action<string, bool> check = (name, success) => { report.AppendLine((success ? "PASS " : "FAIL ") + name); if (!success) failures++; };
            Vector3[] floors = { new Vector3(25, 0, 75), new Vector3(31, 0, 75), new Vector3(48, -6, 75),
                new Vector3(66, -6, 84), new Vector3(68, 2, 64), new Vector3(68, 2, 61) };
            foreach (var p in floors)
                check("floor " + p, Physics.Raycast(p + Vector3.up * .4f, Vector3.down, out var hit, .8f) && Mathf.Abs(hit.point.y - p.y) < .03f);
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(68, -6 + (i + 1) * .2f, 79 - (i + .5f) * .35f);
                check("stair " + (i + 1), Physics.Raycast(p + Vector3.up * .4f, Vector3.down, out var hit, .8f) && Mathf.Abs(hit.point.y - p.y) < .03f);
            }
            foreach (var p in new[] { new Vector3(38, -5, 75), new Vector3(57, -5, 80) })
                check("gap " + p, !Physics.Raycast(p, Vector3.down, 20));
            Vector3[,] connections = {
                { new Vector3(20, 1.6f, 75), new Vector3(28, 1.6f, 75) },
                { new Vector3(68, 3.6f, 64), new Vector3(68, 3.6f, 60) },
                { new Vector3(68, -4.4f, 80), new Vector3(68, 3.6f, 64.5f) }
            };
            for (int i = 0; i < connections.GetLength(0); i++)
            {
                var a = connections[i, 0]; var delta = connections[i, 1] - a;
                check("connector " + i, !Physics.SphereCast(a, .3f, delta.normalized, out _, delta.magnitude));
            }
            var transforms = group.GetComponentsInChildren<Transform>(true);
            check("missing scripts", transforms.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0);
            check("materials", group.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name != "Hidden/InternalErrorShader")));
            check("identity world scale", (group.transform.lossyScale - Vector3.one).sqrMagnitude < .0001f);
            check("legacy inactive", !group.transform.parent.Find("03_CannonRopeVoid_50x25").gameObject.activeSelf && !group.transform.parent.Find(RevealGroup).gameObject.activeSelf);
            check("main camera", Camera.main != null);
            report.AppendLine("Failures=" + failures + "; static geometry only, no gameplay traversal.");
            return report.ToString();
        }
    }
}
