using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    // Background authoring only. No stunt, pursuit, interaction or escape rules.
    public static partial class EscapeBackgroundBuilder
    {
        const string ScenePath = "Assets/Scenes/Levels/escape_route.unity";
        const string RootName = "EscapeBackground";
        const string Materials = "Assets/Scenes/Levels/EscapeRouteMaterials/";
        static readonly Dictionary<string, GameObject> Prefabs = new();
        static Material grey, metal, dark, amber;
        static TMP_FontAsset font;
        static Transform root;

        static Transform Group(string name){var g=new GameObject(name).transform;g.SetParent(root,false);return g;}
        static TMP_FontAsset GetFont()
        {
            const string path=Materials+"escape-labels.asset";
            var f=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if(f!=null)return f;
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/_ThirdParty/curtaincall_c1_v1/Source/Fonts/Pretendard-Medium.ttf");
            f=TMP_FontAsset.CreateFontAsset(source,48,5,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic);
            f.name="Escape Labels";AssetDatabase.CreateAsset(f,path);
            foreach(var texture in f.atlasTextures)AssetDatabase.AddObjectToAsset(texture,f);
            AssetDatabase.AddObjectToAsset(f.material,f);return f;
        }

        static Material Mat(string name,Color color)
        {
            var path=Materials+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m!=null)return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};
            m.SetFloat("_Smoothness",.15f);AssetDatabase.CreateAsset(m,path);return m;
        }
        static GameObject Asset(Transform parent,string name,Vector3 bottom,Vector3 size,float yaw=0)
        {
            if(!Prefabs.TryGetValue(name,out var prefab))
            {
                var path=AssetDatabase.FindAssets(name+" t:Prefab").Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(p=>System.IO.Path.GetFileNameWithoutExtension(p)==name);
                if(path==null)throw new InvalidOperationException("Missing existing asset: "+name);
                Prefabs[name]=prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            g.transform.position=Vector3.zero;g.transform.rotation=Quaternion.Euler(0,yaw,0);
            var rs=g.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            // Scale in the prefab's local axes, compensating for quarter-turn orientation.
            var s=new Vector3(size.x/Mathf.Max(.01f,b.size.x),size.y/Mathf.Max(.01f,b.size.y),size.z/Mathf.Max(.01f,b.size.z));
            if(Mathf.Abs(Mathf.Sin(yaw*Mathf.Deg2Rad))>.99f)s=new Vector3(s.z,s.y,s.x);
            g.transform.localScale=Vector3.Scale(g.transform.localScale,s);
            b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            g.transform.position+=bottom-new Vector3(b.center.x,b.min.y,b.center.z);
            return g;
        }
        static GameObject Box(Transform p,string n,Vector3 center,Vector3 size,Material m)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,true);
            g.transform.position=center;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;return g;
        }
        static void Placeholder(Transform p,string n,Vector3 c,Vector3 s)
        {var g=Box(p,n,c,s,grey);Sign(g.transform,n,c+Vector3.up*(s.y/2+.8f));}
        static void Sign(Transform p,string text,Vector3 pos,float yaw=0,float width=8)
        {
            var g=new GameObject("Label_"+text,typeof(RectTransform),typeof(TextMeshPro));g.transform.SetParent(p,true);
            g.transform.position=pos;g.transform.rotation=Quaternion.Euler(0,yaw,0);
            g.transform.localScale=Vector3.one;
            g.transform.localScale=new Vector3(1/g.transform.TransformVector(Vector3.right).magnitude,1/g.transform.TransformVector(Vector3.up).magnitude,1/g.transform.TransformVector(Vector3.forward).magnitude);
            EnsureGlyphs(text);
            var t=g.GetComponent<TextMeshPro>();t.font=font;t.text=text;t.fontSize=3;t.alignment=TextAlignmentOptions.Center;
            t.color=new Color(1,.78f,.39f);t.rectTransform.sizeDelta=new Vector2(width,1.5f);t.textWrappingMode=TextWrappingModes.NoWrap;
        }
        static void EnsureGlyphs(string text)
        {
            var absent=new string(text.Where(c=>!font.HasCharacter(c)).Distinct().ToArray());
            if(absent.Length>0)font.TryAddCharacters(absent,out _);
            if(text.Any(c=>!font.HasCharacter(c)))throw new InvalidOperationException("Label glyphs missing: "+text);
            EditorUtility.SetDirty(font);
        }
        static void Lamp(Transform p,Vector3 at,bool across)
        {
            Asset(p,"SM_Prop_Light_Industrial_Wall_01",at,new Vector3(.5f,.7f,.5f),across?0:90);
            var g=new GameObject("Warm service light",typeof(Light));g.transform.SetParent(p,true);g.transform.position=at+new Vector3(across?0:1,0,across?-1:0);
            var l=g.GetComponent<Light>();l.type=LightType.Point;l.color=new Color(1,.60f,.27f);l.intensity=5;l.range=16;l.shadows=LightShadows.None;
        }
    }
}
