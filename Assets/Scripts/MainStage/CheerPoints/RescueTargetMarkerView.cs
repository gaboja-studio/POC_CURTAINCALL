using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.MainStage.CheerPoints
{
    /// <summary>Prefab-authored, local-only presentation. Does not select or move targets.</summary>
    public sealed class RescueTargetMarkerView : MonoBehaviour
    {
        [SerializeField] Canvas canvas;
        [SerializeField] RectTransform anchor;
        [SerializeField] RectTransform motion;
        [SerializeField] CanvasGroup opacity;
        [SerializeField] Image halo;
        [SerializeField] RectTransform keycap;
        [SerializeField] TMP_Text keyLabel;
        [SerializeField, Min(.01f)] float appearSeconds = .16f;
        [SerializeField, Min(0)] float floatPixels = 1.5f;
        TMP_FontAsset runtimeFont;
        Object target;
        float appeared;
        string lastKey;

        public void Configure(Canvas cv, RectTransform at, RectTransform animated, CanvasGroup group,
            Image glow, RectTransform key, TMP_Text text)
        {
            canvas=cv; anchor=at; motion=animated; opacity=group; halo=glow; keycap=key; keyLabel=text;
        }

        void Awake()
        {
            // Arbitrary rebound key names, without mutating a shared font atlas.
            var source=Resources.Load<Font>("Fonts/Pretendard-Medium/Pretendard-Medium 1");
            if(source) {runtimeFont=TMP_FontAsset.CreateFontAsset(source);keyLabel.font=runtimeFont;}
            Hide();
        }

        public void Show(Object selected, Vector3 screenPoint, string key, float alpha)
        {
            if(target!=selected) {target=selected; appeared=Time.unscaledTime;}
            canvas.enabled=true;
            if(lastKey!=key)
            {
                lastKey=key; keyLabel.text=key;
                keycap.sizeDelta=new Vector2(Mathf.Clamp(keyLabel.GetPreferredValues(key).x+16,30,96),30);
            }
            float scale=canvas.scaleFactor;
            float right=(40+keycap.sizeDelta.x)*scale;
            anchor.position=new Vector3(Mathf.Clamp(screenPoint.x,48*scale,Mathf.Max(48*scale,Screen.width-right)),
                Mathf.Clamp(screenPoint.y,12*scale,Mathf.Max(12*scale,Screen.height-88*scale)),0);
            float age=Time.unscaledTime-appeared;
            float t=Mathf.Clamp01(age/appearSeconds);
            float eased=1-Mathf.Pow(1-t,3);
            opacity.alpha=eased*Mathf.Clamp01(alpha);
            motion.localScale=Vector3.one*Mathf.Lerp(.86f,1,eased);
            motion.anchoredPosition=Vector2.up*(Mathf.Sin(age*3.2f)*floatPixels);
            var tint=halo.color; tint.a=.28f+.1f*Mathf.Sin(age*3.2f);halo.color=tint;
        }

        public void Hide() {target=null;if(canvas)canvas.enabled=false;}
        void OnDestroy() {if(runtimeFont)Destroy(runtimeFont);}
    }
}
