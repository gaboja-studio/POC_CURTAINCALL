#if UNITY_EDITOR
using System;
using System.IO;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.WorldIndicators.EditorTools
{
    public static class WorldIndicatorBuilder
    {
        public const string Kit="Assets/_ThirdParty/curtaincall_world_ui_v1";
        public const string Root="Assets/Resources/Prefabs/UIs/WorldIndicators";
        public const string SettingsPath="Assets/Resources/Settings/GameSettings/WorldUiSettings.asset";
        public const string ScenePath="Assets/Scenes/Tests/WorldIndicators/WorldIndicators.unity";
        static readonly string[] Names={"CircleFrame","CircleFill","RectangleFrame","RectangleFill","Hatch","Dash","Arrowhead","ProgressTrack","TargetReticle"};
        public static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path)) return;
            string parent=path.Substring(0,path.LastIndexOf('/')); Folder(parent);
            AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));
        }
        [MenuItem("Tools/Curtain Call/World UI/Build approved kit and review scene")]
        public static string Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the current scene before building.");
            Folder(Kit+"/Sprites"); Folder(Root); Folder(Root+"/Materials"); Folder("Assets/Scenes/Tests/WorldIndicators");
            var style=Asset<WorldIndicatorStyle>(Root+"/WorldIndicatorStyle.asset");
            style.layers=new Sprite[Names.Length];
            for(int i=0;i<Names.Length;i++) style.layers[i]=Layer(i);
            style.stroke=Material("Stroke",style.layers[5].texture);
            style.circleFill=Material("CircleFill",style.layers[1].texture);
            style.rectangleFill=Material("RectangleFill",style.layers[3].texture);
            style.arrow=Material("Arrow",style.layers[6].texture);
            style.target=Material("Target",style.layers[8].texture);
            EditorUtility.SetDirty(style);
            var settings=Asset<WorldUiSettings>(SettingsPath); settings.style=style;
            settings.circlePrefab=Prefab<AreaIndicatorView>("DangerCircle",style,true);
            settings.rectanglePrefab=Prefab<AreaIndicatorView>("DangerRectangle",style,false);
            settings.pathPrefab=Prefab<PathIndicatorView>("DangerPath",style,false);
            settings.volumePrefab=Prefab<SweptVolumeIndicatorView>("AirborneGrab",style,false);
            settings.trajectoryPrefab=Prefab<TrajectoryIndicatorView>("CannonTrajectory",style,false);
            settings.targetPrefab=Prefab<TargetIndicatorView>("GrabTarget",style,false);
            EditorUtility.SetDirty(settings);
            var game=AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Resources/Settings/GameSettings/GameSettings.asset");
            if(!game) throw new InvalidOperationException("Canonical GameSettings missing.");
            var data=new SerializedObject(game); data.FindProperty("worldUi").objectReferenceValue=settings; data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets(); BuildScene(settings); AssetDatabase.SaveAssets();
            return "9 independent RGBA sprites; 6 layered prefabs; WorldUiSettings linked; review scene saved";
        }
        static T Asset<T>(string path) where T:ScriptableObject
        {
            var value=AssetDatabase.LoadAssetAtPath<T>(path);
            if(!value) { value=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value,path); }
            return value;
        }
        static Material Material(string name,Texture texture)
        {
            string path=Root+"/Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat) { mat=new Material(Shader.Find("CurtainCall/UI/WorldIndicator")); AssetDatabase.CreateAsset(mat,path); }
            mat.mainTexture=texture; mat.enableInstancing=true; EditorUtility.SetDirty(mat); return mat;
        }
        static T Prefab<T>(string name,WorldIndicatorStyle style,bool circle) where T:WorldIndicatorView
        {
            var go=new GameObject(name); var view=go.AddComponent<T>();
            if(view is AreaIndicatorView area) area.circular=circle;
            foreach(string layer in new[]{"Fill","Boundary","Hatch","Progress","Direction","Arrow"})
            {
                var child=new GameObject(layer,typeof(MeshFilter),typeof(MeshRenderer)); child.transform.SetParent(go.transform,false);
                var r=child.GetComponent<MeshRenderer>(); r.sharedMaterial=layer=="Fill"?(circle?style.circleFill:style.rectangleFill):layer=="Arrow"?style.arrow:style.stroke;
                r.shadowCastingMode=ShadowCastingMode.Off; r.receiveShadows=false; r.enabled=false;
            }
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"/"+name+".prefab"); UnityEngine.Object.DestroyImmediate(go);
            return prefab.GetComponent<T>();
        }
        // Native UI authoring: analytic masks preserve the approved silhouette and exact alpha.
        // These are exported independently; generated concept art is retained only as reference.
        static Sprite Layer(int kind)
        {
            int w=kind==5?128:512,h=kind==5?32:kind==7?64:512;
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false); var pixels=new Color32[w*h];
            for(int y=0;y<h;y++) for(int x=0;x<w;x++)
            {
                float sum=0;
                for(int sy=0;sy<4;sy++) for(int sx=0;sx<4;sx++)
                {
                    float u=(x+(sx+.5f)/4)/w*2-1,v=(y+(sy+.5f)/4)/h*2-1;
                    float r=Mathf.Sqrt(u*u+v*v),box=Mathf.Max(Mathf.Abs(u),Mathf.Abs(v));
                    bool inside=kind switch {
                        0 => r>=.945f && r<=.995f || Mathf.Abs(u)<.018f && Mathf.Abs(v)>.90f || Mathf.Abs(v)<.018f && Mathf.Abs(u)>.90f,
                        1 => r<=.995f,
                        2 => box>=.95f && box<=.995f,
                        3 => box<=.995f,
                        4 => Mathf.Repeat((u+v)*4,1)<.22f,
                        5 => box<=.995f,
                        6 => u>=-.98f && u<=.98f && Mathf.Abs(v)<(.98f-u)*.5f,
                        7 => box<=.995f && (Mathf.Abs(u)>.98f || Mathf.Abs(v)>.78f),
                        8 => Mathf.Abs(u)+Mathf.Abs(v)>.74f && Mathf.Abs(u)+Mathf.Abs(v)<.88f,
                        _ => false };
                    if(inside) sum+=1/16f;
                }
                pixels[y*w+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(sum*255));
            }
            texture.SetPixels32(pixels); texture.Apply(); string path=Kit+"/Sprites/"+Names[kind]+".png";
            File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single; importer.alphaIsTransparency=true; importer.mipmapEnabled=true;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.filterMode=FilterMode.Bilinear;
            importer.wrapMode=kind==4?TextureWrapMode.Repeat:TextureWrapMode.Clamp; importer.spritePixelsPerUnit=256;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static void BuildScene(WorldUiSettings settings)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("World UI review"); var preview=root.AddComponent<WorldIndicatorPreview>();
            var camera=new GameObject("Review Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
            camera.tag="MainCamera"; camera.fieldOfView=48; camera.nearClipPlane=.08f; camera.farClipPlane=120;
            camera.backgroundColor=new Color(.06f,.085f,.10f); camera.clearFlags=CameraClearFlags.SolidColor; preview.reviewCamera=camera;
            camera.transform.SetPositionAndRotation(new Vector3(16,17,-20),Quaternion.LookRotation(new Vector3(0,1.5f,5)-new Vector3(16,17,-20)));
            RenderSettings.ambientLight=new Color(.36f,.40f,.43f); RenderSettings.ambientMode=AmbientMode.Flat;
            var light=new GameObject("Stage key",typeof(Light)).GetComponent<Light>(); light.type=LightType.Directional; light.intensity=1.4f;
            light.transform.rotation=Quaternion.Euler(50,-30,0); light.shadows=LightShadows.Soft;
            var floor=Lit("Floor",new Color(.14f,.18f,.20f)); var concrete=Lit("Concrete",new Color(.24f,.28f,.29f));
            var brass=Lit("Brass",new Color(.50f,.38f,.18f)); var actor=Lit("Actor",new Color(.60f,.65f,.63f));
            Box("Stage floor",new Vector3(0,-.25f,5),new Vector3(24,.5f,26),floor);
            for(int i=-10;i<=10;i+=2) Box("Floor seam",new Vector3(i,.001f,5),new Vector3(.012f,.004f,26),concrete);
            Box("Backdrop",new Vector3(0,3.5f,17),new Vector3(24,7,.4f),concrete);
            Box("Landing plinth",new Vector3(4,1.05f,7),new Vector3(2,2.1f,2),concrete);
            Box("Left truss",new Vector3(-8,3,7),new Vector3(.35f,6,.35f),brass);
            preview.hand=Primitive("Hand sweep source",PrimitiveType.Sphere,new Vector3(-5,2.5f,7),Vector3.one*.7f,brass).transform;
            preview.runner=Primitive("Crossing runner capsule",PrimitiveType.Capsule,new Vector3(-.5f,1.7f,7),new Vector3(.5f,.85f,.5f),actor).transform;
            preview.targetRunner=Primitive("Target runner capsule",PrimitiveType.Capsule,new Vector3(4,3f,7),new Vector3(.5f,.85f,.5f),actor).transform;
            var barrel=Primitive("Cannon barrel",PrimitiveType.Cylinder,new Vector3(5,.85f,-4),new Vector3(.65f,1.1f,.65f),brass);
            barrel.transform.rotation=Quaternion.Euler(40,0,0);
            preview.cannonMuzzle=new GameObject("Muzzle").transform; preview.cannonMuzzle.position=new Vector3(5,1.5f,-3.3f);
            // NewScene can unload the prefab handles retained during asset creation.
            preview.circle=Instantiate<AreaIndicatorView>("DangerCircle",root.transform);
            preview.rectangle=Instantiate<AreaIndicatorView>("DangerRectangle",root.transform);
            preview.grab=Instantiate<SweptVolumeIndicatorView>("AirborneGrab",root.transform);
            preview.path=Instantiate<PathIndicatorView>("DangerPath",root.transform);
            preview.trajectory=Instantiate<TrajectoryIndicatorView>("CannonTrajectory",root.transform);
            preview.target=Instantiate<TargetIndicatorView>("GrabTarget",root.transform);
            var canvasGo=new GameObject("Review threat overlay",typeof(Canvas)); canvasGo.transform.SetParent(root.transform);
            var canvas=canvasGo.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=-5;
            var edgeGo=new GameObject("Shared danger edge (review)",typeof(RectTransform),typeof(CanvasRenderer),typeof(CurtainCall.WaitingRoom.Pursuit.PursuitEdgeGraphic));
            edgeGo.transform.SetParent(canvasGo.transform,false);
            preview.threatEdges=edgeGo.GetComponent<CurtainCall.WaitingRoom.Pursuit.PursuitEdgeGraphic>(); preview.threatEdges.raycastTarget=false;
            var rect=(RectTransform)edgeGo.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
            Label("RADIAL AREA",new Vector3(-5,.06f,-2.3f)); Label("RECTANGULAR AREA",new Vector3(1,.06f,-2.3f));
            Label("AIRBORNE GRAB",new Vector3(-3,5.2f,8)); Label("HAZARD TRAVEL",new Vector3(-2,.06f,14));
            EditorSceneManager.SaveScene(scene,ScenePath);
        }
        static T Instantiate<T>(string name,Transform parent) where T:Component => ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+name+".prefab"),parent)).GetComponent<T>();
        static Material Lit(string name,Color color)
        {
            string path=Root+"/Materials/Review"+name+".mat"; var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
            mat.color=color; EditorUtility.SetDirty(mat); return mat;
        }
        static GameObject Primitive(string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat)
        { var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.position=pos; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=mat; return go; }
        static void Box(string name,Vector3 p,Vector3 s,Material m) => Primitive(name,PrimitiveType.Cube,p,s,m);
        static void Label(string text,Vector3 at)
        {
            var go=new GameObject(text,typeof(TextMesh)); go.transform.position=at;
            var label=go.GetComponent<TextMesh>(); label.text=text; label.fontSize=60; label.characterSize=.075f;
            label.anchor=TextAnchor.MiddleCenter; label.color=new Color(.8f,.75f,.6f);
            go.transform.rotation=Quaternion.Euler(80,0,0);
        }
    }
}
#endif
