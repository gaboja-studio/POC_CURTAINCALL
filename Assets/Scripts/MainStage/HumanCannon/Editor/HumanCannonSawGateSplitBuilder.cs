using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.HumanCannon.EditorTools
{
    /// <summary>
    /// 두 협동 관문의 톱벽을 가운데에서 좌우 절반으로 나눈다. 톱 44장·철망·충돌 벽을 반씩 묶고,
    /// 연결 봉을 잡으면 트램펄린 폭 + 좌우 1m 틈이 열리도록 관문에 묶음을 연결한다.
    /// </summary>
    public static class HumanCannonSawGateSplitBuilder
    {
        // 2026-11-03 담당자 요청으로 좌우 0.5m → 1m.
        const float SideMargin = 1f;

        [MenuItem("Tools/CurtainCall/인간 대포/톱벽 좌우 분리 적용")]
        public static void ApplyToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 HumanCannon 전용 씬에서 실행하세요.");
            var roots = scene.GetRootGameObjects();
            var gates = roots.SelectMany(root => root.GetComponentsInChildren<HumanCannonSawGate>(true)).ToArray();
            if (gates.Length != 2) throw new InvalidOperationException("협동 관문 두 개가 필요합니다.");
            // 이미 나눈 관문은 다시 나누지 않는다.
            if (gates.All(gate => gate.transform.Find("SawVisual/LeftBlades") != null)) return;
            var trampoline = roots.SelectMany(root => root.GetComponentsInChildren<HumanCannonTrampoline>(true)).FirstOrDefault()
                ?? throw new InvalidOperationException("트램펄린 폭을 잴 HumanCannonTrampoline이 필요합니다.");
            float width = trampoline.GetComponentsInChildren<Renderer>(true).Max(r => Mathf.Max(r.bounds.size.x, r.bounds.size.z));
            foreach (var gate in gates) Validate(gate);
            foreach (var gate in gates) Split(gate, width + SideMargin * 2f);
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>
        /// 톱벽 위를 가로지르는 관문 틀 파이프(TopBeam)를 철망 조각과 같은 구간으로 잘라 각 철망 묶음에 붙인다.
        /// 열린 틈에 파이프가 남지 않고, 바닥 밖 끝 조각은 철망과 함께 떨어진다.
        /// </summary>
        [MenuItem("Tools/CurtainCall/인간 대포/톱벽 위 파이프 분리 적용")]
        public static void SplitTopBeamInOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 HumanCannon 전용 씬에서 실행하세요.");
            var gates = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HumanCannonSawGate>(true)).ToArray();
            foreach (var gate in gates)
            {
                var beam = gate.transform.Find("AppliedGateFrame/TopBeam");
                var mesh = gate.transform.Find("Barrier/MetalMesh");
                if (beam == null) continue; // 이미 나눈 관문.
                if (mesh?.Find("LeftFence") == null || mesh.Find("RightFence") == null)
                    throw new InvalidOperationException("톱벽 좌우 분리를 먼저 적용하세요.");
                var whole = Extent(beam);
                foreach (var side in new[] { mesh.Find("LeftFence"), mesh.Find("RightFence") })
                {
                    var panels = side.Cast<Transform>().Where(t => t.name.StartsWith("FencePanel"))
                        .OrderBy(t => Extent(t).center.x).ToArray();
                    for (int i = 0; i < panels.Length; i++)
                    {
                        var range = Extent(panels[i]);
                        float min = range.min.x, max = range.max.x;
                        // 가장 바깥 조각은 원래 파이프 끝까지 늘인다.
                        if (min - whole.min.x < 0.5f) min = whole.min.x;
                        if (whole.max.x - max < 0.5f) max = whole.max.x;
                        var piece = Object.Instantiate(beam, beam.parent);
                        piece.name = "TopBeam" + panels[i].name.Substring("FencePanel".Length);
                        Vector3 scale = piece.localScale;
                        scale.x *= (max - min) / whole.size.x;
                        piece.localScale = scale;
                        piece.position += Vector3.right * ((min + max) * 0.5f - Extent(piece).center.x);
                        piece.SetParent(side, true);
                    }
                }
                Object.DestroyImmediate(beam.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        const string SparkPrefab = "Assets/_ThirdParty/ithappy/Battle_Stadium/VFX/FX_Prefabs/FX_Sparks_01.prefab";

        /// <summary>
        /// 좌우 톱 묶음 바닥면에 끌림 스파크를 붙인다. 문짝이 갈라지는 동안만 켜지고,
        /// 불똥은 끌려가는 반대쪽(가운데)으로 튄다.
        /// </summary>
        [MenuItem("Tools/CurtainCall/인간 대포/톱벽 열림 스파크 적용")]
        public static void AddDragSparksToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != HumanCannonTestSceneBuilder.ScenePath)
                throw new InvalidOperationException("Play 종료 후 HumanCannon 전용 씬에서 실행하세요.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SparkPrefab)
                ?? throw new InvalidOperationException("스파크 프리팹이 없습니다: " + SparkPrefab);
            foreach (var gate in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HumanCannonSawGate>(true)))
            {
                var visual = gate.transform.Find("SawVisual");
                var halves = new[] { (visual?.Find("LeftBlades"), -1f), (visual?.Find("RightBlades"), 1f) };
                if (halves.Any(h => h.Item1 == null)) throw new InvalidOperationException("톱벽 좌우 분리를 먼저 적용하세요.");
                if (halves.Any(h => h.Item1.Find("DragSparks") != null)) continue; // 이미 적용.
                var sparks = new ParticleSystem[2];
                for (int i = 0; i < halves.Length; i++)
                {
                    var (half, sign) = halves[i];
                    var discs = half.Cast<Transform>().Where(t => t.name.StartsWith("GateSawDisc")).ToArray();
                    // 다 열리면 바깥 끝이 틈 절반만큼 바닥 밖으로 나가므로, 그 구간을 빼고 바닥 위에서만 튀게 한다.
                    float overhang = gate.OpenGap * 0.5f;
                    float length = discs.Max(t => t.position.x) - discs.Min(t => t.position.x) + 1f - overhang;
                    var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, half);
                    item.name = "DragSparks";
                    item.transform.localScale = Vector3.one;
                    item.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 방출 방향(+Z)을 위로.
                    // 톱 묶음 가운데, 관문 바닥 바로 위.
                    float centerX = discs.Average(t => t.position.x) - sign * overhang * 0.5f;
                    item.transform.position = new Vector3(centerX, gate.transform.position.y + 0.05f, gate.transform.position.z);
                    sparks[i] = Configure(item.GetComponent<ParticleSystem>(), length, -sign);
                }
                using var data = new SerializedObject(gate);
                var list = data.FindProperty("dragSparks");
                list.arraySize = sparks.Length;
                for (int i = 0; i < sparks.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = sparks[i];
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static ParticleSystem Configure(ParticleSystem sparks, float length, float towardCenter)
        {
            var main = sparks.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = new Color(1f, 0.95f, 0.6f);
            main.gravityModifier = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 600;
            var emission = sparks.emission;
            emission.rateOverTime = 250f;
            var shape = sparks.shape;
            shape.enabled = true;
            // 납작한 Box는 z 배율이 방출 방향까지 줄여 불똥이 옆으로 퍼지므로, 바닥 사각형에서 위(+Z)로 쏜다.
            shape.shapeType = ParticleSystemShapeType.Rectangle;
            shape.scale = new Vector3(length, 2.4f, 1f); // x: 톱 줄 길이, y: 톱날 지름만큼(월드 z).
            shape.randomDirectionAmount = 1f;
            // 문짝이 바깥으로 끌려가므로 불똥은 가운데 쪽으로 튀고, 톱날에 가리지 않게 벽 앞뒤로도 퍼진다.
            var velocity = sparks.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            // 방출 모양의 방향 대신 월드 속도로 위·가운데·앞뒤 방향을 정한다(중력 1.6배로 약 0.3~0.9m 솟았다 떨어짐).
            velocity.x = new ParticleSystem.MinMaxCurve(towardCenter * 2f, towardCenter * 6f);
            velocity.y = new ParticleSystem.MinMaxCurve(3f, 6f);
            velocity.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            // 원본의 월드 충돌은 불똥이 나오는 충돌 벽 안에서 바로 막히므로 끈다. 짧은 수명으로 바닥 아래로 내려가기 전에 사라진다.
            var collision = sparks.collision;
            collision.enabled = false;
            // 원본은 크기가 빨리 줄고 붉게 투명해져 넓은 공연장에서 안 보인다. 밝은 노랑 → 주황으로 바꾸고 끝에서만 사라진다.
            var size = sparks.sizeOverLifetime;
            size.enabled = false;
            var color = sparks.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.6f), new GradientColorKey(new Color(1f, 0.3f, 0f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return sparks;
        }

        static Bounds Extent(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            var bounds = renderers[0].bounds;
            foreach (var item in renderers.Skip(1)) bounds.Encapsulate(item.bounds);
            return bounds;
        }

        static void Validate(HumanCannonSawGate gate)
        {
            var barrier = gate.transform.Find("Barrier");
            var visual = gate.transform.Find("SawVisual");
            if (barrier == null || barrier.GetComponent<BoxCollider>() == null || barrier.Find("MetalMesh/FencePanel5") == null
                || visual == null || visual.childCount != 44 || visual.Cast<Transform>().Any(t => !t.name.StartsWith("GateSawDisc")))
                throw new InvalidOperationException($"{gate.transform.parent.name}: Barrier 충돌 벽·철망·톱 44장 구성이 필요합니다.");
        }

        static void Split(HumanCannonSawGate gate, float openGap)
        {
            var barrier = gate.transform.Find("Barrier");
            var collider = barrier.GetComponent<BoxCollider>();
            var wall = collider.bounds;
            var visual = gate.transform.Find("SawVisual");
            var mesh = barrier.Find("MetalMesh");
            Vector3 right = gate.transform.right;

            var leftBlades = Group(visual, "LeftBlades");
            var rightBlades = Group(visual, "RightBlades");
            foreach (var disc in visual.Cast<Transform>().Where(t => t.name.StartsWith("GateSawDisc")).ToArray())
                disc.SetParent(Side(gate, disc.position) < 0 ? leftBlades : rightBlades, true);
            Wall(leftBlades, collider, wall.center - right * wall.extents.x * 0.5f, wall.size);
            Wall(rightBlades, collider, wall.center + right * wall.extents.x * 0.5f, wall.size);

            // 가운데에 걸친 철망 한 장은 좌우 절반으로 나눠 각 문짝에 붙인다.
            var middle = mesh.Find("FencePanel5");
            float half = middle.GetComponent<Renderer>().bounds.extents.x * 0.5f;
            foreach (var (suffix, sign) in new[] { ("L", -1f), ("R", 1f) })
            {
                var piece = Object.Instantiate(middle, mesh);
                piece.name = "FencePanel5" + suffix;
                Vector3 scale = piece.localScale;
                scale.x *= 0.5f;
                piece.localScale = scale;
                piece.position = middle.position + right * (sign * half);
            }
            Object.DestroyImmediate(middle.gameObject);
            var leftFence = Group(mesh, "LeftFence");
            var rightFence = Group(mesh, "RightFence");
            foreach (var panel in mesh.Cast<Transform>().Where(t => t.name.StartsWith("FencePanel")).ToArray())
                panel.SetParent(Side(gate, panel.GetComponent<Renderer>().bounds.center) < 0 ? leftFence : rightFence, true);
            Object.DestroyImmediate(collider);

            using var data = new SerializedObject(gate);
            Assign(data.FindProperty("leftParts"), leftBlades, leftFence);
            Assign(data.FindProperty("rightParts"), rightBlades, rightFence);
            data.FindProperty("openGap").floatValue = openGap;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static float Side(HumanCannonSawGate gate, Vector3 point) => Vector3.Dot(point - gate.transform.position, gate.transform.right);

        static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.gameObject.layer = parent.gameObject.layer;
            return group;
        }

        static void Wall(Transform group, BoxCollider source, Vector3 center, Vector3 size)
        {
            group.gameObject.layer = source.gameObject.layer;
            var wall = group.gameObject.AddComponent<BoxCollider>();
            wall.sharedMaterial = source.sharedMaterial;
            wall.isTrigger = source.isTrigger;
            wall.center = group.InverseTransformPoint(center);
            wall.size = new Vector3(size.x * 0.5f, size.y, size.z);
        }

        static void Assign(SerializedProperty list, params Transform[] parts)
        {
            list.arraySize = parts.Length;
            for (int i = 0; i < parts.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
        }
    }
}
