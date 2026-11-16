using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using CurtainCall.EscapeRoute;
using CurtainCall.EscapeRoute.EditorTools;
using Object = UnityEngine.Object;

public static class HeavyGaitPreview
{
    public static string Run()
    {
        const string output = ".harness/local/pursuer-weight-20261116/unity-preview/";
        Directory.CreateDirectory(output);
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Heavy gait inspection"); SceneManager.MoveGameObjectToScene(root, scene);
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EscapePursuerRevision7Builder.Model + "circus-golem-look.fbx"), root.transform);
        var doll = go.AddComponent<EscapePursuerDoll>(); doll.enabled = false;
        EscapePursuerRevision7Builder.Configure(doll);
        var camera = new GameObject("Inspection camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform, false);
        camera.scene = scene; camera.transform.localPosition = new Vector3(7, 3.7f, 9);
        camera.transform.LookAt(new Vector3(0, 2.3f, 0));
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.065f,.075f,.085f);
        camera.fieldOfView = 39; camera.farClipPlane = 100;
        foreach (var pair in new[] { (angle: new Vector3(35,-145,0), intensity: 2.8f), (angle: new Vector3(35,40,0), intensity: .8f) })
        {
            var light = new GameObject("Inspection light").AddComponent<Light>(); light.transform.SetParent(root.transform, false);
            light.type = LightType.Directional; light.intensity = pair.intensity; light.transform.rotation = Quaternion.Euler(pair.angle);
        }
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(root.transform, false);
        floor.transform.localPosition = new Vector3(0,-.12f,0); floor.transform.localScale = new Vector3(10,.2f,10);
        var texture = new RenderTexture(800,800,24); camera.targetTexture = texture;
        var png = new Texture2D(800,800,TextureFormat.RGB24,false);
        var old = RenderTexture.active;
        var skins = go.GetComponentsInChildren<SkinnedMeshRenderer>();
        var meshes = skins.Select(s => {
            var part = new GameObject(s.name + " baked"); part.transform.SetParent(s.transform,false);
            var mesh = new Mesh(); part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterials = s.sharedMaterials; s.enabled = false; return mesh;
        }).ToArray();
        try
        {
            for (int frame = 0; frame < 32; frame++)
            {
                doll.Sample(EscapePursuerDoll.Motion.Run, frame/32f);
                for (int i = 0; i < skins.Length; i++) skins[i].BakeMesh(meshes[i],true);
                camera.Render(); if (frame == 0) camera.Render(); RenderTexture.active = texture;
                png.ReadPixels(new Rect(0,0,800,800),0,0); png.Apply();
                File.WriteAllBytes(output + $"run-{frame:00}.png",png.EncodeToPNG());
            }
            return "32 Unity run frames rendered with production materials.";
        }
        finally
        {
            RenderTexture.active = old; camera.targetTexture = null;
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(png); texture.Release(); Object.DestroyImmediate(texture);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
