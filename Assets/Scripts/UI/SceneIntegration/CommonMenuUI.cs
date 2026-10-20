using System;
using CurtainCall.Player;
using CurtainCall.StartLobby;
using CurtainCall.SpaceLink;
using CurtainCall.UI.PanelsV1;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CurtainCall.UI.SceneIntegration
{
    // ESC must see the pre-close state of Shop/Schedule before their Update and the camera.
    [DefaultExecutionOrder(-1000)]
    public sealed class CommonMenuUI : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/UI/CommonMenuUI.unity";
        static readonly string[] SpacePaths = {
            "Assets/Scenes/Tests/MainStage/Tightrope/Tightrope.unity",
            "Assets/Scenes/Tests/MainStage/HumanCannon/HumanCannon.unity",
            "Assets/Scenes/Tests/WaitingRoom/Base/WaitingRoomBase.unity",
            "Assets/Scenes/Start_Lobby/Lobby.unity", "Assets/Scenes/Start_Lobby/Start.unity" };
        public static CommonMenuUI Current { get; private set; }
        public static Scene CurrentScene
        {
            get { foreach (var path in SpacePaths) { var scene = SceneManager.GetSceneByPath(path); if (scene.isLoaded) return scene; } return default; }
        }
        public static bool HasSupportedSpace => CurrentScene.IsValid();
        public static bool IsStart => CurrentScene.name == "Start";
        [SerializeField] CurtainCallMenuPanels menu;
        [SerializeField] CommonSettingsUI settings;
        [SerializeField] GameObject backdrop;
        [SerializeField] TMP_Text leaveMessage;
        [SerializeField] TMP_Text footer;
        CurtainCallPanelView[] panels;
        int contextHandle;
        bool leaving;
        EventSystem ownedEvents;
        public CurtainCallMenuPanels Menu => menu;
        public CommonSettingsUI Settings => settings;
        public bool IsOpen => menu.IsMenuOpen || menu.IsConfirmationOpen || settings.IsOpen;
        bool Transitioning => SceneCurtainTransition.IsActive
            || (SpaceLinkDirector.Instance != null && SpaceLinkDirector.Instance.IsTransitioning)
            || (PerformanceEndingPresentation.Current != null && PerformanceEndingPresentation.Current.ResultVisible);
        public void Configure(CurtainCallMenuPanels view, CommonSettingsUI options, GameObject block, TMP_Text message, TMP_Text hint)
        { menu = view; settings = options; backdrop = block; leaveMessage = message; footer = hint; }
        void Awake()
        {
            Current = this; contextHandle = CurrentScene.handle;
            panels = menu.GetComponentsInChildren<CurtainCallPanelView>(true);
            InputGate.AddReleaseKeys(Key.Escape);
            menu.OnSettingsRequested.AddListener(OpenMenuSettings);
            menu.OnLeaveConfirmed.AddListener(Leave);
            backdrop.SetActive(false);
        }
        void Update()
        {
            EnsureEvents();
            if (CurrentScene.handle != contextHandle || Transitioning || !HasSupportedSpace)
            {
                if (IsOpen) CloseAll();
                contextHandle = CurrentScene.handle;
            }
            bool cancel = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            cancel |= Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
            if (cancel && !Transitioning && !leaving)
            {
                if (settings.IsOpen) settings.CancelTop();
                else if (menu.IsMenuOpen || menu.IsConfirmationOpen) menu.Cancel();
                else if (!IsStart && HasSupportedSpace && !InputGate.IsBlocked) OpenMenu();
            }
            SyncInput();
        }
        void EnsureEvents()
        {
            if (!HasSupportedSpace) return;
            EventSystem external = null;
            foreach (var events in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (events != ownedEvents && events.isActiveAndEnabled) { external = events; break; }
            if (external != null)
            {
                if (ownedEvents != null)
                {
                    ownedEvents.gameObject.SetActive(false);
                    Destroy(ownedEvents.gameObject); ownedEvents = null;
                    EventSystem.current = external;
                }
                return;
            }
            if (ownedEvents != null) return;
            var root = new GameObject("CommonUiEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            root.transform.SetParent(transform, false);
            ownedEvents = root.GetComponent<EventSystem>();
        }
        void LateUpdate() => SyncInput();
        void SyncInput()
        {
            bool owns = IsOpen || leaving;
            if (!owns) foreach (var panel in panels) owns |= panel.IsAnimating;
            InputGate.SetBlocking(this, owns);
            backdrop.SetActive(owns);
            if (owns) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        public void OpenMenu()
        {
            if (IsStart || !HasSupportedSpace || Transitioning || leaving || InputGate.IsBlocked) return;
            var network = NetworkManager.Singleton;
            bool host = network != null && network.IsServer;
            leaveMessage.text = host ? "방장이 나가면 방이 종료되고\n모든 단원이 시작 화면으로 돌아갑니다." :
                CommonUiSceneLoader.CurrentSpace != null ? "진행 중인 공연에서 이탈하고\n시작 화면으로 돌아갑니다." : "방에서 나가고\n시작 화면으로 돌아갑니다.";
            footer.text = CommonUiSceneLoader.CurrentSpace != null ? "공연은 계속 진행 중입니다" : "게임은 계속 진행 중입니다";
            menu.Show(); SyncInput();
        }
        public void OpenStartSettings(Action closed)
        {
            if (!IsStart || Transitioning) { closed?.Invoke(); return; }
            settings.Open(false, () => { closed?.Invoke(); SyncInput(); }); SyncInput();
        }
        void OpenMenuSettings()
        {
            menu.Hide();
            settings.Open(true, () => { if (!Transitioning && !leaving) menu.Show(); SyncInput(); });
            SyncInput();
        }
        void Leave()
        {
            if (leaving) return;
            leaving = true; settings.Close(); SyncInput();
            StartLobbyFlow.EnsureExists().LeaveToStart();
        }
        public void CloseAll()
        { settings.Close(); menu.Hide(); SyncInput(); }
        void OnDisable()
        {
            if (settings != null) settings.Close();
            InputGate.SetBlocking(this, false);
            if (Current == this) Current = null;
        }
    }
}
