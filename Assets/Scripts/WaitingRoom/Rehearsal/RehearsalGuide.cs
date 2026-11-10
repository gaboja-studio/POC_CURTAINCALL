using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.UI.SceneIntegration;

namespace CurtainCall.WaitingRoom
{
    // The lesson owns its existing sequence and completion tracking; instruction copy always uses the left guide.
    public static class RehearsalGuide
    {
        public static string Format(string text,string interact,string action)=>CommonInteractionUI.FormatControlLabels(text,interact,action).Replace("{interact}",interact).Replace("{action}",action);
        public static bool Show(string title,string body,float seconds,string badge=null)
        {
            var reader=NetworkPlayer.Local ? NetworkPlayer.Local.GetComponent<PlayerInputReader>():null;
            var f=reader?reader.BindingLabel("Interact","F"):"F";
            var jump=reader?reader.BindingLabel("Action","Space"):"Space";
            return WaitingRoomTutorialPanel.ShowAs(RoomNotice.Guide,Format(title,f,jump),Format(body,f,jump),seconds,badge==null?null:Format(badge,f,jump));
        }
    }
}
