using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapePursuitRemodel
    {
        public static void ApplyConveyorJunction()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("저장된 복사 씬의 편집 모드가 필요합니다.");
            var route = Object.FindFirstObjectByType<EscapePursuerRoute>();
            module = route.transform;
            frame = module.GetComponentsInParent<Transform>().Single(t => t.name == "EscapeCampaignFrame");
            var feedback = frame.Find(FeedbackRoot);
            if (feedback.Find("Conveyor wire junction")) return;
            concrete = feedback.Find("Switchback central concrete divider").GetComponent<MeshRenderer>().sharedMaterial;
            var metal = frame.GetComponentsInChildren<MeshRenderer>(true)
                .First(r => r.name == "작업등 함체").sharedMaterial;
            var rope = frame.GetComponentsInChildren<BoxCollider>(true).Single(c => c.name == "A04B_OneWaySwitchWire");
            structure = new GameObject("Conveyor wire junction").transform;
            structure.SetParent(feedback, false);
            GuideOntoWire(rope, metal);
            FinishBreachPresentation(module.GetComponent<EscapePursuerWalls>(), metal);
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void GuideOntoWire(BoxCollider rope, Material metal)
        {
            // Use the authored wire basis. Both rails finish at the actual capsule capture width,
            // while keeping the entire ten-metre wire and the drop below it.
            float lane = rope.size.x * .5f + .28f;
            var yellow = frame.GetComponentsInChildren<MeshRenderer>(true)
                .First(r => r.name == "노란 하차장 수신 발판").sharedMaterial;
            for (int side = -1; side <= 1; side += 2)
            {
                AddRail(side, new Vector2(2.05f, -2.9f), new Vector2(lane, -.65f));
                AddRail(side, new Vector2(lane, -.65f), new Vector2(lane, -.02f));
                // The rest of the ledge still overlooks the abyss, with a single deliberate mouth.
                AddRail(side, new Vector2(lane, -.06f), new Vector2(3.98f, -.06f));
            }
            var mark = Box("Wire entry floor marking", Local(rope.transform) + Vector3.up * .014f -
                frame.InverseTransformDirection(rope.transform.forward) * 1.35f, new(.95f, .016f, .36f));
            mark.rotation = rope.transform.rotation;
            mark.GetComponent<MeshRenderer>().sharedMaterial = yellow;
            mark.GetComponent<Collider>().enabled = false;

            void AddRail(int side, Vector2 start, Vector2 end)
            {
                Vector3 a = rope.transform.TransformPoint(new Vector3(side * start.x, 0, start.y));
                Vector3 b = rope.transform.TransformPoint(new Vector3(side * end.x, 0, end.y));
                Vector3 direction = b - a;
                var block = Box("Wire entry steel guide", frame.InverseTransformPoint((a + b) * .5f + Vector3.up * .52f),
                    new Vector3(.14f, 1.04f, direction.magnitude + .05f));
                block.rotation = Quaternion.LookRotation(direction);
                block.GetComponent<MeshRenderer>().sharedMaterial = metal;
            }
        }

        static void FinishBreachPresentation(EscapePursuerWalls walls, Material metal)
        {
            var chase = module.GetComponentInChildren<EscapeUnderstageChase>(true);
            var encounters = new SerializedObject(chase).FindProperty("encounters");
            var impact = new GameObject("Directional wall impact").AddComponent<AudioSource>();
            impact.transform.SetParent(structure, false);
            impact.playOnAwake = false;
            impact.spatialBlend = 1;
            impact.minDistance = 8;
            impact.maxDistance = 52;
            impact.rolloffMode = AudioRolloffMode.Linear;
            impact.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/UnderstageConcreteAndSteps.wav");
            var chaseData = new SerializedObject(chase);
            chaseData.FindProperty("wallImpact").objectReferenceValue = impact;
            chaseData.ApplyModifiedProperties();
            foreach (var beacon in module.GetComponentsInChildren<EscapePursuitBeacon>(true))
            {
                var data = new SerializedObject(beacon);
                int index = data.FindProperty("gate").intValue;
                var gate = walls.At(index);
                if (gate == null) continue;
                Vector3 face = gate.panel.forward;
                var row = Enumerable.Range(0, encounters.arraySize).Select(encounters.GetArrayElementAtIndex)
                    .Single(e => e.FindPropertyRelative("gate").intValue == index);
                var landing = (Transform)row.FindPropertyRelative("landing").objectReferenceValue;
                var light = new GameObject("Breach deck worklight " + index).AddComponent<Light>();
                light.transform.SetParent(structure, false);
                Vector3 position = Vector3.Lerp(gate.panel.position - Vector3.up * 1.2f,
                    landing.position + Vector3.up * 9f, .55f) + gate.panel.right * 2.8f;
                light.transform.SetPositionAndRotation(position,
                    Quaternion.LookRotation(landing.position + Vector3.up * 2.5f - position));
                light.type = LightType.Spot;
                light.color = new Color(1f, .77f, .48f);
                light.spotAngle = 103;
                light.innerSpotAngle = 52;
                light.range = 36;
                light.shadows = LightShadows.Soft;
                light.enabled = false;
                SetLocalShadowTier(light, 0);
                data.FindProperty("emergenceLight").objectReferenceValue = light;
                data.ApplyModifiedProperties();
                // The collision sits inside the breached wall, before any part of the
                // service ledge. Ignore Raycast keeps arms, sight and route sweeps independent.
                var restrict = new GameObject("Personnel safety boundary " + index, typeof(BoxCollider));
                restrict.transform.SetParent(structure, false);
                restrict.layer = LayerMask.NameToLayer("Ignore Raycast");
                restrict.transform.SetPositionAndRotation(gate.panel.position - face * .35f, gate.panel.rotation);
                Vector3 size = gate.barrier.size;
                Vector3 scale = gate.panel.lossyScale;
                restrict.GetComponent<BoxCollider>().size = new Vector3(size.x * scale.x + 1f, size.y * scale.y + .5f, .28f);
                if (index == 3 || index == 4)
                {
                    var sill = Box("Service threshold striped beam " + index,
                        frame.InverseTransformPoint(gate.panel.position - face * .35f - Vector3.up * 6.86f),
                        new Vector3(size.x * scale.x, .12f, .24f));
                    sill.rotation = gate.panel.rotation;
                    sill.GetComponent<Collider>().enabled = false;
                    sill.GetComponent<MeshRenderer>().sharedMaterial = metal;
                }
            }
        }
    }
}
