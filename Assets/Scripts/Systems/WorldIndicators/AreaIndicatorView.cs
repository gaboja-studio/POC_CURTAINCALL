using UnityEngine;

namespace CurtainCall.WorldIndicators
{
    public sealed class AreaIndicatorView : WorldIndicatorView
    {
        public bool circular = true;
        readonly Vector3[] corners=new Vector3[4];
        // Size is the complete physical diameter / width and depth, never a decorative scale.
        public void Show(Vector3 center,Quaternion rotation,Vector2 size,float readiness)
        {
            if(!float.IsFinite(size.sqrMagnitude) || size.x<=0 || size.y<=0) { Hide(); return; }
            if(!Begin(center)) return;
            Vector3 normal=rotation*Vector3.up, right=rotation*Vector3.right, forward=rotation*Vector3.forward;
            center+=normal*Settings.surfaceOffset;
            Vector3 x=right*size.x*.5f,z=forward*size.y*.5f;
            fill.Clear(circular?Style.circleFill:Style.rectangleFill);
            Color shade=Settings.danger; shade.a=Settings.fillOpacity;
            fill.Quad(center-x-z,center+x-z,center+x+z,center-x+z,shade);
            float width=Mathf.Max(.003f,Settings.lineWidth);
            readiness=Mathf.Clamp01(readiness);
            if(circular)
            {
                int count=Mathf.Clamp(Settings.circleSegments,24,128);
                Vector3 Point(float t,float inset=0) => center+right*Mathf.Sin(t)*(size.x*.5f-inset)+forward*Mathf.Cos(t)*(size.y*.5f-inset);
                for(int i=0;i<count;i++)
                {
                    float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;
                    boundary.Line(Point(a,width*.5f),Point(b,width*.5f),width,Settings.danger,normal);
                    if(i%4!=0) boundary.Line(Point(a,width*2),Point(b,width*2),width*.3f,Settings.ivory,normal);
                    if(i%3==0) hatch.Line(Point(a,width*3),Point(b,Mathf.Min(Settings.hatchDepth,Mathf.Min(size.x,size.y)*.2f)),width*.65f,shade.WithAlpha(.45f),normal);
                    float p0=i/(float)count,p1=(i+1)/(float)count;
                    if(readiness>p0) progress.Line(Point(a,-Settings.progressOffset),Point(Mathf.Lerp(a,b,Mathf.InverseLerp(p0,p1,readiness)),-Settings.progressOffset),Settings.progressWidth,Settings.progressColor,normal);
                }
            }
            else
            {
                corners[0]=center-x-z; corners[1]=center+x-z; corners[2]=center+x+z; corners[3]=center-x+z;
                for(int i=0;i<4;i++)
                {
                    Vector3 a=corners[i],b=corners[(i+1)%4],inward=(center-(a+b)*.5f).normalized;
                    boundary.Line(a+inward*width*.5f,b+inward*width*.5f,width,Settings.danger,normal);
                    float length=Vector3.Distance(a,b); Vector3 tangent=(b-a).normalized;
                    for(float d=width;d<length-Settings.hatchDepth;d+=Mathf.Max(.05f,Settings.hatchSpacing))
                        hatch.Line(a+tangent*d+inward*width,a+tangent*(d+Settings.hatchDepth)+inward*Mathf.Min(Settings.hatchDepth,Mathf.Min(size.x,size.y)*.2f),width*.65f,shade.WithAlpha(.45f),normal);
                }
                Vector3 start=center-x+z+forward*Settings.progressOffset,finish=center+x+z+forward*Settings.progressOffset;
                boundary.Line(start,finish,Settings.progressWidth,Settings.ivory.WithAlpha(.28f),normal);
                progress.Line(start,Vector3.Lerp(start,finish,readiness),Settings.progressWidth,Settings.progressColor,normal);
            }
            End();
        }
    }
    static class IndicatorColor { public static Color WithAlpha(this Color color,float alpha) { color.a=alpha; return color; } }
}
