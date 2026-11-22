using System.Collections.Generic;
using CurtainCall.StartLobby;
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
        // Scale 2 allowed positive gain. Scales 1 and 3 top out at unity.
        const string MicVolumeScaleKey = "CurtainCall.Voice.MicVolumeScale";
        const int MicVolumeScale = 3;
        /// <summary>내 마이크 입력 음량 기본값. 예전 최대(Vivox 기본 0)와 같은 크기다.</summary>
        public const float DefaultMicVolume = 1f;
        public float MicVolume { get; private set; } = DefaultMicVolume;
        public float TeamVolume { get; private set; } = 1f;
        public bool IsTeamMuted { get; private set; }
        // PlayerId, never a reusable seat number. Kept across alive/dead channels in this run.
        readonly Dictionary<string, (float volume, bool muted)> participantPreferences = new();

        void LoadAudioPreferences()
        {
            IsMicMuted = PlayerPrefs.GetInt(MicMuteKey, 0) != 0;
            MicVolume = LoadMicVolume();
            TeamVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(TeamVolumeKey, 1));
            IsTeamMuted = PlayerPrefs.GetInt(TeamMuteKey, 0) != 0;
        }

        /// <summary>Local device settings; never changes another player's microphone.</summary>
        public void ApplyAudioPreferences(float mic, bool micMuted, float team, bool teamMuted)
        {
            MicVolume = Mathf.Clamp01(mic); TeamVolume = Mathf.Clamp01(team); IsTeamMuted = teamMuted;
            SetMicMuted(micMuted);
            PlayerPrefs.SetFloat(MicVolumeKey, MicVolume);
            PlayerPrefs.SetInt(MicVolumeScaleKey, MicVolumeScale);
            PlayerPrefs.SetFloat(TeamVolumeKey, TeamVolume);
            PlayerPrefs.SetInt(TeamMuteKey, IsTeamMuted ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMicMute(); ApplyDeviceLevels();
        }

        static float LoadMicVolume()
        {
            if (!PlayerPrefs.HasKey(MicVolumeKey)) return DefaultMicVolume;
            float saved = Mathf.Clamp01(PlayerPrefs.GetFloat(MicVolumeKey, 1));
            int scale = PlayerPrefs.GetInt(MicVolumeScaleKey, 1);
            if (scale >= MicVolumeScale) return saved;
            float migrated = scale == 2 ? MigrateLegacyMicVolume(saved) : saved;
            PlayerPrefs.SetFloat(MicVolumeKey, migrated);
            PlayerPrefs.SetInt(MicVolumeScaleKey, MicVolumeScale);
            PlayerPrefs.Save();
            return migrated;
        }

        // Preserve the old input level up to unity and cap previously boosted settings.
        public static float MigrateLegacyMicVolume(float saved) => Mathf.Clamp01(saved * 2f);

        // 100% is Vivox's unchanged default (0), not its +50 boost. Zero explicitly mutes.
        public static int ToVivoxVolume(float normalized) => Mathf.RoundToInt(Mathf.Lerp(-50, 0, Mathf.Clamp01(normalized)));
        public static int ToVivoxMicVolume(float normalized) => ToVivoxVolume(normalized);
        void ApplyDeviceLevels()
        {
            if (!_vivoxReady) return;
            var service = VivoxService.Instance;
            service.SetInputDeviceVolume(ToVivoxMicVolume(MicVolume));
            // Vivox uses native output, not AudioListener. Apply master once, never BGM/SFX.
            float output = TeamVolume * ClientSettings.Volume;
            service.SetOutputDeviceVolume(ToVivoxVolume(output));
            if (IsTeamMuted || output <= 0) service.MuteOutputDevice(); else service.UnmuteOutputDevice();
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
