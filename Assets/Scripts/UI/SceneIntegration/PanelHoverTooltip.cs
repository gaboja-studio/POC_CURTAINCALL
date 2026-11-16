using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CurtainCall.UI.ShopV1;
using CurtainCall.UI.TooltipsV1;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    public sealed class PanelHoverTooltip : MonoBehaviour
    {
        public TooltipView tooltip;
        public WaitingRoomShopUI shop;
        public WaitingRoomScheduleUI schedule;
        readonly List<RaycastResult> hits = new();
        string shown;
        GameObject target;
        float since;
        // 스케줄표 카드 설명 툴팁은 스케줄표를 보는 동안에도 보여야 한다.
        void OnEnable() { if (tooltip != null) CurtainCall.WaitingRoom.ScheduleScreenHold.Keep(tooltip.GetComponentInParent<Canvas>(true)); }
        void LateUpdate()
        {
            if(tooltip==null) return;
            if(EventSystem.current==null || Mouse.current==null || (CommonMenuUI.Current!=null && CommonMenuUI.Current.IsOpen)) { Hide();return; }
            var position=Mouse.current.position.ReadValue();
            hits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},hits);
            var hit=hits.Count>0?hits[0].gameObject:null;
            string title=null,description=null;
            if(hit!=null && shop!=null && shop.View.IsVisible)
            {
                var row=hit.GetComponentInParent<ShopRowVisual>();
                if(row!=null && row.transform.IsChildOf(shop.View.transform))
                {
                    var entry=shop.Build().Entries.FirstOrDefault(e=>e.Id==row.EntryId);
                    if(entry!=null){title=entry.Title;var format=shop.Texts!=null?shop.Texts.rowTooltip:null;description=string.IsNullOrEmpty(format)?null:CurtainCall.WaitingRoom.WaitingRoomText.Fill(format,new[]{("description",(object)TooltipDescription(entry)),("price",(object)entry.PriceText),("reason",(object)entry.UnavailableReason)});hit=row.gameObject;}
                }
            }
            if(hit!=null && title==null && schedule!=null && schedule.View.IsVisible)
            {
                var button=hit.GetComponentInParent<Button>();
                if(button!=null){description=schedule.HoverDescription(button);if(description!=null){title=schedule.HoverTitle(button);hit=button.gameObject;}}
            }
            if(string.IsNullOrWhiteSpace(title)||string.IsNullOrWhiteSpace(description)){Hide();return;}
            if(target!=hit){Hide();target=hit;since=Time.unscaledTime;}
            if(Time.unscaledTime-since<.35f)return;
            var parent=(RectTransform)tooltip.transform.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,position,null,out var local);
            float x=local.x-parent.rect.xMin+20,y=parent.rect.yMax-local.y+20;
            string key=title+description;
            if(shown!=key){if(tooltip.ShowPanel(title,description,new Vector2(x,y),Width))FitWholeDescription(tooltip);shown=key;}
            float height=((RectTransform)tooltip.transform).rect.height;
            x=Mathf.Clamp(x,12,Mathf.Max(12,parent.rect.width-Width-12));y=Mathf.Clamp(y,12,Mathf.Max(12,parent.rect.height-height-16));
            tooltip.SetPosition(new Vector2(x,y));
        }

        const float Width=480f;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly FieldInfo DescriptionField=typeof(TooltipView).GetField("descriptionLabel",Private);
        static readonly FieldInfo BottomEdgeField=typeof(TooltipView).GetField("bottomEdgeLeft",Private);
        static readonly FieldInfo HeightField=typeof(TooltipView).GetField("currentHeight",Private);

        /// <summary>
        /// 툴팁 키트(TooltipView)는 설명 높이를 최대 240으로 막고 넘치는 글을 "…"으로 자른다(2026-11-02 사용자: 내용 모두 표시).
        /// 키트는 외부 저장소라 고치지 않고, 표시한 직후 같은 배치 규칙(위 64·아래 20 여백, 4 단위)으로 글 전체가 들어가게 늘린다.
        /// </summary>
        internal static void FitWholeDescription(TooltipView view)
        {
            var label=DescriptionField?.GetValue(view) as TMP_Text;
            if(label==null) return;
            HudText.Configure(label);
            var root=(RectTransform)view.transform;
            label.overflowMode=TextOverflowModes.Overflow;
            float width=root.sizeDelta.x;
            float needed=Mathf.Ceil((HudText.Measure(label,width-48f).y+84f)/4f)*4f;
            if(needed<=root.sizeDelta.y) return;
            root.sizeDelta=new Vector2(width,needed);
            var text=label.rectTransform;text.sizeDelta=new Vector2(text.sizeDelta.x,needed-84f);
            if(BottomEdgeField?.GetValue(view) is Image edge) edge.rectTransform.anchoredPosition=new Vector2(edge.rectTransform.anchoredPosition.x,-(needed-4f));
            HeightField?.SetValue(view,needed);
        }
        /// <summary>기물 줄은 기물 목록의 툴팁 전용 설명(shopTooltip)을 쓰고, 비어 있거나 기물이 아니면 설명 칸과 같은 설명을 쓴다.</summary>
        internal static string TooltipDescription(ShopEntryPresentation entry)
        {
            const string itemPrefix="item:";
            if(entry.Id!=null && entry.Id.StartsWith(itemPrefix))
            {
                var tooltip=CurtainCall.WaitingRoom.ItemCatalog.Tooltip(entry.Id.Substring(itemPrefix.Length));
                if(!string.IsNullOrEmpty(tooltip)) return tooltip;
            }
            return entry.Description;
        }
        void Hide(){tooltip.HideImmediate();shown=null;target=null;}
        void OnDisable(){if(tooltip!=null)Hide();}
    }
}
