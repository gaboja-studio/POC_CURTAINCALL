using System;
using CurtainCall.UI.SceneIntegration;
using CurtainCall.Cameras;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 화면 오른쪽 아래 투표 패널(리허설 넘기기, 사용자 2026-11-04). 기존 BasicPanels 몸체와 CommonButtons 버튼을 쓴다.
    /// 내용이 바뀔 때만 다시 그린다. 패널 위에서는 커서를 다시 잠그지 않는다(Esc로 커서를 풀고 누른다).
    /// </summary>
    public sealed class RehearsalVotePanel : MonoBehaviour
    {
        HudRoleCard card;
        CameraCursorExclusion cursorRegion;
        RectTransform surface => card ? card.Rect : null;
        public bool IsVisible => card && !card.Closing;
        public string Title => IsVisible ? card.Title : "";
        public string Body => IsVisible ? card.Content : "";
        public RectTransform VisibleSurface => IsVisible ? surface : null;

        public void Show(string heading,string text,params HudVoteChoice[] choices)
        {
            heading=WaitingRoomHudProfile.FormatLocalKeys(heading);
            text=WaitingRoomHudProfile.FormatLocalKeys(text);
            string hint=WaitingRoomHudProfile.FormatLocalKeys(WaitingRoomText.Get("rehearsal.vote-panel.hint","버튼에 표시된 키로 선택"));
            var shownChoices=choices;
            for (int i=0;i<choices.Length;i++)
            {
                var choice=choices[i];
                var caption=WaitingRoomHudProfile.FormatLocalKeys(choice.Label);
                if(caption==choice.Label)continue;
                if(shownChoices==choices)shownChoices=(HudVoteChoice[])choices.Clone();
                shownChoices[i]=new HudVoteChoice(caption,choice.Interactable,choice.OnClick);
            }
            if (!IsVisible)
            {
                card=HudUIManager.Current.ShowVote(text,transform,heading,hint,shownChoices);
                if(!cursorRegion) cursorRegion=gameObject.AddComponent<CameraCursorExclusion>();
            }
            card.SetText(text,heading,hint);
            card.SetChoices(shownChoices);
            UpdateCursorRegion();
        }
        /// <summary>카드 맨 아래 남은 선택 시간 게이지(0~1). null이면 숨긴다.</summary>
        public void SetTimerGauge(float? remaining)
        {
            if (IsVisible) card.SetTimerGauge(remaining);
        }
        public void Hide()
        {
            if(card) card.Close();
            card=null;
            if(cursorRegion) cursorRegion.enabled=false;
        }

        void UpdateCursorRegion()
        {
            if (cursorRegion == null || surface == null) return;
            var corners = new Vector3[4];
            surface.GetWorldCorners(corners);
            // 화면 좌표(왼쪽 아래 원점)를 IMGUI 기준(왼쪽 위 원점)으로 바꾼다.
            float x = corners[0].x, y = Screen.height - corners[1].y;
            cursorRegion.SetScreenRect(new Rect(x, y, corners[2].x - corners[0].x, corners[1].y - corners[0].y));
            cursorRegion.enabled = true;
        }

        void LateUpdate() { if(IsVisible) UpdateCursorRegion(); }
        void OnDisable() => Hide();
    }
}
