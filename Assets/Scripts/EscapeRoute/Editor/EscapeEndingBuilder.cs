using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.UI.SceneIntegration;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static class EscapeEndingBuilder
    {
        const string Art = "Assets/Scenes/Levels/EscapeEndingArt";
        const string Vendor = "Assets/_ThirdParty/Task073/Ending";
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); Folder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
        static Material Meadow(string name, Color color, float wind, float ground)
        {
            var m = new Material(Shader.Find("CurtainCall/Escape/Meadow")) { name = name, enableInstancing = true };
            m.SetColor("_BaseColor", color); m.SetFloat("_Wind", wind); m.SetFloat("_Ground", ground);
            AssetDatabase.CreateAsset(m, Art + "/" + name + ".mat"); return m;
        }
        sealed class ReedMesh
        {
            public readonly List<Vector3> vertices = new();
            readonly List<Vector2> uv = new(); readonly List<Color> colors = new(); readonly List<int> triangles = new();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float lower, float upper)
            {
                int n = vertices.Count; vertices.AddRange(new[] { a, b, c, d });
                uv.AddRange(new[] { new Vector2(0,lower), new Vector2(1,lower), new Vector2(1,upper), new Vector2(0,upper) });
                colors.AddRange(new[] { color, color, color, color });
                triangles.AddRange(new[] { n, n+1, n+2, n, n+2, n+3 });
            }
            public void Stem(Vector3 at, float height, Vector3 lean, float angle)
            {
                var side = new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var green = new Color(.78f,.76f,.54f); var seed = new Color(1.12f,1.04f,.84f);
                for (int i=0;i<3;i++)
                {
                    float t=i/3f,u=(i+1)/3f;
                    Vector3 a=at+Vector3.up*(height*t)+lean*t*t,b=at+Vector3.up*(height*u)+lean*u*u;
                    float w=.016f*(1-t*.6f),v=.016f*(1-u*.6f);
                    Quad(a-side*w,a+side*w,b+side*v,b-side*v,green,t,u);
                    var cross=Vector3.Cross(side,Vector3.up); Quad(a-cross*w,a+cross*w,b+cross*v,b-cross*v,green,t,u);
                }
                for(int leaf=0;leaf<2;leaf++)
                {
                    float t=.32f+leaf*.24f;Vector3 center=at+Vector3.up*(height*t)+lean*t*t;
                    Vector3 dir=Quaternion.AngleAxis(leaf*163,Vector3.up)*side;
                    Vector3 middle=center+dir*.38f+Vector3.up*.16f,end=center+dir*.78f+Vector3.down*.08f;
                    var width=Vector3.Cross(dir,Vector3.up)*.022f;
                    Quad(center,middle+width,end,middle-width,green*.92f,t,t+.12f);
                }
                Vector3 tip=at+Vector3.up*height+lean;
                float plume=.32f+height*.055f;
                for(int k=0;k<6;k++)
                {
                    float a=k*Mathf.PI/3;
                    Vector3 direction=new(Mathf.Cos(a),0,Mathf.Sin(a));
                    var width=Vector3.Cross(direction,Vector3.up)*.012f;
                    for(int layer=0;layer<3;layer++)
                    {
                        Vector3 start=tip+Vector3.up*(plume*layer*.22f);
                        Vector3 end=start+direction*(.11f-layer*.025f)+Vector3.up*(plume*.48f);
                        Quad(start-width,start+width,end+width*.15f,end-width*.15f,seed,.88f,1);
                    }
                }
            }
            public Mesh Save(string name)
            {
                var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);
                mesh.RecalculateNormals();mesh.RecalculateBounds();
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(Art+"/"+name+".asset");
                if(existing!=null){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;}
                AssetDatabase.CreateAsset(mesh,Art+"/"+name+".asset");return mesh;
            }
        }
        static Transform Child(Transform parent,string name)
        {
            var go=new GameObject(name); Undo.RegisterCreatedObjectUndo(go,"Exit meadow");go.transform.SetParent(parent,false);return go.transform;
        }
        static void Rock(Transform parent,string name,Vector3 center,Vector3 size,int variant)
        {
            string source="Assets/_ThirdParty/LeartesStudios/AbandonedFactory/HDRP/Art/Prefabs/SM_Rock"+variant+".prefab";
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source),parent);
            Undo.RegisterCreatedObjectUndo(go,"Existing factory exterior stone");go.name=name;
            foreach(var lod in go.GetComponentsInChildren<LODGroup>(true))
            {
                var levels=lod.GetLODs(); var selected=levels[Mathf.Min(1,levels.Length-1)].renderers;
                foreach(var r in lod.GetComponentsInChildren<Renderer>(true))r.enabled=selected.Contains(r);
                UnityEngine.Object.DestroyImmediate(lod);
            }
            foreach(var c in go.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
            var renderers=go.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            foreach(var r in renderers)
            {
                r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                r.sharedMaterials=r.sharedMaterials.Select(m=>
                {
                    string path=Vendor+"/"+m.name+".mat";var clone=AssetDatabase.LoadAssetAtPath<Material>(path);if(clone!=null)return clone;
                    clone=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=m.name+" exterior"};
                    var texture=m.HasProperty("_BaseColorMap")?m.GetTexture("_BaseColorMap"):m.mainTexture;
                    clone.SetTexture("_BaseMap",texture);clone.SetColor("_BaseColor",new Color(.68f,.64f,.53f));clone.SetFloat("_Smoothness",.08f);
                    foreach(string key in new[]{"_NormalMap","_Normal","_BumpMap"})if(m.HasProperty(key)&&m.GetTexture(key)!=null){clone.SetTexture("_BumpMap",m.GetTexture(key));clone.EnableKeyword("_NORMALMAP");break;}
                    AssetDatabase.CreateAsset(clone,path);return clone;
                }).ToArray();
            }
            Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            go.transform.localScale=Vector3.Scale(go.transform.localScale,new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z));
            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            go.transform.position+=parent.TransformPoint(center)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            foreach(var c in go.GetComponentsInChildren<Component>(true))if(c!=null&&PrefabUtility.IsPartOfPrefabInstance(c))PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        }
        static void Title(Transform parent,EscapeEndingPresentation ending)
        {
            var canvas=new GameObject("Ending title",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(canvas,"The End presentation");canvas.transform.SetParent(parent,false);
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=200;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var group=canvas.GetComponent<CanvasGroup>();group.alpha=0;group.interactable=group.blocksRaycasts=false;
            foreach(bool top in new[]{false,true})
            {
                var bar=new GameObject(top?"Top letterbox":"Bottom letterbox",typeof(RectTransform),typeof(Image));bar.transform.SetParent(canvas.transform,false);
                var rect=(RectTransform)bar.transform;rect.anchorMin=new Vector2(0,top?1:0);rect.anchorMax=new Vector2(1,top?1:0);rect.pivot=new Vector2(.5f,top?1:0);rect.sizeDelta=new Vector2(0,92);
                bar.GetComponent<Image>().color=new Color(.015f,.018f,.014f,.92f);bar.GetComponent<Image>().raycastTarget=false;
            }
            var text=new GameObject("The End?",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(canvas.transform,false);
            var label=text.GetComponent<TextMeshProUGUI>();label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/PerformanceHud/Pretendard-Medium SDF.asset");
            label.text="The End?";label.fontSize=78;label.characterSpacing=10;label.alignment=TextAlignmentOptions.Center;label.color=new Color(1,.97f,.87f);label.raycastTarget=false;
            var t=label.rectTransform;t.anchorMin=t.anchorMax=new Vector2(.5f,.34f);t.pivot=new Vector2(.5f,.5f);t.anchoredPosition=Vector2.zero;t.sizeDelta=new Vector2(1000,150);
            var data=new SerializedObject(ending);data.FindProperty("title").objectReferenceValue=group;data.ApplyModifiedPropertiesWithoutUndo();
        }
        [MenuItem("Tools/CurtainCall/탈출로/A 갈대밭·The End 연출 적용")]
        public static void Apply()
        {
            var scene=SceneManager.GetActiveScene();if(Application.isPlaying||scene.isDirty||scene.path!="Assets/Scenes/Levels/escape_route.unity")throw new InvalidOperationException("Saved A edit scene required");
            var frame=scene.GetRootGameObjects().Single(r=>r.name=="EscapeCampaignFrame").transform;
            var background=frame.Find("EscapeBackground");if(background.Find("56_ExitMeadow")!=null)throw new InvalidOperationException("Edit existing exit meadow directly");
            Folder(Art);Folder(Vendor);EscapeStaticGeometryBatch.Restore();
            var ground=Meadow("meadow-ground",new Color(.32f,.30f,.20f),0,1);var plants=Meadow("meadow-reeds",new Color(.54f,.49f,.31f),.23f,0);
            var field=background.Cast<Transform>().First(t=>t.name.StartsWith("08_Reeds"));
            foreach(var r in field.GetComponentsInChildren<MeshRenderer>(true))
                if(r.sharedMaterials.Any(m=>m!=null&&(m.name=="factory-floor"||m.name=="earth")))
                {Undo.RecordObject(r,"Meadow earth surface");r.sharedMaterials=r.sharedMaterials.Select(_=>ground).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
            var root=Child(background,"56_ExitMeadow");root.SetPositionAndRotation(frame.position,frame.rotation);
            root.localScale=new Vector3(1/background.lossyScale.x,1/background.lossyScale.y,1/background.lossyScale.z);
            Reeds(root,plants);
            Rock(root,"Weathered stone beside exit",new Vector3(408,0,65),new Vector3(3.6f,1.1f,2.3f),1);
            Rock(root,"Meadow margin stone",new Vector3(421,0,94),new Vector3(4.5f,1.4f,3),3);
            Rock(root,"Distant field stone",new Vector3(455,-.1f,51),new Vector3(5.5f,1.8f,3.5f),2);
            var owner=Child(frame,"Escape ending presentation");var ending=Undo.AddComponent<EscapeEndingPresentation>(owner.gameObject);
            var data=new SerializedObject(ending);data.FindProperty("content").objectReferenceValue=frame.GetComponent<EscapeCampaignContent>();
            var content=new SerializedObject(frame.GetComponent<EscapeCampaignContent>());data.FindProperty("exitSaw").objectReferenceValue=content.FindProperty("exitSaw").objectReferenceValue;data.FindProperty("arrivalArea").objectReferenceValue=content.FindProperty("arrivalArea").objectReferenceValue;
            var cameraRoot=Child(root,"Meadow ending camera");cameraRoot.localPosition=new Vector3(407,2.7f,78);cameraRoot.localRotation=Quaternion.LookRotation(new Vector3(1,.015f,.025f));
            var camera=Undo.AddComponent<CinemachineCamera>(cameraRoot.gameObject);camera.Priority=100;camera.Lens.FieldOfView=62;camera.enabled=false;data.FindProperty("endingCamera").objectReferenceValue=camera;data.ApplyModifiedPropertiesWithoutUndo();
            Title(owner,ending);Undo.FlushUndoRecordObjects();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        static void Reeds(Transform root,Material plants)
        {
            var cells=new Dictionary<(int,int),ReedMesh>();var random=new System.Random(7303);
            for(int i=0;i<600;i++)
            {
                float x=404+(float)random.NextDouble()*120,z=29+(float)random.NextDouble()*102;
                if(x<437&&Mathf.Abs(z-78)<5.6f||Mathf.Abs(z-(78+Mathf.Sin((x-430)*.026f)*7))<3.2f)continue;
                if(x<412&&Mathf.Abs(z-65)<3||Mathf.Abs(x-421)<3&&Mathf.Abs(z-94)<3||Mathf.Abs(x-455)<4&&Mathf.Abs(z-51)<3)continue;
                var key=((int)((x-400)/30),(int)((z-20)/30));if(!cells.TryGetValue(key,out var mesh))cells[key]=mesh=new ReedMesh();
                for(int j=0;j<7;j++)
                {
                    Vector3 at=new(x+((float)random.NextDouble()-.5f)*2.5f,0,z+((float)random.NextDouble()-.5f)*2.5f);
                    at.y=at.x<=430&&at.z>=59.25f&&at.z<=96.75f?0:-.1f;
                    mesh.Stem(at,1.55f+(float)random.NextDouble()*1.25f,new Vector3(((float)random.NextDouble()-.5f)*.5f,0,((float)random.NextDouble()-.5f)*.5f),(float)random.NextDouble()*Mathf.PI*2);
                }
            }
            int index=0;foreach(var mesh in cells.Values)
            {
                var node=root.Find("Wind reeds "+index);if(node==null)node=Child(root,"Wind reeds "+index);
                var filter=node.GetComponent<MeshFilter>();if(filter==null)filter=node.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=mesh.Save("reed-band-"+index++);
                var renderer=node.GetComponent<MeshRenderer>();if(renderer==null)renderer=node.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=plants;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
        }
        public static void RefineReeds()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.path!="Assets/Scenes/Levels/escape_route.unity")throw new InvalidOperationException("A Edit Mode required");
            var root=scene.GetRootGameObjects().Single().transform.Find("EscapeBackground/56_ExitMeadow");
            Reeds(root,AssetDatabase.LoadAssetAtPath<Material>(Art+"/meadow-reeds.mat"));
            Undo.FlushUndoRecordObjects();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
    }
}
