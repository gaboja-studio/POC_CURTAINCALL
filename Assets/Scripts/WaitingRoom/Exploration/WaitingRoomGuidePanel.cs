using System.Collections.Generic;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    // Queued narrative events keep their order while the schedule covers the HUD.
    public sealed class WaitingRoomGuidePanel : MonoBehaviour
    {
        static WaitingRoomGuidePanel instance;
        static readonly Queue<(string title,string text,float seconds,RoomNotice role)> pending=new();
        HudRoleCard card;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance=null; pending.Clear(); }
        public static bool IsVisible => instance && instance.card && !instance.card.Closing;
        public static string CurrentTitle => IsVisible ? instance.card.Title : "";
        public static string CurrentBody => IsVisible ? instance.card.Content : "";
        public static int PendingCount => pending.Count;
        public static void Show(string heading,string text,float seconds=7,bool feedback=false,bool reward=false)
            =>ShowAs(heading,text,seconds,feedback ? RoomNotice.Feedback : reward ? RoomNotice.Reward : RoomNotice.Notice);
        public static void ShowAs(string heading,string text,float seconds,RoomNotice role)
        {
            Ensure();
            if (ScheduleScreenHold.IsHeld || pending.Count>0) pending.Enqueue((heading,text,seconds,role));
            else instance.Open(heading,text,seconds,role);
        }
        static void Ensure()
        {
            if(!instance)
            {
                var go=new GameObject(nameof(WaitingRoomGuidePanel)); DontDestroyOnLoad(go);
                instance=go.AddComponent<WaitingRoomGuidePanel>();
            }

        }
        public static HudRoleCard ShowContext(Transform source,string heading,string text,float seconds,RoomNotice role)
        {
            Ensure();
            return instance.card=WaitingRoomHudProfile.Emit(role,source,heading,text,seconds);
        }
        void Open(string heading,string text,float seconds,RoomNotice role)
        {
            card=WaitingRoomHudProfile.Emit(role,transform,heading,text,seconds);
        }
        public static void Hide() { if(instance){if(instance.card)instance.card.Close(true);HudUIManager.DismissOwned(instance.transform,HudRole.SystemEvent);HudUIManager.DismissOwned(instance.transform,HudRole.Feedback);HudUIManager.DismissOwned(instance.transform,HudRole.Guide);} }
        void Update()
        {
            if(ScheduleScreenHold.IsHeld || IsVisible || pending.Count==0) return;
            var n=pending.Dequeue(); Open(n.title,n.text,n.seconds,n.role);
        }
    }
}
