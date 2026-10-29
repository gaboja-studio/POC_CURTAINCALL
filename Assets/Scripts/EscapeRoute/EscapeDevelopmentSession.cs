using System;
using System.Collections;
using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute
{
    /// <summary>A/B/C 공통 개발 실행. Play 즉시 원본 플레이어 생성, 실패 후 복구, 공연/로비 진행 없음.</summary>
    [DisallowMultipleComponent]
    public sealed class EscapeDevelopmentSession : MonoBehaviour
    {
        public const float RecoverySeconds = 1f;
        [Tooltip("이 개발 씬에서만 추락 부상·낙사·균형 붕괴를 임시 해제한다. 톱·화염 등 직접 피해는 유지한다.")]
        [SerializeField] bool suppressFallConsequences;
        static readonly HashSet<EscapeDevelopmentSession> activeSessions = new();

        public bool SuppressFallConsequences
        {
            get => suppressFallConsequences;
            set => suppressFallConsequences = value;
        }

        /// <summary>옵션을 켠 개발 씬의 추락 경로만 차단한다. 직접 위험물 피해의 상태 가드로 사용하지 않는다.</summary>
        public static bool FallConsequencesSuppressed(Component source)
        {
            if (source == null) return false;
            var scene = source.gameObject.scene;
            // NGO 영속 플레이어도 현재 플레이 중인 개발 씬의 명시적 옵션을 따른다.
            if (scene.name == "DontDestroyOnLoad") scene = SceneManager.GetActiveScene();
            foreach (var candidate in activeSessions)
                if (candidate != null && candidate.isActiveAndEnabled && candidate.suppressFallConsequences
                    && candidate.gameObject.scene == scene) return true;
            return false;
        }

        void OnEnable() => activeSessions.Add(this);
        NetworkSessionManager session;
        EscapeLocalPracticeConnector connector;
        IDisposable minimumPlayers;
        double recoverAt = -1;
        public bool Ready { get; private set; }
        public int RecoveryCount { get; private set; }
        public string Status { get; private set; } = "혼자 테스트 준비 중…";
        public float RecoveryRemaining => recoverAt < 0 ? 0 : Mathf.Max(0, (float)(recoverAt - Time.unscaledTimeAsDouble));

        IEnumerator Start()
        {
            session = NetworkSessionManager.EnsureExists();
            if (session == null) { Status = "플레이어 실행 설정을 찾지 못했습니다. Console을 확인해 주세요."; yield break; }
            if (session.ConnectionState != SessionConnectionState.Offline)
            {
                // 나중에 다인 개발 연결에서 들어오면 기존 원본 세션을 그대로 사용한다.
                Ready = session.ConnectionState == SessionConnectionState.Host || session.ConnectionState == SessionConnectionState.Client;
                Status = Ready ? "개발 테스트" : "접속 준비 중";
                yield break;
            }
            minimumPlayers = session.RequestMinimumStartPlayers(1);
            connector = new EscapeLocalPracticeConnector(session.NetworkManager);
            var start = session.HostAsync(connector);
            while (!start.IsCompleted) yield return null;
            if (start.IsFaulted || start.IsCanceled || !start.Result)
            { Status = "혼자 테스트 시작에 실패했습니다. Console을 확인해 주세요."; yield break; }
            // 원본 플레이어와 각 묘기 시스템의 Start/OnNetworkSpawn이 끝난 뒤 시작한다.
            yield return null;
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while ((NetworkPlayer.Local == null || !NetworkPlayer.Local.IsSpawned || !session.CanStartGame)
                && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (NetworkPlayer.Local == null || !session.CanStartGame || !session.StartGame())
            { Status = "플레이어 준비가 완료되지 않았습니다. Play를 다시 실행해 주세요."; yield break; }
            Ready = true;
            Status = "혼자 테스트 · 시간제한/게임 오버 없음";
        }

        void Update()
        {
            if (!Ready || session == null || !session.IsHost || session.ActiveConnector != connector) return;
            // 원본 기물이 종료를 요청하더라도 개발씬에서는 같은 플레이어로 계속 검토한다.
            if (session.GameState == GameSessionState.Ended && session.RestartEndedGame()) Restart();
            var player = NetworkPlayer.Local;
            bool failed = player != null && player.IsSpawned
                && (player.State == PlayerState.Fallen || player.State == PlayerState.Retired);
            if (!failed) { recoverAt = -1; return; }
            if (recoverAt < 0) recoverAt = Time.unscaledTimeAsDouble + RecoverySeconds;
            if (Time.unscaledTimeAsDouble >= recoverAt) Restart();
        }

        /// <summary>묘기의 점유/신체/균형/카메라 복구는 공용 Restarted 경계를 그대로 쓴다.</summary>
        public bool Restart()
        {
            if (!Ready || session == null || !session.IsHost || session.ActiveConnector != connector
                || NetworkPlayer.Local == null || !NetworkPlayer.Local.IsSpawned) return false;
            NetworkPlayer.ServerRestoreEveryone();
            NetworkPlayer.ServerRestartAll();
            recoverAt = -1;
            RecoveryCount++;
            return true;
        }

        void OnDisable()
        {
            activeSessions.Remove(this);
            StopAllCoroutines();
            Ready = false;
            recoverAt = -1;
            minimumPlayers?.Dispose(); minimumPlayers = null;
            if (session != null && connector != null && session.ActiveConnector == connector)
                _ = session.LeaveAsync();
        }
    }
}
