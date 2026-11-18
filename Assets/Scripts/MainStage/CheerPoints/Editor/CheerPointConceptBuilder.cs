#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using CurtainCall.UI.SceneIntegration;

namespace CurtainCall.MainStage.CheerPoints.Editor
{
    /// <summary>Reproducible visual proposals. No gameplay reward/damage components are added.</summary>
    public static class CheerPointConceptBuilder
    {
        public const string Folder = "Assets/_ThirdParty/curtaincall/CheerPointConcepts";
        public const string Gallery = "Assets/Scenes/Tests/MainStage/CheerPoints/CheerPointGallery.unity";
        const string Synty = "Assets/_ThirdParty/Synty/PolygonHorrorCarnival/Prefabs/";
        const string Background = "Assets/_ThirdParty/curtaincall/PerformanceBackground/Scenes/performance_background.unity";
        static readonly string[] Names = { "A_FireHoop", "B_EncoreHalo", "C_MaskApparition" };

        [MenuItem("CurtainCall/Performance/Build Hanging Fire Hoop")]
        public static void BuildHangingFireHoop()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop play before authoring.");
            const string generic="Assets/_ThirdParty/Synty/PolygonGeneric/Prefabs/Props/";
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(generic+"SM_Gen_Prop_Rope_01.prefab");
            var mesh=source.GetComponentInChildren<MeshFilter>().sharedMesh;
            // A separate readable copy allows one combined rope renderer in a built player.
            // The licensed mesh remains inside the private asset repository.
            string meshPath=Folder+"/HangingRope.asset";
            var readable=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(!readable) { readable=new Mesh(); AssetDatabase.CreateAsset(readable,meshPath); }
            readable.Clear(); readable.name="HangingRope"; readable.vertices=mesh.vertices;
            readable.normals=mesh.normals; readable.tangents=mesh.tangents; readable.uv=mesh.uv; readable.triangles=mesh.triangles;
            readable.RecalculateBounds(); EditorUtility.SetDirty(readable); AssetDatabase.SaveAssetIfDirty(readable);
            string materialPath=Folder+"/HempRope.mat";
            var rope=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!rope) { rope=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(rope,materialPath); }
            var sourceMaterial=source.GetComponentInChildren<Renderer>().sharedMaterial;
            rope.SetTexture("_BaseMap",sourceMaterial.GetTexture("_Albedo_Map"));
            rope.SetTexture("_BumpMap",sourceMaterial.GetTexture("_Normal_Map")); rope.EnableKeyword("_NORMALMAP");
            rope.SetColor("_BaseColor",new Color(.82f,.73f,.59f)); rope.SetFloat("_Metallic",0); rope.SetFloat("_Smoothness",.12f);
            EditorUtility.SetDirty(rope); AssetDatabase.SaveAssetIfDirty(rope);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/A_FireHoop.prefab"));
            try
            {
                PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
                root.name="A_HangingFireHoop";
                var hoop=root.GetComponentInChildren<MeshRenderer>();
                var hoopFilter=hoop.GetComponent<MeshFilter>();
                var hoopSource=hoopFilter.sharedMesh;
                string hoopMeshPath=Folder+"/HangingHoop.asset";
                var hoopMesh=AssetDatabase.LoadAssetAtPath<Mesh>(hoopMeshPath);
                if(!hoopMesh) { hoopMesh=new Mesh(); AssetDatabase.CreateAsset(hoopMesh,hoopMeshPath); }
                hoopMesh.Clear(); hoopMesh.name="HangingHoop"; hoopMesh.vertices=hoopSource.vertices;
                hoopMesh.normals=hoopSource.normals; hoopMesh.tangents=hoopSource.tangents; hoopMesh.uv=hoopSource.uv; hoopMesh.triangles=hoopSource.triangles;
                hoopMesh.RecalculateBounds(); EditorUtility.SetDirty(hoopMesh); AssetDatabase.SaveAssetIfDirty(hoopMesh);
                hoopFilter.sharedMesh=hoopMesh;
                var motion=root.GetComponent<CheerPointVisualPreview>(); motion.floatHeight=.018f; motion.tiltDegrees=.8f; motion.lightIntensity=1.6f;
                foreach(var ps in root.GetComponentsInChildren<ParticleSystem>())
                {
                    ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    bool fire=ps.name=="Fire";
                    ps.transform.localPosition=new Vector3(0,0,-.02f);
                    var main=ps.main; main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                    main.startLifetime=new ParticleSystem.MinMaxCurve(fire?.5f:.7f,fire?.8f:1.3f);
                    main.startSize=new ParticleSystem.MinMaxCurve(fire?.28f:.015f,fire?.52f:.035f);
                    main.startColor=fire ? new Color(1f,.55f,.19f,.8f) : new Color(1f,.46f,.08f,.75f);
                    var emission=ps.emission; emission.rateOverTime=fire?150:9;
                    var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.MeshRenderer;
                    shape.meshRenderer=hoop; shape.meshShapeType=ParticleSystemMeshShapeType.Triangle;
                    var velocity=ps.velocityOverLifetime; velocity.y=fire?.48f:1.1f;
                    var noise=ps.noise; noise.enabled=true; noise.strength=.12f; noise.frequency=.7f; noise.scrollSpeed=.35f;
                    var size=ps.sizeOverLifetime; size.enabled=true;
                    size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,.25f));
                }
                string path=Folder+"/A_HangingFireHoop.prefab";
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);
                var settings=CurtainCall.Settings.GameSettings.PerformanceEquipment;
                settings.cheerVisuals[0]=prefab; settings.cheerRopeMesh=readable;
                settings.cheerRopeKnot=AssetDatabase.LoadAssetAtPath<GameObject>(generic+"SM_Gen_Prop_Rope_Knot_01.prefab");
                settings.cheerRopeMaterial=rope; settings.cheerRopeWidth=.075f;
                EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        public static string Inspect()
        {
            var paths = new[] { "Props/SM_Prop_Hoops_Hoola_01.prefab", "Props/SM_Prop_Hoops_Small_01.prefab", "FX/FX_Fire_01.prefab", "FX/FX_Sparks_01.prefab" };
            return string.Join("\n", paths.Select(p => {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Synty + p);
                if (!asset) return p + " MISSING";
                return p + ": " + string.Join(", ", asset.GetComponentsInChildren<Renderer>(true).Select(r => r.name + " size=" + r.bounds.size + " materials=" + string.Join("/", r.sharedMaterials.Select(m => m ? m.name + "(" + m.shader.name + ")" : "NULL"))));
            }));
        }

        [MenuItem("CurtainCall/Preview/Build Cheer Point Concepts")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before authoring.");
            EnsureFolder(Folder);
            EnsureFolder("Assets/Scenes/Tests/MainStage/CheerPoints");
            var metal = Material("AgedBrass", new Color(.32f, .18f, .075f), .65f);
            var iron = Material("RiggingIron", new Color(.065f, .071f, .085f), .75f);
            var gold = Material("EncoreGold", new Color(.8f, .38f, .055f), .4f, new Color(1.8f, .55f, .07f));
            for (int i = 0; i < Names.Length; i++)
            {
                var root = new GameObject(Names[i]);
                try
                {
                    var visual = new GameObject("Visual").transform; visual.SetParent(root.transform, false);
                    var motion = root.AddComponent<CheerPointVisualPreview>(); motion.visual = visual;
                    motion.floatHeight = i == 2 ? .12f : .045f; motion.tiltDegrees = i == 2 ? 5f : 1.5f;
                    var hoop = PlaceModel("Props/SM_Prop_Hoops_Hoola_01.prefab", visual, 3.05f, true);
                    foreach (var renderer in hoop.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = i == 1 ? gold : metal;
                    // A floating performance target: no beam/cables that resemble an interactable swing.
                    if (i == 0)
                    {
                        RingParticles(visual, "Fire", "FX/FX_Fire_01.prefab", 1.47f, 170f, 1f, .72f, new Color(2f, 1.1f, .3f), false);
                        RingParticles(visual, "EscapingEmbers", "FX/FX_Sparks_01.prefab", 1.48f, 24f, 1.2f, .035f, new Color(1f, .65f, .12f), false);
                    }
                    else if (i == 1)
                    {
                        RingParticles(visual, "OrbitingGold", "FX/FX_Sparks_01.prefab", 1.6f, 110f, 1.1f, .08f, new Color(1f, .68f, .19f), true);
                        RingParticles(visual, "FallingGoldDust", "FX/FX_Sparks_01.prefab", 1.66f, 35f, 1.5f, .05f, new Color(1f, .46f, .08f), false);
                    }
                    else
                    {
                        hoop.transform.localScale *= .78f;
                        var mask = PlaceModel("Characters/Attachments/SM_Chr_Attach_Clown_Female_Mask_01.prefab", visual, 1.35f, false);
                        mask.transform.localPosition = new Vector3(0, .03f, -.05f);
                        mask.transform.localRotation = Quaternion.Euler(0, 180, 0);
                        RingParticles(visual, "CrimsonApparition", "FX/FX_Fire_01.prefab", 1.13f, 140f, 1f, .48f, new Color(1.8f, .08f, .12f), true);
                        RingParticles(visual, "GoldMotes", "FX/FX_Sparks_01.prefab", 1.16f, 30f, 1.5f, .035f, new Color(1f, .48f, .13f), true);
                    }
                    var lightGo = new GameObject("WarmAccent"); lightGo.transform.SetParent(visual, false); lightGo.transform.localPosition = new Vector3(0, 0, -.45f);
                    var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.range = 5f;
                    light.color = i == 2 ? new Color(1, .15f, .12f) : new Color(1, .48f, .12f); light.intensity = i == 0 ? 3f : 1.5f; light.shadows = LightShadows.None;
                    motion.accent = light; motion.lightIntensity = light.intensity;
                    foreach (var collider in root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                    PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + Names[i] + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            return "Created 3 visual-only prefabs in " + Folder;
        }

        static GameObject PlaceModel(string path, Transform parent, float size, bool upright)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Synty + path);
            if (!asset) throw new InvalidOperationException("Missing source " + path);
            var wrapper = new GameObject(Path.GetFileNameWithoutExtension(path)); wrapper.transform.SetParent(parent, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset); model.transform.SetParent(wrapper.transform, false);
            Bounds bounds = BoundsOf(model);
            if (upright && bounds.size.y < Mathf.Min(bounds.size.x, bounds.size.z)) model.transform.localRotation = Quaternion.Euler(90, 0, 0);
            else if (upright && bounds.size.x < Mathf.Min(bounds.size.y, bounds.size.z)) model.transform.localRotation = Quaternion.Euler(0, 90, 0);
            bounds = BoundsOf(model);
            float scale = size / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            model.transform.localScale *= scale;
            bounds = BoundsOf(model); model.transform.position += wrapper.transform.position - bounds.center;
            return wrapper;
        }

        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("No renderers: " + go.name);
            var bounds = renderers[0].bounds; foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds); return bounds;
        }

        static void RingParticles(Transform parent, string name, string source, float radius, float rate, float life, float size, Color color, bool orbit)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Synty + source));
            go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(0,0,-.18f); go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main; main.loop = true; main.prewarm = true; main.duration = 3f; main.playOnAwake = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(life * .65f, life); main.startSize = new ParticleSystem.MinMaxCurve(size * .65f, size * 1.3f);
                main.startSpeed = 0; main.startColor = color; main.maxParticles = 250; main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var limit = ps.limitVelocityOverLifetime; limit.enabled = false;
                var emission = ps.emission; emission.enabled = true; emission.rateOverTime = rate; emission.rateOverDistance = 0; emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
                var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = radius; shape.radiusThickness = .015f; shape.arc = 360; shape.rotation = Vector3.zero; shape.position = Vector3.zero; shape.scale = Vector3.one;
                var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = 0; velocity.y = orbit ? 0 : .42f; velocity.z = 0; velocity.orbitalX = 0; velocity.orbitalY = 0; velocity.orbitalZ = orbit ? .8f : 0;
                var gravity = ps.main; gravity.gravityModifier = 0;
                var colorLife = ps.colorOverLifetime; colorLife.enabled = true;
                var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(.8f, .55f), new GradientAlphaKey(0, 1) }); colorLife.color = gradient;
                var collision = ps.collision; collision.enabled = false;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                bool flames = source.Contains("Fire");
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sharedMaterial = ParticleMaterial(renderer.sharedMaterial, flames);
                if (flames)
                {
                    var sheet = ps.textureSheetAnimation; sheet.enabled = true; sheet.mode = ParticleSystemAnimationMode.Grid;
                    sheet.numTilesX = 10; sheet.numTilesY = 10; sheet.animation = ParticleSystemAnimationType.WholeSheet;
                    sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0, 1, 1)); sheet.cycleCount = 1;
                    main.startRotation = new ParticleSystem.MinMaxCurve(-.12f, .12f);
                }
                ps.useAutoRandomSeed = false; ps.randomSeed = (uint)(17 + name.Length);
            }
        }

        static Material ParticleMaterial(Material source, bool flame)
        {
            string path = Folder + (flame ? "/FlameFlipbook.mat" : "/GoldSpark.mat");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(material, path); }
            Texture texture = flame ? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_ThirdParty/Synty/PolygonGeneric/Textures/FX/Fire/Gen_FireFrames_Grey_01.png") : source.mainTexture;
            material.SetTexture("_BaseMap", texture); material.SetTexture("_MainTex", texture);
            material.SetColor("_BaseColor", flame ? Color.white : new Color(3,3,3,1)); material.SetColor("_Color", Color.white);
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 2);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = 3000;
            EditorUtility.SetDirty(material); return material;
        }

        static void Beam(Transform parent, Vector3 a, Vector3 b, float width, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = "Suspension"; go.transform.SetParent(parent, false);
            go.transform.localPosition = (a + b) * .5f; go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a); go.transform.localScale = new Vector3(width, Vector3.Distance(a, b) * .5f, width);
            go.GetComponent<Renderer>().sharedMaterial = material; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        static Material Material(string name, Color color, float metallic, Color? emission = null)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", .45f);
            if (emission.HasValue) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", emission.Value); }
            EditorUtility.SetDirty(material); return material;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [MenuItem("CurtainCall/Preview/Open Cheer Point Gallery")]
        public static void OpenGalleryMenu() => Debug.Log(CreateGallery());

        public static string CreateGallery()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before authoring.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Open scenes have unsaved changes.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bgScene = EditorSceneManager.OpenScene(Background, OpenSceneMode.Additive);
            var source = bgScene.GetRootGameObjects().First(g => g.name == "PerformanceBackground");
            var background = UnityEngine.Object.Instantiate(source); background.name = "PerformanceBackground_PreviewCopy";
            SceneManager.MoveGameObjectToScene(background, scene);
            background.transform.localScale = Vector3.one / 3f; background.transform.position = new Vector3(0, 0, 55.833333f);
            EditorSceneManager.CloseScene(bgScene, true); SceneManager.SetActiveScene(scene);
            foreach (var camera in background.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            for (int i = 0; i < Names.Length; i++)
            {
                var item = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + Names[i] + ".prefab"), scene);
                item.transform.position = new Vector3((i - 1) * 5f, 2.2f, 50);
            }
            var cameraGo = new GameObject("ConceptCamera"); var cam = cameraGo.AddComponent<Camera>(); cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0, 3.5f, 35); cameraGo.transform.LookAt(new Vector3(0, 2.25f, 50));
            cam.fieldOfView = 52; cam.nearClipPlane = .1f; cam.farClipPlane = 1000; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.016f, .021f, .034f);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var fill = new GameObject("PreviewSoftbox").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .35f; fill.color = new Color(.8f, .86f, 1); fill.transform.rotation = Quaternion.Euler(35, -25, 0);
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.085f, .12f, .19f); RenderSettings.ambientEquatorColor = new Color(.065f, .075f, .11f); RenderSettings.ambientGroundColor = new Color(.16f, .061f, .026f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .00396f; RenderSettings.fogColor = cam.backgroundColor;
            var volume = new GameObject("ConceptBloom").AddComponent<Volume>(); volume.isGlobal = true;
            string profilePath = Folder + "/ConceptBloom.asset"; var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); var bloom = profile.Add<Bloom>(); bloom.intensity.Override(.65f); bloom.threshold.Override(.8f); AssetDatabase.AddObjectToAsset(bloom, profile); }
            if (!profile.TryGet<ColorAdjustments>(out var grading)) { grading = profile.Add<ColorAdjustments>(); AssetDatabase.AddObjectToAsset(grading, profile); }
            grading.postExposure.Override(-1.1f); grading.saturation.Override(-12f); EditorUtility.SetDirty(profile);
            volume.sharedProfile = profile;
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene, Gallery);
            return "Saved gallery with actual performance background copy: " + Gallery;
        }

        public static string Capture(string directory, int concept = -1, float seconds = 2.3f)
        {
            Directory.CreateDirectory(directory);
            var cam = GameObject.Find("ConceptCamera").GetComponent<Camera>();
            for (int i = 0; i < Names.Length; i++)
            {
                var root = GameObject.Find(Names[i]);
                if (!root) root = Resources.FindObjectsOfTypeAll<GameObject>().First(g => g.name == Names[i] && g.scene.IsValid());
                root.SetActive(concept < 0 || concept == i);
                if (!root.activeSelf) continue;
                root.GetComponent<CheerPointVisualPreview>().Sample(seconds);
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>()) ps.Simulate(seconds, false, true, true);
            }
            float x = concept < 0 ? 0 : (concept - 1) * 5f;
            cam.transform.position = new Vector3(x + (concept < 0 ? 0 : .3f), 3.1f, concept < 0 ? 34 : 42.8f);
            cam.transform.LookAt(new Vector3(x, 2.45f, 50)); cam.fieldOfView = concept < 0 ? 52 : 48;
            string path = Path.Combine(directory, concept < 0 ? "cheer-hoops-comparison.png" : Names[concept] + ".png");
            Render(cam, path); return path;
        }

        public static void Render(Camera camera, string path)
        {
            var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
            var rt = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(texture); }
        }

        public static async Task<string> CaptureMotion(string directory)
        {
            string frames = Path.Combine(directory, "hoop-frames"); Directory.CreateDirectory(frames);
            for (int frame=0; frame<48; frame++)
            {
                string capture = Capture(directory, -1, 3f + frame / 12f);
                File.Copy(capture, Path.Combine(frames, frame.ToString("D3")+".png"), true);
                await Task.Delay(20);
            }
            Capture(directory, -1, 2.3f);
            File.WriteAllText(Path.Combine(frames,"complete.txt"), "48 Unity-rendered frames / 12 fps");
            return frames;
        }

        public static string UpdateInventoryTemplate()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/UI/CommonMenuUI.unity", OpenSceneMode.Additive);
            try
            {
                var effects = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<InventorySlotUseEffect>(true)).ToArray();
                if (effects.Length == 0) throw new InvalidOperationException("No inventory effect template found.");
                foreach (var effect in effects)
                {
                    var so = new SerializedObject(effect);
                    if (!so.FindProperty("effectMaterial").objectReferenceValue) throw new InvalidOperationException("Inventory effect material is missing.");
                    so.FindProperty("breakSeconds").floatValue = 1.05f; so.FindProperty("breakRise").floatValue = 25f;
                    so.FindProperty("breakGhostScale").floatValue = 1.2f; so.FindProperty("breakGhostSpin").floatValue = -9f;
                    so.FindProperty("useIconPunch").floatValue = .2f; so.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.SaveScene(scene); return "Updated " + effects.Length + " shared inventory template(s).";
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
#endif
