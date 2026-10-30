using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        /// <summary>Upgrade the existing exit in place; preserves the fire chase and every previous cooperative obstacle.</summary>
        public static string UpgradeAColoredCables()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath || scene.isDirty)
                throw new InvalidOperationException("Saved A edit scene required");
            var root = GameObject.Find(RootName).transform.Find("40_AExitCableSaw");
            if (root == null || root.Find("ColorCableBanks") != null) throw new InvalidOperationException("Original A saw exit required");
            CompletionMaterials();
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Long colored cable banks and automatic saw launcher");
            var saw = root.GetComponent<EscapeExitSaw>(); var data = new SerializedObject(saw);
            foreach (string name in new[] { "절단할 노출 배선", "배선 제어반 연결", "하부 배선 연결", "출구 잠금 배선 제어반" })
            {
                var old = root.Find(name);
                if (old != null) { Undo.RecordObject(old.gameObject, "Replace single short cable"); old.gameObject.SetActive(false); }
            }
            var banks = VerticalChild(root, "ColorCableBanks");
            var cables = new List<EscapeCuttableCable>();
            var colors = new[] { new Color(.86f, .035f, .055f), new Color(.03f, .55f, .95f), new Color(1, .72f, .025f),
                new Color(.08f, .8f, .22f), new Color(.69f, .08f, .95f), new Color(1, .28f, .03f) };
            var names = new[] { "Red", "Cyan", "Yellow", "Green", "Violet", "Orange" };
            var rubber = colors.Select((c, i) => CableMaterial("cable-" + names[i].ToLowerInvariant(), c, false)).ToArray();
            var copper = CableMaterial("cable-copper-core", new Color(.7f, .28f, .09f), true);
            var cutClip = CableSound(false);
            var centers = new[] { new Vector3(383, 0, 72.9f), new Vector3(386.2f, 0, 82.9f), new Vector3(389, 0, 78) };
            for (int bankIndex = 0; bankIndex < centers.Length; bankIndex++)
            {
                var bank = VerticalChild(banks, $"{bankIndex + 1:00}_FourColorPowerBank");
                bank.localPosition = centers[bankIndex];
                float top = bankIndex == 1 ? 5.35f : bankIndex == 0 ? 4.6f : 4.2f;
                BMass(bank, "상부 전력 분배함", new Vector3(.35f, top + .15f, 0), new Vector3(.5f, .42f, 2.5f), wornSteel, false);
                BMass(bank, "하부 전력 분배함", new Vector3(.35f, .2f, 0), new Vector3(.5f, .4f, 1.8f), wornSteel, false);
                float ceiling = bankIndex == 2 ? 6.5f : 15;
                foreach (float side in new[] { -1.15f, 1.15f })
                {
                    VerticalCable(bank, "분배함 천장 행거", new Vector3(.35f, top + .36f, side), new Vector3(.35f, ceiling, side), .045f, wornSteel);
                    BMass(bank, "분배함 천장 고정판", new Vector3(.35f, ceiling + .05f, side), new Vector3(.5f, .1f, .5f), wornSteel, false);
                }
                // Keep machinery above/below the saw's shooting height. No invisible wire colliders.
                for (int strand = 0; strand < 4; strand++)
                {
                    int color = (bankIndex * 2 + strand) % rubber.Length;
                    float z = (strand - 1.5f) * .5f;
                    var cableRoot = VerticalChild(bank, $"{strand + 1:00}_{names[color]}_CuttableCable");
                    var cable = Undo.AddComponent<EscapeCuttableCable>(cableRoot.gameObject);
                    var cableData = new SerializedObject(cable);
                    var path = cableData.FindProperty("localPoints"); path.arraySize = 24;
                    Vector3 a = new(.3f, top, z), b = new(-.65f - strand * .08f, top - .8f, z + .18f),
                        c = new(-1.6f + strand * .14f, .65f, z - .16f), d = new(.3f, .35f, z * .75f);
                    for (int p = 0; p < path.arraySize; p++)
                    {
                        float t = (float)p / (path.arraySize - 1), u = 1 - t;
                        path.GetArrayElementAtIndex(p).vector3Value = u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
                    }
                    cableData.FindProperty("radius").floatValue = .085f;
                    cableData.FindProperty("insulationMaterial").objectReferenceValue = rubber[color];
                    cableData.FindProperty("copperMaterial").objectReferenceValue = copper;
                    cableData.FindProperty("requiredForExit").boolValue = true;
                    var sparks = FinaleSparks(cableRoot);
                    sparks.name = "절단 순간 스파크";
                    var main = sparks.main; main.loop = false; main.duration = .22f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .45f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(3, 7); main.startSize = .045f; main.maxParticles = 40;
                    var emission = sparks.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 28) });
                    var source = Undo.AddComponent<AudioSource>(VerticalChild(cableRoot, "배선 절단 소리").gameObject);
                    source.clip = cutClip; source.playOnAwake = false; source.spatialBlend = 1;
                    source.minDistance = 3; source.maxDistance = 35; source.volume = .55f; source.pitch = .94f + strand * .045f;
                    source.dopplerLevel = 0;
                    cableData.FindProperty("cutSparks").objectReferenceValue = sparks;
                    cableData.FindProperty("cutAudio").objectReferenceValue = source;
                    cableData.ApplyModifiedProperties(); cable.Rebuild(); cables.Add(cable);
                    foreach (float y in new[] { top + .06f, .36f })
                        BMass(bank, "색상별 배선 접속구", new Vector3(.3f, y, z), new Vector3(.58f, .2f, .2f), rubber[color], false);
                }
                BLight(banks, centers[bankIndex] + new Vector3(-3, 4.2f, 0), centers[bankIndex] + new Vector3(0, 1.5f, 0), 270, 11);
            }
            SetFinaleReferences(data.FindProperty("cables"), cables.ToArray());
            var prompt = VerticalChild(banks, "출구 배선 안내 위치"); prompt.localPosition = new Vector3(386, 1.45f, 78);
            data.FindProperty("cableSocket").objectReferenceValue = prompt;
            ConfigureSawLauncher(root, saw, data);
            data.ApplyModifiedProperties();
            Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene); Selection.activeGameObject = banks.gameObject;
            Undo.CollapseUndoOperations(undo);
            return "A: 12 long individually cuttable colored cables at three banks; edge-first saw volley every 2 seconds";
        }

        static void ConfigureSawLauncher(Transform root, EscapeExitSaw saw, SerializedObject data)
        {
            var device = saw.CarryRoot;
            var blade = (Transform)data.FindProperty("blade").objectReferenceValue;
            Undo.RecordObject(blade, "Edge-first saw direction"); blade.localRotation = Quaternion.Euler(0, 90, 0);
            var hood = device.Find("톱날 상부 안전 덮개");
            Undo.RecordObject(hood, "Fit longitudinal saw guard"); hood.localRotation = Quaternion.Euler(0, 90, 0);
            var recoil = VerticalChild(device, "발사부 반동 외형");
            Undo.SetTransformParent(blade, recoil, "Saw visual recoil"); Undo.SetTransformParent(hood, recoil, "Saw guard recoil");
            var muzzle = VerticalChild(device, "톱날 발사 위치"); muzzle.localPosition = new Vector3(0, 1.45f, .65f);
            var template = VerticalChild(root, "SawProjectileTemplate");
            var art = UnityEngine.Object.Instantiate(blade.gameObject, template);
            Undo.RegisterCreatedObjectUndo(art, "Original saw projectile mesh"); art.name = "EdgeFirstBlade";
            art.transform.localPosition = Vector3.zero; art.transform.localRotation = Quaternion.Euler(0, 90, 0);
            template.gameObject.SetActive(false);
            var flash = FinaleSparks(muzzle); flash.name = "톱날 발사 불꽃";
            flash.transform.localRotation = Quaternion.identity;
            var main = flash.main; main.loop = false; main.duration = .12f; main.startLifetime = .12f;
            main.startSpeed = 2; main.startSize = .055f; main.startColor = new Color(1, .65f, .15f); main.gravityModifier = 0;
            var emission = flash.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 12) });
            var source = Undo.AddComponent<AudioSource>(muzzle.gameObject);
            source.clip = CableSound(true); source.playOnAwake = false; source.spatialBlend = 1;
            source.minDistance = 3; source.maxDistance = 45; source.volume = .65f; source.dopplerLevel = 0;
            data.FindProperty("muzzle").objectReferenceValue = muzzle;
            data.FindProperty("projectileVisual").objectReferenceValue = template.gameObject;
            data.FindProperty("launchFlash").objectReferenceValue = flash;
            data.FindProperty("launchAudio").objectReferenceValue = source;
            data.FindProperty("recoilVisual").objectReferenceValue = recoil;
            data.FindProperty("shotInterval").floatValue = 2;
            data.FindProperty("projectileSpeed").floatValue = 18;
            data.FindProperty("projectileLifetime").floatValue = 2.5f;
            data.FindProperty("projectileRadius").floatValue = .55f;
            data.FindProperty("projectileHalfThickness").floatValue = .12f;
        }

        static Material CableMaterial(string name, Color color, bool metal)
        {
            string path = Materials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metal ? .75f : .05f);
            material.SetFloat("_Smoothness", metal ? .6f : .32f);
            if (!metal) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * .035f); }
            AssetDatabase.CreateAsset(material, path); return material;
        }

        static AudioClip CableSound(bool launch)
        {
            const string folder = "Assets/Scenes/Levels/EscapeRouteAudio";
            string path = folder + (launch ? "/SawLaunch.wav" : "/CableSnap.wav");
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path); if (clip != null) return clip;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Scenes/Levels", "EscapeRouteAudio");
            const int rate = 24000; float duration = launch ? .42f : .23f; int count = Mathf.CeilToInt(rate * duration);
            using (var stream = File.Create(path)) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                var random = new System.Random(launch ? 713 : 739); float filtered = 0;
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / rate, noise = (float)(random.NextDouble() * 2 - 1);
                    filtered += (noise - filtered) * (launch ? .18f : .6f);
                    float env = Mathf.Clamp01(t / .005f) * Mathf.Pow(1 - t / duration, 2);
                    float tone = Mathf.Sin(2 * Mathf.PI * ((launch ? 340 : 140) * t - (launch ? 260 : 100) * t * t));
                    float sample = (filtered * (launch ? .8f : 1.2f) + tone * .2f) * env;
                    writer.Write((short)(Mathf.Clamp(sample, -.85f, .85f) * short.MaxValue));
                }
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
