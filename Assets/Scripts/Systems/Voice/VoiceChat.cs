using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Settings;
using CurtainCall.StartLobby;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CurtainCall.Voice
{
    /// <summary>
    /// 음성 채팅 공개 진입점. 실행하면 저절로 하나 생기고(DontDestroyOnLoad), Start 씬부터 이어진 흐름에서 세팅 `사용 씬`에 있는 씬일 때만 동작한다.
    /// 생존자 = Vivox 위치 채널(거리 감쇠), 사망자(추락·리타이어) = 그룹 채널(거리 무관). 한 번에 한 채널만 들어가 두 무리는 서로 안 들린다.
    /// 상시 마이크 + 음소거 키. 결정: Harness/Project/Decisions/voice-chat.md, 값: 공통 세팅 <see cref="BaseGameSettings.VoiceChatSettings"/>.
    /// </summary>
    public sealed partial class VoiceChat : MonoBehaviour
    {
        const float FirstRetryDelay = 5f;
        const float MaxRetryDelay = 60f;
        const float PositionInterval = 0.1f;
        const float PositionMoveThreshold = 0.05f;
        const float PositionTurnThreshold = 2f;

        /// <summary>실행 중인 음성 채팅. 첫 씬이 열릴 때 만들어진다.</summary>
        public static VoiceChat Instance { get; private set; }

        /// <summary>지금 들어가 있는 무리.</summary>
        public VoiceRoom Room { get; private set; }

        /// <summary>지금 들어가 있는 Vivox 채널 이름(없으면 null).</summary>
        public string ChannelName => _channel;

        /// <summary>음성 로그인 완료 여부.</summary>
        public bool IsLoggedIn => _vivoxReady && VivoxService.Instance.IsLoggedIn;

        /// <summary>내 마이크가 꺼져 있는지. 무리가 바뀌어도 유지된다.</summary>
        public bool IsMicMuted { get; private set; }

        /// <summary>마지막 실패 이유(없으면 null). 실패해도 게임은 계속되고 잠시 뒤 다시 시도한다.</summary>
        public string LastError { get; private set; }

        /// <summary>무리가 바뀔 때(입장·전환·나감).</summary>
        public event Action<VoiceRoom> RoomChanged;

        /// <summary>내 마이크 꺼짐 상태가 바뀔 때.</summary>
        public event Action<bool> MicMutedChanged;

        readonly VoiceRoomKey _roomKey = new();
        string _channel;
        bool _busy;
        bool _vivoxReady;
        bool _subscribed;
        float _retryAt;
        float _retryDelay = FirstRetryDelay;
        float _nextPositionAt;
        Vector3 _lastPosition;
        Vector3 _lastForward;
        bool _forcePosition;

        static BaseGameSettings.VoiceChatSettings Settings => GameSettings.Base.VoiceChat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateOnLoad()
        {
            if (Instance != null) return;
            var go = new GameObject(nameof(VoiceChat));
            DontDestroyOnLoad(go);
            go.AddComponent<VoiceChat>();
            go.AddComponent<VoiceChatDevOverlay>();
        }

        /// <summary>지금 채널의 참가자(나 포함). 채널이 없으면 빈 목록.</summary>
        public IReadOnlyList<VivoxParticipant> Participants
        {
            get
            {
                if (!_vivoxReady || _channel == null) return Array.Empty<VivoxParticipant>();
                return VivoxService.Instance.ActiveChannels.TryGetValue(_channel, out var list)
                    ? list
                    : Array.Empty<VivoxParticipant>();
            }
        }

        /// <summary>내 마이크를 끄거나 켠다. 로그인 전에 불러도 로그인 뒤 적용된다.</summary>
        public void SetMicMuted(bool muted)
        {
            if (IsMicMuted == muted) return;
            IsMicMuted = muted;
            PlayerPrefs.SetInt(MicMuteKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMicMute();
            Debug.Log(muted ? "[Voice] 내 마이크 끔" : "[Voice] 내 마이크 켬");
            MicMutedChanged?.Invoke(muted);
        }

        /// <summary>내 마이크 끄기/켜기를 바꾼다(음소거 키와 같음).</summary>
        public void ToggleMicMuted() => SetMicMuted(!IsMicMuted);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            LoadAudioPreferences();
            ClientSettings.AudioChanged += ApplyDeviceLevels;
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            ClientSettings.AudioChanged -= ApplyDeviceLevels;
            _roomKey.Reset();
            Unsubscribe();
        }

        void Update()
        {
            var settings = Settings;
            bool active = settings.Enabled
                && VoiceRoomRules.IsVoiceScene(StartLobbyFlow.Instance != null, SceneManager.GetActiveScene().name, settings.Scenes);

            if (active && !CurtainCall.Player.InputGate.IsRequested && MuteKeyPressed(settings.MuteKey))
                ToggleMicMuted();

            string key = active ? _roomKey.Resolve(NetworkSessionManager.Instance, settings.UseOnLan) : null;
            if (!active) _roomKey.Reset();

            var local = NetworkPlayer.Local;
            var desired = VoiceRoomRules.Desired(settings.Enabled, active, key, local != null, local != null ? local.State : PlayerState.Normal);
            string desiredChannel = VoiceRoomRules.ChannelName(key, desired);

            if (!_busy && desiredChannel != _channel && Time.unscaledTime >= _retryAt)
                _ = SwitchAsync(desiredChannel, desired);

            if (Room == VoiceRoom.Alive && !_busy && local != null)
                UpdatePosition(local.transform, settings.MouthHeight);
        }

        // ── 채널 전환 ─────────────────────────────────────────

        async Task SwitchAsync(string channel, VoiceRoom room)
        {
            _busy = true;
            try
            {
                if (_channel != null)
                {
                    string leaving = _channel;
                    SetChannel(null, VoiceRoom.None);
                    if (IsLoggedIn) await VivoxService.Instance.LeaveChannelAsync(leaving);
                    Debug.Log($"[Voice] 채널 나감: {leaving}");
                }

                if (channel == null) return;
                if (!await EnsureLoggedInAsync()) return;

                if (room == VoiceRoom.Alive)
                {
                    var s = Settings;
                    var props = new Channel3DProperties(s.AudibleDistance, s.ConversationalDistance, s.FadeIntensity, (AudioFadeModel)(int)s.FadeModel);
                    await VivoxService.Instance.JoinPositionalChannelAsync(channel, ChatCapability.AudioOnly, props);
                }
                else
                {
                    await VivoxService.Instance.JoinGroupChannelAsync(channel, ChatCapability.AudioOnly);
                }

                SetChannel(channel, room);
                _forcePosition = true;
                LastError = null;
                _retryDelay = FirstRetryDelay;
                Debug.Log($"[Voice] {(room == VoiceRoom.Alive ? "생존자(거리 감쇠)" : "사망자(거리 무관)")} 채널 입장: {channel}");
            }
            catch (Exception e)
            {
                LastError = e.Message;
                _retryAt = Time.unscaledTime + _retryDelay;
                Debug.LogWarning($"[Voice] 음성 채널 처리 실패({_retryDelay:0}초 뒤 다시 시도): {e.Message}");
                _retryDelay = Mathf.Min(_retryDelay * 2f, MaxRetryDelay);
            }
            finally
            {
                _busy = false;
            }
        }

        void SetChannel(string channel, VoiceRoom room)
        {
            _channel = channel;
            if (Room == room) return;
            Room = room;
            RoomChanged?.Invoke(room);
        }

        Task<bool> _loginTask;

        Task<bool> EnsureLoggedInAsync()
        {
            if (_loginTask == null || _loginTask.IsCompleted) _loginTask = LoginCoreAsync();
            return _loginTask;
        }

        async Task<bool> LoginCoreAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // LAN 접속처럼 세션이 UGS를 초기화하지 않았을 때: 세션과 같은 방식으로 실행마다 다른 익명 프로필을 쓴다.
                var options = new InitializationOptions().SetProfile("p" + Guid.NewGuid().ToString("N").Substring(0, 12));
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            var vivox = VivoxService.Instance;
            if (vivox == null)
                throw new InvalidOperationException("Vivox 서비스가 없습니다(패키지·UGS 초기화 확인).");
            Subscribe(vivox);

            if (vivox.InitializationState != VivoxInitializationState.Initialized)
                await vivox.InitializeAsync();
            _vivoxReady = true;

            if (!vivox.IsLoggedIn)
            {
                await vivox.LoginAsync(new LoginOptions { DisplayName = DisplayName() });
                Debug.Log("[Voice] 음성 로그인 완료");
            }
            await RestoreDevicePreferencesAsync();
            ApplyMicMute();
            ApplyDeviceLevels();
            DevicesChanged?.Invoke();
            return vivox.IsLoggedIn;
        }

        static string DisplayName()
        {
            var local = NetworkPlayer.Local;
            return local != null && local.Slot != NetworkPlayer.NoSlot ? $"P{local.Slot + 1}" : "Player";
        }

        void Subscribe(IVivoxService vivox)
        {
            if (_subscribed) return;
            _subscribed = true;
            vivox.ChannelLeft += HandleChannelLeft;
            vivox.LoggedOut += HandleLoggedOut;
            vivox.ConnectionFailedToRecover += HandleLoggedOut;
            vivox.ParticipantAddedToChannel += ApplyParticipantPreference;
            vivox.AvailableInputDevicesChanged += HandleDevicesChanged;
            vivox.AvailableOutputDevicesChanged += HandleDevicesChanged;
            vivox.EffectiveInputDeviceChanged += HandleDevicesChanged;
            vivox.EffectiveOutputDeviceChanged += HandleDevicesChanged;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            var vivox = VivoxService.Instance;
            if (vivox == null) return;
            vivox.ChannelLeft -= HandleChannelLeft;
            vivox.LoggedOut -= HandleLoggedOut;
            vivox.ConnectionFailedToRecover -= HandleLoggedOut;
            vivox.ParticipantAddedToChannel -= ApplyParticipantPreference;
            vivox.AvailableInputDevicesChanged -= HandleDevicesChanged;
            vivox.AvailableOutputDevicesChanged -= HandleDevicesChanged;
            vivox.EffectiveInputDeviceChanged -= HandleDevicesChanged;
            vivox.EffectiveOutputDeviceChanged -= HandleDevicesChanged;
        }

        // 내가 부르지 않았는데 채널에서 빠졌으면(연결 끊김 등) 다음 Update가 다시 들어간다.
        void HandleChannelLeft(string channel)
        {
            if (channel == _channel && !_busy) SetChannel(null, VoiceRoom.None);
        }

        void HandleLoggedOut()
        {
            if (!_busy) SetChannel(null, VoiceRoom.None);
        }

        // ── 마이크·위치 ───────────────────────────────────────

        void ApplyMicMute()
        {
            if (!_vivoxReady) return;
            var vivox = VivoxService.Instance;
            bool muted = IsMicMuted || MicVolume <= 0f;
            if (vivox == null || vivox.IsInputDeviceMuted == muted) return;
            if (muted) vivox.MuteInputDevice();
            else vivox.UnmuteInputDevice();
        }

        /// <summary>생존자 채널: 내 캐릭터 입 높이를 말하는/듣는 위치로, 카메라 정면을 듣는 방향으로 보낸다.</summary>
        void UpdatePosition(Transform player, float mouthHeight)
        {
            if (Time.unscaledTime < _nextPositionAt && !_forcePosition) return;
            if (!VivoxService.Instance.ActiveChannels.ContainsKey(_channel)) return;

            var cam = Camera.main;
            Vector3 position = player.position + Vector3.up * mouthHeight;
            Vector3 forward = cam != null ? cam.transform.forward : player.forward;
            Vector3 up = cam != null ? cam.transform.up : Vector3.up;

            if (!_forcePosition
                && (position - _lastPosition).sqrMagnitude < PositionMoveThreshold * PositionMoveThreshold
                && Vector3.Angle(forward, _lastForward) < PositionTurnThreshold)
                return;

            VivoxService.Instance.Set3DPosition(position, position, forward, up, _channel);
            _lastPosition = position;
            _lastForward = forward;
            _forcePosition = false;
            _nextPositionAt = Time.unscaledTime + PositionInterval;
        }

        /// <summary>글자 입력칸에 입력 중이면 음소거 키로 보지 않는다(방 코드 입력 등).</summary>
        static bool MuteKeyPressed(Key key)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || key == Key.None || !keyboard[key].wasPressedThisFrame) return false;
            if (GUIUtility.keyboardControl != 0) return false;

            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return true;
            if (selected.TryGetComponent<TMP_InputField>(out var tmp) && tmp.isFocused) return false;
            if (selected.TryGetComponent<InputField>(out var legacy) && legacy.isFocused) return false;
            return true;
        }
    }
}
