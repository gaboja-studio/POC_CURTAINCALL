using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WorldIndicators
{
    public sealed class TrajectoryIndicatorView : WorldIndicatorView
    {
        public void Show(IReadOnlyList<Vector3> points,bool obstructed=false)
        {
            if(points==null || points.Count<2) { Hide(); return; }
            for(int i=0;i<points.Count;i++) if(!float.IsFinite(points[i].sqrMagnitude)) { Hide(); return; }
            if(!Begin(points[0])) return;
            float along=0,dash=Mathf.Max(.02f,Settings.dashLength),stride=dash+Mathf.Max(.02f,Settings.dashGap);
            Color color=obstructed?Settings.danger:Settings.ivory;
            for(int i=1;i<points.Count;i++)
            {
                Vector3 a=points[i-1],b=points[i]; float length=Vector3.Distance(a,b);
                if(length<.00001f) continue;
                for(float d=0;d<length;)
                {
                    float phase=Mathf.Repeat(along+d,stride);
                    float step=Mathf.Min(length-d,(phase<dash?dash:stride)-phase);
                    if(step<.00001f) step=Mathf.Min(.0001f,length-d);
                    if(phase<dash) direction.Line(Vector3.Lerp(a,b,d/length),Vector3.Lerp(a,b,(d+step)/length),Settings.lineWidth,color,ViewNormal(a));
                    d+=step;
                }
                along+=length;
            }
            Vector3 tip=points[points.Count-1];
            int previous=points.Count-2;
            while(previous>=0 && (tip-points[previous]).sqrMagnitude<.000001f) previous--;
            if(previous<0) { Hide(); return; }
            Arrow(tip,tip-points[previous],ViewNormal(tip),color);
            End();
        }
    }
}
