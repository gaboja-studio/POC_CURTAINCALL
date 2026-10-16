using System.IO;
using System.Text;
using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CurtainCall.Player.Tests
{
    /// <summary>
    /// Task-045 진단용(테스트 전용, 씬에 저장하지 않는다). Play 중 붙이면 게임 프레임마다
    /// 이동 키(플레이어 쪽 키보드 상태)·입력 리더 값·시점·커서·포커스 창을 읽어, 값이 바뀐 프레임만
    /// 프로젝트 Logs/fpm-input-probe.csv(git 제외)에 남긴다. 키가 눌린 채 남는 현상의 원인을 가리기 위한 것.
    /// </summary>
    [DefaultExecutionOrder(-40)] // 입력 리더(-100)·카메라(-75)·조작(-50) 다음
    public sealed class FirstPersonMovementInputProbe : MonoBehaviour
    {
        StreamWriter writer;
        string last;

        public static string LogPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/fpm-input-probe.csv"));

        void OnEnable()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            writer = new StreamWriter(LogPath, false, Encoding.UTF8) { AutoFlush = true };
            writer.WriteLine("frame,time,keys,move,posture,view,mouseLook,firstPerson,lock,appFocus,focusedWindow,gate");
            last = null;
            InputSystem.onEvent += OnInputEvent;
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        void OnDisable()
        {
            InputSystem.onEvent -= OnInputEvent;
            InputSystem.onDeviceChange -= OnDeviceChange;
            writer?.Dispose();
            writer = null;
        }

        // 상태 이벤트(키 값) 말고 포커스·리셋 같은 이벤트와 장치 변화만 따로 남긴다
        void OnInputEvent(InputEventPtr e, InputDevice device)
        {
            if (e.type == StateEvent.Type || e.type == DeltaStateEvent.Type) return;
            writer?.WriteLine($"{Time.frameCount},{Time.time:F3},EVENT {e.type} device={device?.name}");
        }

        void OnDeviceChange(InputDevice device, InputDeviceChange change) =>
            writer?.WriteLine($"{Time.frameCount},{Time.time:F3},DEVICE {change} device={device?.name}");

        void OnApplicationFocus(bool focus) => writer?.WriteLine($"{Time.frameCount},{Time.time:F3},APPFOCUS {focus}");

        void Update()
        {
            var keyboard = Keyboard.current;
            string keys = keyboard == null ? "none"
                : (keyboard.wKey.isPressed ? "W" : "") + (keyboard.aKey.isPressed ? "A" : "") +
                  (keyboard.sKey.isPressed ? "S" : "") + (keyboard.dKey.isPressed ? "D" : "");
            var local = NetworkPlayer.Local;
            var reader = local != null ? local.GetComponent<PlayerInputReader>() : null;
            var frame = reader != null ? reader.Current : default;
            var director = CameraViewDirector.Current;
            string view = director != null ? $"{director.ViewForward.x:F1};{director.ViewForward.z:F1}" : "-";
            string window = "-";
#if UNITY_EDITOR
            var focused = UnityEditor.EditorWindow.focusedWindow;
            window = focused != null ? focused.GetType().Name : "none";
#endif
            string state = string.Join(",", keys, frame.Move, frame.Posture, view,
                director != null && director.UsesMouseLook, director != null && director.IsFirstPerson,
                Cursor.lockState, Application.isFocused, window, InputGate.IsBlocked);
            if (state == last) return;
            last = state;
            writer?.WriteLine($"{Time.frameCount},{Time.time:F3},{state}");
        }
    }
}
