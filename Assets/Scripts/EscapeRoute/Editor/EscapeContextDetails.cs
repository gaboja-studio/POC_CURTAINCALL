using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        [MenuItem("Tools/CurtainCall/탈출로/A 진입로 사용 흔적과 시선 보완")]
        public static void PolishAContext()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A 탈출로 편집 모드가 필요합니다.");
            InitReference();
            var previous = root.Find("15_ServiceContext");
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
            var details = Group("15_ServiceContext");
            Undo.RegisterCreatedObjectUndo(details.gameObject, "Escape service context");
            var steel = AssetDatabase.LoadAssetAtPath<Material>(Materials + "ref-M_Crane_Beam.mat");
            var fixture = AssetDatabase.LoadAssetAtPath<Material>(Materials + "concrete-service-fixture.mat");
            var bulb = AssetDatabase.LoadAssetAtPath<Material>(Materials + "concrete-service-bulb.mat");
            var drain = RevealMaterial("service-drain", fixture, new Color(.06f, .075f, .08f));
            var damp = RevealMaterial("service-damp-edge", wetFloor, new Color(.18f, .20f, .21f));
            var paint = Mat("service-marking", new Color(.46f, .49f, .47f));
            var approach = root.Find("02_ConcreteApproach_15x60");
            var barrels = approach.GetComponentsInChildren<Transform>(true)
                .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && t.name == "SM_Prop_BarrelStack_01")
                .OrderBy(t => t.position.z).ToArray();
            for (int i = 0; i < barrels.Length; i++)
            {
                Undo.RecordObject(barrels[i].gameObject, "Break repeated barrel rhythm");
                barrels[i].gameObject.SetActive(i == 1 || i == barrels.Length - 1);
                PrefabUtility.RecordPrefabInstancePropertyModifications(barrels[i].gameObject);
            }
            foreach (var light in approach.GetComponentsInChildren<Light>(true))
            {
                if (light.name != "Neutral service light") continue;
                Undo.RecordObject(light, "Alternate approach work lighting");
                float z = root.InverseTransformPoint(light.transform.position).z;
                light.intensity = z < 27 || z > 57 ? 4.5f : z < 40 ? 6f : 1.8f;
                light.range = z < 40 ? 15f : 11f;
            }

            // A shallow service gutter stays beside the walking surface; no raised collision.
            foreach (float z in new[] { 27f, 34f, 41f, 48f, 55f })
            {
                RevealBlock(details, "배수 홈", new Vector3(8.4f, .004f, z), new Vector3(.26f, .008f, 5.6f), drain, false, false);
                for (int i = 0; i < 14; i++)
                    RevealBlock(details, "배수 그레이팅", new Vector3(8.4f, .011f, z - 2.6f + i * .4f),
                        new Vector3(.3f, .012f, .045f), steel, false, false);
                RevealBlock(details, "배수 가장자리 습윤 흔적", new Vector3(8.68f, .003f, z + .8f),
                    new Vector3(.24f, .004f, 2.8f), damp, false, false);
            }
            RevealAsset(details, "케이블 릴 점검 자리", Factory + "SM_Cablespool.prefab",
                new Vector3(9.6f, 0, 34.2f), new Vector3(1.1f, 1.25f, 1.1f), steel, null, true);
            RevealAsset(details, "작업대", Industrial + "SM_ToolCart.prefab",
                new Vector3(8, 0, 36.2f), new Vector3(.65f, .92f, 1.1f), fixture, null, true);
            RevealAsset(details, "정비용 팔레트", Factory + "SM_Woodpallet_1.prefab",
                new Vector3(8.3f, 0, 32.4f), new Vector3(1.1f, .18f, 1.1f), fixture, null, true);
            foreach (float y in new[] { 2.6f, 2.9f })
            {
                RevealAsset(details, "점검함으로 모이는 케이블", Industrial + "SM_Pipe_Small_01.prefab",
                    new Vector3(6.6f, y, 36), new Vector3(.08f, .08f, 7), fixture);
            }
            RevealBlock(details, "벽면 점검함", new Vector3(6.7f, 1.55f, 37), new Vector3(.22f, 1.05f, .72f), fixture, true, false);
            ContextSign(details, "벽면 점검 표식", "SERVICE 02", new Vector3(6.84f, 2.45f, 37), 270, 1.4f);
            ContextSign(details, "굽이 방향 표식", "03  >", new Vector3(12, 2.6f, 67.43f), 0, 1.7f);
            // Restrained floor markings follow the maintenance bay, not the centre of the route.
            foreach (float z in new[] { 33f, 35f, 37f })
                RevealBlock(details, "작업 구역 바닥 도색", new Vector3(10, .014f, z), new Vector3(.07f, .009f, .7f), paint, true, false);
            RevealLight(details, "굽이 너머의 작업 빛", new Vector3(17, 6.5f, 64),
                new Vector3(13.5f, 1.4f, 67.3f), 165, 12, 48);
            EnsureReviewLabels(root.gameObject);
            AssetDatabase.SaveAssetIfDirty(font);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void EnsureReviewLabels(GameObject background)
        {
            if (background.GetComponent<CurtainCall.EscapeRoute.EscapeRouteReviewLabels>() == null)
                Undo.AddComponent<CurtainCall.EscapeRoute.EscapeRouteReviewLabels>(background);
        }

        static void ContextSign(Transform parent, string name, string text, Vector3 localPosition, float localYaw, float width)
        {
            var go = new GameObject("Wayfinding_" + name, typeof(TextMeshPro));
            Undo.RegisterCreatedObjectUndo(go, "Small service wayfinding");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0, localYaw, 0);
            go.transform.localScale = new Vector3(1 / parent.lossyScale.x, 1 / parent.lossyScale.y, 1 / parent.lossyScale.z);
            font = GetFont(); EnsureGlyphs(text);
            var label = go.GetComponent<TextMeshPro>();
            label.font = font; label.text = text; label.fontSize = 1.25f;
            label.color = new Color(.58f, .62f, .61f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(width, .35f);
        }
    }
}
