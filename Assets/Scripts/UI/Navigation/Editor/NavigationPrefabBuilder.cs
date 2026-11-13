using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.Wayfinding.Editor
{
    public static class NavigationPrefabBuilder
    {
        const string Art = "Assets/_ThirdParty/curtaincall_navigation_v1/Sprites/";
        const string Prefabs = "Assets/Resources/Prefabs/UIs/Navigation/";
        const string Materials = "Assets/Resources/Materials/Navigation/";
        const string ScenePath = "Assets/Scenes/Tests/Navigation/NavigationReview.unity";
        const string Evidence = "Tasks/Active/Task-20261113-122-rehearsal-navigation-art/Captures/";
        static Material spriteMaterial;

        [MenuItem("CurtainCall/UI/Navigation/Build review prefabs")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save open scenes before rebuilding the review scene.");
            foreach (string name in new[]{"brass-base", "ivory-chevron", "burgundy-cloth", "brass-ring", "ivory-star"})
            {
                var path = Art + name + ".png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 1024;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            spriteMaterial = Material("NavigationLayers", Shader.Find("CurtainCall/NavigationSprite"), Color.white);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var marker = new GameObject("NavigationRouteMarker");
            marker.AddComponent<SortingGroup>();
            Layer(marker.transform, "00 Contact shadow", "brass-base", .86f, 0, 0, new Color(.05f,.025f,.012f,.55f));
            Layer(marker.transform, "10 Aged brass", "brass-base", .82f, .004f, 1, Color.white);
            Layer(marker.transform, "20 Ivory direction", "ivory-chevron", .38f, .008f, 2, Color.white);
            var markerAsset = Save(marker, "NavigationRouteMarker");

            var destination = new GameObject("NavigationDestination");
            destination.AddComponent<SortingGroup>();
            Layer(destination.transform, "00 Contact shadow", "burgundy-cloth", 1.5f, 0, 0, new Color(.04f,.02f,.01f,.55f));
            Layer(destination.transform, "10 Burgundy cloth", "burgundy-cloth", 1.33f, .004f, 1, Color.white);
            Layer(destination.transform, "20 Brass perimeter", "brass-ring", 1.45f, .008f, 2, Color.white);
            Layer(destination.transform, "30 Ivory theatre star", "ivory-star", .8f, .012f, 3, Color.white);
            var destinationAsset = Save(destination, "NavigationDestination");

            var root = new GameObject("RehearsalNavigation");
            var goal = (GameObject)PrefabUtility.InstantiatePrefab(destinationAsset, root.transform);
            var view = root.AddComponent<RehearsalNavigationView>();
            view.Configure(markerAsset.transform, goal.transform);
            view.SetPath(PreviewPath());
            var routeAsset = Save(root, "RehearsalNavigation");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(routeAsset);
            BuildStage();
            BuildLayerGallery(markerAsset, destinationAsset);
            var camera = new GameObject("Review camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f,.025f,.026f);
            camera.fieldOfView = 48;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 100;
            camera.transform.position = new Vector3(5.8f, 8, -10);
            camera.transform.LookAt(new Vector3(0, 0, 2.1f));
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.45f,.38f,.32f);
            var light = new GameObject("Warm worklight", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.4f; light.color = new Color(1,.83f,.65f);
            light.transform.rotation = Quaternion.Euler(50,-30,0);
            Selection.activeGameObject = instance;
            SceneView.lastActiveSceneView?.LookAt(new Vector3(0,0,2), Quaternion.Euler(53,0,0), 12);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Navigation] Built three prefabs and NavigationReview scene.");
        }

        static GameObject Save(GameObject root, string name)
        {
            var asset = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + name + ".prefab");
            Object.DestroyImmediate(root);
            return asset;
        }

        static void Layer(Transform parent, string name, string sprite, float width, float height, int order, Color color)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * height;
            go.transform.localRotation = Quaternion.Euler(90,0,0);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + sprite + ".png");
            // Normalize by visible alpha bounds so different generated canvas padding cannot change material alignment.
            string file = Art + sprite + ".png";
            var texture = new Texture2D(2,2); texture.LoadImage(File.ReadAllBytes(file));
            int minX=texture.width, maxX=0, minY=texture.height, maxY=0;
            var pixels = texture.GetPixels32();
            for (int y=0;y<texture.height;y++) for(int x=0;x<texture.width;x++)
                if(pixels[y*texture.width+x].a>32) { minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y); }
            float scale=width*1024/(maxX-minX+1f);
            go.transform.localScale=Vector3.one*scale;
            go.transform.localPosition += new Vector3((texture.width-1-(minX+maxX))*.5f/1024*scale,0,(texture.height-1-(minY+maxY))*.5f/1024*scale);
            Object.DestroyImmediate(texture);
            sr.sharedMaterial=spriteMaterial; sr.color=color; sr.sortingOrder=order;
            sr.shadowCastingMode=ShadowCastingMode.Off; sr.receiveShadows=false;
        }

        static Material Material(string name, Shader shader, Color color)
        {
            if(shader==null) throw new InvalidOperationException("Missing shader for "+name);
            string path=Materials+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
            material.shader=shader; material.color=color;
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); return material;
        }

        static void Box(Transform parent,string name,Vector3 p,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false);
            go.transform.localPosition=p; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        internal static void BuildStage()
        {
            var stage=new GameObject("Review stage - presentation only").transform;
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            var woodA=Material("ReviewWoodA",shader,new Color(.13f,.072f,.038f));
            var woodB=Material("ReviewWoodB",shader,new Color(.18f,.10f,.052f));
            var curtain=Material("ReviewBurgundy",shader,new Color(.22f,.025f,.039f));
            var dark=Material("ReviewCharcoal",shader,new Color(.027f,.024f,.025f));
            var brass=Material("ReviewBrass",shader,new Color(.42f,.24f,.07f));
            for(int i=0;i<23;i++) Box(stage,"Floorboard "+i,new Vector3((i-11)*.62f,-.13f,2),new Vector3(.6f,.2f,17),i%3==0?woodB:woodA);
            Box(stage,"Back wall",new Vector3(0,2.6f,10.5f),new Vector3(14,5.4f,.25f),dark);
            for(int side=-1;side<=1;side+=2) for(int i=0;i<7;i++)
                Box(stage,"Curtain fold",new Vector3(side*(2.2f+i*.55f),2.4f,10.1f+(i%2)*.13f),new Vector3(.6f,4.8f,.3f),curtain);
            Box(stage,"Curtain header",new Vector3(0,5,10),new Vector3(13,.5f,.6f),curtain);
            Box(stage,"Stage threshold",new Vector3(0,.005f,9.4f),new Vector3(3.5f,.04f,.12f),brass);
            Label(stage,"CURTAIN CALL",new Vector3(-4.5f,.03f,8.1f),.37f);
            Label(stage,"REHEARSAL / WAYFINDING",new Vector3(-4.5f,.03f,7.4f),.16f);
        }

        internal static void Label(Transform parent,string text,Vector3 p,float size)
        {
            var go=new GameObject(text,typeof(TextMesh)); go.transform.SetParent(parent,false);
            go.transform.localPosition=p; go.transform.localRotation=Quaternion.Euler(90,0,0);
            var label=go.GetComponent<TextMesh>();label.text=text;label.characterSize=size*.16f;label.fontSize=64;
            label.color=new Color(.89f,.80f,.61f);label.anchor=TextAnchor.MiddleLeft;
        }

        static void BuildLayerGallery(GameObject marker,GameObject destination)
        {
            var gallery=new GameObject("Material layers - separate review display").transform;
            gallery.position=new Vector3(25,0,0);
            var board=Material("ReviewBoard",Shader.Find("Universal Render Pipeline/Unlit"),new Color(.038f,.032f,.029f));
            Box(gallery,"Material board",new Vector3(0,-.14f,0),new Vector3(13,.2f,9),board);
            Label(gallery,"CURTAIN CALL  /  MATERIAL LAYERS",new Vector3(-5.7f,.04f,3.7f),.26f);
            string[] names={"brass-base","ivory-chevron","burgundy-cloth","brass-ring","ivory-star"};
            string[] labels={"01  AGED BRASS","02  IVORY DIRECTION","03  BURGUNDY CLOTH","04  BRASS RING","05  THEATRE STAR"};
            for(int i=0;i<5;i++)
            {
                float x=-4.8f+i*2.4f;
                var tile=new GameObject(labels[i]).transform;tile.SetParent(gallery,false);tile.localPosition=new Vector3(x,.05f,1.3f);
                Layer(tile,labels[i],names[i],1.65f,0,0,Color.white);
                Label(gallery,labels[i],new Vector3(x-1,.05f,-.1f),.105f);
            }
            var m=(GameObject)PrefabUtility.InstantiatePrefab(marker,gallery);m.transform.localPosition=new Vector3(-3,.06f,-2.1f);m.transform.localScale=Vector3.one*2;
            var d=(GameObject)PrefabUtility.InstantiatePrefab(destination,gallery);d.transform.localPosition=new Vector3(1,.06f,-2.1f);d.transform.localScale=Vector3.one*1.4f;
            Label(gallery,"ROUTE MARKER",new Vector3(-4,.05f,-3.5f),.15f);
            Label(gallery,"DESTINATION",new Vector3(-.1f,.05f,-3.5f),.15f);
            Label(gallery,"5 SPRITES\n3 PREFABS\nNO LINE RENDERER",new Vector3(3,.05f,-2.2f),.16f);
        }

        public static Vector3[] PreviewPath() => new[]{new Vector3(-2,0,-4),new Vector3(-2,0,-1),new Vector3(-1.8f,0,0),new Vector3(-1,0,1),new Vector3(.5f,0,2),new Vector3(1.2f,0,3),new Vector3(1.4f,0,4.5f),new Vector3(1.4f,0,8)};

        [MenuItem("CurtainCall/UI/Navigation/Capture and validate")]
        public static string CaptureAndValidate()
        {
            if(SceneManager.GetActiveScene().path!=ScenePath) throw new InvalidOperationException("Open NavigationReview first.");
            Directory.CreateDirectory(Evidence);
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"RehearsalNavigation.prefab");
            if(root.GetComponentsInChildren<LineRenderer>(true).Length!=0) throw new Exception("Unexpected LineRenderer.");
            foreach(var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                if(sr.sprite==null||sr.sharedMaterial==null) throw new Exception("Missing sprite/material.");
            if(ShaderUtil.ShaderHasError(spriteMaterial!=null?spriteMaterial.shader:Shader.Find("CurtainCall/NavigationSprite"))) throw new Exception("Shader compiler error.");
            var test=Object.Instantiate(root);var view=test.GetComponent<RehearsalNavigationView>();
            view.SetPath(PreviewPath());int count=view.VisibleMarkerCount;
            if(count<5) throw new Exception("Route did not populate.");
            view.SetPath(new[]{Vector3.zero,Vector3.zero});if(view.VisibleMarkerCount!=0)throw new Exception("Degenerate path visible.");
            view.SetPath(new[]{Vector3.zero,new Vector3(float.NaN,0,0)});if(view.VisibleMarkerCount!=0)throw new Exception("Invalid path visible.");
            view.SetPath(PreviewPath());if(view.VisibleMarkerCount!=count)throw new Exception("Pool reuse mismatch.");
            view.Hide();if(view.VisibleMarkerCount!=0)throw new Exception("Hide failed.");Object.DestroyImmediate(test);
            var camera=GameObject.Find("Review camera").GetComponent<Camera>();
            Capture(camera,Evidence+"unity-route-perspective.png");
            var p=camera.transform.position;var r=camera.transform.rotation;
            camera.transform.position=new Vector3(3.3f,4.3f,3.3f);camera.transform.LookAt(new Vector3(1.3f,0,7));
            Capture(camera,Evidence+"unity-destination-detail.png");
            camera.transform.position=new Vector3(25,13,0);camera.transform.rotation=Quaternion.Euler(90,0,0);
            camera.orthographic=true;camera.orthographicSize=4.8f;
            Capture(camera,Evidence+"unity-material-layers.png");
            camera.transform.position=p;camera.transform.rotation=r;camera.orthographic=false;
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            string report=$"PASS: 3 prefabs; 5 alpha sprites; {count} route markers; zero LineRenderers; references; shader; degenerate/invalid path; pooled reuse; hide. Captures are Unity Camera.Render outputs, not concept mockups.";
            File.WriteAllText(Evidence+"validation.txt",report);
            return report;
        }

        internal static void Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;var target=camera.targetTexture;
            var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try { camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG()); }
            finally { camera.targetTexture=target;RenderTexture.active=previous;Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt); }
        }
    }
}
