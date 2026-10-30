using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Settings;
using CurtainCall.Tightrope;
using CurtainCall.WaitingRoom;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>실제 Player 상태를 외형에 전달한다. 부상/이동/공연 판정은 소유하지 않는다.</summary>
    [DefaultExecutionOrder(650)]
    public sealed partial class HumanoidPlayerPresentation : MonoBehaviour
    {
        PlayerModelSlot slot;
        PlayerCondition condition;
        PlayerMover mover;
        PlayerBalance balance;
        NetworkPlayer network;
        HumanoidModel model;
        HumanoidModel detached;
        bool airborne;
        float jumpSince;
        float landUntil;
        Color color;
        public HumanoidModel Model => model;
        public bool IsHanging { get; private set; }
        [SerializeField, Tooltip("봉 매달림 손 접점의 높이 보정(m). 줄 매달림에는 적용하지 않는다.")]
        float poleGripHeightOffset = -.06f;
        Transform alignedModel;
        Vector3 alignmentOffset;
        Quaternion alignmentRotation;
        bool rotationAligned;

        void RestoreHangAlignment()
        {
            // 분리된 래그돌은 건드리지 않고, 슬롯 아래 외형의 지난 프레임 보정만 해제한다.
            if (alignedModel != null && slot != null && slot.CurrentModel == alignedModel.gameObject)
            {
                alignedModel.localPosition -= alignmentOffset;
                if (rotationAligned) alignedModel.localRotation = alignmentRotation;
            }
            alignedModel = null;
            alignmentOffset = Vector3.zero;
            rotationAligned = false;
        }

        void OnEnable()
        {
            slot = GetComponent<PlayerModelSlot>();
            condition = GetComponent<PlayerCondition>();
            mover = GetComponent<PlayerMover>();
            balance = GetComponent<PlayerBalance>();
            network = GetComponent<NetworkPlayer>();
            slot.ModelChanged += BindModel;
            condition.Changed += Changed;
            NetworkPlayer.Restarted += Restarted;
            BindModel();
        }

        void BindModel()
        {
            var next = slot.CurrentModel != null ? slot.CurrentModel.GetComponent<HumanoidModel>() : null;
            if (model == next) return;
            if (model != null)
            {
                if (next != null) model.ClearPieces();
                model.StopMotion();
                detached = model;
            }
            model = next;
            carryMotion = null;
            ResetRescueMotion();
            airborne = false;
            landUntil = 0;
            if (model == null) return;
            model.Animator.enabled = true;
            color = GameSettings.Base.Character.GetPlayerColor(network != null && network.AppearanceOutfit >= 0 ? network.AppearanceOutfit : network != null ? network.Slot : 0);
            model.SetColor(color);
            ApplySurface(model);
            model.Apply(condition.LostParts, condition.LostParts, false, mover.Body);
        }

        void Changed(BodyPart previous, BodyPart current)
        {
            RestoreHangAlignment();
            BindModel();
            // 사망 이벤트가 먼저 모델을 분리해도 마지막 사지는 현재 포즈에서 분리한다.
            var target = model != null ? model : detached;
            if (target != null)
            {
                ApplySurface(target);
                target.Apply(previous, current, network == null || !network.IsApplyingInitialCondition, mover.Body);
            }
        }

        void ApplySurface(HumanoidModel target)
        {
            if (target == null || !target.TryGetComponent<CurtainCall.CharacterArt.RabbitSurfaceStates>(out var surface)) return;
            surface.SetVariant(network != null && network.AppearanceOutfit >= 0 ? network.AppearanceOutfit : network != null ? network.Slot : 0);
            bool dead = condition.AllLimbsLost || (network != null &&
                (network.State == PlayerState.Fallen || network.State == PlayerState.Retired));
            surface.SetState(dead || condition.LostCount >= 3
                ? CurtainCall.CharacterArt.RabbitSurfaceStates.SurfaceState.Heavy
                : condition.LostCount > 0 ? CurtainCall.CharacterArt.RabbitSurfaceStates.SurfaceState.Light
                : CurtainCall.CharacterArt.RabbitSurfaceStates.SurfaceState.Clean);
        }

        void Restarted(NetworkPlayer player)
        {
            if (player.gameObject != gameObject) return;
            if (model != null) model.ClearPieces();
            if (detached != null) detached.ClearPieces();
        }

        void Update()
        {
            RestoreHangAlignment();
            IsHanging = false;
            SampleFlightVelocity();
            var catalog = CurtainCall.CharacterArt.PlayerAppearanceCatalog.Current;
            if (network != null && network.IsSpawned && catalog != null && catalog.Valid(network.AppearanceModel, network.AppearanceOutfit))
            {
                var desired = catalog.PrefabFor(network.AppearanceModel, network.AppearanceOutfit);
                if (slot.CurrentModel != null && slot.CurrentPrefab != desired) slot.SetModel(desired);
            }
            BindModel();
            ApplySurface(model != null ? model : detached);
            if (model == null) return;
            Color next = GameSettings.Base.Character.GetPlayerColor(network != null && network.AppearanceOutfit >= 0 ? network.AppearanceOutfit : network != null ? network.Slot : 0);
            if (color != next) { color = next; model.SetColor(color); }
            model.Animator.SetLayerWeight(1, 0f);
            if (condition.AllLimbsLost) { ResetCircusMotion(); model.Play("Death"); return; }
            if (PresentRopeRescue()) return;
            if (TryHangGrip(out _, out _))
            {
                IsHanging = true;
                ResetCircusMotion();
                airborne = false;
                landUntil = 0f;
                model.Play(TightropeHangSystem.Current != null && TightropeHangSystem.Current.IsHanging(network)
                    ? CircusVariant("RopeHang") : "Hanging");
                return;
            }
            if (balance != null && balance.HasFallen) { ResetCircusMotion(); return; }
            if (PresentCircusMotion()) return;
            bool moving = network != null ? network.PresentationMoving : mover.IsMoving;
            bool sprinting = network != null ? network.PresentationSprinting : mover.IsSprinting;
            JumpKind jump = network != null && network.IsSpawned && !network.IsOwner ? network.CurrentJump : mover.CurrentJump;
            bool inAir = jump != JumpKind.None || mover.IsAirborne;
            bool carrying = network != null && network.IsSpawned
                && ((PropCarry.Instance != null && PropCarry.Instance.Held(network.OwnerClientId).Count > 0)
                    || HumanCannonCarry.Held(network.OwnerClientId) != null);
            // Jump/landing/legless returns still need the grip layer. Hands are solved after animation.
            model.Animator.SetLayerWeight(1, carrying ? 1f : 0f);
            PresentCarryLayer(carrying);
            if (inAir && !airborne) jumpSince = Time.time;
            if (!inAir && airborne) landUntil = Time.time + .15f;
            airborne = inAir;
            if (CarryingTrampoline(out _))
            {
                if (inAir) { model.Play(CircusVariant("CarryJump")); return; }
                if (Time.time < landUntil) { model.Play(CircusVariant("FlightLand")); return; }
                if (condition.LostLegCount == 2) { model.Play(CircusVariant("TrampolineCarry")); return; }
            }
            if (condition.LostLegCount == 2)
            {
                model.Play("Crawl", moving && !inAir ? 1f : .0001f);
                return;
            }
            if (inAir)
            {
                model.Play(jump == JumpKind.Fall ? "Fall" : Time.time - jumpSince < .12f ? "JumpStart" : "JumpLoop");
                return;
            }
            if (Time.time < landUntil) { model.Play("JumpLand"); return; }
            if (mover.IsCrouched) { model.Play(moving ? "CrouchWalk" : "Crouch"); return; }
            model.Play(moving ? balance != null && balance.IsActive ? "Tightrope" : sprinting ? "Run" : "Walk" : "Idle");
        }

        bool TryHangGrip(out Vector3 grip, out Vector3 axis)
        {
            grip = default;
            axis = transform.right;
            if (network == null || network.State != PlayerState.Normal) return false;
            var hangs = TightropeHangSystem.Current;
            if (hangs != null && hangs.IsHanging(network))
            {
                axis = hangs.transform.forward;
                foreach (var hang in hangs.Hangs)
                    if (hang.Id == network.OwnerClientId) { grip = hangs.GripPoint(hang); return true; }
                // 소유자 예측 매달림은 첫 호스트 스냅샷 전에도 같은 줄 높이를 사용한다.
                grip = transform.position + Vector3.up * GameSettings.Tightrope.Hang.HangDrop;
                return true;
            }
            var trapeze = TightropeTrapeze.Current;
            if (trapeze != null && trapeze.IsHeld(network)) { grip = trapeze.GripPoint(network); return true; }
            var swing = TightropeSwingPole.Current;
            if (swing != null && swing.IsHeld(network)) { grip = swing.GripPoint(network); return true; }
            var relay = TightropeRelayGate.Current;
            int index = relay != null ? relay.HeldPole(network) : -1;
            if (index >= 0) { grip = relay.PhysicalPole(index).BarPosition(0f); return true; }
            var poles = HumanCannonPoleSystem.Current;
            index = poles != null ? poles.HeldBy(network.OwnerClientId) : -1;
            if (index < 0) return false;
            var pole = poles.Pole(index);
            if (pole == null) return false;
            grip = poles.HangingPosition(index, network.OwnerClientId) + Vector3.up * pole.HangLength;
            axis = pole.Grip.right;
            return true;
        }

        void LateUpdate()
        {
            if (ApplyRescueAlignment()) return;
            ApplyCircusAlignment();
            if (!IsHanging || model == null || !model.Animator.isHuman || !TryHangGrip(out var grip, out var axis)) return;
            ApplyHangPose(grip, axis, TightropeHangSystem.Current != null && TightropeHangSystem.Current.IsHanging(network));
        }

        void ApplyHangPose(Vector3 grip, Vector3 axis, bool onRope)
        {
            var animator = model.Animator;
            var left = (condition.LostParts & BodyPart.LeftArm) == 0 ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            var right = (condition.LostParts & BodyPart.RightArm) == 0 ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            int count = (left != null ? 1 : 0) + (right != null ? 1 : 0);
            if (count == 0) return;
            var root = model.transform;
            axis = axis.sqrMagnitude > .0001f ? axis.normalized : transform.right;
            alignmentRotation = root.localRotation;
            rotationAligned = true;
            if (onRope)
            {
                // 양손을 잇는 방향을 줄과 나란히 돌려, 팔만 줄을 가로지르지 않게 한다.
                Vector3 handAxis = Vector3.ProjectOnPlane(count == 2
                    ? Palm(animator, false) - Palm(animator, true) : root.right, Vector3.up);
                Vector3 ropeAxis = Vector3.ProjectOnPlane(axis, Vector3.up);
                if (handAxis.sqrMagnitude > .0001f && ropeAxis.sqrMagnitude > .0001f)
                {
                    if (Vector3.Dot(handAxis, ropeAxis) < 0f) axis = -axis;
                    root.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(handAxis, axis, Vector3.up), Vector3.up) * root.rotation;
                }
            }
            else grip += Vector3.up * poleGripHeightOffset;
            if (onRope && TightropeCourse.Current != null)
                grip -= Vector3.up * (TightropeCourse.Current.Shape.RopeThickness * .5f);
            float strength = onRope ? GameSettings.Base.Presentation.RopeHangSwayAngle : GameSettings.Base.Presentation.PoleHangSwayAngle;
            root.rotation = Quaternion.AngleAxis(Mathf.Sin(Time.time * Mathf.PI * 1.6f) * strength, axis) * root.rotation;
            Vector3 leftPalm = Palm(animator, true), rightPalm = Palm(animator, false);
            Vector3 center = ((left != null ? leftPalm : Vector3.zero) + (right != null ? rightPalm : Vector3.zero)) / count;
            Vector3 before = root.localPosition;
            // 이동 루트·충돌체 대신 모델만 옮겨, 매 프레임 재생된 실제 손바닥을 봉/줄 높이에 맞춘다.
            // 팔을 끝까지 편 채 접점을 바꾸면 IK 목표가 도달 범위 밖이 된다. 몸을 조금 올려 팔꿈치 여유를 둔다.
            root.position += grip - center + Vector3.up * .08f;
            alignedModel = root;
            alignmentOffset = root.localPosition - before;
            axis = axis.sqrMagnitude > .0001f ? axis.normalized : transform.right;
            if (left != null) AlignArm(animator, true, grip - axis * (count == 2 ? .18f : 0f));
            if (right != null) AlignArm(animator, false, grip + axis * (count == 2 ? .18f : 0f));
        }

        static Vector3 Palm(Animator animator, bool left)
        {
            var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (hand == null) return Vector3.zero;
            var finger = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            return finger != null ? Vector3.Lerp(hand.position, finger.position, .7f) : hand.position;
        }

        static void AlignArm(Animator animator, bool left, Vector3 contact)
        {
            var upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (upper == null || lower == null || hand == null) return;
            // 두 관절 길이를 보존한다. 손목·손가락 회전은 원본 매달림 애니메이션을 유지한다.
            for (int iteration = 0; iteration < 2; iteration++)
            {
                Vector3 target = contact - (Palm(animator, left) - hand.position);
                Vector3 from = lower.position - upper.position;
                Vector3 toHand = hand.position - lower.position;
                float a = from.magnitude, b = toHand.magnitude;
                Vector3 direction = target - upper.position;
                float distance = direction.magnitude;
                if (a < .0001f || b < .0001f || distance < .0001f) return;
                direction /= distance;
                distance = Mathf.Clamp(distance, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
                Vector3 bend = Vector3.ProjectOnPlane(from, direction);
                if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(-animator.transform.forward, direction);
                if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(animator.transform.right, direction);
                float along = (a * a + distance * distance - b * b) / (2f * distance);
                Vector3 elbow = upper.position + direction * along + bend.normalized * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
                Quaternion wrist = hand.rotation;
                upper.rotation = Quaternion.FromToRotation(from, elbow - upper.position) * upper.rotation;
                lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation;
                hand.rotation = wrist;
            }
        }

        void OnDisable()
        {
            RestoreHangAlignment();
            IsHanging = false;
            ResetCircusMotion();
            ResetRescueMotion();
            if (slot != null) slot.ModelChanged -= BindModel;
            if (condition != null) condition.Changed -= Changed;
            NetworkPlayer.Restarted -= Restarted;
            if (model != null) model.ClearPieces();
            if (detached != null) detached.ClearPieces();
            model = null;
            detached = null;
        }
    }
}
