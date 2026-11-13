using System;
using System.Linq;
using System.Reflection;
using CurtainCall.CharacterArt;
using CurtainCall.EscapeRoute;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;

public static class EndingAppearanceVerification
{
    const string Art="Assets/Scenes/Levels/FailureEndingArt/";
    public static async System.Threading.Tasks.Task<object> Preview(bool four,bool statistics)
    {
        EditorApplication.isPaused=false;
        typeof(CurtainCall.Dev.DevMode).GetField("_enabled",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);
        var e=FailureEndingPresentation.Current;
        var s=e.GetComponentInChildren<FailureEndingStage>();
        var result=e.GetComponent<EscapeEndingResults>();
        s.Stop();
        s.Begin(four?Enumerable.Range(0,4).Select(i=>new EscapeEndingResults.Person{slot=i,model=i,outfit=PlayerAppearanceCatalog.Current.DefaultFor(i).outfit}).ToArray():result.People);
        result.View.SetVisible(!four);if(statistics)result.View.ShowStatistics();else result.View.ShowPlayers();
        await System.Threading.Tasks.Task.Delay(300);
        while(s.CurrentIntensity<4.5f)await System.Threading.Tasks.Task.Delay(25);
        EditorApplication.isPaused=true;
        return new{fourCharacterArtPreview=four,statistics,intensity=s.CurrentIntensity};
    }
    public static async System.Threading.Tasks.Task<object> VerifyLight()
    {
        var stage=FailureEndingPresentation.Current.GetComponentInChildren<FailureEndingStage>();
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var light=(Light)typeof(FailureEndingStage).GetField("bulbLight",flags).GetValue(stage);
        var filament=(Renderer)typeof(FailureEndingStage).GetField("filament",flags).GetValue(stage);
        var glass=(Renderer)typeof(FailureEndingStage).GetField("bulbGlass",flags).GetValue(stage);
        float brightness=(float)typeof(FailureEndingStage).GetField("brightness",flags).GetValue(stage);
        float distance=Vector3.Distance(light.transform.position,filament.bounds.center);
        if(light.type!=LightType.Spot||light.transform.forward.y>-.7f||distance>.035f)throw new Exception("Bulb source or downward direction mismatch");
        var renderers=stage.GetComponentsInChildren<Renderer>();
        var stageBounds=renderers[0].bounds;
        foreach(var r in renderers)stageBounds.Encapsulate(r.bounds);
        var others=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
            .Where(l=>l.enabled&&l!=light&&(l.type==LightType.Directional||stageBounds.SqrDistance(l.transform.position)<l.range*l.range)).ToArray();
        if(others.Length>0)throw new Exception("Other room lights active: "+string.Join(",",others.Select(l=>l.name)));
        if(filament.sharedMaterial.shader.name!="CurtainCall/Endings/Filament")throw new Exception("Filament must emit independently of room lights");
        float min=1,max=0,maxDifference=0;int samples=0;
        var until=DateTime.UtcNow.AddSeconds(20);
        var a=new MaterialPropertyBlock();var b=new MaterialPropertyBlock();
        while(DateTime.UtcNow<until)
        {
            filament.GetPropertyBlock(a);glass.GetPropertyBlock(b);
            float power=light.intensity/brightness;
            maxDifference=Mathf.Max(maxDifference,Mathf.Abs(power-a.GetColor("_EmissionColor").r/7),Mathf.Abs(power-b.GetFloat("_LampPower")));
            min=Mathf.Min(min,power);max=Mathf.Max(max,power);samples++;
            await System.Threading.Tasks.Task.Delay(20);
        }
        if(maxDifference>1e-5f||min>.1f||max<.9f)throw new Exception("Filament/glass/room flicker is not synchronized");
        return new{lightType=light.type.ToString(),direction=light.transform.forward.ToString("F3"),sourceDistance=distance,otherLights=others.Length,samples,minPower=min,maxPower=max,maxDifference};
    }
    public static object Verify()
    {
        int appearances=0,surfaces=0;
        var catalog=PlayerAppearanceCatalog.Current;
        var tint=typeof(FailureEndingStage).GetMethod("TintCostume",BindingFlags.Static|BindingFlags.NonPublic);
        for(int modelIndex=0;modelIndex<catalog.models.Length;modelIndex++)
        for(int outfit=0;outfit<catalog.outfits.Length;outfit++)
        {
            var original=UnityEngine.Object.Instantiate(catalog.PrefabFor(modelIndex,outfit));
            original.hideFlags=HideFlags.HideAndDontSave;original.transform.position=new Vector3(0,-2000,0);
            try
            {
                var model=original.GetComponentInChildren<HumanoidModel>(true);
                model.SetColor(GameSettings.Base.Character.GetPlayerColor(outfit));
                model.GetComponent<RabbitSurfaceStates>().SetVariant(outfit);
                for(int part=-1;part<4;part++)
                {
                    string path=Art+(part<0?"CostumeMask"+modelIndex:"CostumeLimb"+modelIndex+"_"+part)+".prefab";
                    var prop=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    prop.hideFlags=HideFlags.HideAndDontSave;prop.transform.position=new Vector3(0,-2000,0);
                    try
                    {
                        tint.Invoke(null,new object[]{prop,new EscapeEndingResults.Person{slot=0,model=modelIndex,outfit=outfit}});
                        var target=prop.GetComponentInChildren<Renderer>(true);
                        var source=part<0?model.Original:model.Limbs[part];
                        for(int material=0;material<2;material++)
                        {
                            var a=source.sharedMaterials[material];var b=target.sharedMaterials[material];
                            if(a.mainTexture!=b.mainTexture)throw new Exception("Source atlas changed: "+path);
                            string sourceCode=System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(a.shader));
                            string endingCode=System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(b.shader));
                            int start=sourceCode.IndexOf("float tile=");int end=sourceCode.IndexOf("half3 cloth=",start);
                            if(!endingCode.Contains(sourceCode.Substring(start,end-start)))throw new Exception("Palette/pattern logic changed: "+path);
                            var x=new MaterialPropertyBlock();var y=new MaterialPropertyBlock();
                            source.GetPropertyBlock(x,material);target.GetPropertyBlock(y,material);
                            foreach(string key in new[]{"_BaseColor","_VariantColor"})
                                if(((Vector4)x.GetColor(key)-(Vector4)y.GetColor(key)).sqrMagnitude>1e-8f)throw new Exception(key+" mismatch: "+path+" outfit "+outfit);
                            if(x.GetFloat("_VariantPattern")!=y.GetFloat("_VariantPattern"))throw new Exception("Pattern mismatch: "+path);
                            if(ShaderUtil.GetShaderMessages(b.shader).Any(m=>m.severity.ToString()=="Error"))throw new Exception("Shader compile error: "+b.shader.name);
                            surfaces++;
                        }
                    }
                    finally{UnityEngine.Object.DestroyImmediate(prop);}
                }
                appearances++;
            }
            finally{UnityEngine.Object.DestroyImmediate(original);}
        }
        var coil=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"TungstenCoil.asset");
        if(coil.bounds.size.x>.05f||coil.bounds.size.y>.06f)throw new Exception("Filament exceeds compact dimensions");
        return new{appearances,surfaces,sourceAtlasPreserved=true,paletteAndPatternPreserved=true,filamentBounds=coil.bounds.size.ToString("F4")};
    }
}
