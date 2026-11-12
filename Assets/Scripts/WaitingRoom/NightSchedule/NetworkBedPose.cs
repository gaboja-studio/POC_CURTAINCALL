using CurtainCall.Campaign;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;
namespace CurtainCall.WaitingRoom
{
    // A replicated visual pose only; CharacterController, damage and campaign authority are untouched.
    [DefaultExecutionOrder(1000)]
    public sealed class NetworkBedPose : MonoBehaviour
    {
        NetworkPlayer player;
        Transform visual;
        Vector3 originalPosition;
        Quaternion originalRotation;
        Animator animator;
        bool animatorEnabled, posed;
        public bool IsLying => posed;
        void Awake() => player = GetComponent<NetworkPlayer>();
        void LateUpdate()
        {
            var run = CampaignRun.Instance;
            bool sleeping = run != null && (run.State.Phase == CampaignPhase.Night || run.State.Phase == CampaignPhase.Dawn)
                && !WaitingRoomRehearsal.IsActive && !WaitingRoomCannonRehearsal.IsActive;
            int id = player != null ? player.RestingBed : -1;
            if (!sleeping || id < 0 || player.State != PlayerState.Normal) { Restore(); return; }
            var beds = NightBed.AllBeds();
            if (id >= beds.Length) { Restore(); return; }
            var model = GetComponent<PlayerModelSlot>();
            if (model == null || model.CurrentModel == null) { Restore(); return; }
            var slot = model.Slot;
            var nextAnimator = model.CurrentModel.GetComponentInChildren<Animator>();
            if (visual != slot || animator != nextAnimator) Restore();
            if (!posed)
            {
                visual = slot;
                originalPosition = visual.localPosition; originalRotation = visual.localRotation;
                animator = nextAnimator;
                animatorEnabled = animator != null && animator.enabled;
                posed = true;
            }
            if (animator != null) animator.enabled = false;
            var bed = beds[id].Footprint;
            Vector3 axis = bed.size.x > bed.size.z ? Vector3.right : Vector3.forward;
            var target = bed.center - axis * .75f; target.y = bed.min.y + .58f;
            visual.SetPositionAndRotation(target, Quaternion.LookRotation(-axis, Vector3.up) * Quaternion.Euler(-90, 0, 0));
        }
        void Restore()
        {
            if (!posed) return;
            if (visual != null)
            {
                visual.localPosition = originalPosition; visual.localRotation = originalRotation;
                // The old model may already belong to a ragdoll instead of this slot.
                if (animator != null && animator.transform.IsChildOf(visual)) animator.enabled = animatorEnabled;
            }
            animator = null;
            visual = null;
            posed = false;
        }
        void OnDisable() => Restore();
    }
}
