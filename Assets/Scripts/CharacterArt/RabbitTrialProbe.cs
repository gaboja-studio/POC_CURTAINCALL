using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CurtainCall.Player;
using Newtonsoft.Json;
using UnityEngine;

namespace CurtainCall.CharacterArt
{
    /// <summary>별도 시험 씬에서만 실행하는 절단/복구/물리 검사. 실제 Player와 네트워크 검사는 별도다.</summary>
    public sealed class RabbitTrialProbe : MonoBehaviour
    {
        public GameObject Candidate;
        public string ReportPath;
        public bool Finished { get; private set; }
        public bool Passed { get; private set; }
        readonly List<object> results = new();
        GameObject instance;
        string failure;

#if UNITY_EDITOR
        IEnumerator Start()
        {
            var run = Run();
            while (true)
            {
                bool next;
                try { next = run.MoveNext(); }
                catch (Exception e) { failure = e.ToString(); break; }
                if (!next) break;
                yield return run.Current;
            }
            (run as IDisposable)?.Dispose();
            if (instance != null)
            {
                instance.GetComponent<HumanoidModel>().ClearPieces();
                Destroy(instance);
            }
            Passed = failure == null;
            Finished = true;
            File.WriteAllText(ReportPath, JsonConvert.SerializeObject(new
            {
                passed = Passed, failure, results,
                scope = "독립 PlayMode 절단·복구·재활성·물리 시험. 실제 Player/온라인/최종 외형 승인은 제외."
            }, Formatting.Indented));
            Debug.Log("토끼 독립 시험: " + (Passed ? "PASS" : "FAIL " + failure));
        }

        IEnumerator Run()
        {
            instance = Instantiate(Candidate, transform);
            var model = instance.GetComponent<HumanoidModel>();
            model.Apply(PlayerCondition.PartsForLostCount(0), PlayerCondition.PartsForLostCount(0), false, null);
            model.SetColor(Color.white);
            Require(model.Original.sharedMaterials.All(m => m.GetTexture("_BaseMap") != null), "기본 Tint 고유색 보존");
            model.Play("Walk");
            yield return null;
            for (int stage = 1; stage <= 4; stage++)
            {
                model.Apply(PlayerCondition.PartsForLostCount(stage - 1), PlayerCondition.PartsForLostCount(stage), true, null);
                Require(model.Stage == stage && model.PieceCount == stage, "절단 단계/조각 개수");
                var rb = model.GetPieceBody(stage - 1);
                Require(rb != null && !rb.isKinematic, "분리 물리 활성화");
                var root = rb.transform.parent;
                var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
                Require(skin.bones.All(b => b.IsChildOf(root) && !model.Original.bones.Contains(b)), "분리 본 독립성");
                Require(skin.sharedMaterials.SequenceEqual(model.Limbs[stage - 1].sharedMaterials), "분리 재질 보존");
                var start = rb.position;
                for (int tick = 0; tick < 12; tick++) yield return new WaitForFixedUpdate();
                Require(Finite(rb.position) && Vector3.Distance(start, rb.position) > .001f, "분리 조각 실제 이동");
                Require(root.GetComponentsInChildren<Rigidbody>().Length == 2 && root.GetComponentsInChildren<CharacterJoint>().Length == 1, "조각 관절 개수");
                results.Add(new { test = "sever", stage, pieces = model.PieceCount, distance = Vector3.Distance(start, rb.position) });
            }
            model.Apply(PlayerCondition.PartsForLostCount(4), PlayerCondition.PartsForLostCount(0), false, null);
            yield return null;
            Require(model.Stage == 0 && model.PieceCount == 0 && model.Original.enabled && model.Bodies.All(s => !s.enabled), "정상 복구");
            results.Add(new { test = "restore", passed = true });
            for (int stage = 0; stage <= 4; stage++)
            {
                instance.SetActive(false);
                instance.SetActive(true);
                model.Apply(PlayerCondition.PartsForLostCount(0), PlayerCondition.PartsForLostCount(stage), false, null);
                yield return null;
                Require(model.Stage == stage && model.PieceCount == 0, "재활성 신체 상태");
                Require((stage == 0 ? model.Original : model.Bodies[stage - 1]).enabled, "재활성 표시");
                results.Add(new { test = "reactivate", stage, passed = true });
            }
            // 새 인스턴스로 재시작/모델 재바인딩의 외형 초기 조건을 검사한다.
            Destroy(instance);
            yield return null;
            instance = Instantiate(Candidate, transform);
            model = instance.GetComponent<HumanoidModel>();
            model.Apply(PlayerCondition.PartsForLostCount(0), PlayerCondition.PartsForLostCount(0), false, null);
            model.Play("Idle");
            yield return null;
            Require(model.Stage == 0 && model.PieceCount == 0, "새 인스턴스 초기화");
            model.EnableRagdoll();
            var bodies = instance.GetComponentsInChildren<Rigidbody>();
            var joints = instance.GetComponentsInChildren<CharacterJoint>();
            Require(bodies.Length == 11 && joints.Length == 10, "전신 래그돌 구조");
            foreach (var body in bodies) body.isKinematic = false;
            for (int tick = 0; tick < 60; tick++) yield return new WaitForFixedUpdate();
            Require(bodies.All(b => Finite(b.position) && b.position.y > -.5f && b.position.magnitude < 10), "래그돌 유한/바닥 이탈");
            results.Add(new { test = "ragdoll", bodies = bodies.Length, joints = joints.Length, finite = true });
        }
#endif
        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        static void Require(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
        }
    }
}
