using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.PerformanceBackground;
using CurtainCall.Presentation;
using CurtainCall.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.EditorTools
{
    public static class PerformanceVenueAuthoring
    {
        const string Folder="Assets/_ThirdParty/curtaincall/PerformanceBackground/DeathCircus";
        const string Carnival="Assets/_ThirdParty/Synty/PolygonHorrorCarnival/Prefabs/";
        const string Factory="Assets/_ThirdParty/Dzendor/StylizedFactory/Prefabs/";
        const string Conveyor="Assets/_ThirdParty/Conveyor Works/Prefabs/";
        static Material iron, lens, bulb, beamMaterial;
        static Mesh beamMesh;
        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            int at=path.LastIndexOf('/');EnsureFolder(path.Substring(0,at));AssetDatabase.CreateFolder(path.Substring(0,at),path.Substring(at+1));
        }
        [MenuItem("Tools/CurtainCall/Performance/Select Lighting Settings")]
        public static void SelectSettings(){Selection.activeObject=GameSettings.PerformanceLighting;EditorGUIUtility.PingObject(Selection.activeObject);}
        [MenuItem("Tools/CurtainCall/Performance/Build Death Circus Refit (Once)")]
        public static void BuildMenu()=>Debug.Log(Build());
        public static string Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=PerformanceBackgroundLayout.ScenePath)throw new InvalidOperationException("Open performance_background first.");
            var venue=GameObject.Find("PerformanceBackground/CircularVenue").transform;
            if(venue.Find("33_DeathCircusRefit")!=null)throw new InvalidOperationException("Refit already exists; adjust its objects, do not rebuild.");
            EnsureFolder(Folder);
            var settings=AssetDatabase.LoadAssetAtPath<PerformanceLightingSettings>("Assets/Resources/Settings/GameSettings/PerformanceLightingSettings.asset");
            if(settings==null){settings=ScriptableObject.CreateInstance<PerformanceLightingSettings>();AssetDatabase.CreateAsset(settings,"Assets/Resources/Settings/GameSettings/PerformanceLightingSettings.asset");}
            var linked=new SerializedObject(GameSettings.Current);linked.FindProperty("performanceLighting").objectReferenceValue=settings;linked.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(GameSettings.Current);
            iron=Material("RigIron",new Color(.055f,.045f,.05f),false);
            lens=Material("SpotLens",new Color(.05f,.045f,.04f),true);
            bulb=Material("AudienceBulb",new Color(.9f,.35f,.095f),true);
            beamMaterial=new Material(Shader.Find("CurtainCall/Performance/Focused Beam"));AssetDatabase.CreateAsset(beamMaterial,Folder+"/FocusedBeam.mat");
            beamMesh=Cone();AssetDatabase.CreateAsset(beamMesh,Folder+"/FocusedBeam.asset");
            var root=Node("33_DeathCircusRefit",venue,Vector3.zero);
            var rig=Node("Ceiling Lighting Gallery",root,Vector3.zero);
            for(int row=0;row<6;row++)
            {
                float z=-180+row*132;
                // Repeated native factory trusses form physically supported lighting bars.
                for(int cell=-2;cell<=2;cell++)Place(Factory+"Structure/SM_Truss_Roof_8m.prefab",rig,new Vector3(cell*40,241,z),43,Quaternion.identity);
                for(int side=-1;side<=1;side+=2)
                {
                    var mount=Node("Ceiling Follow Spot "+row+" "+side,rig,new Vector3(side*53,238,z));
                    Cube("Clamp",mount,new Vector3(0,2,0),new Vector3(2,4,2),iron);
                    var head=Node("Rotating Head",mount,Vector3.zero);
                    var housing=Place(Carnival+"Props/SM_Prop_Spotlight_01.prefab",head,Vector3.zero,5,Quaternion.identity);
                    housing.transform.localPosition=new Vector3(0,-1,-1);
                    var glass=Sphere("Lens",head,new Vector3(0,0,1.6f),new Vector3(1.45f,1.45f,.32f),lens);
                    var source=Node("Focused Performer Light",head,new Vector3(0,0,1.75f));
                    var light=source.gameObject.AddComponent<Light>();light.type=LightType.Spot;light.enabled=false;light.range=300;light.spotAngle=4;light.innerSpotAngle=3.2f;light.shadowBias=.05f;light.shadowNormalBias=.1f;light.shadowNearPlane=.2f;
                    var beam=Node("Focused Visible Beam",source,Vector3.zero);beam.gameObject.AddComponent<MeshFilter>().sharedMesh=beamMesh;
                    var renderer=beam.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=beamMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.enabled=false;
                    var fixture=mount.gameObject.AddComponent<PerformanceSpotlightFixture>();fixture.head=head;fixture.spot=light;fixture.beam=renderer;fixture.lens=glass;
                    head.rotation=Quaternion.LookRotation(Vector3.down);
                }
                var wash=Light("Route Key Ceiling "+row,rig,new Vector3(0,235,z),new Vector3(0,0,z),22000,330,50,new Color(1,.81f,.64f));
                wash.shadows=LightShadows.None;
            }
            var audience=Node("Audience Tier Lighting",root,Vector3.zero);
            for(int tier=0;tier<3;tier++)for(int i=0;i<12;i++)
            {
                float angle=i*Mathf.PI*2/12;var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                float y=27+tier*39;var center=new Vector3(0,y,80);
                var p=center+radial*445;
                Light("Audience Wash "+tier+" "+i,audience,p+Vector3.up*9,center+radial*505,2400,145,82,settings.audienceColor);
                for(int j=0;j<3;j++)
                {
                    float a=angle+(j-1)*.10f;var b=new Vector3(Mathf.Cos(a)*452,y,80+Mathf.Sin(a)*452);
                    Sphere("Audience Bulb "+tier+" "+i+" "+j,audience,b,new Vector3(1.4f,1.4f,1.4f),bulb);
                }
            }
            var decor=Node("Circus Drapery And Entrances",root,Vector3.zero);
            var banner=venue.Find("07_VariedIndustrialLayers/TallBanner_0");
            for(int i=0;i<10;i++)
            {
                float angle=(i+.5f)*Mathf.PI*2/10;var p=new Vector3(Mathf.Cos(angle)*385,85,80+Mathf.Sin(angle)*385);
                if(banner!=null)
                {
                    var copy=UnityEngine.Object.Instantiate(banner.gameObject,decor);copy.name="Crimson Hanging Banner "+i;
                    copy.transform.position=p;copy.transform.rotation=Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0);copy.transform.localScale=new Vector3(4,5,3);
                }
                Place(Carnival+"Props/SM_Prop_Flags_03.prefab",decor,p+Vector3.up*30,75,Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0));
                Light("Circus Accent Drapery "+i,decor,p+Vector3.down*14,p+Vector3.up*35,4500,130,55,new Color(.68f,.09f,.10f));
            }
            Place(Carnival+"Props/SM_Prop_Tent_Large_02.prefab",decor,new Vector3(-280,0,370),115,Quaternion.Euler(0,20,0));
            Place(Carnival+"Props/SM_Prop_Tent_Large_01.prefab",decor,new Vector3(270,-6,390),110,Quaternion.Euler(0,-25,0));
            Place(Carnival+"Props/SM_Prop_Carnival_Entrance_01.prefab",decor,new Vector3(0,5,585),95,Quaternion.identity);
            var islands=Node("Threat And Service Islands",root,Vector3.zero);
            for(int side=-1;side<=1;side+=2)for(int station=0;station<3;station++)
            {
                var p=new Vector3(side*(76+station*20),-22-station*13,-116+station*170+(side>0?55:0));
                var group=Node("Service Island "+side+" "+station,islands,p);
                for(int tile=0;tile<3;tile++)Place(Factory+"Structure/SM_Catwalk_Grate_2m.prefab",group,new Vector3(0,0,(tile-1)*15),20,Quaternion.identity);
                Place(Factory+"Structure/SM_Column_IBeam.prefab",group,new Vector3(0,-65,0),65,Quaternion.identity);
                Place(Factory+"Structure/SM_Railing_Steel_2m.prefab",group,new Vector3(side*9,1,0),35,Quaternion.Euler(0,90,0));
                Place(Carnival+"Vehicles/SM_Veh_Wagon_Cage_01.prefab",group,new Vector3(0,4,6),18,Quaternion.Euler(0,side*15,0));
                Place(Factory+"Machines/SM_Hoist_Chain.prefab",group,new Vector3(0,22,6),12,Quaternion.identity);
                Place(Factory+"Machines/SM_Chain_Hanging.prefab",group,new Vector3(0,16,6),12,Quaternion.identity);
                Place(Conveyor+"Conveyor/ConveyorElevator.prefab",group,new Vector3(0,1,-24),22,Quaternion.Euler(0,90,0));
                Place(Factory+"Machines/SM_Control_Panel.prefab",group,new Vector3(side*7,1,-12),7,Quaternion.Euler(0,-side*90,0));
                if(station==1)Place(Factory+"Machines/SM_Grinder_Pedestal.prefab",group,new Vector3(side*12,0,18),10,Quaternion.identity);
                Place(Factory+"PipesDuctsAndElectrical/SM_Pipe_Vertical_5m.prefab",group,new Vector3(side*16,-30,0),55,Quaternion.identity);
                var lamp=Place(Factory+"Lighting/SM_Lamp_Wall_Cage.prefab",group,new Vector3(side*7,12,3),3,Quaternion.identity);
                Light("Circus Accent Cage "+side+" "+station,group,new Vector3(side*6,14,0),new Vector3(0,5,6),650,58,74,new Color(.8f,.20f,.13f));
            }
            // Keep the abyss but lower bulky existing service decks away from the play silhouette.
            var old=venue.Find("32_SuspendedServiceWorks");if(old!=null)old.localPosition+=Vector3.down*18;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=settings.ambientSky;RenderSettings.ambientEquatorColor=settings.ambientEquator;RenderSettings.ambientGroundColor=settings.ambientGround;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=settings.fogColor;RenderSettings.fogDensity=settings.fogDensity*PerformanceBackgroundLayout.Scale;
            AddIndirectGallery();Polish(root.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            return "Built 12 physical ceiling follow fixtures, 6 route washes, 36 audience washes, 108 aisle bulbs, 6 prop islands and circus drapery. Saved only background and linked settings.";
        }
        [MenuItem("Tools/CurtainCall/Performance/Add Indirect Gallery (Once)")]
        public static void AddIndirectGallery()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=PerformanceBackgroundLayout.ScenePath)throw new InvalidOperationException("Open performance_background first.");
            var root=GameObject.Find("PerformanceBackground/CircularVenue/33_DeathCircusRefit").transform;
            if(root.Find("Performance Indirect Gallery")!=null)return;
            var gallery=Node("Performance Indirect Gallery",root,Vector3.zero);
            var metal=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/RigIron.mat");
            var diffuser=Material("IndirectDiffuser",new Color(.45f,.38f,.28f),true);
            for(int row=0;row<11;row++)for(int side=-1;side<=1;side+=2)
            {
                float z=-180+row*54;
                var mount=Node("Side Fill Fixture "+row+" "+side,gallery,new Vector3(side*42,27,z));
                Cube("Service Mast",mount,new Vector3(0,-24,0),new Vector3(.8f,48,.8f),metal);
                Place(Factory+"Lighting/SM_Lamp_Wall_Cage.prefab",mount,Vector3.zero,3,Quaternion.Euler(0,side*90,0));
                Cube("Shielded Diffuser",mount,new Vector3(-side*.8f,1,0),new Vector3(.25f,1.5f,1.5f),diffuser);
                var light=Light("Indirect Fill "+row+" "+side,mount,new Vector3(-side*1.4f,1,0),new Vector3(-side*42,-24,12),1620,90,110,new Color(1,.91f,.81f));
                light.innerSpotAngle=44;light.shadows=LightShadows.None;
                light.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>().renderingLayers=PerformanceFinaleLighting.PerformanceFillLayer;
            }
            Polish(gallery.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        public static void Polish(GameObject root)
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var source=materials[i];
                    if(source==null || !(source.shader.name.StartsWith("Dzendor/") || source.shader.name.Contains("Conveyor")))continue;
                    string path=Folder+"/"+source.name+"_VenueURP.mat";
                    var copy=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(copy==null)
                    {
                        copy=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        copy.SetColor("_BaseColor",new Color(.21f,.16f,.14f));copy.SetFloat("_Metallic",.45f);copy.SetFloat("_Smoothness",.25f);
                        foreach(var prop in source.GetTexturePropertyNames())
                            if(prop.ToLowerInvariant().Contains("base") || prop.ToLowerInvariant().Contains("albedo")){copy.SetTexture("_BaseMap",source.GetTexture(prop));break;}
                        AssetDatabase.CreateAsset(copy,path);
                    }
                    materials[i]=copy;
                }
                renderer.sharedMaterials=materials;
                if(renderer.GetComponentInParent<PerformanceSpotlightFixture>()!=null)renderer.shadowCastingMode=ShadowCastingMode.Off;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            foreach(var collider in root.GetComponentsInChildren<Collider>(true)){collider.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(collider);}
        }
        static Transform Node(string name,Transform parent,Vector3 p){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;return t;}
        static Material Material(string name,Color color,bool unlit){var m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);if(!unlit)m.SetFloat("_Smoothness",.23f);AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;}
        static GameObject Place(string path,Transform parent,Vector3 p,float size,Quaternion rotation)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)throw new InvalidOperationException("Missing prefab "+path);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);go.transform.localPosition=Vector3.zero;go.transform.localRotation=rotation;go.transform.localScale=Vector3.one;
            foreach(var c in go.GetComponentsInChildren<Collider>(true))c.enabled=false;
            foreach(var l in go.GetComponentsInChildren<Light>(true))l.enabled=false;
            var renderers=go.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return go;
            var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
            float factor=size/Mathf.Max(.01f,Mathf.Max(b.size.x,Mathf.Max(b.size.y,b.size.z)));go.transform.localScale*=factor;
            b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
            var desired=parent.TransformPoint(p);go.transform.position+=desired-new Vector3(b.center.x,b.min.y,b.center.z);
            return go;
        }
        static MeshRenderer Cube(string name,Transform parent,Vector3 p,Vector3 scale,Material m)=>Primitive(PrimitiveType.Cube,name,parent,p,scale,m);
        static MeshRenderer Sphere(string name,Transform parent,Vector3 p,Vector3 scale,Material m)=>Primitive(PrimitiveType.Sphere,name,parent,p,scale,m);
        static MeshRenderer Primitive(PrimitiveType type,string name,Transform parent,Vector3 p,Vector3 scale,Material m){var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=m;return r;}
        static Light Light(string name,Transform parent,Vector3 p,Vector3 target,float intensity,float range,float angle,Color color)
        {
            var t=Node(name,parent,p);t.rotation=Quaternion.LookRotation(parent.TransformPoint(target)-t.position);var l=t.gameObject.AddComponent<Light>();l.type=LightType.Spot;l.range=range;l.spotAngle=angle;l.innerSpotAngle=angle*.7f;l.intensity=intensity;l.color=color;l.shadows=LightShadows.None;return l;
        }
        static Mesh Cone()
        {
            const int n=48;var v=new Vector3[n*2];var uv=new Vector2[n*2];var tris=new int[n*6];
            for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;v[i]=new Vector3(Mathf.Cos(a)*.003f,Mathf.Sin(a)*.003f,0);v[i+n]=new Vector3(Mathf.Cos(a),Mathf.Sin(a),1);uv[i]=new Vector2((float)i/n,0);uv[i+n]=new Vector2((float)i/n,1);int j=(i+1)%n;int k=i*6;tris[k]=i;tris[k+1]=i+n;tris[k+2]=j+n;tris[k+3]=i;tris[k+4]=j+n;tris[k+5]=j;}
            var m=new Mesh{name="Focused ceiling beam"};m.vertices=v;m.uv=uv;m.triangles=tris;m.RecalculateNormals();m.RecalculateBounds();return m;
        }
    }
}
