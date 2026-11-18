using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.MainStage.CheerPoints
{
    /// <summary>Repeats the owned rope mesh without stretching its braid. One renderer per ring.</summary>
    public sealed class CheerHangingRope : MonoBehaviour
    {
        Mesh combined, source;
        MeshFilter filter;
        MeshRenderer renderer;
        LineRenderer fallback;
        GameObject knot, knotSource;
        int pieces;
        float width;
        public Vector3 End { get; private set; }
        public Bounds Bounds { get; private set; }

        public void Sample(Vector3 attachment,float length,PerformanceEquipmentSettings settings)
        {
            transform.SetPositionAndRotation(attachment,Quaternion.identity);
            // Keep the braid's thickness independent of the authored ring's scale.
            var parentScale=transform.parent.lossyScale;
            transform.localScale=new Vector3(1/parentScale.x,1/parentScale.y,1/parentScale.z);
            End=attachment+Vector3.up*length;
            float diameter=Mathf.Max(.01f,settings.cheerRopeWidth);
            Bounds=new Bounds((attachment+End)*.5f,new Vector3(diameter,length,diameter));
            var mesh=settings.cheerRopeMesh;
            if(mesh && mesh.isReadable && mesh.bounds.size.y>.001f)
            {
                if(!filter) { filter=gameObject.AddComponent<MeshFilter>(); renderer=gameObject.AddComponent<MeshRenderer>(); }
                renderer.enabled=true; renderer.sharedMaterial=settings.cheerRopeMaterial;
                if(fallback) fallback.enabled=false;
                float scale=diameter/Mathf.Max(.001f,Mathf.Max(mesh.bounds.size.x,mesh.bounds.size.z));
                float step=mesh.bounds.size.y*scale;
                int count=Mathf.CeilToInt(length/step);
                // Camera movement only extends the cached mesh; it does not rebuild every frame.
                if(source!=mesh || !Mathf.Approximately(width,diameter) || count>pieces)
                {
                    ReleaseMesh(); source=mesh; width=diameter; pieces=count+2;
                    var instances=new CombineInstance[pieces];
                    var offset=new Vector3(-mesh.bounds.center.x,-mesh.bounds.min.y,-mesh.bounds.center.z)*scale;
                    for(int i=0;i<pieces;i++) instances[i]=new CombineInstance { mesh=mesh,
                        transform=Matrix4x4.TRS(offset+Vector3.up*(step*i),Quaternion.identity,Vector3.one*scale) };
                    combined=new Mesh { name="Cheer rope (runtime)",hideFlags=HideFlags.HideAndDontSave,indexFormat=IndexFormat.UInt32 };
                    combined.CombineMeshes(instances,true,true); filter.sharedMesh=combined;
                }
                Bounds=renderer.bounds;
                End=attachment+Vector3.up*(step*pieces);
            }
            else
            {
                if(renderer) renderer.enabled=false;
                if(!fallback) { fallback=gameObject.AddComponent<LineRenderer>(); fallback.positionCount=2; fallback.useWorldSpace=true; }
                fallback.enabled=true; fallback.sharedMaterial=settings.cheerRopeMaterial; fallback.widthMultiplier=diameter;
                fallback.SetPosition(0,attachment); fallback.SetPosition(1,End);
            }
            if(knotSource!=settings.cheerRopeKnot)
            {
                if(knot) DestroyObject(knot);
                knotSource=settings.cheerRopeKnot;
                knot=knotSource ? Instantiate(knotSource,transform,false) : null;
                if(knot)
                {
                    foreach(var collider in knot.GetComponentsInChildren<Collider>()) { collider.enabled=false; DestroyObject(collider); }
                    foreach(var part in knot.GetComponentsInChildren<Renderer>()) part.sharedMaterial=settings.cheerRopeMaterial;
                    foreach(var child in knot.GetComponentsInChildren<Transform>()) child.gameObject.hideFlags=gameObject.hideFlags;
                }
            }
            if(knot)
            {
                knot.transform.localPosition=Vector3.down*diameter*.3f;
                knot.transform.localScale=Vector3.one*(diameter/.04f);
                foreach(var part in knot.GetComponentsInChildren<Renderer>()) Bounds=Union(Bounds,part.bounds);
            }
        }
        static Bounds Union(Bounds a,Bounds b) { a.Encapsulate(b); return a; }
        void ReleaseMesh() { if(combined) DestroyObject(combined); combined=null; }
        void OnDestroy() => ReleaseMesh();
        static void DestroyObject(Object value) { if(Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }
}
