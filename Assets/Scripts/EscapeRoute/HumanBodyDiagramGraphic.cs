using System.Collections.Generic;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.EscapeRoute
{
    /// <summary>A human silhouette: head, tapered torso, hands and feet; four independently marked limbs.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HumanBodyDiagramGraphic : MaskableGraphic
    {
        [SerializeField] BodyPart lostParts;
        static readonly Color32 IntactFill = new(125, 143, 149, 125);
        static readonly Color32 IntactLine = new(214, 226, 230, 255);
        static readonly Color32 LostFill = new(117, 31, 41, 170);
        static readonly Color32 LostLine = new(255, 83, 99, 255);
        readonly List<int> indices = new();
        readonly List<float> crossings = new();
        public BodyPart LostParts => lostParts;
        public void Present(BodyPart parts)
        {
            parts &= PlayerCondition.AllLimbs;
            if (parts == lostParts) return;
            lostParts = parts; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var torso = Smooth(new[] { V(35,144), V(43,148), V(44,151), V(56,151), V(57,148),
                V(65,144), V(64,132), V(60,114), V(61,101), V(57,91), V(50,87),
                V(43,91), V(39,101), V(40,114), V(36,132) });
            Draw(vh, torso, IntactFill, IntactLine);
            Draw(vh, Ellipse(V(50,164), V(9,12)), IntactFill, IntactLine);
            // Match the player's screen-facing sides, as viewed from behind.
            Limb(vh, Arm(false), BodyPart.LeftArm);
            Limb(vh, Arm(true), BodyPart.RightArm);
            Limb(vh, Leg(false), BodyPart.LeftLeg);
            Limb(vh, Leg(true), BodyPart.RightLeg);
        }
        void Limb(VertexHelper vh, Vector2[] polygon, BodyPart part)
        {
            bool lost = (lostParts & part) != 0;
            Draw(vh, polygon, lost ? LostFill : IntactFill, lost ? LostLine : IntactLine);
            if (lost) Hatch(vh, polygon);
        }
        static Vector2[] Arm(bool right)
        {
            var p = new[] { V(35,143),V(29,141),V(25,132),V(21,118),V(18,106),V(15,96),
                V(14,88),V(16,82),V(20,81),V(23,85),V(24,94),V(27,105),V(30,117),V(34,129),V(38,138) };
            return Smooth(Mirror(p, right));
        }
        static Vector2[] Leg(bool right)
        {
            var p = new[] { V(40,97),V(48,92),V(47,78),V(46,60),V(44,45),V(43,28),
                V(44,17),V(43,9),V(40,6),V(29,6),V(28,10),V(33,14),V(35,20),V(35,30),
                V(34,45),V(34,60),V(33,78),V(35,91) };
            return Smooth(Mirror(p, right));
        }
        static Vector2[] Mirror(Vector2[] p, bool mirror)
        { if (mirror) for (int i = 0; i < p.Length; i++) p[i].x = 100 - p[i].x; return p; }
        static Vector2 V(float x, float y) => new(x, y);
        static Vector2[] Ellipse(Vector2 center, Vector2 radii)
        {
            var points = new Vector2[32];
            for (int i = 0; i < points.Length; i++)
            { float a = i * Mathf.PI * 2 / points.Length; points[i] = center + V(Mathf.Cos(a)*radii.x, Mathf.Sin(a)*radii.y); }
            return points;
        }
        static Vector2[] Smooth(Vector2[] p)
        {
            var result = new Vector2[p.Length * 3];
            for (int i = 0; i < p.Length; i++) for (int j = 0; j < 3; j++)
            {
                float t = j / 3f, t2 = t*t, t3 = t2*t;
                var a=p[(i+p.Length-1)%p.Length];var b=p[i];var c=p[(i+1)%p.Length];var d=p[(i+2)%p.Length];
                result[i*3+j]=.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3);
            }
            return result;
        }
        Vector2 Point(Vector2 p)
        { var r=rectTransform.rect; return new Vector2(r.xMin+p.x*r.width/100, r.yMin+p.y*r.height/182); }
        void Draw(VertexHelper vh, Vector2[] p, Color fill, Color outline)
        {
            int start=vh.currentVertCount;
            foreach(var point in p) vh.AddVert(Point(point),fill,Vector2.zero);
            indices.Clear(); float area=0;
            for(int i=0;i<p.Length;i++)area+=Cross(p[i],p[(i+1)%p.Length]);
            for(int i=0;i<p.Length;i++)indices.Add(area>0?i:p.Length-1-i);
            int guard=p.Length*p.Length;
            while(indices.Count>2&&guard-->0)
            {
                bool clipped=false;
                for(int i=0;i<indices.Count;i++)
                {
                    int a=indices[(i+indices.Count-1)%indices.Count],b=indices[i],c=indices[(i+1)%indices.Count];
                    if(Cross(p[b]-p[a],p[c]-p[b])<=.00001f)continue;
                    bool inside=false;
                    for(int j=0;j<indices.Count;j++) {int k=indices[j];if(k==a||k==b||k==c)continue;
                        if(Cross(p[b]-p[a],p[k]-p[a])>=0&&Cross(p[c]-p[b],p[k]-p[b])>=0&&Cross(p[a]-p[c],p[k]-p[c])>=0){inside=true;break;}}
                    if(inside)continue;
                    vh.AddTriangle(start+a,start+b,start+c);indices.RemoveAt(i);clipped=true;break;
                }
                if(!clipped)break;
            }
            for(int i=0;i<p.Length;i++) Line(vh,p[i],p[(i+1)%p.Length],1.05f,outline);
        }
        void Hatch(VertexHelper vh, Vector2[] p)
        {
            // Clip diagonal stripes to the missing limb's contour; colour alone is not the signal.
            for(float offset=-180;offset<110;offset+=7)
            {
                crossings.Clear();
                for(int i=0;i<p.Length;i++)
                {
                    var a=p[i];var b=p[(i+1)%p.Length];float da=a.x-a.y-offset,db=b.x-b.y-offset;
                    if((da<0)==(db<0))continue;
                    crossings.Add(Vector2.Lerp(a,b,da/(da-db)).y);
                }
                crossings.Sort();
                for(int i=0;i+1<crossings.Count;i+=2)
                    Line(vh,V(crossings[i]+offset,crossings[i]),V(crossings[i+1]+offset,crossings[i+1]),.65f,new Color32(255,100,111,190));
            }
        }
        void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color tint)
        {
            a=Point(a);b=Point(b);var delta=b-a;if(delta.sqrMagnitude<.000001f)return;
            var n=V(-delta.y,delta.x).normalized*width*.5f;int i=vh.currentVertCount;
            vh.AddVert(a-n,tint,Vector2.zero);vh.AddVert(a+n,tint,Vector2.zero);
            vh.AddVert(b+n,tint,Vector2.zero);vh.AddVert(b-n,tint,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    }
}
