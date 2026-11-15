using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CurtainCall.UI.Wayfinding.Editor
{
    public static class NavigationPulseBuilder
    {
        const string Art = "Assets/_ThirdParty/curtaincall_navigation_v2/Sprites/";
        const string Prefabs = "Assets/Resources/Prefabs/UIs/Navigation/V2/";
        public const string ScenePath = "Assets/Scenes/Tests/Navigation/NavigationPulseReview.unity";
        public const string Evidence = "Tasks/Active/Task-20261113-122-rehearsal-navigation-art/Captures/V2/";
        static readonly Color Cyan = new Color(.12f,1,.86f,1);
        static Material core, halo;

        [MenuItem("CurtainCall/UI/Navigation/V2 Build pulse prefabs")]
        public static void Build()
        {
            if(EditorApplication.isPlaying || Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play Mode and save open scenes before rebuilding.");
            foreach(var name in new[]{"chevron-mask","aura-mask","ring-mask","spark-mask"})
            {
                string path=Art+name+".png";
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
                importer.spritePixelsPerUnit=1024;importer.alphaIsTransparency=true;importer.mipmapEnabled=true;
                importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.wrapMode=TextureWrapMode.Clamp;
                importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Uncompressed;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);importer.SaveAndReimport();
            }
            core=MakeMaterial("SignalCore",false);halo=MakeMaterial("SignalHalo",true);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var marker=new GameObject("PulseArrow");marker.AddComponent<SortingGroup>();
            Layer(marker.transform,"00 Soft cyan aura","aura-mask",1.25f,0,0,new Color(Cyan.r,Cyan.g,Cyan.b,.26f),halo);
            Layer(marker.transform,"10 Contrast edge","chevron-mask",.88f,.004f,1,new Color(.005f,.025f,.025f,.95f),core);
            Layer(marker.transform,"20 Cyan direction","chevron-mask",.76f,.008f,2,Cyan,core);
            marker.AddComponent<NavigationPulseGlyph>().BindLayers();
            var markerAsset=Save(marker,"PulseArrow");

            var goal=new GameObject("PulseDestination");goal.AddComponent<SortingGroup>();
            var baseGlyph=new GameObject("Beacon core").transform;baseGlyph.SetParent(goal.transform,false);
            Layer(baseGlyph,"00 Soft cyan aura","aura-mask",2.1f,0,0,new Color(Cyan.r,Cyan.g,Cyan.b,.23f),halo);
            Layer(baseGlyph,"10 Contrast ring","ring-mask",1.7f,.004f,1,new Color(.005f,.025f,.025f,1),core);
            Layer(baseGlyph,"20 Cyan ring","ring-mask",1.55f,.008f,2,Cyan,core);
            Layer(baseGlyph,"30 Theatre sparkle","spark-mask",.62f,.012f,3,Cyan,core);
            baseGlyph.gameObject.AddComponent<NavigationPulseGlyph>().BindLayers();
            var echo=new GameObject("Arrival echo").transform;echo.SetParent(goal.transform,false);
            Layer(echo,"Expanding cyan ring","ring-mask",1.55f,.016f,4,new Color(Cyan.r,Cyan.g,Cyan.b,.6f),core);
            echo.gameObject.AddComponent<NavigationPulseGlyph>().BindLayers();
            var goalAsset=Save(goal,"PulseDestination");

            var root=new GameObject("RehearsalNavigationPulse");
            var destination=(GameObject)PrefabUtility.InstantiatePrefab(goalAsset,root.transform);
            var view=root.AddComponent<RehearsalNavigationView>();view.Configure(markerAsset.transform,destination.transform);
            view.SetPath(NavigationPrefabBuilder.PreviewPath());
            var sequence=root.AddComponent<NavigationPulseSequence>();
            sequence.Configure(view,destination.transform.Find("Beacon core").GetComponent<NavigationPulseGlyph>(),destination.transform.Find("Arrival echo").GetComponent<NavigationPulseGlyph>());
            var asset=Save(root,"RehearsalNavigationPulse");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);
            NavigationPrefabBuilder.BuildStage();
            BuildGallery(markerAsset,goalAsset);
            var camera=new GameObject("Pulse review camera",typeof(Camera)).GetComponent<Camera>();camera.tag="MainCamera";
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.018f,.016f);
            camera.fieldOfView=48;camera.nearClipPlane=.05f;camera.farClipPlane=100;
            camera.transform.position=new Vector3(5.8f,8,-10);camera.transform.LookAt(new Vector3(0,0,2.1f));
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.38f,.32f);
            var light=new GameObject("Review worklight",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;
            light.color=new Color(1,.83f,.65f);light.transform.rotation=Quaternion.Euler(50,-30,0);
            Selection.activeGameObject=instance;
            SceneView.lastActiveSceneView?.LookAt(new Vector3(0,0,2),Quaternion.Euler(53,0,0),12);
            EditorSceneManager.SaveScene(scene,ScenePath);
        }

        static GameObject Save(GameObject go,string name)
        {
            var result=PrefabUtility.SaveAsPrefabAsset(go,Prefabs+name+".prefab");Object.DestroyImmediate(go);return result;
        }

        static Material MakeMaterial(string name,bool soft)
        {
            string path="Assets/Resources/Materials/Navigation/V2/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("CurtainCall/NavigationSignal"));AssetDatabase.CreateAsset(material,path);}
            material.SetFloat("_SoftMask",soft?1:0);material.SetFloat("_PulseOpacity",1);material.SetFloat("_PulseScale",1);
            EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);return material;
        }

        static void Layer(Transform parent,string name,string image,float width,float height,int order,Color tint,Material material)
        {
            var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(parent,false);
            go.transform.localRotation=Quaternion.Euler(90,0,0);go.transform.localPosition=Vector3.up*height;
            var renderer=go.GetComponent<SpriteRenderer>();renderer.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+image+".png");
            var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(Art+image+".png"));
            var pixels=texture.GetPixels32();int minX=texture.width,maxX=0,minY=texture.height,maxY=0;
            for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)
                if(pixels[y*texture.width+x].a>128){minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
            // Sprite.rect reflects importer resizing; normalize using the original alpha fraction.
            if(image=="aura-mask"){minX=0;maxX=texture.width-1;minY=0;maxY=texture.height-1;}
            float visibleFraction=(maxX-minX+1f)/texture.width;
            float scale=width/(renderer.sprite.bounds.size.x*visibleFraction);
            go.transform.localScale=Vector3.one*scale;
            go.transform.localPosition+=new Vector3((texture.width-1-(minX+maxX))*.5f/texture.width*renderer.sprite.bounds.size.x*scale,0,(texture.height-1-(minY+maxY))*.5f/texture.height*renderer.sprite.bounds.size.y*scale);
            Object.DestroyImmediate(texture);renderer.color=tint;renderer.sharedMaterial=material;renderer.sortingOrder=order;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        static void BuildGallery(GameObject arrow,GameObject goal)
        {
            var gallery=new GameObject("V2 flat signal layer gallery").transform;gallery.position=new Vector3(25,0,0);
            NavigationPrefabBuilder.Label(gallery,"GUIDING PULSE  /  MONOCHROME SIGNAL",new Vector3(-5.7f,.04f,3.7f),.26f);
            string[] names={"chevron-mask","aura-mask","ring-mask","spark-mask"};
            string[] labels={"01  DIRECTION MASK","02  SOFT AURA","03  DESTINATION RING","04  THEATRE SPARK"};
            for(int i=0;i<4;i++)
            {
                float x=-4.5f+i*3;var tile=new GameObject(labels[i]).transform;tile.SetParent(gallery,false);tile.localPosition=new Vector3(x,.05f,1.3f);
                Layer(tile,labels[i],names[i],1.7f,0,0,Cyan,i==1?halo:core);
                NavigationPrefabBuilder.Label(gallery,labels[i],new Vector3(x-1,.05f,-.1f),.115f);
            }
            var m=(GameObject)PrefabUtility.InstantiatePrefab(arrow,gallery);m.transform.localPosition=new Vector3(-3,.06f,-2.1f);m.transform.localScale=Vector3.one*1.8f;
            var d=(GameObject)PrefabUtility.InstantiatePrefab(goal,gallery);d.transform.localPosition=new Vector3(1,.06f,-2.1f);d.transform.Find("Arrival echo").gameObject.SetActive(false);
            NavigationPrefabBuilder.Label(gallery,"PULSE ARROW",new Vector3(-4,.05f,-3.5f),.15f);
            NavigationPrefabBuilder.Label(gallery,"DESTINATION",new Vector3(-.1f,.05f,-3.5f),.15f);
            NavigationPrefabBuilder.Label(gallery,"1s PULSE\nPLAYER > GOAL\nEDIT MODE PREVIEW",new Vector3(3,.05f,-2.2f),.16f);
        }

        [MenuItem("CurtainCall/UI/Navigation/V2 Capture and validate")]
        public static string ValidateAndCapture()
        {
            if(SceneManager.GetActiveScene().path!=ScenePath)throw new Exception("Open NavigationPulseReview first.");
            Directory.CreateDirectory(Evidence);
            var sequence=Object.FindFirstObjectByType<NavigationPulseSequence>();var view=sequence.GetComponent<RehearsalNavigationView>();
            var automatic=sequence.AutomaticPlayback;sequence.AutomaticPlayback=false;
            try
            {
                if(view.VisibleMarkerCount<5)throw new Exception("Serialized route pool missing.");
                int nearIndex=view.VisibleMarkerCount-1;
                var near=view.MarkerInstances[nearIndex].GetComponent<NavigationPulseGlyph>();
                var far=view.MarkerInstances[0].GetComponent<NavigationPulseGlyph>();
                float interval=sequence.PulseIntervalSeconds,nearLit=sequence.LightTime(view.GetMarkerGoalDistance(nearIndex))+.2f;
                float travel=(view.GetMarkerGoalDistance(nearIndex)-view.GetMarkerGoalDistance(0))/sequence.WaveSpeed;
                sequence.EvaluateAt(nearLit);Require(near.Opacity>.95f,"A wave must light the arrow nearest the player.");
                sequence.EvaluateAt(nearLit+travel);Require(far.Opacity>.95f,"The same wave must reach the goal side after the player side.");
                if(interval>sequence.FlashSeconds+.05f){sequence.EvaluateAt(nearLit-.2f+(sequence.FlashSeconds+interval)*.5f);Require(near.Opacity<.001f,"Each arrow must rest between waves.");}
                sequence.EvaluateAt(nearLit+interval);Require(near.Opacity>.95f,"The next wave must repeat after the pulse interval.");
                view.SetPath(NavigationPrefabBuilder.PreviewPath());sequence.EvaluateAt(nearLit);Require(near.Opacity>.95f,"Path refresh must retain animation binding.");
                foreach(var sr in sequence.GetComponentsInChildren<SpriteRenderer>(true))Require(sr.sprite!=null&&sr.sharedMaterial!=null,"Missing layer reference.");
                Require(sequence.GetComponentsInChildren<LineRenderer>(true).Length==0,"LineRenderer found.");
                Require(!ShaderUtil.ShaderHasError(Shader.Find("CurtainCall/NavigationSignal")),"Signal shader error.");
                var camera=GameObject.Find("Pulse review camera").GetComponent<Camera>();
                float[] times={.35f,1.35f,2.65f,3.9f};
                for(int i=0;i<times.Length;i++){sequence.EvaluateAt(times[i]);NavigationPrefabBuilder.Capture(camera,Evidence+$"phase-{i+1:00}.png");}
                var p=camera.transform.position;var r=camera.transform.rotation;
                camera.transform.position=new Vector3(25,13,0);camera.transform.rotation=Quaternion.Euler(90,0,0);camera.orthographic=true;camera.orthographicSize=4.8f;
                NavigationPrefabBuilder.Capture(camera,Evidence+"layers.png");
                camera.transform.position=p;camera.transform.rotation=r;camera.orthographic=false;
                string result=$"PASS: player-to-goal wave order, rest between waves, interval repeat, path refresh, serialized marker count, sprite/material references, shader compilation, zero LineRenderers. Preview values: interval {sequence.PulseIntervalSeconds}s, wave {sequence.WaveSpeed}m/s, flash {sequence.FlashSeconds}s, arrival .8s.";
                File.WriteAllText(Evidence+"validation.txt",result);return result;
            }
            finally {sequence.AutomaticPlayback=automatic;}
        }

        static void Require(bool condition,string message){if(!condition)throw new Exception(message);}

        // Fixed-time sampling of the actual Unity prefab renderer, suitable for an exported loop preview.
        public static async Task<string> CaptureLoop()
        {
            Directory.CreateDirectory(Evidence+"Frames");
            var sequence=Object.FindFirstObjectByType<NavigationPulseSequence>();
            var camera=GameObject.Find("Pulse review camera").GetComponent<Camera>();
            bool automatic=sequence.AutomaticPlayback;sequence.AutomaticPlayback=false;
            try
            {
                int frames=Mathf.RoundToInt(sequence.PulseIntervalSeconds*24);
                for(int i=0;i<frames;i++)
                {
                    sequence.EvaluateAt(i/24f);
                    NavigationPrefabBuilder.Capture(camera,Evidence+$"Frames/frame-{i:0000}.png");
                    if(i%8==0)await Task.Yield();
                }
                return $"Rendered {frames} frames at 24fps from the Unity prefab.";
            }
            finally {sequence.AutomaticPlayback=automatic;}
        }
    }
}
