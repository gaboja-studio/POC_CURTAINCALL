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
        public static string ValidateRunnerEscape()
        {
            string scenePath = SceneManager.GetActiveScene().path;
            bool sharedVariant = (scenePath == VariantBPath && GameObject.Find(RootName + "/" + TraversalSpacePrefix + "B") != null)
                || (scenePath == VariantCPath && GameObject.Find(RootName + "/" + TraversalSpacePrefix + "C") != null);
            if ((scenePath != ScenePath && !sharedVariant) || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A 또는 A 기반 B/C 편집 모드가 필요합니다.");
            var group = GameObject.Find(RootName + "/" + RunnerGroup);
            if (group == null) throw new InvalidOperationException("마지막 질주 구조가 없습니다.");
            Physics.SyncTransforms();
            var report = new StringBuilder(); int passed = 0, failed = 0;
            Action<string, bool> check = (name, ok) => { if (ok) passed++; else { failed++; report.AppendLine("FAIL " + name); } };
            // Sample the usable central strip, not the deliberately obstructed hazard pockets at each edge.
            for (float x = 140.5f; x < 400; x += 2)
            {
                if (x > 276 && x < 298) continue;
                foreach (float z in new[] { 76.5f, 78f, 79.5f })
                {
                    var at = new Vector3(x, 0, z);
                    check("floor " + at, Physics.Raycast(at + Vector3.up * .3f, Vector3.down, out var hit, .6f) && Mathf.Abs(hit.point.y) < .025f);
                }
            }
            foreach (float x in new[] { 276.1f, 280f, 287f, 294f, 297.9f })
                foreach (float z in new[] { 73f, 78f, 83f })
                    check("open swing gap " + x + "," + z, !Physics.Raycast(new Vector3(x, .1f, z), Vector3.down, 15));
            // 3m wide clearance corridor + an uninterrupted low eye line to the daylight opening.
            bool blocked = Physics.SphereCast(new Vector3(140.2f, 1.8f, 78), 1.5f, Vector3.right, out var block, 261);
            check("central 3m clearance" + (blocked ? " by " + block.collider.name : ""), !blocked);
            foreach (float x in new[] { 149f, 227f, 302f, 367f })
                check("exit sightline from " + x, !Physics.Linecast(new Vector3(x, 1.59f, 78), new Vector3(404, 1.59f, 78)));
            foreach (var step in new[] { new Vector3(164, 25, 78), new Vector3(226, 40, 78), new Vector3(276, 51, 78), new Vector3(298, 40, 78), new Vector3(366, 24, 78) })
                check("roof step enclosed " + step.x, Physics.Raycast(step - Vector3.right * 3, Vector3.right, 6));
            foreach (float x in new[] { 399.8f, 400.2f, 403f, 414f })
                check("exit floor " + x, Physics.Raycast(new Vector3(x, .3f, 78), Vector3.down, out var hit, .6f) && Mathf.Abs(hit.point.y) < .025f);
            var transforms = group.GetComponentsInChildren<Transform>(true);
            check("4 side launch anchors", transforms.Count(t => t.name.StartsWith("Anchor_SideLaunch_")) == 4);
            check("3 drop origin/impact pairs", transforms.Count(t => t.name.StartsWith("Anchor_FallOrigin_")) == 3 && transforms.Count(t => t.name.StartsWith("Anchor_FallImpact_")) == 3);
            check("3 swing grip anchors", transforms.Count(t => t.name.StartsWith("Anchor_SwingGrip_")) == 3);
            check("missing scripts", transforms.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0);
            check("materials", group.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name != "Hidden/InternalErrorShader")));
            check("persistent meshes", group.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<TMPro.TMP_Text>() == null).All(f => f.sharedMesh != null && AssetDatabase.Contains(f.sharedMesh)));
            check("world metre scale", (group.transform.lossyScale - Vector3.one).sqrMagnitude < .0001f);
            var outside = GameObject.Find(RootName).transform.Cast<Transform>().First(t => t.name.StartsWith("08_Reeds"));
            check("outside aligned", (outside.position - new Vector3(190, 0, 59.25f)).sqrMagnitude < .0001f);
            report.AppendLine("Passed=" + passed + "; Failed=" + failed + ". Static layout queries only. No live projectile, falling, swing, pursuit or escape mechanic; timing, traversal and performance require human playtesting.");
            return report.ToString();
        }
    }
}
