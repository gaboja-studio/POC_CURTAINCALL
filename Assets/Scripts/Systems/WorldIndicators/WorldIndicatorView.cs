using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.WorldIndicators
{
    public interface IWorldIndicator { void Hide(); bool Visible { get; } }

    public abstract class WorldIndicatorView : MonoBehaviour, IWorldIndicator
    {
        protected IndicatorMesh fill, boundary, hatch, progress, direction, arrow;
        protected WorldUiSettings Settings => GameSettings.WorldUi;
        protected WorldIndicatorStyle Style => Settings.style;
        public bool Visible { get; private set; }
        protected bool Begin(Vector3 center)
        {
            var camera=Camera.main;
            if(!Settings.enabled || !Style || !float.IsFinite(center.sqrMagnitude)
                || camera && (camera.transform.position-center).sqrMagnitude>Settings.maxDistance*Settings.maxDistance)
            { Hide(); return false; }
            if(fill==null)
            {
                fill=new IndicatorMesh(transform,"Fill"); boundary=new IndicatorMesh(transform,"Boundary");
                hatch=new IndicatorMesh(transform,"Hatch"); progress=new IndicatorMesh(transform,"Progress");
                direction=new IndicatorMesh(transform,"Direction"); arrow=new IndicatorMesh(transform,"Arrow");
            }
            gameObject.SetActive(true); Visible=true;
            fill.Clear(Style.rectangleFill); boundary.Clear(Style.stroke); hatch.Clear(Style.stroke);
            progress.Clear(Style.stroke); direction.Clear(Style.stroke); arrow.Clear(Style.arrow);
            return true;
        }
        protected void End() { fill.Upload(); boundary.Upload(); hatch.Upload(); progress.Upload(); direction.Upload(); arrow.Upload(); }
        protected Vector3 ViewNormal(Vector3 at) => Camera.main ? (Camera.main.transform.position-at).normalized : Vector3.up;
        public void Hide() { Visible=false; if(gameObject.activeSelf) gameObject.SetActive(false); }
        protected virtual void OnDisable() { Visible=false; }
        protected virtual void OnDestroy() { fill?.Dispose(); boundary?.Dispose(); hatch?.Dispose(); progress?.Dispose(); direction?.Dispose(); arrow?.Dispose(); }
        protected void Arrow(Vector3 tip,Vector3 tangent,Vector3 normal,Color color)
        {
            tangent.Normalize(); Vector3 side=Vector3.Cross(normal,tangent).normalized;
            if(side.sqrMagnitude<.01f) side=Vector3.right;
            float size=Settings.arrowSize;
            // A separate alpha sprite keeps exactly one solid arrowhead at the end.
            arrow.Quad(tip-tangent*size-side*size*.55f,tip-side*size*.55f,
                tip+side*size*.55f,tip-tangent*size+side*size*.55f,color);
        }
        protected void Dashes(Vector3 a,Vector3 b,Vector3 normal,Color color)
        {
            float length=Vector3.Distance(a,b); if(length<.001f) return;
            Vector3 tangent=(b-a)/length;
            float dash=Mathf.Max(.02f,Settings.dashLength), stride=dash+Mathf.Max(.02f,Settings.dashGap);
            for(float d=0;d<length-Settings.arrowSize;d+=stride)
                direction.Line(a+tangent*d,a+tangent*Mathf.Min(d+dash,length-Settings.arrowSize),Settings.lineWidth,color,normal);
            Arrow(b,tangent,normal,color);
        }
    }
}
