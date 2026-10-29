using System;
using System.Linq;
using CurtainCall.HumanCannon;
using CurtainCall.UI.SceneIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string CompletionGroup = "28_CompletedTraversal";
        const string OriginalCannonPath = "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity";

        static void CompletionMaterials()
        {
            bConcrete = RunnerMaterial("brutal-concrete"); bRecess = RunnerMaterial("brutal-recess");
            bSteel = RunnerMaterial("brutal-steel"); bBulb = RunnerMaterial("concrete-service-bulb");
            wornSteel = RunnerMaterial("working-steel"); wornPipe = RunnerMaterial("working-pipe");
            maintenancePaint = RunnerMaterial("working-faded-ochre"); floorWear = RunnerMaterial("working-floor-dirt");
        }

        public static void CompleteEscapePresentation()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit mode required");
            CompletionMaterials();
            var system = UnityEngine.Object.FindAnyObjectByType<HumanCannonSystem>();
            Ensure<InteractionSceneBinding>(system.gameObject);
            foreach (var hazard in UnityEngine.Object.FindObjectsByType<HumanCannonHazard>(FindObjectsSortMode.None))
            {
                var cue = hazard.GetComponent<HumanCannonHazardCue>();
                if (cue == null) continue;
                if (hazard.Kind == CannonHazardKind.GroundSaw)
                {
                    Vector3 travel = Vector3.ProjectOnPlane(cue.destination - cue.origin, Vector3.up).normalized;
                    if (travel.sqrMagnitude < .9f) continue;
                    // The original prefab's thin axis is local Z. Keep its edge in the travel plane.
                    Undo.RecordObject(hazard.transform, "Edge-first original saw and contact plane");
                    hazard.transform.rotation = Quaternion.LookRotation(Vector3.Cross(travel, Vector3.up));
                    Undo.RecordObject(cue.visual, "Match saw art to contact plane");
                    hazard.Present(0, 1);
                }
                if (hazard.Kind == CannonHazardKind.FireJet && cue.visual.GetComponentInChildren<ParticleSystem>(true) == null)
                    CompleteFlame(hazard, cue);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static void CompleteFlame(HumanCannonHazard hazard, HumanCannonHazardCue cue)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(MidFlamePrefab);
            if (asset == null) throw new InvalidOperationException("Existing flame asset missing");
            var view = cue.visual;
            foreach (var child in view.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
            Undo.RecordObject(view, "Replace prototype fire with original particle asset");
            view.SetPositionAndRotation(cue.origin, Quaternion.identity); view.localScale = Vector3.one;
            var size = new SerializedObject(hazard).FindProperty("boxSize").vector3Value;
            bool fromSouth = cue.origin.z < 78;
            for (int row = 0; row < 2; row++) for (int col = 0; col < 3; col++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, view);
                Undo.RegisterCreatedObjectUndo(go, "Reuse original flame VFX");
                go.name = "ExistingFlameFX_" + row + "_" + col;
                go.transform.localPosition = new Vector3((col - 1) * size.x * .32f, (row == 0 ? -.26f : .26f) * size.y,
                    (fromSouth ? -1 : 1) * (size.z * .5f - .18f));
                go.transform.localRotation = Quaternion.Euler(0, fromSouth ? 0 : 180, 0);
                var ps = go.GetComponent<ParticleSystem>(); var main = ps.main;
                main.loop = true; main.prewarm = false; main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.startLifetime = (size.z - .65f) / 8; main.startSpeed = 8;
                main.startSize = new ParticleSystem.MinMaxCurve(.65f, .95f); main.maxParticles = 80;
                var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 3; shape.radius = .06f;
                var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 70; emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
                var velocity = ps.velocityOverLifetime; velocity.enabled = false;
                var force = ps.forceOverLifetime; force.enabled = false;
                var noise = ps.noise; noise.enabled = false;
                var collision = ps.collision; collision.enabled = false;
                var trails = ps.trails; trails.enabled = false;
                ps.useAutoRandomSeed = false; ps.randomSeed = (uint)(157 + row * 13 + col);
                PrefabUtility.RecordPrefabInstancePropertyModifications(ps); PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            }
            var light = new GameObject("ActiveFlameLight", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(view, false); light.color = new Color(1, .38f, .08f);
            light.intensity = 100; light.range = 6; light.shadows = LightShadows.None;
            var mounting = VerticalChild(hazard.transform.parent, "PermanentFlameManifold");
            float z = cue.origin.z + (fromSouth ? -1 : 1) * (size.z * .5f + .35f);
            BMass(mounting, "설비 화염 분사함", new Vector3(cue.origin.x, cue.origin.y, z), new Vector3(size.x + .6f, size.y, .7f), bRecess);
            for (int i = -1; i <= 1; i++)
                BMass(mounting, "불꽃 분사구", new Vector3(cue.origin.x + i * size.x * .32f, cue.origin.y, z + (fromSouth ? .38f : -.38f)), new Vector3(.3f, 1.5f, .12f), bSteel, false);
        }

        // Clone the original bar mesh and original authored dynamics, including its actual interaction component.
        static HumanCannonPole OriginalPole(Transform parent, HumanCannonPole source, string name, Vector3 grip, Vector3 forward, float ceiling)
        {
            var go = UnityEngine.Object.Instantiate(source.gameObject, parent);
            Undo.RegisterCreatedObjectUndo(go, "Original human cannon swing"); go.name = name; go.SetActive(true);
            go.transform.SetPositionAndRotation(grip, Quaternion.LookRotation(forward)); go.transform.localScale = Vector3.one;
            var pole = go.GetComponent<HumanCannonPole>(); var data = new SerializedObject(pole);
            pole.Grip.SetPositionAndRotation(grip, Quaternion.LookRotation(forward));
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            for (int i = 0; i < 2; i++)
            {
                string prefix = i == 0 ? "left" : "right";
                var anchor = ChildAt(go.transform, prefix + "Anchor", new Vector3(grip.x, ceiling, grip.z) + side * (i == 0 ? -1 : 1), Quaternion.identity);
                var sourceCable = new SerializedObject(source).FindProperty(prefix + "Cable").objectReferenceValue as Transform;
                var cable = UnityEngine.Object.Instantiate(sourceCable.gameObject, go.transform).transform;
                cable.name = prefix + "Cable"; cable.localScale = new Vector3(.065f, 1, .065f);
                data.FindProperty(prefix + "Anchor").objectReferenceValue = anchor;
                data.FindProperty(prefix + "Cable").objectReferenceValue = cable;
            }
            data.FindProperty("linkedGate").objectReferenceValue = null; data.ApplyModifiedProperties();
            pole.enabled = true; pole.PresentSwing(0);
            BMass(parent, name + " 천장 매립 하중보", new Vector3(grip.x, ceiling + .6f, grip.z),
                new Vector3(Mathf.Abs(side.x) * 4 + 1, 1.2f, Mathf.Abs(side.z) * 4 + 1), bConcrete);
            return pole;
        }

        static void RegisterAllPoles()
        {
            var system = UnityEngine.Object.FindAnyObjectByType<HumanCannonPoleSystem>();
            var poles = UnityEngine.Object.FindObjectsByType<HumanCannonPole>(FindObjectsSortMode.None)
                .Where(p => p.gameObject.scene == system.gameObject.scene && p.enabled).OrderBy(p => p.name, StringComparer.Ordinal).ToArray();
            var data = new SerializedObject(system); SetFinaleArray(data, "poles", poles); data.FindProperty("allowPlatformJumpGrab").boolValue = true;
            data.ApplyModifiedProperties();
        }
    }
}
