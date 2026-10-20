using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>Small vector microphone; a slash distinguishes mute without relying on colour.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MicrophoneStatusIcon : MaskableGraphic
    {
        [SerializeField] bool muted;
        public void SetMuted(bool value) { if (muted == value) return; muted = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            float size = Mathf.Min(rect.width, rect.height);
            Vector2 centre = rect.center;
            Color32 tint = muted ? new Color32(224, 108, 120, 255) : new Color32(240, 238, 220, 255);
            void Line(Vector2 a, Vector2 b)
            {
                a = centre + a * size; b = centre + b * size;
                Vector2 normal = new Vector2(-(b-a).y, (b-a).x).normalized * size * .035f;
                int start = mesh.currentVertCount;
                mesh.AddVert(a-normal,tint,Vector2.zero); mesh.AddVert(a+normal,tint,Vector2.zero);
                mesh.AddVert(b+normal,tint,Vector2.zero); mesh.AddVert(b-normal,tint,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start,start+2,start+3);
            }
            // Capsule from y=-.08 to .34, then stand and outer pickup cradle.
            Vector2 previous = default;
            for (int i=0;i<=32;i++)
            {
                float angle=i*Mathf.PI*2/32;
                var p=new Vector2(Mathf.Cos(angle)*.13f, Mathf.Sin(angle)*.13f + (Mathf.Sin(angle)>=0 ? .21f : -.08f));
                if(i>0) Line(previous,p); previous=p;
            }
            Line(new Vector2(-.24f,.02f),new Vector2(-.24f,-.08f));
            for(int i=0;i<16;i++)
            {
                float a=Mathf.PI+i*Mathf.PI/16, b=Mathf.PI+(i+1)*Mathf.PI/16;
                Line(new Vector2(Mathf.Cos(a)*.24f,-.08f+Mathf.Sin(a)*.24f),new Vector2(Mathf.Cos(b)*.24f,-.08f+Mathf.Sin(b)*.24f));
            }
            Line(new Vector2(.24f,-.08f),new Vector2(.24f,.02f));
            Line(new Vector2(0,-.32f),new Vector2(0,-.43f));
            Line(new Vector2(-.15f,-.43f),new Vector2(.15f,-.43f));
            if(muted) Line(new Vector2(-.38f,.4f),new Vector2(.38f,-.4f));
        }
    }
}
