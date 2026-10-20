using System;
using System.Collections.Generic;
using System.Text;
using CurtainCall.Network.PlayerSync;

namespace CurtainCall.Voice
{
    /// <summary>
    /// 음성 무리 규칙(순수 함수). 사용 씬 = Start 흐름 + 세팅 목록. 사망 =<see cref="PlayerState.Fallen"/>·<see cref="PlayerState.Retired"/>(관전 규칙과 같음).
    /// 결정: Harness/Project/Decisions/voice-chat.md
    /// </summary>
    public static class VoiceRoomRules
    {
        /// <summary>채널 이름에 넣는 방 키의 최대 글자 수.</summary>
        public const int MaxKeyLength = 64;

        /// <summary>이 상태면 사망자 무리다.</summary>
        public static bool IsDead(PlayerState state) => state == PlayerState.Fallen || state == PlayerState.Retired;

        /// <summary>
        /// 음성을 쓰는 상황인지: Start 씬부터 이어진 흐름(<paramref name="startFlow"/>)이고 지금 씬이 사용 씬 목록에 있다.
        /// 테스트 씬을 직접 연 경우·목록 밖 씬(아직 확정 안 된 공간)은 쓰지 않는다. 결정: voice-chat.md #2
        /// </summary>
        public static bool IsVoiceScene(bool startFlow, string sceneName, IReadOnlyList<string> scenes)
        {
            if (!startFlow || string.IsNullOrEmpty(sceneName) || scenes == null) return false;
            foreach (var name in scenes)
                if (string.Equals(name?.Trim(), sceneName, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>지금 들어가 있어야 할 무리. 꺼짐·음성 씬 아님·방 키 없음·내 캐릭터 없음이면 <see cref="VoiceRoom.None"/>.</summary>
        public static VoiceRoom Desired(bool enabled, bool voiceScene, string roomKey, bool hasLocalPlayer, PlayerState localState)
        {
            if (!enabled || !voiceScene || string.IsNullOrEmpty(Sanitize(roomKey)) || !hasLocalPlayer)
                return VoiceRoom.None;
            return IsDead(localState) ? VoiceRoom.Dead : VoiceRoom.Alive;
        }

        /// <summary>
        /// 방 키와 무리로 만든 Vivox 채널 이름. 같은 방·같은 무리면 모두 같은 이름이 된다.
        /// <see cref="VoiceRoom.None"/>이거나 쓸 수 있는 키가 없으면 null.
        /// </summary>
        public static string ChannelName(string roomKey, VoiceRoom room)
        {
            if (room == VoiceRoom.None) return null;
            string key = Sanitize(roomKey);
            if (string.IsNullOrEmpty(key)) return null;
            return $"cc-{key}-{(room == VoiceRoom.Alive ? "alive" : "dead")}";
        }

        /// <summary>방 키에서 영문·숫자만 남기고 소문자로, 최대 <see cref="MaxKeyLength"/>자.</summary>
        public static string Sanitize(string roomKey)
        {
            if (string.IsNullOrEmpty(roomKey)) return null;
            var sb = new StringBuilder(roomKey.Length);
            foreach (char c in roomKey)
            {
                if (sb.Length >= MaxKeyLength) break;
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
                else if (c >= 'A' && c <= 'Z') sb.Append(char.ToLowerInvariant(c));
            }
            return sb.Length == 0 ? null : sb.ToString();
        }
    }
}
