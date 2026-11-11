using CurtainCall.UI.SceneIntegration;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    sealed class WaitingRoomRightPanel
    {
        readonly Transform owner;
        readonly string name;
        readonly HudRole role;
        HudRoleCard card;
        string lastTitle,lastBody;
        readonly System.Collections.Generic.List<HudRoleCard> shown=new();
        public bool Visible => card != null && !card.Closing;
        public string Title => Visible ? card.Title : "";
        public string Body => Visible ? card.Content : "";
        public WaitingRoomRightPanel(Transform owner,string name,HudRole role = HudRole.Objective)
        { this.owner=owner; this.name=name; this.role=role; }

        public bool Show(string title,string body)
        {
            title=WaitingRoomHudProfile.FormatLocalKeys(title);
            body=WaitingRoomHudProfile.FormatLocalKeys(body);
            if(role!=HudRole.Feedback && title==lastTitle && body==lastBody)return true; // A superseded, unchanged mission must not reclaim the HUD every frame.
            bool newRequest=!Visible || title!=lastTitle;
            lastTitle=title;lastBody=body;
            if (newRequest)
            {
                card=role==HudRole.Feedback ? HudUIManager.Current.ShowFeedback(body,owner,title,0)
                    : role==HudRole.Status ? HudUIManager.Current.ShowStatus(body,owner,title) : HudUIManager.Current.ShowObjective(body,owner,title);
                card.name=name;shown.RemoveAll(c=>!c || c.Closing);shown.Add(card);
            }
            else card.SetText(body,title,"");
            return true;
        }
        public void Hide() { foreach(var old in shown)if(old)old.Close();shown.Clear();card=null;lastTitle=lastBody=null; }
    }
}
