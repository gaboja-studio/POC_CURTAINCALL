using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        const string IndustrialFinishGroup = "18_WorkingFacilityFinish";
        const string IndustrialTextures = "Assets/_ThirdParty/LeartesStudios/HDRP/Art/Textures/";
        static Material wornSteel, wornPipe, maintenancePaint, leakWear, floorWear, patchConcrete;

        [MenuItem("Tools/CurtainCall/탈출로/A 사용 흔적과 가독성 적용")]
        public static void ApplyIndustrialFinish()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A 씬의 편집 모드가 필요합니다.");
            var background = GameObject.Find(RootName).transform;
            if (background.Find(IndustrialFinishGroup) != null)
                throw new InvalidOperationException("사용 흔적이 이미 있습니다. 18 그룹을 직접 편집하거나 Undo 후 다시 적용하세요.");
            var structure = background.Find(BrutalGroup);
            if (structure == null) throw new InvalidOperationException("A 브루탈리즘 구조가 필요합니다.");

            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("A used industrial facility and readable lighting");
            var root = VerticalChild(background, IndustrialFinishGroup);
            root.localScale = new Vector3(1 / background.lossyScale.x, 1 / background.lossyScale.y, 1 / background.lossyScale.z);
            var concrete = AssetDatabase.LoadAssetAtPath<Material>(Materials + "brutal-concrete.mat");
            wornSteel = FinishMaterial("working-steel", concrete, new Color(.29f, .31f, .30f), .08f);
            wornPipe = FinishMaterial("working-pipe", concrete, new Color(.37f, .33f, .28f), .06f);
            maintenancePaint = FinishMaterial("working-faded-ochre", concrete, new Color(.47f, .42f, .25f), .02f);
            patchConcrete = FinishMaterial("working-concrete-repair", concrete, new Color(.64f, .63f, .59f), .02f);
            leakWear = WearMaterial("working-leak", IndustrialTextures + "Decals/Leak/T_Leak_01_Mask.png", new Color(.20f, .22f, .18f, .66f));
            floorWear = WearMaterial("working-floor-dirt", IndustrialTextures + "Grunges/T_Grunge_Mask.png", new Color(.34f, .31f, .25f, .40f));
            ReadableFacilityLight(background);

            var approach = VerticalChild(root, "01_02_ServiceRuns");
            foreach (float y in new[] { 1.15f, 2.45f })
            {
                ServicePipe(approach, new Vector3(22.08f, y, 25), new Vector3(22.08f, y, 60), .18f);
                ServicePipe(approach, new Vector3(7.05f, y + 1, 1), new Vector3(7.05f, y + 1, 19), .16f);
            }
            CableTray(approach, new Vector3(20.6f, 3.3f, 24), new Vector3(20.6f, 3.3f, 72));
            foreach (float z in new[] { 28f, 39f, 55f })
            {
                RevealBlock(approach, "배관 벽체 브래킷", new Vector3(22.18f, 1.8f, z), new Vector3(.5f, 1.8f, .08f), wornSteel, false, false);
                WallWear(approach, new Vector3(22.48f, 2.25f, z + 1.8f), new Vector2(4.2f, 4.4f), 90);
            }
            WallWear(approach, new Vector3(8.4f, 2.7f, 19.47f), new Vector2(4.2f, 5.4f), 0);
            WallWear(approach, new Vector3(26f, 2.6f, 21.48f), new Vector2(5.5f, 5.2f), 0);
            MaintenanceBay(approach, new Vector3(20.9f, 0, 42), -13);
            MaintenanceBay(approach, new Vector3(8.4f, 0, 15), 17);
            foreach (float z in new[] { 13f, 27f, 44f, 58f, 70f })
                FloorWear(approach, new Vector3(z < 22 ? 11 : 19.7f, .015f, z), new Vector2(4.4f, 5.8f), z * 3);
            foreach (float z in new[] { 26f, 37.4f, 58.2f, 74f })
                RepairJoint(approach, new Vector3(16, .012f, z), 11.5f);
            ServiceLamp(approach, new Vector3(12, 3.3f, 16), 16, 12);
            ServiceLamp(approach, new Vector3(21.6f, 3.5f, 39), 18, 15);
            ServiceLamp(approach, new Vector3(20.5f, 3.5f, 67), 15, 14);

            var cavern = VerticalChild(root, "03_VerticalMaintenance");
            foreach (float x in new[] { 39f, 58f })
            {
                ServicePipe(cavern, new Vector3(x, -68, 111.7f), new Vector3(x, 29, 111.7f), .32f);
                WallWear(cavern, new Vector3(x + 1.8f, -9, 111.45f), new Vector2(8, 38), 0);
            }
            foreach (float y in new[] { -42f, -18f, 16f })
                WallWear(cavern, new Vector3(47.83f, y, 90), new Vector2(6, 12), 90);
            FloorWear(cavern, new Vector3(30, .015f, 76), new Vector2(4, 5), 11);
            FloorWear(cavern, new Vector3(67, -5.985f, 83), new Vector2(3, 5), -20);
            MaintenanceBay(cavern, new Vector3(28, 0, 81), 8);
            ServiceLamp(cavern, new Vector3(29, 3, 79), 24, 19);
            ServiceLamp(cavern, new Vector3(68, -2.5f, 84), 25, 18);

            var later = VerticalChild(root, "04_07_WornServiceAreas");
            ServicePipe(later, new Vector3(77.1f, 2.9f, 37), new Vector3(77.1f, 2.9f, 58), .24f);
            CableTray(later, new Vector3(73.9f, 4, 37), new Vector3(73.9f, 4, 57));
            WallWear(later, new Vector3(77.48f, 3.5f, 51), new Vector2(7, 7), 90);
            FloorWear(later, new Vector3(70, .015f, 38), new Vector2(3, 8), -12);
            ServiceLamp(later, new Vector3(70.5f, 3.5f, 39), 16, 12);
            foreach (float x in new[] { 83f, 123f })
            {
                WallWear(later, new Vector3(x, -10, 44.97f), new Vector2(9, 26), 0);
                ServicePipe(later, new Vector3(x, -65, 43.9f), new Vector3(x, 20, 43.9f), .28f);
            }
            FloorWear(later, new Vector3(83, .015f, 29), new Vector2(3.8f, 5), -23);
            FloorWear(later, new Vector3(121, -7.985f, 35), new Vector2(5, 5.5f), 31);
            ServiceLamp(later, new Vector3(84, 3, 30), 28, 20);
            ServiceLamp(later, new Vector3(123, -4, 36), 30, 21);
            MaintenanceBay(later, new Vector3(116.8f, 0, 71), -18);
            WallWear(later, new Vector3(127.8f, 2.7f, 87.47f), new Vector2(6, 5.4f), 0);
            ServiceLamp(later, new Vector3(125, 3.5f, 71), 22, 18);
            CableTray(later, new Vector3(145, 4.1f, 81.7f), new Vector3(187, 4.1f, 81.7f));
            ServicePipe(later, new Vector3(141, 1.2f, 82.7f), new Vector3(187, 1.2f, 82.7f), .20f);
            foreach (float x in new[] { 149f, 169f, 189f })
            {
                WallWear(later, new Vector3(x, 3.2f, 81.97f), new Vector2(4, 6.4f), 0);
                FloorWear(later, new Vector3(x + 3, .015f, 80), new Vector2(5, 4), x);
                RepairJoint(later, new Vector3(x + 4.8f, .012f, 77.4f), 11.8f, true);
            }
            MaintenanceBay(later, new Vector3(177.4f, 0, 57), 23);
            ServiceLamp(later, new Vector3(160, 4.4f, 80), 21, 16);
            ServiceLamp(later, new Vector3(185, 3.5f, 52), 19, 16);
            foreach (float x in new[] { 194.97f, 203.97f })
                WallWear(later, new Vector3(x, 3.5f, 27), new Vector2(6, 7), 90);
            MaintenanceBay(later, new Vector3(190.8f, 0, 29), -9);
            FloorWear(later, new Vector3(191, .015f, 22), new Vector2(6, 4), 28);
            AddIndustrialServiceDetails();
            EnsureReviewLabels(background.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undo);
        }

        static void ReadableFacilityLight(Transform background)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.36f, .37f, .38f);
            DynamicGI.UpdateEnvironment();
            foreach (var light in background.GetComponentsInChildren<Light>())
            {
                if (light.type != LightType.Spot || light.transform.position.y < -10 || light.intensity > 700) continue;
                Undo.RecordObject(light, "Broader practical light");
                light.spotAngle = 112; light.innerSpotAngle = 38;
                light.color = new Color(.97f, .97f, .94f);
            }
            var camera = Camera.main;
            if (camera != null) { Undo.RecordObject(camera, "Restore exterior sky"); camera.clearFlags = CameraClearFlags.Skybox; }
        }

        static Material FinishMaterial(string name, Material source, Color tint, float smoothness)
        {
            var material = RevealMaterial(name, source, tint);
            material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); return material;
        }

        static Material WearMaterial(string name, string maskPath, Color tint)
        {
            string path = Materials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("CurtainCall/Escape/SurfaceWear")); AssetDatabase.CreateAsset(material, path); }
            material.SetTexture("_Mask", AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
            material.SetColor("_Tint", tint); EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); return material;
        }

        static void WearQuad(Transform parent, Vector3 position, Vector2 size, Quaternion rotation, Material material, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); Undo.RegisterCreatedObjectUndo(go, "Facility surface wear");
            go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.transform.localRotation = rotation; go.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            Undo.DestroyObjectImmediate(go.GetComponent<Collider>());
        }
        static void WallWear(Transform p, Vector3 at, Vector2 size, float yaw)
            => WearQuad(p, at, size, Quaternion.Euler(0, yaw, 0), leakWear, "이음부 누수와 광물 흔적");
        static void FloorWear(Transform p, Vector3 at, Vector2 size, float yaw)
            => WearQuad(p, at, size, Quaternion.Euler(90, yaw, 0), floorWear, "정비 작업 바닥 오염");

        static void ServicePipe(Transform p, Vector3 a, Vector3 b, float diameter)
        {
            VerticalCable(p, "오래된 시설 배관", a, b, diameter, wornPipe);
            var direction = (b - a).normalized;
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 7));
            for (int i = 0; i <= count; i++)
            {
                var at = Vector3.Lerp(a, b, (float)i / count);
                VerticalCable(p, "배관 플랜지 이음", at - direction * .07f, at + direction * .07f, diameter * 1.5f, wornSteel);
            }
        }

        static void CableTray(Transform p, Vector3 a, Vector3 b)
        {
            var direction = (b - a).normalized; var side = Vector3.Cross(direction, Vector3.up) * .22f;
            for (int i = 0; i < 4; i++)
            {
                var offset = side * (-1 + i * .65f);
                VerticalCable(p, "정비 케이블", a + offset, b + offset, .045f + i * .009f, wornSteel);
            }
            int count = Mathf.CeilToInt(Vector3.Distance(a, b) / 2.4f);
            for (int i = 0; i <= count; i++)
            {
                var at = Vector3.Lerp(a, b, (float)i / count);
                VerticalCable(p, "케이블 트레이 가로대", at - side * 1.4f, at + side * 1.4f, .04f, wornPipe);
            }
        }

        static void MaintenanceBay(Transform p, Vector3 feet, float yaw)
        {
            var bay = VerticalChild(p, "사용 중인 정비 자리"); bay.localPosition = feet; bay.localRotation = Quaternion.Euler(0, yaw, 0);
            RevealAsset(bay, "이동 정비 카트", Industrial + "SM_ToolCart.prefab", Vector3.zero, new Vector3(.65f, .9f, 1.1f), wornSteel);
            RevealAsset(bay, "임시 적치 팔레트", Factory + "SM_Woodpallet_1.prefab", new Vector3(.2f, 0, 1.8f), new Vector3(1.15f, .14f, .9f), wornPipe, Quaternion.Euler(0, 11, 0));
            RevealAsset(bay, "사용한 케이블 릴", Factory + "SM_Cablespool.prefab", new Vector3(.35f, 0, 2.9f), new Vector3(.8f, .85f, .8f), wornSteel, Quaternion.Euler(0, 32, 0));
            for (int i = 0; i < 3; i++)
                RevealBlock(bay, "적치한 보수 자재", new Vector3(.1f + .16f * i, .2f + .08f * i, 1.8f), new Vector3(.95f, .07f, .18f), patchConcrete, false, false).transform.localRotation = Quaternion.Euler(0, 7 + i * 13, 0);
            FloorWear(bay, new Vector3(.2f, .016f, 1.5f), new Vector2(2.5f, 4.5f), 17);
        }

        static void RepairJoint(Transform p, Vector3 at, float length, bool alongZ = false)
        {
            RevealBlock(p, "바닥 보수 이음", at, alongZ ? new Vector3(.035f, .008f, length) : new Vector3(length, .008f, .035f), wornSteel, false, false);
            for (int i = 0; i < 3; i++)
            {
                var position = at + (alongZ ? Vector3.forward : Vector3.right) * (length * .2f + i * .19f);
                RevealBlock(p, "마모된 설비 경계 도색", position + Vector3.up * .009f, alongZ ? new Vector3(.5f, .008f, .09f) : new Vector3(.09f, .008f, .5f), maintenancePaint, false, false);
            }
        }

        static void ServiceLamp(Transform p, Vector3 at, float intensity, float range)
        {
            var lamp = new GameObject("넓게 퍼지는 시설 작업등", typeof(Light)); Undo.RegisterCreatedObjectUndo(lamp, "Practical work light");
            lamp.transform.SetParent(p, false); lamp.transform.localPosition = at;
            var light = lamp.GetComponent<Light>(); light.type = LightType.Point; light.color = new Color(.95f, .97f, 1);
            light.intensity = intensity; light.range = range; light.shadows = LightShadows.None;
            var bulb = AssetDatabase.LoadAssetAtPath<Material>(Materials + "concrete-service-bulb.mat");
            RevealBlock(p, "노출 작업등 몸체", at + Vector3.up * .12f, new Vector3(1.1f, .16f, .22f), wornSteel, false, false);
            RevealBlock(p, "노출 작업등 광원", at, new Vector3(.95f, .06f, .14f), bulb, false, false);
        }
    }
}
