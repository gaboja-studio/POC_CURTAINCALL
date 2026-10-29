using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string SharedASpaceSignature()
        {
            var root = GameObject.Find(RootName).transform;
            var paths = new[] { BrutalGroup + "/01_Understage", BrutalGroup + "/02_CompressionGallery",
                BrutalGroup + "/04_DeepServiceCut", BrutalGroup + "/06_AscendingWorks", RunnerGroup, "08_Reeds_30x37.5" };
            var rows = new List<object>();
            foreach (string path in paths)
            {
                var group = root.Find(path);
                if (group == null) throw new InvalidOperationException("공통 A 구간 누락: " + path);
                foreach (var t in group.GetComponentsInChildren<Transform>(true))
                {
                    string Key(Transform at) => at == group ? path : Key(at.parent) + "/" + at.GetSiblingIndex() + ":" + at.name;
                    var r = t.GetComponent<MeshRenderer>(); var f = t.GetComponent<MeshFilter>(); var l = t.GetComponent<Light>();
                    var p = t.localPosition; var q = t.localRotation; var s = t.localScale;
                    rows.Add(new { path = Key(t), active = t.gameObject.activeSelf, p = new[]{p.x,p.y,p.z}, q = new[]{q.x,q.y,q.z,q.w}, s = new[]{s.x,s.y,s.z},
                        mesh = f == null || f.sharedMesh == null ? null : AssetDatabase.GetAssetPath(f.sharedMesh) + ":" + f.sharedMesh.name,
                        materials = r == null ? null : r.sharedMaterials.Select(m => m == null ? "null" : AssetDatabase.GetAssetPath(m)).ToArray(),
                        light = l == null ? null : new { l.enabled, l.type, l.intensity, l.range, l.spotAngle, l.innerSpotAngle, color=new[]{l.color.r,l.color.g,l.color.b,l.color.a}, l.shadows } });
                }
            }
            var settings = new Newtonsoft.Json.JsonSerializerSettings { ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore };
            string text = Newtonsoft.Json.JsonConvert.SerializeObject(rows, settings);
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }

        public static object ValidateASharedVariant()
        {
            var scene = SceneManager.GetActiveScene(); bool b = scene.path == VariantBPath;
            if ((!b && scene.path != VariantCPath) || Application.isPlaying) throw new InvalidOperationException("B/C 편집 씬이 필요합니다.");
            var background = GameObject.Find(RootName);
            if (background.transform.Find("29_" + (b ? "B" : "C") + "_OriginalMechanics") != null)
                return new { scene = scene.name, mechanics = ValidateCompletedEscape(), traversal = ValidateVariantTraversal(), joints = ValidateEscapeJoints() };
            var group = background.transform.Find(TraversalSpacePrefix + (b ? "B" : "C"));
            if (group == null) throw new InvalidOperationException("A 기반 공간이 없습니다.");
            var failures = new List<string>(); int floors = 0, links = 0;
            Physics.SyncTransforms();
            void Floor(Vector3 at)
            {
                floors++;
                if (!Physics.Raycast(at + Vector3.up * .12f, Vector3.down, out var hit, .24f, ~0, QueryTriggerInteraction.Ignore)
                    || Mathf.Abs(hit.point.y - at.y) > .025f) failures.Add("floor " + at);
            }
            foreach (var at in new[] { new Vector3(15,0,10),new Vector3(15,0,50),new Vector3(29,0,75),
                new Vector3(66,-6,84),new Vector3(68,2,64),new Vector3(68,2,61),new Vector3(68,0,52),new Vector3(75,0,25),
                new Vector3(83,0,25),new Vector3(121,-8,35),new Vector3(121,-8,49),new Vector3(123,0,65),new Vector3(130,0,78) }) Floor(at);
            for (int i = 0; i < 40; i++)
            {
                Floor(new Vector3(68.5f, -6 + (i + 1) * .2f, 79 - (i + .5f) * .35f));
                Floor(new Vector3(123, -8 + (i + 1) * .2f, 50 + (i + .5f) * .35f));
            }
            for (int i = 0; i < 10; i++) Floor(new Vector3(68, 2 - (i + 1) * .2f, 60.5f - (i + .5f) * .5f));
            if (b)
            {
                foreach (var at in new[] { new Vector3(38,2,75),new Vector3(47,5,78),new Vector3(56.5f,.5f,82),
                    new Vector3(96.5f,2,25),new Vector3(105.5f,-2.5f,27.5f),new Vector3(113,-5.5f,32) })
                    foreach (var offset in new[] { Vector3.zero, Vector3.right, Vector3.left, Vector3.forward, Vector3.back }) Floor(at + offset);
                foreach (var at in new[] { new Vector3(33,0,75),new Vector3(42.5f,2,77),new Vector3(52,0,80),
                    new Vector3(60.5f,-6,83),new Vector3(92,0,25),new Vector3(101,-2.5f,26),new Vector3(109.5f,-5.5f,30) })
                    if (Physics.Raycast(at + Vector3.up * .2f, Vector3.down, 1)) failures.Add("reserved gap filled " + at);
            }
            else
            {
                foreach (var at in new[] { new Vector3(34,0,74.5f),new Vector3(39.5f,0,74.5f),new Vector3(57,-6,75),
                    new Vector3(62,-6,77.3f),new Vector3(63.6f,-6,80),new Vector3(92,0,25),new Vector3(97,0,25),new Vector3(110,-4,25) }) Floor(at);
                for (int i = 0; i < 30; i++) Floor(new Vector3(43 + (i + .5f) * .35f, -(i + 1) * .2f, 74.5f));
                for (int i = 0; i < 20; i++) Floor(new Vector3(100 + (i + .5f) * .35f, -(i + 1) * .2f, 25));
                for (int i = 0; i < 19; i++) Floor(new Vector3(113 + (i + .5f) * .35f, -4 - (i + 1) * .2f, 25));
            }
            foreach (var ends in new[] {
                new[]{new Vector3(20,1.6f,75),new Vector3(30,1.6f,75)},
                new[]{new Vector3(68,3.6f,64),new Vector3(68,3.6f,61)},
                new[]{new Vector3(68,1.6f,25),new Vector3(85,1.6f,25)},
                new[]{new Vector3(121,-6.4f,42),new Vector3(121,-6.4f,49)},
                new[]{new Vector3(130,1.6f,78),new Vector3(145,1.6f,78)} })
            {
                links++; var delta = ends[1] - ends[0];
                if (Physics.SphereCast(ends[0], .3f, delta.normalized, out var hit, delta.magnitude)) failures.Add("common connection blocked by " + hit.collider.name);
            }
            int missing = background.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            int materials = background.GetComponentsInChildren<Renderer>().Count(r => r.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader"));
            int meshes = background.GetComponentsInChildren<MeshFilter>().Where(f => f.GetComponent<TMPro.TMP_Text>() == null).Count(f => f.sharedMesh == null || !AssetDatabase.Contains(f.sharedMesh));
            if (missing + materials + meshes != 0) failures.Add("missing scripts/materials/persistent meshes");
            if (group.Find("99_Complete") == null) failures.Add("unfinished layout");
            if (scene.isDirty) failures.Add("unsaved scene");
            string runner = ValidateRunnerEscape();
            if (!runner.Contains("Failed=0")) failures.Add("shared final corridor failed: " + runner);
            return new {scene=scene.path,ok=failures.Count==0,floors,links,missingScripts=missing,badMaterials=materials,badMeshes=meshes,
                sharedSignature=SharedASpaceSignature(),runner,failures,scope="Spatial layout only. Stunt fusion, ranges and live traversal remain future work."};
        }
    }
}
