using System.Collections.Generic;
using System.Linq;
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
                    if(entry!=null){title=entry.Title;description=entry.Description+"\n가격: "+entry.PriceText+"\n"+entry.UnavailableReason;hit=row.gameObject;}
                }
            }
            if(hit!=null && title==null && schedule!=null && schedule.View.IsVisible)
            {
                var button=hit.GetComponentInParent<Button>();
                if(button!=null){description=schedule.HoverDescription(button);if(description!=null){title=string.Join(" · ",button.GetComponentsInChildren<TMP_Text>().Select(t=>t.text));hit=button.gameObject;}}
            }
            if(string.IsNullOrWhiteSpace(title)||string.IsNullOrWhiteSpace(description)){Hide();return;}
            if(target!=hit){Hide();target=hit;since=Time.unscaledTime;}
            if(Time.unscaledTime-since<.35f)return;
            var parent=(RectTransform)tooltip.transform.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,position,null,out var local);
            float x=local.x-parent.rect.xMin+20,y=parent.rect.yMax-local.y+20;
            x=Mathf.Clamp(x,12,Mathf.Max(12,parent.rect.width-492));y=Mathf.Clamp(y,12,Mathf.Max(12,parent.rect.height-256));
            string key=title+description;
            if(shown!=key){tooltip.ShowPanel(title,description,new Vector2(x,y),480);shown=key;}
            else tooltip.SetPosition(new Vector2(x,y));
        }
        void Hide(){tooltip.HideImmediate();shown=null;target=null;}
        void OnDisable(){if(tooltip!=null)Hide();}
    }
}
