using UnityEngine;

namespace CurtainCall.WorldIndicators
{
    public sealed class PathIndicatorView : WorldIndicatorView
    {
        public void Show(Vector3 from,Vector3 to,float width,float readiness,bool bidirectional=false)
        {
            Vector3 delta=to-from;
            if(width<=0 || !float.IsFinite(delta.sqrMagnitude+width) || delta.sqrMagnitude<.001f) { Hide(); return; }
            if(!Begin((from+to)*.5f)) return;
            Vector3 axis=delta.normalized,side=Vector3.Cross(Vector3.up,axis).normalized*width*.5f;
            from+=Vector3.up*Settings.surfaceOffset; to+=Vector3.up*Settings.surfaceOffset;
            fill.Quad(from-side,from+side,to+side,to-side,Settings.danger.WithAlpha(Settings.fillOpacity));
            boundary.Line(from-side,to-side,Settings.lineWidth,Settings.danger,Vector3.up);
            boundary.Line(from+side,to+side,Settings.lineWidth,Settings.danger,Vector3.up);
            boundary.Line(to-side,to+side,Settings.lineWidth,Settings.danger,Vector3.up);
            Dashes(from,to,Vector3.up,Settings.danger);
            if(bidirectional) Arrow(from,-axis,Vector3.up,Settings.danger);
            Vector3 p=side+side.normalized*Settings.progressOffset;
            progress.Line(from+p,Vector3.Lerp(from,to,Mathf.Clamp01(readiness))+p,Settings.progressWidth,Settings.progressColor,Vector3.up);
            End();
        }
    }
}
