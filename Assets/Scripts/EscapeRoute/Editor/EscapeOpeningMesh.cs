#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.Editor
{
    /// <summary>Derive a mesh with a door aperture; do not modify the shared room-shell asset.</summary>
    static class EscapeOpeningMesh
    {
        struct Vertex
        {
            public Vector3 point, normal;
            public Vector2 uv, uv2;
            public Color color;
            public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex { point=Vector3.Lerp(a.point,b.point,t),
                normal=Vector3.Lerp(a.normal,b.normal,t),uv=Vector2.Lerp(a.uv,b.uv,t),uv2=Vector2.Lerp(a.uv2,b.uv2,t),color=Color.Lerp(a.color,b.color,t) };
        }
        public static void Cut(MeshFilter filter, Bounds aperture, string assetPath)
        {
            var original=filter.sharedMesh;
            var verts=original.vertices; var normals=original.normals; var uv=original.uv; var uv2=original.uv2; var colors=original.colors;
            var output=new List<Vertex>(); var submeshes=new List<int[]>();
            for(int sub=0;sub<original.subMeshCount;sub++)
            {
                var indices=new List<int>(); var triangles=original.GetTriangles(sub);
                for(int t=0;t<triangles.Length;t+=3)
                {
                    var polygon=new List<Vertex>();
                    for(int i=0;i<3;i++)
                    {
                        int n=triangles[t+i]; polygon.Add(new Vertex { point=filter.transform.TransformPoint(verts[n]),
                            normal=normals.Length==verts.Length?normals[n]:Vector3.up, uv=uv.Length==verts.Length?uv[n]:Vector2.zero,
                            uv2=uv2.Length==verts.Length?uv2[n]:Vector2.zero,color=colors.Length==verts.Length?colors[n]:Color.white });
                    }
                    for(int plane=0;plane<6 && polygon.Count>0;plane++)
                    {
                        var inside=new List<Vertex>(); var outside=new List<Vertex>();
                        var a=polygon[polygon.Count-1]; float da=Distance(a.point,aperture,plane);
                        foreach(var b in polygon)
                        {
                            float db=Distance(b.point,aperture,plane); bool ai=da>=0,bi=db>=0;
                            if(ai!=bi) { var cross=Vertex.Lerp(a,b,da/(da-db)); inside.Add(cross); outside.Add(cross); }
                            if(bi) inside.Add(b); else outside.Add(b);
                            a=b; da=db;
                        }
                        if(outside.Count>=3)
                            for(int i=1;i<outside.Count-1;i++)
                                foreach(var v in new[]{outside[0],outside[i],outside[i+1]}) { indices.Add(output.Count); output.Add(v); }
                        polygon=inside;
                    }
                }
                submeshes.Add(indices.ToArray());
            }
            var mesh=new Mesh { name=original.name+" — escape opening",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32 };
            var positions=new Vector3[output.Count]; var ns=new Vector3[output.Count]; var us=new Vector2[output.Count]; var u2=new Vector2[output.Count]; var cs=new Color[output.Count];
            for(int i=0;i<output.Count;i++) { positions[i]=filter.transform.InverseTransformPoint(output[i].point); ns[i]=output[i].normal.normalized; us[i]=output[i].uv; u2[i]=output[i].uv2; cs[i]=output[i].color; }
            mesh.vertices=positions; mesh.normals=ns; mesh.uv=us; mesh.uv2=u2; mesh.colors=cs; mesh.subMeshCount=submeshes.Count;
            for(int i=0;i<submeshes.Count;i++) mesh.SetTriangles(submeshes[i],i);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh,assetPath); filter.sharedMesh=mesh;
            var collider=filter.GetComponent<MeshCollider>(); if(collider!=null) collider.sharedMesh=mesh;
        }
        static float Distance(Vector3 p,Bounds b,int plane)=>plane switch
        { 0=>p.x-b.min.x,1=>b.max.x-p.x,2=>p.y-b.min.y,3=>b.max.y-p.y,4=>p.z-b.min.z,_=>b.max.z-p.z };
    }
}
#endif
