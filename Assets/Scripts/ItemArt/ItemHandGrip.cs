using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.ItemArt
{
    // Retargeting preserves motion, but different phalanx lengths need different
    // closure angles around a handle of fixed thickness. Fit once per avatar/item.
    public static class ItemHandGrip
    {
        static readonly Dictionary<(Avatar,string),Vector3[]> fitted = new();
        static readonly HumanBodyBones[] roots={HumanBodyBones.RightIndexProximal,HumanBodyBones.RightMiddleProximal,HumanBodyBones.RightRingProximal,HumanBodyBones.RightLittleProximal,HumanBodyBones.RightThumbProximal};
        public static void Apply(Animator animator,string item,Vector3 center,Vector3 fingers,Vector3 normal,float radius)
        {
            var key=(animator.avatar,item);
            bool fit=!fitted.TryGetValue(key,out var angles);
            if(fit)angles=new Vector3[5];
            for(int digit=0;digit<5;digit++)
            {
                var first=animator.GetBoneTransform(roots[digit]);
                var second=animator.GetBoneTransform(roots[digit]+1);
                var third=animator.GetBoneTransform(roots[digit]+2);
                if(first==null||second==null||third==null)continue;
                Vector3 forward=digit==4?-fingers:fingers;
                if(fit)
                {
                    Vector3 offset=first.position-center;
                    var origin=new Vector2(Vector3.Dot(offset,forward),Vector3.Dot(offset,-normal));
                    float l1=Vector3.Distance(first.position,second.position),l2=Vector3.Distance(second.position,third.position),l3=l2*.73f;
                    float width=Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.RightIndexProximal).position,animator.GetBoneTransform(HumanBodyBones.RightLittleProximal).position);
                    float padding=.004f+Mathf.InverseLerp(.080f,.088f,width)*.008f;
                    angles[digit]=Fit(origin,l1,l2,l3,radius+(digit==4?.011f:.008f)+padding);
                }
                Vector3 angle=angles[digit];
                Aim(first,forward,normal,angle.x);
                Aim(second,forward,normal,angle.x+angle.y);
                Aim(third,forward,normal,angle.x+angle.y+angle.z);
            }
            if(fit)fitted[key]=angles;
        }
        static void Aim(Transform bone,Vector3 forward,Vector3 normal,float degrees)
        {
            float radians=degrees*Mathf.Deg2Rad;
            Vector3 direction=forward*Mathf.Cos(radians)-normal*Mathf.Sin(radians);
            bone.rotation=Quaternion.FromToRotation(bone.up,direction)*bone.rotation;
        }
        static Vector2 Direction(float degrees){float a=degrees*Mathf.Deg2Rad;return new Vector2(Mathf.Cos(a),Mathf.Sin(a));}
        static float Distance(Vector2 a,Vector2 b)
        {Vector2 d=b-a;return (a+d*Mathf.Clamp01(-Vector2.Dot(a,d)/Mathf.Max(d.sqrMagnitude,1e-8f))).magnitude;}
        static Vector3 Fit(Vector2 start,float l1,float l2,float l3,float radius)
        {
            float best=float.PositiveInfinity;Vector3 result=new(50,60,50);
            for(int a=15;a<=95;a+=5)for(int b=15;b<=100;b+=5)for(int c=10;c<=90;c+=10)
            {
                if(a+b+c<135||a+b+c>240)continue;
                Vector2 p=start+Direction(a)*l1,q=p+Direction(a+b)*l2,end=q+Direction(a+b+c)*l3;
                float d1=Distance(start,p)-radius,d2=Distance(p,q)-radius,d3=Distance(q,end)-radius;
                float penetration=Mathf.Min(0,d1)*Mathf.Min(0,d1)+Mathf.Min(0,d2)*Mathf.Min(0,d2)+Mathf.Min(0,d3)*Mathf.Min(0,d3);
                float score=penetration*10000+d2*d2+d3*d3+(end-new Vector2(-radius*.3f,radius)).sqrMagnitude*.3f;
                if(score<best){best=score;result=new Vector3(a,b,c);}
            }
            return result;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset()=>fitted.Clear();
    }
}
