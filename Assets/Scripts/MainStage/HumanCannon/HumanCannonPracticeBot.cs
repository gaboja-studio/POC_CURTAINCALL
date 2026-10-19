using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    public enum CannonBotRole : byte { Carrier, Flyer }

    /// <summary>인간대포 테스트 씬에서만 쓰는 로컬 LAN 봇 한 명의 실행·역할 제어.</summary>
    public sealed class HumanCannonPracticeBot : MonoBehaviour
    {
        public const string StatusMessage = "CurtainCall.CannonBot.Status";
        public const string ControlMessage = "CurtainCall.CannonBot.Control";
        public const string WorkerFlag = "-humanCannonPracticeBot";
        public static string ExecutablePath => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../.harness/local/human-cannon-bot/Player/HumanCannonBot.exe"));
        public CannonBotRole Role { get; private set; }
        public ulong BotId { get; private set; } = CannonSeatState.Nobody;
        public string Status { get; private set; } = "봇을 추가하면 혼자 협동 연습을 할 수 있습니다.";
        public bool Running => process != null && !process.HasExited;
        Process process;
        CustomMessagingManager messaging;
        float launchedAt, lastStatus;
        bool starting;

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
            if (!Running)
            {
                Status = "봇이 종료되었습니다. 다시 추가할 수 있습니다.";
                DisposeProcess();
            }
            else if (session == null || !session.IsHost) StopBot();
            else if (Time.realtimeSinceStartup - (BotId == CannonSeatState.Nobody ? launchedAt : lastStatus) > 45f)
            {
                StopBot();
                Status = "봇 연결 시간이 초과되었습니다. 봇 실행 로그를 확인해 주세요.";
            }
        }

        public async void StartBot()
        {
            if (!Application.isEditor || Running || starting) return;
            if (!File.Exists(ExecutablePath)) { Status = "봇 실행파일을 먼저 준비해야 합니다."; return; }
            starting = true;
            try
            {
                var session = NetworkSessionManager.EnsureExists();
                if (session.ConnectionState == SessionConnectionState.Offline && !await session.HostLanAsync())
                { Status = "로컬 연습방을 열지 못했습니다."; return; }
                if (!session.IsHost || session.GameState != GameSessionState.Waiting || session.PlayerCount != 1
                    || session.ActiveConnector is not LanSessionConnector)
                { Status = "혼자 있는 LAN 대기방에서 봇을 추가해 주세요."; return; }
                ushort port = LanSessionConnector.DefaultPort;
                LanSessionConnector.TryParseJoinKey(session.JoinKey, port, out _, out port);
                string log = Path.Combine(Path.GetDirectoryName(ExecutablePath), "../bot-player.log");
                var info = new ProcessStartInfo(ExecutablePath)
                {
                    Arguments = $"-batchmode -nographics {WorkerFlag} -botPort {port} -botParent {Process.GetCurrentProcess().Id} -logFile \"{log}\"",
                    WorkingDirectory = Path.GetDirectoryName(ExecutablePath),
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
                };
                process = Process.Start(info);
                launchedAt = Time.realtimeSinceStartup;
                Status = "봇 접속 중…";
            }
            catch (Exception exception) { Status = "봇 실행 실패: " + exception.Message; }
            finally { starting = false; }
        }

        public void SetRole(CannonBotRole next)
        {
            if (!Enum.IsDefined(typeof(CannonBotRole), next) || Role == next) return;
            Role = next;
            if (BotId == CannonSeatState.Nobody) return;
            // 날아가거나 들고 있는 중에 역할을 바꾸어 소유권을 남기지 않는다.
            NetworkPlayer.Local?.RequestRestartAll();
            SendControl((byte)Role);
            Status = "역할 교대 · 출발점에서 다시 준비 중";
        }

        void ReceiveStatus(ulong sender, FastBufferReader reader)
        {
            if (!Running || sender == NetworkManager.ServerClientId || reader.Length - reader.Position > 132
                || (BotId != CannonSeatState.Nobody && sender != BotId)) return;
            reader.ReadValueSafe(out FixedString128Bytes status);
            bool first = BotId == CannonSeatState.Nobody;
            BotId = sender;
            lastStatus = Time.realtimeSinceStartup;
            Status = status.ToString();
            if (first) SendControl((byte)Role);
        }

        void SendControl(byte command)
        {
            if (messaging == null || BotId == CannonSeatState.Nobody) return;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteValueSafe(command);
            messaging.SendNamedMessage(ControlMessage, BotId, writer, NetworkDelivery.ReliableSequenced);
        }

        public void StopBot()
        {
            // 먼저 정상 LAN 종료를 요청한다. 응답 없는 경우에도 소유한 자식만 정리한다.
            SendControl(255);
            DisconnectBot();
            var child = process;
            process = null;
            BotId = CannonSeatState.Nobody;
            if (child != null) _ = Task.Run(() =>
            {
                try { if (!child.WaitForExit(2500)) child.Kill(); }
                catch (InvalidOperationException) { }
                finally { child.Dispose(); }
            });
            Status = "봇을 내보냈습니다.";
        }

        void DisposeProcess()
        {
            DisconnectBot();
            process?.Dispose(); process = null;
            BotId = CannonSeatState.Nobody;
        }

        void DisconnectBot()
        {
            var session = NetworkSessionManager.Instance;
            if (session != null && session.IsHost && BotId != CannonSeatState.Nobody
                && session.NetworkManager.ConnectedClients.ContainsKey(BotId))
                session.NetworkManager.DisconnectClient(BotId);
        }

        void OnGUI()
        {
            if (!Application.isEditor) return;
            GUILayout.BeginArea(new Rect(Mathf.Max(190, Screen.width * .5f - 180), 10, 360, 156), GUI.skin.box);
            GUILayout.Label("인간대포 · 연습 봇 1명");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(Role == CannonBotRole.Carrier, "봇: 트램폴린 운반", GUI.skin.button)) SetRole(CannonBotRole.Carrier);
            if (GUILayout.Toggle(Role == CannonBotRole.Flyer, "봇: 공중 이동", GUI.skin.button)) SetRole(CannonBotRole.Flyer);
            GUILayout.EndHorizontal();
            GUILayout.Label("역할 교대 시 전체 재시작 · 사망하면 R");
            GUILayout.Label(Status);
            GUI.enabled = !starting;
            if (GUILayout.Button(Running ? "봇 내보내기" : "봇 추가 · 로컬 연습"))
            { if (Running) StopBot(); else StartBot(); }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        void OnDestroy()
        {
            messaging?.UnregisterNamedMessageHandler(StatusMessage);
            StopBot();
        }
    }
}
