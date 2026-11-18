using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.MainStage.CheerPoints
{
    /// <summary>Shared runtime/editor presentation. The scoring plane never moves.</summary>
    public sealed class CheerPointPresentation : MonoBehaviour
    {
        CheerHangingRope rope;
        Renderer[] ringRenderers;
        MeshRenderer[] meshes;
        CheerPointVisualPreview motion;
        Vector3 restLocalPosition;
        bool hidden;
        public bool Hidden => hidden;

        void Initialize()
        {
            if(rope) return;
            restLocalPosition=transform.localPosition;
            ringRenderers=GetComponentsInChildren<Renderer>(true);
            meshes=GetComponentsInChildren<MeshRenderer>(true);
            motion=GetComponent<CheerPointVisualPreview>();
            var cable=new GameObject("OverheadRope");
            cable.hideFlags=gameObject.hideFlags;
            cable.transform.SetParent(transform,false);
            rope=cable.AddComponent<CheerHangingRope>();
        }

        public void Sample(float elapsed,bool collected,Camera view)
        {
            Initialize();
            if(!collected) hidden=false;
            if(hidden) { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);
            var settings=GameSettings.PerformanceEquipment;
            float rise=collected ? RiseOffset(elapsed,settings) : 0;
            var origin=transform.parent ? transform.parent.TransformPoint(restLocalPosition) : restLocalPosition;
            transform.position=origin+Vector3.up*rise;
            if(motion) motion.Sample(Application.isPlaying ? Time.time : elapsed);
            Bounds body=new Bounds(transform.position,Vector3.zero);
            bool hasBody=false;
            foreach(var mesh in meshes) if(mesh) { if(!hasBody) body=mesh.bounds; else body.Encapsulate(mesh.bounds); hasBody=true; }
            Vector3 attachment=new Vector3(body.center.x,body.max.y,body.center.z);
            // The end always lies beyond this camera's clipping distance, even looking up.
            float length=Mathf.Max(10,settings.cheerRopeLength);
            if(view) length=Mathf.Max(length,view.farClipPlane*1.2f+Vector3.Distance(view.transform.position,attachment));
            rope.Sample(attachment,length,settings);
            if(!collected || rise<settings.cheerHideMinHeight || !view) return;
            Bounds visible=body;
            foreach(var renderer in ringRenderers)
            {
                if(!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                // Stopped particles have an empty world-origin bound; that is not visible content.
                if(renderer is ParticleSystemRenderer && (!renderer.TryGetComponent<ParticleSystem>(out var particles) || !particles.IsAlive())) continue;
                visible.Encapsulate(renderer.bounds);
            }
            visible.Encapsulate(rope.Bounds);
            visible.Expand(1f); // Keep flames and the cable safely outside before removal.
            if(CanHide(view,visible,rise,settings.cheerHideMinHeight))
            {
                hidden=true;
                gameObject.SetActive(false);
            }
        }

        public static float RiseOffset(float elapsed,PerformanceEquipmentSettings settings)
        {
            float dip=Mathf.Max(.05f,settings.cheerDipSeconds);
            if(elapsed<=dip) return -settings.cheerDipDistance*Mathf.SmoothStep(0,1,Mathf.Max(0,elapsed)/dip);
            float t=elapsed-dip,acceleration=Mathf.Max(1,settings.cheerRetractAcceleration),speed=Mathf.Max(1,settings.cheerRetractSpeed);
            float accelerating=Mathf.Min(t,speed/acceleration);
            return -settings.cheerDipDistance+.5f*acceleration*accelerating*accelerating+speed*(t-accelerating);
        }

        public static bool CanHide(Camera view,Bounds visible,float rise,float minimumHeight)
            => view && rise>=minimumHeight && !GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(view),visible);
    }
}
