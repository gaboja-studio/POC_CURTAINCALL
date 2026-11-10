using TMPro;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    public sealed class HudKeycapArt : MonoBehaviour
    {
        public TMP_Text key;
        public RectTransform surface,fill,frame;
        public Vector2 inset=new Vector2(8,1);
        void LateUpdate()
        {
            if(!key || !fill || !frame)return;
            bool show=key.isActiveAndEnabled;
            fill.gameObject.SetActive(show);frame.gameObject.SetActive(show);
            if(!show)return;
            var r=key.rectTransform;
            var topLeft=surface.InverseTransformPoint(r.TransformPoint(new Vector2(r.rect.xMin,r.rect.yMax)));
            Vector2 offset=(Vector2)topLeft-new Vector2(surface.rect.xMin,surface.rect.yMax);
            Vector2 size=new Vector2(Mathf.Min(r.rect.width,key.GetPreferredValues(key.text).x+inset.x*2),r.rect.height+inset.y*2);
            void Place(RectTransform t){t.anchorMin=t.anchorMax=t.pivot=new Vector2(0,1);t.anchoredPosition=offset+Vector2.up*inset.y;t.sizeDelta=size;}
            Place(fill);Place(frame);
        }
    }
}
