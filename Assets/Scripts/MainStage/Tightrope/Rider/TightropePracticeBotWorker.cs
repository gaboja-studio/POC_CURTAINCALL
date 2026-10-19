using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CurtainCall.Tightrope
{
    /// <summary>명시적 실행 인자를 받은 별도 프로세스에서만 자기 키보드 입력을 만든다.</summary>
    public sealed class TightropePracticeBotWorker : MonoBehaviour
    {
        TightropePracticeBotBrain brain;
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        NetworkPlayer player;
        Keyboard keyboard;
        InputSettings workerSettings;
        InputSettings.BackgroundBehavior previousBackground;
        readonly List<InputDevice> disabledDevices = new();
        readonly List<Key> keys = new(6);
        string token, loggedStatus;
        int parentId;
        bool assigned, leaving, tapHeld;
        float nextStatus, nextParentCheck, idleUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), TightropePracticeBot.WorkerFlag) < 0) return;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            new GameObject("Tightrope Practice Bot Worker").AddComponent<TightropePracticeBotWorker>();
        }

        IEnumerator Start()
        {
            ushort port = ushort.TryParse(Argument("-botPort"), out var parsed) ? parsed : (ushort)7777;
            int.TryParse(Argument("-botParent"), out parentId);
            token = Argument("-botToken");
            if (parentId <= 0 || string.IsNullOrEmpty(token) || token.Length > 48) { Application.Quit(2); yield break; }
            session = NetworkSessionManager.EnsureExists();
            if (session == null) { Application.Quit(2); yield break; }
            var join = session.JoinLanAsync("127.0.0.1", port);
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!join.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            if (!join.IsCompleted || join.IsFaulted || !join.Result) { Application.Quit(3); yield break; }
            while (NetworkPlayer.Local == null && Time.realtimeSinceStartup < deadline) yield return null;
            player = NetworkPlayer.Local;
            if (player == null || TightropeCourse.Current == null) { Application.Quit(4); yield break; }
            // 이 worker의 실제 PlayerController/PlayerInputReader는 계속 정상 경로로 실행한다.
            // 사람의 키보드 이벤트는 별도 프로세스에서도 섞이지 않게 worker 안에서만 차단한다.
            foreach (var device in InputSystem.devices)
                if (device.enabled && (device is Keyboard || device is Mouse))
                { disabledDevices.Add(device); InputSystem.DisableDevice(device); }
            // 별도 worker 프로세스의 값만 바꾼다. settings를 교체하면 InputSystem이
            // 임시 기본 인스턴스를 파괴하므로 나중에 그 인스턴스로 복구할 수 없다.
            workerSettings = InputSystem.settings;
            previousBackground = workerSettings.backgroundBehavior;
            workerSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>("Tightrope Practice Bot Keyboard");
            keyboard.MakeCurrent();
            var mover = player.GetComponent<PlayerMover>();
            if (mover != null && mover.Body != null) mover.Body.minMoveDistance = 0f;
            brain = new TightropePracticeBotBrain(player);
            messaging = session.NetworkManager.CustomMessagingManager;
            messaging.RegisterNamedMessageHandler(TightropePracticeBot.ControlMessage, ReceiveControl);
            NetworkPlayer.Restarted += Restarted;
            QueueInput(default);
            SendStatus("접속 완료 · 역할 대기");
        }

        static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        void ReceiveControl(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || reader.Length - reader.Position != 1) return;
            reader.ReadValueSafe(out byte command);
            if (command == 255) { BeginLeave(); return; }
            if (command > (byte)TightropeBotRole.Lower) return;
            brain?.SetRole((TightropeBotRole)command);
            assigned = true;
            idleUntil = Time.time + .2f;
            QueueInput(default);
        }

        void BeginLeave()
        {
            if (leaving) return;
            leaving = true;
            assigned = false;
            QueueInput(default);
            StartCoroutine(Leave());
        }

        IEnumerator Leave()
        {
            var task = session != null ? session.LeaveAsync() : null;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (task != null && !task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Application.Quit();
        }

        void Restarted(NetworkPlayer target)
        {
            if (target != player) return;
            brain?.Reset();
            idleUntil = Time.time + .6f;
            QueueInput(default);
        }

        void Update()
        {
            if (Time.realtimeSinceStartup >= nextParentCheck)
            {
                nextParentCheck = Time.realtimeSinceStartup + 2f;
                try
                {
                    if (parentId > 0)
                    { using var parent = Process.GetProcessById(parentId); if (parent.HasExited) BeginLeave(); }
                }
                catch (ArgumentException) { BeginLeave(); }
            }
            if (brain == null || leaving) return;
            if (session.ConnectionState != SessionConnectionState.Client || TightropeCourse.Current == null)
            { BeginLeave(); return; }
            QueueInput(assigned && Time.time >= idleUntil && !InputGate.IsBlocked ? brain.BuildInput() : default);
            if (Time.realtimeSinceStartup >= nextStatus)
            {
                nextStatus = Time.realtimeSinceStartup + .5f;
                SendStatus(assigned ? brain.Status : "접속 완료 · 역할 대기");
            }
        }

        void QueueInput(PlayerInputFrame input)
        {
            if (keyboard == null || !keyboard.added) return;
            keys.Clear();
            if (input.Move > .25f) keys.Add(Key.W);
            else if (input.Move < -.25f) keys.Add(Key.S);
            if (input.Posture > .25f) keys.Add(Key.D);
            else if (input.Posture < -.25f) keys.Add(Key.A);
            if (input.AuxDirection > 0) keys.Add(Key.E);
            else if (input.AuxDirection < 0) keys.Add(Key.Q);
            if (input.ActionPressed) keys.Add(Key.Space);
            // 짧게 누르기는 한 프레임 눌렀다 떼고, 하차는 기존 F 길게 기준까지 유지한다.
            bool press = input.InteractLongPressed || (input.InteractTapped && !tapHeld);
            if (press) keys.Add(Key.F);
            tapHeld = press;
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
        }

        void SendStatus(string value)
        {
            if (messaging == null) return;
            if (loggedStatus != value)
            {
                loggedStatus = value;
                var mover = player != null ? player.GetComponent<PlayerMover>() : null;
                UnityEngine.Debug.Log($"[TightropeBot] status={value}, role={brain?.Role}, position={(player != null ? player.transform.position.ToString() : "missing")}, "
                    + $"state={(player != null ? player.State.ToString() : "missing")}, airborne={(player != null && player.IsAirborne)}, simulated={mover?.Simulated}, "
                    + $"controls={mover?.ControlEnabled}, inputBlocked={InputGate.IsBlocked}");
            }
            using var writer = new FastBufferWriter(256, Allocator.Temp);
            writer.WriteValueSafe(new FixedString64Bytes(token));
            writer.WriteValueSafe(new FixedString128Bytes(value));
            messaging.SendNamedMessage(TightropePracticeBot.StatusMessage, NetworkManager.ServerClientId,
                writer, NetworkDelivery.ReliableSequenced);
        }

        void OnDestroy()
        {
            NetworkPlayer.Restarted -= Restarted;
            messaging?.UnregisterNamedMessageHandler(TightropePracticeBot.ControlMessage);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            foreach (var device in disabledDevices) if (device.added) InputSystem.EnableDevice(device);
            if (workerSettings != null) workerSettings.backgroundBehavior = previousBackground;
        }
    }
}
