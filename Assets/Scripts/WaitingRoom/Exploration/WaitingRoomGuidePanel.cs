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
        static void ResetStatics() { instance=null; pending.Clear(); suspended=hasShown=hasHeld=false; }
        public static bool IsVisible => instance && instance.card && !instance.card.Closing;
        public static string CurrentTitle => IsVisible ? instance.card.Title : "";
        public static string CurrentBody => IsVisible ? instance.card.Content : "";
        public static int PendingCount => pending.Count;
        public static void Show(string heading,string text,float seconds=7,bool feedback=false,bool reward=false)
            =>ShowAs(heading,text,seconds,feedback ? RoomNotice.Feedback : reward ? RoomNotice.Reward : RoomNotice.Notice);
        public static void ShowAs(string heading,string text,float seconds,RoomNotice role)
        {
            Ensure();
            if (ScheduleScreenHold.IsHeld || suspended || pending.Count>0) pending.Enqueue((heading,text,seconds,role));
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
            shown=(heading,text,seconds,role); shownAt=Time.unscaledTime; hasShown=true;
        }
        public static void Hide() { if(instance){if(instance.card)instance.card.Close(true);HudUIManager.DismissOwned(instance.transform,HudRole.SystemEvent);HudUIManager.DismissOwned(instance.transform,HudRole.Feedback);HudUIManager.DismissOwned(instance.transform,HudRole.Guide);} }
        // ---- 잠시 내려 두기(2026-11-17 사용자) ----
        // 카메라 튜토리얼이 시작될 때 떠 있던 알림을 화면에서 내렸다가, 튜토리얼이 끝나면 남은 시간만큼 이어서 다시 띄운다. 그 사이 들어온 알림은 줄에서 기다린다.
        static bool suspended, hasShown, hasHeld; static float shownAt;
        static (string title,string text,float seconds,RoomNotice role) shown, held;
        const float MinResumeSeconds=4f;
        public static bool IsSuspended => suspended;
        public static void Suspend()
        {
            if(suspended) return;
            suspended=true; hasHeld=false;
            if(!IsVisible || !hasShown || instance.card.Title!=shown.title) return; // 다른 경로(ShowContext)로 띄운 알림은 다시 띄울 수 없어 그대로 둔다
            held=shown; held.seconds=Mathf.Max(MinResumeSeconds,shown.seconds-(Time.unscaledTime-shownAt)); hasHeld=true;
            instance.card.Close(true);
        }
        public static void Resume()
        {
            if(!suspended) return;
            suspended=false;
            if(hasHeld && instance) { hasHeld=false; instance.Open(held.title,held.text,held.seconds,held.role); }
        }
        void Update()
        {
            if(ScheduleScreenHold.IsHeld || suspended || IsVisible || pending.Count==0) return;
            var n=pending.Dequeue(); Open(n.title,n.text,n.seconds,n.role);
        }
    }
}
