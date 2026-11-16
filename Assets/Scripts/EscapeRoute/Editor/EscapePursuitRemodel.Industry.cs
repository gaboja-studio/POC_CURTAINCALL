using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapePursuitRemodel
    {
        const string IndustryRoot = "Industrial processing vignettes";
        const string IndustryGeometry = "Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRoutePursuitRemodelIndustryGeometry.asset";
        const string IndustryArt = "Assets/Scenes/Levels/EscapeFactoryArt/";
        const string IndustryMotion = "Assets/Scenes/Levels/EscapeFacilityMotion/";
        const float IndustryLength = 16f, IndustryCut = 9.5f, IndustrySpeed = 1.2f;

        [MenuItem("Tools/CurtainCall/탈출로/복사 씬 산업 공포 연출 추가")]
        public static void AddIndustrialVignettes()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath)
                throw new InvalidOperationException("복사 씬의 편집 모드에서만 산업 연출을 추가하세요.");
            var route = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EscapePursuerRoute>(true)).Single();
            var campaign = route.GetComponentsInParent<Transform>().Single(t => t.name == "EscapeCampaignFrame");
            if (campaign.Find(IndustryRoot)) return;
            var source = campaign.GetComponentsInChildren<EscapeConveyorProcession>(true).Single(p => p.name == "58_ProcessingLine");
            var sourceData = new SerializedObject(source);
            var sourceCarriers = sourceData.FindProperty("carriers");
            int[] selected = { 0, 2 };
            if (sourceCarriers.arraySize <= selected.Max()) throw new InvalidOperationException("기존 장식 운반체가 부족합니다.");
            foreach (int index in selected)
            {
                var row = sourceCarriers.GetArrayElementAtIndex(index);
                foreach (string name in new[] { "hanger", "load", "costume", "separated" })
                    if (!row.FindPropertyRelative(name).objectReferenceValue) throw new InvalidOperationException("기존 절단 연출 참조 누락: " + name);
                if (row.FindPropertyRelative("pieces").arraySize != 5) throw new InvalidOperationException("베이크된 몸통과 사지 5개가 필요합니다.");
            }
            var steel = IndustryAsset<Material>(IndustryArt + "factory-steel.mat");
            var dark = IndustryAsset<Material>(IndustryArt + "factory-recess.mat");
            var ochre = IndustryAsset<Material>(IndustryArt + "factory-safety-ochre.mat");
            var pipe = IndustryAsset<Material>(IndustryArt + "factory-pipe.mat");
            var wall = IndustryAsset<Material>(IndustryArt + "factory-concrete.mat");
            var intakeMesh = IndustryAsset<Mesh>(IndustryMotion + "processing-intake.asset");
            var liquid = IndustryAsset<Material>(IndustryMotion + "settled-liquor.mat");
            var vapor = IndustryAsset<Material>(IndustryMotion + "pressure-vapor.mat");
            var bubbles = IndustryBubbleMaterial();
            var sparkMaterial = IndustryAsset<Material>("Assets/Resources/Prefabs/Objects/ETCs/TightropeArtKit/SawSpark.mat");
            var bloodMaterial = IndustryAsset<Material>("Assets/Resources/Effects/Blood/BloodParticle.mat");
            var sawTemplate = IndustryAsset<GameObject>("Assets/Resources/Prefabs/Objects/ETCs/PerformanceArtKit/SawDisc.prefab");

            var installation = IndustryNode(campaign, IndustryRoot, Vector3.zero);
            installation.gameObject.SetActive(false);
            try
            {
                // This floor begins nine metres behind the moving pole line. It never fills the traversal abyss.
                IndustryBox(installation, "Processing bay foundation", new(94, -26.3f, -30), new(20, .6f, 12), wall, true);
                IndustryBox(installation, "Processing bay rear wall", new(94, -17, -35.7f), new(20, 18, .6f), wall, true);
                IndustryBox(installation, "Processing bay west wall", new(84.3f, -17, -30), new(.6f, 18, 12), wall, true);
                IndustryBox(installation, "Processing bay east wall", new(103.7f, -17, -30), new(.6f, 18, 12), wall, true);
                IndustryBox(installation, "Processing bay ceiling", new(94, -8.3f, -30), new(20, .6f, 12), wall, true);

                var line = IndustryNode(installation, "Compact body processing line", new(86, -13, -31));
                var procession = line.gameObject.AddComponent<EscapeConveyorProcession>();
                var data = new SerializedObject(procession);
                var carriers = data.FindProperty("carriers"); carriers.arraySize = selected.Length;
                var cuts = new List<float>();
                for (int i = 0; i < selected.Length; i++)
                {
                    var original = sourceCarriers.GetArrayElementAtIndex(selected[i]);
                    var target = carriers.GetArrayElementAtIndex(i);
                    var map = new Dictionary<Transform, Transform>();
                    var originalHanger = (Transform)original.FindPropertyRelative("hanger").objectReferenceValue;
                    var originalLoad = (GameObject)original.FindPropertyRelative("load").objectReferenceValue;
                    var originalSeparated = (Transform)original.FindPropertyRelative("separated").objectReferenceValue;
                    var hanger = IndustryCloneVisual(originalHanger, line, map); hanger.name = "Body carrier " + (i + 1);
                    var load = map.TryGetValue(originalLoad.transform, out var copiedLoad)
                        ? copiedLoad : IndustryCloneVisual(originalLoad.transform, line, map);
                    if (load.parent != hanger && load.parent != line)
                        throw new InvalidOperationException("장식 운반체의 예상하지 못한 부모 구조입니다.");
                    var separated = IndustryCloneVisual(originalSeparated, line, map);
                    separated.name = "Separated body pieces " + (i + 1);
                    float firstContact = IndustryCut - (sourceData.FindProperty("cutAt").floatValue - original.FindPropertyRelative("firstContact").floatValue);
                    float initialX = 2 + i * IndustryLength / selected.Length;
                    hanger.localPosition = Vector3.right * initialX;
                    load.localPosition = load.parent == hanger ? Vector3.zero : Vector3.right * initialX;
                    load.gameObject.SetActive(initialX < firstContact);
                    separated.localPosition = Vector3.right * firstContact;
                    separated.localRotation = Quaternion.identity;
                    separated.gameObject.SetActive(false);
                    target.FindPropertyRelative("hanger").objectReferenceValue = hanger;
                    target.FindPropertyRelative("opposite").objectReferenceValue = null;
                    target.FindPropertyRelative("load").objectReferenceValue = load.gameObject;
                    var originalCostume = (Renderer)original.FindPropertyRelative("costume").objectReferenceValue;
                    target.FindPropertyRelative("costume").objectReferenceValue = map[originalCostume.transform].GetComponent<MeshRenderer>();
                    target.FindPropertyRelative("outfitColor").colorValue = original.FindPropertyRelative("outfitColor").colorValue;
                    target.FindPropertyRelative("outfitPattern").intValue = original.FindPropertyRelative("outfitPattern").intValue;
                    target.FindPropertyRelative("firstContact").floatValue = firstContact;
                    target.FindPropertyRelative("separated").objectReferenceValue = separated;
                    var originalPieces = original.FindPropertyRelative("pieces");
                    var pieces = target.FindPropertyRelative("pieces"); pieces.arraySize = originalPieces.arraySize;
                    for (int j = 0; j < pieces.arraySize; j++)
                    {
                        var originalPiece = originalPieces.GetArrayElementAtIndex(j);
                        var renderer = (MeshRenderer)originalPiece.FindPropertyRelative("renderer").objectReferenceValue;
                        var copied = map[renderer.transform].GetComponent<MeshRenderer>();
                        Vector3 center = originalPiece.FindPropertyRelative("hangingCenter").vector3Value;
                        copied.enabled = true; copied.transform.localPosition = center; copied.transform.localRotation = Quaternion.identity;
                        var piece = pieces.GetArrayElementAtIndex(j);
                        piece.FindPropertyRelative("renderer").objectReferenceValue = copied;
                        piece.FindPropertyRelative("hangingCenter").vector3Value = center;
                    }
                    cuts.Add(Mathf.Repeat((firstContact - 2 - i * IndustryLength / selected.Length) / IndustryLength, 1));
                }
                var oldSaw = (Transform)sourceData.FindProperty("saw").objectReferenceValue;
                var saw = IndustryCloneVisual(oldSaw, line, new Dictionary<Transform, Transform>());
                saw.name = "Processing cutting wheel";
                saw.localPosition = new Vector3(IndustryCut, oldSaw.localPosition.y, oldSaw.localPosition.z);
                saw.localRotation = Quaternion.identity; // Same initial pose as the cosmetic Present(0) timeline.
                data.FindProperty("saw").objectReferenceValue = saw;
                data.FindProperty("trackLength").floatValue = IndustryLength;
                data.FindProperty("cutAt").floatValue = IndustryCut;
                data.FindProperty("speed").floatValue = IndustrySpeed;
                data.FindProperty("playbackSpeed").floatValue = 1;
                Vector3 intake = sourceData.FindProperty("intake").vector3Value;
                intake.x += IndustryCut - sourceData.FindProperty("cutAt").floatValue;
                data.FindProperty("intake").vector3Value = intake;
                data.ApplyModifiedPropertiesWithoutUndo();
                procession.RefreshOutfits();

                IndustryBox(line, "Overhead carrier rail", new(8, .28f, 0), new(18, .38f, .35f), steel);
                foreach (float x in new[] { -.6f, 17.1f })
                {
                    IndustryBox(line, "Mill side column", new(x, -6.55f, -.9f), new(.3f, 13.1f, .35f), steel);
                    IndustryBox(line, "Carrier roof suspension", new(x, 2.3f, 0), new(.18f, 4.2f, .18f), steel);
                    IndustryBox(line, "Rail column brace", new(x, 0, -.45f), new(.42f, .45f, 1.25f), steel);
                }
                IndustryCabinet(line, "Covered feed entry", -.9f, 3.5f, steel, dark);
                IndustryCabinet(line, "Covered empty return", 12.8f, 17.3f, steel, dark);
                var hopper = IndustryNode(line, "Visible cut-piece receiving hopper", new(intake.x, -6.22f, intake.z));
                IndustryMesh(hopper, intakeMesh, new[] { steel, ochre, dark });
                foreach (float x in new[] { intake.x - 3.03f, intake.x + 3.03f })
                    foreach (float z in new[] { intake.z - 1.73f, intake.z + 1.73f })
                        IndustryBox(line, "Hopper foundation leg", new(x, -9.6f, z), new(.15f, 6.8f, .15f), steel);
                IndustryBox(line, "Front intake cross brace", new(intake.x, -9.9f, intake.z + 1.76f), new(6.3f, .2f, .16f), steel);

                var effects = line.gameObject.AddComponent<EscapeIndustrialVignette>();
                effects.cycleSeconds = IndustryLength / IndustrySpeed;
                var emissions = new List<EscapeIndustrialVignette.EmissionWindow>();
                var sparks = IndustryParticles(line, "Cut contact sparks", new(8.7f, -2.1f, .42f), sparkMaterial,
                    new Color(.94f, .83f, .58f, 1), 64, 75, .35f, 3.5f, .035f, 1f);
                sparks.transform.localRotation = Quaternion.Euler(0, -90, 0);
                var blood = IndustryParticles(line, "Cut contact blood spray", new(8.5f, -2.4f, .45f), bloodMaterial,
                    new Color(.38f, .035f, .025f, .9f), 40, 48, .38f, 2.2f, .075f, 1.5f);
                blood.transform.localRotation = Quaternion.Euler(35, -80, 0);
                foreach (float cut in cuts)
                {
                    IndustryWindow(emissions, sparks, cut, 1.35f / effects.cycleSeconds);
                    IndustryWindow(emissions, blood, cut + .04f / effects.cycleSeconds, .55f / effects.cycleSeconds);
                }
                var grinderMount = IndustryNode(line, "Auxiliary wheel spindle mount", new(12, -3, .6f));
                grinderMount.localRotation = Quaternion.Euler(90, 0, 0);
                var grinder = IndustryCloneVisual(sawTemplate.transform, grinderMount, new Dictionary<Transform, Transform>());
                grinder.localPosition = Vector3.zero; grinder.localRotation = Quaternion.identity; grinder.localScale = Vector3.one * 1.8f;
                var pivot = grinder.Find("RotationPivot");
                effects.rotors = new[] { new EscapeIndustrialVignette.Rotor { target = pivot, localAxis = Vector3.up, degreesPerSecond = 430 } };
                IndustryBox(line, "Auxiliary wheel fixed spindle", new(12, -3, -.1f), new(.32f, .32f, 1.6f), steel);
                IndustryBox(line, "Auxiliary grinder pedestal", new(12, -8.1f, -.75f), new(.6f, 9.8f, .65f), steel);
                var grinding = IndustryParticles(line, "Metal edge grinding sparks", new(11.1f, -3.45f, .7f), sparkMaterial,
                    new Color(.95f, .9f, .72f, 1), 40, 34, .25f, 2.6f, .025f, 1.2f);
                grinding.transform.localRotation = Quaternion.Euler(55, -90, 0);
                IndustryWindow(emissions, grinding, .12f, .32f);
                effects.emissions = emissions.ToArray();
                var impactClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Sounds/EscapeRouteAudio/SawLaunch.wav");
                if (impactClip)
                {
                    var audio = IndustryNode(line, "Quiet cutting contact", new(IndustryCut, -2, 0)).gameObject.AddComponent<AudioSource>();
                    audio.clip = impactClip; audio.playOnAwake = false; audio.loop = false; audio.spatialBlend = 1;
                    audio.volume = .075f; audio.minDistance = 2; audio.maxDistance = 35; audio.rolloffMode = AudioRolloffMode.Linear;
                    effects.cycleAudio = audio; effects.audioPhase = cuts[0];
                }
                AddIndustrialLoop(line, "Cutting mill motor", new(IndustryCut, -3, 0), false);
                BuildIndustryVat(installation, steel, pipe, dark, ochre, liquid, vapor, bubbles);
                var lens = IndustryAsset<Material>(IndustryMotion + "pendant-bulb.mat");
                effects.pulseLight = IndustryWorklight(installation, "Processing neutral worklight", new(94, -9, -26),
                    new(94, -8.6f, -26), new(94.5f, -16.5f, -31), 480, 20, steel, lens);
                effects.lightPulseFraction = .025f;
                var vatEffects = installation.Find("Open boiling residue vat").GetComponent<EscapeIndustrialVignette>();
                vatEffects.pulseLight = IndustryWorklight(installation, "Vat neutral worklight", new(103.2f, -18, -33),
                    new(103.4f, -18, -33), new(100.7f, -22.2f, -32), 180, 11, steel, lens);
                vatEffects.lightPulseFraction = .02f;

                // Complete the compact visual graph before opening only the copied scene's concrete.
                EscapePursuerConcreteRoutes.OpenPhysicalVolume(campaign, route.transform, installation,
                    new Bounds(new Vector3(94, -17, -30), new Vector3(22, 18, 12)));
                installation.gameObject.SetActive(true);
                RefineIndustrialPresentation();
                Physics.SyncTransforms();
                var geometry = AssetDatabase.LoadMainAssetAtPath(IndustryGeometry);
                if (geometry) AssetDatabase.SaveAssetIfDirty(geometry);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            catch
            {
                Object.DestroyImmediate(installation.gameObject);
                throw;
            }
        }

        public static void RefineIndustrialPresentation()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath)
                throw new InvalidOperationException("복사 씬 편집 모드가 필요합니다.");
            var route = Object.FindFirstObjectByType<EscapePursuerRoute>();
            var campaign = route.GetComponentsInParent<Transform>().Single(t => t.name == "EscapeCampaignFrame");
            var installation = campaign.Find(IndustryRoot);
            if (!installation) return;
            var line = installation.Find("Compact body processing line");
            var steel = IndustryAsset<Material>(IndustryArt + "factory-steel.mat");
            var pipe = IndustryAsset<Material>(IndustryArt + "factory-pipe.mat");
            void BoxAt(Transform parent, string name, Vector3 at, Vector3 size)
            {
                var t = parent.Find(name);
                if (!t) t = IndustryBox(parent, name, at, size, steel);
                t.localPosition = at; t.localScale = size;
            }
            // The rail runs behind the cutting plane; a rear bearing carries the saw.
            line.Find("Overhead carrier rail").localPosition = new Vector3(8, .28f, -.75f);
            foreach (var hanger in line.Cast<Transform>().Where(t => t.name.StartsWith("Body carrier ")))
                BoxAt(hanger, "Rear trolley suspension", new(0, .14f, -.37f), new(.16f,.18f,.85f));
            BoxAt(line, "Main saw fixed rear bearing", new(IndustryCut,-.7f,-2.65f), new(.8f,.65f,3.5f));
            BoxAt(line, "Main saw wall anchor", new(IndustryCut,-.7f,-4.3f), new(1.1f,1.1f,.3f));
            if (!line.Find("Main saw axle"))
                IndustryPipe(line, "Main saw axle", new(IndustryCut,-.7f,-1), new(IndustryCut,-.7f,.12f), .14f, pipe);
            foreach (float x in new[] { -.7f, 3.3f, 13f, 17.1f })
                BoxAt(line, "Enclosure floor support " + x, new(x,-9.94f,1.35f), new(.2f,6.12f,.2f));
            foreach (var span in new[] { new Vector2(-.9f,3.5f), new Vector2(12.8f,17.3f) })
            {
                BoxAt(line, "Enclosure bottom crossmember " + span.x, new((span.x+span.y)*.5f,-6.9f,1.35f), new(span.y-span.x,.2f,.2f));
                foreach (float ratio in new[] { .12f,.5f,.88f })
                    BoxAt(line, "Sheet metal rib " + span.x + " " + ratio, new(Mathf.Lerp(span.x,span.y,ratio),-3.1f,1.32f), new(.08f,7.2f,.14f));
            }
            var oldSpindle = line.Find("Auxiliary wheel fixed spindle");
            if (oldSpindle) Object.DestroyImmediate(oldSpindle.gameObject);
            if (!line.Find("Auxiliary wheel axle"))
            {
                IndustryPipe(line, "Auxiliary wheel axle", new(12,-3,-.9f), new(12,-3,.95f), .12f, pipe);
                IndustryPipe(line, "Auxiliary wheel round hub", new(12,-3,.72f), new(12,-3,.94f), .2f, pipe);
            }
            BoxAt(line, "Auxiliary grinder bearing block", new(12,-3,-.6f), new(.6f,.6f,.7f));
            foreach (var renderer in line.Find("Auxiliary wheel spindle mount").GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => pipe).ToArray();
            var cutLight = installation.Find("Processing neutral worklight").GetComponentInChildren<Light>();
            cutLight.intensity = 140; cutLight.spotAngle = 90; cutLight.innerSpotAngle = 35;
            var vatLight = installation.Find("Vat neutral worklight").GetComponentInChildren<Light>();
            vatLight.intensity = 70; vatLight.spotAngle = 85; vatLight.innerSpotAngle = 30;
            var steam = installation.GetComponentsInChildren<ParticleSystem>(true).Single(p => p.name == "Vat surface steam");
            installation.GetComponentsInChildren<ParticleSystem>(true).Single(p => p.name == "Boiling surface bubbles")
                .GetComponent<ParticleSystemRenderer>().sharedMaterial = IndustryBubbleMaterial();
            var main = steam.main;
            main.startColor = new Color(.65f,.7f,.68f,.38f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.7f,2.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(.65f,1.1f);
            // The factory smoke texture is an 8x8 flipbook, not a single puff.
            var sheet = steam.textureSheetAnimation;
            sheet.enabled = true; sheet.numTilesX = 8; sheet.numTilesY = 8;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0,0,1,1));
            sheet.cycleCount = 1;
            var color = steam.colorOverLifetime; color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.15f), new GradientAlphaKey(0,1) });
            color.color = fade;
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static Material IndustryBubbleMaterial()
        {
            const string path = "Assets/Scenes/Tests/EscapeRoute/Pursuer/EscapeRoutePursuitRemodelBubbles.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            var material = new Material(IndustryAsset<Material>(IndustryMotion + "pressure-vapor.mat"))
                { name = "Industrial residue bubbles" };
            material.SetTexture("_BaseMap", IndustryAsset<Texture2D>("Assets/_ThirdParty/ithappy/Battle_Stadium/Textures/Bubble_Texture.png"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static T IndustryAsset<T>(string path) where T : Object
            => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("산업 연출 에셋 누락: " + path);

        static Transform IndustryNode(Transform parent, string name, Vector3 local)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = local; return t;
        }

        static Transform IndustryBox(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool solid = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = center; go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }

        static void IndustryMesh(Transform node, Mesh mesh, Material[] materials)
        {
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
        }

        static Transform IndustryCloneVisual(Transform source, Transform parent, Dictionary<Transform, Transform> map)
        {
            if (map.TryGetValue(source, out var existing)) return existing;
            if (source.GetComponent<SkinnedMeshRenderer>()) throw new InvalidOperationException("베이크된 정적 장식 메시만 복사할 수 있습니다: " + source.name);
            var copy = IndustryNode(parent, source.name, source.localPosition); map.Add(source, copy);
            copy.localRotation = source.localRotation; copy.localScale = source.localScale;
            var filter = source.GetComponent<MeshFilter>(); var renderer = source.GetComponent<MeshRenderer>();
            if (filter && renderer) IndustryMesh(copy, filter.sharedMesh, renderer.sharedMaterials);
            foreach (Transform child in source) IndustryCloneVisual(child, copy, map);
            // Only transforms and static mesh renderers are reconstructed: no actor, network,
            // rigidbody, hazard, animation, collider, or self-destroy component can be copied.
            return copy;
        }

        static void IndustryCabinet(Transform line, string name, float start, float end, Material steel, Material dark)
        {
            float center = (start + end) * .5f, width = end - start;
            IndustryBox(line, name + " front enclosure", new(center, -3.1f, 1.15f), new(width, 7.2f, .24f), steel);
            IndustryBox(line, name + " rear enclosure", new(center, -3.1f, -1.15f), new(width, 7.2f, .24f), dark);
            IndustryBox(line, name + " top", new(center, .62f, 0), new(width, .24f, 2.54f), steel);
            IndustryBox(line, name + " bottom", new(center, -6.82f, 0), new(width, .24f, 2.54f), dark);
            float closed = start < 0 ? start : end;
            IndustryBox(line, name + " closed outer end", new(closed, -3.1f, 0), new(.24f, 7.2f, 2.54f), steel);
        }

        static ParticleSystem IndustryParticles(Transform parent, string name, Vector3 local, Material material,
            Color color, int maximum, float rate, float lifetime, float speed, float size, float gravity)
        {
            var particles = IndustryNode(parent, name, local).gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.loop = true; main.playOnAwake = false; main.stopAction = ParticleSystemStopAction.None;
            main.maxParticles = Mathf.Min(maximum, 100); main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .5f, speed); main.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size);
            main.startColor = color; main.gravityModifier = gravity; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            var emission = particles.emission; emission.enabled = true; emission.rateOverTime = rate; emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            var shape = particles.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = .07f; shape.angle = 24;
            var collision = particles.collision; collision.enabled = false;
            var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return particles;
        }

        static void IndustryWindow(List<EscapeIndustrialVignette.EmissionWindow> windows, ParticleSystem particles, float start, float duration)
            => windows.Add(new EscapeIndustrialVignette.EmissionWindow
                { particles = particles, from = Mathf.Repeat(start, 1), until = Mathf.Repeat(start + duration, 1) });

        static void BuildIndustryVat(Transform installation, Material steel, Material pipe, Material dark, Material ochre,
            Material liquid, Material vapor, Material bubbles)
        {
            var vat = IndustryNode(installation, "Open boiling residue vat", new(101, -25.8f, -32));
            var shell = IndustryVatMesh(); IndustryMesh(vat, shell, new[] { steel, dark, ochre });
            var liquidNode = IndustryNode(vat, "Recessed moving liquor surface", new(0, 3.32f, 0));
            IndustryMesh(liquidNode, IndustryLiquidMesh(), new[] { liquid });
            IndustryBox(vat, "Vat foundation plinth", new(0, -.1f, 0), new(4.4f, .2f, 4.4f), dark);
            foreach (float side in new[] { -1f, 1f })
            {
                IndustryPipe(vat, "Vat vertical feed", new(side * 2.12f, .25f, -.8f), new(side * 2.12f, 4.6f, -.8f), .12f, pipe);
                IndustryPipe(vat, "Vat upper return elbow", new(side * 2.12f, 4.6f, -.8f), new(side * 1.4f, 4.6f, -.8f), .12f, pipe);
                IndustryPipe(vat, "Vat downward outlet", new(side * 1.4f, 4.6f, -.8f), new(side * 1.4f, 3.7f, -.8f), .12f, pipe);
                IndustryBox(vat, "Feed pipe wall foot", new(side * 2.12f, .15f, -.8f), new(.4f, .3f, .4f), steel);
            }
            var effects = vat.gameObject.AddComponent<EscapeIndustrialVignette>(); effects.cycleSeconds = 7.7f; effects.phaseOffsetSeconds = 1.1f;
            var steam = IndustryParticles(vat, "Vat surface steam", new(0, 3.48f, 0), vapor,
                new Color(.58f, .65f, .64f, .22f), 50, 13, 1.9f, .8f, .55f, -.08f);
            steam.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var steamShape = steam.shape; steamShape.radius = 1.5f; steamShape.angle = 8;
            var bubble = IndustryParticles(vat, "Boiling surface bubbles", new(0, 3.37f, 0), bubbles,
                new Color(.52f, .58f, .4f, .75f), 40, 24, .75f, .24f, .13f, -.04f);
            bubble.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var bubbleShape = bubble.shape; bubbleShape.shapeType = ParticleSystemShapeType.Circle; bubbleShape.radius = 1.65f;
            var windows = new List<EscapeIndustrialVignette.EmissionWindow>();
            // Two overlapping windows keep bubbles alive at the phase boundary without a one-frame restart.
            IndustryWindow(windows, bubble, 0, .7f); IndustryWindow(windows, bubble, .5f, .5f);
            IndustryWindow(windows, steam, .06f, .8f); effects.emissions = windows.ToArray();
            AddIndustrialLoop(vat, "Vat bubbling", new(0, 3.3f, 0), true);
        }

        static void IndustryPipe(Transform parent, string name, Vector3 start, Vector3 end, float radius, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = (start + end) * .5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (end - start).normalized);
            go.transform.localScale = new Vector3(radius * 2, Vector3.Distance(start, end) * .5f, radius * 2);
            go.GetComponent<MeshRenderer>().sharedMaterial = material; Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        static Light IndustryWorklight(Transform parent, string name, Vector3 position, Vector3 support, Vector3 target,
            float intensity, float range, Material steel, Material lens)
        {
            IndustryPipe(parent, name + " attached bracket", support, position, .055f, steel);
            var fixture = IndustryNode(parent, name, position);
            fixture.localRotation = Quaternion.LookRotation((target - position).normalized);
            IndustryBox(fixture, "Worklight metal housing", Vector3.zero, new(.75f, .32f, .4f), steel);
            IndustryBox(fixture, "Worklight recessed lens", new(0, 0, .211f), new(.58f, .16f, .025f), lens);
            var lamp = IndustryNode(fixture, "Focused neutral task light", new(0, 0, .23f)).gameObject.AddComponent<Light>();
            lamp.type = LightType.Spot; lamp.color = new Color(.74f, .81f, .85f); lamp.intensity = intensity;
            lamp.range = range; lamp.spotAngle = 64; lamp.innerSpotAngle = 39; lamp.shadows = LightShadows.Soft;
            SetLocalShadowTier(lamp, 0);
            return lamp;
        }

        static Mesh IndustryPersist(Mesh created)
        {
            var existing = AssetDatabase.LoadAllAssetsAtPath(IndustryGeometry).OfType<Mesh>().FirstOrDefault(m => m.name == created.name);
            if (existing) { Object.DestroyImmediate(created); return existing; }
            if (!AssetDatabase.LoadMainAssetAtPath(IndustryGeometry)) AssetDatabase.CreateAsset(created, IndustryGeometry);
            else AssetDatabase.AddObjectToAsset(created, IndustryGeometry);
            return created;
        }

        static Mesh IndustryVatMesh()
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>();
            var slots = new[] { new List<int>(), new List<int>(), new List<int>() };
            void Quad(int slot, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int k = vertices.Count; vertices.AddRange(new[] { a, b, c, d });
                uv.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
                slots[slot].AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }
            Vector3 Ring(int i, float radius, float y) => new(Mathf.Cos(i * Mathf.PI / 16) * radius, y, Mathf.Sin(i * Mathf.PI / 16) * radius);
            for (int i = 0; i < 32; i++)
            {
                int n = i + 1;
                Quad(0, Ring(i, 2.1f, 0), Ring(i, 2.1f, 3.6f), Ring(n, 2.1f, 3.6f), Ring(n, 2.1f, 0));
                Quad(1, Ring(n, 1.88f, .2f), Ring(n, 1.88f, 3.6f), Ring(i, 1.88f, 3.6f), Ring(i, 1.88f, .2f));
                Quad(2, Ring(i, 2.1f, 3.6f), Ring(i, 1.88f, 3.6f), Ring(n, 1.88f, 3.6f), Ring(n, 2.1f, 3.6f));
                Quad(1, Vector3.up * .2f, Ring(n, 1.88f, .2f), Ring(i, 1.88f, .2f), Vector3.up * .2f);
            }
            var mesh = new Mesh { name = "Industrial vat shell" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) mesh.SetTriangles(slots[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return IndustryPersist(mesh);
        }

        static Mesh IndustryLiquidMesh()
        {
            var vertices = new List<Vector3> { Vector3.zero }; var uv = new List<Vector2> { Vector2.one * .5f }; var triangles = new List<int>();
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI / 16;
                var point = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                vertices.Add(point * 1.86f); uv.Add(new Vector2(point.x, point.z) * .5f + Vector2.one * .5f);
                triangles.AddRange(new[] { 0, (i + 1) % 32 + 1, i + 1 });
            }
            var mesh = new Mesh { name = "Industrial vat liquor surface" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return IndustryPersist(mesh);
        }
    }
}
