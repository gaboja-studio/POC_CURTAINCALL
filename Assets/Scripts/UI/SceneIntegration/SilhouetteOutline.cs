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
            public readonly bool OccludedOnly;
            public Draw(Renderer renderer, Material material, int submesh, bool occludedOnly)
            {
                Renderer = renderer;
                Material = material;
                Submesh = submesh;
                OccludedOnly = occludedOnly;
            }
        }
        static readonly List<SilhouetteOutline> active = new();
        readonly List<Renderer> sources = new();
        readonly List<Material> materials = new();
        readonly Material mask;
        readonly bool occludedOnly;
        readonly bool xray;
        float nextRefresh;
        bool disposed;
        public Transform Target { get; }
        public Camera Viewer { get; set; }
        public Color Color { get; set; }
        public float Pixels { get; set; }
        public int Priority { get; }
        public static int ActiveCount => active.Count;
        /// <summary>
        /// 남은 시간 비율(0~1). 0 이상이면 몸 발끝부터 이 비율 높이까지만 선명하고 그 위는 흐리게 그린다(시간이 줄면 머리부터 흐려짐).
        /// 음수면 시간 표시 없이 전체를 같은 색으로 그린다(2026-11-26 M3 위험한 동료 표시).
        /// </summary>
        public float Timer { get; set; } = -1f;
        /// <summary>시간 표시의 발끝 높이(월드 Y)와 몸 높이(m). 비우면 대상 위치부터 1.8m.</summary>
        public float? TimerBottom { get; set; }
        public float TimerHeight { get; set; } = 1.8f;

        /// <param name="xray">켜면 벽·구조물 뒤에 가려져도 바깥 테두리를 그린다(위험한 동료 표시). occludedOnly와 함께 쓰지 않는다.</param>
        public SilhouetteOutline(Transform target, Color color, float pixels=3, int priority=0, bool occludedOnly=false, bool xray=false)
        {
            Target=target;Color=color;Pixels=pixels;Priority=priority;
            this.occludedOnly = occludedOnly;
            this.xray = xray && !occludedOnly;
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
                // 0 = 보이는 곳만 테두리, 1 = 가려진 곳만 채움, 2 = 가려져도 테두리(벽 너머).
                entry.mask.SetFloat("_SilhouetteOccludedOnly", entry.occludedOnly ? 1f : entry.xray ? 2f : 0f);
                entry.mask.SetFloat("_SilhouetteTimer", entry.Timer);
                entry.mask.SetVector("_SilhouetteTimerRange", new Vector4(entry.TimerBottom ?? entry.Target.position.y, Mathf.Max(.01f, entry.TimerHeight), 0f, 0f));
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
                        draws.Add(new Draw(source,entry.mask,i,entry.occludedOnly));
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
