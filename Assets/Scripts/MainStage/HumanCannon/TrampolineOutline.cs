using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>Continuous contact against the authored round body and three grip bars, in device space.</summary>
    public static class TrampolineOutline
    {
        public static bool TryTouch(Vector3 from, Vector3 to, Vector3 previousSurface, Vector3 surface,
            Quaternion previousRotation, Quaternion rotation, Bounds bounds, float radius, float height, out Vector3 contact)
        {
            contact=default;
            if(!Finite(from)||!Finite(to)||!Finite(surface)||!Finite(previousSurface)||!float.IsFinite(radius)||!float.IsFinite(height)
                ||radius<0||height<0||bounds.size.x<=0||bounds.size.z<=0)return false;
            int steps=Mathf.Clamp(Mathf.CeilToInt(Quaternion.Angle(previousRotation,rotation)/8f),1,32);
            float half=Mathf.Max(radius,height*.5f);
            for(int i=0;i<steps;i++)
            {
                float a=(float)i/steps,b=(float)(i+1)/steps;
                Vector3 start=Quaternion.Inverse(Quaternion.Slerp(previousRotation,rotation,a))*(Vector3.Lerp(from,to,a)+Vector3.up*half-Vector3.Lerp(previousSurface,surface,a));
                Vector3 end=Quaternion.Inverse(Quaternion.Slerp(previousRotation,rotation,b))*(Vector3.Lerp(from,to,b)+Vector3.up*half-Vector3.Lerp(previousSurface,surface,b));
                if(!Segment(start,end,bounds,radius,half,out var local))continue;
                contact=surface+rotation*local;return true;
            }
            return false;
        }

        public static bool Contains(Vector3 point,Bounds bounds,float margin)
        {
            if(point.y<bounds.min.y-margin||point.y>bounds.max.y+margin)return false;
            float r=Mathf.Min(bounds.max.x,bounds.extents.z);
            if(new Vector2(point.x,point.z).sqrMagnitude<=(r+margin)*(r+margin))return true;
            for(int i=0;i<3;i++){var bar=Bar(bounds,i);bar.Expand(margin*2);if(bar.Contains(point))return true;}
            return false;
        }

        static bool Segment(Vector3 start,Vector3 end,Bounds bounds,float radius,float half,out Vector3 contact)
        {
            float best=float.PositiveInfinity;Vector3 hit=default;var delta=end-start;
            float r=Mathf.Min(bounds.max.x,bounds.extents.z), expanded=r+radius;
            float lo=0,hi=1;
            if(Slab(start.y,delta.y,bounds.min.y-half,bounds.max.y+half,ref lo,ref hi))
            {
                float qa=delta.x*delta.x+delta.z*delta.z;
                float qb=2*(start.x*delta.x+start.z*delta.z);
                float qc=start.x*start.x+start.z*start.z-expanded*expanded;
                bool radial=true;
                if(qa<.0000001f)radial=qc<=0;
                else
                {
                    float discriminant=qb*qb-4*qa*qc;
                    if(discriminant<0)radial=false;
                    else{float root=Mathf.Sqrt(discriminant);lo=Mathf.Max(lo,(-qb-root)/(2*qa));hi=Mathf.Min(hi,(-qb+root)/(2*qa));}
                }
                if(radial&&lo<=hi&&lo<=1&&hi>=0)
                {
                    best=Mathf.Max(0,lo);var p=start+delta*best;
                    var horizontal=Vector2.ClampMagnitude(new Vector2(p.x,p.z),r);
                    hit=new Vector3(horizontal.x,bounds.max.y,horizontal.y);
                }
            }
            for(int i=0;i<3;i++)
            {
                var bar=Bar(bounds,i);var expandedBar=bar;expandedBar.Expand(new Vector3(radius,half,radius)*2);
                lo=0;hi=1;
                if(Slab(start.x,delta.x,expandedBar.min.x,expandedBar.max.x,ref lo,ref hi)
                    &&Slab(start.y,delta.y,expandedBar.min.y,expandedBar.max.y,ref lo,ref hi)
                    &&Slab(start.z,delta.z,expandedBar.min.z,expandedBar.max.z,ref lo,ref hi)&&lo<best)
                {best=lo;hit=bar.ClosestPoint(start+delta*lo);hit.y=bounds.max.y;}
            }
            contact=hit;return float.IsFinite(best);
        }

        static Bounds Bar(Bounds all,int index)
        {
            float scale=Mathf.Min(all.max.x,all.extents.z)/2.4753408f;
            // Surface is 0.50m above prop origin. Bar and paired struts come from build-props.py.
            if(index==0)return new Bounds(new Vector3(-2.8f,.10f,0)*scale,new Vector3(.15f,.10f,1.36f)*scale);
            return new Bounds(new Vector3(-2.51f,.035f,index==1?-.55f:.55f)*scale,new Vector3(.65f,.20f,.07f)*scale);
        }
        static bool Slab(float start,float delta,float min,float max,ref float lo,ref float hi)
        {
            if(Mathf.Abs(delta)<.0000001f)return start>=min&&start<=max;
            float a=(min-start)/delta,b=(max-start)/delta;
            lo=Mathf.Max(lo,Mathf.Min(a,b));hi=Mathf.Min(hi,Mathf.Max(a,b));return lo<=hi;
        }
        static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
    }
}
