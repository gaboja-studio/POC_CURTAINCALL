using System;
using System.Linq;
using CurtainCall.HumanCannon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    /// <summary>Reviewed A-only changes. Resumable without regenerating the whole layout.</summary>
    public static class EscapeFeedbackPolish
    {
        const string StepName = "하차장 낮은 단차 1";
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/Levels/escape_route.unity")
                throw new InvalidOperationException("Open saved A in Edit Mode.");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            bool hasSteps = all.Any(t => t.name == StepName);
            var lower = all.Single(t => t.name == "노란 하차장 수신 발판");
            var upper = all.Single(t => t.name == "한 곳으로 모이는 하차장");
            var low = lower.GetComponent<BoxCollider>().bounds;
            var high = upper.GetComponent<BoxCollider>().bounds;
            if (Mathf.Abs(high.max.y - low.max.y - .7f) > .01f || !hasSteps && Mathf.Abs(high.min.z - low.max.z) > .01f)
                throw new InvalidOperationException("Landing seam changed; review again.");
            if (!hasSteps)
            {
                EscapeStaticGeometryBatch.Restore();
                // Split the existing footprint; no extra catch floor below the conveyor.
                for (int i = 0; i < 2; i++)
                {
                    var step = UnityEngine.Object.Instantiate(lower.gameObject, lower.parent).transform;
                    step.name = "하차장 낮은 단차 " + (i + 1);
                    float height = low.size.y + .7f * (i + 1) / 3f;
                    var scale = step.localScale;
                    scale.y *= height / low.size.y; scale.z *= .6f / low.size.z; step.localScale = scale;
                    step.position = new Vector3(low.center.x, low.min.y + height * .5f, low.max.z - .9f + i * .6f);
                }
                var lowerScale = lower.localScale; lowerScale.z *= (low.size.z - 1.2f) / low.size.z; lower.localScale = lowerScale;
                lower.position = low.center - Vector3.forward * .6f;
            }

            all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var locks = all.Select(t => t.GetComponent<EscapeCooperativeLocks>()).Single(x => x != null);
            var serialized = new SerializedObject(locks);
            var checkpoints = serialized.FindProperty("checkpoints");
            MoveControlFrame(checkpoints.GetArrayElementAtIndex(0), new Vector3(-243.1f, -29.9f, 192.300018f));
            MoveControlFrame(checkpoints.GetArrayElementAtIndex(2), new Vector3(-368f, -11.4f, 40.30002f));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // Only the first cannon has one prescribed receiving wire. Other stations retain steering.
            var station = all.Select(t => t.GetComponent<HumanCannonStation>()).Single(x => x != null && x.name == "03_Cannon");
            var cannon = new SerializedObject(station); cannon.FindProperty("airControlSpeed").floatValue = 0;
            cannon.ApplyModifiedPropertiesWithoutUndo();
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void MoveControlFrame(SerializedProperty checkpoint, Vector3 position)
        {
            var control = (Transform)checkpoint.FindPropertyRelative("shoulderControl").objectReferenceValue;
            Vector3 offset = position - control.position;
            control.position += offset;
            var indicator = checkpoint.FindPropertyRelative("indicator").objectReferenceValue as Renderer;
            if (indicator != null) indicator.transform.position += offset;
            foreach (Transform item in control.parent)
            {
                if (item.name == "정비 권상기 지지기둥" || item.name == "기존 크레인 횡보" || item.name == "권상틀 하부 교각")
                    item.position += new Vector3(offset.x, 0, offset.z);
                if (item.name != "상부 레버 권상선") continue;
                item.position += new Vector3(offset.x, offset.y * .5f, offset.z);
                var scale = item.localScale; scale.y -= offset.y / Mathf.Abs(item.parent.lossyScale.y); item.localScale = scale;
            }
            checkpoint.FindPropertyRelative("instruction").stringValue = "동료의 목마를 타고 높은 레버에 F → 통로 가동";
        }
    }
}
