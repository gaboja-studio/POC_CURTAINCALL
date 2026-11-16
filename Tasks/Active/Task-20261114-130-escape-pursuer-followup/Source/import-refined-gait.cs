using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CurtainCall.EscapeRoute.EditorTools;
using Object = UnityEngine.Object;

public static class ImportRefinedGait
{
    const string Source = ".harness/local/pursuer-space-20261116/gait/refined-v1/";
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Edit mode required.");
        string folder = EscapePursuerHeavyGaitBuilder.Folder;
        foreach (var pair in new[]{(source:"Golem_Walk_Refined",name:"Golem_WalkWeighted"),(source:"Golem_Run_Refined",name:"Golem_RunHeavy")})
        {
            string fbx = folder + pair.source + ".fbx";
            File.Copy(Source + pair.source + ".fbx",fbx,true);
            AssetDatabase.ImportAsset(fbx,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(fbx);
            importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;
            importer.animationCompression=ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects=false;importer.optimizeBones=false;
            importer.importCameras=false;importer.importLights=false;importer.globalScale=1;
            importer.SaveAndReimport();
            var clip=AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Single(c=>!c.name.StartsWith("__preview__"));
            var copy=Object.Instantiate(clip);copy.name=pair.name;copy.legacy=true;copy.wrapMode=WrapMode.Loop;
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(EscapePursuerRevision7Builder.Model+"circus-golem-look.fbx");
            var bindings=AnimationUtility.GetCurveBindings(copy);
            var curves=bindings.Select(b=>AnimationUtility.GetEditorCurve(copy,b)).ToArray();
            for(int i=0;i<bindings.Length;i++)
            {
                var b=bindings[i];
                if(b.path.Length==0 || !model.transform.Find(b.path)) bindings[i].path="PursuerDollRig"+(b.path.Length>0?"/"+b.path:"");
                if(!model.transform.Find(bindings[i].path))throw new InvalidOperationException("Unmapped bone: "+b.path);
                var keys=curves[i].keys;
                if(keys.Length>2&&keys.All(k=>Mathf.Abs(k.value-keys[0].value)<.000001f))
                    curves[i]=new AnimationCurve(new Keyframe(keys[0].time,keys[0].value),new Keyframe(keys[keys.Length-1].time,keys[0].value));
            }
            copy.ClearCurves();AnimationUtility.SetEditorCurves(copy,bindings,curves);copy.EnsureQuaternionContinuity();
            string path=folder+pair.name+".anim";
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(existing){EditorUtility.CopySerialized(copy,existing);Object.DestroyImmediate(copy);EditorUtility.SetDirty(existing);}
            else AssetDatabase.CreateAsset(copy,path);
            AssetDatabase.SaveAssets();
            if(AssetDatabase.GetDependencies(path,true).Contains(fbx))throw new InvalidOperationException("Temporary FBX dependency.");
            AssetDatabase.DeleteAsset(fbx);
        }
        foreach(string name in new[]{"animation-manifest-gait.json","animation-manifest-run.json"})
        {
            File.Copy(Source+"animation-manifest-gait.json",folder+name,true);
            AssetDatabase.ImportAsset(folder+name,ImportAssetOptions.ForceSynchronousImport);
        }
        EscapePursuerHeavyGaitBuilder.Apply();
        EscapePursuerEntranceGaitBuilder.Apply();
        return "Refined walk/run imported with existing run GUID preserved; prefab and three working scenes connected. Original rollback scene excluded.";
    }
}
