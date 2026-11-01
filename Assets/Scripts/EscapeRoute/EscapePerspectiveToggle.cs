using CurtainCall.Cameras;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.HumanCannon;
using CurtainCall.Tightrope;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute
{
    /// <summary>Local escape-route view preference. Stunt ownership and movement remain unchanged.</summary>
    [DefaultExecutionOrder(-90)]
    public sealed class EscapePerspectiveToggle : MonoBehaviour
    {
        [SerializeField] CameraViewPreset firstPersonView;
        CameraViewDirector director;
        EscapeCampaignContent content;
        CameraViewPreset ownedPreset;
        public static EscapePerspectiveToggle Current { get; private set; }
        public bool FirstPersonSelected { get; private set; }
        public static bool ReservesNumberKeys => Current != null && Current.InEscapeScene;
        public bool InEscapeScene => isActiveAndEnabled && gameObject.scene == SceneManager.GetActiveScene()
            && content != null && content.IsActive;

        void Awake() => content = GetComponent<EscapeCampaignContent>();
        void OnEnable()
        {
            Current = this;
            InputGate.AddReleaseKeys(Key.Digit2);
        }
        void Update()
        {
            BindDirector();
            var keys = Keyboard.current;
            if (Application.isFocused && keys != null && keys.digit2Key.wasPressedThisFrame) TryToggleView();
            RefreshRequest();
        }
        public bool TryToggleView()
        {
            var player = NetworkPlayer.Local;
            if (!InEscapeScene || InputGate.IsBlocked || SceneCurtainTransition.IsActive
                || player == null || !player.IsSpawned || player.State != PlayerState.Normal
                || director == null || !director.isActiveAndEnabled) return false;
            FirstPersonSelected = !FirstPersonSelected;
            RefreshRequest();
            return true;
        }
        void BindDirector()
        {
            if (director == CameraViewDirector.Current) return;
            ReleaseDirector();
            director = CameraViewDirector.Current;
            if (director != null) director.PreparingView += RefreshRequest;
        }
        void RefreshRequest()
        {
            if (director == null) return;
            var player = NetworkPlayer.Local;
            if (!InEscapeScene || !FirstPersonSelected || player == null || !player.IsSpawned
                || player.State != PlayerState.Normal || director.LocalPlayer != player || UsesStuntView(player))
            { director.ReleaseView(this); return; }
            if (ownedPreset == null)
            {
                var source = firstPersonView != null ? firstPersonView : director.Settings?.FirstPerson;
                if (source == null) return;
                ownedPreset = Instantiate(source);
                ownedPreset.hideFlags = HideFlags.DontSave;
                ownedPreset.TransitionDuration = .12f;
            }
            // Ordinary movement/flight are first person. Rope, boarding and carrying keep their authored view.
            director.RequestView(this, ownedPreset, 300);
        }
        static bool UsesStuntView(NetworkPlayer player)
        {
            if (player.TryGetComponent(out PlayerController controller) && controller.Scheme is TightropeControlScheme) return true;
            var hangs = TightropeHangSystem.Current;
            if (hangs != null)
            {
                if (hangs.IsHanging(player)) return true;
                foreach (var hang in hangs.Hangs)
                    if (hang.IsPulling && (hang.Id == player.OwnerClientId || hang.Rescuer == player.OwnerClientId)) return true;
            }
            if (TightropeTrapeze.Current != null && TightropeTrapeze.Current.IsCrossing(player)) return true;
            return HumanCannonCarry.Held(player.OwnerClientId) != null
                || HumanCannonSystem.Current != null && HumanCannonSystem.Current.IsSeated(player.OwnerClientId);
        }
        void ReleaseDirector()
        {
            if (director != null) { director.PreparingView -= RefreshRequest; director.ReleaseView(this); }
            director = null;
        }
        void OnDisable()
        {
            ReleaseDirector(); FirstPersonSelected = false;
            if (Current == this) Current = null;
        }
        void OnDestroy() { if (ownedPreset != null) Destroy(ownedPreset); }
    }
}
