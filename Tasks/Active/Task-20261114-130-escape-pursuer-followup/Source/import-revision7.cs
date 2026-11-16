using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

public static class ImportRevision7
{
    const string Source = "C:/Unity/github-worktrees/POC_CURTAINCALL/pursuer-doll-model/Tasks/Active/Task-20261114-130-pursuer-doll-model/Model/Revision7/";
    const string Model = "Assets/Resources/Models/Characters/Enemies/CircusGolemRevision7/";
    const string Motions = "Assets/Resources/Animations/Enemies/CircusGolemRevision7/";
    static void Folder(string path)
    {
        path = path.TrimEnd('/'); if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    static void Copy(string source, string target)
    {
        if (!File.Exists(target)) File.Copy(source, target);
        AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
    }
    public static string Run()
    {
        Folder(Model); Folder(Motions); Folder(Model + "Textures"); Folder(Model + "Materials");
        foreach (var file in Directory.GetFiles(Source + "Textures", "*.png")) Copy(file, Model + "Textures/" + Path.GetFileName(file));
        foreach (var name in new[] { "animation-manifest.json", "rig-contract.json", "neck-tracking.json" }) Copy(Source + name, Model + name);
        string modelPath = Model + "circus-golem-look.fbx", motionPath = Motions + "circus-golem-motions.fbx";
        Copy(Source + "circus-golem-look.fbx", modelPath); Copy(Source + "circus-golem-motions.fbx", motionPath);
        foreach (string path in new[] { modelPath, motionPath })
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = path == motionPath;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects = false; importer.optimizeBones = false;
            importer.importCameras = false; importer.importLights = false;
            importer.globalScale = 1; importer.isReadable = true;
            importer.SaveAndReimport();
        }
        var manifest = JObject.Parse(File.ReadAllText(Model + "animation-manifest.json"))["clips"];
        var imported = AssetDatabase.LoadAllAssetsAtPath(motionPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        foreach (var spec in ((JObject)manifest).Properties())
        {
            var clip = imported.Single(c => c.name.EndsWith(spec.Name, StringComparison.Ordinal));
            var copy = Object.Instantiate(clip); copy.name = spec.Name; copy.legacy = true;
            copy.wrapMode = (bool?)spec.Value["loop"] == true ? WrapMode.Loop : WrapMode.ClampForever;
            copy.EnsureQuaternionContinuity();
            string path = Motions + spec.Name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing) { EditorUtility.CopySerialized(copy, existing); Object.DestroyImmediate(copy); EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(copy, path);
        }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        foreach (var source in model.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(s => s.sharedMaterials).Distinct())
        {
            string path = Model + "Materials/" + source.name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) continue;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = source.name };
            bool face = source.name.Contains("Face recess");
            string texture = source.name.Contains("front") ? "layered-canvas-front" : source.name.Contains("back") ? "layered-canvas-back"
                : source.name.Contains("patchwork") ? "circus-golem-basecolor" : null;
            material.SetFloat("_Smoothness", 0.04f); material.SetFloat("_SpecularHighlights", face ? 0 : 1);
            material.SetColor("_BaseColor", face ? new Color(.0004f,.0004f,.0004f,1) : texture != null ? Color.white : source.color);
            if (texture != null) material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Model + "Textures/" + texture + ".png"));
            AssetDatabase.CreateAsset(material, path);
        }
        AssetDatabase.SaveAssets();
        var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
        var report = new { model = modelPath, skins = skins.Length, bones = skins.SelectMany(s => s.bones).Distinct().Count(), clips = imported.Select(c => c.name).ToArray(),
            transforms = model.GetComponentsInChildren<Transform>().Where(t => t.name == "Forearm.R" || t.name == "Hand.R" || t.name == "Fold00.R").Select(t => new {t.name,parent=t.parent.name,position=t.position.ToString(),rotation=t.rotation.ToString()}) };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText("Tasks/Active/Task-20261114-130-escape-pursuer-followup/Preview/revision7-import.json", json);
        var clips = ((JObject)manifest).Properties().Select(p => Motions + p.Name + ".anim").ToArray();
        if (AssetDatabase.GetDependencies(clips, true).Contains(motionPath))
            throw new InvalidOperationException("독립 클립에 임시 FBX 참조가 남았습니다.");
        // The source FBX remains in the Blender worktree; do not package its duplicate mesh.
        AssetDatabase.DeleteAsset(motionPath);
        return json;
    }
}
