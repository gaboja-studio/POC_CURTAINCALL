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
        // These disconnected mesh islands were left behind when their pipe, railing,
        // light or stair support was removed. Their ThirdParty source meshes stay intact.
        public static object FinishLabyrinthOrphanDecorations()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=LabyrinthScenePath)
                throw new InvalidOperationException("Stopped Prototype required");
            var selected=new Dictionary<int,int[]> {
                {25,new[]{0,1,2,3}},{26,new[]{0,1,2}},{29,new[]{8}},
                {37,new[]{0,1,2,3}},{45,new[]{0,1,2,3}},{46,new[]{0,1,2,3}},
                {52,new[]{0,1,2}},{83,new[]{2}},{91,new[]{0}},{92,new[]{5}} };
            var frame=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="EscapeCampaignFrame");
            const string folder="Assets/Scenes/Tests/EscapeRoute/Pursuer/PolishedFactoryMeshes";
            var renderers=frame.GetComponentsInChildren<MeshRenderer>(true);
            int removedParts=0,removedInstances=0,copiedMeshes=0;
            foreach(var pair in selected)
            {
                var r=renderers.SingleOrDefault(r=>r&&r.name=="Factory static "+pair.Key);
                if(!r)continue;
                var filter=r.GetComponent<MeshFilter>(); var mesh=filter?filter.sharedMesh:null;
                if(mesh&&AssetDatabase.GetAssetPath(mesh)==folder+"/fitted-static-"+pair.Key+".asset")
                { if(CompactPolishedFactoryMesh(mesh)){EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssetIfDirty(mesh);}continue; }
                if(!mesh||AssetDatabase.GetAssetPath(mesh)!="Assets/_ThirdParty/Task073/FactoryMeshes/static-"+pair.Key+".asset"
                    ||r.transform.childCount!=0||r.GetComponents<Component>().Any(c=>!(c is Transform||c is MeshFilter||c is MeshRenderer)))
                    throw new InvalidOperationException("Unexpected legacy decorative batch: "+pair.Key);
                var vertices=mesh.vertices;var triangles=mesh.triangles;
                int[] parent=Enumerable.Range(0,vertices.Length).ToArray();
                int Find(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
                void Union(int a,int b){parent[Find(a)]=Find(b);}
                var weld=new Dictionary<string,int>();
                for(int i=0;i<vertices.Length;i++){var key=vertices[i].ToString("F4");if(weld.TryGetValue(key,out int other))Union(i,other);else weld.Add(key,i);}
                for(int i=0;i<triangles.Length;i+=3){Union(triangles[i],triangles[i+1]);Union(triangles[i],triangles[i+2]);}
                var groups=Enumerable.Range(0,triangles.Length/3).GroupBy(i=>Find(triangles[i*3])).ToArray();
                if(pair.Value.Any(i=>i>=groups.Length))throw new InvalidOperationException("Legacy mesh islands changed: "+pair.Key);
                var keep=groups.Where((g,i)=>!pair.Value.Contains(i)).SelectMany(g=>g.SelectMany(t=>new[]{triangles[t*3],triangles[t*3+1],triangles[t*3+2]})).ToArray();
                removedParts+=pair.Value.Length;
                if(keep.Length==0){Undo.DestroyObjectImmediate(r.gameObject);removedInstances++;continue;}
                if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Scenes/Tests/EscapeRoute/Pursuer","PolishedFactoryMeshes");
                var copy=Object.Instantiate(mesh);copy.name="Polished factory static "+pair.Key;
                copy.triangles=keep;CompactPolishedFactoryMesh(copy);
                string path=folder+"/fitted-static-"+pair.Key+".asset";
                if(AssetDatabase.LoadAssetAtPath<Mesh>(path))throw new InvalidOperationException("Unconnected polish mesh already exists: "+path);
                AssetDatabase.CreateAsset(copy,path);Undo.RecordObject(filter,"Remove orphaned decorative islands");filter.sharedMesh=copy;copiedMeshes++;
            }
            int looseFlanges=0,looseConcrete=0;
            foreach(var r in renderers)
            {
                if(!r)continue;
                var p=frame.InverseTransformPoint(r.bounds.center);
                bool flange=r.name=="배관 플랜지 이음"&&Mathf.Abs(p.x-123)<.01f&&Mathf.Abs(p.z-43.9f)<.01f
                    &&new[]{-65f,-58.46154f,20f}.Any(y=>Mathf.Abs(p.y-y)<.01f);
                bool fragment=r.name=="Concrete remainder"&&Vector3.Distance(p,new Vector3(84.99186f,-17.4f,-78))<.01f
                    &&Vector3.Distance(r.bounds.size,new Vector3(1.18366f,.07f,.07f))<.01f;
                if(!flange&&!fragment)continue;
                if(r.transform.childCount!=0||r.GetComponents<Component>().Any(c=>!(c is Transform||c is MeshFilter||c is MeshRenderer||c is BoxCollider||c is MeshCollider)))
                    throw new InvalidOperationException("Unexpected detached decoration components");
                Undo.DestroyObjectImmediate(r.gameObject);
                if(flange)looseFlanges++;else looseConcrete++;
            }
            if(removedParts+looseFlanges+looseConcrete>0)EditorSceneManager.MarkSceneDirty(scene);
            return new{removedParts,removedInstances,copiedMeshes,looseFlanges,looseConcrete,sharedSourceMeshesChanged=0};
        }

        static bool CompactPolishedFactoryMesh(Mesh mesh)
        {
            var triangles=mesh.triangles;var used=triangles.Distinct().OrderBy(i=>i).ToArray();
            if(used.Length==mesh.vertexCount){mesh.RecalculateBounds();return false;}
            var map=used.Select((old,index)=>(old,index)).ToDictionary(p=>p.old,p=>p.index);
            int count=mesh.vertexCount;var vertices=mesh.vertices;var normals=mesh.normals;
            var tangents=mesh.tangents;var colors=mesh.colors32;
            var uv=new List<Vector4>[8];for(int channel=0;channel<8;channel++){uv[channel]=new();mesh.GetUVs(channel,uv[channel]);}
            mesh.Clear();mesh.vertices=used.Select(i=>vertices[i]).ToArray();
            if(normals.Length==count)mesh.normals=used.Select(i=>normals[i]).ToArray();
            if(tangents.Length==count)mesh.tangents=used.Select(i=>tangents[i]).ToArray();
            if(colors.Length==count)mesh.colors32=used.Select(i=>colors[i]).ToArray();
            for(int channel=0;channel<8;channel++)if(uv[channel].Count==count)mesh.SetUVs(channel,used.Select(i=>uv[channel][i]).ToList());
            mesh.triangles=triangles.Select(i=>map[i]).ToArray();mesh.RecalculateBounds();return true;
        }
    }
}
