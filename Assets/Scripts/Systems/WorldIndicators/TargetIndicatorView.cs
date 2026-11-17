using UnityEngine;

namespace CurtainCall.WorldIndicators
{
    /// <summary>A point cue, not an attack area. Preparation follows the body aim; locked input stays in world space.</summary>
    public sealed class TargetIndicatorView : WorldIndicatorView
    {
        public void Show(Vector3 point,float readiness,bool locked,float bodyClearance=0)
        {
            if(!Begin(point)) return;
            var camera=Camera.main;
            float scale=1;
            // Pull the billboard in front of the body along the viewing ray, preserving
            // its screen position and size. Combat still uses the original world aim.
            if(camera && bodyClearance>0)
            {
                Vector3 delta=point-camera.transform.position;
                float depth=Vector3.Dot(delta,camera.transform.forward);
                if(depth<=camera.nearClipPlane+.01f) { Hide(); return; }
                if(camera.orthographic)
                    point-=camera.transform.forward*Mathf.Min(bodyClearance,depth-camera.nearClipPlane-.01f);
                else
                {
                    float distance=delta.magnitude;
                    float pull=Mathf.Min(bodyClearance,distance*(1-(camera.nearClipPlane+.01f)/depth));
                    scale=Mathf.Clamp01(1-pull/Mathf.Max(.001f,distance));
                    point=camera.transform.position+delta*scale;
                }
            }
            Vector3 normal=camera?-camera.transform.forward:Vector3.back;
            Vector3 right=camera?camera.transform.right:Vector3.right;
            Vector3 up=camera?camera.transform.up:Vector3.up;
            float half=Mathf.Max(.05f,Settings.grabTargetSize)*.5f*scale;
            float segment=half*.48f,width=Mathf.Max(.008f,Settings.lineWidth*.8f)*scale;
            Color color=locked?Settings.danger:Settings.progressColor;
            // Four open corners leave the actor/locked point visible through the centre.
            for(int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2)
            {
                Vector3 corner=point+right*(x*half)+up*(y*half);
                boundary.Line(corner,corner-right*(x*segment),width,color,normal);
                boundary.Line(corner,corner-up*(y*segment),width,color,normal);
            }
            // Dedicated exported reticle mask is the small locked-point cue.
            if(locked && Style.target)
            {
                fill.Clear(Style.target);
                Vector3 x=right*half*.6f,y=up*half*.6f;
                fill.Quad(point-x-y,point+x-y,point+x+y,point-x+y,color);
            }
            Vector3 begin=point-right*half-up*(half+Settings.progressOffset*scale);
            Vector3 end=point+right*half-up*(half+Settings.progressOffset*scale);
            boundary.Line(begin,end,Settings.progressWidth*scale,Settings.ivory.WithAlpha(.28f),normal);
            progress.Line(begin,Vector3.Lerp(begin,end,Mathf.Clamp01(readiness)),Settings.progressWidth*scale,Settings.progressColor,normal);
            End();
        }
    }
}
