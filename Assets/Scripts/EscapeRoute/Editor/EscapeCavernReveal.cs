using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string RevealGroup = "14_CavernReveal";

        // A local composition pass. Keep the existing route, isolated platforms and Y levels.
        [MenuItem("Tools/CurtainCall/탈출로/03 공동 진입 압도감 조정")]
        public static void ApplyCavernReveal()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("escape_route 편집 모드가 필요합니다.");
            var background = scene.GetRootGameObjects().First(g => g.name == RootName).transform;
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("03 cavern reveal composition");
            var old = background.Find(RevealGroup);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var group = new GameObject(RevealGroup).transform;
            Undo.RegisterCreatedObjectUndo(group.gameObject, "Cavern reveal");
            group.SetParent(background, false);

            var wall = AssetDatabase.LoadAssetAtPath<Material>(Materials + "escape-V4_Wall_DamagedConcrete.mat");
            var steel = AssetDatabase.LoadAssetAtPath<Material>(Materials + "ref-M_Crane_Beam.mat");
            var fixture = AssetDatabase.LoadAssetAtPath<Material>(Materials + "concrete-service-fixture.mat");
            var bulb = AssetDatabase.LoadAssetAtPath<Material>(Materials + "concrete-service-bulb.mat");
            if (wall == null || steel == null || fixture == null || bulb == null)
                throw new InvalidOperationException("기존 탈출로 재질이 필요합니다.");
            var depth = RevealMaterial("cavern-depth-concrete", wall, new Color(.11f, .125f, .14f));
            var shadow = RevealMaterial("cavern-shadow-concrete", wall, new Color(.30f, .32f, .34f));

            // End the straight approach; the right-hand aperture reveals the cavern.
            RevealBlock(group, "진입로 끝막이", new Vector3(12, 14, 68), new Vector3(12.8f, 28, 1), wall);
            RevealBlock(group, "전경 왼쪽 교각", new Vector3(18, 14, 53.25f), new Vector3(4, 28, 6.5f), wall);
            RevealBlock(group, "전경 오른쪽 교각", new Vector3(18, 14, 66.75f), new Vector3(4, 28, 6.5f), wall);
            RevealBlock(group, "5m 높이 진입 인방", new Vector3(18, 16.5f, 60), new Vector3(4, 23, 7), shadow);

            // Continuous lower envelope: no sky through the 60m foundations.
            RevealBlock(group, "공동 후면 하부 외피", new Vector3(38, -31, 70.5f), new Vector3(41, 62, 1), depth);
            RevealBlock(group, "공동 전면 하부 외피", new Vector3(38, -31, 49.5f), new Vector3(41, 62, 1), depth);
            RevealBlock(group, "공동 끝 하부 외피", new Vector3(58.5f, -31, 60), new Vector3(1, 62, 22), depth);
            RevealBlock(group, "공동 암부 바닥", new Vector3(38, -62, 60), new Vector3(41, 1, 22), depth);
            // The approach shell also needs a continuous foundation at the near corner.
            RevealBlock(group, "진입측 하부 외피", new Vector3(17.5f, -31, 60), new Vector3(1, 62, 22), depth);

            // A single readable industrial landmark, spanning several human service levels.
            foreach (float x in new[] { 37f, 44.5f })
            {
                RevealAsset(group, "수직 집관 주관", Industrial + "SM_Pipe_Big_01.prefab",
                    new Vector3(x, -48, 68), new Vector3(3.4f, 73, 3.4f), steel, Quaternion.Euler(0, 0, 90));
                foreach (float y in new[] { -35f, -17f, 1f, 19f })
                    RevealAsset(group, "집관 보강 링", Industrial + "SM_Pipe_Big_Ring_01.prefab",
                        new Vector3(x, y, 68), new Vector3(4.1f, .45f, 4.1f), steel, Quaternion.Euler(0, 0, 90));
            }
            foreach (float y in new[] { -18f, 12f, 23f })
            {
                RevealAsset(group, "집관 횡보", Industrial + "SM_Crane_Beam.prefab",
                    new Vector3(41, y, 68), new Vector3(22, .8f, 1), steel);
                // Short, unconnected maintenance ledges are scale cues, never a player bypass.
                RevealAsset(group, "집관 점검 발판", Industrial + "SM_Scaffolding_Flooring.prefab",
                    new Vector3(44.5f, y - .3f, 66.2f), new Vector3(5.5f, .2f, 1.3f), steel);
                RevealRail(group, new Vector3(42.3f, y, 65.7f), new Vector3(46.7f, y, 65.7f), steel);
            }
            RevealAsset(group, "상부 깊이 횡보", Industrial + "SM_Crane_Beam.prefab",
                new Vector3(36, 25, 57), new Vector3(34, 1.4f, 1.2f), steel);
            RevealAsset(group, "상부 후경 횡보", Industrial + "SM_Crane_Beam.prefab",
                new Vector3(45, 21, 63), new Vector3(22, 1, 1), steel);

            // A 14m sealed service port gives the far wall an identifiable industrial purpose.
            // Its tiny access door below supplies a familiar human-scale comparison.
            RevealAsset(group, "14m 설비 점검구", Industrial + "SM_Pipe_Big_Cap.prefab",
                new Vector3(57.2f, 8, 60), new Vector3(1.1f, 14, 14), steel, null, true);
            RevealAsset(group, "점검구 외곽 플랜지", Industrial + "SM_Pipe_Big_Ring_01.prefab",
                new Vector3(56.7f, 7.5f, 60), new Vector3(.45f, 15, 15), fixture, null, true);

            // Human-sized details retain actual metres despite the map's horizontal expansion.
            RevealAsset(group, "2.2m 점검 사다리", Industrial + "SM_Ladder_01.prefab",
                new Vector3(47.1f, 12, 66.6f), new Vector3(.55f, 2.2f, .2f), fixture, null, true);
            RevealBlock(group, "도착부 사람 크기 점검문", new Vector3(57.65f, 3.1f, 63.5f),
                new Vector3(.10f, 2.2f, 1.05f), fixture, true, false);
            RevealBlock(group, "점검문 손잡이", new Vector3(57.52f, 3.05f, 63.22f),
                new Vector3(.12f, .12f, .25f), steel, true, false);
            RevealBlock(group, "점검문 위 작업등", new Vector3(57.35f, 4.55f, 63.5f),
                new Vector3(.18f, .12f, .65f), bulb, true, false);

            // Suppress the even local wash; keep the exterior sky and global lighting intact.
            foreach (var light in background.GetComponentsInChildren<Light>())
            {
                if (light.transform.IsChildOf(group)) continue;
                var local = background.InverseTransformPoint(light.transform.position);
                if (local.x < 17 || local.x > 59 || local.z < 49 || local.z > 72) continue;
                Undo.RecordObject(light, "03 lighting hierarchy");
                if (light.name.StartsWith("Concrete upper fill", StringComparison.Ordinal)) light.intensity = 8;
                else if (light.name == "Neutral service light") { light.intensity = 5; light.range = 11; }
                else if (light.name == "Level depth light") { light.intensity = 2.4f; light.range = 9; }
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            }
            RevealLight(group, "집관 상부의 중성 사광", new Vector3(29, 20, 63),
                new Vector3(44, 16, 68), 464, 32, 54);
            RevealLight(group, "도착 발판 작업 조명", new Vector3(56, 10, 61),
                new Vector3(52, 2, 60), 368, 18, 53);
            RevealLight(group, "중간 발판 판독 조명", new Vector3(35, 9, 56),
                new Vector3(38, 2, 60), 200, 16, 48);
            RevealLight(group, "하부 점검층의 약한 빛", new Vector3(40, -9, 64),
                new Vector3(44, -18, 68), 160, 22, 40);
            RevealLight(group, "입구 바닥의 반사광", new Vector3(22, 5, 59),
                new Vector3(25, 0, 60), 128, 12, 65);
            RevealLight(group, "대형 점검구를 읽는 사광", new Vector3(44, 22, 54),
                new Vector3(57, 15, 60), 500, 29, 46);

            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("03 공동: 진입 문틀·연속 외피·수직 집관·사람 크기 기준·조명 계층 적용. 씬 저장은 별도.");
        }

        static Material RevealMaterial(string name, Material source, Color color)
        {
            string path = Materials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(source) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .12f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject RevealBlock(Transform parent, string name, Vector3 localCenter, Vector3 size,
            Material material, bool humanSize = false, bool collision = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(g, "Cavern composition block");
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localCenter;
            g.transform.localScale = humanSize ? Vector3.Scale(size,
                new Vector3(1 / parent.lossyScale.x, 1 / parent.lossyScale.y, 1 / parent.lossyScale.z)) : size;
            g.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Undo.DestroyObjectImmediate(g.GetComponent<Collider>());
            return g;
        }

        static void RevealAsset(Transform parent, string name, string path, Vector3 localBottom,
            Vector3 size, Material material, Quaternion? rotation = null, bool humanSize = false)
        {
            var worldSize = humanSize ? size : Vector3.Scale(size, parent.lossyScale);
            var g = Real(parent, path, parent.TransformPoint(localBottom), worldSize, material, rotation);
            Undo.RegisterCreatedObjectUndo(g, "Cavern infrastructure");
            g.name = name;
            foreach (var collider in g.GetComponentsInChildren<Collider>(true)) Undo.DestroyObjectImmediate(collider);
            PrefabUtility.RecordPrefabInstancePropertyModifications(g);
            PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
        }

        static void RevealRail(Transform parent, Vector3 a, Vector3 b, Material material)
        {
            // Rail modules are authored as a few human-scale pieces, not scaled to hall height.
            float length = Vector3.Distance(parent.TransformPoint(a), parent.TransformPoint(b));
            int count = Mathf.CeilToInt(length / 2);
            for (int i = 0; i < count; i++)
            {
                var point = Vector3.Lerp(a, b, (i + .5f) / count);
                RevealAsset(parent, "1.1m 점검 난간", Industrial + "SM_Scaffolding_Railing_200m.prefab",
                    point, new Vector3(length / count, 1.1f, .12f), material, Quaternion.Euler(0, 90, 0), true);
            }
        }

        static void RevealLight(Transform parent, string name, Vector3 localPosition, Vector3 localTarget,
            float intensity, float range, float angle)
        {
            var g = new GameObject(name, typeof(Light));
            Undo.RegisterCreatedObjectUndo(g, "Cavern composition light");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPosition;
            g.transform.rotation = Quaternion.LookRotation(parent.TransformPoint(localTarget) - g.transform.position);
            var light = g.GetComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(.94f, .97f, 1);
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * .6f;
            light.shadows = LightShadows.Soft;
            light.shadowBias = .04f;
            light.shadowNormalBias = .12f;
        }
    }
}
