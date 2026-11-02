using CurtainCall.Player;
using CurtainCall.Network.PlayerSync;
using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    [DefaultExecutionOrder(1000)]
    public sealed class PursuitVictimPose : MonoBehaviour
    {
        public float Elapsed;
        public bool Hidden;
        public bool IsReady => visual != null;
        public Vector3 HeadDirection => Vector3.ProjectOnPlane(upright * Vector3.back, Vector3.up).normalized;
        public Vector3 AttackerPosition => new Vector3(hipPoint.x, anchor.y, hipPoint.z) - HeadDirection * .12f;
        public Vector3 BloodPosition => chestBone != null ? chestBone.position : hipPoint + HeadDirection * .3f;
        public bool IsLocalVictim => local;
        Vector3 anchor, hipPoint, hipLocal;
        Quaternion upright;
        Transform chestBone;
        bool captureFrameSet;
        PursuitCaptureBlood blood;
        public void SetCaptureFrame(Vector3 worldAnchor, Quaternion worldUpright)
        { anchor = worldAnchor; upright = worldUpright; captureFrameSet = true; }
        PlayerMover mover;
        HumanoidModel model;
        Transform visual;
        Vector3 position;
        Quaternion rotation;
        bool priorControl, priorAnimator;
        Renderer[] renderers;
        bool[] forceRenderingOff;
        bool local;
        GameObject overlay;
        UnityEngine.UI.Image shade;
        readonly Dictionary<Transform, Quaternion> struggle = new();
        void Start()
        {
            mover = GetComponent<PlayerMover>();
            var network = GetComponent<NetworkPlayer>();
            local = network == null || !network.IsSpawned || network.IsOwner;
            if (local) {
                InputGate.SetBlocking(this, true);
                overlay = new GameObject("PursuitCaptureScreen", typeof(Canvas));
                var canvas = overlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 70;
                var image = new GameObject("CaptureShade", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                image.transform.SetParent(overlay.transform, false);
                var rect = image.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                shade = image.GetComponent<UnityEngine.UI.Image>(); shade.raycastTarget = false;
                WaitingRoomItemBrokenNotice.ShowMessage("포획", "마네킹에게 잡혔습니다. 다음 장소로 이동할 때까지 관전합니다.");
            }
            if (mover != null) { priorControl = mover.ControlEnabled; mover.SetControlEnabled(false); }
            model = GetComponent<HumanoidPlayerPresentation>()?.Model;
            if (model == null) model = GetComponentInChildren<HumanoidModel>();
            if (model != null) {
                visual = model.transform; position = visual.localPosition; rotation = visual.localRotation;
                if (!captureFrameSet) SetCaptureFrame(visual.position, visual.rotation);
                hipLocal = Vector3.up * .9f;
                if (model.Animator != null && model.Animator.isHuman) {
                    var hip = model.Animator.GetBoneTransform(HumanBodyBones.Hips);
                    if (hip != null) hipLocal = visual.InverseTransformPoint(hip.position);
                    chestBone = model.Animator.GetBoneTransform(HumanBodyBones.Chest);
                }
                if (model.Animator != null) { priorAnimator = model.Animator.enabled; model.Animator.enabled = false; }
                if (model.Animator != null && model.Animator.isHuman)
                    foreach (var id in new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg }) {
                        var b = model.Animator.GetBoneTransform(id); if (b != null) struggle[b] = b.localRotation;
                    }
            }
            blood = gameObject.AddComponent<PursuitCaptureBlood>(); blood.Pose = this;
        }
        void LateUpdate()
        {
            if (mover != null) mover.SetControlEnabled(false);
            if (local) {
                InputGate.SetBlocking(this, !Hidden);
                if (overlay != null) overlay.SetActive(!Hidden);
                if (shade != null) shade.color = new Color(.18f, 0, .015f, .12f + .17f * Mathf.Max(0, Mathf.Sin(Elapsed * 5)));
            }
            if (visual != null) {
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Elapsed / .75f));
                visual.rotation = upright * Quaternion.Euler(-88 * t, 0, Mathf.Sin(Elapsed * 17) * 5 * t);
                visual.position = anchor + Vector3.up * (.15f * t);
                hipPoint = anchor + Vector3.up * .15f + upright * Quaternion.Euler(-88, 0, 0) * Vector3.Scale(hipLocal, visual.lossyScale);
                // Irregular struggle on remaining limbs, independent of locomotion animation.
                int n = 0;
                foreach (var b in struggle) if (b.Key != null) b.Key.localRotation = b.Value * Quaternion.Euler(28 * Mathf.Sin(Elapsed * 12 + n++), 0, 0);
            }
            if (Hidden) {
                if (renderers == null) {
                    renderers = GetComponentsInChildren<Renderer>(true);
                    forceRenderingOff = new bool[renderers.Length];
                    for (int i = 0; i < renderers.Length; i++) forceRenderingOff[i] = renderers[i].forceRenderingOff;
                }
                // HumanoidModel owns enabled (which injury stage is visible). Capture
                // only suppresses drawing, so releasing it cannot restore pre-cut limbs.
                foreach (var r in renderers) if (r != null) r.forceRenderingOff = true;
            } else RestoreVisibility();
        }
        void RestoreVisibility()
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].forceRenderingOff = forceRenderingOff[i];
            renderers = null; forceRenderingOff = null;
        }
        void OnDestroy()
        {
            if (local) InputGate.SetBlocking(this, false);
            if (overlay != null) Destroy(overlay);
            if (blood != null) Destroy(blood);
            foreach (var b in struggle) if (b.Key != null) b.Key.localRotation = b.Value;
            if (mover != null) mover.SetControlEnabled(priorControl);
            if (visual != null) { visual.localPosition = position; visual.localRotation = rotation; }
            if (model != null && model.Animator != null) model.Animator.enabled = priorAnimator;
            // Cuts may have changed the required local height while this pose owned
            // the transform. Restore from current injury, never the pre-capture height.
            if (model != null && visual.parent != null && TryGetComponent<PlayerCondition>(out var condition))
                model.Apply(condition.LostParts, condition.LostParts, false, mover != null ? mover.Body : null);
            RestoreVisibility();
        }
    }
}
