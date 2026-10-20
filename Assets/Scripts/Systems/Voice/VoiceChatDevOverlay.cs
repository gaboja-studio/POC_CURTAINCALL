using CurtainCall.Dev;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Voice
{
    /// <summary>
    /// 개발자 모드(<see cref="DevMode.Enabled"/>)일 때만 보이는 음성 확인 화면(IMGUI, 오른쪽 위).
    /// 무리·채널·마이크 상태와 참가자 말하기 표시, 생존 무리면 참가자와의 거리. <see cref="VoiceChat"/>가 같은 오브젝트에 붙인다.
    /// </summary>
    public sealed class VoiceChatDevOverlay : MonoBehaviour
    {
        const float Width = 300f;
        const float Height = 220f;
        public float PresentationTop { get; set; } = 80f;
        public bool PresentationSuppressed { get; set; }

        void OnGUI()
        {
            var voice = VoiceChat.Instance;
            if (!DevMode.Enabled || PresentationSuppressed || voice == null || voice.gameObject != gameObject
                || (UI.SceneIntegration.CommonMenuUI.Current != null && UI.SceneIntegration.CommonMenuUI.Current.IsOpen)) return;

            var settings = GameSettings.Base.VoiceChat;
            GUILayout.BeginArea(new Rect(Screen.width - Width - 10f, PresentationTop, Width, Height), GUI.skin.box);
            GUILayout.Label($"음성: {RoomText(voice.Room)}   로그인 {(voice.IsLoggedIn ? "O" : "X")}");
            GUILayout.Label($"마이크: {(voice.IsMicMuted ? "꺼짐" : "켜짐")} ({settings.MuteKey})   생존 {settings.ConversationalDistance}~{settings.AudibleDistance}m");
            if (!string.IsNullOrEmpty(voice.LastError))
                GUILayout.Label($"오류: {voice.LastError}");

            var local = NetworkPlayer.Local;
            foreach (var p in voice.Participants)
            {
                string name = p.IsSelf ? $"{p.DisplayName} (나)" : p.DisplayName;
                string distance = voice.Room == VoiceRoom.Alive && !p.IsSelf ? DistanceText(p.DisplayName, local) : "";
                GUILayout.Label($"{(p.SpeechDetected ? "●" : "○")} {name}{(p.IsMuted ? " [음소거]" : "")}{distance}");
            }
            GUILayout.EndArea();
        }

        static string RoomText(VoiceRoom room) => room switch
        {
            VoiceRoom.Alive => "생존자(거리 감쇠)",
            VoiceRoom.Dead => "사망자(거리 무관)",
            _ => "없음",
        };

        // 표시 이름 "P<자리+1>"로 같은 자리의 캐릭터를 찾아 내 캐릭터와의 거리를 보여 준다.
        static string DistanceText(string displayName, NetworkPlayer local)
        {
            if (local == null) return "";
            foreach (var other in NetworkPlayer.All)
            {
                if (other == null || other == local || $"P{other.Slot + 1}" != displayName) continue;
                return $"  {Vector3.Distance(local.transform.position, other.transform.position):0.0}m";
            }
            return "";
        }
    }
}
