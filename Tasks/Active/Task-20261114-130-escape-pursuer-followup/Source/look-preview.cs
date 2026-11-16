using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using CurtainCall.EscapeRoute;

public static class LookPreview
{
    public static string Run()
    {
        const string folder = "Assets/Resources/Models/Characters/Enemies/CircusGolemRevision5/";
        const string output = "Tasks/Active/Task-20261114-130-escape-pursuer-followup/Preview/";
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Look render"); SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = new Vector3(10000, 10000, 10000);
        var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(folder + "circus-golem.fbx"), root.transform);
        foreach (var animation in go.GetComponents<Animation>()) UnityEngine.Object.DestroyImmediate(animation);
        var doll = go.AddComponent<EscapePursuerDoll>(); doll.enabled = false;
        doll.stretchIsolation = EscapePursuerDoll.PrepareRig(go.transform);
        doll.clips = Enum.GetNames(typeof(EscapePursuerDoll.Motion)).Select(n => AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Resources/Animations/Enemies/CircusGolemRevision5/Golem_" + n + ".anim")).ToArray();
        CurtainCall.EscapeRoute.EditorTools.EscapePursuerLookBuilder.Configure(doll);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Characters/Enemies/UnderstagePursuer/EscapeAPursuer.prefab");
        var materials = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            skin.sharedMaterials = materials.Single(s => s.name == skin.name).sharedMaterials;
        var camera = new GameObject("Look preview camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform, false);
        camera.scene = scene;
        camera.transform.localPosition = new Vector3(1.8f, 3.7f, 11);
        camera.transform.LookAt(root.transform.TransformPoint(new Vector3(0, 2.7f, 0)));
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.045f, .05f, .06f);
        camera.fieldOfView = 31; camera.farClipPlane = 40; camera.nearClipPlane = .1f;
        var key = new GameObject("Look preview key").AddComponent<Light>(); key.transform.SetParent(root.transform, false);
        key.type = LightType.Directional; key.intensity = 3.2f; key.transform.rotation = Quaternion.Euler(35, -155, 0);
        var fill = new GameObject("Look preview fill").AddComponent<Light>(); fill.transform.SetParent(root.transform, false);
        fill.type = LightType.Directional; fill.intensity = 1.1f; fill.transform.rotation = Quaternion.Euler(25, 40, 0);
        var texture = new RenderTexture(900, 900, 24); camera.targetTexture = texture;
        var png = new Texture2D(900, 900, TextureFormat.RGB24, false);
        var oldRT = RenderTexture.active;
        var baked = new Mesh();
        // Several poses are rendered within one editor frame. Bake each skin explicitly,
        // since the GPU skinning buffer is normally refreshed only once per frame.
        var skins = go.GetComponentsInChildren<SkinnedMeshRenderer>();
        var renderMeshes = skins.Select(s =>
        {
            var part = new GameObject(s.name + " rendered pose"); part.transform.SetParent(s.transform, false);
            var mesh = new Mesh(); part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterials = s.sharedMaterials; s.enabled = false;
            return mesh;
        }).ToArray();
        try
        {
            for (int side = -1; side <= 1; side++)
            {
                var point = root.transform.TransformPoint(new Vector3(side * 6, 1.7f, 7));
                for (int i = 0; i < 120; i++) { doll.Sample(EscapePursuerDoll.Motion.Idle, .25f); doll.ApplyLook(point, 1f / 60); }
                for (int i = 0; i < skins.Length; i++) skins[i].BakeMesh(renderMeshes[i], true);
                camera.Render(); RenderTexture.active = texture;
                png.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); png.Apply();
                File.WriteAllBytes(output + "look-" + (side + 1) + ".png", png.EncodeToPNG());
            }
            float highest = 0;
            for (int f = 0; f <= 30; f++)
            {
                doll.Sample(EscapePursuerDoll.Motion.CrouchWalk, f / 30f);
                doll.ApplyLook(root.transform.TransformPoint(new Vector3(2, 1.5f, 7)), 1f / 30);
                foreach (var skin in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.BakeMesh(baked, true);
                    foreach (var v in baked.vertices) highest = Mathf.Max(highest, root.transform.InverseTransformPoint(skin.transform.TransformPoint(v)).y);
                }
            }
            return "Rendered three Unity look poses; crouched tracking height=" + highest.ToString("F4") + "m";
        }
        finally
        {
            RenderTexture.active = oldRT; camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(baked); UnityEngine.Object.DestroyImmediate(png);
            foreach (var mesh in renderMeshes) UnityEngine.Object.DestroyImmediate(mesh);
            texture.Release(); UnityEngine.Object.DestroyImmediate(texture);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
