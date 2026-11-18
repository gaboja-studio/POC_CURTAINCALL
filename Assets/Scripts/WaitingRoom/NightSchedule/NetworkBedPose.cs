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
                // 걷던 자세 그대로 굳지 않게 기본(서 있기) 자세로 되돌린 뒤 멈춘다(2026-11-17 사용자: 자연스럽게 눕기).
                if (animator != null && animator.enabled && animator.runtimeAnimatorController != null) { animator.Rebind(); animator.Update(0f); }
                renderers = model.CurrentModel.GetComponentsInChildren<Renderer>();
                lift = null;
                posed = true;
            }
            if (animator != null) animator.enabled = false;
            var bed = beds[id].Footprint;
            Vector3 axis = bed.size.x > bed.size.z ? Vector3.right : Vector3.forward;
            var target = bed.center - axis * .75f; target.y = bed.min.y + MattressHeight;
            visual.SetPositionAndRotation(target, Quaternion.LookRotation(-axis, Vector3.up) * Quaternion.Euler(-90, 0, 0));
            // 몸이 매트리스에 묻히지 않게, 누운 모델의 가장 낮은 면을 매트리스 윗면에 맞춘다(체형마다 등 두께가 달라 실제 모습으로 잰다).
            // 스킨 메시의 bounds는 엉덩이 뼈 기준의 넉넉한 상자라 누우면 실제 등보다 한참 아래로 내려가 몸이 침대 위에 떴다(2026-11-18 사용자).
            // 몸(스킨 메시)의 실제 정점으로 처음 한 번 재고, 같은 높이를 계속 쓴다.
            lift ??= Lowest() is float lowest ? target.y + .02f - lowest : 0f;
            visual.position += Vector3.up * lift.Value;
        }
        float? lift;
        static Mesh baked;
        float? Lowest()
        {
            float lowest = float.MaxValue;
            foreach (var r in renderers)
            {
                if (r is not SkinnedMeshRenderer skin || !skin.enabled || !skin.gameObject.activeInHierarchy || skin.sharedMesh == null) continue;
                baked ??= new Mesh { name = "BedPoseBake", hideFlags = HideFlags.HideAndDontSave };
                skin.BakeMesh(baked, true);
                var t = skin.transform;
                foreach (var v in baked.vertices) lowest = Mathf.Min(lowest, (t.position + t.rotation * v).y);
            }
            // 스킨 메시가 없으면 예전처럼 표시 범위로 잰다.
            if (lowest == float.MaxValue)
                foreach (var r in renderers) if (r != null && r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)) lowest = Mathf.Min(lowest, r.bounds.min.y);
            return lowest < float.MaxValue ? lowest : null;
        }
        /// <summary>대기실 이층 침대(SM_Prop_Bed_Bunk_01) 아래 칸 매트리스 윗면 높이(침대 바닥 기준, 메시 실측 0.65m).</summary>
        const float MattressHeight = .65f;
        Renderer[] renderers = System.Array.Empty<Renderer>();
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
