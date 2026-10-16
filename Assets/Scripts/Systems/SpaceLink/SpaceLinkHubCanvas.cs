using System;
using System.Collections.Generic;
using CurtainCall.Campaign;
using CurtainCall.Network.Session;
using CurtainCall.Reward;
using CurtainCall.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.SpaceLink
{
    /// <summary>
    /// SpaceLink 시작 씬(도입·런 전·엔딩 때 머무는 곳)의 Canvas 화면. 왼쪽 패널 탭: 접속(방 만들기·참가·게임 시작), 흐름(오프라인 런·도입 끝).
    /// 공간 안의 신호(백스테이지로 나가기·탈출 개시)는 각 더미 공간 화면에 있다. 실제 로비 UI가 생기면 지운다.
    /// </summary>
    public sealed class SpaceLinkHubCanvas : MonoBehaviour
    {
        [Serializable]
        public sealed class ActionButton
        {
            public string id;
            public Button button;
        }

        [Header("HUD")]
        public TMP_Text place;
        public TMP_Text status;
        public TMP_Text funds;

        [Header("왼쪽 패널")]
        public RectTransform panel;
        public Button panelToggle;
        public TMP_Text panelToggleLabel;
        public Button[] tabButtons;
        public GameObject[] tabPages;
        public TMP_Text connection;
        public TMP_Text flow;
        public GameObject sessionGroup;
        public GameObject lanGroup;
        public TMP_InputField code;
        public TMP_InputField address;
        public ActionButton[] actions;

        static readonly Color TabOn = new(0.055f, 0.46f, 0.41f);
        static readonly Color TabOff = new(0.17f, 0.25f, 0.31f);

        readonly List<(Button button, UnityEngine.Events.UnityAction handler)> listeners = new();
        NetworkSessionManager session;
        CampaignRun campaign;
        bool useLan;
        bool panelOpen = true;
        float nextRefresh;

        public bool UseLan => useLan;

        void Start()
        {
            session = NetworkSessionManager.EnsureExists();
            campaign = CampaignRun.EnsureExists();
            foreach (var action in actions)
            {
                string id = action.id;
                Listen(action.button, () => Execute(id));
            }
            if (panelToggle != null) Listen(panelToggle, () => SetPanelOpen(!panelOpen));
            for (int i = 0; tabButtons != null && i < tabButtons.Length; i++)
            {
                int index = i;
                Listen(tabButtons[i], () => ShowTab(index));
            }
            ShowTab(0);
            SetPanelOpen(true);
            Refresh();
        }

        void Listen(Button button, UnityEngine.Events.UnityAction handler)
        {
            if (button == null) return;
            button.onClick.AddListener(handler);
            listeners.Add((button, handler));
        }

        void OnDestroy()
        {
            foreach (var item in listeners)
                if (item.button != null) item.button.onClick.RemoveListener(item.handler);
            listeners.Clear();
        }

        void Update()
        {
            if (session == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.15f;
            Refresh();
        }

        public void SetPanelOpen(bool open)
        {
            panelOpen = open;
            if (panel != null) panel.anchoredPosition = new Vector2(open ? 0 : -panel.sizeDelta.x, panel.anchoredPosition.y);
            if (panelToggleLabel != null) panelToggleLabel.text = open ? "◀\n접기" : "▶\n조작";
            FrameCamera();
        }

        /// <summary>패널이 가린 만큼 카메라 화면 중심을 오른쪽으로 옮긴다(물리 카메라 lens shift).</summary>
        void FrameCamera()
        {
            var cam = Camera.main;
            var root = GetComponent<Canvas>();
            if (cam == null || !cam.usePhysicalProperties || panel == null || root == null) return;
            float covered = panelOpen ? panel.rect.width * root.scaleFactor / Mathf.Max(1, Screen.width) : 0;
            cam.lensShift = new Vector2(-covered * 0.5f * cam.sensorSize.y * cam.aspect / cam.sensorSize.x, 0);
        }

        public void ShowTab(int index)
        {
            if (tabPages == null || tabPages.Length == 0) return;
            index = Mathf.Clamp(index, 0, tabPages.Length - 1);
            for (int i = 0; i < tabPages.Length; i++)
            {
                if (tabPages[i] != null) tabPages[i].SetActive(i == index);
                if (tabButtons != null && i < tabButtons.Length && tabButtons[i] != null) tabButtons[i].image.color = i == index ? TabOn : TabOff;
            }
            if (!panelOpen) SetPanelOpen(true);
        }

        bool Offline => session.ConnectionState == SessionConnectionState.Offline;
        bool Connected => session.ConnectionState == SessionConnectionState.Host || session.ConnectionState == SessionConnectionState.Client;

        public bool CanExecute(string id)
        {
            if (session == null || campaign == null) return false;
            var phase = campaign.State.Phase;
            switch (id)
            {
                case "modeSession": case "modeLan": return Offline;
                case "host": case "join": return Offline && !useLan;
                case "lanHost": case "lanJoin": return Offline && useLan;
                case "copy": return Connected && session.ActiveConnector is ServicesSessionConnector;
                case "startGame": return session.CanStartGame;
                case "endGame": return session.IsHost && session.GameState == GameSessionState.Playing;
                case "leave": return Connected;
                case "offlineStart": return Offline && phase == CampaignPhase.None;
                case "intro": return campaign.HasAuthority && phase == CampaignPhase.Intro;
                case "offlineEnd": return campaign.IsOfflineRun && phase == CampaignPhase.Ending;
                default: return false;
            }
        }

        public void Execute(string id)
        {
            if (!CanExecute(id)) return;
            switch (id)
            {
                case "modeSession": useLan = false; break;
                case "modeLan": useLan = true; break;
                case "host": _ = session.HostSessionAsync(); break;
                case "join": _ = session.JoinSessionAsync(code != null ? code.text.Trim() : ""); break;
                case "lanHost": _ = session.HostLanAsync(); break;
                case "lanJoin": _ = session.JoinLanAsync(address != null ? address.text.Trim() : ""); break;
                case "copy": GUIUtility.systemCopyBuffer = session.JoinKey; break;
                case "startGame": session.StartGame(); break;
                case "endGame": session.EndGame(); break;
                case "leave": _ = session.LeaveAsync(); break;
                case "offlineStart": campaign.StartOfflineRun(); break;
                case "intro": campaign.EndIntro(); break;
                case "offlineEnd": campaign.StopOfflineRun(); break;
            }
            Refresh();
        }

        public void Refresh()
        {
            if (session == null || campaign == null) return;
            var s = campaign.State;
            var director = SpaceLinkDirector.Instance;
            if (place != null) place.text = $"시작 로비   <size=70%><color=#9FB8BF>더미 · 장소 전환 [{SpaceRoute.Describe(GameSettings.Base.SpaceTransition.Mode)}]{(director != null && director.IsTransitioning ? " 전환 중" : "")}</color></size>";
            if (status != null) status.text = s.Phase == CampaignPhase.None ? "런 시작 전" : $"세대 {s.Generation}   ·   {s.Day}일차   ·   {CampaignRun.Describe(s)}";
            if (funds != null) funds.text = $"공동 자금  <b>{(PerformanceRewards.Instance != null ? PerformanceRewards.Instance.Balance : 0)}</b>";
            if (sessionGroup != null) sessionGroup.SetActive(Offline && !useLan);
            if (lanGroup != null) lanGroup.SetActive(Offline && useLan);
            if (connection != null) connection.text = ConnectionText();
            if (flow != null) flow.text = FlowText(s);
            foreach (var item in actions)
                if (item.button != null) item.button.interactable = CanExecute(item.id);
            FrameCamera();
        }

        string ConnectionText()
        {
            switch (session.ConnectionState)
            {
                case SessionConnectionState.Offline:
                    string ended = session.GameState == GameSessionState.Ended ? "게임 종료됨\n" : "";
                    string last = string.IsNullOrEmpty(session.LastDisconnectReason) ? "" : $"\n마지막 종료: {session.LastDisconnectReason}";
                    return $"{ended}접속 안 함 · {(useLan ? "LAN(같은 PC는 127.0.0.1:7777)" : "방 코드(세션)")}{last}";
                case SessionConnectionState.Connecting:
                    return "연결 중...";
                default:
                    bool isSession = session.ActiveConnector is ServicesSessionConnector;
                    string state = session.GameState == GameSessionState.Waiting ? "대기" : session.GameState == GameSessionState.Playing ? "진행" : "종료";
                    return $"{(session.IsHost ? "방장(호스트)" : "참가자")} · {(isSession ? "방 코드" : "주소")} {session.JoinKey}\n게임 {state} · 인원 {session.PlayerCount} / {session.RequiredPlayers} (최소 {session.MinPlayers})";
            }
        }

        static string FlowText(CampaignSnapshot s) => s.Phase switch
        {
            CampaignPhase.None => "런 시작 전. 접속했다면 방장이 [접속] 탭의 '게임 시작', 혼자면 '오프라인 런 시작'.",
            CampaignPhase.Intro => "도입. 방장이 '도입 끝 → 공연'을 누르면 공연장으로 이동합니다.",
            CampaignPhase.Ending => "탈출 성공 · 엔딩. 오프라인 런이면 '오프라인 런 끝'으로 처음으로.",
            _ => $"{CampaignRun.Describe(s)} · 더미 공간에서 진행 중입니다."
        };
    }
}
