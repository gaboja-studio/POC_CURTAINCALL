using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static object ValidateCurrentVariant()
        {
            var scene = SceneManager.GetActiveScene();
            bool b = scene.path == VariantBPath;
            if (!b && scene.path != VariantCPath) throw new InvalidOperationException("B/C 구조 씬을 먼저 여세요.");
            if (GameObject.Find(RootName + "/" + TraversalSpacePrefix + (b ? "B" : "C")) != null)
                return ValidateASharedVariant();
            var errors = new List<string>();
            var background = scene.GetRootGameObjects().First(g => g.name == RootName);
            Physics.SyncTransforms();
            // This is static geometry inspection, never input injection or a gameplay traversal test.
            Vector3[] from = b ? new[] {
                new Vector3(17.5f,1.6f,20),new Vector3(22,1.6f,58),new Vector3(69,1.6f,48),
                new Vector3(79,1.6f,23),new Vector3(126,5.6f,31),new Vector3(142,1.6f,63),
                new Vector3(185,1.6f,55),new Vector3(185,1.6f,32),new Vector3(207,1.6f,18.75f)
            } : new[] {
                new Vector3(28,1.6f,13.5f),new Vector3(72.5f,1.6f,18),new Vector3(82,3.6f,64),
                new Vector3(119,1.6f,54),new Vector3(140,1.6f,8),new Vector3(157,1.6f,42),
                new Vector3(165,1.6f,80),new Vector3(192,1.6f,73),new Vector3(207,1.6f,51.25f)
            };
            Vector3[] to = b ? new[] {
                new Vector3(17.5f,1.6f,25),new Vector3(29,1.6f,58),new Vector3(69,1.6f,43),
                new Vector3(87,1.6f,23),new Vector3(126,5.6f,36),new Vector3(149,1.6f,63),
                new Vector3(185,1.6f,50),new Vector3(185,1.6f,27),new Vector3(214,1.6f,18.75f)
            } : new[] {
                new Vector3(33,1.6f,13.5f),new Vector3(72.5f,1.6f,25),new Vector3(89,3.6f,64),
                new Vector3(119,1.6f,47),new Vector3(146,1.6f,8),new Vector3(157,1.6f,48),
                new Vector3(171,1.6f,80),new Vector3(192,1.6f,67),new Vector3(214,1.6f,51.25f)
            };
            var connections = from.Select((a, i) => {
                var direction = to[i] - a;
                bool blocked = Physics.SphereCast(a, .3f, direction.normalized, out var hit, direction.magnitude,
                    ~0, QueryTriggerInteraction.Ignore);
                if (blocked) errors.Add("연결 " + (i + 1) + " 막힘: " + hit.collider.name);
                return new { index = i + 1, blocked, blocker = blocked ? hit.collider.name : null };
            }).ToArray();

            Vector3[] floors = b ? new[] {
                new Vector3(17.5f,0,40),new Vector3(30,0,62),new Vector3(69,0,68),new Vector3(74,0,35),
                new Vector3(91,0,29),new Vector3(108.5f,4,25.5f),new Vector3(126,4,23),new Vector3(132,0,60),
                new Vector3(155,0,63),new Vector3(185,0,42),new Vector3(190,0,25)
            } : new[] {
                new Vector3(48,0,13.5f),new Vector3(77,0,29),new Vector3(68,2,47),new Vector3(76,2,64),
                new Vector3(100,0,61),new Vector3(119,0,46),new Vector3(119,0,8),new Vector3(154,0,24),
                new Vector3(157,0,60),new Vector3(185,0,80),new Vector3(192,0,61)
            };
            var heights = floors.Select(p => {
                var hits = Physics.RaycastAll(new Vector3(p.x, 12, p.z), Vector3.down, 20, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.collider.name == "SM_ConcreteFloor").OrderBy(h => h.distance).ToArray();
                bool ok = hits.Length > 0 && Mathf.Abs(hits[0].point.y - p.y) < .08f;
                if (!ok) errors.Add("바닥 높이/참조 불일치: " + p);
                return new { point = p.ToString(), ok, actualY = hits.Length > 0 ? (float?)hits[0].point.y : null };
            }).ToArray();
            Vector3[] gaps = b ? new[] {
                new Vector3(49,12,64),new Vector3(100,12,23),new Vector3(116,12,23)
            } : new[] {
                new Vector3(72,12,39),new Vector3(76,12,55),new Vector3(123,12,24)
            };
            int filledGaps = gaps.Count(p => Physics.Raycast(p, Vector3.down, 20, ~0, QueryTriggerInteraction.Ignore));
            if (filledGaps != 0) errors.Add("단절부에 원치 않는 바닥/장애물 있음: " + filledGaps);
            int missing = background.GetComponentsInChildren<Transform>(true)
                .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            int badMaterials = background.GetComponentsInChildren<Renderer>(true)
                .Count(r => r.enabled && r.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader"));
            if (missing != 0 || badMaterials != 0) errors.Add("누락 스크립트/재질 참조");
            if (scene.isDirty) errors.Add("씬 미저장");
            var camera = GameObject.Find("Main Camera");
            if (background.GetComponent<CurtainCall.EscapeRoute.EscapeRouteReviewLabels>() == null) errors.Add("검토 라벨 숨김 연결 없음");
            return new { scene = scene.path, ok = errors.Count == 0, connections, heights,
                gapChecks = gaps.Length, filledGaps, missingScripts = missing, badMaterials, errors = errors.ToArray() };
        }
    }
}
