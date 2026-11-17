using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.WorldIndicators
{
    // Reused buffers. Each layer is one mesh, not one GameObject per dash/hatch/segment.
    public sealed class IndicatorMesh
    {
        readonly List<Vector3> vertices = new(512);
        readonly List<Vector2> uvs = new(512);
        readonly List<Color> colors = new(512);
        readonly List<int> triangles = new(768);
        readonly Mesh mesh;
        public readonly MeshRenderer Renderer;
        readonly Transform frame;
        public IndicatorMesh(Transform parent, string name)
        {
            var existing = parent.Find(name);
            var go = existing ? existing.gameObject : new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false); frame = go.transform;
            mesh = new Mesh { name = name + " (runtime)" }; mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            Renderer = go.GetComponent<MeshRenderer>();
            Renderer.shadowCastingMode = ShadowCastingMode.Off; Renderer.receiveShadows = false;
            Renderer.lightProbeUsage = LightProbeUsage.Off; Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        public void Clear(Material material)
        { vertices.Clear(); uvs.Clear(); colors.Clear(); triangles.Clear(); Renderer.sharedMaterial = material; }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int n=vertices.Count;
            vertices.Add(frame.InverseTransformPoint(a)); vertices.Add(frame.InverseTransformPoint(b));
            vertices.Add(frame.InverseTransformPoint(c)); vertices.Add(frame.InverseTransformPoint(d));
            uvs.Add(new Vector2(0,0)); uvs.Add(new Vector2(1,0)); uvs.Add(new Vector2(1,1)); uvs.Add(new Vector2(0,1));
            for(int i=0;i<4;i++) colors.Add(color);
            triangles.Add(n); triangles.Add(n+1); triangles.Add(n+2); triangles.Add(n); triangles.Add(n+2); triangles.Add(n+3);
        }
        public void Line(Vector3 a, Vector3 b, float width, Color color, Vector3 normal)
        {
            Vector3 delta=b-a;
            if(delta.sqrMagnitude<.0000001f) return;
            Vector3 side=Vector3.Cross(normal,delta).normalized*width*.5f;
            if(side.sqrMagnitude<.0000001f) side=Vector3.Cross(Vector3.up,delta).normalized*width*.5f;
            if(side.sqrMagnitude<.0000001f) side=Vector3.right*width*.5f;
            Quad(a-side,b-side,b+side,a+side,color);
        }
        public void Upload()
        {
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0,uvs); mesh.SetColors(colors);
            mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
            Renderer.enabled=vertices.Count>0 && Renderer.sharedMaterial;
        }
        public void Dispose()
        {
            if(!mesh) return;
            if(Application.isPlaying) Object.Destroy(mesh); else Object.DestroyImmediate(mesh);
        }
    }
}
