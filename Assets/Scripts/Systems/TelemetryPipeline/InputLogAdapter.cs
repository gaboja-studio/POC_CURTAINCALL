using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.Voice;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CurtainCall.Telemetry.Pipeline
{
    sealed class InputLogAdapter : IDisposable
    {
        readonly TelemetryRuntime log;
        string microphoneStart;
        int keyboardFrame = -1;
        readonly Dictionary<Button, UnityAction> buttons = new();
        float nextButtons;
        public InputLogAdapter(TelemetryRuntime runtime) { log = runtime; InputSystem.onAfterUpdate += Keys; }
        void Keys()
        {
            if (!Application.isFocused || keyboardFrame == Time.frameCount) return;
            keyboardFrame = Time.frameCount;
            var keyboard = Keyboard.current;
            if (keyboard != null)
                foreach (var key in keyboard.allKeys) Button(key, "Keyboard");
            var mouse = Mouse.current;
            if (mouse == null) return;
            Button(mouse.leftButton, "Mouse"); Button(mouse.rightButton, "Mouse"); Button(mouse.middleButton, "Mouse");
            float scroll = mouse.scroll.ReadValue().y;
            if (Math.Abs(scroll) > 0) log.Emit("INPUT_KEY", new() { ["keyId"] = "<Mouse>/scroll", ["inputMethod"] = scroll > 0 ? "WHEEL_UP" : "WHEEL_DOWN" });
        }
        void Button(ButtonControl control, string device)
        {
            if (control.wasPressedThisFrame) Record(control, device, "PRESS_DOWN");
            if (control.wasReleasedThisFrame) Record(control, device, "PRESS_UP");
        }
        void Record(ButtonControl control, string device, string method) => log.Emit("INPUT_KEY", new()
        { ["keyId"] = "<" + device + ">/" + control.name, ["inputMethod"] = method });
        public void Tick()
        {
            if (Time.unscaledTime >= nextButtons)
            {
                nextButtons = Time.unscaledTime + 0.5f;
                foreach (var button in buttons.Keys.ToArray()) if (button == null) buttons.Remove(button);
                foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (buttons.ContainsKey(button)) continue;
                    UnityAction clicked = () =>
                    {
                        string type = TelemetryRuntime.SceneType(SceneManager.GetActiveScene().name);
                        if (type != "UNKNOWN") log.Emit("BUTTON_INPUT", new() { ["buttonId"] = "<" + type + ">/" + button.name,
                            ["objectId"] = DetailLogIds.ObjectId(button, "IN_") });
                    };
                    buttons.Add(button, clicked); button.onClick.AddListener(clicked);
                }
            }
            var voice = VoiceChat.Instance;
            bool speaking = voice != null && voice.IsLoggedIn && !voice.IsMicMuted
                && voice.Participants.Any(p => p.IsSelf && p.SpeechDetected);
            if (speaking && microphoneStart == null) microphoneStart = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            if (!speaking) CloseMicrophone();
        }
        public void CloseMicrophone()
        {
            if (microphoneStart == null) return;
            log.Emit("MICROPHONE_INPUT", new() { ["startedAt"] = microphoneStart, ["endedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), ["source"] = "VIVOX_SPEECH_DETECTION" });
            microphoneStart = null;
        }
        public void Dispose()
        {
            InputSystem.onAfterUpdate -= Keys; CloseMicrophone();
            foreach (var pair in buttons) if (pair.Key != null) pair.Key.onClick.RemoveListener(pair.Value);
            buttons.Clear();
        }
    }
}
