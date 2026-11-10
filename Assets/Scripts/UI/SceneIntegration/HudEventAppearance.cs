using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    public enum HudEventKind { Notice, Warning, Reward }

    public sealed class HudEventAppearance : MonoBehaviour
    {
        [Serializable] public struct Theme { public Sprite icon; public Color color; }
        public Theme notice, warning, reward;
        public Image symbol;
        public TMP_Text line;
        public HorizontalLayoutGroup strip;
        public HudEventKind Kind {get;private set;}
        public void Select(HudEventKind kind) => Kind=kind;
        public void PrioritizeBroadcast()
        {
            if(!TryGetComponent<Canvas>(out var canvas))canvas=gameObject.AddComponent<Canvas>();
            canvas.overrideSorting=true;canvas.sortingOrder=short.MaxValue;
            foreach(var layer in SortingLayer.layers)
                if(SortingLayer.GetLayerValueFromID(layer.id)>SortingLayer.GetLayerValueFromID(canvas.sortingLayerID))
                    canvas.sortingLayerID=layer.id;
        }
        static string Literal(string value)=>"<noparse>"+value.Replace("</noparse>","<\u200b/noparse>",StringComparison.OrdinalIgnoreCase)+"</noparse>";
        HudRoleCard fitted;
        float parentWidth;
        void LateUpdate()
        {
            if (fitted && fitted.Rect.parent is RectTransform parent && !Mathf.Approximately(parentWidth,parent.rect.width))
                Fit(fitted);
        }
        public void Fit(HudRoleCard c)
        {
            var theme=Kind==HudEventKind.Warning?warning:Kind==HudEventKind.Reward?reward:notice;
            symbol.sprite=theme.icon;symbol.color=theme.color;c.title.color=theme.color;
            string head=c.title.gameObject.activeSelf?c.title.text:"", body=c.body.gameObject.activeSelf?c.body.text:"";
            line.font=c.body.font;line.color=c.body.color;line.fontSize=c.body.fontSize;
            string prefix=head.Length==0?"":"<color=#"+ColorUtility.ToHtmlStringRGBA(theme.color)+"><size="+c.title.fontSize.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"><b>"+Literal(head)+"</b></size></color>";
            line.text=prefix+(head.Length>0 && body.Length>0?"\n":"")+(body.Length>0?Literal(body):"");
            if(!c.rows || !c.rows.enabled)return;
            float chrome=c.rows.padding.horizontal+strip.padding.horizontal+LayoutUtility.GetPreferredWidth(symbol.rectTransform)+strip.spacing;
            float natural=line.GetPreferredValues(line.text,Mathf.Infinity,Mathf.Infinity).x;
            float limit=Mathf.Max(c.minWidth,c.maxWidth);
            if(c.Rect.parent is RectTransform parent && parent.rect.width>0)limit=Mathf.Min(limit,Mathf.Max(1,parent.rect.width-32));
            if(c.fitWidth)c.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Clamp(Mathf.Ceil(natural)+chrome,Mathf.Min(c.minWidth,limit),limit));
            float width=Mathf.Max(1,c.Rect.rect.width-chrome);
            fitted=c;
            parentWidth=c.Rect.parent is RectTransform viewport ? viewport.rect.width : 0;
            var le=line.GetComponent<LayoutElement>();
            le.preferredHeight=Mathf.Ceil(line.GetPreferredValues(line.text,width,Mathf.Infinity).y)+2;
            if(c.fitHeight)c.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(c.minHeight,c.rows.padding.vertical+strip.padding.vertical+Mathf.Max(le.preferredHeight,LayoutUtility.GetPreferredHeight(symbol.rectTransform))));
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)c.rows.transform);
        }
    }
}
