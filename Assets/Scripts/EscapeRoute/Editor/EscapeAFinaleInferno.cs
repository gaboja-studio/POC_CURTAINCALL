using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        /// <summary>One-time addition to the saved A map; preserves all existing traversal and external prefabs.</summary>
        public static string AddAFinaleInferno()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Saved A edit scene required");
            var bg = GameObject.Find(RootName).transform;
            if (bg.Find("39_AFinaleInferno") != null || bg.Find("40_AExitCableSaw") != null)
                throw new InvalidOperationException("Finale already authored; edit instances instead of rebuilding");
            CompletionMaterials();
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("A sequential inferno and portable saw exit");
            var fireRoot = FinaleGroup(bg, "39_AFinaleInferno");
            BuildInferno(fireRoot);
            var exitRoot = FinaleGroup(bg, "40_AExitCableSaw");
            BuildExitSaw(exitRoot);
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = exitRoot.gameObject;
            Undo.CollapseUndoOperations(undo);
            return "A: 13 alternating wall eruptions, persistent fire, portable cable-cutting saw and physical exit shutter";
        }

        static Transform FinaleGroup(Transform bg, string name)
        {
            var t = VerticalChild(bg, name);
            t.localScale = new Vector3(1 / bg.lossyScale.x, 1 / bg.lossyScale.y, 1 / bg.lossyScale.z);
            return t;
        }

        static BoxCollider FinaleVolume(Transform p, string name, Vector3 center, Vector3 size)
        {
            var t = VerticalChild(p, name); t.localPosition = center;
            var box = Undo.AddComponent<BoxCollider>(t.gameObject); box.size = size; box.isTrigger = true;
            return box;
        }

        static void BuildInferno(Transform root)
        {
            var fire = Undo.AddComponent<EscapePursuitFire>(root.gameObject);
            var data = new SerializedObject(fire);
            data.FindProperty("trigger").objectReferenceValue = FinaleVolume(root, "최종 질주 진입 감지", new Vector3(152, 1, 78), new Vector3(24, 4, 14));
            data.FindProperty("startDelay").floatValue = 20;
            data.FindProperty("stepSeconds").floatValue = 12;
            data.FindProperty("warnSeconds").floatValue = 3;
            data.FindProperty("burstSeconds").floatValue = 3;
            data.FindProperty("safeExitX").floatValue = 400;
            var bays = data.FindProperty("bays"); bays.arraySize = 13;
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(MidFlamePrefab);
            var roar = FinaleRoar();
            for (int i = 0; i < 13; i++)
            {
                float x = 150 + 20 * i; bool south = i % 2 == 0;
                float wall = south ? 57.2f : 98.8f;
                var bay = VerticalChild(root, $"{i + 1:00}_{(south ? "South" : "North")}_Inferno_{x - 10:0}_{x + 10:0}");
                var entry = bays.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("coverage").objectReferenceValue = FinaleVolume(bay, "점화 후 영구 화염 범위", new Vector3(x, 3, 78), new Vector3(20, 102, 42));
                var effects = new List<ParticleSystem>();
                for (int row = 0; row < 7; row++) for (int col = 0; col < 2; col++)
                {
                    float y = -42 + row * 15;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(original, bay);
                    Undo.RegisterCreatedObjectUndo(go, "Reuse corridor flame at facility scale");
                    go.name = $"기존 거대 화염_{row}_{col}";
                    go.transform.localPosition = new Vector3(x + (col == 0 ? -4 : 4), y, wall);
                    go.transform.localRotation = Quaternion.Euler(0, south ? 0 : 180, 0);
                    var ps = go.GetComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = ps.main; main.loop = true; main.prewarm = false; main.playOnAwake = false;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                    main.startSpeed = 36; main.startLifetime = 1.2f; main.gravityModifier = 0;
                    main.startSize = new ParticleSystem.MinMaxCurve(8, 10); main.maxParticles = 96;
                    var emission = ps.emission; emission.enabled = false; emission.rateOverTime = 18;
                    emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
                    var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(2, 11, .1f); shape.position = Vector3.zero; shape.rotation = Vector3.zero;
                    shape.randomDirectionAmount = 0; shape.sphericalDirectionAmount = 0;
                    var velocity = ps.velocityOverLifetime; velocity.enabled = false;
                    var limit = ps.limitVelocityOverLifetime; limit.enabled = false;
                    var force = ps.forceOverLifetime; force.enabled = false;
                    var noise = ps.noise; noise.enabled = false;
                    var collision = ps.collision; collision.enabled = false;
                    var trails = ps.trails; trails.enabled = false;
                    var size = ps.sizeOverLifetime; size.enabled = true;
                    size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .55f), new Keyframe(.2f, 1), new Keyframe(1, .65f)));
                    InfernoColor(ps);
                    ps.useAutoRandomSeed = false; ps.randomSeed = (uint)(2700 + i * 100 + row * 7 + col);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(ps);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                    effects.Add(ps);
                }
                SetFinaleReferences(entry.FindPropertyRelative("effects"), effects.ToArray());
                var lamps = new List<Light>();
                foreach (float z in new[] { 65f, 91f })
                {
                    var lamp = Undo.AddComponent<Light>(VerticalChild(bay, "예고 및 폭발 반사광").gameObject);
                    lamp.transform.localPosition = new Vector3(x, 5, z); lamp.type = LightType.Point;
                    lamp.color = new Color(1, .22f, .035f); lamp.range = 28; lamp.intensity = 1800;
                    lamp.shadows = LightShadows.None; lamp.enabled = false; lamps.Add(lamp);
                }
                SetFinaleReferences(entry.FindPropertyRelative("lights"), lamps.ToArray());
                var source = Undo.AddComponent<AudioSource>(VerticalChild(bay, "분사 폭발음").gameObject);
                source.transform.localPosition = new Vector3(x, 2, wall);
                source.clip = roar; source.playOnAwake = false; source.spatialBlend = 1;
                source.minDistance = 18; source.maxDistance = 180; source.rolloffMode = AudioRolloffMode.Linear;
                source.dopplerLevel = 0; source.volume = .8f;
                entry.FindPropertyRelative("burstAudio").objectReferenceValue = source;
                // Permanent machinery sits inside the wall recess, clear of the original walking/rope volumes.
                BMass(bay, "벽체 매립 대형 분사함", new Vector3(x, 4, wall + (south ? -.15f : .15f)), new Vector3(17, 10, .5f), bRecess, false);
                foreach (float dx in new[] { -5f, 5f })
                {
                    BMass(bay, "화염 분사구", new Vector3(x + dx, 4, wall + (south ? .3f : -.3f)), new Vector3(3, 7, .2f), bSteel, false);
                    foreach (float dy in new[] { -2f, 0, 2f })
                        BMass(bay, "분사구 내열 수평살", new Vector3(x + dx, 4 + dy, wall + (south ? .45f : -.45f)), new Vector3(3.4f, .18f, .2f), wornSteel, false);
                }
            }
            data.ApplyModifiedProperties();
        }

        static void BuildExitSaw(Transform root)
        {
            var saw = Undo.AddComponent<EscapeExitSaw>(root.gameObject);
            var data = new SerializedObject(saw);
            var device = VerticalChild(root, "휴대 절단기");
            RevealAsset(device, "기존 절단 설비 본체", Industrial + "SM_Machinery_03_Base.prefab", new Vector3(0, .18f, 0), new Vector3(.92f, .9f, 1.05f), maintenancePaint);
            BMass(device, "구동 모터", new Vector3(0, 1.05f, .28f), new Vector3(.62f, .46f, .65f), wornSteel, false);
            BMass(device, "톱날 상부 안전 덮개", new Vector3(0, 1.91f, .65f), new Vector3(1.18f, .16f, .28f), maintenancePaint, false);
            var blade = VerticalChild(device, "회전 톱날"); blade.localPosition = new Vector3(0, 1.45f, .65f);
            RevealAsset(blade, "원본 톱날", "Assets/Resources/Prefabs/Objects/ETCs/HumanCannonArtKit/CannonSawDiscVisual.prefab", new Vector3(0, -.55f, 0), new Vector3(1.1f, 1.1f, .08f), null);
            foreach (var behaviour in blade.GetComponentsInChildren<MonoBehaviour>()) Undo.DestroyObjectImmediate(behaviour);
            var handle = VerticalChild(device, "원본 운반 방식 손잡이"); handle.localPosition = new Vector3(0, 1.05f, -1.1f);
            VerticalCable(device, "운반 가로 손잡이", new Vector3(-.42f, 1.05f, -1.1f), new Vector3(.42f, 1.05f, -1.1f), .065f, bSteel);
            foreach (float side in new[] { -.4f, .4f })
                VerticalCable(device, "운반 손잡이 지지대", new Vector3(side, 1.05f, -1.1f), new Vector3(side, .7f, -.35f), .05f, wornSteel);
            var load = Undo.AddComponent<BoxCollider>(device.gameObject);
            load.center = new Vector3(0, .78f, 0); load.size = new Vector3(1.05f, 1.5f, 1.15f);
            device.SetPositionAndRotation(new Vector3(379, 0, 78), Quaternion.Euler(0, 90, 0));
            var recovery = VerticalChild(root, "절단기 복귀 위치"); recovery.SetPositionAndRotation(device.position, device.rotation);

            var door = VerticalChild(root, "배선 잠금 출구 셔터"); door.localPosition = new Vector3(390, 0, 78);
            // Shutter closes the existing 14m opening. Existing concrete lintel prevents jumping over it.
            BMass(door, "셔터 충돌판", new Vector3(0, 3.25f, 0), new Vector3(.38f, 6.5f, 14), wornSteel);
            for (int i = 0; i < 13; i++)
                BMass(door, "산업용 셔터 가로 리브", new Vector3(-.26f, .25f + i * .5f, 0), new Vector3(.15f, .1f, 14), bSteel, false);
            foreach (float z in new[] { 71.15f, 84.85f })
                BMass(root, "기존 개구부 셔터 가이드", new Vector3(389.7f, 3.25f, z), new Vector3(.65f, 6.5f, .3f), maintenancePaint);
            // The right-hand wall is at z85; this panel and exposed loop face the approaching crew.
            RevealAsset(root, "출구 잠금 배선 제어반", Industrial + "SM_Control_Panel.prefab", new Vector3(388.65f, .7f, 85.6f), new Vector3(.45f, 1.65f, 1.1f), wornSteel, Quaternion.Euler(0, -90, 0));
            var socket = VerticalChild(root, "절단할 노출 배선"); socket.localPosition = new Vector3(388.6f, 1.45f, 84);
            var intact = VerticalChild(socket, "연결된 노란 전력선");
            VerticalCable(intact, "절단 지점", new Vector3(0, -.52f, 0), new Vector3(0, .52f, 0), .13f, maintenancePaint);
            var severed = VerticalChild(socket, "끊어진 전력선");
            VerticalCable(severed, "위쪽 절단면", new Vector3(0, .2f, 0), new Vector3(0, .52f, 0), .13f, maintenancePaint);
            VerticalCable(severed, "아래로 처진 전력선", new Vector3(0, -.52f, 0), new Vector3(-.18f, -.18f, -.08f), .13f, maintenancePaint);
            severed.gameObject.SetActive(false);
            VerticalCable(root, "배선 제어반 연결", socket.position + Vector3.up * .52f, new Vector3(388.6f, 2.5f, 85.4f), .13f, maintenancePaint);
            VerticalCable(root, "하부 배선 연결", socket.position - Vector3.up * .52f, new Vector3(388.6f, .6f, 85.4f), .13f, maintenancePaint);
            var lamp = CoopLamp(root, "출구 잠금 및 절단 상태", new Vector3(388.3f, 2.65f, 84.25f));
            BLight(root, new Vector3(385, 5, 82), new Vector3(388.5f, 1, 84), 420, 12);
            var sparks = FinaleSparks(socket);
            foreach (var pair in new (string, UnityEngine.Object)[] { ("carryRoot", device), ("handle", handle), ("blade", blade), ("loadCollider", load),
                ("cableSocket", socket), ("doorPanel", door), ("recoveryAnchor", recovery), ("intactCable", intact.gameObject),
                ("severedCable", severed.gameObject), ("indicator", lamp), ("sparks", sparks) })
                data.FindProperty(pair.Item1).objectReferenceValue = pair.Item2;
            data.FindProperty("carryHandleHeight").floatValue = 1.05f;
            data.FindProperty("carryMoveSpeed").floatValue = 3;
            data.FindProperty("bladeRadius").floatValue = .55f;
            data.FindProperty("bladeHalfThickness").floatValue = .12f;
            data.FindProperty("cableRadius").floatValue = .08f;
            data.FindProperty("cutSeconds").floatValue = 3;
            data.FindProperty("doorLift").floatValue = 7;
            data.FindProperty("doorSpeed").floatValue = 5;
            data.ApplyModifiedProperties();
        }

        static ParticleSystem FinaleSparks(Transform socket)
        {
            var sparks = Undo.AddComponent<ParticleSystem>(VerticalChild(socket, "실제 접촉 절단 불꽃").gameObject);
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            sparks.transform.localRotation = Quaternion.Euler(0, -90, 0);
            var main = sparks.main; main.loop = true; main.playOnAwake = false; main.startLifetime = .3f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2, 5); main.startSize = .04f;
            main.startColor = new Color(1, .65f, .1f); main.gravityModifier = 1; main.maxParticles = 48;
            var emission = sparks.emission; emission.rateOverTime = 90;
            var shape = sparks.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 28; shape.radius = .03f;
            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Prefabs/Objects/ETCs/TightropeArtKit/SawSpark.mat");
            return sparks;
        }

        static void SetFinaleReferences(SerializedProperty array, UnityEngine.Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static void InfernoColor(ParticleSystem ps)
        {
            // The original blue/white two-gradient core saturates when enlarged into overlapping hall-sized meshes.
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(.8f, .24f, .008f), 0),
                new GradientColorKey(new Color(1, .34f, .012f), .2f), new GradientColorKey(new Color(.7f, .07f, .002f), .7f),
                new GradientColorKey(new Color(.18f, .008f, 0), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.38f, .12f), new GradientAlphaKey(.3f, .75f), new GradientAlphaKey(0, 1) });
            var color = ps.colorOverLifetime; color.enabled = true; color.color = new ParticleSystem.MinMaxGradient(gradient);
        }
    }
}
