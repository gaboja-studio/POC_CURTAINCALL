using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>One camera-local silhouette of a whole hierarchy. Never duplicates or edits source meshes.</summary>
    public sealed class SilhouetteOutline : IDisposable
    {
        internal readonly struct Draw
        {
            public readonly Renderer Renderer;
            public readonly Material Material;
            public readonly int Submesh;
            public Draw(Renderer renderer, Material material, int submesh) { Renderer=renderer;Material=material;Submesh=submesh; }
        }
        static readonly List<SilhouetteOutline> active = new();
        readonly List<Renderer> sources = new();
        readonly List<Material> materials = new();
        readonly Material mask;
        float nextRefresh;
        bool disposed;
        public Transform Target { get; }
        public Camera Viewer { get; set; }
        public Color Color { get; set; }
        public float Pixels { get; set; }
        public int Priority { get; }
        public static int ActiveCount => active.Count;

        public SilhouetteOutline(Transform target, Color color, float pixels=3, int priority=0)
        {
            Target=target;Color=color;Pixels=pixels;Priority=priority;
            var shader=Resources.Load<Shader>("SilhouetteOutline");
            if(shader!=null)mask=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            Refresh();active.Add(this);
        }
        void Refresh()
        {
            sources.Clear();if(Target!=null)Target.GetComponentsInChildren(true,sources);
            nextRefresh=Time.unscaledTime+.5f;
        }
        internal static Draw[] Collect(Camera camera)
        {
            var draws=new List<Draw>();
            var planes=GeometryUtility.CalculateFrustumPlanes(camera);
            var chosen=new Dictionary<Transform,SilhouetteOutline>();
            foreach(var entry in active)
            {
                if(entry.disposed||entry.Target==null||!entry.Target.gameObject.activeInHierarchy||entry.mask==null||entry.Color.a<=0||entry.Pixels<=0)continue;
                if(camera!=(entry.Viewer!=null?entry.Viewer:Camera.main))continue;
                if(!chosen.TryGetValue(entry.Target,out var other)||entry.Priority>other.Priority)chosen[entry.Target]=entry;
            }
            foreach(var entry in chosen.Values)
            {
                if(Time.unscaledTime>=entry.nextRefresh)entry.Refresh();
                entry.mask.SetColor("_SilhouetteColor",entry.Color);
                entry.mask.SetFloat("_SilhouettePixels",Mathf.Clamp(entry.Pixels,0,8));
                foreach(var source in entry.sources)
                {
                    if(source==null||source.name.StartsWith("__")||!source.enabled||source.forceRenderingOff||!source.gameObject.activeInHierarchy
                        ||(camera.cullingMask & (1<<source.gameObject.layer))==0||!GeometryUtility.TestPlanesAABB(planes,source.bounds)||!SelectedLod(source,camera))continue;
                    Mesh mesh=source is SkinnedMeshRenderer skin?skin.sharedMesh:
                        source is MeshRenderer&&source.TryGetComponent<MeshFilter>(out var filter)?filter.sharedMesh:null;
                    if(mesh==null)continue;
                    source.GetSharedMaterials(entry.materials);
                    for(int i=0;i<mesh.subMeshCount;i++)
                    {
                        var material=entry.materials.Count>0?entry.materials[Mathf.Min(i,entry.materials.Count-1)]:null;
                        if(material==null||material.renderQueue>=(int)RenderQueue.Transparent
                            ||material.HasProperty("_Surface")&&material.GetFloat("_Surface")>.5f
                            ||material.HasProperty("_ZWrite")&&material.GetFloat("_ZWrite")<.5f)continue;
                        draws.Add(new Draw(source,entry.mask,i));
                    }
                }
            }
            return draws.ToArray();
        }
        static bool SelectedLod(Renderer source,Camera camera)
        {
            var group=source.GetComponentInParent<LODGroup>();
            if(group==null||!group.enabled)return true;
            var lods=group.GetLODs();int sourceLevel=-1;
            for(int i=0;i<lods.Length;i++)if(Array.IndexOf(lods[i].renderers,source)>=0){sourceLevel=i;break;}
            if(sourceLevel<0)return true;
            var scale=group.transform.lossyScale;
            float size=group.size*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            float distance=Vector3.Distance(camera.transform.position,group.transform.TransformPoint(group.localReferencePoint));
            float height=camera.orthographic?size/(2*camera.orthographicSize):size/(2*Mathf.Max(.001f,distance)*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f));
            height*=QualitySettings.lodBias;
            for(int i=0;i<lods.Length;i++)if(height>=lods[i].screenRelativeTransitionHeight)return i==sourceLevel;
            return false;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;active.Remove(this);sources.Clear();
            if(mask!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(mask);else UnityEngine.Object.DestroyImmediate(mask);}
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){foreach(var item in active.ToArray())item.Dispose();active.Clear();}
    }
}
