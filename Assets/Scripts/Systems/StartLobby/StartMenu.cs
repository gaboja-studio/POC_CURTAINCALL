using System.Threading.Tasks;
using CurtainCall.Network.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.StartLobby
{
    /// <summary>
    /// 시작 씬 메뉴(Task-052). 메인: 방 만들기 · 방 코드로 참가 · LAN · 설정 · 종료. 방을 열면 로비로 가고,
    /// 참가하면 호스트가 있는 로비로 NGO가 옮겨 준다. 화면은 <c>StartLobbySceneBuilder</c>가 만든다.
    /// </summary>
    public sealed class StartMenu : MonoBehaviour
    {
        public enum Page { Main, Join, Lan, Settings }

        [Header("페이지")]
        public GameObject mainPage;
        public GameObject joinPage;
        public GameObject lanPage;
        public SettingsPanel settingsPage;

        [Header("메인")]
        public Button host;
        public Button join;
        public Button lan;
        public Button settings;
        public Button quit;

        [Header("방 코드 참가")]
        public TMP_InputField code;
        public Button joinConfirm;
        public Button joinBack;

        [Header("LAN")]
        public TMP_InputField address;
        public Button lanHost;
        public Button lanJoin;
        public Button lanBack;

        [Header("상태")]
        public TMP_Text status;

        NetworkSessionManager session;
        StartLobbyFlow flow;
        bool busy;

        public Page Current { get; private set; }

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            flow = StartLobbyFlow.EnsureExists();
            session = NetworkSessionManager.EnsureExists();

            host.onClick.AddListener(() => Run(HostSession));
            join.onClick.AddListener(() => Show(Page.Join));
            lan.onClick.AddListener(() => Show(Page.Lan));
            settings.onClick.AddListener(() => Show(Page.Settings));
            quit.onClick.AddListener(Quit);
            joinConfirm.onClick.AddListener(() => Run(JoinSession));
            joinBack.onClick.AddListener(() => Show(Page.Main));
            lanHost.onClick.AddListener(() => Run(HostLan));
            lanJoin.onClick.AddListener(() => Run(JoinLan));
            lanBack.onClick.AddListener(() => Show(Page.Main));
            settingsPage.Closed += () => Show(Page.Main);

            Show(Page.Main);
            SetStatus(flow.TakeMessage());
        }

        public void Show(Page page)
        {
            Current = page;
            mainPage.SetActive(page == Page.Main);
            joinPage.SetActive(page == Page.Join);
            lanPage.SetActive(page == Page.Lan);
            settingsPage.gameObject.SetActive(page == Page.Settings);
            if (page == Page.Join) code.Select();
        }

        void Update()
        {
            bool idle = !busy && session != null && session.ConnectionState == SessionConnectionState.Offline;
            foreach (var button in new[] { host, join, lan, joinConfirm, lanHost, lanJoin })
                if (button != null) button.interactable = idle;
        }

        async void Run(System.Func<Task<bool>> action)
        {
            if (busy || session == null || session.ConnectionState != SessionConnectionState.Offline) return;
            busy = true;
            SetStatus("연결하는 중...");
            bool ok = false;
            try
            {
                ok = await action();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            busy = false;
            if (this == null) return; // 성공해서 씬이 바뀌었다.
            if (!ok) SetStatus(Failure());
        }

        async Task<bool> HostSession() => await session.HostSessionAsync() && flow.OpenLobbyAsHost();

        async Task<bool> HostLan() => await session.HostLanAsync() && flow.OpenLobbyAsHost();

        Task<bool> JoinSession()
        {
            string value = code.text.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(value))
            {
                SetStatus("방 코드를 입력하세요.");
                return Task.FromResult(true);
            }
            return session.JoinSessionAsync(value);
        }

        Task<bool> JoinLan() => session.JoinLanAsync(address.text.Trim());

        string Failure()
        {
            string reason = flow.TakeMessage();
            if (string.IsNullOrEmpty(reason)) reason = session.LastDisconnectReason;
            return string.IsNullOrEmpty(reason) ? "연결하지 못했습니다." : reason;
        }

        void SetStatus(string text)
        {
            if (status != null) status.text = text ?? "";
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
