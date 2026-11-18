#if UNITY_EDITOR
using System;
using System.Linq;
using CurtainCall.ItemArt;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.MainStage.CheerPoints.Editor
{
    public static class EquipmentUseVisualAuthoring
    {
        public const string Folder = "Assets/_ThirdParty/curtaincall/EquipmentUseFx";
        static readonly string[] Ids = { "spring-shoes", "rescue-gloves", "sticky-gloves", "balloon-pants" };
        [MenuItem("CurtainCall/Performance/Build Equipment Use Effects")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop playback before authoring.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_ThirdParty/curtaincall", "EquipmentUseFx");
            var stroke = Material("Ribbon", null);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/Synty/PolygonHorrorCarnival/Prefabs/FX/FX_Sparks_01.prefab");
            var spark = Material("Motes", source.GetComponentInChildren<ParticleSystemRenderer>().sharedMaterial.mainTexture);
            var settings = AssetDatabase.LoadAssetAtPath<PerformanceEquipmentSettings>(PerformanceEquipmentAuthoring.SettingsPath);
            settings.useVisuals = new GameObject[4];
            var colors = new[] { new Color(1,.55f,.16f), new Color(1,.75f,.32f), new Color(.42f,1,.66f), new Color(.57f,.72f,1) };
            for (int kind = 0; kind < 4; kind++)
            {
                var root = new GameObject("Use_" + Ids[kind]);
                try
                {
                    var fx = root.AddComponent<EquipmentUseVisual>(); fx.tint = colors[kind];
                    fx.strokes = new LineRenderer[kind == 1 ? 3 : 2];
                    for (int j = 0; j < fx.strokes.Length; j++)
                    {
                        var child = new GameObject("Ribbon_" + j); child.transform.SetParent(root.transform, false);
                        var line = child.AddComponent<LineRenderer>(); line.sharedMaterial = stroke;
                        line.useWorldSpace = true; line.positionCount = 49; line.widthMultiplier = kind == 3 ? .018f : .025f;
                        line.startColor = line.endColor = colors[kind]; line.numCapVertices = 3;
                        line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                        fx.strokes[j] = line;
                    }
                    fx.motes = new ParticleSystem[kind == 2 ? 2 : 1];
                    for (int j = 0; j < fx.motes.Length; j++)
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(source); go.name = "SyntyMotes_" + j;
                        go.transform.SetParent(root.transform, false); go.transform.localPosition = Vector3.up * .45f;
                        go.transform.localRotation = Quaternion.Euler(-90,0,0); go.transform.localScale = Vector3.one;
                        var ps = go.GetComponentInChildren<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main = ps.main; main.loop = kind >= 2; main.prewarm = false; main.duration = .5f; main.playOnAwake = true;
                        main.startLifetime = new ParticleSystem.MinMaxCurve(.2f,.5f); main.startSpeed = kind >= 2 ? .25f : 1.2f;
                        main.startSize = new ParticleSystem.MinMaxCurve(.025f,.07f); main.startColor = colors[kind]; main.maxParticles = 80;
                        main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = kind == 0 ? .35f : 0;
                        var emission = ps.emission; emission.enabled = true; emission.rateOverTime = kind >= 2 ? 16 : 0; emission.rateOverDistance = 0;
                        emission.SetBursts(kind >= 2 ? Array.Empty<ParticleSystem.Burst>() : new[] {new ParticleSystem.Burst(0,30)});
                        var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = kind == 2 ? .07f : .35f; shape.angle = 55;
                        var velocity = ps.velocityOverLifetime; velocity.enabled = false;
                        var limit = ps.limitVelocityOverLifetime; limit.enabled = false;
                        var collision = ps.collision; collision.enabled = false;
                        var sheet = ps.textureSheetAnimation; sheet.enabled = false;
                        var color = ps.colorOverLifetime; color.enabled = true;
                        var gradient = new Gradient(); gradient.SetKeys(new[] {new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[] {new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)}); color.color = gradient;
                        var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = spark; renderer.renderMode = ParticleSystemRenderMode.Billboard;
                        fx.motes[j] = ps;
                    }
                    if (kind == 3)
                    {
                        var pivot = new GameObject("InflatingPants").transform; pivot.SetParent(root.transform,false);
                        var model = ItemVisualAssets.Create(Ids[kind],pivot);
                        var renderers = model.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
                        foreach(var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                        float scale = 1.1f / Mathf.Max(.01f,bounds.size.x);
                        model.transform.localPosition = -bounds.center * scale; model.transform.localScale = Vector3.one * scale;
                        foreach(var c in model.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                        pivot.localPosition = Vector3.up * .65f; fx.model = pivot;
                    }
                    foreach(var c in root.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                    settings.useVisuals[kind] = PrefabUtility.SaveAsPrefabAsset(root,Folder + "/Use_" + Ids[kind] + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        }
        static Material Material(string name,Texture texture)
        {
            string path = Folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m,path); }
            m.SetTexture("_BaseMap",texture); m.SetColor("_BaseColor",new Color(2,2,2,1));
            m.SetFloat("_Surface",1); m.SetFloat("_Blend",2); m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend",(float)BlendMode.One);
            m.SetFloat("_ZWrite",0); m.SetFloat("_Cull",0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType","Transparent"); m.renderQueue=3000;
            EditorUtility.SetDirty(m); return m;
        }
    }
}
#endif
