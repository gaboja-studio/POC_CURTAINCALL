using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;

namespace CurtainCall.Voice
{
    public sealed partial class VoiceChat
    {
        // Vivox SDK가 제공하는 시스템 기본 장치의 안정된 ID(물리 기기 이름이 아님).
        public const string SystemDefaultDeviceId = "Default System Device";
        const string InputDeviceKey = "CurtainCall.Voice.InputDevice";
        const string OutputDeviceKey = "CurtainCall.Voice.OutputDevice";
        readonly SemaphoreSlim deviceSelection = new(1, 1);
        bool devicePreferencesRestored;

        /// <summary>SDK 기기 목록·실제 적용 기기가 바뀌면 목록과 선택 ID를 다시 읽는다.</summary>
        public event Action DevicesChanged;
        public bool AreAudioDevicesReady => _vivoxReady;
        public IReadOnlyList<VivoxInputDevice> AvailableInputDevices => _vivoxReady
            ? VivoxService.Instance.AvailableInputDevices : Array.Empty<VivoxInputDevice>();
        public IReadOnlyList<VivoxOutputDevice> AvailableOutputDevices => _vivoxReady
            ? VivoxService.Instance.AvailableOutputDevices : Array.Empty<VivoxOutputDevice>();
        public VivoxInputDevice ActiveInputDevice => _vivoxReady ? VivoxService.Instance.ActiveInputDevice : null;
        public VivoxOutputDevice ActiveOutputDevice => _vivoxReady ? VivoxService.Instance.ActiveOutputDevice : null;
        public VivoxInputDevice EffectiveInputDevice => _vivoxReady ? VivoxService.Instance.EffectiveInputDevice : null;
        public VivoxOutputDevice EffectiveOutputDevice => _vivoxReady ? VivoxService.Instance.EffectiveOutputDevice : null;

        /// <summary>채널 참여 없이 Vivox 초기화·로그인한다. UGS·인증·네트워크 실패가 발생할 수 있다.
        /// 초기화 전 목록은 비어 있으며 SDK가 제공하지 않는 운영체제 기기를 임의로 만들지 않는다.</summary>
        public Task<bool> InitializeAudioDevicesAsync() => EnsureLoggedInAsync();

        /// <summary>표시명·인덱스 대신 AvailableInputDevices의 DeviceID로 선택한다.
        /// ID가 사라지면 false, SDK·초기화 실패는 호출자에게 예외로 전달한다.</summary>
        public async Task<bool> SelectInputDeviceAsync(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return false;
            if (deviceId == SystemDefaultDeviceId && !_vivoxReady)
            {
                PlayerPrefs.DeleteKey(InputDeviceKey); PlayerPrefs.Save();
                return true;
            }
            if (!await EnsureLoggedInAsync()) return false;
            await deviceSelection.WaitAsync();
            try
            {
                foreach (var device in AvailableInputDevices)
                {
                    if (device.DeviceID != deviceId) continue;
                    await VivoxService.Instance.SetActiveInputDeviceAsync(device);
                    PlayerPrefs.SetString(InputDeviceKey, deviceId);
                    PlayerPrefs.Save();
                    ApplyMicMute(); ApplyDeviceLevels();
                    DevicesChanged?.Invoke();
                    return true;
                }
                return false;
            }
            finally { deviceSelection.Release(); }
        }

        /// <summary>Vivox 음성 출력만 선택한다. Unity 게임 소리 출력은 운영체제가 관리한다.</summary>
        public async Task<bool> SelectOutputDeviceAsync(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return false;
            if (deviceId == SystemDefaultDeviceId && !_vivoxReady)
            {
                PlayerPrefs.DeleteKey(OutputDeviceKey); PlayerPrefs.Save();
                return true;
            }
            if (!await EnsureLoggedInAsync()) return false;
            await deviceSelection.WaitAsync();
            try
            {
                foreach (var device in AvailableOutputDevices)
                {
                    if (device.DeviceID != deviceId) continue;
                    await VivoxService.Instance.SetActiveOutputDeviceAsync(device);
                    PlayerPrefs.SetString(OutputDeviceKey, deviceId);
                    PlayerPrefs.Save();
                    ApplyDeviceLevels();
                    DevicesChanged?.Invoke();
                    return true;
                }
                return false;
            }
            finally { deviceSelection.Release(); }
        }

        async Task RestoreDevicePreferencesAsync()
        {
            if (devicePreferencesRestored) return;
            // VoiceChat 수명 중 한 번 복구한다. 사라진 기기는 SDK의 대체 선택을 유지하고,
            // 저장만 된 기기를 현재 실제 적용 기기로 표시하지 않는다.
            devicePreferencesRestored = true;
            string input = PlayerPrefs.GetString(InputDeviceKey, "");
            string output = PlayerPrefs.GetString(OutputDeviceKey, "");
            foreach (var device in AvailableInputDevices)
            {
                if (device.DeviceID != input) continue;
                try { await VivoxService.Instance.SetActiveInputDeviceAsync(device); }
                catch (Exception) { Debug.LogWarning("[Voice] 저장한 입력 장치를 적용하지 못했습니다. 현재 SDK 장치를 유지합니다."); }
                break;
            }
            foreach (var device in AvailableOutputDevices)
            {
                if (device.DeviceID != output) continue;
                try { await VivoxService.Instance.SetActiveOutputDeviceAsync(device); }
                catch (Exception) { Debug.LogWarning("[Voice] 저장한 출력 장치를 적용하지 못했습니다. 현재 SDK 장치를 유지합니다."); }
                break;
            }
        }

        void HandleDevicesChanged() => DevicesChanged?.Invoke();
    }
}
