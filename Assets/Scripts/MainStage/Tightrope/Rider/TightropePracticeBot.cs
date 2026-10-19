using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    public enum TightropeBotRole : byte { Follow, Upper, Lower }

    /// <summary>독립 외줄 연습방에서만 실제 LAN 동료 한 명을 실행한다. 사람의 입력이나 정원은 변경하지 않는다.</summary>
    public sealed class TightropePracticeBot : MonoBehaviour
    {
        public const string WorkerFlag = "-tightropePracticeBot";
        public const string StatusMessage = "CurtainCall.TightropeBot.Status";
        public const string ControlMessage = "CurtainCall.TightropeBot.Control";
        public const ulong Nobody = ulong.MaxValue;
        public static string ExecutablePath => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../.harness/local/tightrope-bot/Player/TightropeBot.exe"));

        public TightropeBotRole Role { get; private set; }
        public ulong BotId { get; private set; } = Nobody;
        public string Status { get; private set; } = "혼자 있는 대기방에서 봇을 추가하세요.";
        public bool Running => process != null && !process.HasExited;
        public bool Ready => Running && BotId != Nobody;
        public bool Busy => starting || (cleanup != null && !cleanup.IsCompleted);

        Process process;
        Task cleanup;
        CustomMessagingManager messaging;
        TightropeBotRole? pendingRole;
        string launchToken;
        float launchedAt, lastStatus;
        bool starting;
        int generation;

        void Update()
        {
            var session = NetworkSessionManager.Instance;
            var current = session != null && session.IsHost ? session.NetworkManager.CustomMessagingManager : null;
            if (messaging != current)
            {
                messaging?.UnregisterNamedMessageHandler(StatusMessage);
                messaging = current;
                messaging?.RegisterNamedMessageHandler(StatusMessage, ReceiveStatus);
            }
            if (process == null) return;
            if (!Running) { StopBot(); Status = "봇이 종료되었습니다. 다시 추가할 수 있습니다."; return; }
            if (session == null || !session.IsHost || !IsPracticeCourse()) { StopBot(); return; }
            // 다른 사람이 들어오면 자기 자식 프로세스만 종료해 그 사람의 자리를 보호한다.
            if (session.PlayerCount > 2) { StopBot(); Status = "다른 사람이 접속해 연습 봇을 내보냈습니다."; return; }
            if (Time.realtimeSinceStartup - (BotId == Nobody ? launchedAt : lastStatus) > 45f)
            { StopBot(); Status = "봇 연결이 끊겼습니다. 다시 추가해 주세요."; return; }
            ApplyPendingRole();
        }

        static bool IsPracticeCourse() => TightropeCourse.Current != null
            && TightropeCourse.Current.isActiveAndEnabled
            && (TightropeRun.Current == null || !TightropeRun.Current.ExternallyManaged);

        public bool CanStartBot(out string reason)
        {
            reason = "";
            if (!Application.isEditor || !Application.isPlaying || !isActiveAndEnabled || !IsPracticeCourse())
                reason = "독립 외줄 연습 씬에서만 사용할 수 있습니다.";
            else if (Running || Busy) reason = "봇을 준비하거나 정리하고 있습니다.";
            else
            {
                var session = NetworkSessionManager.Instance;
                if (session != null && session.ConnectionState != SessionConnectionState.Offline
                    && (!session.IsHost || session.ActiveConnector is not LanSessionConnector
                        || session.GameState != GameSessionState.Waiting || session.PlayerCount != 1))
                    reason = "혼자 있는 LAN 대기방에서 봇을 추가해 주세요.";
                else if (session != null && session.RequiredPlayers < 2)
                    reason = "이 방에는 두 번째 플레이어가 들어갈 자리가 없습니다.";
            }
            return reason.Length == 0;
        }

        public async Task<bool> StartBotAsync()
        {
            if (!CanStartBot(out var reason)) { Status = reason; return false; }
            if (!File.Exists(ExecutablePath)) { Status = "연습 봇 실행파일을 먼저 준비해야 합니다."; return false; }
            starting = true;
            int request = ++generation;
            try
            {
                var session = NetworkSessionManager.EnsureExists();
                if (session == null) { Status = "연습방을 준비하지 못했습니다."; return false; }
                if (session.ConnectionState == SessionConnectionState.Offline && !await session.HostLanAsync())
                { Status = "로컬 연습방을 열지 못했습니다."; return false; }
                if (this == null || !isActiveAndEnabled || generation != request) return false;
                if (!session.IsHost || session.ActiveConnector is not LanSessionConnector
                    || session.GameState != GameSessionState.Waiting || session.PlayerCount != 1 || session.RequiredPlayers < 2)
                { Status = "혼자 있는 LAN 대기방에서 봇을 추가해 주세요."; return false; }
                ushort port = LanSessionConnector.DefaultPort;
                LanSessionConnector.TryParseJoinKey(session.JoinKey, port, out _, out port);
                launchToken = Guid.NewGuid().ToString("N");
                string log = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ExecutablePath), "../bot-player.log"));
                process = Process.Start(new ProcessStartInfo(ExecutablePath)
                {
                    Arguments = $"-batchmode -nographics {WorkerFlag} -botPort {port} -botParent {Process.GetCurrentProcess().Id} -botToken {launchToken} -logFile \"{log}\"",
                    WorkingDirectory = Path.GetDirectoryName(ExecutablePath),
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
                });
                if (process == null) { Status = "봇을 시작하지 못했습니다."; return false; }
                launchedAt = Time.realtimeSinceStartup;
                Status = "봇 접속 중…";
                return true;
            }
            catch (Exception exception) { Status = "봇 실행 실패: " + exception.Message; return false; }
            finally { if (this != null && generation == request) starting = false; }
        }

        public void SetRole(TightropeBotRole next)
        {
            if (!Enum.IsDefined(typeof(TightropeBotRole), next) || (Role == next && !pendingRole.HasValue)) return;
            pendingRole = next;
            ApplyPendingRole();
        }

        void ApplyPendingRole()
        {
            if (!pendingRole.HasValue) return;
            var local = NetworkPlayer.Local;
            NetworkPlayer bot = null;
            foreach (var other in NetworkPlayer.All) if (other != null && other.OwnerClientId == BotId) { bot = other; break; }
            if (Ready && (local == null || bot == null || local.IsAirborne || bot.IsAirborne))
            { Status = "착지한 뒤 역할을 바꿉니다."; return; }
            var stack = PiggybackSystem.Current;
            if (Ready && stack != null && stack.IsSameStack(local, bot) && stack.GetBelow(local) != null)
            {
                // 역할 버튼은 내 F 길게와 같은 하차 요청만 보낸다. 이동·균형 입력은 건드리지 않는다.
                stack.RequestLocalRelease();
                Status = "목마에서 내려온 뒤 역할을 바꿉니다.";
                return;
            }
            Role = pendingRole.Value;
            pendingRole = null;
            SendControl((byte)Role);
            Status = "역할 변경 · 동료와 다시 준비 중";
        }

        void ReceiveStatus(ulong sender, FastBufferReader reader)
        {
            if (!Running || sender == NetworkManager.ServerClientId || reader.Length - reader.Position > 256
                || (BotId != Nobody && sender != BotId)) return;
            try
            {
                reader.ReadValueSafe(out FixedString64Bytes token);
                reader.ReadValueSafe(out FixedString128Bytes status);
                if (token.ToString() != launchToken) return;
                bool first = BotId == Nobody;
                BotId = sender;
                lastStatus = Time.realtimeSinceStartup;
                Status = status.ToString();
                if (first) SendControl((byte)Role);
            }
            catch (OverflowException) { }
        }

        void SendControl(byte command)
        {
            if (messaging == null || BotId == Nobody) return;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteValueSafe(command);
            messaging.SendNamedMessage(ControlMessage, BotId, writer, NetworkDelivery.ReliableSequenced);
        }

        public void StopBot()
        {
            generation++;
            starting = false;
            pendingRole = null;
            SendControl(255);
            var session = NetworkSessionManager.Instance;
            if (session != null && session.IsHost && BotId != Nobody
                && session.NetworkManager.ConnectedClients.ContainsKey(BotId))
                session.NetworkManager.DisconnectClient(BotId);
            var child = process;
            process = null;
            BotId = Nobody;
            launchToken = null;
            if (child != null) cleanup = Task.Run(() =>
            {
                try { if (!child.HasExited && !child.WaitForExit(2500)) child.Kill(); }
                catch (InvalidOperationException) { }
                finally { child.Dispose(); }
            });
            Status = "봇을 내보냈습니다.";
        }

        void OnDisable()
        {
            StopBot();
            messaging?.UnregisterNamedMessageHandler(StatusMessage);
            messaging = null;
        }

        void OnGUI()
        {
            if (!Application.isEditor || !IsPracticeCourse()) return;
            GUILayout.BeginArea(new Rect(Mathf.Max(200f, Screen.width * .5f - 190f), 10f, 380f, 176f), GUI.skin.box);
            GUILayout.Label("외줄타기 · 연습 봇 1명");
            GUILayout.BeginHorizontal();
            foreach (TightropeBotRole role in Enum.GetValues(typeof(TightropeBotRole)))
                if (GUILayout.Toggle(Role == role, role switch
                    { TightropeBotRole.Upper => "봇이 위", TightropeBotRole.Lower => "봇이 아래", _ => "따라오기" }, GUI.skin.button)) SetRole(role);
            GUILayout.EndHorizontal();
            GUILayout.Label(Role switch
            {
                TightropeBotRole.Lower => "F로 봇 위에 타면 봇이 전진합니다.",
                TightropeBotRole.Upper => "봇이 위에 타면 내가 W/S로 이동합니다.",
                _ => "동료를 따라오고 불 예고 때 옆줄로 피합니다."
            });
            GUILayout.Label("역할 변경: 착지·하차 후 적용 · 처음부터: R");
            GUILayout.Label(Status);
            bool enabledBefore = GUI.enabled;
            GUI.enabled &= !Busy;
            if (GUILayout.Button(Running ? "봇 내보내기" : "봇 추가 · 로컬 연습"))
            { if (Running) StopBot(); else _ = StartBotAsync(); }
            GUI.enabled = enabledBefore;
            GUILayout.EndArea();
        }
    }
}
