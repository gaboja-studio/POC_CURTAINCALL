using CurtainCall.Player;
using CurtainCall.UI.SceneIntegration;

namespace CurtainCall.WaitingRoom
{
    // The lesson owns its existing sequence and completion tracking; instruction copy always uses the left guide.
    public static class RehearsalGuide
    {
        public static string Format(string text,string interact,string action) => Format(text,interact,action,null);

        static string Format(string text,string interact,string action,PlayerInputReader reader)
        {
            string Part(string name,string part,string fallback) => reader ? reader.BindingPartLabel(name,part,fallback) : fallback;
            string move=Part("Move","positive","W")+"/"+Part("Move","negative","S");
            string balance=Part("Posture","negative","A")+"/"+Part("Posture","positive","D");
            string legacy=CommonInteractionUI.FormatControlLabels(text,interact,action,move,balance);
            return PerformanceHudProfile.FormatKeys(legacy,reader,interact,action);
        }
        public static bool Show(string title,string body,float seconds,string badge=null)
        {
            var reader=WaitingRoomHudProfile.LocalInput;
            var f=reader?reader.BindingLabel("Interact","F"):"F";
            var jump=reader?reader.BindingLabel("Action","Space"):"Space";
            return WaitingRoomTutorialPanel.ShowAs(RoomNotice.Guide,Format(title,f,jump,reader),Format(body,f,jump,reader),seconds,
                badge==null?null:Format(badge,f,jump,reader));
        }
    }
}
