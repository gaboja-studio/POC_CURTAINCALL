using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WorldIndicators
{
    public sealed class HazardWorldIndicator : MonoBehaviour
    {
        AreaIndicatorView area;
        PathIndicatorView path;
        readonly RaycastHit[] hits=new RaycastHit[32];
        Vector3 cachedStart,cachedFinish,cachedA,cachedB;
        float nextSupport;
        bool supported;
        public bool Present(HumanCannonHazard hazard,HumanCannonHazardCue cue,bool running,double elapsed)
        {
            var settings=GameSettings.WorldUi;
            if(!settings.style || !settings.rectanglePrefab || !settings.pathPrefab) { Hide(); return false; }
            double cycle=cue?cue.Cycle(elapsed):0;
            bool warning=cue && cycle>=0 && cycle<cue.warningSeconds;
            if(!running || !hazard.isActiveAndEnabled || !settings.enabled || cue && !warning) { Hide(); return true; }
            Vector3 start=cue?cue.origin:hazard.transform.position-hazard.Travel;
            Vector3 finish=cue?cue.destination:hazard.transform.position+hazard.Travel;
            Vector3 size=hazard.WorldUiSize;
            float ready=cue?Mathf.Clamp01((float)cycle/Mathf.Max(.1f,cue.warningSeconds)):0;
            if(Time.unscaledTime>=nextSupport || start!=cachedStart || finish!=cachedFinish)
            {
                cachedStart=start; cachedFinish=finish;
                nextSupport=Time.unscaledTime+Mathf.Max(.02f,settings.surfaceRefreshSeconds);
                supported=Ground(start,hazard,out cachedA) && Ground(finish,hazard,out cachedB);
            }
            if(!supported) { Hide(); return true; }
            var a=cachedA; var b=cachedB;
            bool moving=Vector3.ProjectOnPlane(finish-start,Vector3.up).sqrMagnitude>.1f;
            if(moving)
            {
                if(!path) path=WorldIndicatorFactory.Path(transform);
                area?.Hide();
                Vector3 side=Vector3.Cross(Vector3.up,(b-a).normalized);
                float width=Mathf.Abs(side.x)*size.x+Mathf.Abs(side.z)*size.z;
                Vector3 axis=Vector3.ProjectOnPlane(b-a,Vector3.up).normalized;
                float halfLength=(Mathf.Abs(axis.x)*size.x+Mathf.Abs(axis.z)*size.z)*.5f;
                path?.Show(a-axis*halfLength,b+axis*halfLength,Mathf.Max(.05f,width),ready,!cue);
            }
            else
            {
                if(!area) area=WorldIndicatorFactory.Area(transform,false);
                path?.Hide(); area?.Show(b,Quaternion.identity,new Vector2(size.x,size.z),ready);
            }
            return true;
        }
        bool Ground(Vector3 point,HumanCannonHazard hazard,out Vector3 ground)
        {
            ground=default; int n=Physics.RaycastNonAlloc(point+Vector3.up,Vector3.down,hits,60,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            float best=float.PositiveInfinity;
            if(n==hits.Length) return false;
            for(int i=0;i<n;i++)
            {
                var h=hits[i];
                if(h.normal.y<.7f || h.distance>=best || h.collider.GetComponentInParent<NetworkPlayer>()
                    || h.collider.transform.IsChildOf(hazard.transform) || hazard.WorldUiVisual && h.collider.transform.IsChildOf(hazard.WorldUiVisual)) continue;
                ground=h.point; best=h.distance;
            }
            return float.IsFinite(best);
        }
        public void Hide() { area?.Hide(); path?.Hide(); }
        void OnDisable() => Hide();
    }
}
