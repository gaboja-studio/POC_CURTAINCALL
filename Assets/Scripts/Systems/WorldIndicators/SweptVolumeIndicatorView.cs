using UnityEngine;

namespace CurtainCall.WorldIndicators
{
    public sealed class SweptVolumeIndicatorView : WorldIndicatorView
    {
        public void Show(Vector3 from,Vector3 to,float radius,float readiness,bool warning=true)
        {
            Vector3 delta=to-from;
            if(!float.IsFinite(delta.sqrMagnitude+radius) || radius<=0 || delta.sqrMagnitude<.0001f) { Hide(); return; }
            if(!Begin((from+to)*.5f)) return;
            Vector3 axis=delta.normalized;
            Vector3 side=Vector3.Cross(axis,Mathf.Abs(axis.y)>.95f?Vector3.right:Vector3.up).normalized;
            Vector3 up=Vector3.Cross(side,axis).normalized,normal=ViewNormal((from+to)*.5f);
            Vector3 Offset(float t) => (side*Mathf.Cos(t)+up*Mathf.Sin(t))*radius;
            int count=32;
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;
                boundary.Line(from+Offset(a),from+Offset(b),Settings.lineWidth*.7f,Settings.danger,normal);
                boundary.Line(to+Offset(a),to+Offset(b),Settings.lineWidth*.7f,Settings.danger,normal);
            }
            for(int rail=0;rail<4;rail++)
            {
                Vector3 offset=Offset(rail*Mathf.PI*.5f);
                boundary.Line(from+offset,to+offset,Settings.lineWidth*.7f,Settings.danger,normal);
                // Hemispherical ends represent the sphere sweep, including endpoint radius.
                for(int k=0;k<12;k++)
                {
                    float a=k*Mathf.PI*.5f/12,b=(k+1)*Mathf.PI*.5f/12;
                    boundary.Line(from+offset*Mathf.Cos(a)-axis*radius*Mathf.Sin(a),from+offset*Mathf.Cos(b)-axis*radius*Mathf.Sin(b),Settings.lineWidth*.5f,Settings.danger,normal);
                    boundary.Line(to+offset*Mathf.Cos(a)+axis*radius*Mathf.Sin(a),to+offset*Mathf.Cos(b)+axis*radius*Mathf.Sin(b),Settings.lineWidth*.5f,Settings.danger,normal);
                }
            }
            Dashes(from,to,normal,Settings.ivory);
            if(warning)
            {
                Vector3 offset=up*(radius+Settings.progressOffset);
                boundary.Line(from+offset,to+offset,Settings.progressWidth,Settings.ivory.WithAlpha(.25f),normal);
                progress.Line(from+offset,Vector3.Lerp(from,to,Mathf.Clamp01(readiness))+offset,Settings.progressWidth,Settings.progressColor,normal);
            }
            End();
        }
    }
}
