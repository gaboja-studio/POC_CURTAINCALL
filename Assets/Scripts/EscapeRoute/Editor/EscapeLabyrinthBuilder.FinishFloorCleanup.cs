using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static object RemoveLabyrinthLooseFloorDressing()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=LabyrinthScenePath)throw new InvalidOperationException("Stopped Prototype required");
            var frame=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EscapeCampaignFrame");
            var names=new HashSet<string>{"배수 홈","배수 가장자리 습윤 흔적","바닥 보수 이음","자재가 끌린 바닥 흠집","정비 작업 바닥 오염","늘어진 임시 전력선"};
            var referenced=new HashSet<Object>();
            foreach(var c in frame.GetComponentsInChildren<MonoBehaviour>(true))if(c){var it=new SerializedObject(c).GetIterator();while(it.Next(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue)referenced.Add(it.objectReferenceValue);}
            bool Plain(GameObject go)=>go.transform.childCount==0&&!referenced.Contains(go)&&go.GetComponents<Component>().All(c=>(c is Transform||c is MeshRenderer||c is MeshFilter)&&!referenced.Contains(c));
            int looseObjects=0,bakedPieces=0,wholeBatches=0,trimmedBatches=0;
            var removedNames=new List<string>();
            foreach(var r in frame.GetComponentsInChildren<MeshRenderer>(true).ToArray())
            {
                if(!r||!r.enabled||!r.gameObject.activeInHierarchy||!Plain(r.gameObject))continue;
                if(names.Contains(r.name)){removedNames.Add(r.name);Undo.DestroyObjectImmediate(r.gameObject);looseObjects++;continue;}
                if(!r.name.StartsWith("Factory static ",StringComparison.Ordinal))continue;
                var mf=r.GetComponent<MeshFilter>();var source=mf?mf.sharedMesh:null;
                if(!source||source.subMeshCount!=1||r.sharedMaterials.Length!=1||!r.sharedMaterial)continue;
                var material=r.sharedMaterial.name;
                if(material!="factory-steel"&&material!="factory-recess"&&material!="service-marking"&&material!="service-drain"&&material!="service-damp-edge")continue;
                var vertices=source.vertices;var triangles=source.triangles;var parent=Enumerable.Range(0,vertices.Length).ToArray();
                int Find(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
                void Union(int a,int b){parent[Find(a)]=Find(b);}
                var weld=new Dictionary<string,int>();for(int i=0;i<vertices.Length;i++){string key=vertices[i].ToString("F4");if(weld.TryGetValue(key,out int other))Union(i,other);else weld.Add(key,i);}
                for(int i=0;i<triangles.Length;i+=3){Union(triangles[i],triangles[i+1]);Union(triangles[i],triangles[i+2]);}
                var groups=Enumerable.Range(0,triangles.Length/3).GroupBy(i=>Find(triangles[i*3])).ToArray();
                var keep=new List<int>();int removed=0;
                foreach(var group in groups)
                {
                    var indices=group.SelectMany(t=>new[]{triangles[t*3],triangles[t*3+1],triangles[t*3+2]}).ToArray();
                    var points=indices.Distinct().Select(i=>frame.InverseTransformPoint(r.transform.TransformPoint(vertices[i]))).ToArray();
                    var b=new Bounds(points[0],Vector3.zero);foreach(var p in points)b.Encapsulate(p);
                    bool flat=b.size.y<.026f&&Mathf.Max(b.size.x,b.size.z)>.25f;
                    bool floorLevel=new[]{0f,-8f,-22f,-18.5f,-6f}.Any(y=>b.min.y>=y-.025f&&b.max.y<=y+.09f);
                    if(flat&&floorLevel)removed++;else keep.AddRange(indices);
                }
                if(removed==0)continue;bakedPieces+=removed;
                if(keep.Count==0){Undo.DestroyObjectImmediate(r.gameObject);wholeBatches++;continue;}
                const string folder="Assets/Scenes/Tests/EscapeRoute/Pursuer/PolishedFactoryMeshes";
                if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Scenes/Tests/EscapeRoute/Pursuer","PolishedFactoryMeshes");
                var copy=Object.Instantiate(source);copy.name="Polished floor dressing "+r.name;copy.triangles=keep.ToArray();CompactPolishedFactoryMesh(copy);
                string path=folder+"/fitted-static-"+r.name.Substring("Factory static ".Length)+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved){EditorUtility.CopySerialized(copy,saved);Object.DestroyImmediate(copy);EditorUtility.SetDirty(saved);AssetDatabase.SaveAssetIfDirty(saved);}
                else{saved=copy;AssetDatabase.CreateAsset(saved,path);}
                Undo.RecordObject(mf,"Remove old floor line dressing");mf.sharedMesh=saved;trimmedBatches++;
            }
            if(looseObjects+bakedPieces>0)EditorSceneManager.MarkSceneDirty(scene);
            return new{looseObjects,bakedPieces,wholeBatches,trimmedBatches,removedNames,sharedGameplayRulesChanged=false,sharedSourceMeshesChanged=0};
        }
    }
}
