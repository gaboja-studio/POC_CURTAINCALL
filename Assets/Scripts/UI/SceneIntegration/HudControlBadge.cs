using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    // The visible badge width comes from the prefab; only longer bindings expand its lane.
    public sealed class HudControlBadge : MonoBehaviour
    {
        public TMP_Text label;
        public RectTransform plate, frame;
        public VerticalLayoutGroup rows;
        [Min(0)] public float horizontalInset=7;
        [Min(1)] public float maxWidth=180;
        float baseWidth;
        int basePadding;
        string defaultLabel;
        public void Bind(string key)
        {
            if(defaultLabel==null)
            {
                defaultLabel=label.text; baseWidth=label.rectTransform.rect.width; basePadding=rows.padding.left;
            }
            label.text=string.IsNullOrWhiteSpace(key)?defaultLabel:key.Trim();
            if(!rows.enabled)return;
            float natural=label.GetPreferredValues(label.text,Mathf.Infinity,Mathf.Infinity).x;
            float width=Mathf.Clamp(Mathf.Ceil(natural+2*horizontalInset),baseWidth,Mathf.Max(baseWidth,maxWidth));
            foreach(var rect in new[]{label.rectTransform,plate,frame})rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width);
            rows.padding.left=basePadding+Mathf.CeilToInt(width-baseWidth);
        }
    }
}
