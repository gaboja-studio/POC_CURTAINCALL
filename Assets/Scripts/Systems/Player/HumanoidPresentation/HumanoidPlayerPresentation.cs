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
            mover.Teleported += ResetTravelMotion;
            NetworkPlayer.Restarted += Restarted;
            NetworkPlayer.Revived += Restarted;
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
            itemLayerState = null;
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
            // 혈흔 표시가 꺼져 있으면(게임 설정 또는 이 PC 설정) 부상과 상관없이 처음 의상을 유지한다. 매 프레임 다시 맞추므로 설정을 바꾸면 바로 반영된다.
            if (!CurtainCall.Damage.InjuryBloodEffects.IsVisible)
            {
                surface.SetState(CurtainCall.CharacterArt.RabbitSurfaceStates.SurfaceState.Clean);
                return;
            }
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
            ResetTravelMotion();
            ClearItemVisual();
            if (model != null) model.ClearPieces();
            if (detached != null) detached.ClearPieces();
        }

        void ResetTravelMotion()
        {
            RestoreHangAlignment();
            IsHanging = false;
            CancelItemAction();
            ResetCircusMotion();
            ResetRescueMotion();
            airborne = false;
            landUntil = 0f;
            presentationSampleReady = false;
            presentationVelocity = Vector3.zero;
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
            bool canUseItemHands = CanUseItemHands && !condition.AllLimbsLost;
            if (!canUseItemHands || model == null)
            {
                CancelItemAction();
                shownItem = null;
            }
            if (model == null) return;
            Color next = GameSettings.Base.Character.GetPlayerColor(network != null && network.AppearanceOutfit >= 0 ? network.AppearanceOutfit : network != null ? network.Slot : 0);
            if (color != next) { color = next; model.SetColor(color); }
            model.Animator.SetLayerWeight(1, 0f);
            ClearItemLayers(model.Animator);
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
            bool moving = network != null ? network.PresentationMoving : mover.IsMoving;
            bool sprinting = network != null ? network.PresentationSprinting : mover.IsSprinting;
            JumpKind jump = network != null ? network.CurrentJump : mover.CurrentJump;
            var stack = PiggybackSystem.Current;
            if (stack != null && stack.GetBelow(network) != null && !PiggybackSystem.UsingDevice(network)
                && stack.TryGetComponent<PiggybackFollow>(out var follow))
                jump = follow.PresentationJump(network);
            // Remote movement is not simulated here. Its old local mover state must not
            // override the grounded state sent by its owner.
            bool inAir = jump != JumpKind.None || ((network == null || !network.IsSpawned || network.IsOwner) && mover.IsAirborne);
            if (PresentCircusMotion(inAir, moving)) return;
            bool carrying = network != null && network.IsSpawned
                && HumanCannonCarry.Held(network.OwnerClientId) != null;
            // Jump/landing/legless returns still need the grip layer. Hands are solved after animation.
            model.Animator.SetLayerWeight(1, carrying ? 1f : 0f);
            PresentCarryLayer(carrying);
            if (canUseItemHands && PresentItemMotion(moving, inAir)) return;
            if (inAir && !airborne) jumpSince = Time.time;
            if (!inAir && airborne) landUntil = Time.time + .15f;
            airborne = inAir;
            if (CarryingTrampoline(out _))
            {
                if (inAir) { model.Play(CircusVariant("CarryJump")); return; }
                if (Time.time < landUntil) { model.Play(CircusVariant("FlightLand")); return; }
                if (condition.LostLegCount == 2 || (condition.LostLegCount == 1 && !moving))
                { model.Play(CircusVariant("TrampolineCarry")); return; }
            }
            if (condition.LostLegCount == 2)
            {
                // The crawl is a ground pose. Reuse the existing injury-aware air
                // and impact clips so surviving arms still react during a jump.
                if (inAir)
                {
                    model.Play(CircusVariant(presentationVelocity.y > 0f ? "TrampolineRise" : "PanicFall"));
                    return;
                }
                if (Time.time < landUntil) { model.Play(CircusVariant("FlightLand")); return; }
                string crawl = CurtainCall.ItemArt.ItemVisualAssets.State("ItemCrawl",model.Stage);
                if (moving && !inAir) PlayLocomotion(crawl);
                else model.PlayLocomotion(crawl, 0f, immediate: true);
                return;
            }
            if (inAir)
            {
                model.Play(jump == JumpKind.Fall ? "Fall" : Time.time - jumpSince < .12f ? "JumpStart" : "JumpLoop");
                return;
            }
            if (Time.time < landUntil) { model.Play("JumpLand"); return; }
            if (mover.IsCrouched)
            { if (moving) PlayLocomotion("CrouchWalk"); else model.Play("Crouch"); return; }
            if (moving) PlayLocomotion(balance != null && balance.IsActive ? "Tightrope" : sprinting ? "Run" : "Walk");
            else model.Play("Idle");
        }

        /// <summary>막혀서 못 움직일 때 이동 동작의 최소 재생 배율(1 = 평소 속도).</summary>
        const float BlockedLocomotionRate = .8f;
        void PlayLocomotion(string motion)
        {
            var velocity = Vector3.ProjectOnPlane(presentationVelocity, Vector3.up);
            float speed = velocity.magnitude;
            if (balance != null && balance.IsActive && Vector3.Dot(velocity, transform.forward) < 0f) speed = -speed;
            float rate = speed / GameSettings.Base.Presentation.LocomotionAnimationSpeed(motion);
            // 벽에 막혀 실제로는 못 나아가도 이동 입력 중이면 걷기·달리기 동작이 멈춘 자세로 굳지 않게 한다(2026-11-14 사용자).
            // 외줄 균형 중에는 속도·방향을 그대로 따른다(뒤로 걷기 포함).
            if (!(balance != null && balance.IsActive)) rate = Mathf.Max(rate, BlockedLocomotionRate);
            model.PlayLocomotion(motion, rate);
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
            // 모든 봉은 현재 반동 각도와 실제 탑승 순서의 공통 위치로 손을 맞춘다.
            // 옛 외줄 어댑터의 중립 봉 위치/고정 높이를 먼저 쓰면 몸 외형만 판정 위치에서 이탈한다.
            var poles = HumanCannonPoleSystem.Current;
            int index = poles != null ? poles.HeldBy(network.OwnerClientId) : -1;
            if (index < 0) return false;
            var pole = poles.Pole(index);
            if (pole == null) return false;
            grip = poles.HangingPosition(index, network.OwnerClientId) + Vector3.up * pole.HangLength;
            axis = poles.RiderRotation(index, network.OwnerClientId) * Vector3.right;
            return true;
        }

        void LateUpdate()
        {
            ApplyItemAlignment();
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
                    // 몸은 줄 방향을 보고 있어 손 방향이 줄과 거의 수직이다. 손 방향 부호로 돌 쪽을 고르면 흔들림마다
                    // 좌우가 뒤집힌다(2026-11-01 버그). 바라볼 쪽을 고정 규칙으로 정한다: 코스 가운데를 본다.
                    axis = RopeHangHandAxis(ropeAxis, transform.position, transform.forward);
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

        /// <summary>
        /// 줄 매달림의 손 방향(왼손 → 오른손). 몸은 줄에 수직으로, 코스 가운데 쪽을 바라본다
        /// (왼쪽 줄은 오른쪽, 오른쪽 줄은 왼쪽. 가운데 줄·코스 없음은 줄 왼쪽 = 오른손이 줄 진행 방향).
        /// 위치만으로 정하므로 모든 화면에서 같고 흔들림·애니메이션에 따라 바뀌지 않는다.
        /// </summary>
        internal static Vector3 RopeHangHandAxis(Vector3 ropeAxis, Vector3 position, Vector3 bodyForward)
        {
            ropeAxis = Vector3.ProjectOnPlane(ropeAxis, Vector3.up).normalized;
            // 줄 축의 부호는 의미가 없다. 매달림 시스템이 줄 진행 방향으로 세운 몸 정면에 맞춘다.
            if (Vector3.Dot(ropeAxis, bodyForward) < 0f) ropeAxis = -ropeAxis;
            Vector3 right = Vector3.Cross(Vector3.up, ropeAxis); // 줄 진행 방향의 오른쪽
            var course = TightropeCourse.Current;
            // 줄 축의 부호와 상관없이 코스 오른쪽(GetSide +)과 같은 쪽을 오른쪽으로 쓴다.
            if (course != null && Vector3.Dot(right, Vector3.Cross(Vector3.up, course.Forward)) < 0f) right = -right;
            Vector3 facing = course != null && course.GetSide(position) < -.05f ? right : -right;
            return Vector3.Cross(Vector3.up, facing); // 바라보는 방향의 오른쪽 = 오른손 쪽
        }

        internal static Vector3 Palm(Animator animator, bool left)
        {
            var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (hand == null) return Vector3.zero;
            var finger = animator.GetBoneTransform(left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            return finger != null ? Vector3.Lerp(hand.position, finger.position, .7f) : hand.position;
        }

        internal static void AlignArm(Animator animator, bool left, Vector3 contact)
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
            ClearItemVisual();
            ResetTravelMotion();
            if (slot != null) slot.ModelChanged -= BindModel;
            if (condition != null) condition.Changed -= Changed;
            if (mover != null) mover.Teleported -= ResetTravelMotion;
            NetworkPlayer.Restarted -= Restarted;
            NetworkPlayer.Revived -= Restarted;
            if (model != null) model.ClearPieces();
            if (detached != null) detached.ClearPieces();
            model = null;
            detached = null;
        }
    }
}
