using System;
using System.Collections;
using System.Diagnostics;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.HumanCannon
{
    /// <summary>명시적 실행 인자가 있는 별도 프로세스에서만 실제 두 번째 플레이어를 조작한다.</summary>
    [DefaultExecutionOrder(-45)]
    public sealed class HumanCannonBotWorker : MonoBehaviour
    {
        internal static bool IsWorkerProcess { get; private set; }
        HumanCannonBotBrain brain;
        NetworkSessionManager session;
        CustomMessagingManager messaging;
        NetworkPlayer player;
        PlayerMover mover;
        HumanCannonPilot pilot;
        CannonBotRole role;
        bool assigned;
        float nextStatus, nextParentCheck, idleUntil;
        int parentId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), HumanCannonPracticeBot.WorkerFlag) < 0) return;
            IsWorkerProcess = true;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            new GameObject("HumanCannonPracticeBotWorker").AddComponent<HumanCannonBotWorker>();
        }

        IEnumerator Start()
        {
            ushort port = ushort.TryParse(Argument("-botPort"), out var parsed) ? parsed : (ushort)7777;
            int.TryParse(Argument("-botParent"), out parentId);
            session = NetworkSessionManager.EnsureExists();
            var join = session.JoinLanAsync("127.0.0.1", port);
            yield return new WaitUntil(() => join.IsCompleted);
            if (join.IsFaulted || !join.Result) { Application.Quit(2); yield break; }
            float deadline = Time.realtimeSinceStartup + 30;
            while (NetworkPlayer.Local == null && Time.realtimeSinceStartup < deadline) yield return null;
            player = NetworkPlayer.Local;
            if (player == null) { Application.Quit(3); yield break; }
            while ((pilot = player.GetComponent<HumanCannonPilot>()) == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (pilot == null) { Application.Quit(4); yield break; }
            mover = player.GetComponent<PlayerMover>();
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.GetComponent<PlayerController>().enabled = false;
            // 작은 이동이 높은 프레임에서 반올림되지 않게 봇의 런타임 몸통에만 적용한다.
            mover.Body.minMoveDistance = 0;
            messaging = session.NetworkManager.CustomMessagingManager;
            messaging.RegisterNamedMessageHandler(HumanCannonPracticeBot.ControlMessage, ReceiveControl);
            NetworkPlayer.Restarted += Restarted;
            brain = new HumanCannonBotBrain(player, pilot, mover);
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
            if (command == 255) { assigned = false; StartCoroutine(Leave()); return; }
            if (command > 1) return;
            role = (CannonBotRole)command;
            assigned = true;
            brain?.Reset();
            idleUntil = Time.time + .6f;
        }

        IEnumerator Leave()
        {
            var leaving = session.LeaveAsync();
            yield return new WaitUntil(() => leaving.IsCompleted);
            Application.Quit();
        }

        void Restarted(NetworkPlayer target)
        {
            if (target != player) return;
            brain?.Reset();
            idleUntil = Time.time + .6f;
        }

        void Update()
        {
            if (Time.realtimeSinceStartup >= nextParentCheck)
            {
                nextParentCheck = Time.realtimeSinceStartup + 2;
                try { if (parentId > 0) { using var parent = Process.GetProcessById(parentId); if (parent.HasExited) Application.Quit(); } }
                catch (ArgumentException) { Application.Quit(); }
            }
            if (brain == null) return;
            if (session.ConnectionState != SessionConnectionState.Client) { Application.Quit(); return; }
            if (assigned && Time.time >= idleUntil && !InputGate.IsBlocked) brain.Tick(role);
            if (Time.realtimeSinceStartup >= nextStatus)
            {
                nextStatus = Time.realtimeSinceStartup + .5f;
                SendStatus(assigned ? brain.Status : "접속 완료 · 역할 대기");
            }
        }

        void SendStatus(string value)
        {
            using var writer = new FastBufferWriter(132, Allocator.Temp);
            writer.WriteValueSafe(new FixedString128Bytes(value));
            messaging.SendNamedMessage(HumanCannonPracticeBot.StatusMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        void OnDestroy()
        {
            NetworkPlayer.Restarted -= Restarted;
            messaging?.UnregisterNamedMessageHandler(HumanCannonPracticeBot.ControlMessage);
        }
    }
}
