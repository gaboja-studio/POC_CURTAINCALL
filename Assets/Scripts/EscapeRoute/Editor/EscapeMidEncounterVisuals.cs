using System;
using System.Linq;
using CurtainCall.HumanCannon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string MidFlamePrefab = "Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/FX/FX_Flame_Thrower_01.prefab";

        public static void FinishMidRouteVisuals()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || Application.isPlaying) throw new InvalidOperationException("현재 A 편집 씬이 필요합니다.");
            var root = GameObject.Find(RootName).transform.Find(MidEncounterGroup);
            if (root == null || root.Find("VisualsFinished") != null) throw new InvalidOperationException("미적용 중반 기믹이 필요합니다.");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(MidFlamePrefab);
            if (asset == null) throw new InvalidOperationException("기존 화염방사 이펙트를 찾지 못했습니다.");
            midWarning = RunnerMaterial("solo-danger-red");
            foreach (var hazard in root.GetComponentsInChildren<HumanCannonHazard>())
            {
                var cue = hazard.GetComponent<HumanCannonHazardCue>();
                var old = cue.warning.Find("작동 전 점멸 범위");
                if (old != null)
                {
                    var bounds = old.GetComponent<Renderer>().bounds;
                    MidWarningLines(cue.warning, bounds.center, bounds.size);
                    Undo.DestroyObjectImmediate(old.gameObject);
                }
                if (hazard.Kind != CannonHazardKind.FireJet) continue;
                var view = cue.visual;
                foreach (Transform child in view.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
                Undo.RecordObject(view, "Replace prototype volumes with existing flame VFX");
                view.localScale = Vector3.one;
                bool left = cue.origin.x < 122.5f;
                for (int row = 0; row < 2; row++) for (int nozzle = 0; nozzle < 3; nozzle++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, view);
                    Undo.RegisterCreatedObjectUndo(instance, "Existing flamethrower visual");
                    instance.name = "ExistingFlameFX_" + row + "_" + nozzle;
                    instance.transform.localPosition = new Vector3(left ? -1.17f : 1.17f, row == 0 ? -.6f : .6f, (nozzle - 1) * 1.15f);
                    instance.transform.localRotation = Quaternion.Euler(0, left ? 90 : -90, 0);
                    instance.transform.localScale = Vector3.one;
                    var ps = instance.GetComponent<ParticleSystem>();
                    Undo.RecordObject(ps, "Fit flames to authored contact envelope");
                    var main = ps.main;
                    main.loop = true; main.prewarm = false; main.playOnAwake = true;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.startLifetime = .33f; main.startSpeed = 6f;
                    main.startSize = new ParticleSystem.MinMaxCurve(.65f, .95f); main.maxParticles = 64;
                    var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 4; shape.radius = .06f;
                    var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 70; emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
                    var velocity = ps.velocityOverLifetime; velocity.enabled = false;
                    var force = ps.forceOverLifetime; force.enabled = false;
                    var noise = ps.noise; noise.enabled = false;
                    var collision = ps.collision; collision.enabled = false;
                    var trails = ps.trails; trails.enabled = false;
                    var size = ps.sizeOverLifetime; size.enabled = true;
                    size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .45f), new Keyframe(.4f, 1), new Keyframe(1, .55f)));
                    ps.useAutoRandomSeed = false; ps.randomSeed = (uint)(137 + row * 13 + nozzle);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(ps);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                }
                var lightObject = new GameObject("ActiveFlameLight", typeof(Light));
                Undo.RegisterCreatedObjectUndo(lightObject, "Flame spill light");
                lightObject.transform.SetParent(view, false);
                var light = lightObject.GetComponent<Light>(); light.type = LightType.Point;
                light.color = new Color(1, .38f, .08f); light.intensity = 100; light.range = 5; light.shadows = LightShadows.None;
            }
            ContextSign(root, "앉아서 통과", "LOW CLEARANCE / CROUCH", new Vector3(110, -19.9f, -36.04f), 180, 5);
            ContextSign(root, "대기 포켓 표식", "SERVICE REFUGE", new Vector3(62.7f, -19.8f, -20.77f), 0, 3);
            VerticalChild(root, "VisualsFinished");
            AssetDatabase.SaveAssetIfDirty(font);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void MidWarningLines(Transform p, Vector3 at, Vector3 size)
        {
            foreach (float sign in new[] { -1f, 1f })
            {
                BMass(p, "작동 전 점멸 측선", at + Vector3.right * sign * size.x * .5f, new Vector3(.12f, .02f, size.z), midWarning, false);
                BMass(p, "작동 전 점멸 진입선", at + Vector3.forward * sign * size.z * .5f, new Vector3(size.x, .02f, .12f), midWarning, false);
            }
            for (int i = -1; i <= 1; i++)
                BMass(p, "작동 전 점멸 내부 표식", at + Vector3.right * i * size.x * .25f, new Vector3(.15f, .02f, Mathf.Min(.8f, size.z)), midWarning, false);
        }
    }
}
