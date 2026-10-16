using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Network.Session;
using CurtainCall.Player;
using CurtainCall.SpaceLink;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CurtainCall.StartLobby
{
    /// <summary>
    /// 로비 씬(Task-052) 진행. 런 전 대기·도입·엔딩 때 머무는 곳으로, 기존 SpaceLink 시작 씬의 역할을 대신한다(장소 전환 세팅의 시작 씬).
    /// - 내 캐릭터를 자리 번호의 출발 위치에 세우고, 가까운 로비 물건(<see cref="LobbyInteractable"/>)의 F 안내를 띄운다.
    /// - 게임이 시작되면 무대 커튼을 열고, 호스트가 잠시 뒤 도입을 끝내 첫 공연으로 보낸다(도입 컷씬 자리).
    /// - ESC 메뉴: 계속하기 · 설정 · 방 나가기.
    /// 추가 로드로 공간 씬이 붙어 있는 동안에는(<see cref="SpaceLinkHub"/>가 화면을 끈다) 아무것도 하지 않는다.
    /// </summary>
    public sealed class LobbyRoom : MonoBehaviour
    {
        [Header("HUD")]
        public TMP_Text roomCode;
        public TMP_Text players;
        public Button copyCode;
        public TMP_Text prompt;
        public TMP_Text banner;
        public TMP_Text bannerSub;
        [Tooltip("알림 글자 뒤 어두운 띠(알림이 있을 때만 보인다).")]
        public GameObject bannerBack;

        [Header("ESC 메뉴")]
        public GameObject menu;
        public Button resume;
        public Button openSettings;
        public Button leave;
        public TMP_Text leaveLabel;
        public SettingsPanel settings;

        [Header("로비 물건")]
        public LobbyInteractable[] interactables = new LobbyInteractable[0];

        [Header("무대 커튼")]
        public Transform curtainLeft;
        public Transform curtainRight;
        [Min(0f)] public float curtainTravel = 2.6f;

        [Tooltip("게임 시작 뒤 첫 공연으로 넘어가기 전 커튼이 열리는 시간(초, 도입 컷씬 자리).")]
        [Min(0f)] public float introSeconds = 3f;

        static readonly Color Usable = new(0.98f, 0.93f, 0.82f);
        static readonly Color Blocked = new(0.62f, 0.56f, 0.5f);

        NetworkSessionManager session;
        CampaignRun campaign;
        StartLobbyFlow flow;
        Vector3 leftHome;
        Vector3 rightHome;
        float curtain;
        float introTimer;
        bool introEnded;
        bool shown = true;
        bool menuOpen;
        bool placePending;

        void Start()
        {
            flow = StartLobbyFlow.EnsureExists();
            session = NetworkSessionManager.EnsureExists();
            campaign = CampaignRun.EnsureExists();
            if (curtainLeft != null) leftHome = curtainLeft.localPosition;
            if (curtainRight != null) rightHome = curtainRight.localPosition;

            if (copyCode != null) copyCode.onClick.AddListener(CopyCode);
            if (resume != null) resume.onClick.AddListener(() => SetMenu(false));
            if (openSettings != null) openSettings.onClick.AddListener(() => settings.gameObject.SetActive(true));
            if (leave != null) leave.onClick.AddListener(() => flow.LeaveToStart());
            if (settings != null) settings.gameObject.SetActive(false);
            SetMenu(false);

            NetworkPlayer.LocalSpawned += Place;
            if (NetworkPlayer.Local != null) Place(NetworkPlayer.Local);
            ShowCursor();

            // 에디터에서 로비 씬을 바로 열면 접속 전이므로 시작 씬으로 보낸다.
            if (session == null || session.ConnectionState == SessionConnectionState.Offline) flow.LeaveToStart();
        }

        void OnDestroy()
        {
            NetworkPlayer.LocalSpawned -= Place;
            InputGate.SetBlocking(this, false);
        }

        void Update()
        {
            var director = SpaceLinkDirector.Instance;
            bool visible = SceneManager.sceneCount == 1 && (director == null || !director.IsTransitioning);
            if (visible != shown)
            {
                shown = visible;
                if (!shown) SetMenu(false);
                else
                {
                    placePending = true; // SpaceLinkHub가 출발 위치(로비 환경)를 다시 켠 뒤 LateUpdate에서
                    ShowCursor();
                }
            }
            if (!shown || session == null || campaign == null) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (settings != null && settings.gameObject.activeSelf) settings.Close();
                else SetMenu(!menuOpen);
            }

            RefreshHud();
            UpdateInteraction();
            UpdateIntro();
            UpdateCurtain();
        }

        void LateUpdate()
        {
            if (!placePending) return;
            placePending = false;
            if (NetworkPlayer.Local != null) Place(NetworkPlayer.Local);
        }

        // ── 로비 물건 ────────────────────────────────────────

        void UpdateInteraction()
        {
            var local = NetworkPlayer.Local;
            LobbyInteractable nearest = null;
            float best = float.MaxValue;
            if (!menuOpen && local != null)
            {
                foreach (var item in interactables)
                {
                    if (item == null || !item.isActiveAndEnabled) continue;
                    float d = item.HorizontalDistance(local.transform.position);
                    if (d <= item.Reach && d < best)
                    {
                        best = d;
                        nearest = item;
                    }
                }
            }

            if (nearest == null)
            {
                SetPrompt("", true);
                return;
            }
            string text = nearest.Prompt(out bool usable);
            SetPrompt(text, usable);
            if (usable && local.TryGetComponent(out PlayerInputReader reader) && reader.Current.InteractTapped) nearest.Use();
        }

        void SetPrompt(string text, bool usable)
        {
            if (prompt == null) return;
            if (prompt.text != text) prompt.text = text;
            prompt.color = usable ? Usable : Blocked;
        }

        // ── 도입(커튼이 열린 뒤 첫 공연) ─────────────────────

        void UpdateIntro()
        {
            var s = campaign.State;
            if (s.Phase != CampaignPhase.Intro)
            {
                introTimer = 0f;
                introEnded = false;
                return;
            }
            introTimer += Time.deltaTime;
            if (!introEnded && campaign.HasAuthority && introTimer >= introSeconds)
                introEnded = campaign.EndIntro();
        }

        void UpdateCurtain()
        {
            float target = campaign.State.Phase == CampaignPhase.Intro ? 1f : 0f;
            float speed = introSeconds > 0f ? 1f / introSeconds : 10f;
            curtain = Mathf.MoveTowards(curtain, target, speed * Time.deltaTime);
            float eased = Mathf.SmoothStep(0f, 1f, curtain);
            if (curtainLeft != null) curtainLeft.localPosition = leftHome + Vector3.left * (curtainTravel * eased);
            if (curtainRight != null) curtainRight.localPosition = rightHome + Vector3.right * (curtainTravel * eased);
        }

        // ── HUD ─────────────────────────────────────────────

        void RefreshHud()
        {
            bool connected = session.ConnectionState == SessionConnectionState.Host || session.ConnectionState == SessionConnectionState.Client;
            bool code = session.ActiveConnector is ServicesSessionConnector;
            string key = string.IsNullOrEmpty(session.JoinKey) ? "-" : session.JoinKey;
            Set(roomCode, connected ? $"{(code ? "방 코드" : "LAN 주소")}  <b><color=#F2CC66>{key}</color></b>" : "연결 중...");
            Set(players, $"{(session.IsHost ? "방장" : "단원")}  ·  {session.PlayerCount} / {session.RequiredPlayers}명");
            if (copyCode != null) copyCode.gameObject.SetActive(connected);
            if (leaveLabel != null) Set(leaveLabel, session.IsHost ? "방 닫기" : "방 나가기");

            var s = campaign.State;
            string title = "", sub = "";
            if (s.Phase == CampaignPhase.Intro)
            {
                title = s.IsGenerationChangeIntro ? "새 세대가 무대에 섭니다" : "막이 오릅니다";
                sub = s.IsGenerationChangeIntro ? $"세대 {s.Generation}  ·  1일차" : "첫 공연을 준비하세요";
            }
            else if (s.Phase == CampaignPhase.Ending)
            {
                title = "커튼 콜!";
                sub = session.IsHost ? "탈출 성공  ·  커튼 레버를 올리면 다시 준비해요" : "탈출 성공  ·  방장이 다시 준비할 때까지 기다려요";
            }
            else if (session.GameState == GameSessionState.Playing)
            {
                title = "공연 준비 중";
            }
            Set(banner, title);
            Set(bannerSub, sub);
            if (bannerBack != null && bannerBack.activeSelf != (title.Length > 0)) bannerBack.SetActive(title.Length > 0);
        }

        static void Set(TMP_Text label, string text)
        {
            if (label != null && label.text != text) label.text = text;
        }

        void CopyCode()
        {
            if (!string.IsNullOrEmpty(session.JoinKey)) GUIUtility.systemCopyBuffer = session.JoinKey;
        }

        // ── ESC 메뉴 ────────────────────────────────────────

        void SetMenu(bool open)
        {
            menuOpen = open;
            if (menu != null) menu.SetActive(open);
            if (!open && settings != null) settings.gameObject.SetActive(false);
            InputGate.SetBlocking(this, open);
        }

        // ── 내 캐릭터 ────────────────────────────────────────

        /// <summary>
        /// 내 캐릭터를 자리 번호의 로비 출발 위치로 옮긴다(움직임은 내 화면이 정하므로 각자 자기 캐릭터만).
        /// 균형은 외줄 전용이라 로비에서는 끈다(함정 balance-on-outside-tightrope: 켜 두면 서 있기만 해도 쓰러진다).
        /// </summary>
        static void Place(NetworkPlayer player)
        {
            if (player == null || !player.IsOwner) return;
            if (player.TryGetComponent(out PlayerBalance balance)) balance.SetBalanceActive(false);
            if (player.Slot < 0 || !player.TryGetComponent(out PlayerMover mover)) return;
            PlayerSpawnPoints.GetPose(player.Slot, mover.LaneSpacing, out Vector3 position, out Quaternion rotation);
            mover.SetStartPose(position, rotation);
        }

        static void ShowCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
