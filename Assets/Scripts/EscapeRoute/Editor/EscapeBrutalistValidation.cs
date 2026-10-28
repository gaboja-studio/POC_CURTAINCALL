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
        public static string ValidateBrutalistRoute()
        {
            if (SceneManager.GetActiveScene().path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A 편집 모드가 필요합니다.");
            var group = GameObject.Find(RootName + "/" + BrutalGroup);
            if (group == null) throw new InvalidOperationException("새 A 구조가 없습니다.");
            Physics.SyncTransforms();
            var report = new StringBuilder(); int passed = 0, failed = 0;
            Action<string, bool> check = (name, success) => { if (success) passed++; else { failed++; report.AppendLine("FAIL " + name); } };
            Action<Vector3> floor = p => check("floor " + p, Physics.Raycast(p + Vector3.up * .4f, Vector3.down, out var hit, .8f) && Mathf.Abs(hit.point.y - p.y) < .03f);
            foreach (var p in new[] { new Vector3(15,0,10), new Vector3(15,0,25), new Vector3(15,0,60), new Vector3(21,0,75),
                new Vector3(68,2,61), new Vector3(68,0,54), new Vector3(68,0,27), new Vector3(75,0,25), new Vector3(83,0,25),
                new Vector3(121,-8,30), new Vector3(121,-8,49), new Vector3(123,0,65), new Vector3(130,0,78),
                new Vector3(145,0,78), new Vector3(180,0,78), new Vector3(185,0,41), new Vector3(185,0,25), new Vector3(202,0,18), new Vector3(215,0,18) }) floor(p);
            for (int i = 0; i < 10; i++) floor(new Vector3(68, 2 - (i + 1) * .2f, 60.5f - (i + .5f) * .5f));
            for (int i = 0; i < 40; i++) floor(new Vector3(123, -8 + (i + 1) * .2f, 50 + (i + .5f) * .35f));
            Vector3[,] spans = {
                {new Vector3(15,1.6f,20),new Vector3(15,1.6f,26)},
                {new Vector3(15,1.6f,26),new Vector3(15,1.6f,75)},
                {new Vector3(20,1.6f,75),new Vector3(28,1.6f,75)},
                {new Vector3(68,3.6f,64),new Vector3(68,3.6f,61)},
                {new Vector3(68,3.6f,61),new Vector3(68,1.6f,55)},
                {new Vector3(68,1.6f,54),new Vector3(68,1.6f,25)},
                {new Vector3(68,1.6f,25),new Vector3(85,1.6f,25)},
                {new Vector3(121,-6.4f,43),new Vector3(121,-6.4f,49)},
                {new Vector3(123,-6.4f,50),new Vector3(123,1.6f,64)},
                {new Vector3(123,1.6f,65),new Vector3(123,1.6f,80)},
                {new Vector3(123,1.6f,78),new Vector3(145,1.6f,78)},
                {new Vector3(145,1.6f,78),new Vector3(185,1.6f,78)},
                {new Vector3(185,1.6f,78),new Vector3(185,1.6f,40)},
                {new Vector3(185,1.6f,40),new Vector3(185,1.6f,25)},
                {new Vector3(185,1.6f,25),new Vector3(188,1.6f,18)},
                {new Vector3(188,1.6f,18),new Vector3(215,1.6f,18)}
            };
            for (int i = 0; i < spans.GetLength(0); i++)
            {
                var delta = spans[i,1] - spans[i,0];
                bool blocked = Physics.SphereCast(spans[i,0], .3f, delta.normalized, out var hit, delta.magnitude);
                check("connector " + i + (blocked ? " blocked by " + hit.collider.name : ""), !blocked);
            }
            foreach (float x in new[] { 94f, 102.5f, 112f }) check("05 gap " + x, !Physics.Raycast(new Vector3(x, -7, 25), Vector3.down, 16));
            check("missing scripts", group.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0);
            check("materials", group.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name != "Hidden/InternalErrorShader")));
            // TMP regenerates its own text mesh on load; authored geometry must be persistent.
            check("persistent meshes", group.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<TMPro.TMP_Text>() == null)
                .All(f => f.sharedMesh != null && AssetDatabase.Contains(f.sharedMesh)));
            check("world metres", (group.transform.lossyScale - Vector3.one).sqrMagnitude < .0001f);
            report.AppendLine("Passed=" + passed + "; Failed=" + failed + "; floors=19+50 stairs, connectors=16, gaps=3. Static queries only; gameplay and performance not tested.");
            report.AppendLine("A03 validation:\n" + ValidateVerticalCavern());
            return report.ToString();
        }
    }
}
