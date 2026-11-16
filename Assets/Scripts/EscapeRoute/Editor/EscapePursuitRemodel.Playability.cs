using System;
using System.Linq;
using CurtainCall.HumanCannon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapePursuitRemodel
    {
        const string FeedbackRoot = "Pole feedback architecture";

        [MenuItem("Tools/CurtainCall/탈출로/복사 씬 봉 피드백 적용")]
        public static void ApplyPoleFeedback()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath)
                throw new InvalidOperationException("복사 씬의 편집 모드에서만 적용하세요.");
            var route = Object.FindFirstObjectByType<EscapePursuerRoute>();
            module = route.transform;
            frame = module.GetComponentsInParent<Transform>().Single(t => t.name == "EscapeCampaignFrame");
            if (frame.Find(FeedbackRoot)) return;
            concrete = frame.GetComponentsInChildren<MeshRenderer>(true)
                .First(r => r.name == "타설 콘크리트 벽" && !r.transform.IsChildOf(module)).sharedMaterial;
            structure = new GameObject(FeedbackRoot).transform;
            structure.SetParent(frame, false);
            OpenConveyorObservationBay();
            BuildSwitchbackDescent();
            FinishPoleArchitecture();
            DarkenFacility();
            AddIndustrialVignettes();
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void OpenConveyorObservationBay()
        {
            // Open the public shaft horizontally, below the upper hidden circulation.
            // Keep the wall behind the cannon and the abyss below every moving pole.
            EscapePursuerConcreteRoutes.OpenPhysicalVolume(frame, module, structure,
                new Bounds(new Vector3(93, -16.5f, -8), new Vector3(80, 13, 32)));
            var cannon = frame.GetComponentsInChildren<HumanCannonStation>(true)
                .Single(c => c.name == "A04B_ConveyorCannon");
            cannon.transform.SetPositionAndRotation(frame.TransformPoint(new Vector3(67, -22, 2)),
                frame.rotation * Quaternion.LookRotation(Vector3.back));
            var target = frame.TransformPoint(new Vector3(67, -18.5f, -15));
            const float flight = 2.2f;
            Vector3 velocity = (target - cannon.Muzzle.position) / flight +
                Vector3.up * (CurtainCall.Settings.GameSettings.Base.Character.Gravity * flight * .5f);
            var data = new SerializedObject(cannon);
            data.FindProperty("forwardSpeed").floatValue = Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
            data.FindProperty("upwardSpeed").floatValue = velocity.y;
            data.ApplyModifiedPropertiesWithoutUndo();
            var marker = cannon.transform.Find("AuthoredLanding");
            if (marker) marker.position = target;
            var chase = module.GetComponentInChildren<EscapeUnderstageChase>(true);
            var encountersData = new SerializedObject(chase).FindProperty("encounters");
            for (int i = 0; i < encountersData.arraySize; i++)
            {
                var row = encountersData.GetArrayElementAtIndex(i);
                if (row.FindPropertyRelative("gate").intValue == 2)
                    ((Transform)row.FindPropertyRelative("landing").objectReferenceValue).position =
                        frame.TransformPoint(new Vector3(67.6f,-22,6.3f));
            }
        }

        static void BuildSwitchbackDescent()
        {
            foreach (var t in frame.GetComponentsInChildren<Transform>(true).Where(t =>
                t.name.StartsWith("화염 복도 연결 계단 ") || t.name.StartsWith("하강 계단 연속 난간") ||
                t.name == "화염 복도 무단차 연결").ToArray())
                if (t) Object.DestroyImmediate(t.gameObject);
            EscapePursuerConcreteRoutes.OpenPhysicalVolume(frame, module, structure,
                new Bounds(new Vector3(124.75f, -2.4f, 41.5f), new Vector3(10.5f, 13.2f, 20)));

            // Four two-metre flights. Upper landings/solid guards intercept a running jump;
            // the player must turn twice before descending the next four metres.
            Flight("Upper south to north", 122.25f, 35, 41, 0);
            Landing("Upper turning landing", -2, 42.5f, 3);
            Flight("Upper north to south", 127.25f, 41, 35, -2);
            Landing("Lower turning landing", -4, 33.5f, 3);
            Flight("Lower south to north", 122.25f, 35, 41, -4);
            Landing("Lower north landing", -6, 42.5f, 3);
            Flight("Flame exit flight", 127.25f, 44, 49, -6);
            Landing("Flame exit flush landing", -8, 50, 2);
            var finalLanding = structure.Find("Flame exit flush landing");
            // Join the existing west-side gallery at X125 without coplanar top faces.
            finalLanding.position = frame.TransformPoint(new Vector3(127.25f,-8.3f,50));
            finalLanding.localScale = new Vector3(4.5f,.6f,2);
            var lowerConnection = frame.GetComponentsInChildren<Transform>().Single(t => t.name == "A05 하부 연결");
            lowerConnection.position = frame.TransformPoint(new Vector3(120,-8.75f,47));
            lowerConnection.localScale = new Vector3(10,1.5f,4);

            Box("Switchback central concrete divider", new(124.75f,-2.9f,38), new(.5f,12.2f,6));
            Box("Switchback west retaining wall", new(119.75f,-2.9f,41.75f), new(.5f,12.2f,13.5f));
            Box("Switchback east retaining wall", new(129.75f,-2.9f,41.5f), new(.5f,12.2f,19));
            Box("Upper landing jump guard", new(124.75f,-.4f,44.15f), new(10,3.2f,.3f));
            Box("Lower south landing jump guard", new(124.75f,-2.4f,31.85f), new(10,3.2f,.3f));
            Box("Lower north landing jump guard", new(122.25f,-4.4f,44.15f), new(5,3.2f,.3f));
            Box("Stairwell south side guard", new(129.75f,-2.4f,33.5f), new(.5f,3.2f,3));
            Box("Entry platform side guard", new(125.15f,1.6f,33.5f), new(.3f,3.2f,3));
            Box("Lower landing underside closure", new(127.25f,-4.4f,40.85f), new(4.5f,3.2f,.3f));
            Box("Flame landing turn guard", new(127.25f,-6.4f,50.85f), new(4.5f,3.2f,.3f));
        }

        static void Flight(string name, float x, float fromZ, float toZ, float top)
        {
            for (int i = 0; i < 10; i++)
            {
                float tread = top - (i + 1) * .2f;
                float depth = top <= -4 ? tread + 9 : .6f;
                Box(name + " step " + i, new Vector3(x, tread - depth * .5f,
                    Mathf.Lerp(fromZ, toZ, (i + .5f) / 10)), new Vector3(4.5f, depth, Mathf.Abs(toZ - fromZ) / 10));
            }
        }

        static void Landing(string name, float top, float z, float depth)
        {
            float thickness = top <= -4 && top > -8 ? top + 9 : .6f;
            Box(name, new(124.75f, top - thickness * .5f, z), new(9.5f,thickness,depth));
        }

        public static void FinishPoleArchitecture()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("복사 씬 편집 모드가 필요합니다.");
            module = Object.FindFirstObjectByType<EscapePursuerRoute>().transform;
            frame = module.GetComponentsInParent<Transform>().Single(t => t.name == "EscapeCampaignFrame");
            structure = frame.Find(FeedbackRoot);
            concrete = structure.Find("Switchback central concrete divider").GetComponent<MeshRenderer>().sharedMaterial;
            void SetBox(string name, Vector3 p, Vector3 size)
            {
                var t = structure.Find(name);
                if (!t) t = Box(name,p,size);
                t.SetPositionAndRotation(frame.TransformPoint(p),frame.rotation); t.localScale = size;
            }
            SetBox("Final stair west jump guard",new(124.85f,-5.8f,46.5f),new(.3f,4.4f,5));
            SetBox("Stair exit west jump guard",new(119.75f,-7.25f,50),new(.5f,1.5f,2));
            // The last metre puts a grounded runner inside the shared grab radius.
            SetBox("Flame exit swing approach tread",new(122.5f,-8.3f,89.55f),new(3.4f,.6f,1.1f));
            FitFlameSwingLanding(frame);
            FitWireExitLanding(frame);
            FitHoistApproach(frame);
            // Fin walls close oblique views from moving pole riders without a visible service floor.
            SetBox("Conveyor unloading concrete fin",new(33.8f,-50,-71.25f),new(.8f,90,9.5f));
            SetBox("Return bay concrete fin",new(83.9f,-48,-87.25f),new(3,80,1.5f));
            CleanupFragments();
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static void FitFlameSwingLanding(Transform campaignFrame)
        {
            // Keep the far edge, surface height and existing cannon landing component.
            // Extend the near edge from Z100 to Z97 so a forward release clears the deck lip.
            var deck = campaignFrame.GetComponentsInChildren<Transform>(true)
                .Single(t => t.name == "코어 끝 짧은 대포대");
            var position = deck.localPosition;
            var size = deck.localScale;
            position.z = 102.5f;
            size.z = 11f;
            deck.localPosition = position;
            deck.localScale = size;
        }

        static void FitWireExitLanding(Transform campaignFrame)
        {
            // The upper swing descends to deck height before the original Z80 edge.
            var deck = campaignFrame.GetComponentsInChildren<Transform>(true)
                .Single(t => t.name == "반출동 착지 측면 이음");
            var position = deck.localPosition;
            var size = deck.localScale;
            position.z = 79.9f;
            size.z = 7.8f;
            deck.localPosition = position;
            deck.localScale = size;
        }

        static void FitHoistApproach(Transform campaignFrame)
        {
            // A crawling runner's lower interaction centre must fit inside the normal grab radius.
            var deck = campaignFrame.GetComponentsInChildren<Transform>(true)
                .Single(t => t.name == "공동 탑승 대기턱");
            var position = deck.localPosition;
            var size = deck.localScale;
            position.x = 277.625f;
            size.x = 3.25f;
            deck.localPosition = position;
            deck.localScale = size;
        }

        static void DarkenFacility()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.1f,.115f,.135f);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.reflectionIntensity = .09f;
            RenderSettings.fogColor = new Color(.017f,.024f,.032f);
            foreach (var light in frame.GetComponentsInChildren<Light>().Where(l => l.enabled))
            {
                if (light.type == LightType.Directional) light.intensity = .32f;
                else light.intensity *= .52f;
            }
            RefineFeedbackPresentation();
        }

        public static void RebalanceFeedbackLighting()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("복사 씬 편집 모드가 필요합니다.");
            module = Object.FindFirstObjectByType<EscapePursuerRoute>().transform;
            frame = module.GetComponentsInParent<Transform>().Single(t => t.name == "EscapeCampaignFrame");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.1f,.115f,.135f);
            RenderSettings.reflectionIntensity = .09f;
            foreach (var light in frame.GetComponentsInChildren<Light>().Where(l => l.enabled && l.type == LightType.Directional))
                light.intensity = .32f;
            RefineFeedbackPresentation();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }

        public static void RefineFeedbackPresentation()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("복사 씬 편집 모드가 필요합니다.");
            module = Object.FindFirstObjectByType<EscapePursuerRoute>().transform;
            frame = module.GetComponentsInParent<Transform>().Single(t => t.name == "EscapeCampaignFrame");
            structure = frame.Find(FeedbackRoot);
            concrete = frame.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name == "타설 콘크리트 벽").sharedMaterial;
            var metal = frame.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name == "작업등 함체").sharedMaterial;
            var lens = frame.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name == "작업등 광원").sharedMaterial;
            foreach (var t in frame.GetComponentsInChildren<Transform>(true).Where(t =>
            {
                var p = frame.InverseTransformPoint(t.position);
                return t.name.StartsWith("Switchback amber") || t.name.StartsWith("Switchback worklight") || t.name.StartsWith("Recessed stair") || t.name.StartsWith("Cannon observation lamp") ||
                    p.x >= 119 && p.x <= 129 && p.z >= 32 && p.z <= 49 && p.y > -8 && p.y < 8 &&
                    (t.name.Contains("작업등") || t.name.Contains("정비등"));
            }).ToArray()) if (t) Object.DestroyImmediate(t.gameObject);
            if (!structure.Find("Lower landing west lamp support"))
                Box("Lower landing west lamp support", new(119.75f,-2.8f,33.5f), new(.5f,3.6f,3));
            foreach (var p in new[] { new Vector3(120.1f,1.8f,35.5f), new Vector3(129.4f,.5f,42.5f),
                new Vector3(120.1f,-1.7f,33.5f), new Vector3(129.4f,-3.5f,43), new Vector3(129.4f,-5.5f,49.7f) })
            {
                float side = p.x < 125 ? 1 : -1;
                Box("Recessed stair worklight housing", p, new(.2f,.25f,.45f)).GetComponent<MeshRenderer>().sharedMaterial = metal;
                Box("Recessed stair luminous lens", p + Vector3.right * side * .11f, new(.02f,.16f,.32f)).GetComponent<MeshRenderer>().sharedMaterial = lens;
                var lamp = new GameObject("Switchback worklight").AddComponent<Light>();
                lamp.transform.SetParent(structure, false); lamp.transform.position = frame.TransformPoint(p + Vector3.right * side * .25f);
                lamp.type = LightType.Spot; lamp.spotAngle = 130; lamp.innerSpotAngle = 45;
                lamp.transform.rotation = Quaternion.LookRotation(frame.TransformDirection(new Vector3(side*2,-2,p.x<125?2:-.5f)));
                lamp.color = new Color(.96f,.88f,.75f); lamp.intensity = 8; lamp.range = 8.5f;
                lamp.shadows = LightShadows.Soft;
                lamp.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Low;
                SetLocalShadowTier(lamp, 0);
            }
            Box("Cannon observation lamp housing", new(70.73f,-16,12), new(.18f,.35f,.65f)).GetComponent<MeshRenderer>().sharedMaterial = metal;
            Box("Cannon observation lamp lens", new(70.63f,-16,12), new(.02f,.24f,.52f)).GetComponent<MeshRenderer>().sharedMaterial = lens;
            var beam = new GameObject("Cannon observation lamp beam").AddComponent<Light>();
            beam.transform.SetParent(structure,false);
            beam.transform.position = frame.TransformPoint(new Vector3(70.5f,-16,12));
            beam.transform.rotation = Quaternion.LookRotation(frame.TransformPoint(new Vector3(65,-17,-15))-beam.transform.position);
            beam.type = LightType.Spot; beam.spotAngle = 100; beam.innerSpotAngle = 20; beam.intensity = 220;
            beam.range = 55; beam.color = new Color(.9f,.94f,1); beam.shadows = LightShadows.Soft;
            SetLocalShadowTier(beam, 1);
            RemoveArtificialLightCone();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static void SetLocalShadowTier(Light lamp, int tier)
        {
            var extra = lamp.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            if (!extra) extra = lamp.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            var data = new SerializedObject(extra);
            data.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = tier;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void RemoveArtificialLightCone()
        {
            const string path = "Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRoutePursuitRemodelDust.mat";
            var views = frame.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.sharedMaterials.Any(m =>
                m && m.shader && m.shader.name == "CurtainCall/Escape/Pendant Light Shaft")).ToArray();
            if (views.Length == 0) return;
            var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/Levels/EscapeFacilityMotion/pendant-dust.mat");
            foreach (var view in views)
            {
                if (original) view.sharedMaterials = view.sharedMaterials.Select(m => m && m.shader && m.shader.name == "CurtainCall/Escape/Pendant Light Shaft" ? original : m).ToArray();
                view.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view.gameObject);
            }
            // Remove only the temporary material created for this isolated lighting revision.
            if (original && AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
        }
    }
}
