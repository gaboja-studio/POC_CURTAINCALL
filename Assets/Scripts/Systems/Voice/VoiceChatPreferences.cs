using System.Collections.Generic;
using Unity.Services.Vivox;
using UnityEngine;

namespace CurtainCall.Voice
{
    public sealed partial class VoiceChat
    {
        const string MicMuteKey = "CurtainCall.Voice.MicMuted";
        const string MicVolumeKey = "CurtainCall.Voice.MicVolume";
        const string TeamVolumeKey = "CurtainCall.Voice.TeamVolume";
        const string TeamMuteKey = "CurtainCall.Voice.TeamMuted";
        public float MicVolume { get; private set; } = 1f;
        public float TeamVolume { get; private set; } = 1f;
        public bool IsTeamMuted { get; private set; }
        // PlayerId, never a reusable seat number. Kept across alive/dead channels in this run.
        readonly Dictionary<string, (float volume, bool muted)> participantPreferences = new();

        void LoadAudioPreferences()
        {
            IsMicMuted = PlayerPrefs.GetInt(MicMuteKey, 0) != 0;
            MicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MicVolumeKey, 1));
            TeamVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(TeamVolumeKey, 1));
            IsTeamMuted = PlayerPrefs.GetInt(TeamMuteKey, 0) != 0;
        }

        /// <summary>Local device settings; never changes another player's microphone.</summary>
        public void ApplyAudioPreferences(float mic, bool micMuted, float team, bool teamMuted)
        {
            MicVolume = Mathf.Clamp01(mic); TeamVolume = Mathf.Clamp01(team); IsTeamMuted = teamMuted;
            SetMicMuted(micMuted);
            PlayerPrefs.SetFloat(MicVolumeKey, MicVolume);
            PlayerPrefs.SetFloat(TeamVolumeKey, TeamVolume);
            PlayerPrefs.SetInt(TeamMuteKey, IsTeamMuted ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMicMute(); ApplyDeviceLevels();
        }

        // 100% is Vivox's unchanged default (0), not its +50 boost. Zero explicitly mutes.
        public static int ToVivoxVolume(float normalized) => Mathf.RoundToInt(Mathf.Lerp(-50, 0, Mathf.Clamp01(normalized)));
        void ApplyDeviceLevels()
        {
            if (!_vivoxReady) return;
            var service = VivoxService.Instance;
            service.SetInputDeviceVolume(ToVivoxVolume(MicVolume));
            service.SetOutputDeviceVolume(ToVivoxVolume(TeamVolume));
            if (IsTeamMuted || TeamVolume <= 0) service.MuteOutputDevice(); else service.UnmuteOutputDevice();
        }

        public float ParticipantVolume(string id) => participantPreferences.TryGetValue(id, out var value) ? value.volume : 1;
        public bool ParticipantMuted(string id) => participantPreferences.TryGetValue(id, out var value) && value.muted;
        public void SetParticipantPreference(string id, float volume, bool muted)
        {
            if (string.IsNullOrEmpty(id)) return;
            participantPreferences[id] = (Mathf.Clamp01(volume), muted);
            foreach (var participant in Participants)
                if (!participant.IsSelf && participant.PlayerId == id) ApplyParticipantPreference(participant);
        }
        void ApplyParticipantPreference(VivoxParticipant participant)
        {
            if (participant == null || participant.IsSelf) return;
            float volume = ParticipantVolume(participant.PlayerId);
            participant.SetLocalVolume(ToVivoxVolume(volume));
            if (ParticipantMuted(participant.PlayerId) || volume <= 0) participant.MutePlayerLocally();
            else participant.UnmutePlayerLocally();
        }
    }
}
