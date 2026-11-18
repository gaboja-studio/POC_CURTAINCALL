using UnityEngine;
namespace CurtainCall.Presentation
{
    public sealed class PerformanceSpotlightFixture : MonoBehaviour
    {
        [Tooltip("공연장 최상단 좌우 외곽 등기구의 회전 헤드. 위치는 움직이지 않습니다.")] public Transform head;
        [Tooltip("렌즈 위치의 실제 Spot Light.")] public Light spot;
        [Tooltip("광원에서 시작하는 좁은 빛기둥.")] public MeshRenderer beam;
        [Tooltip("등기구 렌즈 발광 표면.")] public Renderer lens;
        [System.NonSerialized] public float fade;
        Texture2D aperture;
        Texture originalCookie;
        Color32[] pixels;
        float lastRadius=-1,lastInner=-1;

        public void UpdateAperture(float distance,float radius,float innerRatio)
        {
            // Unity clamps Spot Light angles to at least one degree. A cookie keeps
            // the requested small footprint even when the fixture is far overhead.
            float ratio=Mathf.Clamp01(radius/(Mathf.Tan(spot.spotAngle*.5f*Mathf.Deg2Rad)*distance));
            if(ratio>=.999f){ReleaseAperture();return;}
            const int size=128;
            if(aperture==null)
            {
                originalCookie=spot.cookie;
                aperture=new Texture2D(size,size,TextureFormat.RGBA32,false,true)
                {name="Performer spot aperture",hideFlags=HideFlags.DontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                pixels=new Color32[size*size];spot.cookie=aperture;
            }
            if(Mathf.Abs(lastRadius-ratio)<.002f && Mathf.Abs(lastInner-innerRatio)<.002f)return;
            lastRadius=ratio;lastInner=innerRatio;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float r=new Vector2((x+.5f)/size*2-1,(y+.5f)/size*2-1).magnitude;
                byte v=(byte)Mathf.RoundToInt(255*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(ratio*innerRatio,ratio,r))));
                pixels[y*size+x]=new Color32(v,v,v,255);
            }
            aperture.SetPixels32(pixels);aperture.Apply(false,false);
        }
        public void ReleaseAperture()
        {
            if(aperture==null)return;
            if(spot!=null && spot.cookie==aperture)spot.cookie=originalCookie;
            Destroy(aperture);aperture=null;pixels=null;lastRadius=lastInner=-1;
        }
        void OnDestroy()=>ReleaseAperture();
    }
}
