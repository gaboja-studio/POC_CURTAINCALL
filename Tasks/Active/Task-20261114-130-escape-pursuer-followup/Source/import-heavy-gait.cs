using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CurtainCall.EscapeRoute.EditorTools;
using Object = UnityEngine.Object;

public static class ImportHeavyGait
{
    const string Source = ".harness/local/pursuer-weight-20261116/";
    static void Folder(string path)
    {
        path = path.TrimEnd('/'); if (AssetDatabase.IsValidFolder(path)) return;
        Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }
    static void Copy(string from, string to)
    {
        File.Copy(from, to, true);
        AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceSynchronousImport);
    }
    public static string Run()
    {
        string folder = EscapePursuerHeavyGaitBuilder.Folder, audio = EscapePursuerHeavyGaitBuilder.Audio;
        Folder(folder); Folder(audio);
        foreach (string name in new[] { "HeavyFootfallL", "HeavyFootfallR", "HeavyBodyImpact" })
        {
            string path = audio + name + ".wav";
            Copy(Source + "audio/" + name + ".wav", path);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true; importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings; importer.SaveAndReimport();
        }
        Copy(Source + "heavy-858-v2/animation-manifest-run.json", folder + "animation-manifest-run.json");
        string fbx = folder + "Golem_Run_Heavy.fbx";
        Copy(Source + "heavy-858-v2/Golem_Run_Heavy.fbx", fbx);
        var modelImporter = (ModelImporter)AssetImporter.GetAtPath(fbx);
        modelImporter.animationType = ModelImporterAnimationType.Legacy;
        modelImporter.importAnimation = true; modelImporter.animationCompression = ModelImporterAnimationCompression.Off;
        modelImporter.optimizeGameObjects = false; modelImporter.optimizeBones = false;
        modelImporter.importCameras = false; modelImporter.importLights = false;
        modelImporter.globalScale = 1; modelImporter.isReadable = true;
        modelImporter.SaveAndReimport();
        var clip = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));
        var copy = Object.Instantiate(clip); copy.name = "Golem_RunHeavy"; copy.legacy = true;
        copy.wrapMode = WrapMode.Loop; copy.EnsureQuaternionContinuity();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(EscapePursuerRevision7Builder.Model + "circus-golem-look.fbx");
        // Unity strips the single armature container from an animation-only FBX.
        // Restore the same container path used by the nine-mesh production model.
        var bindings = AnimationUtility.GetCurveBindings(copy);
        var curves = bindings.Select(b => AnimationUtility.GetEditorCurve(copy, b)).ToArray();
        for (int i = 0; i < bindings.Length; i++)
        {
            var binding = bindings[i];
            if (binding.path.Length == 0 || !model.transform.Find(binding.path))
                bindings[i].path = "PursuerDollRig" + (binding.path.Length > 0 ? "/" + binding.path : "");
            if (!model.transform.Find(bindings[i].path)) throw new InvalidOperationException("Unmapped heavy bone: " + binding.path);
            var keys = curves[i].keys;
            if (keys.Length > 2 && keys.All(k => Mathf.Abs(k.value - keys[0].value) < .000001f))
                curves[i] = new AnimationCurve(new Keyframe(keys[0].time, keys[0].value), new Keyframe(keys[keys.Length-1].time, keys[0].value));
        }
        copy.ClearCurves(); AnimationUtility.SetEditorCurves(copy, bindings, curves);
        copy.EnsureQuaternionContinuity();
        string anim = folder + "Golem_RunHeavy.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(anim);
        if (existing) { EditorUtility.CopySerialized(copy, existing); Object.DestroyImmediate(copy); EditorUtility.SetDirty(existing); }
        else AssetDatabase.CreateAsset(copy, anim);
        AssetDatabase.SaveAssets();
        if (AssetDatabase.GetDependencies(anim, true).Contains(fbx)) throw new InvalidOperationException("Animation still references temporary FBX.");
        AssetDatabase.DeleteAsset(fbx);
        EscapePursuerHeavyGaitBuilder.Apply();
        return "Heavy run and three mono impact sounds imported and connected to prefab, Prototype, production and playtest.";
    }
}
