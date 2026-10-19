#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using CurtainCall.Campaign;
using CurtainCall.Dev;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.Reward;
using CurtainCall.Tightrope.Saws;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace CurtainCall.Tightrope.Tests
{
    /// <summary>명시적 Begin으로만 켜는 실제 LAN 검증 도구. 게임 동작은 공개 API와 합성 키 입력으로만 요청한다.</summary>
    public sealed partial class TightropeMultiplayerProbe : MonoBehaviour
    {
        static TightropeMultiplayerProbe current;
        static readonly FieldInfo DevCache = typeof(DevMode).GetField("_enabled", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly FieldInfo RestartCounter = typeof(NetworkPlayer).GetField("restartCount", BindingFlags.Instance | BindingFlags.NonPublic);
        string role, status = "starting", error = "";
        Keyboard keyboard, previousKeyboard;
        InputSettings originalSettings, workingSettings;
        InputSettings.BackgroundBehavior previousBackground;
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        object previousDev;
        int previousTargetFrameRate;
        bool previousRunInBackground, overridesApplied, walking, autoBalance;
        int walkDirection, resetFrames, emptyFrames, interactFrames;
        float targetDistance, walkDeadline;
        bool performanceRequested;
        NetworkSessionManager session;

        /// <summary>이미 켜져 있으면 거절한다. Play Mode와 외줄 씬을 준비한 뒤 각 프로세스에서 한 번 호출한다.</summary>
        public static bool Begin(string role, int port = 7799)
        {
            if (!Application.isPlaying || current != null || (role != "host" && role != "client")
                || port < 1 || port > ushort.MaxValue || TightropeCourse.Current == null) return false;
            var existing = NetworkSessionManager.Instance;
            if (existing != null && existing.ConnectionState != SessionConnectionState.Offline) return false;
            var go = new GameObject("Tightrope Multiplayer Probe") { hideFlags = HideFlags.DontSave };
            current = go.AddComponent<TightropeMultiplayerProbe>();
            current.role = role;
            current.Connect((ushort)port);
            return true;
        }

        async void Connect(ushort port)
        {
            try
            {
                ApplyOverrides();
                session = NetworkSessionManager.EnsureExists();
                bool connected = role == "host" ? await session.HostLanAsync(port)
                    : await session.JoinLanAsync("127.0.0.1", port);
                if (this == null) return;
                status = connected ? "connected" : "connection failed";
                if (!connected) { error = session.LastDisconnectReason ?? "LAN connection rejected"; RestoreOverrides(); }
            }
            catch (Exception exception)
            {
                if (this == null) return;
                error = exception.ToString(); status = "error"; RestoreOverrides();
            }
        }

        void ApplyOverrides()
        {
            previousDev = DevCache.GetValue(null);
            previousRunInBackground = Application.runInBackground;
            previousTargetFrameRate = Application.targetFrameRate;
            previousKeyboard = Keyboard.current;
            originalSettings = InputSystem.settings;
            previousBackground = originalSettings.backgroundBehavior;
            previousEditorInput = originalSettings.editorInputBehaviorInPlayMode;
            overridesApplied = true;
            DevCache.SetValue(null, true); // EditorPrefs에는 기록하지 않는다.
            Application.runInBackground = true;
            // 렌더링 없는 클라이언트의 수천 FPS에서는 프레임당 이동이 CharacterController의
            // minMoveDistance보다 작아 버려진다. 실제 입력 경로 검증은 동일한 60 FPS로 맞춘다.
            Application.targetFrameRate = 60;
            workingSettings = originalSettings;
            if (UnityEditor.EditorUtility.IsPersistent(originalSettings))
            {
                workingSettings = Instantiate(originalSettings);
                workingSettings.hideFlags = HideFlags.DontSave;
                InputSystem.settings = workingSettings;
            }
            workingSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            workingSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("Tightrope Multiplayer Probe Keyboard");
            keyboard.MakeCurrent();
            emptyFrames = 3;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }

        /// <summary>목표는 시작선 기준 절대 거리(m). +1 W / -1 S로 목표를 통과할 때까지 걷는다.</summary>
        public static bool StartWalk(int direction, float targetCourseDistance, bool balance = true)
        {
            if (current == null || current.keyboard == null || NetworkPlayer.Local == null || direction == 0
                || !float.IsFinite(targetCourseDistance)) return false;
            current.walkDirection = Math.Sign(direction);
            current.targetDistance = targetCourseDistance;
            current.autoBalance = balance;
            current.walking = true;
            current.walkDeadline = Time.realtimeSinceStartup + 20f;
            current.status = "walking";
            return true;
        }

        public static void StopWalk() { if (current != null) { current.walking = false; current.emptyFrames = 2; } }
        public static void SetAutoBalance(bool value) { if (current != null) current.autoBalance = value; }

        /// <summary>호스트와 클라이언트 모두 같은 R 키 경로를 사용한다. 클라이언트 거절도 게임 코드가 판단한다.</summary>
        public static bool PressReset()
        {
            if (current == null || current.keyboard == null) return false;
            current.walking = false;
            current.resetFrames = 2;
            current.emptyFrames = 2;
            return true;
        }

        /// <summary>참가자의 실제 F 누름/뗌 입력으로 봉 요청과 응답을 검사한다.</summary>
        public static bool PressInteract()
        {
            if (current == null || current.keyboard == null) return false;
            current.walking = false; current.interactFrames = 2; current.emptyFrames = 2;
            return true;
        }

        public static bool StartPerformance()
        {
            if (current == null || current.session == null || !current.session.IsHost) return false;
            var campaign = CampaignRun.Instance;
            if (current.session.GameState == GameSessionState.Waiting && !current.session.StartGame()) return false;
            if (campaign == null) return false;
            current.performanceRequested = true;
            return true;
        }

        public static bool InjureRemote(ulong ownerId, int cuts = 1, bool kill = false)
        {
            if (current == null || current.session == null || !current.session.IsHost) return false;
            foreach (var player in NetworkPlayer.All)
                if (player.OwnerClientId == ownerId && !player.IsLocal)
                {
                    if (kill) return player.ServerKill();
                    return player.ServerCut(Mathf.Clamp(cuts, 1, 4)) != BodyPart.None;
                }
            return false;
        }

        void Update()
        {
            if (!journey) QueueProbeInput();
        }

        void QueueProbeInput()
        {
            if (keyboard == null) return;
            if (performanceRequested && CampaignRun.Instance != null)
            {
                if (CampaignRun.Instance.State.Phase == CampaignPhase.Intro)
                { CampaignRun.Instance.EndIntro(); performanceRequested = false; }
                else if (CampaignRun.Instance.State.Phase == CampaignPhase.Performance) performanceRequested = false;
            }
            var keys = new List<Key>(3);
            var player = NetworkPlayer.Local;
            var course = TightropeCourse.Current;
            bool blocked = InputGate.IsBlocked;
            if (walking && (player == null || course == null || player.State != PlayerState.Normal
                || Time.realtimeSinceStartup >= walkDeadline))
            { walking = false; status = "walk stopped"; }
            if (walking && walkDirection * (course.GetDistance(player.transform.position) - targetDistance) >= 0f)
            { walking = false; status = "walk complete"; }
            // R 직전에는 키를 완전히 뗀 두 프레임을 거쳐 RequireRelease 경계를 넘긴다.
            if (emptyFrames > 0) emptyFrames--;
            else if (resetFrames > 0 && (resetFrames < 2 || !blocked))
            { keys.Add(Key.R); resetFrames--; }
            else if (interactFrames > 0 && (interactFrames < 2 || !blocked))
            { keys.Add(Key.F); interactFrames--; }
            else if (!blocked && !JourneyKeys(keys))
            {
                if (walking) keys.Add(walkDirection > 0 ? Key.W : Key.S);
                var control = player != null ? player.GetComponent<PlayerController>() : null;
                var balance = player != null ? player.GetComponent<PlayerBalance>() : null;
                if (autoBalance && control != null && control.Scheme is TightropeControlScheme
                    && balance != null && balance.IsActive && !balance.HasFallen)
                {
                    if (balance.Value > 3f) keys.Add(Key.A);
                    else if (balance.Value < -3f) keys.Add(Key.D);
                }
            }
            // Update는 다음 정상 InputSystem 갱신에 소비할 이벤트만 쌓는다. 강제 InputSystem.Update는 하지 않는다.
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
        }

        public static ProbeSnapshot Capture()
        {
            var result = new ProbeSnapshot
            {
                processId = System.Diagnostics.Process.GetCurrentProcess().Id,
                role = current != null ? current.role : "inactive",
                status = current != null ? current.status : "inactive",
                error = current != null ? current.error : "",
                scene = SceneManager.GetActiveScene().path,
                frame = Time.frameCount, elapsed = Time.realtimeSinceStartup,
                walking = current != null && current.walking,
                inputBlocked = InputGate.IsBlocked
            };
            var session = NetworkSessionManager.Instance;
            result.connection = session != null ? session.ConnectionState.ToString() : "none";
            result.gameState = session != null ? session.GameState.ToString() : "none";
            result.connectedCount = session != null ? session.PlayerCount : 0;
            var campaign = CampaignRun.Instance;
            result.campaignPhase = campaign != null ? campaign.State.Phase.ToString() : "none";
            result.generation = campaign != null ? campaign.State.Generation : 0;
            var bridge = TightropeCampaignBridge.Current;
            result.performanceId = bridge != null ? bridge.PerformanceId : "";
            result.performanceOpen = bridge != null && bridge.PerformanceOpen;
            var run = TightropeRun.Current;
            result.round = run != null ? run.Round : 0;
            result.runState = run != null ? run.State.ToString() : "none";
            result.timeRemaining = run != null ? run.TimeRemaining : 0f;
            result.performanceStarted = run != null && run.IsPerformanceStarted;
            var course = TightropeCourse.Current;
            foreach (var player in NetworkPlayer.All)
            {
                var item = PlayerSnapshot.From(player, course);
                result.players.Add(item);
                if (player.IsLocal) result.local = item;
            }
            var rewards = PerformanceRewards.Instance;
            result.fundBalance = rewards != null ? rewards.Balance : 0;
            result.historyCount = rewards != null ? rewards.History.Count : 0;
            if (rewards?.State != null)
                foreach (var segment in rewards.State.Segments)
                    result.segments.Add(new SegmentSnapshot { ownerId = segment.ClientId, dead = segment.Dead,
                        connected = segment.Connected, lostCount = segment.LostCount });
            var saws = TightropeSaws.Current;
            result.verticalActive = saws != null && saws.IsVerticalActive;
            result.verticalLane = saws != null ? saws.VerticalLane : -1;
            result.obstacleElapsed = saws != null ? saws.GameElapsed : 0f;
            // 톱 자체의 별도 round 값은 없다. 연결된 런 round와 실제 장애물 상태를 함께 기록한다.
            result.obstacleRunRound = run != null && saws != null ? run.Round : -1;
            return result;
        }

        public static string SnapshotJson() => JsonUtility.ToJson(Capture());

        public static void End()
        {
            if (current == null) return;
            var ending = current;
            current = null;
            ending.RestoreOverrides();
            ending.LeaveAndDestroy();
        }

        async void LeaveAndDestroy()
        {
            try { if (session != null) await session.LeaveAsync(); }
            catch (Exception exception) { Debug.LogWarning("[TightropeProbe] Cleanup: " + exception.Message); }
            finally { if (this != null) Destroy(gameObject); }
        }

        void OnDisable() => RestoreOverrides();
        void OnDestroy() { RestoreOverrides(); if (current == this) current = null; }

        void RestoreOverrides()
        {
            if (!overridesApplied) return;
            overridesApplied = false;
            RestoreJourneyDevices();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            if (workingSettings != null)
            {
                if (workingSettings != originalSettings && originalSettings != null)
                {
                    InputSystem.settings = originalSettings;
                    if (workingSettings != null) Destroy(workingSettings);
                }
                else
                {
                    workingSettings.backgroundBehavior = previousBackground;
                    workingSettings.editorInputBehaviorInPlayMode = previousEditorInput;
                }
            }
            Application.runInBackground = previousRunInBackground;
            Application.targetFrameRate = previousTargetFrameRate;
            DevCache.SetValue(null, previousDev);
        }

        [Serializable]
        public sealed class ProbeSnapshot
        {
            public int processId, frame, connectedCount, generation, round, historyCount, verticalLane, obstacleRunRound;
            public string role, status, error, scene, connection, gameState, campaignPhase, performanceId, runState;
            public float elapsed, timeRemaining, obstacleElapsed;
            public bool walking, inputBlocked, performanceOpen, performanceStarted, verticalActive;
            public long fundBalance;
            public PlayerSnapshot local;
            public List<PlayerSnapshot> players = new();
            public List<SegmentSnapshot> segments = new();
        }

        [Serializable]
        public sealed class PlayerSnapshot
        {
            public ulong ownerId;
            public int slot, lostParts, restartCount;
            public bool local, balanceActive, freeMovement, onRope, airborne, inputReaderEnabled, controllerEnabled;
            public float balanceValue, courseDistance;
            public Vector3 position;
            public string state, scheme;
            public static PlayerSnapshot From(NetworkPlayer player, TightropeCourse course)
            {
                var mover = player.GetComponent<PlayerMover>();
                var balance = player.GetComponent<PlayerBalance>();
                var controller = player.GetComponent<PlayerController>();
                var reader = player.GetComponent<PlayerInputReader>();
                var switcher = course != null ? course.GetComponent<CourseControlSwitcher>() : null;
                return new PlayerSnapshot
                {
                    ownerId = player.OwnerClientId, slot = player.Slot, local = player.IsLocal,
                    position = player.transform.position, state = player.State.ToString(), lostParts = (int)player.LostParts,
                    restartCount = ((NetworkVariable<int>)RestartCounter.GetValue(player)).Value,
                    courseDistance = course != null ? course.GetDistance(player.transform.position) : 0f,
                    balanceActive = balance != null && balance.IsActive, balanceValue = balance != null ? balance.Value : 0f,
                    freeMovement = mover != null && mover.FreeMovement,
                    onRope = player.IsLocal && switcher != null && switcher.IsLocalOnRope,
                    airborne = player.IsAirborne, inputReaderEnabled = reader != null && reader.enabled,
                    controllerEnabled = controller != null && controller.enabled,
                    scheme = controller != null && controller.Scheme != null ? controller.Scheme.GetType().Name : "none"
                };
            }
        }

        [Serializable]
        public sealed class SegmentSnapshot { public ulong ownerId; public bool dead, connected; public int lostCount; }
    }
}
#endif
